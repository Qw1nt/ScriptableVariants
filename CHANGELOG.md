# Changelog

All notable changes to this package are documented in this file.

## Unreleased

- Removed the hard dependency on Tri Inspector. The Tri integration now compiles only when
  Tri Inspector 2 is installed.
- Added an Odin Inspector integration with the same header, override gutter, bold labels,
  automatic overrides on edit, and context actions.
- Added a built-in IMGUI editor used when neither Odin Inspector nor Tri Inspector is installed.
- When both Odin and Tri are installed, Odin is used; the `SCRIPTABLE_VARIANTS_PREFER_TRI`
  scripting define selects Tri instead.
- Added `ScriptableVariantAssetUtility.OverrideChangedValues` for custom editors that need
  automatic overrides after a value change.

## 0.1.2 - 2026-09-04

- Made non-generic `ScriptableVariant` the primary API while retaining
  `ScriptableVariant<TSelf>` as an optional typed convenience base.

## 0.1.1 - 2026-09-04

- Moved the parent selector, inheritance chain, and actions into Unity's native Inspector header.
- Assigning or changing **Parent** now keeps existing overrides and automatically overrides
  every serialized property whose current value differs from the new parent.
- Replaced the header action-button row with a compact **Actions** menu and removed
  **Create Child** from the Inspector.
- Replaced override buttons with compact Unity-style blue gutter bars.
- Added property and gutter context actions for overriding, applying to the parent, and reverting.
- Editing an inherited property now creates its override automatically.
- Override bars now align with the actual property row below Tri Inspector decorators.
- Locally controlled property labels and field values are displayed in bold.
- Prevented Unity `Header` and `Space` decorators, including those on `[VariantLocal]` fields,
  from being drawn twice when another attribute makes Tri Inspector use Unity's native property
  handler.
- Refactored override mutations and serialized path resolution to remove duplicate work.
- Fixed the **Actions** menu anchor in the Inspector header.
- Packaged the weapon configuration demo as an importable Unity Package Manager sample.

## 0.1.0 - 2026-09-04

- Added single-parent ScriptableObject inheritance.
- Added per-property and nested-field overrides.
- Added atomic collection and managed-reference overrides.
- Added `[VariantLocal]`, cycle protection, flattening, and orphan-path cleanup.
- Added Tri Inspector 2 integration.
- Added editor tests and a three-level weapon configuration demo.
