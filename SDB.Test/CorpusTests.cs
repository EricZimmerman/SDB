using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using SDB.EntryTypes;

namespace SDB.Test;

/// <summary>
///     Every .sdb in a local collection that is not in the repo: the 'SDB samples' folder next to the repository, or
///     SDB_CORPUS. Skipped when it is missing (as in CI)
/// </summary>
[TestFixture]
[NonParallelizable]
public class CorpusTests
{
    /// <summary>Entry counts from an independent walker, for samples with these names</summary>
    private static readonly Dictionary<string, int> KnownEntryCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cve2012-4792.sdb"] = 27080,
        ["CVE-2013-3893.sdb"] = 4897,
        ["cve-2014-0322.sdb"] = 198
    };

    private static string CorpusDirectory()
    {
        var env = Environment.GetEnvironmentVariable("SDB_CORPUS");
        if (string.IsNullOrEmpty(env) == false)
        {
            return env;
        }

        // bin/Release/net10.0 -> SDB.Test -> repo -> its parent
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        for (var i = 0; i < 6 && dir != null; i++, dir = dir.Parent)
        {
            foreach (var candidate in new[]
                     {
                         Path.Combine(dir.FullName, "Corpus", "SDB samples"),
                         Path.Combine(dir.FullName, "SDB samples")
                     })
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static int Count(IEnumerable<ISdbEntry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            _ = entry.Value?.ToString();
            count += 1 + Count(entry.Children);
        }

        return count;
    }

    [Test]
    public void EverySampleParses()
    {
        var dir = CorpusDirectory();
        if (dir == null || Directory.Exists(dir) == false)
        {
            Assert.Ignore("No 'SDB samples' folder next to the repository and no SDB_CORPUS");
        }

        var files = Directory.GetFiles(dir, "*.sdb", SearchOption.AllDirectories);
        files.Should().NotBeEmpty();

        foreach (var file in files)
        {
            var sdb = Sdb.LoadFile(file);
            var name = Path.GetFileName(file);

            sdb.ParseErrors.Should().BeEmpty(name);
            sdb.Strings.Should().NotBeEmpty(name);

            var count = Count(sdb.Children);
            if (KnownEntryCounts.TryGetValue(name, out var expected))
            {
                count.Should().Be(expected, name);
            }

            TestContext.Out.WriteLine($"{name}: {count:N0} entries, {sdb.Strings.Count:N0} strings");
        }
    }
}
