# Weapon Category Color Map prototype

> THROWAWAY PROTOTYPE — this is a decision aid, not runtime configuration or an implementation contract.

This prototype asks which default `Weapon Category Color Map` best communicates
the agreed lighter-to-heavier firearm progression without turning category names
into Recolor Tier or warning labels. It follows the classifier and SPT 4.0.13
counts accepted in [Map SPT weapon classes to the approved recolor categories](https://github.com/mosesdasilva/ODT-ItemInfo-4.0/issues/19).

Run the terminal viewer from the repository root with PowerShell:

```powershell
powershell -NoProfile -File docs/prototypes/weapon-category-color-map/inspect.ps1
```

Use the left and right arrow keys to compare the three candidates and `q` to
quit. The viewer keeps the selection in memory and reprints the complete map
after every change.

## Candidates

### A — Shared progression (accepted with Machine Gun adjustment)

Mirrors the already accepted six-step Recolor Tier palette while keeping the
meaning contextual: `default`, `green`, `blue`, `violet`, `orange`, and `red`.
Pairs categories where the distinction remains visible in the category label.
The accepted hybrid moves Machine Gun to `blue` beside Assault Carbine and
Assault Rifle, while Sniper Rifle remains the sole `orange` category.

### B — Role grouping

Uses the same six colors but separates compact/mobile firearms from standard
long guns: Assault Carbine joins SMG in green, while Shotgun joins Assault Rifle
in blue. This emphasizes role more than a strict weight progression.

### C — Category identity

Introduces `grey` for Revolver and `yellow` for Shotgun. It improves distinction
between neighboring category labels but weakens the single, predictable
lighter-to-heavier color grammar.

All candidates keep Launcher Category on independently configurable `red`.
Flea Restricted remains a warning label with independently configurable
`tracerRed`; flare and signal weapons remain outside the category map and use
Tier 1 without a warning.

## Review question

The maintainer selected the A progression with Machine Gun moved to `blue`.

## Verdict

Accepted by the maintainer on 2026-07-12:

- Pistol and Revolver: `default`
- Submachine Gun and Shotgun: `green`
- Assault Carbine, Assault Rifle, and Machine Gun: `blue`
- Marksman Rifle: `violet`
- Sniper Rifle: `orange`
- Launcher Category: independently configurable `red`

This keeps the progression easy to read while grouping the automatic long-gun
categories under `blue`. Flea Restricted remains independently configurable as
`tracerRed`; it does not share Launcher semantics.
