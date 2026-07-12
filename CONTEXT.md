# Item Background Recoloring

This context defines how the mod assigns item background colors while preserving ODT Item Info's existing color system.

## Language

**Background Recolor Basis**:
The selected rule that determines an item's background color. It is either Trader Tier or Trader Buy Value.
_Avoid_: Rarity mode, pricing mode

**Static Recolor Pass**:
The single delayed post-database operation that classifies item templates after loaded mods have registered their items and traders. It does not recalculate from live inventory instances.
_Avoid_: Per-instance recoloring, continuous recalculation

**Specialized Classification Fallback**:
When armor, ammunition, rig, or backpack classification is enabled but its required item data is missing or invalid, log a warning and use the selected Background Recolor Basis. Do not crash or force the item to Common.
_Avoid_: Silent failure, startup failure, zero-value substitution

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
A static ammunition-only background classification derived from the ammo template's penetration value. Ammunition bypasses Trader Buy Value recoloring because its background communicates combat effectiveness rather than resale value.
_Avoid_: Ammo price tier, full-stack value

**Armor Class Tier**:
A static armor-only background classification derived from armor class rather than resale value. For armor with removable components, the class is the front plate installed in its SPT Default Armor Preset rather than its maximum compatible plate class or current per-instance contents.
_Avoid_: Armor price tier, current durability

**Armored Rig Classification**:
An armored rig is classified as armor, not as an ordinary rig. Its Armor Class Tier always takes precedence over Rig Capacity Tier; storage capacity never determines an armored rig's background. If armor-class coloring is disabled, it falls back to the selected Background Recolor Basis.
_Avoid_: Combining armor and capacity tiers, treating armored and unarmored rigs identically

**Default Front Plate Class**:
The armor class of the plate installed in the front-plate slot of an item's Default Armor Preset. Other default components, including back and side plates, do not raise or otherwise alter the item's Armor Class Tier.
_Avoid_: Highest default component, highest compatible plate

**Soft-Armor Fallback Class**:
The built-in armor class of a protective item that has no front plate in its Default Armor Preset, such as PACA armor. It supplies the Armor Class Tier only when no Default Front Plate Class exists.
_Avoid_: Compatible plate class, Trader Buy Value

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
An optional recoloring rule that marks flea-market-banned items as Overpowered. When enabled, it overrides ammunition, armor, rig, backpack, and normal basis calculations, but not an explicit Custom Rarity Override. It is disabled by default so flea restrictions do not affect trader-focused recoloring.
_Avoid_: Mandatory flea-ban override, trader restriction

**Custom Rarity Override**:
An explicit item-specific rarity assignment from ODT Item Info configuration. It has higher priority than the Flea Ban Warning and any calculated Background Recolor Basis for items eligible for recoloring.
_Avoid_: Suggested rarity, calculated tier

**Recolor Blacklist**:
ODT Item Info's existing absolute opt-out from background recoloring. A blacklisted item or category is not recolored, even when a Custom Rarity Override exists.
_Avoid_: Low-priority rule, calculated override
