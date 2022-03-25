using System;
using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(RangeAttribute))]
    [CustomPropertyDrawer(typeof(RangeAttribute))]
    public class RangAttributeDrawer : PropertyDrawer {
        private static RangeAttribute DefaultRange = new RangeAttribute(float.NegativeInfinity, float.PositiveInfinity);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.Float && property.propertyType != SerializedPropertyType.Integer) {
                EditorGUI.LabelField(position, "Attribute not implemented for this type");
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(position, property, label);
            if (EditorGUI.EndChangeCheck()) {
                var range = attribute as RangeAttribute ?? DefaultRange;
                if (property.propertyType == SerializedPropertyType.Integer) {
                    property.intValue = Mathf.Clamp(property.intValue, (int)range.min, (int)range.max);
                }
                else if (property.propertyType == SerializedPropertyType.Float) {
                    property.floatValue = Mathf.Clamp(property.floatValue, range.min, range.max);
                }
                else if (property.propertyType == SerializedPropertyType.Enum) {
                    Enum targetEnum = (Enum)Enum.ToObject(fieldInfo.FieldType, property.intValue);
                    Enum enumNew = EditorGUI.EnumPopup(position, label, targetEnum);
                    property.intValue = Mathf.Clamp((int)Convert.ChangeType(enumNew, targetEnum.GetType()), (int)range.min, (int)range.max);
                }
            }
            EditorGUI.EndProperty();
        }
    }
}