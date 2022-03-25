using System;
using UnityEngine;

namespace ExtendedEvents {
    /// <summary>Base class to derive custom property attributes from. If applied to a method parameter, ExtendedEvent will use this drawer instead of standard drawer.
    /// <para></para>
    /// Being derived from <see cref="PropertyAttribute"/> allows you to decorate a field to be used as Unity's PropertyDrawer or parameter to be used as ExtendedEvent Parameter drawer with this PropertyAttribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Field, AllowMultiple = false)]
    public class ExtendedEventPropertyAttribute : PropertyAttribute {
    }
}
