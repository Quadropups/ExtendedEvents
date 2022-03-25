using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(IntAsStringAttribute))]
    /// We can't put an attribute on parameters of methods defined on <see cref="UnityEngine"/>, like <see cref="Animator.SetBool(int, bool)"/>.
    /// This attribute, howerever can come around this restriction. 
    /// Attribute below tells <see cref="ExtendedEventDrawer"/> to use <see cref="IntAsStringAttributeDrawer"/> fo any parameter with name "id" in any Method defined on <see cref="Animator"/> class
    [CustomParameterDrawer(typeof(Animator), "id")]
    public class IntAsStringAttributeDrawer : ArgumentDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty intProperty = GetIntProperty(property);
            if (TryGetStringProperty(property, out SerializedProperty stringProperty)) {
                EditorGUI.BeginChangeCheck();
                Rect stringRect = position;
                stringRect.width = position.width * 0.5f;
                Rect intRect = position;
                intRect.xMin = stringRect.xMax + EditorGUIUtility.standardVerticalSpacing;
                stringProperty.stringValue = EditorGUI.TextField(stringRect, label, stringProperty.stringValue);
                if (EditorGUI.EndChangeCheck()) intProperty.intValue = Animator.StringToHash(stringProperty.stringValue);
                EditorGUI.BeginDisabledGroup(true);
                EditorGUI.IntField(intRect, intProperty.intValue);
                EditorGUI.EndDisabledGroup();
            }
            else {
                base.OnGUI(position, property, label);
            }
        }
    }
}
