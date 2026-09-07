#if !ODIN_INSPECTOR && !DCFA_SCRIPTABLE_VARIANTS_TRI_2
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.Editor
{
    /// <summary>Built-in Inspector used when neither Odin Inspector nor Tri Inspector is installed.</summary>
    [CustomEditor(typeof(ScriptableVariant), editorForChildClasses: true)]
    internal sealed class ScriptableVariantEditor : UnityEditor.Editor
    {
        private ScriptableVariantHeader _header;

        private void OnEnable()
        {
            _header = new ScriptableVariantHeader(this);
        }

        private void OnDisable()
        {
            _header.Dispose();
        }

        protected override void OnHeaderGUI()
        {
            base.OnHeaderGUI();
            _header.Draw();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var variant = (ScriptableVariant) target;
            var variantType = variant.GetType();
            string changedPath = null;

            // ponytail: override bars are drawn for top-level rows only; nested rows rely on the parent row
            // state and Unity's property context menu. Recurse into Generic properties if per-leaf bars matter.
            var property = serializedObject.GetIterator();
            for (var enterChildren = true; property.NextVisible(enterChildren); enterChildren = false)
            {
                var propertyPath = property.propertyPath;
                var isVariantProperty = variant.HasParent &&
                                        VariantSerialization.IsKnownPath(variantType, propertyPath);
                using (new EditorGUI.DisabledScope(propertyPath == "m_Script"))
                {
                    ScriptableVariantGUI.SetBoldDefaultFont(
                        isVariantProperty && variant.IsLocallyControlled(propertyPath));
                    EditorGUI.BeginChangeCheck();
                    try
                    {
                        EditorGUILayout.PropertyField(property, true);
                    }
                    finally
                    {
                        ScriptableVariantGUI.SetBoldDefaultFont(false);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        changedPath = propertyPath;
                    }
                }

                if (isVariantProperty)
                {
                    ScriptableVariantGUI.DrawOverrideBar(GUILayoutUtility.GetLastRect(), variant, propertyPath);
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (changedPath != null)
            {
                ScriptableVariantAssetUtility.OverrideChangedValues(variant, changedPath);
            }
        }
    }
}
#endif
