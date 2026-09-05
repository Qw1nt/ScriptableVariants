using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DCFApixels.ScriptableVariants.Editor
{
    public static class ScriptableVariantAssetUtility
    {
        public static bool SetParent(ScriptableVariant variant, ScriptableVariant parent, out string error)
        {
            if (variant == null)
            {
                error = "Variant is null.";
                return false;
            }

            if (!variant.CanAssignParent(parent, out error))
            {
                return false;
            }

            if (ReferenceEquals(variant.Parent, parent))
            {
                return true;
            }

            var differingPaths = parent != null
                ? GetDifferingOverridePaths(variant, parent)
                : null;

            Undo.RecordObject(variant, "Change Scriptable Variant Parent");
            variant.EditorSetParent(parent, differingPaths);
            MarkChanged(variant);
            return true;
        }

        public static void SetOverride(ScriptableVariant variant, string propertyPath, bool enabled)
        {
            if (variant == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            Undo.RecordObject(variant, enabled ? "Override Variant Property" : "Revert Variant Property");
            variant.EditorSetOverride(propertyPath, enabled);
            MarkChanged(variant);
        }

        public static void Revert(ScriptableVariant variant, string propertyPath)
        {
            if (variant == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            var overridePaths = variant.EditorGetOverridesAffectingSubtree(propertyPath);
            if (overridePaths.Length == 0)
            {
                return;
            }

            Undo.RecordObject(variant, "Revert Variant Property");
            variant.EditorRemoveOverrides(overridePaths);
            MarkChanged(variant);
        }

        public static bool ApplyToParent(ScriptableVariant variant, string propertyPath)
        {
            if (variant == null || variant.Parent == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            var overridePaths = variant.EditorGetOverridesAffectingSubtree(propertyPath);
            if (overridePaths.Length == 0)
            {
                return false;
            }

            var parent = variant.Parent;
            variant.EnsureResolved();
            parent.EnsureResolved();

            for (var i = 0; i < overridePaths.Length; i++)
            {
                if (!VariantSerialization.CanCopyPathValue(variant, parent, overridePaths[i]))
                {
                    return false;
                }
            }

            Undo.RecordObjects(
                new UnityEngine.Object[] {variant, parent},
                "Apply Scriptable Variant Override to Parent");

            var copiedPaths = new List<string>(overridePaths.Length);
            for (var i = 0; i < overridePaths.Length; i++)
            {
                var path = overridePaths[i];
                if (parent.HasParent)
                {
                    parent.EditorSetOverride(path, true);
                }

                if (VariantSerialization.CopyPathValue(variant, parent, path))
                {
                    copiedPaths.Add(path);
                }
            }

            if (copiedPaths.Count == 0)
            {
                return false;
            }

            parent.EditorNotifyValuesChanged();
            MarkChanged(parent);

            variant.EditorRemoveOverrides(copiedPaths);
            MarkChanged(variant);
            return true;
        }

        public static void RevertAll(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return;
            }

            Undo.RecordObject(variant, "Revert All Variant Overrides");
            variant.EditorClearOverrides();
            MarkChanged(variant);
        }

        public static void OverrideAll(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return;
            }

            Undo.RecordObject(variant, "Override All Variant Properties");
            variant.EditorOverrideAll();
            MarkChanged(variant);
        }

        public static void Flatten(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return;
            }

            Undo.RecordObject(variant, "Flatten Scriptable Variant");
            variant.EditorFlatten();
            MarkChanged(variant);
        }

        public static void RemoveOrphanOverrides(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return;
            }

            Undo.RecordObject(variant, "Remove Orphan Variant Overrides");
            variant.EditorRemoveOrphanOverrides();
            MarkChanged(variant);
        }

        public static void NotifyValuesChanged(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return;
            }

            variant.EditorNotifyValuesChanged();
            MarkChanged(variant);
        }

        /// <summary>
        /// Creates overrides for every atomic path at or below <paramref name="propertyPath"/> whose value
        /// differs from the parent. Pass <paramref name="childObject"/> when the edited values are still
        /// pending in a SerializedObject and have not been applied to the asset yet.
        /// </summary>
        public static void OverrideChangedValues(
            ScriptableVariant variant,
            string propertyPath,
            SerializedObject childObject = null)
        {
            if (variant == null || variant.Parent == null || string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            variant.Parent.EnsureResolved();
            var ownsChildObject = childObject == null;
            if (ownsChildObject)
            {
                variant.EnsureResolved();
                childObject = new SerializedObject(variant);
            }

            try
            {
                using (var parentObject = new SerializedObject(variant.Parent))
                {
                    var root = childObject.FindProperty(propertyPath);
                    if (root == null)
                    {
                        return;
                    }

                    var paths = GetDifferingOverridePaths(childObject, parentObject, root);
                    paths.RemoveAll(variant.IsLocallyControlled);
                    if (paths.Count == 0)
                    {
                        return;
                    }

                    Undo.RecordObject(variant, "Override Variant Property");
                    variant.EditorAddOverrides(paths);
                    MarkChanged(variant);
                }
            }
            finally
            {
                if (ownsChildObject)
                {
                    childObject.Dispose();
                }
            }
        }

        internal static bool ValueMatchesParent(ScriptableVariant variant, string propertyPath)
        {
            if (variant == null || variant.Parent == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            variant.EnsureResolved();
            using (var childObject = new SerializedObject(variant))
            using (var parentObject = new SerializedObject(variant.Parent))
            {
                childObject.Update();
                parentObject.Update();

                var childProperty = childObject.FindProperty(propertyPath);
                var parentProperty = parentObject.FindProperty(propertyPath);
                return childProperty != null && parentProperty != null &&
                       SerializedProperty.DataEquals(childProperty, parentProperty);
            }
        }

        private static List<string> GetDifferingOverridePaths(
            ScriptableVariant variant,
            ScriptableVariant parent)
        {
            variant.EnsureResolved();
            parent.EnsureResolved();

            using (var childObject = new SerializedObject(variant))
            using (var parentObject = new SerializedObject(parent))
            {
                return GetDifferingOverridePaths(childObject, parentObject, null);
            }
        }

        /// <summary>
        /// Collects the atomic paths, at or below <paramref name="root"/> or in the whole object when it is null,
        /// whose serialized values differ between the child and the parent.
        /// </summary>
        private static List<string> GetDifferingOverridePaths(
            SerializedObject childObject,
            SerializedObject parentObject,
            SerializedProperty root)
        {
            var variantType = childObject.targetObject.GetType();
            var result = new List<string>();
            var property = root != null ? root.Copy() : childObject.GetIterator();
            var end = root?.GetEndProperty();
            if (root == null && !property.Next(true))
            {
                return result;
            }

            do
            {
                var enterChildren = true;
                var propertyPath = property.propertyPath;
                var parentProperty = VariantSerialization.IsKnownPath(variantType, propertyPath)
                    ? parentObject.FindProperty(propertyPath)
                    : null;
                if (parentProperty != null)
                {
                    if (SerializedProperty.DataEquals(property, parentProperty))
                    {
                        enterChildren = false;
                    }
                    else if (VariantSerialization.IsAtomicOverridePath(variantType, propertyPath) ||
                             !property.hasChildren)
                    {
                        result.Add(propertyPath);
                        enterChildren = false;
                    }
                }

                if (!property.Next(enterChildren))
                {
                    break;
                }
            } while (end == null || !SerializedProperty.EqualContents(property, end));

            return result;
        }

        public static ScriptableVariant CreateChild(ScriptableVariant parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            parent.EnsureResolved();

            var parentPath = AssetDatabase.GetAssetPath(parent);
            var directory = string.IsNullOrEmpty(parentPath)
                ? "Assets"
                : Path.GetDirectoryName(parentPath)?.Replace('\\', '/');
            var defaultName = parent.name + " Child.asset";
            var path = EditorUtility.SaveFilePanelInProject(
                "Create Scriptable Variant Child",
                defaultName,
                "asset",
                "Choose where to create the child variant.",
                directory);

            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var child = ScriptableObject.CreateInstance(parent.GetType()) as ScriptableVariant;
            if (child == null)
            {
                throw new InvalidOperationException($"Could not instantiate {parent.GetType().FullName}.");
            }

            child.name = Path.GetFileNameWithoutExtension(path);
            child.EditorSetParent(parent);
            AssetDatabase.CreateAsset(child, AssetDatabase.GenerateUniqueAssetPath(path));
            AssetDatabase.SaveAssetIfDirty(child);
            Selection.activeObject = child;
            EditorGUIUtility.PingObject(child);
            return child;
        }

        public static string GetChainLabel(ScriptableVariant variant)
        {
            if (variant == null)
            {
                return string.Empty;
            }

            var names = new List<string>();
            var visited = new HashSet<ScriptableVariant>();
            for (var current = variant; current != null && visited.Add(current); current = current.Parent)
            {
                names.Add(current.name);
            }

            names.Reverse();
            return string.Join("  →  ", names);
        }

        private static void MarkChanged(ScriptableVariant variant)
        {
            EditorUtility.SetDirty(variant);
            variant.EnsureResolved();
        }
    }
}
