using DCFApixels.ScriptableVariants.Editor;
using TriInspector.Editors;
using UnityEditor;
using UnityEngine.UIElements;

namespace DCFApixels.ScriptableVariants.TriInspector.Editor
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(ScriptableVariant), editorForChildClasses: true)]
    internal sealed class ScriptableVariantTriEditor : TriEditor
    {
        private ScriptableVariantHeader _header;

        protected override void OnEnable()
        {
            _header = new ScriptableVariantHeader(this);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            _header.Dispose();
            base.OnDisable();
        }

        protected override void OnHeaderGUI()
        {
            base.OnHeaderGUI();
            _header.Draw();
        }

        public override VisualElement CreateInspectorGUI()
        {
            if (targets.Length == 1)
            {
                return base.CreateInspectorGUI();
            }

            var root = new VisualElement();
            root.Add(new HelpBox(
                "Multi-object editing is disabled for Scriptable Variants because selected assets can have different inheritance sources.",
                HelpBoxMessageType.Info));
            var disabledInspector = base.CreateInspectorGUI();
            disabledInspector.SetEnabled(false);
            root.Add(disabledInspector);
            return root;
        }
    }
}
