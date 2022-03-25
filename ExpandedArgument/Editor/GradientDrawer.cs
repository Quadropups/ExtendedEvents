using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {
    [CustomParameterDrawer(typeof(Gradient))]
    public class GradientDrawer : ArgumentDrawer {
        public const string GradientArgumentFieldName = "_gradientArgument";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty gradientProperty = property.FindPropertyRelative(GradientArgumentFieldName);
            //if this is ExpandedArgument it has _gradientArgument field and we draw gradient.
            if (gradientProperty != null) EditorGUI.PropertyField(position, gradientProperty, label);
            else base.OnGUI(position, property, label);
        }
    }
}

