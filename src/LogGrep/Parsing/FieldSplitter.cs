using System.Buffers.Text;

namespace LogGrep.Parsing;

/// <summary>
/// Splits a combat log line into its comma separated fields without allocating.
/// Quoted fields (spell names contain commas) and bracketed arrays (COMBATANT_INFO
/// talent/item blobs) are kept as a single field.
/// </summary>
internal sealed class FieldSplitter
{
    private int[] _start = new int[512];
    private int[] _length = new int[512];

    public int Count { get; private set; }

    public void Split(ReadOnlySpan<byte> line)
    {
        Count = 0;
        int depth = 0;
        bool quoted = false;
        int fieldStart = 0;

        for (int i = 0; i < line.Length; i++)
        {
            byte c = line[i];
            if (c == (byte)'"')
            {
                quoted = !quoted;
            }
            else if (quoted)
            {
                continue;
            }
            else if (c == (byte)'[' || c == (byte)'(')
            {
                depth++;
            }
            else if (c == (byte)']' || c == (byte)')')
            {
                if (depth > 0) depth--;
            }
            else if (c == (byte)',' && depth == 0)
            {
                Add(fieldStart, i - fieldStart);
                fieldStart = i + 1;
            }
        }

        Add(fieldStart, line.Length - fieldStart);
    }

    private void Add(int start, int length)
    {
        if (Count == _start.Length)
        {
            Array.Resize(ref _start, _start.Length * 2);
            Array.Resize(ref _length, _length.Length * 2);
        }

        _start[Count] = start;
        _length[Count] = length;
        Count++;
    }

    /// <summary>Returns field <paramref name="index"/> with surrounding quotes stripped.</summary>
    public ReadOnlySpan<byte> Field(ReadOnlySpan<byte> line, int index)
    {
        if ((uint)index >= (uint)Count) return default;
        return Unquote(line.Slice(_start[index], _length[index]));
    }

    public static ReadOnlySpan<byte> Unquote(ReadOnlySpan<byte> span)
        => span.Length >= 2 && span[0] == (byte)'"' && span[^1] == (byte)'"' ? span[1..^1] : span;

    public long Long(ReadOnlySpan<byte> line, int index, long fallback = 0)
        => Utf8Parser.TryParse(Field(line, index), out long value, out _) ? value : fallback;

    public int Int(ReadOnlySpan<byte> line, int index, int fallback = 0)
        => Utf8Parser.TryParse(Field(line, index), out int value, out _) ? value : fallback;

    public bool Flag(ReadOnlySpan<byte> line, int index)
    {
        var span = Field(line, index);
        if (span.Length == 0) return false;
        if (span[0] == (byte)'n' || span[0] == (byte)'N') return false; // nil
        return !(span.Length == 1 && span[0] == (byte)'0');
    }

    public string Text(ReadOnlySpan<byte> line, int index)
    {
        var span = Field(line, index);
        return span.IsEmpty ? string.Empty : System.Text.Encoding.UTF8.GetString(span);
    }

    /// <summary>Parses the "0x511" style unit flag masks.</summary>
    public int Hex(ReadOnlySpan<byte> line, int index) => ParseHex(Field(line, index));

    public static int ParseHex(ReadOnlySpan<byte> span)
    {
        if (span.Length > 2 && span[0] == (byte)'0' && (span[1] == (byte)'x' || span[1] == (byte)'X'))
        {
            span = span[2..];
            int value = 0;
            foreach (byte b in span)
            {
                int digit = b switch
                {
                    >= (byte)'0' and <= (byte)'9' => b - '0',
                    >= (byte)'a' and <= (byte)'f' => b - 'a' + 10,
                    >= (byte)'A' and <= (byte)'F' => b - 'A' + 10,
                    _ => -1,
                };
                if (digit < 0) return value;
                value = (value << 4) | digit;
            }
            return value;
        }

        return Utf8Parser.TryParse(span, out int parsed, out _) ? parsed : 0;
    }
}
