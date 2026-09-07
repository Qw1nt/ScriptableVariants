using System;
using System.Collections.Generic;
using DCFApixels.ScriptableVariants.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.Tests
{
    public sealed class ScriptableVariantTests
    {
        private readonly List<ScriptableObject> _created = new List<ScriptableObject>();

        [TearDown]
        public void TearDown()
        {
            for (var i = 0; i < _created.Count; i++)
            {
                UnityEngine.Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void ChildInheritsScalarFromParent()
        {
            var parent = CreateVariant();
            parent.SetNumber(12);

            var child = CreateVariant();
            child.EditorSetParent(parent);

            Assert.That(child.Number, Is.EqualTo(12));
            Assert.That(child.GetValueSource("_number"), Is.SameAs(parent));
        }

        [Test]
        public void AssigningParentKeepsDifferentValuesAsOverrides()
        {
            var parent = CreateVariant();
            parent.SetNumber(12);
            parent.SetNested(5, "parent");
            parent.SetValues(1, 2, 3);
            parent.SetLocalNote("parent note");

            var child = CreateVariant();
            child.SetNumber(37);
            child.SetNested(5, "child");
            child.SetValues(7, 8);
            child.SetLocalNote("child note");

            Assert.That(ScriptableVariantAssetUtility.SetParent(child, parent, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(child.Number, Is.EqualTo(37));
            Assert.That(child.NestedAmount, Is.EqualTo(5));
            Assert.That(child.NestedLabel, Is.EqualTo("child"));
            Assert.That(child.Values, Is.EqualTo(new[] {7, 8}));
            Assert.That(child.LocalNote, Is.EqualTo("child note"));
            Assert.That(child.IsOverridden("_number"), Is.True);
            Assert.That(child.IsOverridden("_nested._amount"), Is.False);
            Assert.That(child.IsOverridden("_nested._label"), Is.True);
            Assert.That(child.IsOverridden("_values"), Is.True);
            Assert.That(child.IsOverridden("_localNote"), Is.False);

            parent.SetNumber(99);
            parent.SetNested(8, "updated");
            parent.SetValues(9);
            parent.SetLocalNote("updated parent note");

            Assert.That(child.Number, Is.EqualTo(37));
            Assert.That(child.NestedAmount, Is.EqualTo(8));
            Assert.That(child.NestedLabel, Is.EqualTo("child"));
            Assert.That(child.Values, Is.EqualTo(new[] {7, 8}));
            Assert.That(child.LocalNote, Is.EqualTo("child note"));
        }

        [Test]
        public void ChangingParentKeepsExistingOverridesAndAddsNewDifferences()
        {
            var firstParent = CreateVariant();
            firstParent.SetNumber(10);
            firstParent.SetNested(1, "first");

            var secondParent = CreateVariant();
            secondParent.SetNumber(20);
            secondParent.SetNested(1, "local");

            var child = CreateVariant();
            child.EditorSetParent(firstParent);
            child.EditorSetOverride("_nested._label", true);
            child.SetNested(1, "local");

            Assert.That(ScriptableVariantAssetUtility.SetParent(child, secondParent, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(child.Number, Is.EqualTo(10));
            Assert.That(child.NestedAmount, Is.EqualTo(1));
            Assert.That(child.NestedLabel, Is.EqualTo("local"));
            Assert.That(child.IsOverridden("_number"), Is.True);
            Assert.That(child.IsOverridden("_nested._amount"), Is.False);
            Assert.That(child.IsOverridden("_nested._label"), Is.True);

            secondParent.SetNumber(99);
            secondParent.SetNested(2, "updated");

            Assert.That(child.Number, Is.EqualTo(10));
            Assert.That(child.NestedAmount, Is.EqualTo(2));
            Assert.That(child.NestedLabel, Is.EqualTo("local"));
        }

        [Test]
        public void LocalOverrideSurvivesParentChange()
        {
            var parent = CreateVariant();
            parent.SetNumber(12);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_number", true);
            child.SetNumber(37);

            parent.SetNumber(99);

            Assert.That(child.Number, Is.EqualTo(37));
            Assert.That(child.GetValueSource("_number"), Is.SameAs(child));
        }

        [Test]
        public void EnablingExistingOverrideKeepsItActive()
        {
            var parent = CreateVariant();
            parent.SetNumber(12);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_number", true);
            child.SetNumber(37);

            child.EditorSetOverride("_number", true);
            parent.SetNumber(99);

            Assert.That(child.IsOverridden("_number"), Is.True);
            Assert.That(child.Number, Is.EqualTo(37));
        }

        [Test]
        public void NestedLeafCanOverrideIndependently()
        {
            var parent = CreateVariant();
            parent.SetNested(5, "parent");

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_nested._amount", true);
            child.SetNestedAmount(42);

            parent.SetNested(8, "updated");

            Assert.That(child.NestedAmount, Is.EqualTo(42));
            Assert.That(child.NestedLabel, Is.EqualTo("updated"));
        }

        [Test]
        public void CollectionIsOverriddenAsOneValue()
        {
            var parent = CreateVariant();
            parent.SetValues(1, 2, 3);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            Assert.That(child.Values, Is.EqualTo(new[] {1, 2, 3}));

            child.EditorSetOverride("_values", true);
            child.SetValues(7, 8);
            parent.SetValues(9);

            Assert.That(child.Values, Is.EqualTo(new[] {7, 8}));
        }

        [Test]
        public void RevertingSubtreeRestoresInheritedValue()
        {
            var parent = CreateVariant();
            parent.SetNested(3, "parent");

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_nested._amount", true);
            child.SetNestedAmount(14);

            child.EditorSetOverride("_nested", false);

            Assert.That(child.NestedAmount, Is.EqualTo(3));
            Assert.That(child.OverridePaths, Is.Empty);
        }

        [Test]
        public void ApplyOverrideMovesValueToImmediateParent()
        {
            var parent = CreateVariant();
            parent.SetNumber(12);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_number", true);
            child.SetNumber(37);

            Assert.That(ScriptableVariantAssetUtility.ApplyToParent(child, "_number"), Is.True);
            Assert.That(parent.Number, Is.EqualTo(37));
            Assert.That(child.Number, Is.EqualTo(37));
            Assert.That(child.IsOverridden("_number"), Is.False);
        }

        [Test]
        public void ApplyOverrideKeepsOverrideOnIntermediateParent()
        {
            var root = CreateVariant();
            root.SetNested(3, "root");

            var parent = CreateVariant();
            parent.EditorSetParent(root);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_nested._amount", true);
            child.SetNestedAmount(42);

            Assert.That(ScriptableVariantAssetUtility.ApplyToParent(child, "_nested._amount"), Is.True);
            Assert.That(root.NestedAmount, Is.EqualTo(3));
            Assert.That(parent.NestedAmount, Is.EqualTo(42));
            Assert.That(parent.IsOverridden("_nested._amount"), Is.True);
            Assert.That(child.NestedAmount, Is.EqualTo(42));
            Assert.That(child.IsOverridden("_nested._amount"), Is.False);
        }

        [Test]
        public void ApplyContainerMovesAllNestedOverrides()
        {
            var parent = CreateVariant();
            parent.SetNested(3, "parent");

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_nested._amount", true);
            child.EditorSetOverride("_nested._label", true);
            child.SetNested(14, "child");

            Assert.That(ScriptableVariantAssetUtility.ApplyToParent(child, "_nested"), Is.True);
            Assert.That(parent.NestedAmount, Is.EqualTo(14));
            Assert.That(parent.NestedLabel, Is.EqualTo("child"));
            Assert.That(child.OverridePaths, Is.Empty);
        }

        [Test]
        public void RevertOnNestedFieldRemovesOwningOverride()
        {
            var parent = CreateVariant();
            parent.SetNested(3, "parent");

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorSetOverride("_nested", true);
            child.SetNested(14, "child");

            ScriptableVariantAssetUtility.Revert(child, "_nested._amount");

            Assert.That(child.NestedAmount, Is.EqualTo(3));
            Assert.That(child.NestedLabel, Is.EqualTo("parent"));
            Assert.That(child.OverridePaths, Is.Empty);
        }

        [Test]
        public void FlattenKeepsEffectiveValuesAndRemovesParent()
        {
            var parent = CreateVariant();
            parent.SetNumber(21);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            child.EditorFlatten();

            parent.SetNumber(55);

            Assert.That(child.Parent, Is.Null);
            Assert.That(child.Number, Is.EqualTo(21));
            Assert.That(child.OverridePaths, Is.Empty);
        }

        [Test]
        public void ParentAssignmentRejectsCycle()
        {
            var root = CreateVariant();
            var child = CreateVariant();
            child.EditorSetParent(root);

            Assert.That(root.CanAssignParent(child, out var error), Is.False);
            Assert.That(error, Does.Contain("cycle"));
        }

        [Test]
        public void ParentMustHaveTheSameConcreteTypeEvenWithSharedBase()
        {
            var enemy = CreateVariant();
            var character = CreateVariant<OtherTestVariant>();

            Assert.That(character.CanAssignParent(enemy, out var error), Is.False);
            Assert.That(error, Does.Contain("exact type"));
            Assert.That(ScriptableVariantAssetUtility.SetParent(character, enemy, out error), Is.False);
            Assert.That(character.Parent, Is.Null);
            Assert.That(enemy.CanAssignParent(CreateVariant(), out error), Is.True);
        }

        [Test]
        public void VariantTypeSelectionAllowsAssignableParentsAndKeepsUnsharedFieldsLocal()
        {
            var parent = CreateVariant<FamilyTestVariant>();
            parent.SetNumber(12);

            var child = CreateVariant<FamilyChildTestVariant>();
            child.SetExtra(7);

            Assert.That(child.ParentType, Is.EqualTo(typeof(FamilyTestVariant)));
            Assert.That(ScriptableVariantAssetUtility.SetParent(child, parent, out var error), Is.True);
            Assert.That(error, Is.Null);
            Assert.That(child.Number, Is.EqualTo(12));
            Assert.That(child.Extra, Is.EqualTo(7));
            Assert.That(child.IsOverridden("_extra"), Is.False);

            parent.SetNumber(99);
            child.SetExtra(8);

            Assert.That(child.Number, Is.EqualTo(99));
            Assert.That(child.Extra, Is.EqualTo(8));

            var reversed = CreateVariant<FamilyTestVariant>();
            Assert.That(reversed.CanAssignParent(child, out error), Is.True);
            Assert.That(CreateVariant().CanAssignParent(parent, out error), Is.False);
        }

        [Test]
        public void GenericConvenienceBaseStillProvidesTypedParent()
        {
            var parent = CreateVariant<TypedTestVariant>();
            var child = CreateVariant<TypedTestVariant>();

            child.EditorSetParent(parent);

            TypedTestVariant typedParent = child.Parent;
            Assert.That(typedParent, Is.SameAs(parent));
        }

        [Test]
        public void OverrideChangedValuesOverridesOnlyDifferingLeaves()
        {
            var parent = CreateVariant();
            parent.SetNested(5, "parent");

            var child = CreateVariant();
            child.EditorSetParent(parent);

            using (var serializedChild = new SerializedObject(child))
            {
                serializedChild.FindProperty("_nested._label").stringValue = "child";
                serializedChild.ApplyModifiedProperties();
            }

            ScriptableVariantAssetUtility.OverrideChangedValues(child, "_nested");

            Assert.That(child.IsOverridden("_nested._label"), Is.True);
            Assert.That(child.IsOverridden("_nested._amount"), Is.False);
            Assert.That(child.NestedLabel, Is.EqualTo("child"));

            parent.SetNested(8, "updated");

            Assert.That(child.NestedAmount, Is.EqualTo(8));
            Assert.That(child.NestedLabel, Is.EqualTo("child"));
        }

        [Test]
        public void ParentChangesPropagateToLoadedDescendantsImmediately()
        {
            var parent = CreateVariant();
            parent.SetNumber(1);

            var child = CreateVariant();
            child.EditorSetParent(parent);
            var grandchild = CreateVariant();
            grandchild.EditorSetParent(child);

            parent.SetNumber(2);

            Assert.That(child.RawNumber, Is.EqualTo(2));
            Assert.That(grandchild.RawNumber, Is.EqualTo(2));

            child.EditorSetOverride("_number", true);
            child.SetNumber(5);

            Assert.That(grandchild.RawNumber, Is.EqualTo(5));

            ScriptableVariantAssetUtility.Revert(child, "_number");

            Assert.That(child.RawNumber, Is.EqualTo(2));
            Assert.That(grandchild.RawNumber, Is.EqualTo(2));
        }

        [Test]
        public void RootEditPropagatesThroughOverrideChangedValuesWithoutOnValidate()
        {
            var parent = CreateVariant();
            parent.SetNumber(1);

            var child = CreateVariant();
            child.EditorSetParent(parent);

            using (var serializedParent = new SerializedObject(parent))
            {
                serializedParent.FindProperty("_number").intValue = 3;
                serializedParent.ApplyModifiedProperties();
            }

            Assert.That(child.RawNumber, Is.EqualTo(1));

            ScriptableVariantAssetUtility.OverrideChangedValues(parent, "_number");

            Assert.That(child.RawNumber, Is.EqualTo(3));
            Assert.That(parent.OverridePaths, Is.Empty);
        }

        [Test]
        public void GetLoadedDescendantsReturnsDescendantsOnly()
        {
            var parent = CreateVariant();
            var child = CreateVariant();
            var grandchild = CreateVariant();
            var sibling = CreateVariant();
            child.EditorSetParent(parent);
            grandchild.EditorSetParent(child);

            Assert.That(parent.GetLoadedDescendants(), Is.EquivalentTo(new ScriptableVariant[] {child, grandchild}));
            Assert.That(child.GetLoadedDescendants(), Is.EquivalentTo(new ScriptableVariant[] {grandchild}));
            Assert.That(grandchild.IsDescendantOf(parent), Is.True);
            Assert.That(sibling.IsDescendantOf(parent), Is.False);
            Assert.That(parent.IsDescendantOf(parent), Is.False);
        }

        private TestVariant CreateVariant()
        {
            return CreateVariant<TestVariant>();
        }

        private T CreateVariant<T>() where T : ScriptableObject
        {
            var variant = ScriptableObject.CreateInstance<T>();
            _created.Add(variant);
            return variant;
        }

    }

    [Serializable]
    public sealed class ScriptableVariantTestNestedData
    {
        [SerializeField]
        private int _amount;

        [SerializeField]
        private string _label;

        public int Amount => _amount;
        public string Label => _label;

        public void Set(int amount, string label)
        {
            _amount = amount;
            _label = label;
        }

        public void SetAmount(int amount)
        {
            _amount = amount;
        }
    }

    public abstract class TestVariantBase : ScriptableVariant
    {
        [SerializeField]
        private int _number;

        [SerializeField]
        private ScriptableVariantTestNestedData _nested = new ScriptableVariantTestNestedData();

        [SerializeField]
        private List<int> _values = new List<int>();

        [SerializeField, VariantLocal]
        private string _localNote;

        public int Number
        {
            get
            {
                EnsureResolved();
                return _number;
            }
        }

        public int NestedAmount
        {
            get
            {
                EnsureResolved();
                return _nested.Amount;
            }
        }

        public string NestedLabel
        {
            get
            {
                EnsureResolved();
                return _nested.Label;
            }
        }

        public IReadOnlyList<int> Values
        {
            get
            {
                EnsureResolved();
                return _values;
            }
        }

        public string LocalNote => _localNote;

        /// <summary>Reads the field without resolving, to observe eager propagation.</summary>
        public int RawNumber => _number;

        public void SetNumber(int value)
        {
            _number = value;
            EditorNotifyValuesChanged();
        }

        public void SetNested(int amount, string label)
        {
            _nested.Set(amount, label);
            EditorNotifyValuesChanged();
        }

        public void SetNestedAmount(int amount)
        {
            _nested.SetAmount(amount);
            EditorNotifyValuesChanged();
        }

        public void SetValues(params int[] values)
        {
            _values = new List<int>(values);
            EditorNotifyValuesChanged();
        }

        public void SetLocalNote(string value)
        {
            _localNote = value;
            EditorNotifyValuesChanged();
        }
    }

    public sealed class TestVariant : TestVariantBase
    {
    }

    public sealed class OtherTestVariant : TestVariantBase
    {
    }

    [VariantTypeSelection(typeof(FamilyTestVariant))]
    public class FamilyTestVariant : TestVariantBase
    {
    }

    public sealed class FamilyChildTestVariant : FamilyTestVariant
    {
        [SerializeField]
        private int _extra;

        public int Extra
        {
            get
            {
                EnsureResolved();
                return _extra;
            }
        }

        public void SetExtra(int value)
        {
            _extra = value;
            EditorNotifyValuesChanged();
        }
    }

    public sealed class TypedTestVariant : ScriptableVariant<TypedTestVariant>
    {
    }
}
