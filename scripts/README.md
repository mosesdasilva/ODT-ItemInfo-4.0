# SPT Test Clone smoke gate

## Build a validated local release

Run the complete release gate with Windows PowerShell 5.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Release.ps1
```

The command always restores, builds and tests Release, packages the exact
five-file SPT layout, validates the staged and archived bytes, and atomically
replaces `artifacts\releases\ODT-ItemInfo-4.0_<Version>.zip` only after every
gate succeeds. `-OutputDirectory` may redirect local output; there are no
version overrides or skip switches.

Hexadecimal background colors require Color Converter API 1.1.1 or newer on
every client. That external client dependency is not bundled in the server ZIP.

The repository keeps one persistent, Git-ignored SPT Test Clone at
`artifacts\spt-test-clone`. Provisioning is occasional; reset, build, install,
start, readiness detection, stop, and reporting happen on every smoke run.

## Provision the clone

Run provisioning from an Administrator Windows PowerShell 5.1 window:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\New-SptShallowClone.ps1
```

Use `-SourcePath` when SPT 4.0.13 is installed elsewhere. Provisioning copies
mutable SPT content and `EscapeFromTarkov_Data\Managed`, then creates symbolic
links for every other game-data entry. The ownership marker records source and
target paths so the smoke gate cannot resolve back to the everyday installation.

The symbolic-link layout keeps the clone's visible size near the genuinely
copied content (roughly one gigabyte for the current local installation) rather
than reporting the linked game data as another 25 GB.

## Run the smoke gate

The repeatable operation does not require administrator privileges or
PowerShell 7:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Invoke-SptSmokeTest.ps1
```

Every run:

1. validates the clone ownership marker;
2. clears all user mods and relevant SPT/game/BepInEx logs;
3. builds the current Release output;
4. installs only ODT Item Info, its license, and current configuration;
5. assigns the clone-only loopback endpoint (port `6970` by default), starts
   `SPT\SPT.Server.exe`, and waits for the SPT readiness message;
6. detects fatal, dependency, configuration, early-exit, and timeout failures;
7. stops the server and writes full build logs, authoritative SPT logs, launch
   diagnostics, and `smoke-report.json` under `artifacts\spt-smoke\<run-id>`.

Exit codes are stable: `0` ready, `10` preflight, `12` endpoint setup, `20`
build, `30` install, `40` fatal server error, `41` dependency failure, `42`
configuration failure, `43` server exited before readiness, `44` timeout, `50`
start failure, and `51` stop failure.
Use `-ServerPort` when another isolated test instance already owns port `6970`.

## Regression tests

Provisioning tests require an Administrator shell because they create symbolic
links. The smoke and build regressions do not.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-NewSptShallowClone.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-SptSmokeGate.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-BuildExcludesArtifacts.ps1
```
