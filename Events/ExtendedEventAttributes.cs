using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using static ExtendedEvents.EventCall;
using static ExtendedEvents.ExtendedEvent;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.Linq;

namespace ExtendedEvents {

    /// <summary>
    /// Decorator class for ExtendedEvents.
    /// </summary>
    /// Functionality might be expanded later on.
    public class ExtendedEventAttribute : PropertyAttribute {
        public ExtendedEventAttribute() {
        }
        public ExtendedEventAttribute(ParentType parented) {
            this.parented = parented;
        }

        public enum ParentType {
            /// <summary>Event sequencing and coroutine invokation are allowed</summary>
            Parented,
            /// <summary>Event sequencing and coroutine invokation are not allowed</summary>
            Unparented,
            /// <summary>Events are only used to provide EventCalls</summary>
            EventBuilder,
        }

        /// <summary>Should event sequencing and coroutine invokation be allowed</summary>
        public ParentType parented = ParentType.Parented;

        /// <summary>If true, tag field will be drawn by default drawer specified for it's type</summary>
        public bool useDefaultDrawer = false;
    }


    /// <summary>Attributed used to colorize ExtendedEvent method or parameter field</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
    public class ColorizeMethodAttribute : Attribute {
        public Color color;
        public ColorizeMethodAttribute(float r, float g, float b) {
            this.color = new Color(r, g, b);
        }
    }

    /// <summary>Attributed used to group ExtendedEvent methods together in dropout selector</summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property)]
    public class MethodGroupAttribute : Attribute {
        public string groupName;
        public MethodGroupAttribute(string groupName) {
            this.groupName = groupName;
        }
    }

    /// <summary>Allows argument preview by Invoking Method or property during edit mode</summary>
    /// Use this attribute with care because it will cause argument functions to be callse in editor mode.
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
    public class FuncPreviewAttribute : Attribute { }

    /// <summary>Should this specified method be hidden in selector. Also works for custom casters</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class HiddenAttribute : Attribute { }

}