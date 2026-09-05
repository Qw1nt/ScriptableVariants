using DCFApixels.ScriptableVariants.Editor;
using Sirenix.OdinInspector.Editor;
using UnityEditor;

namespace DCFApixels.ScriptableVariants.OdinInspector.Editor
{
    [CustomEditor(typeof(ScriptableVariant), editorForChildClasses: true)]
    internal sealed class ScriptableVariantOdinEditor : OdinEditor
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
    }
}
