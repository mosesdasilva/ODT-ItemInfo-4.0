# SPT Test Clone provisioning spike

These scripts preserve the validated Windows PowerShell 5.1 provisioning spike
that predates implementation ticket
[#25](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/25).

The spike only creates a shallow clone with copied mutable SPT content, copied
managed assemblies, and non-admin links for immutable game data. It is not the
complete SPT Test Clone smoke harness and does not satisfy ticket #25 by itself.
That ticket still owns repeatable reset, build, install, start, readiness and
failure detection, clean stop, retained logs, and machine-readable reporting.

Run the synthetic provisioning regression under Windows PowerShell 5.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\tests\Test-NewSptShallowClone.ps1
```
