using System;

namespace DCFApixels.ScriptableVariants
{
    /// <summary>
    /// Declares which assets a Scriptable Variant class accepts as its parent.
    /// Without the attribute a parent must have exactly the same concrete type. With it, any asset
    /// assignable to <see cref="ParentType"/> is accepted and offered by the Parent picker.
    /// Fields that the chosen parent type does not declare stay local on the child.
    /// The attribute is inherited by subclasses.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public sealed class VariantTypeSelectionAttribute : Attribute
    {
        public Type ParentType { get; }

        public VariantTypeSelectionAttribute(Type parentType)
        {
            if (parentType == null || !typeof(ScriptableVariant).IsAssignableFrom(parentType))
            {
                throw new ArgumentException(
                    $"{nameof(VariantTypeSelectionAttribute)} requires a {nameof(ScriptableVariant)} type.",
                    nameof(parentType));
            }

            ParentType = parentType;
        }
    }
}
