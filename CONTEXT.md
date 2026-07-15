# Item Background Recoloring

This context defines how the mod assigns item background colors while preserving ODT Item Info's existing color system.

## Language

**Background Recolor Basis**:
The selected rule that determines an item's background color. It is either Trader Tier or Trader Buy Value.
_Avoid_: Rarity mode, pricing mode

**Static Recolor Pass**:
The single delayed post-database operation that classifies item templates after loaded mods have registered their items and traders. It does not recalculate from live inventory instances.
_Avoid_: Per-instance recoloring, continuous recalculation

**Recolor Configuration**:
The single user-facing set of settings that selects background recoloring modes and defines their colors, thresholds, specialized classifiers, and explicit overrides.
_Avoid_: Split tier configuration, hidden color precedence

**Release Candidate Artifact**:
The exact versioned ZIP produced by the local release command that has passed strict archive validation and is eligible for final SPT server smoke testing, in-game inspection, and local installation confirmation. Any later code or configuration change invalidates it and requires the release-readiness sequence to restart.
_Avoid_: Loose build output, repackaged ZIP, unverified artifact

**SPT Test Clone**:
The persistent, repository-local, Git-ignored shallow clone that shares immutable base-game data while isolating mutable SPT and client content. It is the sole target for automated server smoke testing, in-game inspection, and Release Candidate Artifact installation confirmation. Release tooling and verification workflows must never target the maintainer's everyday SPT installation.
_Avoid_: Full physical copy, everyday installation, play instance, optional production target

**PowerShell Compatibility Baseline**:
Every repository PowerShell script and documented command must run under the Windows PowerShell 5.1 installation included with Windows. PowerShell 7, `pwsh`, and installation of an additional PowerShell runtime are not prerequisites.
_Avoid_: PowerShell 7-only syntax, `pwsh`-only command, additional runtime installation

**Recolor Tier**:
One of exactly six neutral ordinal color bands, numbered 1 through 6. A Recolor Tier selects a color but does not by itself claim that an item is rare, valuable, powerful, or otherwise superior; contextual classifications such as Flea Restricted and Launcher own additional colors without becoming extra tiers.
_Avoid_: Universal rarity name, RPG rarity tier, Tier 7, warning tier

**Contextual Tier Label**:
The user-facing name for a Recolor Tier that identifies the classifier that produced it, such as Penetration Tier, Armor Class, Value Tier, or Capacity Tier.
_Avoid_: Rare, Epic, Legendary, Uber, Unobtainium

**Weapon Recolor Mode**:
The single mutually exclusive rule used for weapons. Inherit uses the selected Background Recolor Basis, Trader Tier forces the existing trader-availability classification for weapons, and Weapon Category assigns colors by firearm category.
_Avoid_: Overlapping weapon recolor toggles, duplicate trader-availability calculation

**Conventional Firearm**:
A primary or secondary firearm eligible for Weapon Category coloring, including pistols and revolvers as well as rifles, carbines, machine guns, submachine guns, and shotguns. It excludes melee weapons, handheld flares, signal pistols, and the separate Launcher Category.
_Avoid_: Primary weapon only, every weapon-like item

**Weapon Category Color Map**:
The configurable assignment of each Conventional Firearm category and the Launcher Category to a Color Specification. Its default progression communicates lighter through heavier firearm categories; categories may share colors and the mapping does not claim that one category is better than another.
_Avoid_: Weapon quality ladder, one unique color per category

**Launcher Category**:
The single Weapon Category covering grenade launchers and rocket launchers. It owns an independently configurable high-emphasis color and retains the contextual Launcher label without becoming a Recolor Tier. Handheld flares and signal pistols are not launchers and use the default Tier 1 color.
_Avoid_: Separate grenade and rocket tiers, restricted label for every launcher, flare launcher, launcher tier

**Color Specification**:
A user-configurable background color expressed either as a supported Tarkov color name or as an explicit hexadecimal color value.
_Avoid_: Hex-only configuration, color names stored separately from color values

**Specialized Classification Fallback**:
When armor, ammunition, rig, backpack, or Weapon Category classification is enabled but its required item data is missing, invalid, or unrecognized, log a warning and use the selected Background Recolor Basis. Do not crash or force the item to Tier 1.
_Avoid_: Silent failure, startup failure, zero-value substitution, unknown-category Tier 1

**Protective Classification Warning**:
The single actionable warning emitted for a recognized Protective Item when armor coloring is enabled and every applicable armor-class source has been exhausted without finding a valid class from 1 through 6. It identifies the item, its recognized type, the attempted sources and failures, and the Background Recolor Basis fallback. Legitimately unarmored rigs and missing optional attachments do not produce this warning.
_Avoid_: Warning per attempted source, item ID only, warning for ordinary rig, warning for optional attachment

**Tier Configuration Fallback**:
When a `tiers.json` section is missing, malformed, or not strictly ascending, log a warning and replace only that section with its built-in default cutoffs. Other valid sections remain active and server startup continues.
_Avoid_: Whole-file rejection, startup failure, silently sorting user values

**Trader Tier**:
ODT Item Info's original classification based on the trader loyalty level or barter tier through which an item is obtainable.
_Avoid_: Price tier, value tier

**Trader Buy Value**:
The highest Rouble-Normalized Offer calculated at server startup for a full-condition item template, divided by the item's Inventory Footprint. It excludes durability, remaining uses, attachments, and other per-instance state.
_Avoid_: Flea price, handbook price

**Unsellable Item**:
An item that no Eligible Trader accepts. Its Trader Buy Value is zero roubles per slot, which assigns it the Common background color.
_Avoid_: Handbook fallback, Fence-only value

**Base Weapon Value**:
The Trader Buy Value of the weapon template itself. Default-preset components and attachments are excluded from the static calculation.
_Avoid_: Assembled weapon value, preset value

**Ammo Penetration Tier**:
A static ammunition-only background classification derived from the ammo template's penetration value. Ammunition bypasses Trader Buy Value recoloring because its background communicates combat effectiveness rather than resale value. Penetration values of 60 or greater use the highest normal tier rather than the warning tier.
_Avoid_: Ammo price tier, full-stack value, warning tier for penetration overflow

**Armor Class Tier**:
A static Protective Item background classification derived from effective armor class rather than resale value. For armor with removable components, the class is the front plate installed in its SPT Default Armor Preset rather than its maximum compatible plate class or current per-instance contents. Other Protective Items use their own armor class.
_Avoid_: Body-armor-only tier, armor price tier, current durability

**Protective Item**:
Any item whose gameplay purpose includes ballistic protection, including body armor, armored rigs, helmets, armored masks or face shields, and standalone armor plates.
_Avoid_: Body armor only, plate-bearing equipment only

**Protective Item Recognition**:
The structural rule that recognizes built-in and modded Protective Items through descent from SPT protective base classes or, for a rig, the presence of valid default armor data. Recognition does not use item-ID allowlists. A rig without armor data remains an unarmored rig eligible for Rig Capacity Tier.
_Avoid_: Item-ID database, every rig is armored, built-in items only

**Armored Rig Classification**:
An armored rig is classified as armor, not as an ordinary rig. Its Armor Class Tier always takes precedence over Rig Capacity Tier; storage capacity never determines an armored rig's background. If armor-class coloring is disabled, it falls back to the selected Background Recolor Basis.
_Avoid_: Combining armor and capacity tiers, treating armored and unarmored rigs identically

**Default Front Plate Class**:
The armor class of the plate installed in the front-plate slot of an item's Default Armor Preset. Other default components, including back and side plates, do not raise or otherwise alter the item's Armor Class Tier.
_Avoid_: Highest default component, highest compatible plate

**Root-Slot Default Front Plate Class**:
The armor class of the default plate declared directly by a Protective Item's front-plate slot. It supplies the Armor Class Tier only when no Default Front Plate Class is available from a Default Armor Preset.
_Avoid_: Highest compatible plate, optional plate, live equipped plate

**Soft-Armor Fallback Class**:
The built-in armor class of a protective item that has no default front plate, such as PACA armor. It supplies the Armor Class Tier only when neither a Default Front Plate Class nor a Root-Slot Default Front Plate Class exists.
_Avoid_: Compatible plate class, Trader Buy Value

**Helmet Effective Armor Class**:
The highest valid armor class among a helmet's intrinsic, required default protective components, such as its top, back, ears, or nape protection. Optional attachments such as face shields and appliques do not raise the helmet's Armor Class Tier; each separately listed protective attachment uses its own armor class.
_Avoid_: Highest optional attachment, compatible face shield class, assembled helmet class

**Direct Protective Armor Class**:
The valid root armor class of a standalone plate, mask, face shield, applique, or other non-container Protective Item. It is authoritative for that item. A helmet uses its direct root class when valid and derives its Helmet Effective Armor Class only when the root class is absent, zero, or invalid. Modular armor and armored rigs instead use their approved front-plate and soft-armor precedence.
_Avoid_: Compatible component class, assembled equipment class, direct soft class overriding front plate

**Default Armor Preset**:
SPT's standard assembled configuration for an armor or armored rig, including its shipped plates and soft-armor components. It is retrieved by root item template through SPT's preset helper.
_Avoid_: Highest compatible plate, live equipped plates

**Container Storage Capacity**:
The total physical storage space inside a rig or backpack, calculated by summing `cellsH * cellsV` for every direct internal grid. Grid shape and item-type restrictions do not reduce the count. It is distinct from the number of external cells the item occupies.
_Avoid_: Inventory Footprint, trader value

**Rig Capacity Tier**:
One of ODT's six normal background colors assigned only to an unarmored rig from its Container Storage Capacity using five inclusive upper-bound cutoffs: 8, 12, 16, 20, and 24 cells.
_Avoid_: Five colors, external footprint, Trader Buy Value

**Backpack Capacity Tier**:
One of ODT's six normal background colors assigned to a backpack from its Container Storage Capacity using five inclusive upper-bound cutoffs: 12, 20, 25, 30, and 40 cells.
_Avoid_: Rig Capacity Tier, external footprint, Trader Buy Value

**Inventory Footprint**:
The number of inventory cells occupied by an item, calculated as width multiplied by height.
_Avoid_: Internal container capacity, total value

**Eligible Trader**:
Any loaded trader, including a modded trader, that accepts the item and does not explicitly prohibit it. Fence is never an Eligible Trader.
_Avoid_: First trader, Fence

**Rouble-Normalized Offer**:
A trader's purchase offer expressed in comparable roubles. Non-rouble offers, including Peacekeeper's dollar offers, use the currency exchange value stored in the game's handbook at server startup.
_Avoid_: Hard-coded exchange value, raw dollar offer

**Flea Ban Warning**:
An optional recoloring rule that marks flea-market-banned items with the Flea Restricted contextual label and its own independently configurable warning color, which defaults to `tracerRed`. In precedence order, the Recolor Blacklist and Custom Rarity Override remain above it, while ammunition, armor, rig, backpack, weapon, and normal basis classifications remain below it. It is disabled by default so flea restrictions do not affect trader-focused recoloring.
_Avoid_: Mandatory flea-ban override, Overpowered tier, trader restriction, shared tier color

**Flea Restricted**:
The user-facing contextual label produced by the Flea Ban Warning. It identifies a flea-market restriction without claiming that the item is powerful, rare, or part of Recolor Tier 6, and appears only through the same configured contextual-label display used by other classifications.
_Avoid_: Overpowered, Tier 7, highest tier, forced warning badge

**Custom Rarity Override**:
An explicit item-specific rarity assignment from ODT Item Info configuration. It has higher priority than the Flea Ban Warning and any calculated Background Recolor Basis for items eligible for recoloring.
_Avoid_: Suggested rarity, calculated tier

**Recolor Blacklist**:
ODT Item Info's existing absolute opt-out from background recoloring. A blacklisted item or category is not recolored, even when a Custom Rarity Override exists.
_Avoid_: Low-priority rule, calculated override
