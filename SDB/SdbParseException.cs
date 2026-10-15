using System;

namespace SDB;

/// <summary>
///     Thrown when data is not a usable SDB file (smaller than the header or no 'sdbf' signature).
///     Problems found after the header are reported in <see cref="SdbFile.ParseErrors" /> instead.
/// </summary>
public class SdbParseException : Exception
{
    public SdbParseException(string message, int offset = -1, string field = null, Exception inner = null)
        : base(message, inner)
    {
        Offset = offset;
        Field = field;
    }

    /// <summary>
    ///     Byte offset of the failing read, or -1 when unknown.
    /// </summary>
    public int Offset { get; }

    /// <summary>
    ///     Name of the field or structure being read when parsing failed, if known.
    /// </summary>
    public string Field { get; }
}
