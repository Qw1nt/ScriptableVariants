using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCFApixels.ScriptableVariants
{
    /// <summary>
    /// Base class for ScriptableObject assets whose serialized fields can inherit values from a parent asset.
    /// </summary>
    public abstract class ScriptableVariant : ScriptableObject, ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector, VariantLocal]
        private ScriptableVariant _variantParent;

        [SerializeField, HideInInspector, VariantLocal]
        private List<string> _variantOverrides = new List<string>();

        [NonSerialized]
        private HashSet<string> _overrideLookup;

        [NonSerialized]
        private bool _resolutionDirty = true;

        [NonSerialized]
        private bool _overridePathsNeedNormalization = true;

        [NonSerialized]
        private bool _isResolving;

        [NonSerialized]
        private bool _resolutionErrorLogged;

        [NonSerialized]
        private int _resolvedRevision;

        [NonSerialized]
        private int _observedParentRevision = -1;

        [NonSerialized]
        private Type _parentType;

        private static readonly List<ScriptableVariant> ActiveVariants = new List<ScriptableVariant>();

        /// <summary>The parent asset. Use ScriptableVariant&lt;TSelf&gt; when a typed Parent is convenient.</summary>
        public ScriptableVariant Parent => _variantParent;

        public bool HasParent => _variantParent != null;

        /// <summary>
        /// The type a parent asset must be assignable to. This is the exact concrete type unless the class
        /// carries <see cref="VariantTypeSelectionAttribute"/>.
        /// </summary>
        public Type ParentType
        {
            get
            {
                if (_parentType == null)
                {
                    var selection = (VariantTypeSelectionAttribute) Attribute.GetCustomAttribute(
                        GetType(),
                        typeof(VariantTypeSelectionAttribute),
                        true);
                    _parentType = selection != null ? selection.ParentType : GetType();
                }

                return _parentType;
            }
        }

        /// <summary>The serialized override paths. The returned collection is read-only.</summary>
        public IReadOnlyList<string> OverridePaths
        {
            get
            {
                NormalizeOverridePaths();
                return _variantOverrides;
            }
        }

        /// <summary>
        /// Changes whenever this asset's materialized values are recomputed.
        /// Calling this property also guarantees that inherited values are current.
        /// </summary>
        public int ResolvedRevision
        {
            get
            {
                EnsureResolved();
                return _resolvedRevision;
            }
        }

        /// <summary>
        /// Ensures that every non-overridden serialized field contains its effective inherited value.
        /// Only needed before OnEnable has run; the call is allocation-free and returns immediately when nothing changed.
        /// </summary>
        public void EnsureResolved()
        {
            Resolve();
        }

        /// <summary>
        /// Invalidates this asset and immediately re-materializes every currently loaded descendant.
        /// Call this after changing values from code. Descendants that are not loaded resolve when they load.
        /// </summary>
        public void InvalidateResolvedData()
        {
            _resolutionDirty = true;
            var descendants = GetLoadedDescendants();
            for (var i = 0; i < descendants.Count; i++)
            {
                descendants[i]._resolutionDirty = true;
            }

            for (var i = 0; i < descendants.Count; i++)
            {
                descendants[i].EnsureResolved();
            }
        }

        /// <summary>Returns every loaded asset that inherits, directly or indirectly, from this asset.</summary>
        public List<ScriptableVariant> GetLoadedDescendants()
        {
            var result = new List<ScriptableVariant>();
            CollectLoadedDescendants(this, new HashSet<ScriptableVariant>(ReferenceComparer.Instance), result);
            return result;
        }

        /// <summary>Returns true when <paramref name="ancestor"/> appears anywhere in this asset's parent chain.</summary>
        public bool IsDescendantOf(ScriptableVariant ancestor)
        {
            if (ancestor == null)
            {
                return false;
            }

            var visited = new HashSet<ScriptableVariant>(ReferenceComparer.Instance);
            for (var current = _variantParent; current != null && visited.Add(current); current = current._variantParent)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns true when the exact property path has a local override.</summary>
        public bool IsOverridden(string propertyPath)
        {
            NormalizeOverridePaths();
            return !string.IsNullOrEmpty(propertyPath) && GetOverrideLookup().Contains(propertyPath);
        }

        /// <summary>Returns true when this path or one of its owning paths has a local override.</summary>
        public bool IsLocallyControlled(string propertyPath)
        {
            NormalizeOverridePaths();
            return FindOverrideAtOrAbove(propertyPath) != null;
        }

        /// <summary>Returns true when at least one child path is overridden locally.</summary>
        public bool HasOverridesBelow(string propertyPath)
        {
            NormalizeOverridePaths();
            if (string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            var prefix = propertyPath + ".";
            var paths = _variantOverrides;
            for (var i = 0; i < paths.Count; i++)
            {
                if (paths[i] != null && paths[i].StartsWith(prefix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Called after effective values have been materialized.</summary>
        protected virtual void OnVariantResolved()
        {
        }

        protected virtual void OnEnable()
        {
            RegisterActive(this);
            EnsureResolved();
        }

        protected virtual void OnDisable()
        {
            UnregisterActive(this);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_variantOverrides == null)
            {
                _variantOverrides = new List<string>();
            }

            _overrideLookup = null;
            _overridePathsNeedNormalization = true;
            _resolutionDirty = true;
            _observedParentRevision = -1;
        }

        internal bool CanAssignParent(ScriptableVariant candidate, out string error)
        {
            if (candidate == null)
            {
                error = null;
                return true;
            }

            if (ReferenceEquals(candidate, this))
            {
                error = "A Scriptable Variant cannot inherit from itself.";
                return false;
            }

            if (!IsCompatibleParent(candidate))
            {
                error = ParentType == GetType()
                    ? $"Parent must have the exact type {GetType().Name}."
                    : $"Parent must be a {ParentType.Name}.";
                return false;
            }

            var visited = new HashSet<ScriptableVariant>(ReferenceComparer.Instance);
            for (var current = candidate; current != null; current = current._variantParent)
            {
                if (ReferenceEquals(current, this))
                {
                    error = "The selected parent would create an inheritance cycle.";
                    return false;
                }

                if (!visited.Add(current))
                {
                    error = "The selected parent already belongs to a cyclic inheritance chain.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        internal void EditorSetParent(ScriptableVariant parent)
        {
            EditorSetParent(parent, null);
        }

        internal void EditorSetParent(ScriptableVariant parent, IReadOnlyList<string> additionalOverridePaths)
        {
            if (!CanAssignParent(parent, out var error))
            {
                throw new ArgumentException(error, nameof(parent));
            }

            if (ReferenceEquals(_variantParent, parent))
            {
                return;
            }

            EnsureResolved();
            if (parent != null && additionalOverridePaths != null && additionalOverridePaths.Count > 0)
            {
                NormalizeOverridePaths();
                for (var i = 0; i < additionalOverridePaths.Count; i++)
                {
                    AddOverridePath(additionalOverridePaths[i]);
                }

                SortAndDeduplicateOverrides();
            }

            _variantParent = parent;
            _resolutionErrorLogged = false;
            InvalidateResolvedData();
            EnsureResolved();
        }

        internal void EditorFlatten()
        {
            EnsureResolved();
            _variantParent = null;
            _variantOverrides.Clear();
            _overrideLookup = null;
            _resolutionErrorLogged = false;
            InvalidateResolvedData();
            EnsureResolved();
        }

        internal void EditorSetOverride(string propertyPath, bool enabled)
        {
            if (string.IsNullOrEmpty(propertyPath) || _variantParent == null)
            {
                return;
            }

            EnsureResolved();
            NormalizeOverridePaths();
            if (enabled)
            {
                AddOverridePath(propertyPath);
            }
            else
            {
                RemoveOverridesAtOrBelow(propertyPath);
            }

            ResolveOverrideChanges();
        }

        internal void EditorRemoveOverrides(IReadOnlyList<string> propertyPaths)
        {
            if (propertyPaths == null || propertyPaths.Count == 0 || _variantParent == null)
            {
                return;
            }

            EnsureResolved();
            NormalizeOverridePaths();
            for (var i = 0; i < propertyPaths.Count; i++)
            {
                RemoveOverridesAtOrBelow(propertyPaths[i]);
            }

            ResolveOverrideChanges();
        }

        internal void EditorAddOverrides(IReadOnlyList<string> propertyPaths)
        {
            if (propertyPaths == null || propertyPaths.Count == 0 || _variantParent == null)
            {
                return;
            }

            EnsureResolved();
            NormalizeOverridePaths();
            for (var i = 0; i < propertyPaths.Count; i++)
            {
                AddOverridePath(propertyPaths[i]);
            }

            ResolveOverrideChanges();
        }

        internal void EditorClearOverrides()
        {
            if (_variantOverrides.Count == 0)
            {
                return;
            }

            _variantOverrides.Clear();
            _overrideLookup = null;
            InvalidateResolvedData();
            EnsureResolved();
        }

        internal void EditorOverrideAll()
        {
            if (_variantParent == null)
            {
                return;
            }

            EnsureResolved();
            _variantOverrides.Clear();

            var fields = VariantSerialization.GetRootFields(GetType());
            for (var i = 0; i < fields.Length; i++)
            {
                if (!fields[i].IsDefined(typeof(VariantLocalAttribute), true))
                {
                    _variantOverrides.Add(fields[i].Name);
                }
            }

            ResolveOverrideChanges();
        }

        internal void EditorRemoveOrphanOverrides()
        {
            NormalizeOverridePaths();
            for (var i = _variantOverrides.Count - 1; i >= 0; i--)
            {
                if (!VariantSerialization.IsKnownPath(GetType(), _variantOverrides[i]))
                {
                    _variantOverrides.RemoveAt(i);
                }
            }

            ResolveOverrideChanges();
        }

        internal string[] EditorGetOrphanOverrides()
        {
            NormalizeOverridePaths();
            var result = new List<string>();
            for (var i = 0; i < _variantOverrides.Count; i++)
            {
                if (!VariantSerialization.IsKnownPath(GetType(), _variantOverrides[i]))
                {
                    result.Add(_variantOverrides[i]);
                }
            }

            return result.ToArray();
        }

        internal string[] EditorGetOverridesAffectingSubtree(string propertyPath)
        {
            NormalizeOverridePaths();
            if (string.IsNullOrEmpty(propertyPath))
            {
                return Array.Empty<string>();
            }

            var controllingOverride = FindOverrideAtOrAbove(propertyPath);
            if (controllingOverride != null)
            {
                return new[] {controllingOverride};
            }

            var prefix = propertyPath + ".";
            var result = new List<string>();
            for (var i = 0; i < _variantOverrides.Count; i++)
            {
                var candidate = _variantOverrides[i];
                if (candidate.StartsWith(prefix, StringComparison.Ordinal))
                {
                    result.Add(candidate);
                }
            }

            return result.ToArray();
        }

        internal void EditorNotifyValuesChanged()
        {
            _overrideLookup = null;
            _overridePathsNeedNormalization = true;
            InvalidateResolvedData();
        }

        internal ScriptableVariant GetValueSource(string propertyPath)
        {
            if (_variantParent == null || IsLocallyControlled(propertyPath))
            {
                return this;
            }

            return _variantParent.GetValueSource(propertyPath);
        }

        private bool IsCompatibleParent(ScriptableVariant candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            var parentType = ParentType;
            return parentType == GetType()
                ? candidate.GetType() == parentType
                : parentType.IsInstanceOfType(candidate);
        }

        private bool Resolve()
        {
            // _isResolving marks every asset on the current resolution chain, so re-entry means a cycle.
            if (_isResolving)
            {
                LogResolutionErrorOnce("Cyclic Scriptable Variant inheritance detected.");
                return false;
            }

            _isResolving = true;
            try
            {
                NormalizeOverridePaths();

                var parent = _variantParent;
                var parentIsUsable = parent == null ||
                                     IsCompatibleParent(parent) && parent.Resolve();
                if (!parentIsUsable)
                {
                    LogResolutionErrorOnce("Scriptable Variant parent is incompatible or cyclic. Local values are used.");
                    parent = null;
                }

                var parentRevision = parent != null ? parent._resolvedRevision : -1;
                if (!_resolutionDirty && _observedParentRevision == parentRevision)
                {
                    return true;
                }

                if (parent != null)
                {
                    VariantSerialization.ApplyParent(parent, this, GetOverrideLookup());
                }

                _resolutionDirty = false;
                _observedParentRevision = parentRevision;
                unchecked
                {
                    _resolvedRevision++;
                }

                _resolutionErrorLogged = false;
                OnVariantResolved();
                return true;
            }
            finally
            {
                _isResolving = false;
            }
        }

        private void NormalizeOverridePaths()
        {
            if (!_overridePathsNeedNormalization)
            {
                return;
            }

            _overridePathsNeedNormalization = false;
            var changed = false;
            for (var i = 0; i < _variantOverrides.Count; i++)
            {
                var oldPath = _variantOverrides[i];
                if (VariantSerialization.TryRemapFormerPath(GetType(), oldPath, out var remappedPath) &&
                    !string.Equals(oldPath, remappedPath, StringComparison.Ordinal))
                {
                    _variantOverrides[i] = remappedPath;
                    changed = true;
                }
            }

            if (changed)
            {
                SortAndDeduplicateOverrides();
            }
            else
            {
                _overrideLookup = null;
            }
        }

        private void SortAndDeduplicateOverrides()
        {
            _variantOverrides.RemoveAll(string.IsNullOrEmpty);
            _variantOverrides.Sort(StringComparer.Ordinal);

            for (var i = _variantOverrides.Count - 1; i > 0; i--)
            {
                if (string.Equals(_variantOverrides[i], _variantOverrides[i - 1], StringComparison.Ordinal))
                {
                    _variantOverrides.RemoveAt(i);
                }
            }

            _overrideLookup = null;
        }

        private void AddOverridePath(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath) ||
                !VariantSerialization.IsKnownPath(GetType(), propertyPath))
            {
                return;
            }

            var controllingOverride = FindOverrideAtOrAbove(propertyPath);
            var controlledByAncestor = controllingOverride != null &&
                                       !string.Equals(controllingOverride, propertyPath, StringComparison.Ordinal);
            RemoveOverridesAtOrBelow(propertyPath);

            if (!controlledByAncestor)
            {
                _variantOverrides.Add(propertyPath);
            }
        }

        private string FindOverrideAtOrAbove(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return null;
            }

            var lookup = GetOverrideLookup();
            if (lookup.Contains(propertyPath))
            {
                return propertyPath;
            }

            for (var separator = propertyPath.LastIndexOf('.'); separator > 0;
                 separator = propertyPath.LastIndexOf('.', separator - 1))
            {
                var ancestorPath = propertyPath.Substring(0, separator);
                if (lookup.Contains(ancestorPath))
                {
                    return ancestorPath;
                }
            }

            return null;
        }

        private void RemoveOverridesAtOrBelow(string propertyPath)
        {
            if (string.IsNullOrEmpty(propertyPath))
            {
                return;
            }

            var prefix = propertyPath + ".";
            for (var i = _variantOverrides.Count - 1; i >= 0; i--)
            {
                var existing = _variantOverrides[i];
                if (string.Equals(existing, propertyPath, StringComparison.Ordinal) ||
                    existing.StartsWith(prefix, StringComparison.Ordinal))
                {
                    _variantOverrides.RemoveAt(i);
                }
            }

            _overrideLookup = null;
        }

        private void ResolveOverrideChanges()
        {
            SortAndDeduplicateOverrides();
            InvalidateResolvedData();
            EnsureResolved();
        }

        private HashSet<string> GetOverrideLookup()
        {
            if (_overrideLookup == null)
            {
                _overrideLookup = new HashSet<string>(_variantOverrides, StringComparer.Ordinal);
            }

            return _overrideLookup;
        }

        private void LogResolutionErrorOnce(string message)
        {
            if (_resolutionErrorLogged)
            {
                return;
            }

            _resolutionErrorLogged = true;
            Debug.LogError(message, this);
        }

        private static void RegisterActive(ScriptableVariant variant)
        {
            if (!ActiveVariants.Contains(variant))
            {
                ActiveVariants.Add(variant);
            }
        }

        private static void UnregisterActive(ScriptableVariant variant)
        {
            ActiveVariants.Remove(variant);
        }

        private static void CollectLoadedDescendants(
            ScriptableVariant root,
            HashSet<ScriptableVariant> visited,
            List<ScriptableVariant> result)
        {
            if (root == null || !visited.Add(root))
            {
                return;
            }

            for (var i = ActiveVariants.Count - 1; i >= 0; i--)
            {
                var candidate = ActiveVariants[i];
                if (candidate == null)
                {
                    ActiveVariants.RemoveAt(i);
                    continue;
                }

                if (ReferenceEquals(candidate._variantParent, root) && !visited.Contains(candidate))
                {
                    result.Add(candidate);
                    CollectLoadedDescendants(candidate, visited, result);
                }
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<ScriptableVariant>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();

            public bool Equals(ScriptableVariant x, ScriptableVariant y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(ScriptableVariant obj)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    /// <summary>Optional strongly typed convenience base for a family of variants.</summary>
    public abstract class ScriptableVariant<TSelf> : ScriptableVariant
        where TSelf : ScriptableVariant<TSelf>
    {
        public new TSelf Parent => base.Parent as TSelf;
    }
}
