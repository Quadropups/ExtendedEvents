using System;
using UnityEngine;

namespace ExtendedEvents {
    /// <summary>Custom names for boolean's true and false values</summary>
    public class ToggleAttribute : ExtendedEventPropertyAttribute {
        public string onText;
        public string offText;
        public ToggleAttribute(string onText, string offText) {
            this.onText = onText;
            this.offText = offText;
        }
    }
}