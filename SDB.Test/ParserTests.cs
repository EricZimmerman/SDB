using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using SDB.EntryTypes;
using SDB.Other;
using static SDB.SdbFile.TagValue;

namespace SDB.Test;

/// <summary>
///     Files built with <see cref="SdbBuilder" />, so every value and offset is known.
///     <see cref="SdbFile.StringTableEntries" /> and <see cref="SdbFile.Metrics" /> are static, so no parallel runs
/// </summary>
[TestFixture]
[NonParallelizable]
public class ParserTests
{
    public static readonly Guid DatabaseId = new("11223344-5566-7788-99aa-bbccddeeff00");

    public static readonly string[] Strings =
        { "Test shim database", "3.0.0.9", "evil.exe", "Evil App", "Evil Vendor", "InjectDll", "kernel32.dll" };

    /// <summary>Offset of each string table item from the TAG_STRINGTABLE tag, as TAG_TYPE_STRINGREF values hold</summary>
    public static int StringOffset(string value)
    {
        var offset = 6;
        foreach (var s in Strings)
        {
            if (s == value)
            {
                return offset;
            }

            offset += 6 + Encoding.Unicode.GetByteCount(s + "\0");
        }

        throw new ArgumentException(value);
    }

    public static readonly byte[] Pattern = { 0x90, 0x90, 0xCC };

    /// <summary>A small but complete database: DATABASE (EXE, LIBRARY with SHIM and PATCH) and STRINGTABLE</summary>
    public static SdbBuilder BuildSample()
    {
        var patchBits = SdbBuilder.PatchBitEntry(SdbBuilder.PatchBitsOp.Replace, 0x1234, "kernel32.dll", Pattern)
            .Concat(new byte[4]) // END
            .ToArray();

        return new SdbBuilder()
            .BeginList(TAG_DATABASE, "database")
            .QWord(TAG_TIME, 0x01D9_0000_0000_0000, "time")
            .StringRef(TAG_COMPILER_VERSION, StringOffset("3.0.0.9"), "compiler")
            .StringRef(TAG_NAME, StringOffset("Test shim database"), "dbname")
            .Binary(TAG_DATABASE_ID, DatabaseId.ToByteArray(), "dbid")
            .BeginList(TAG_LIBRARY, "library")
            .BeginList(TAG_SHIM, "shim")
            .StringRef(TAG_NAME, StringOffset("InjectDll"), "shimname")
            .StringRef(TAG_DLLFILE, StringOffset("kernel32.dll"), "dll")
            .EndList()
            .BeginList(TAG_PATCH, "patch")
            .Binary(TAG_PATCH_BITS, patchBits, "patchbits")
            .EndList()
            .EndList()
            .BeginList(TAG_EXE, "exe")
            .StringRef(TAG_NAME, StringOffset("evil.exe"), "exename")
            .StringRef(TAG_APP_NAME, StringOffset("Evil App"), "appname")
            .StringRef(TAG_VENDOR, StringOffset("Evil Vendor"), "vendor")
            .Word(TAG_MATCH_MODE, 2, "matchmode")
            .DWord(TAG_SHIM_TAGID, 0x40, "shimtagid")
            .Null(TAG_GENERAL, "general")
            .BeginList(TAG_MATCHING_FILE, "matchingfile")
            .StringRef(TAG_NAME, StringOffset("evil.exe"), "matchname")
            .EndList()
            .BeginList(TAG_SHIM_REF, "shimref")
            .StringRef(TAG_NAME, StringOffset("InjectDll"), "shimrefname")
            .EndList()
            .EndList()
            .EndList()
            .BeginList(TAG_STRINGTABLE, "stringtable")
            .String(TAG_STRINGTABLE_ITEM, Strings[0])
            .String(TAG_STRINGTABLE_ITEM, Strings[1])
            .String(TAG_STRINGTABLE_ITEM, Strings[2])
            .String(TAG_STRINGTABLE_ITEM, Strings[3])
            .String(TAG_STRINGTABLE_ITEM, Strings[4])
            .String(TAG_STRINGTABLE_ITEM, Strings[5])
            .String(TAG_STRINGTABLE_ITEM, Strings[6])
            .EndList();
    }

    private static ISdbEntry Find(IEnumerable<ISdbEntry> entries, int offset)
    {
        foreach (var entry in entries)
        {
            if (entry.Offset == offset)
            {
                return entry;
            }

            var found = Find(entry.Children, offset);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    [Test]
    public void SampleValuesAndOffsets()
    {
        var builder = BuildSample();
        var sdb = new SdbFile(builder.ToArray(), "built.sdb");
        ISdbEntry At(string name) => Find(sdb.Children, builder.Offsets[name]);

        sdb.ParseErrors.Should().BeEmpty();
        sdb.ParsingError.Should().BeFalse();
        sdb.MajorVersion.Should().Be(2);
        sdb.MinorVersion.Should().Be(1);
        sdb.SourceFile.Should().Be(Path.GetFullPath("built.sdb"));

        sdb.Children.Select(t => t.TypeId).Should().Equal(TAG_DATABASE, TAG_STRINGTABLE);
        sdb.Children[0].Offset.Should().Be(0xC);
        sdb.Children[0].Value.Should().Be("TAG_DATABASE");

        At("time").Should().BeOfType<SdbEntryQWord>().Which.Value.Should().Be(0x01D9_0000_0000_0000L);
        At("compiler").Value.Should().Be("3.0.0.9");
        At("dbname").Value.Should().Be("Test shim database");
        At("dbid").Value.Should().Be(DatabaseId);
        At("shimname").Value.Should().Be("InjectDll");
        At("dll").Value.Should().Be("kernel32.dll");
        At("exename").Value.Should().Be("evil.exe");
        At("appname").Value.Should().Be("Evil App");
        At("vendor").Value.Should().Be("Evil Vendor");
        At("matchmode").Should().BeOfType<SdbEntryWord>().Which.Value.Should().Be((ushort)2);
        At("shimtagid").Should().BeOfType<SdbEntryDWord>().Which.Value.Should().Be(0x40);
        At("general").Should().BeOfType<SdbEntryNull>().Which.Value.Should().Be(true);
        At("matchname").Value.Should().Be("evil.exe");
        At("shimrefname").Value.Should().Be("InjectDll");

        // every tag the builder wrote is found at its offset, with the tag it was written with
        foreach (var (name, offset) in builder.Offsets)
        {
            Find(sdb.Children, offset).Should().NotBeNull(name);
        }

        var exe = (SdbEntryList)At("exe");
        exe.Children.Select(t => t.TypeId).Should().Equal(TAG_NAME, TAG_APP_NAME, TAG_VENDOR, TAG_MATCH_MODE,
            TAG_SHIM_TAGID, TAG_GENERAL, TAG_MATCHING_FILE, TAG_SHIM_REF);
    }

    [Test]
    public void StringTable()
    {
        var sdb = new SdbFile(BuildSample().ToArray(), "built.sdb");

        sdb.Strings.Values.Select(t => t.Value).Should().Equal(Strings);
        sdb.Strings.Keys.Should().Equal(Strings.Select(StringOffset));
        sdb.Strings[StringOffset("evil.exe")].ToString().Should().Be("evil.exe");

        var items = sdb.Children[1].Children;
        items.Should().HaveCount(Strings.Length).And.AllBeOfType<SdbEntryStringTableItem>();
        items.Select(t => t.Value).Should().Equal(Strings);
    }

    [Test]
    public void PatchBitsAreParsed()
    {
        var builder = BuildSample();
        var sdb = new SdbFile(builder.ToArray(), "built.sdb");

        var bits = Find(sdb.Children, builder.Offsets["patchbits"]).Should().BeOfType<SdbEntryBinary>().Subject;

        bits.PatchBits.Should().NotBeNull();
        bits.PatchBits.ParseErrors.Should().BeEmpty();
        var entry = bits.PatchBits.Entries.Should().ContainSingle().Subject;
        entry.OpCode.Should().Be(PatchBits.OpCode_.PATCH_REPLACE);
        entry.Rva.Should().Be(0x1234);
        entry.ModuleName.Should().Be("kernel32.dll");
        entry.Pattern.Should().Equal(Pattern);
        entry.PatternSize.Should().Be(3);
        entry.ToString().Should().Be("OpCode: PATCH_REPLACE Action Size: 0x57 Pattern size: 0x3 Rva: 0x1234 Module: kernel32.dll");

        // still usable on its own, as SDBExplorer does
        new PatchBits(bits.Bytes).Entries.Should().ContainSingle();
    }

    [Test]
    public void LoadFileChecksTheHeader()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sdb-test-{Guid.NewGuid():N}.sdb");
        try
        {
            File.WriteAllBytes(path, BuildSample().ToArray());
            Sdb.LoadFile(path).ParseErrors.Should().BeEmpty();

            var bad = BuildSample().ToArray();
            bad[8] = (byte)'x';
            File.WriteAllBytes(path, bad);
            FluentActions.Invoking(() => Sdb.LoadFile(path)).Should().Throw<SdbParseException>()
                .WithMessage("Invalid signature at 0x08. Should be 'sdbf'").Which.Offset.Should().Be(8);

            File.WriteAllBytes(path, new byte[] { 2, 0, 0, 0 });
            FluentActions.Invoking(() => Sdb.LoadFile(path)).Should().Throw<SdbParseException>().WithMessage("*too small*");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void EachFileKeepsItsOwnStrings()
    {
        var first = new SdbFile(BuildSample().ToArray(), "first.sdb");

        // a second file whose string table has different strings at the same offsets
        var second = new SdbBuilder()
            .BeginList(TAG_DATABASE)
            .StringRef(TAG_NAME, 6)
            .EndList()
            .BeginList(TAG_STRINGTABLE)
            .String(TAG_STRINGTABLE_ITEM, "Other database")
            .EndList()
            .ToArray();
        var other = new SdbFile(second, "second.sdb");

        first.Children[0].Children.First(t => t.TypeId == TAG_NAME).Value.Should().Be("Test shim database");
        other.Children[0].Children[0].Value.Should().Be("Other database");

        // the static collections describe the last file loaded, as before
        SdbFile.StringTableEntries.Values.Select(t => t.Value).Should().Equal("Other database");
        SdbFile.Metrics.Should().BeEquivalentTo(new Dictionary<SdbFile.TagValue, int>
            { [TAG_NAME] = 1, [TAG_STRINGTABLE_ITEM] = 1 });
    }

    [Test]
    public void MetricsCountTagsInsideTheTopLevelLists()
    {
        _ = new SdbFile(BuildSample().ToArray(), "built.sdb");

        SdbFile.Metrics[TAG_NAME].Should().Be(5);
        SdbFile.Metrics[TAG_STRINGTABLE_ITEM].Should().Be(Strings.Length);
        SdbFile.Metrics[TAG_PATCH_BITS].Should().Be(1);
        SdbFile.Metrics.Should().NotContainKey(TAG_DATABASE);
        SdbFile.Metrics.Should().NotContainKey(TAG_STRINGTABLE);
    }
}
