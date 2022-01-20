using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using SDB;

namespace SBD.Test;

[TestFixture]
public class Tests
{
    //TODO Finish this
    [Test]
    public void ParseTest()
    {
        var files = Directory.GetFiles(@"D:\Code\SDB\SBD.Test\Test Files", "*.sdb");

        var f = @"C:\Temp\sdb\CVE-2013-3893.sdb";

        var ss1 = Sdb.LoadFile(f);

        foreach (var file in files)
        {
            Debug.WriteLine($"File: {file}");
            var ss = Sdb.LoadFile(file);

            //    File.WriteAllText($@"C:\temp\sdb\{Path.GetFileName(file)}.json", ss.Children.ToJson());

            Debug.WriteLine("METRICS");
            foreach (var metricsKey in SdbFile.Metrics.Keys)
            {
                Debug.WriteLine($"{metricsKey} (0x{metricsKey:X}): {SdbFile.Metrics[metricsKey]}");
            }

//                foreach (var chunk in ss.Children)
//                {
//                    Debug.WriteLine($"Chunk: {chunk.TypeId} Child count: {chunk.Children.Count:N0}");
//
//                    var exe = chunk.Children.Where(t => t.TypeId == SdbFile.TagValue.TAG_LAYER).ToList();
//
//                    Debug.WriteLine($"Exe count: {exe.Count:N0}, string count: {SdbFile.StringTableEntries.Count:N0}");
//
//                    foreach (var sdbEntry in exe)
//                    {
//                        Debug.WriteLine(sdbEntry.Children.Single(t => t.TypeId == SdbFile.TagValue.TAG_NAME));
//                    }
//
////                foreach (var chunkChild in chunk.Children)
////                {
////                    DumpChildren(chunkChild, 1);
////                }
//
//                    //     Debug.WriteLine($"ID: {chunk.TypeId} ({chunk.TypeId:X}) bytes len: 0x{chunk.Bytes.Length:X}");
//
//                    //      ProcessBytes(chunk);
//
//                    //  var fname = $"{Path.GetFileName(sourceFile)}_{chunk.TypeId}_{chunk.Bytes.Length:X}.bin";
//
//                    //    File.WriteAllBytes(Path.Combine(@"C:\temp",fname),chunk.Bytes);
//                }
        }
    }
}