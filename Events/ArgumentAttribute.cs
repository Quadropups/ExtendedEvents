using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ExtendedEvents {

    /// <summary>
    /// Base class to derive your custom ArgumentDrawers.
    /// <para></para>
    /// This attribute can be applied to method's parameter or property, as well as to the Type itself.
    /// </summary>
    /// As an example look at <see cref="IntAsStringAttribute"/> which will make any integer parameter drawn as a string field.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Parameter | AttributeTargets.Method | AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public abstract class ArgumentAttribute : PropertyAttribute { }

}

