using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtendedEvents {
    public class ArgumentDrawer {

        private MethodInfo m_MethodInfo;
        private ParameterInfo m_ParameterInfo;
        private Type m_ParameterType;
        private Attribute m_Attribute;

        public MethodInfo methodInfo => m_MethodInfo;
        public ParameterInfo parameterInfo => m_ParameterInfo;
        public Type parameterType => m_ParameterType;
        public Attribute attribute => m_Attribute;

        /// <summary>Override this method to make your own GUI for the property based on IMGUI.</summary>
        public virtual void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            ExtendedEventDrawer.ArgumentField(position, property, label, parameterInfo.ParameterType, true);
        }

        private void Setup(MethodInfo methodInfo, ParameterInfo parameterInfo, Type parameterType, Attribute attribute) {
            m_MethodInfo = methodInfo;
            m_ParameterInfo = parameterInfo;
            m_ParameterType = parameterType;
            m_Attribute = attribute;
        }

        protected static bool ArgumentIsFunc(SerializedProperty property) {
            return ((Argument.Definition)property.FindPropertyRelative(ExtendedEventDrawer.ArgumentDefitionFieldName).intValue & Argument.Definition.IsMethod) != 0;
        }

        /// <summary>Returns string argument only if it's not used as method descriptor</summary>
        protected static bool TryGetStringProperty(SerializedProperty property, out SerializedProperty stringProperty) {
            stringProperty = null;
            if (!ArgumentIsFunc(property)) {
                stringProperty = property.FindPropertyRelative(ExtendedEventDrawer.StringArgumentFieldName);
                return true;
            }
            return false;
        }

        protected static SerializedProperty GetBoolProperty(SerializedProperty property) => property.FindPropertyRelative(ExtendedEventDrawer.BoolArgumentFieldName);
        protected static SerializedProperty GetObjectProperty(SerializedProperty property) => property.FindPropertyRelative(ExtendedEventDrawer.ObjectArgumentFieldName);
        protected static SerializedProperty GetIntProperty(SerializedProperty property) => property.FindPropertyRelative(ExtendedEventDrawer.IntArgumentFieldName);
        protected static SerializedProperty GetVetor3Property(SerializedProperty property) => property.FindPropertyRelative(ExtendedEventDrawer.Vector3ArgumentFieldName);
        protected static SerializedProperty GetFloatProperty(SerializedProperty property) => property.FindPropertyRelative(ExtendedEventDrawer.FloatArgumentFieldName);


        protected static SerializedProperty GetEventsProperty(SerializedProperty property) {
            string path = property.propertyPath;
            int index = path.LastIndexOf("._calls.Array.data[");
            return property.serializedObject.FindProperty(path.Substring(0, index));
        }

        protected static ExtendedEvent GetEvents(SerializedProperty property) {
            SerializedProperty eventsProperty = GetEventsProperty(property);
            return ExtendedEventDrawer.GetValue(eventsProperty) as ExtendedEvent;
        }

        /// <summary>Returns method's target (object instance for Instance methods or first argument for Static methods</summary>
        protected static Object GetMethodTarget(SerializedProperty property) {
            if (ArgumentIsFunc(property)) {
                return GetObjectProperty(property).objectReferenceValue;
            }
            else {
                string propertyPath = property.propertyPath;
                int arrayElementIndex = propertyPath.LastIndexOf('[');
                propertyPath = propertyPath.Substring(0, arrayElementIndex).Insert(arrayElementIndex, "[0]");

                SerializedProperty argument0Property = property.serializedObject.FindProperty(propertyPath);

                if (ArgumentIsFunc(argument0Property)) {
                    return null;
                }
                else {
                    return GetObjectProperty(argument0Property).objectReferenceValue;
                }
            }
        }

    }
}

