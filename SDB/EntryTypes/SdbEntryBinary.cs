using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SDB.EntryTypes;

public class SdbEntryBinary : ISdbEntry
{
    public SdbEntryBinary(SdbFile.TagValue typeId, byte[] bytes, int offset)
    {
        TypeId = typeId;
        Bytes = bytes;
        Offset = offset;

        Children = new List<ISdbEntry>();
    }

    public List<ISdbEntry> Children { get; }

    public SdbFile.TagValue TypeId { get; }

    [IgnoreDataMember] public byte[] Bytes { get; }

    public object Value => GetValue();

    public int Offset { get; set; }

    private object GetValue()
    {
        if (Bytes.Length == 0x10)
        {
            return new Guid(Bytes);
        }

        return Bytes;
    }

    public override string ToString()
    {
        return $"Type: {TypeId} (0x{TypeId:X}) Bytes length: {Bytes.Length:N0} Children count: {Children.Count:N0}";
    }
}