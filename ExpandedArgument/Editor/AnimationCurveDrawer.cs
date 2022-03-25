using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {
    [CustomParameterDrawer(typeof(AnimationCurve))]
    public class AnimationCurveDrawer : ArgumentDrawer {
        public const string AnimationCurveArgumentFieldName = "_animationCurveArgument";

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty animationCurveProperty = property.FindPropertyRelative(AnimationCurveArgumentFieldName);
            //if this is ExpandedArgument it has _animationCurveArgument field and we draw animation curve.
            if (animationCurveProperty != null) EditorGUI.PropertyField(position, animationCurveProperty, label);
            else base.OnGUI(position, property, label);
        }
    }
}

