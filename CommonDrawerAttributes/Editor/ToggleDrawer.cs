using System;
using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(ToggleAttribute))]
    public class ToggleDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var toggleAttribute = attribute as ToggleAttribute;
            ExtendedEventDrawer.BoolField(position, property, label, toggleAttribute.onText, toggleAttribute.offText);
        }
    }
}