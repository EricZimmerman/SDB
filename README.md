# SDB
Parse Microsoft shim databases

```csharp
var sdb = Sdb.LoadFile(@"C:\Windows\apppatch\sysmain.sdb");

foreach (var entry in sdb.Children) // TAG_INDEXES, TAG_DATABASE, TAG_STRINGTABLE
{
    Console.WriteLine($"{entry.TypeId} at 0x{entry.Offset:X}: {entry.Children.Count} children");
}

foreach (var error in sdb.ParseErrors) // damaged data is reported here; parsing keeps what it can
{
    Console.WriteLine(error);
}
```

`Sdb.LoadFile` throws `SdbParseException` only when the file is not an SDB file at all (smaller than the header or no `sdbf` signature).
