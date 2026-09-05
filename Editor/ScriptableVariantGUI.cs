using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.Editor
{
    internal readonly struct OverrideState
    {
        public readonly Color BarColor;
        public readonly string Tooltip;
        public readonly bool LocallyControlled;

        public OverrideState(Color barColor, string tooltip, bool locallyControlled)
        {
            BarColor = barColor;
            Tooltip = tooltip;
            LocallyControlled = locallyControlled;
        }
    }

    /// <summary>Override gutter helpers shared by every Inspector integration.</summary>
    internal static class ScriptableVariantGUI
    {
        private const float HitAreaWidth = 8f;

        private static readonly Color OverrideColor = new Color32(47, 145, 255, 255);
        private static readonly Color ChildOverrideColor = new Color32(47, 145, 255, 150);
        private static readonly GUIContent TooltipContent = new GUIContent();
        private static readonly MethodInfo SetBoldDefaultFontMethod = typeof(EditorGUIUtility).GetMethod(
            "SetBoldDefaultFont",
            BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly object[] BoldArguments = {true};
        private static readonly object[] RegularArguments = {false};

        internal static OverrideState GetOverrideState(ScriptableVariant variant, string propertyPath)
        {
            var exact = variant.IsOverridden(propertyPath);
            var locallyControlled = variant.IsLocallyControlled(propertyPath);
            if (locallyControlled && !exact)
            {
                return new OverrideState(
                    Color.clear,
                    "Controlled by an owning property override. Right-click to apply or revert it.",
                    true);
            }

            if (exact)
            {
                return new OverrideState(
                    OverrideColor,
                    "Local override. Right-click to apply it to the parent or revert it.",
                    true);
            }

            if (variant.HasOverridesBelow(propertyPath))
            {
                return new OverrideState(
                    ChildOverrideColor,
                    "Contains local child overrides. Right-click to apply or revert the subtree.",
                    false);
            }

            var source = variant.GetValueSource(propertyPath);
            return new OverrideState(
                Color.clear,
                source != null
                    ? $"Inherited from {source.name}. Right-click to override."
                    : "Inherited. Right-click to override.",
                false);
        }

        /// <summary>Draws the override bar in the gutter left of an IMGUI property row and serves its context menu.</summary>
        internal static void DrawOverrideBar(Rect rowRect, ScriptableVariant variant, string propertyPath)
        {
            if (variant == null || !variant.HasParent)
            {
                return;
            }

            var hitRect = new Rect(
                rowRect.x - HitAreaWidth,
                rowRect.y,
                HitAreaWidth,
                EditorGUIUtility.singleLineHeight);
            var evt = Event.current;
            if (evt.type == EventType.ContextClick && hitRect.Contains(evt.mousePosition))
            {
                var menu = new GenericMenu();
                ScriptableVariantContextMenu.Populate(menu, variant, propertyPath);
                menu.ShowAsContext();
                evt.Use();
                return;
            }

            if (evt.type != EventType.Repaint)
            {
                return;
            }

            var state = GetOverrideState(variant, propertyPath);
            if (state.BarColor.a > 0f)
            {
                EditorGUI.DrawRect(
                    new Rect(hitRect.x + 3f, hitRect.y + 2f, 2f, hitRect.height - 4f),
                    state.BarColor);
            }

            TooltipContent.tooltip = state.Tooltip;
            GUI.Label(hitRect, TooltipContent, GUIStyle.none);
        }

        /// <summary>Toggles Unity's bold default font, the same mechanism prefab overrides use.</summary>
        internal static void SetBoldDefaultFont(bool bold)
        {
            SetBoldDefaultFontMethod?.Invoke(null, bold ? BoldArguments : RegularArguments);
        }
    }
}
