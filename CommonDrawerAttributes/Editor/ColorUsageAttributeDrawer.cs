using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(ColorUsageAttribute))]
    public class ColorUsageAttributeDrawer : ArgumentDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

            ColorUsageAttribute colorUsage = attribute as ColorUsageAttribute;

            ExtendedEventDrawer.ColorField(position, label, property, true, colorUsage.showAlpha, colorUsage.hdr);
        }
    }

}

