# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A `netstandard2.0` NuGet library that parses Windows application compatibility shim databases (`.sdb`: `sysmain.sdb`, custom shims). Its consumer is SDBExplorer (a private Windows GUI, which uses the NuGet package), so keep the public API compatible: `Sdb.LoadFile`, `SdbFile.Children`, `ISdbEntry` (`TypeId`, `Value`, `Offset`, `Bytes`, `Children`), the static `SdbFile.Metrics` and `SdbFile.StringTableEntries`, and `new PatchBits(byte[])`. Input may be corrupt or hostile.

## Commands

The SDK is pinned in `global.json` (.NET 10). The library targets `netstandard2.0`; tests target `net10.0`. Both use `LangVersion 10`. Tests use NUnit 5 + FluentAssertions 7 (pinned to `[7.2.2,8.0.0)`, since 8.x requires a commercial license).

```bash
dotnet build                                                  # builds SDB.slnx
dotnet test                                                   # CI runs: dotnet test --configuration Release
dotnet test --filter "FullyQualifiedName~RobustnessTests"     # one fixture
dotnet test --filter "Name=SampleValuesAndOffsets"            # one test
dotnet pack --configuration Release
```

## Format and parser

- 12 byte header: Int32 major, Int32 minor, `sdbf`. Then the top level, which is only lists (normally `TAG_INDEXES`, `TAG_DATABASE`, `TAG_STRINGTABLE`).
- Every tag is a UInt16; the high nibble is the type (`SdbFile.TagType`): NULL (no data), WORD/DWORD/QWORD (2/4/8 bytes), STRINGREF (Int32 offset of a `TAG_STRINGTABLE_ITEM`, measured from the `TAG_STRINGTABLE` tag), LIST/STRING/BINARY (Int32 size, then the data). BYTE has no known tags. Strings are UTF-16 with a NUL. A 16 byte BINARY's `Value` is a `Guid`. References: Geoff Chappell's apphelp/sdb pages and evil-e/sdb-explorer's `sdb.h` (links in `SdbFile.cs`).
- `Chunk.Parse` (internal, static) parses one list or the top level, recursively. `Offset` of every entry is the file offset of its tag. The `0x53db` special case (skip one byte) is from a real file; keep it.
- String table: only items inside `TAG_STRINGTABLE` go in the file's `Strings` (keyed by STRINGREF value). Each `SdbEntryStringRef` resolves against its own file's table. `SdbFile.StringTableEntries`/`Metrics` are static and describe the last file loaded (SDBExplorer reads them); `Metrics` counts tags inside the top level lists.
- `TAG_PATCH_BITS` binaries are parsed into `SdbEntryBinary.PatchBits` (entries: OpCode, entry size, pattern size, RVA, unknown, 0x20 byte UTF-16 module, 0x20 unknown bytes, pattern; until an END opcode).

### Error model
- `SdbParseException` (`Offset`, `Field`; derives from `Exception`) only for unusable input: smaller than the header, or no `sdbf` signature (`LoadFile`; the message is unchanged). The `SdbFile` constructor only requires the 12 header bytes.
- Everything else goes to `SdbFile.ParseErrors` (`ParsingError` when not empty) and parsing keeps everything before the problem in each list: a size past the end is cut to what is there; a negative size, a fixed-size value cut short, or an unsupported type stops that list; a non-list at the top level stops the file; lists nested deeper than `Chunk.MaxDepth` (32) are not parsed (a stack overflow can't be caught); a STRINGREF with no string is reported and its `Value` says so. `PatchBits.ParseErrors` (copied into the file's) for entry sizes under 84 bytes or past the end, which used to loop forever on size 0.

## Tests

- `ParserTests`: files built with `SdbBuilder` (writes tags and records their offsets), checking every value and offset, the string table, patch bits, `LoadFile` errors, two files keeping their own strings, metrics. Non-parallelizable, because of the static collections.
- `RobustnessTests`: truncation at every length, bad sizes, unsupported types, 200,000 nested lists, bad patch bit sizes (with a deadline), missing strings, random corruption. Add regression tests here.
- `CorpusTests`: every `.sdb` in a local collection that is not in the repo: the `SDB samples` folder in `Corpus/` next to the repository (or directly next to it), or `SDB_CORPUS`. Skipped when it is missing (as in CI). It expects no `ParseErrors`, and exact entry counts (from an independent walker) for the three evil-e/sdb-explorer samples. Don't copy its files into the repo (Microsoft's shim databases).

## Versioning and release

- MinVer sets the version from the git tag (bare tags like `2026.10.0`); there is no `<Version>` in the csproj.
- To release: push to `master`, then `git tag <ver> && git push origin <ver> && gh release create <ver> --verify-tag --generate-notes`.
- `.github/workflows/publish.yml` (identical to GuidMapping's) packs, validates and tests on PRs and pushes to `master`. When a GitHub Release is published, it pushes to NuGet.org using trusted publishing (`NuGet/login` with the `NUGET_USER` repo variable).
- Dependabot (`.github/dependabot.yml`) opens monthly PRs for GitHub Actions and NuGet updates.
