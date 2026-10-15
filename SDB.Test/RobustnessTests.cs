using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using SDB.EntryTypes;
using static SDB.SdbFile.TagValue;

namespace SDB.Test;

/// <summary>
///     Malformed and hostile input: parsing never throws past the header, never hangs, and keeps what it can.
///     Add regression tests here
/// </summary>
[TestFixture]
[NonParallelizable]
public class RobustnessTests
{
    private static SdbFile Parse(byte[] bytes)
    {
        // a hang would block the test run, so give it a deadline
        var task = Task.Run(() => new SdbFile(bytes, "test.sdb"));
        task.Wait(TimeSpan.FromSeconds(30)).Should().BeTrue("parsing must finish");
        return task.Result;
    }

    /// <summary>Reads every Value and ToString, as a viewer would</summary>
    private static int Touch(IEnumerable<ISdbEntry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            _ = entry.Value?.ToString();
            _ = entry.ToString();
            count += 1 + Touch(entry.Children);
        }

        return count;
    }

    [Test]
    public void HeaderOnlyIsEmpty()
    {
        var sdb = Parse(new SdbBuilder().ToArray());

        sdb.Children.Should().BeEmpty();
        sdb.ParseErrors.Should().BeEmpty();
    }

    [Test]
    public void TooSmallThrows()
    {
        FluentActions.Invoking(() => new SdbFile(new byte[11], "x.sdb")).Should().Throw<SdbParseException>()
            .Which.Field.Should().Be("header");
    }

    [Test]
    public void TruncatedFileKeepsWhatIsThere()
    {
        var full = ParserTests.BuildSample().ToArray();

        // cut inside the string table: the DATABASE list is complete, the string table is not
        var cut = full.Take(full.Length - 30).ToArray();
        var sdb = Parse(cut);

        sdb.ParseErrors.Should().Contain(t => t.Contains("TAG_STRINGTABLE") && t.Contains("bytes are left"));
        sdb.Children.Select(t => t.TypeId).Should().Equal(TAG_DATABASE, TAG_STRINGTABLE);
        sdb.Children[1].Children.Should().HaveCountGreaterThan(3, "the complete items are kept");
        sdb.Children[0].Children.First(t => t.TypeId == TAG_NAME).Value.Should().Be("Test shim database");
        Touch(sdb.Children);
    }

    [Test]
    public void EveryTruncationParses()
    {
        var full = ParserTests.BuildSample().ToArray();

        for (var length = 12; length < full.Length; length++)
        {
            var sdb = Parse(full.Take(length).ToArray());
            Touch(sdb.Children);
        }
    }

    [Test]
    public void SizePastTheEndIsCut()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .DWord(TAG_SIZE, 1)
            .EndList()
            .ToArray();
        BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, 0xC + 2); // list size

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain("has size 0x7FFFFFF0");
        sdb.Children.Should().ContainSingle().Which.Children.Should().ContainSingle()
            .Which.Value.Should().Be(1);
    }

    [Test]
    public void NegativeSizeStopsTheList()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .DWord(TAG_SIZE, 7)
            .Binary(TAG_DATABASE_ID, new byte[16])
            .DWord(TAG_SIZE, 8)
            .EndList()
            .ToArray();
        BitConverter.GetBytes(-5).CopyTo(bytes, 0xC + 6 + 6 + 2); // binary size

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain("negative size (-5)");
        sdb.Children[0].Children.Select(t => t.Value).Should().Equal(7);
    }

    [Test]
    public void FixedSizeValueCutShort()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .Raw(0x01, 0x50, 0x00, 0x00) // TAG_TIME (QWORD) with only 2 bytes of data
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain("needs 8 bytes of data, only 2 left");
        sdb.Children[0].Children.Should().BeEmpty();
    }

    [TestCase((ushort)0x2001, "0x2000")] // TAG_TYPE_BYTE: no known tags
    [TestCase((ushort)0xA001, "0xA000")]
    [TestCase((ushort)0x0001, "0x0000")]
    public void UnsupportedTypeSkipsTheRestOfTheList(ushort tag, string type)
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .DWord(TAG_SIZE, 1)
            .Raw(BitConverter.GetBytes(tag))
            .DWord(TAG_SIZE, 2)
            .EndList()
            .BeginList(TAG_STRINGTABLE)
            .String(TAG_STRINGTABLE_ITEM, "still parsed")
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain($"Unsupported tag type {type}")
            .And.Contain("rest of TAG_DATABASE");
        sdb.Children[0].Children.Select(t => t.Value).Should().Equal(1);
        sdb.Children[1].Children.Single().Value.Should().Be("still parsed", "the next list is parsed");
    }

    [Test]
    public void NonListAtTopLevelStops()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .EndList()
            .DWord(TAG_SIZE, 1)
            .ToArray();

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain("only lists are expected at the top level");
        sdb.Children.Should().ContainSingle();
    }

    [Test]
    public void DeepNestingDoesNotOverflowTheStack()
    {
        // 200,000 nested lists would overflow the stack (which kills the process) without a depth limit
        const int depth = 200_000;
        var builder = new SdbBuilder();
        for (var i = 0; i < depth; i++)
        {
            builder.BeginList(TAG_DATABASE);
        }

        for (var i = 0; i < depth; i++)
        {
            builder.EndList();
        }

        var sdb = Parse(builder.ToArray());

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain($"more than {Chunk.MaxDepth} deep");

        var levels = 0;
        var entries = sdb.Children;
        while (entries.Count > 0)
        {
            levels += 1;
            entries = entries[0].Children;
        }

        levels.Should().Be(Chunk.MaxDepth + 1);
    }

    [TestCase(0)]
    [TestCase(8)]
    [TestCase(83)]
    [TestCase(0x7FFFFFFF)]
    public void BadPatchBitsSizeDoesNotHang(int size)
    {
        // size 0 used to loop forever
        var patchBits = SdbBuilder.PatchBitEntry(SdbBuilder.PatchBitsOp.Replace, 1, "m", new byte[] { 1 },
            sizeOverride: size);

        var bytes = new SdbBuilder()
            .BeginList(TAG_LIBRARY)
            .BeginList(TAG_PATCH)
            .Binary(TAG_PATCH_BITS, patchBits)
            .EndList()
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        var bits = (SdbEntryBinary)sdb.Children[0].Children[0].Children[0];
        bits.PatchBits.Entries.Should().BeEmpty();
        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain($"has size 0x{size:X}").And
            .Contain("TAG_PATCH_BITS");
    }

    [Test]
    public void PatternLongerThanTheEntryIsCut()
    {
        var patchBits = SdbBuilder.PatchBitEntry(SdbBuilder.PatchBitsOp.Match, 1, "m", new byte[] { 1, 2 },
            patternSizeOverride: 100);

        var pb = new SDB.Other.PatchBits(patchBits);

        pb.Entries.Should().ContainSingle().Which.Pattern.Should().Equal(1, 2);
        pb.ParseErrors.Should().ContainSingle().Which.Should().Contain("pattern truncated");
    }

    [Test]
    public void MissingStringIsReported()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .StringRef(TAG_NAME, 0x999)
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().ContainSingle().Which.Should().Contain("0x999, which has no string");
        sdb.Children[0].Children[0].Value.Should().Be("(string not found at string table offset 0x999)");
    }

    [Test]
    public void StringsOutsideTheStringTableAreNotLookedUp()
    {
        // used to be added to the same lookup, where a matching offset threw "same key"
        var bytes = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .String(TAG_STRINGTABLE_ITEM, "inline")
            .EndList()
            .BeginList(TAG_STRINGTABLE)
            .String(TAG_STRINGTABLE_ITEM, "table")
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        sdb.ParseErrors.Should().BeEmpty();
        sdb.Strings.Values.Select(t => t.Value).Should().Equal("table");
        sdb.Children[0].Children[0].Value.Should().Be("inline");
    }

    [Test]
    public void OddLengthStringIsRead()
    {
        var bytes = new SdbBuilder()
            .BeginList(TAG_STRINGTABLE)
            .Raw(0x01, 0x88, 0x03, 0x00, 0x00, 0x00, (byte)'A', 0x00, 0xFF) // 3 byte string
            .EndList()
            .ToArray();

        var sdb = Parse(bytes);

        sdb.Strings.Values.Single().Value.Should().Be("A");
        Touch(sdb.Children);
    }

    [Test]
    public void RandomCorruptionNeverThrows()
    {
        var original = ParserTests.BuildSample().ToArray();
        var random = new Random(1234);

        for (var i = 0; i < 3000; i++)
        {
            var bytes = (byte[])original.Clone();
            var changes = random.Next(1, 8);
            for (var c = 0; c < changes; c++)
            {
                // leave the header alone; that is checked separately
                bytes[random.Next(12, bytes.Length)] = (byte)random.Next(256);
            }

            var sdb = Parse(bytes);
            Touch(sdb.Children);
        }
    }
}
