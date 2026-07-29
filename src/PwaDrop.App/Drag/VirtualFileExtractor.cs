using System.Runtime.InteropServices;
using PwaDrop.App.Interop;
using PwaDrop.Core;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.App.Drag;

internal sealed class VirtualFileExtractor
{
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

    internal bool CanExtract(ComTypes.IDataObject dataObject)
    {
        var format = CreateFormat(_descriptorFormat, -1, NativeMethods.TymedHGlobal);
        return dataObject.QueryGetData(ref format) == 0;
    }

    internal ExtractionResult Extract(ComTypes.IDataObject dataObject)
    {
        var descriptors = ReadDescriptors(dataObject);
        if (descriptors.Count == 0)
        {
            throw new InvalidDataException("The drag did not contain any virtual files.");
        }

        var sessionPath = _cache.CreateSessionDirectory();
        var outputPaths = new List<string>(descriptors.Count);
        var claimedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var asyncOperation = dataObject as IDataObjectAsyncCapability;
        var asyncStarted = false;

        try
        {
            if (asyncOperation is not null && asyncOperation.GetAsyncMode(out var asyncMode) == 0 && asyncMode)
            {
                asyncStarted = asyncOperation.StartOperation(null) == 0;
            }

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

            if (asyncStarted)
            {
                asyncOperation!.EndOperation(0, null, NativeMethods.DropEffectCopy);
            }

            return new ExtractionResult(sessionPath, outputPaths);
        }
        catch (Exception exception)
        {
            if (asyncStarted)
            {
                asyncOperation!.EndOperation(Marshal.GetHRForException(exception), null, NativeMethods.DropEffectNone);
            }

            try
            {
                Directory.Delete(sessionPath, recursive: true);
            }
            catch (IOException)
            {
                // Cleanup will run again at startup.
            }

            throw;
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
