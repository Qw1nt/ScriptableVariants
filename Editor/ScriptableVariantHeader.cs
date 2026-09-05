using System;
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.Editor
{
    /// <summary>
    /// Draws the parent selector, inheritance chain, warnings, and the Actions menu below the native Inspector header.
    /// Shared by the built-in, Odin Inspector, and Tri Inspector editors.
    /// </summary>
    internal sealed class ScriptableVariantHeader
    {
        private static readonly GUIContent ParentLabel = new GUIContent("Parent");
        private static readonly GUIContent ActionsLabel = new GUIContent(
            "Actions",
            "Scriptable Variant actions");
        private static readonly GUIContent OverrideAllLabel = new GUIContent("Override All");
        private static readonly GUIContent RevertAllLabel = new GUIContent("Revert All");
        private static readonly GUIContent FlattenLabel = new GUIContent("Flatten");
        private static readonly GUIContent RemoveOrphansLabel = new GUIContent("Remove Orphan Overrides");

        private readonly GUIContent _chainLabel = new GUIContent();
        private readonly UnityEditor.Editor _editor;
        private readonly ScriptableVariant _variant;
        private string _parentError;

        public ScriptableVariantHeader(UnityEditor.Editor editor)
        {
            _editor = editor;
            _variant = editor.target as ScriptableVariant;
            if (_variant != null)
            {
                _variant.EnsureResolved();
            }

            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public void Dispose()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>Call from <c>Editor.OnHeaderGUI</c> after the base header has been drawn.</summary>
        public void Draw()
        {
            if (_variant == null || _editor.targets.Length != 1)
            {
                return;
            }

            GUILayout.Space(2f);
            DrawParentField();
            DrawStatusRow();
            DrawWarnings();
            GUILayout.Space(6f);
        }

        private void DrawParentField()
        {
            EditorGUI.BeginChangeCheck();
            var newParent = EditorGUILayout.ObjectField(
                ParentLabel,
                _variant.Parent,
                _variant.ParentType,
                false) as ScriptableVariant;
            if (EditorGUI.EndChangeCheck())
            {
                if (!ScriptableVariantAssetUtility.SetParent(_variant, newParent, out var error))
                {
                    _parentError = error;
                }
                else
                {
                    _parentError = null;
                    _editor.serializedObject.Update();
                }

                _editor.Repaint();
            }
        }

        private void DrawStatusRow()
        {
            _chainLabel.text = ScriptableVariantAssetUtility.GetChainLabel(_variant);
            _chainLabel.tooltip = _chainLabel.text;
            var statusRowHeight = EditorGUIUtility.singleLineHeight + 2f;
            using (new EditorGUI.DisabledScope(!_variant.HasParent))
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(statusRowHeight)))
            {
                GUILayout.Label(
                    _chainLabel,
                    EditorStyles.miniLabel,
                    GUILayout.MinWidth(0f),
                    GUILayout.ExpandWidth(true),
                    GUILayout.Height(statusRowHeight));

                var actionsRect = GUILayoutUtility.GetRect(
                    ActionsLabel,
                    EditorStyles.popup,
                    GUILayout.Width(86f),
                    GUILayout.Height(statusRowHeight));
                if (EditorGUI.DropdownButton(
                        actionsRect,
                        ActionsLabel,
                        FocusType.Passive,
                        EditorStyles.popup))
                {
                    ShowActionsMenu(actionsRect);
                }
            }
        }

        private void DrawWarnings()
        {
            if (!string.IsNullOrEmpty(_parentError))
            {
                EditorGUILayout.HelpBox(_parentError, MessageType.Error);
            }

            var orphans = _variant.EditorGetOrphanOverrides();
            if (orphans.Length > 0)
            {
                EditorGUILayout.HelpBox(
                    "Unknown override paths: " + string.Join(", ", orphans),
                    MessageType.Warning);
            }
        }

        private void ShowActionsMenu(Rect buttonRect)
        {
            var menu = new GenericMenu();
            menu.AddItem(OverrideAllLabel, false, () => Run(ScriptableVariantAssetUtility.OverrideAll));

            if (_variant.OverridePaths.Count > 0)
            {
                menu.AddItem(RevertAllLabel, false, () => Run(ScriptableVariantAssetUtility.RevertAll));
            }
            else
            {
                menu.AddDisabledItem(RevertAllLabel);
            }

            menu.AddSeparator(string.Empty);
            menu.AddItem(FlattenLabel, false, () => Run(ScriptableVariantAssetUtility.Flatten));

            menu.AddSeparator(string.Empty);
            if (_variant.EditorGetOrphanOverrides().Length > 0)
            {
                menu.AddItem(
                    RemoveOrphansLabel,
                    false,
                    () => Run(ScriptableVariantAssetUtility.RemoveOrphanOverrides));
            }
            else
            {
                menu.AddDisabledItem(RemoveOrphansLabel);
            }

            menu.DropDown(buttonRect);
        }

        private void Run(Action<ScriptableVariant> action)
        {
            action(_variant);
            _parentError = null;
            _editor.serializedObject.Update();
            _editor.Repaint();
        }

        private void OnUndoRedo()
        {
            if (_variant == null)
            {
                return;
            }

            _variant.EditorNotifyValuesChanged();
            _variant.EnsureResolved();
            _parentError = null;
            _editor.serializedObject.Update();
            _editor.Repaint();
        }
    }
}
