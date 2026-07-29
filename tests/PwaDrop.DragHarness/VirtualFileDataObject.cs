using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.DragHarness;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class VirtualFileDataObject : ComTypes.IDataObject
{
    private const uint GmemMoveableAndZeroInit = 0x0042;
    private const uint FileDescriptorFlags = 0x00000044;
    private readonly IReadOnlyList<VirtualTestFile> _files;
    private readonly short _descriptorFormat;
    private readonly short _contentsFormat;

    internal VirtualFileDataObject(params VirtualTestFile[] files)
    {
        _files = files;
        _descriptorFormat = unchecked((short)NativeMethods.RegisterClipboardFormat("FileGroupDescriptorW"));
        _contentsFormat = unchecked((short)NativeMethods.RegisterClipboardFormat("FileContents"));
    }

    public void GetData(ref ComTypes.FORMATETC format, out ComTypes.STGMEDIUM medium)
    {
        if (format.cfFormat == _descriptorFormat)
        {
            medium = CreateDescriptors();
            return;
        }

        if (format.cfFormat == _contentsFormat && format.lindex >= 0 && format.lindex < _files.Count)
        {
            var stream = new ManagedComStream(_files[format.lindex].Contents);
            medium = new ComTypes.STGMEDIUM
            {
                tymed = ComTypes.TYMED.TYMED_ISTREAM,
                unionmember = Marshal.GetComInterfaceForObject<ManagedComStream, ComTypes.IStream>(stream),
                pUnkForRelease = null
            };
            return;
        }

        throw new COMException("Unsupported format.", unchecked((int)0x80040064));
    }

    public void GetDataHere(ref ComTypes.FORMATETC format, ref ComTypes.STGMEDIUM medium) =>
        throw new COMException("GetDataHere is not supported.", unchecked((int)0x80004001));

    public int QueryGetData(ref ComTypes.FORMATETC format)
    {
        if (format.cfFormat == _descriptorFormat && (format.tymed & ComTypes.TYMED.TYMED_HGLOBAL) != 0)
        {
            return 0;
        }

        if (format.cfFormat == _contentsFormat &&
            format.lindex >= 0 &&
            format.lindex < _files.Count &&
            (format.tymed & ComTypes.TYMED.TYMED_ISTREAM) != 0)
        {
            return 0;
        }

        return unchecked((int)0x80040064);
    }

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

        return new FormatEnumerator(
            CreateFormat(_descriptorFormat, -1, ComTypes.TYMED.TYMED_HGLOBAL),
            CreateFormat(_contentsFormat, 0, ComTypes.TYMED.TYMED_ISTREAM));
    }

    public int DAdvise(ref ComTypes.FORMATETC format, ComTypes.ADVF advf, ComTypes.IAdviseSink adviseSink, out int connection)
    {
        connection = 0;
        return unchecked((int)0x80040003);
    }

    public void DUnadvise(int connection) => throw new COMException("Advisories are not supported.", unchecked((int)0x80040003));

    public int EnumDAdvise(out ComTypes.IEnumSTATDATA? enumAdvise)
    {
        enumAdvise = null;
        return unchecked((int)0x80040003);
    }

    private ComTypes.STGMEDIUM CreateDescriptors()
    {
        var descriptorSize = Marshal.SizeOf<FileDescriptorW>();
        var totalSize = sizeof(uint) + (_files.Count * descriptorSize);
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
            Marshal.WriteInt32(memory, _files.Count);
            for (var index = 0; index < _files.Count; index++)
            {
                var file = _files[index];
                var descriptor = new FileDescriptorW
                {
                    Flags = FileDescriptorFlags,
                    FileAttributes = 0x00000080,
                    LastWriteTime = DateTime.UtcNow.ToFileTimeUtc(),
                    FileSizeHigh = (uint)((ulong)file.Contents.LongLength >> 32),
                    FileSizeLow = (uint)file.Contents.LongLength,
                    FileName = file.Name
                };
                Marshal.StructureToPtr(descriptor, IntPtr.Add(memory, sizeof(uint) + (index * descriptorSize)), false);
            }
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

    private static ComTypes.FORMATETC CreateFormat(short format, int index, ComTypes.TYMED medium) => new()
    {
        cfFormat = format,
        dwAspect = ComTypes.DVASPECT.DVASPECT_CONTENT,
        lindex = index,
        ptd = IntPtr.Zero,
        tymed = medium
    };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileDescriptorW
    {
        internal uint Flags;
        internal Guid ClassId;
        internal SizeL Size;
        internal PointL Point;
        internal uint FileAttributes;
        internal long CreationTime;
        internal long LastAccessTime;
        internal long LastWriteTime;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        internal string FileName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SizeL
    {
        internal int Width;
        internal int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointL
    {
        internal int X;
        internal int Y;
    }
}

internal sealed record VirtualTestFile(string Name, byte[] Contents);

