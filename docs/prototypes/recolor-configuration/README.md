# Consolidated Recolor Configuration prototype

> THROWAWAY PROTOTYPE — this is not runtime configuration and is not an implementation contract yet.

This prototype asks whether one `RarityRecolor` object can clearly replace the
current recolor settings plus `tiers.json` and `tiers_hex.json`. It makes the
proposed nesting concrete so the maintainer can judge whether settings are easy
to find, edit, and explain before implementation is specified.

Run from the repository root with Windows PowerShell 5.1:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File docs/prototypes/recolor-configuration/inspect.ps1
```

The viewer keeps state in memory. Use `b` to cycle the Background Recolor Basis,
`w` to cycle the Weapon Recolor Mode, `f` to toggle the Flea Ban Warning, and `q`
to quit. It reprints the full proposed `RarityRecolor` object after every change.

## Review questions

1. Are `basis`, `display`, `tiers`, `specializedClassifiers`, `fleaBanWarning`,
   `customOverrides`, and `blacklist` the right top-level concepts?
2. Should contextual labels be configurable or fixed translation-backed runtime
   output selected by the classifier?
3. Is an item-ID-to-tier map clearer than the legacy `customRarity` name?
4. Should `bypassAmmoRecolor` and `bypassKeysRecolor` disappear in favor of the
   explicit classifier toggles and normal-basis fallback described here?

Record the maintainer verdict in this file before deleting or absorbing the
prototype.

## Verdict

Palette direction accepted by the maintainer:

- Recolor Tiers 1–6 progress through `default`, `green`, `blue`, `violet`,
  `orange`, and `red`.
- Weapon Category progresses through the same palette from lighter to heavier
  categories. Assault Carbine, Assault Rifle, and Machine Gun use `blue`;
  Marksman Rifle uses `violet`; Sniper Rifle uses `orange`; and Launcher uses
  `red`. Other category pairs may share a color.
- Flea Restricted keeps its independent contextual meaning and uses
  `tracerRed`.

The maintainer accepted the overall nesting. Contextual labels are real runtime
output selected by the classifier, but their text is fixed through translation
keys rather than repeated as configurable strings in `RarityRecolor`.
