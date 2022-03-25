using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(GameObject), "layer")]
    [CustomParameterDrawer(typeof(LayerFieldAttribute))]
    [CustomPropertyDrawer(typeof(LayerFieldAttribute))]
    public class LayerFieldDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            property.intValue = EditorGUI.LayerField(position, property.intValue);
        }
    }
}
