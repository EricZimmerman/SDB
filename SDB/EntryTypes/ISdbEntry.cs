using System.Collections.Generic;

namespace SDB.EntryTypes;

public interface ISdbEntry
{
    List<ISdbEntry> Children { get; }

    SdbFile.TagValue TypeId { get; }
    byte[] Bytes { get; }

    object Value { get; }

    int Offset { get; set; }
}