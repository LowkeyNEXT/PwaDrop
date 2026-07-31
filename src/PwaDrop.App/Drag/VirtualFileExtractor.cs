using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using PwaDrop.Core;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal sealed class VirtualFileExtractor
{
    private static readonly TimeSpan OriginalDropUnwindDelay = TimeSpan.FromMilliseconds(150);
    private const uint FileDescriptorHasSize = 0x00000040;
    private const uint FileDescriptorHasWriteTime = 0x00000020;
    private readonly CacheManager _cache;
    private readonly short _descriptorFormat;
    private readonly short _contentsFormat;

    internal VirtualFileExtractor(CacheManager cache)
    {
        _cache = cache;
        _descriptorFormat = unchecked((short)NativeMethods.RegisterClipboardFormat("FileGroupDescriptorW"));
        _contentsFormat = unchecked((short)NativeMethods.RegisterClipboardFormat("FileContents"));
    }

    internal DragPayloadKind DetectPayload(ComTypes.IDataObject dataObject)
    {
        var descriptor = CreateFormat(_descriptorFormat, -1, NativeMethods.TymedHGlobal);
        if (dataObject.QueryGetData(ref descriptor) == 0)
        {
            return DragPayloadKind.VirtualFileDescriptors;
        }

        var fileDrop = CreateFormat(NativeMethods.CfHDrop, -1, NativeMethods.TymedHGlobal);
        if (dataObject.QueryGetData(ref fileDrop) != 0)
        {
            return DragPayloadKind.Unsupported;
        }

        var asyncOperation = GetAsyncCapability(dataObject);
        try
        {
            return asyncOperation is not null &&
                   asyncOperation.GetAsyncMode(out var asyncMode) == 0 &&
                   asyncMode
                ? DragPayloadKind.AsyncFileDrop
                : DragPayloadKind.Unsupported;
        }
        catch (COMException)
        {
            return DragPayloadKind.Unsupported;
        }
    }

    internal Task<ExtractionResult> ExtractAfterDropAsync(
        ComTypes.IDataObject dataObject,
        DragPayloadKind payloadKind)
    {
        if (payloadKind == DragPayloadKind.Unsupported)
        {
            throw new NotSupportedException("The drag did not contain a supported virtual-file payload.");
        }

        var asyncOperation = GetAsyncCapability(dataObject);
        if (payloadKind != DragPayloadKind.AsyncFileDrop)
        {
            // Non-async virtual-file sources do not promise that their data object
            // survives Drop, so preserve the legacy synchronous extraction path.
            return Task.FromResult(ExtractCore(dataObject, payloadKind, asyncOperation: null, asyncStarted: false));
        }

        if (asyncOperation is null ||
            asyncOperation.GetAsyncMode(out var asyncMode) != 0 ||
            !asyncMode)
        {
            throw new InvalidOperationException("The delayed file drop did not expose an asynchronous operation.");
        }

        var startResult = asyncOperation.StartOperation(null);
        if (startResult < 0)
        {
            Marshal.ThrowExceptionForHR(startResult);
        }

        return CompleteAsyncFileDropAfterOriginalDragAsync(dataObject, payloadKind, asyncOperation);
    }

    internal PrimedDragOperation PrimeAsyncFileDrop(ComTypes.IDataObject dataObject)
    {
        var fileDrop = CreateFormat(NativeMethods.CfHDrop, -1, NativeMethods.TymedHGlobal);
        if (dataObject.QueryGetData(ref fileDrop) != 0)
        {
            throw new NotSupportedException("The drag did not advertise CF_HDROP.");
        }

        var asyncOperation = GetAsyncCapability(dataObject);
        if (asyncOperation is null ||
            asyncOperation.GetAsyncMode(out var asyncMode) != 0 ||
            !asyncMode)
        {
            throw new InvalidOperationException("The file drop did not expose asynchronous capability.");
        }

        var ownsOperation = true;
        if (asyncOperation.InOperation(out var alreadyInOperation) == 0 && alreadyInOperation)
        {
            ownsOperation = false;
        }
        else
        {
            var startResult = asyncOperation.StartOperation(null);
            if (startResult < 0)
            {
                Marshal.ThrowExceptionForHR(startResult);
            }
        }

        return new PrimedDragOperation(dataObject, asyncOperation, ownsOperation);
    }

    private async Task<ExtractionResult> CompleteAsyncFileDropAfterOriginalDragAsync(
        ComTypes.IDataObject dataObject,
        DragPayloadKind payloadKind,
        IDataObjectAsyncCapability asyncOperation)
    {
        // Returning from IDropTarget.Drop lets the source application unwind its drag loop.
        // Continue on a pool thread only after that loop has had time to finish.
        await Task.Delay(OriginalDropUnwindDelay).ConfigureAwait(false);
        return ExtractCore(dataObject, payloadKind, asyncOperation, asyncStarted: true);
    }

    private ExtractionResult ExtractCore(
        ComTypes.IDataObject dataObject,
        DragPayloadKind payloadKind,
        IDataObjectAsyncCapability? asyncOperation,
        bool asyncStarted)
    {
        string? sessionPath = null;

        try
        {
            if (payloadKind == DragPayloadKind.AsyncFileDrop && !asyncStarted)
            {
                throw new InvalidOperationException("The delayed file drop could not start its asynchronous operation.");
            }

            sessionPath = _cache.CreateSessionDirectory();
            var outputPaths = payloadKind switch
            {
                DragPayloadKind.VirtualFileDescriptors => ExtractDescriptors(dataObject, sessionPath),
                DragPayloadKind.AsyncFileDrop => ExtractFileDrop(dataObject, sessionPath),
                _ => throw new NotSupportedException("The drag payload was not supported.")
            };

            if (asyncStarted)
            {
                var endResult = asyncOperation!.EndOperation(0, null, NativeMethods.DropEffectCopy);
                asyncStarted = false;
                if (endResult < 0)
                {
                    Marshal.ThrowExceptionForHR(endResult);
                }
            }

            return new ExtractionResult(sessionPath, outputPaths);
        }
        catch (Exception exception)
        {
            if (asyncStarted)
            {
                _ = asyncOperation!.EndOperation(
                    Marshal.GetHRForException(exception),
                    null,
                    NativeMethods.DropEffectNone);
            }

            try
            {
                if (sessionPath is not null)
                {
                    Directory.Delete(sessionPath, recursive: true);
                }
            }
            catch (IOException)
            {
                // Cleanup will run again at startup.
            }

            throw;
        }
    }

    private IReadOnlyList<string> ExtractDescriptors(ComTypes.IDataObject dataObject, string sessionPath)
    {
        var descriptors = ReadDescriptors(dataObject);
        if (descriptors.Count == 0)
        {
            throw new InvalidDataException("The drag did not contain any virtual files.");
        }

        var outputPaths = new List<string>(descriptors.Count);
        var claimedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in descriptors)
        {
            var safeName = FileNameSanitizer.Sanitize(descriptor.DisplayName, descriptor.Index);
            safeName = FileNameSanitizer.MakeUnique(safeName, claimedNames);
            var finalPath = Path.Combine(sessionPath, safeName);
            var partialPath = finalPath + ".partial";

            WriteFileContents(dataObject, descriptor.Index, partialPath);
            File.Move(partialPath, finalPath);
            ApplyInternetZoneMarker(finalPath);

            if (descriptor.LastWriteTime is { } lastWrite)
            {
                File.SetLastWriteTimeUtc(finalPath, lastWrite.UtcDateTime);
            }

            outputPaths.Add(finalPath);
        }

        return outputPaths;
    }

    private IReadOnlyList<string> ExtractFileDrop(ComTypes.IDataObject dataObject, string sessionPath)
    {
        var sourcePaths = ReadFileDropPaths(dataObject);
        if (sourcePaths.Count == 0)
        {
            throw new InvalidDataException("The delayed file drop did not produce any files.");
        }

        var outputPaths = new List<string>(sourcePaths.Count);
        var claimedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < sourcePaths.Count; index++)
        {
            var sourcePath = sourcePaths[index];
            if (!Path.IsPathFullyQualified(sourcePath) || !File.Exists(sourcePath))
            {
                throw new InvalidDataException("The delayed file drop returned an unavailable path.");
            }

            var safeName = FileNameSanitizer.Sanitize(Path.GetFileName(sourcePath), index);
            safeName = FileNameSanitizer.MakeUnique(safeName, claimedNames);
            var finalPath = Path.Combine(sessionPath, safeName);
            var partialPath = finalPath + ".partial";
            var lastWriteTime = File.GetLastWriteTimeUtc(sourcePath);

            CopyPhysicalFile(sourcePath, partialPath);
            File.Move(partialPath, finalPath);
            ApplyInternetZoneMarker(finalPath);
            File.SetLastWriteTimeUtc(finalPath, lastWriteTime);
            outputPaths.Add(finalPath);
        }

        return outputPaths;
    }

    internal static IReadOnlyList<string> ReadFileDropPaths(ComTypes.IDataObject dataObject)
    {
        var format = CreateFormat(NativeMethods.CfHDrop, -1, NativeMethods.TymedHGlobal);
        dataObject.GetData(ref format, out var medium);

        try
        {
            if ((uint)medium.tymed != NativeMethods.TymedHGlobal || medium.unionmember == IntPtr.Zero)
            {
                throw new InvalidDataException("CF_HDROP was not provided as global memory.");
            }

            var count = NativeMethods.DragQueryFile(
                medium.unionmember,
                NativeMethods.DragQueryFileCount,
                null,
                0);
            if (count > 10_000)
            {
                throw new InvalidDataException("The delayed file-drop count was invalid.");
            }

            var paths = new List<string>(checked((int)count));
            for (uint index = 0; index < count; index++)
            {
                var length = NativeMethods.DragQueryFile(medium.unionmember, index, null, 0);
                if (length == 0 || length >= short.MaxValue)
                {
                    throw new InvalidDataException("A delayed file-drop path was invalid.");
                }

                var path = new System.Text.StringBuilder(checked((int)length + 1));
                if (NativeMethods.DragQueryFile(medium.unionmember, index, path, length + 1) == 0)
                {
                    throw new InvalidDataException("A delayed file-drop path could not be read.");
                }

                paths.Add(path.ToString());
            }

            return paths;
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    private static void CopyPhysicalFile(string sourcePath, string outputPath)
    {
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read | FileShare.Delete,
            1024 * 1024,
            FileOptions.SequentialScan);
        using var output = new FileStream(
            outputPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.SequentialScan);
        source.CopyTo(output, 1024 * 1024);
    }

    private static IDataObjectAsyncCapability? GetAsyncCapability(ComTypes.IDataObject dataObject)
    {
        try
        {
            return dataObject as IDataObjectAsyncCapability;
        }
        catch (COMException)
        {
            return null;
        }
        catch (InvalidCastException)
        {
            return null;
        }
    }

    private IReadOnlyList<DragFileDescriptor> ReadDescriptors(ComTypes.IDataObject dataObject)
    {
        var format = CreateFormat(_descriptorFormat, -1, NativeMethods.TymedHGlobal);
        dataObject.GetData(ref format, out var medium);

        try
        {
            if ((uint)medium.tymed != NativeMethods.TymedHGlobal || medium.unionmember == IntPtr.Zero)
            {
                throw new InvalidDataException("FileGroupDescriptorW was not provided as global memory.");
            }

            var memory = NativeMethods.GlobalLock(medium.unionmember);
            if (memory == IntPtr.Zero)
            {
                throw new IOException("Unable to lock the virtual file descriptor list.");
            }

            try
            {
                var count = Marshal.ReadInt32(memory);
                if (count is < 0 or > 10_000)
                {
                    throw new InvalidDataException("The virtual file descriptor count was invalid.");
                }

                var descriptorSize = Marshal.SizeOf<FileDescriptorW>();
                var descriptors = new List<DragFileDescriptor>(count);
                for (var index = 0; index < count; index++)
                {
                    var pointer = IntPtr.Add(memory, sizeof(uint) + (index * descriptorSize));
                    var descriptor = Marshal.PtrToStructure<FileDescriptorW>(pointer);
                    long? size = null;
                    if ((descriptor.Flags & FileDescriptorHasSize) != 0)
                    {
                        size = ((long)descriptor.FileSizeHigh << 32) | descriptor.FileSizeLow;
                    }

                    DateTimeOffset? lastWrite = null;
                    if ((descriptor.Flags & FileDescriptorHasWriteTime) != 0 && descriptor.LastWriteTime > 0)
                    {
                        lastWrite = DateTimeOffset.FromFileTime(descriptor.LastWriteTime);
                    }

                    descriptors.Add(new DragFileDescriptor(index, descriptor.FileName ?? string.Empty, size, lastWrite));
                }

                return descriptors;
            }
            finally
            {
                NativeMethods.GlobalUnlock(medium.unionmember);
            }
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    private void WriteFileContents(ComTypes.IDataObject dataObject, int index, string outputPath)
    {
        var format = CreateFormat(
            _contentsFormat,
            index,
            NativeMethods.TymedIStream | NativeMethods.TymedHGlobal);

        dataObject.GetData(ref format, out var medium);
        try
        {
            using var output = new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1024 * 1024,
                FileOptions.SequentialScan);

            switch ((uint)medium.tymed)
            {
                case NativeMethods.TymedIStream:
                    CopyComStream(medium.unionmember, output);
                    break;
                case NativeMethods.TymedHGlobal:
                    CopyGlobalMemory(medium.unionmember, output);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported virtual file storage medium: {medium.tymed}.");
            }
        }
        finally
        {
            NativeMethods.ReleaseStgMedium(ref medium);
        }
    }

    private static void CopyComStream(IntPtr streamPointer, Stream output)
    {
        if (streamPointer == IntPtr.Zero)
        {
            throw new InvalidDataException("The virtual file stream was empty.");
        }

        var stream = (ComTypes.IStream)Marshal.GetTypedObjectForIUnknown(streamPointer, typeof(ComTypes.IStream));
        var buffer = new byte[1024 * 1024];
        var bytesReadPointer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            while (true)
            {
                stream.Read(buffer, buffer.Length, bytesReadPointer);
                var bytesRead = Marshal.ReadInt32(bytesReadPointer);
                if (bytesRead <= 0)
                {
                    break;
                }

                output.Write(buffer, 0, bytesRead);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(bytesReadPointer);
            Marshal.ReleaseComObject(stream);
        }
    }

    private static void CopyGlobalMemory(IntPtr globalMemory, Stream output)
    {
        var memory = NativeMethods.GlobalLock(globalMemory);
        if (memory == IntPtr.Zero)
        {
            throw new IOException("Unable to lock virtual file contents.");
        }

        try
        {
            var remaining = checked((long)NativeMethods.GlobalSize(globalMemory).ToUInt64());
            var offset = 0L;
            var buffer = new byte[1024 * 1024];
            while (remaining > 0)
            {
                var count = (int)Math.Min(buffer.Length, remaining);
                Marshal.Copy(new IntPtr(memory.ToInt64() + offset), buffer, 0, count);
                output.Write(buffer, 0, count);
                offset += count;
                remaining -= count;
            }
        }
        finally
        {
            NativeMethods.GlobalUnlock(globalMemory);
        }
    }

    private static void ApplyInternetZoneMarker(string filePath)
    {
        try
        {
            File.WriteAllText(filePath + ":Zone.Identifier", "[ZoneTransfer]\r\nZoneId=3\r\n");
        }
        catch (IOException)
        {
            // Some filesystems do not support alternate data streams.
        }
        catch (UnauthorizedAccessException)
        {
            // The data remains usable; do not fail the user's drop.
        }
    }

    private static ComTypes.FORMATETC CreateFormat(short clipboardFormat, int index, uint tymed) => new()
    {
        cfFormat = clipboardFormat,
        dwAspect = (ComTypes.DVASPECT)NativeMethods.DvAspectContent,
        lindex = index,
        ptd = IntPtr.Zero,
        tymed = (ComTypes.TYMED)tymed
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
    private readonly struct SizeL(int width, int height)
    {
        internal readonly int Width = width;
        internal readonly int Height = height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct PointL(int x, int y)
    {
        internal readonly int X = x;
        internal readonly int Y = y;
    }
}

internal sealed record ExtractionResult(string SessionPath, IReadOnlyList<string> Files);

internal enum DragPayloadKind
{
    Unsupported,
    VirtualFileDescriptors,
    AsyncFileDrop
}

internal sealed class PrimedDragOperation : IDisposable
{
    private ComTypes.IDataObject? _dataObject;
    private IDataObjectAsyncCapability? _asyncOperation;
    private readonly bool _ownsOperation;
    private int _completed;

    internal PrimedDragOperation(
        ComTypes.IDataObject dataObject,
        IDataObjectAsyncCapability asyncOperation,
        bool ownsOperation)
    {
        _dataObject = dataObject;
        _asyncOperation = asyncOperation;
        _ownsOperation = ownsOperation;
    }

    internal bool OwnsOperation => _ownsOperation;

    internal int Complete(int result = 0, uint effect = NativeMethods.DropEffectCopy)
    {
        return TryComplete(result, effect, out var endResult) ? endResult : 0;
    }

    internal bool TryComplete(int result, uint effect, out int endResult)
    {
        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            endResult = 0;
            return false;
        }

        var asyncOperation = Interlocked.Exchange(ref _asyncOperation, null);
        var dataObject = Interlocked.Exchange(ref _dataObject, null);
        endResult = _ownsOperation && asyncOperation is not null
            ? asyncOperation.EndOperation(result, null, effect)
            : 0;
        GC.KeepAlive(dataObject);
        return true;
    }

    public void Dispose()
    {
        _ = Complete();
        GC.SuppressFinalize(this);
    }
}
