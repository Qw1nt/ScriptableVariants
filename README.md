# Scriptable Variants

`ScriptableVariants` adds single-parent value inheritance and per-property overrides to
Unity `ScriptableObject` assets. It ships a built-in Inspector and optional integrations for
Odin Inspector and Tri Inspector 2.

## Requirements and installation

- Unity 6000.0 or newer.
- Optional: Odin Inspector 3.x or newer, or Tri Inspector 2 at commit
  `f3239650e307275edd06c25e7cda1fdc7207f5b5`.

Add the package to the consuming project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.dcfapixels.scriptable-variants": "https://github.com/DCFApixels/ScriptableVariants.git#v0.1.2"
  }
}
```

Alternatively, use **Package Manager → Add package from git URL** with
`https://github.com/DCFApixels/ScriptableVariants.git#v0.1.2`.
Authentication must already be configured for the private repository's HTTPS or SSH URL.

Tri Inspector is not a package dependency. To use it, add
`"com.codewriter.triinspector": "https://github.com/codewriter-packages/Tri-Inspector.git#f3239650e307275edd06c25e7cda1fdc7207f5b5"`
to the same manifest; the integration activates automatically.

## Quick start

```csharp
using DCFApixels.ScriptableVariants;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon Config")]
public sealed class WeaponConfig : ScriptableVariant
{
    [SerializeField, Range(0, 100)]
    private float _damage = 10;

    [SerializeField]
    private WeaponVisuals _visuals;

    public float Damage => _damage;
}
```

Deriving directly from `ScriptableVariant` is the primary API and keeps regular C# inheritance
simple. `ScriptableVariant<TSelf>` remains available as an optional convenience when a strongly
typed `Parent` property is preferred:

```csharp
public abstract class WeaponConfigBase : ScriptableVariant
{
    // Shared serialized fields and behavior.
}

public sealed class RifleConfig : WeaponConfigBase
{
    // Rifle-specific fields and behavior.
}
```

The generic convenience remains available when no intermediate C# base class is needed:

```csharp
public sealed class SimpleConfig : ScriptableVariant<SimpleConfig>
{
}
```

Create assets normally and assign another asset of the exact same type to **Parent** in the
native Inspector header. The same header shows the inheritance chain and the **Actions** menu.
A child reads all values from its parent. A thin blue line marks a local override; a softer blue
line on a container means that it contains overridden child fields. Locally controlled property
labels and displayed field values use bold text.

When **Parent** is assigned or changed, the asset's current effective values are compared with
the new parent's values. Every difference becomes a local override, while existing overrides
remain. Equal properties continue inheriting from the new parent; `[VariantLocal]` fields are
kept local and are not added to the override list.

Editing an inherited property automatically creates an override while preserving the rest of
the inherited data. Right-click an overridden property or its left gutter to open the variant
actions. **Apply to Parent** moves the local value to the immediate parent. **Revert** discards
the local value and restores the value from the nearest ancestor. The same menu can explicitly
create an override without changing its value. **Actions → Flatten** removes the parent while
preserving all currently effective values.

## Runtime contract

Inherited values are materialized into the child object once, in `OnEnable`. Changing a parent
re-materializes every loaded descendant immediately, so plain field reads stay current, like
prefab variants. Descendants that are not loaded resolve when they load. Reflection and deep
copies occur only while resolving; normal field/property reads do not walk the parent chain.

A change propagates when it is made through the package's Inspector editors, through the editor
actions and context menu, through Undo/Redo while the asset's Inspector is open, or when code
calls `InvalidateResolvedData()` after writing fields directly. The package does not use
`OnValidate`, so asset import, domain reload, and idle Inspector repaints do no extra work.
Editor scripts that write variant fields must call `InvalidateResolvedData()` themselves.

Call `EnsureResolved()` only when values are read before `OnEnable` has run. The call is
allocation-free and returns immediately when nothing changed.

Saving a parent asset also saves its loaded descendants, so the materialized values stored in
their files do not go stale. Descendants that are not loaded update their files when they are
next loaded and saved; their values in memory are always current.

If a derived class implements `OnEnable` or `OnDisable`, it must override the protected base
method and call `base` so materialization and change propagation remain active.

## Override boundaries

- Inline `[Serializable]` classes and structs support leaf-field overrides.
- Arrays and `List<T>` are overridden as a whole collection.
- `[SerializeReference]` values are overridden as a whole managed reference.
- Unity object references and built-in Unity values are overridden as a whole value.
- Add `[VariantLocal]` to a serialized field that must always remain local.
- Parent and child assets must have exactly the same concrete type, unless the class carries
  `[VariantTypeSelection(typeof(T))]`. Then any asset assignable to `T` is accepted and offered
  by the **Parent** picker, and the attribute is inherited by subclasses. Fields the chosen parent
  type does not declare stay local on the child.
- A cyclic parent chain is rejected by the Inspector and guarded against at runtime.

Override identifiers use Unity property paths. Fields renamed with `[FormerlySerializedAs]`
are remapped automatically. Unknown paths are reported in the Inspector and can be removed
with **Remove Orphans**.

## Inspector integrations

The Inspector is selected automatically at compile time:

| Installed                    | Editor used                                              |
|------------------------------|----------------------------------------------------------|
| Neither                      | Built-in IMGUI editor                                    |
| Odin Inspector               | `OdinEditor` with an Odin value drawer for the gutter    |
| Tri Inspector 2              | `TriEditor` with a Tri attribute drawer for the gutter   |
| Odin Inspector and Tri Inspector | Odin. Add the `SCRIPTABLE_VARIANTS_PREFER_TRI` scripting define to use Tri instead. |

Odin is detected through its `ODIN_INSPECTOR` define; Tri through the `com.codewriter.triinspector`
package version. Every editor shares the same header, override gutter, bold labels, automatic
override creation on edit, and context actions.

Variant actions are added to Unity's property context menu and to Odin's property context menu.
The blue override gutter has the same context menu as a fallback for custom controls that consume
the field event.

The built-in editor draws override bars for top-level properties only; nested fields are still
overridden individually and can be reverted or applied from the property context menu.

The Tri integration wraps Tri Inspector's existing visual-element drawer chain and targets the
pinned Tri Inspector commit above so preview API changes cannot silently break its editor
bindings. The Odin integration wraps Odin's drawer chain the same way, so groups, validation,
conditionals, and custom drawers keep rendering the actual value field.

## Sample

A ready-made three-level weapon configuration chain is available from the package details under
**Samples → Weapon Configuration Demo → Import**. See
[`Samples~/Demo/README.md`](Samples~/Demo/README.md) for its inherited and overridden fields
and a short Inspector walkthrough.
