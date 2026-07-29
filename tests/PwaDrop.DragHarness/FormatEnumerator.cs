using System.Runtime.InteropServices.ComTypes;

namespace PwaDrop.DragHarness;

internal sealed class FormatEnumerator : IEnumFORMATETC
{
    private readonly FORMATETC[] _formats;
    private int _index;

    internal FormatEnumerator(params FORMATETC[] formats)
    {
        _formats = formats;
    }

    public int Next(int count, FORMATETC[] elements, int[]? fetched)
    {
        var copied = 0;
        while (copied < count && _index < _formats.Length)
        {
            elements[copied++] = _formats[_index++];
        }

        if (fetched is { Length: > 0 })
        {
            fetched[0] = copied;
        }

        return copied == count ? 0 : 1;
    }

    public int Skip(int count)
    {
        _index = Math.Min(_formats.Length, _index + count);
        return _index < _formats.Length ? 0 : 1;
    }

    public int Reset()
    {
        _index = 0;
        return 0;
    }

    public void Clone(out IEnumFORMATETC newEnum)
    {
        var clone = new FormatEnumerator(_formats) { _index = _index };
        newEnum = clone;
    }
}
