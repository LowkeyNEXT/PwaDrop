using System.Runtime.InteropServices;
using System.Text;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.DragHarness;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class VirtualFileDataObject :
    ComTypes.IDataObject,
    IDataObjectAsyncCapability,
    IDisposable
{
    private const uint GmemMoveableAndZeroInit = 0x0042;
    private const int DropFilesHeaderSize = 20;
    private readonly IReadOnlyList<VirtualTestFile> _files;
    private string? _temporaryDirectory;
    private IReadOnlyList<string>? _temporaryPaths;
    private bool _asyncMode = true;
    private bool _inOperation;

    internal VirtualFileDataObject(params VirtualTestFile[] files)
    {
        _files = files;
    }

    public void GetData(ref ComTypes.FORMATETC format, out ComTypes.STGMEDIUM medium)
    {
        if (format.cfFormat != NativeMethods.CfHDrop ||
            (format.tymed & ComTypes.TYMED.TYMED_HGLOBAL) == 0)
        {
            throw new COMException("Unsupported format.", unchecked((int)0x80040064));
        }

        if (!_inOperation)
        {
            throw new COMException("The delayed download has not started.", unchecked((int)0x8000000A));
        }

        var paths = MaterializeTemporaryFiles();
        medium = CreateFileDrop(paths);
    }

    public void GetDataHere(ref ComTypes.FORMATETC format, ref ComTypes.STGMEDIUM medium) =>
        throw new COMException("GetDataHere is not supported.", unchecked((int)0x80004001));

    public int QueryGetData(ref ComTypes.FORMATETC format) =>
        format.cfFormat == NativeMethods.CfHDrop &&
        (format.tymed & ComTypes.TYMED.TYMED_HGLOBAL) != 0
            ? 0
            : unchecked((int)0x80040064);

    public int GetCanonicalFormatEtc(ref ComTypes.FORMATETC formatIn, out ComTypes.FORMATETC formatOut)
    {
        formatOut = formatIn;
        formatOut.ptd = IntPtr.Zero;
        return unchecked((int)0x00040130);
    }

    public void SetData(ref ComTypes.FORMATETC formatIn, ref ComTypes.STGMEDIUM medium, bool release) =>
        throw new COMException("SetData is not supported.", unchecked((int)0x80004001));

    public ComTypes.IEnumFORMATETC EnumFormatEtc(ComTypes.DATADIR direction)
    {
        if (direction != ComTypes.DATADIR.DATADIR_GET)
        {
            throw new COMException("Only DATADIR_GET is supported.", unchecked((int)0x80004001));
        }

        return new FormatEnumerator(CreateFormat());
    }

    public int DAdvise(
        ref ComTypes.FORMATETC format,
        ComTypes.ADVF advf,
        ComTypes.IAdviseSink adviseSink,
        out int connection)
    {
        connection = 0;
        return unchecked((int)0x80040003);
    }

    public void DUnadvise(int connection) =>
        throw new COMException("Advisories are not supported.", unchecked((int)0x80040003));

    public int EnumDAdvise(out ComTypes.IEnumSTATDATA? enumAdvise)
    {
        enumAdvise = null;
        return unchecked((int)0x80040003);
    }

    public int SetAsyncMode(bool asyncMode)
    {
        _asyncMode = asyncMode;
        return 0;
    }

    public int GetAsyncMode(out bool asyncMode)
    {
        asyncMode = _asyncMode;
        return 0;
    }

    public int StartOperation(object? reserved)
    {
        if (!_asyncMode)
        {
            return unchecked((int)0x8000FFFF);
        }

        _inOperation = true;
        return 0;
    }

    public int InOperation(out bool inAsyncOperation)
    {
        inAsyncOperation = _inOperation;
        return 0;
    }

    public int EndOperation(int result, object? reserved, uint effects)
    {
        _inOperation = false;
        return 0;
    }

    public void Dispose()
    {
        if (_temporaryDirectory is not null)
        {
            try
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
            catch (IOException)
            {
                // The harness will retry on its next cleanup cycle.
            }
            catch (UnauthorizedAccessException)
            {
                // A destination may still have a test file open briefly.
            }
        }

        GC.SuppressFinalize(this);
    }

    private IReadOnlyList<string> MaterializeTemporaryFiles()
    {
        if (_temporaryPaths is not null)
        {
            return _temporaryPaths;
        }

        // Chromium starts the download from GetData after StartOperation. A short
        // pause makes the harness catch receivers that incorrectly request data
        // during DragEnter instead of waiting for Drop.
        Thread.Sleep(250);
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "PwaDrop.DragHarness",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);

        var paths = new List<string>(_files.Count);
        foreach (var file in _files)
        {
            var path = Path.Combine(_temporaryDirectory, Path.GetFileName(file.Name));
            File.WriteAllBytes(path, file.Contents);
            paths.Add(path);
        }

        _temporaryPaths = paths;
        return paths;
    }

    private static ComTypes.STGMEDIUM CreateFileDrop(IReadOnlyList<string> paths)
    {
        var pathList = string.Join('\0', paths) + "\0\0";
        var pathBytes = Encoding.Unicode.GetBytes(pathList);
        var totalSize = checked(DropFilesHeaderSize + pathBytes.Length);
        var global = NativeMethods.GlobalAlloc(GmemMoveableAndZeroInit, (UIntPtr)(uint)totalSize);
        if (global == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }

        var memory = NativeMethods.GlobalLock(global);
        if (memory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }

        try
        {
            Marshal.WriteInt32(memory, 0, DropFilesHeaderSize);
            Marshal.WriteInt32(memory, 16, 1);
            Marshal.Copy(pathBytes, 0, IntPtr.Add(memory, DropFilesHeaderSize), pathBytes.Length);
        }
        finally
        {
            NativeMethods.GlobalUnlock(global);
        }

        return new ComTypes.STGMEDIUM
        {
            tymed = ComTypes.TYMED.TYMED_HGLOBAL,
            unionmember = global,
            pUnkForRelease = null
        };
    }

    private static ComTypes.FORMATETC CreateFormat() => new()
    {
        cfFormat = NativeMethods.CfHDrop,
        dwAspect = ComTypes.DVASPECT.DVASPECT_CONTENT,
        lindex = -1,
        ptd = IntPtr.Zero,
        tymed = ComTypes.TYMED.TYMED_HGLOBAL
    };
}

internal sealed record VirtualTestFile(string Name, byte[] Contents);
