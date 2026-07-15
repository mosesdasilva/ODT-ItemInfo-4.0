# Install-ready SPT release archive contract

Research for [Wayfinder issue #12](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/12), completed 2026-07-12.

## Conclusion

The private vNext release should be a ZIP named `ODT-ItemInfo-4.0_<Version>.zip`. Its first path component must be `SPT`, so extracting it into the SPT installation directory places the mod at `SPT/user/mods/ODT-ItemInfo-4.0` without moving files by hand.

This combines the current Forge mandate for an extract-ready ZIP/7z layout with the layout demonstrated by both reference releases. Forge allows `.zip` or `.7z`, requires compiled C# server-mod files, and requires a license file inside the archive. It also requires dependencies to be declared on each submitted mod version and prohibits bundling multiple mods as a compilation. ([Forge Content Guidelines](https://forge.sp-tarkov.com/content-guidelines))

## Exact vNext archive

```text
ODT-ItemInfo-4.0_<Version>.zip
+-- SPT/
    +-- user/
        +-- mods/
            +-- ODT-ItemInfo-4.0/
                +-- ODT-ItemInfo-4.0.dll
                +-- LICENSE
                +-- config/
                    +-- bsgblacklist.json
                    +-- config.json
                    +-- translations.json
```

The `SPT/user/mods/<mod>` shape is not guesswork: the latest upstream ODT archive and Item Valuation archive both ship that root. The inspected ODT 2.0.14 asset contains its DLL plus five config files under `SPT/user/mods/ODT-ItemInfo-4.0`; the inspected Item Valuation 2.0.1 asset contains its DLL and `config.json` under `SPT/user/mods/acidphantasm-itemvaluation`. ([ODT 2.0.14 release](https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0/releases/tag/2.0.14), [ODT 2.0.14 archive](https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0/releases/download/2.0.14/ODT-ItemInfo-4.0_2.0.14.7z), [Item Valuation 2.0.1 release](https://github.com/acidphantasm/itemvaluation-csharp/releases/tag/2.0.1), [Item Valuation 2.0.1 archive](https://github.com/acidphantasm/itemvaluation-csharp/releases/download/2.0.1/acidphantasm-itemvaluation.7z))

The three-file config list is **ODT vNext project policy**, not a Forge rule. It implements the settled consolidation direction in which `tiers.json` and `tiers_hex.json` disappear after their data moves into the existing recolor configuration. The current checkout still loads those two files, so this vNext archive must not be produced until that consolidation is implemented.

`LICENSE` is the one deliberate addition over both inspected reference archives. Forge now explicitly says license files must be included in mod archives; copying either older reference payload verbatim would therefore not meet the current rule. ([Forge license requirements](https://forge.sp-tarkov.com/content-guidelines#61-license-requirements))

## Allowlist and denylist

Packaging must use the five-file allowlist shown above and fail if any expected file is missing or any other file enters the ZIP.

The following are explicitly excluded:

- `tiers.json` and `tiers_hex.json` after config consolidation
- `.pdb`, `.deps.json`, and `.staticwebassets.endpoints.json`
- copied SPT framework DLLs or NuGet dependencies
- Color Converter API or any other separately maintained mod
- source files, project/solution files, tests, build scripts, repository docs, and `TODO.md`

The binary-only shape follows the official SPT C# example guidance to build in Release mode and install the Release output as a server mod; Forge likewise requires compiled files ready for use. ([SPT server-mod examples: Distribution](https://github.com/sp-tarkov/server-mod-examples#distribution), [Forge C# server-mod requirements](https://forge.sp-tarkov.com/content-guidelines#22-mod-types-and-requirements))

The strict denylist is **ODT vNext project policy**. Forge requires a complete working archive but does not prescribe this repository's exact file allowlist.

## Version and archive naming

`<Version>` must come from the evaluated MSBuild `Version` property in [`ODT-ItemInfo-4.0.csproj`](../../ODT-ItemInfo-4.0.csproj). The release command must then compare it with `ModMetadata.Version` in [`ItemInfo.cs`](../../ItemInfo.cs) and fail before building the ZIP if they differ.

This makes the deterministic filename:

```text
ODT-ItemInfo-4.0_<csproj Version>.zip
```

Choosing the project property as the packaging input is **ODT vNext project policy**. The equality check is mandatory for Forge compatibility: current guidance requires the version in the `.csproj` and `AbstractModMetadata` and says all declared versions must match exactly. ([Forge version declaration requirements](https://forge.sp-tarkov.com/content-guidelines#22-mod-types-and-requirements), [Forge version consistency](https://forge.sp-tarkov.com/content-guidelines#33-implementation-requirements))

The checkout currently violates that gate: the project says `0.0.1`, while runtime metadata says `2.0.14`. Packaging must report this mismatch rather than silently choosing one.

Forge does not mandate an archive filename pattern. The recommended pattern follows upstream ODT's versioned `ODT-ItemInfo-4.0_2.0.14.7z` naming while using the user-approved ZIP format. ([ODT 2.0.14 release](https://github.com/thuynguyentrungdang/ODT-ItemInfo-4.0/releases/tag/2.0.14))

## Dependency behavior

Do not put Color Converter API in this archive. Forge requires dependencies to be attached to the mod-version submission and separately prohibits mod compilations, so a dependency must remain a separately installed package. ([Forge archive and dependency requirements](https://forge.sp-tarkov.com/content-guidelines#21-file-format-standards), [Forge compilation prohibition](https://forge.sp-tarkov.com/content-guidelines#73-compilation-and-collection-guidelines))

The resolved [Color Specification pipeline](supported-color-specification-pipeline.md) keeps Color Converter API external. Native background names work without it, while any configured hexadecimal background requires Color Converter API 1.1.1 or newer on every client. Release instructions must state that client requirement, but the server archive must not bundle, link, or declare Color Converter as an SPT server dependency.

The current ODT metadata correctly declares no Color Converter `ModDependencies`, and the project references only SPT packages. Item Valuation remains useful prior art for keeping the client plugin out of the server archive. ([Item Valuation 2.0.1 source](https://github.com/acidphantasm/itemvaluation-csharp/blob/2.0.1/ItemValuation.cs))

## Install and upgrade behavior

For this private prototype, use a **clean replacement**:

1. Stop the SPT server.
2. Delete the existing `SPT/user/mods/ODT-ItemInfo-4.0` directory.
3. Extract the ZIP into the SPT installation directory--the directory that already contains the `SPT` folder.
4. Confirm the installed mod directory matches the five-file allowlist, then start the server.

Clean replacement is **ODT vNext project policy**, not a Forge mandate. It prevents removed files such as `tiers.json` and `tiers_hex.json` from surviving an in-place extraction and disguising an invalid package. The vNext migration contract requires users to start from the shipped consolidated configuration, manually reapply unrelated preferences, and follow the exhaustive legacy-setting lineage guidance specified in issue #22.

The installation destination itself follows Forge's rule that archives must match SPT directory conventions and be usable by direct extraction without folder reorganization. ([Forge file-structure requirement](https://forge.sp-tarkov.com/content-guidelines#21-file-format-standards))

## Preconditions surfaced for later tickets

The one-command release cannot satisfy this contract until all three are true:

1. `tiers.json` and `tiers_hex.json` are no longer runtime inputs.
2. A correct `LICENSE` file exists for inclusion and preserves required upstream rights/notices.
3. The `.csproj` and runtime metadata versions match.

The second gate is already satisfied by the repository's root MIT `LICENSE`; the release implementation must copy and validate it. The first and third gates remain unresolved. These are packaging gates, not work to perform in this research ticket.
