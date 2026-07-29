using System.Runtime.InteropServices;
using ComTypes = System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.DragHarness;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class ManagedComStream : ComTypes.IStream
{
    private readonly MemoryStream _stream;

    internal ManagedComStream(byte[] content)
    {
        _stream = new MemoryStream(content, writable: false);
    }

    public void Read(byte[] buffer, int count, IntPtr bytesRead)
    {
        var read = _stream.Read(buffer, 0, count);
        if (bytesRead != IntPtr.Zero)
        {
            Marshal.WriteInt32(bytesRead, read);
        }
    }

    public void Write(byte[] buffer, int count, IntPtr bytesWritten) =>
        throw new COMException("The stream is read-only.", unchecked((int)0x80030005));

    public void Seek(long offset, int origin, IntPtr newPosition)
    {
        var position = _stream.Seek(offset, (SeekOrigin)origin);
        if (newPosition != IntPtr.Zero)
        {
            Marshal.WriteInt64(newPosition, position);
        }
    }

    public void SetSize(long value) => throw new COMException("The stream is read-only.", unchecked((int)0x80030005));

    public void CopyTo(ComTypes.IStream target, long count, IntPtr bytesRead, IntPtr bytesWritten)
    {
        var buffer = new byte[81920];
        var remaining = count;
        long copied = 0;
        while (remaining > 0)
        {
            var read = _stream.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0)
            {
                break;
            }

            target.Write(buffer, read, IntPtr.Zero);
            remaining -= read;
            copied += read;
        }

        if (bytesRead != IntPtr.Zero)
        {
            Marshal.WriteInt64(bytesRead, copied);
        }

        if (bytesWritten != IntPtr.Zero)
        {
            Marshal.WriteInt64(bytesWritten, copied);
        }
    }

    public void Commit(int flags)
    {
    }

    public void Revert() => throw new COMException("Revert is not supported.", unchecked((int)0x80030102));

    public void LockRegion(long offset, long count, int lockType) =>
        throw new COMException("LockRegion is not supported.", unchecked((int)0x80030001));

    public void UnlockRegion(long offset, long count, int lockType) =>
        throw new COMException("UnlockRegion is not supported.", unchecked((int)0x80030001));

    public void Stat(out ComTypes.STATSTG statistics, int flags)
    {
        statistics = new ComTypes.STATSTG
        {
            cbSize = _stream.Length,
            type = 2,
            grfMode = 0
        };
    }

    public void Clone(out ComTypes.IStream stream)
    {
        var clone = new ManagedComStream(_stream.ToArray());
        clone._stream.Position = _stream.Position;
        stream = clone;
    }
}

