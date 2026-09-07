using System.Reflection;
using DCFApixels.ScriptableVariants.Editor;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.OdinInspector.Editor
{
    /// <summary>
    /// Wraps every inheritable field of a Scriptable Variant with the override gutter, bold labels,
    /// automatic override creation, and the variant context menu entries.
    /// </summary>
    [DrawerPriority(0, 1000, 0)]
    public sealed class VariantPropertyOdinDrawer<T> : OdinValueDrawer<T>, IDefinesGenericMenuItems
    {
        private ScriptableVariant _variant;
        private string _propertyPath;

        protected override bool CanDrawValueProperty(InspectorProperty property)
        {
            return property.Tree.WeakTargets.Count == 1 &&
                   property.Tree.WeakTargets[0] is ScriptableVariant variant &&
                   property.Info.GetMemberInfo() is FieldInfo &&
                   VariantSerialization.IsKnownPath(variant.GetType(), property.UnityPropertyPath);
        }

        protected override void Initialize()
        {
            _variant = (ScriptableVariant) Property.Tree.WeakTargets[0];
            _propertyPath = Property.UnityPropertyPath;

            ValueEntry.OnValueChanged += OnValueChanged;
            if (VariantSerialization.IsAtomicOverridePath(_variant.GetType(), _propertyPath))
            {
                // Leaves of inline composites carry their own drawer. Atomic values (collections, managed
                // references, Unity structs) only report nested edits through the parent property.
                ValueEntry.OnChildValueChanged += OnValueChanged;
                if (Property.ChildResolver is ICollectionResolver collectionResolver)
                {
                    collectionResolver.OnAfterChange += OnCollectionChanged;
                }
            }
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            var bold = _variant.HasParent && _variant.IsLocallyControlled(_propertyPath);
            GUIHelper.PushIsBoldLabel(bold);
            var rect = EditorGUILayout.BeginVertical();
            try
            {
                CallNextDrawer(label);
            }
            finally
            {
                EditorGUILayout.EndVertical();
                GUIHelper.PopIsBoldLabel();
            }

            ScriptableVariantGUI.DrawOverrideBar(rect, _variant, _propertyPath);
        }

        public void PopulateGenericMenu(InspectorProperty property, GenericMenu genericMenu)
        {
            ScriptableVariantContextMenu.Populate(genericMenu, _variant, _propertyPath);
        }

        private void OnValueChanged(int targetIndex)
        {
            ScriptableVariantAssetUtility.OverrideChangedValues(_variant, _propertyPath);
        }

        private void OnCollectionChanged(CollectionChangeInfo info)
        {
            ScriptableVariantAssetUtility.OverrideChangedValues(_variant, _propertyPath);
        }
    }
}
