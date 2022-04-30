using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(GetDataAttribute))]
    [CustomParameterDrawer(typeof(ExtendedEvent), "id")]
    [CustomParameterDrawer(typeof(ExtendedEvent), "key")]
    public class GetDataAttributeDrawer : ArgumentDrawer {

        private Type _dataType;

        private Type dataType {
            get {
                if (_dataType == null) {
                    if (typeof(CachedData).IsCastableFrom(methodInfo.ReturnType)) {
                        _dataType = methodInfo.ReturnType;
                    }
                    else if (methodInfo.Name == nameof(ExtendedEvent.StopCoroutine)) {
                        _dataType = typeof(CachedData);
                    }
                    else _dataType = typeof(CachedData);
                }
                return _dataType;
            }
        }

        private static readonly Color ColorOff = new Color(1f, 0.6f, 0.6f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty intProperty = GetIntProperty(property);

            Color backgroundColor = GUI.backgroundColor;

            ExtendedEvent refEvents = TypeCasterUtility.Cast<ExtendedEvent>(GetMethodTarget(property));
            if (refEvents == null) refEvents = GetEvents(property);

            if (refEvents == null) {
                GUI.backgroundColor = Color.red;
                base.OnGUI(position, property, label);
                GUI.backgroundColor = backgroundColor;
                return;
            }

            if (label != GUIContent.none) ExtendedEventDrawer.DrawLabel(ref position, label);

            CachedData data = refEvents.GetData(intProperty.intValue);

            string eventName = GetEventName(data, intProperty.intValue, dataType);
            GUIContent buttonContentCall = new GUIContent(eventName, eventName);
            if (GUI.Button(position, buttonContentCall, EditorStyles.popup)) {
                BuildPopupListForEvents(intProperty, refEvents).DropDown(position);
            }

            GUI.backgroundColor = backgroundColor;
        }

        public static string GetEventName(CachedData data, int id, Type dataType) {
            if (id == 0) return "Nothing";
            if (data == null) {
                GUI.backgroundColor = ColorOff;
                return $"<{id}>";
            }

            string name = GetDisplayMethodName(data.GetMethodInfo());
            if (!dataType.IsCastableFrom(data.GetType())) {
                GUI.backgroundColor = ColorOff;
                name = $"Type mismatch {name}, {dataType.Name} expected";
            }
            return name;
        }

        public static string GetDisplayMethodName(MethodInfo mi) {
            return mi?.ToString() ?? "Missing";
        }

        private GenericMenu BuildPopupListForEvents(SerializedProperty intArgument, ExtendedEvent events) {
            var menu = new GenericMenu();
            var calls = events.GetSerializedCalls();

            int currentIndex = 0;

            foreach (var call in calls) {
                string callName = $"{GetDisplayMethodName(call?.GetMethodInfo())}";
                object tag = call.GetTag();
                string tagName = "null";
                if (tag != null) {
                    ITagToString converter = ExtendedEventDrawer.GetTypeCache(tag.GetType())?.Drawer as ITagToString;
                    tagName = converter?.TagToString(tag) ?? tag.ToString();
                }

                string guiContent = $"{tagName} / Call {currentIndex}. {callName}";

                string addCallName = null;
                if (call.argumentCount > 0) addCallName = $" / {callName}";

                menu.AddItem(new GUIContent(call.argumentCount != 0 ? $"{guiContent}{addCallName}" : guiContent), intArgument.intValue == call.id, SelectCall, new SelectedIData(intArgument, call.id));

                for (int i = 0; i < call.argumentCount; i++) {
                    int argId = GetArgumentID(call.id, i);
                    Type callParameterType = call.GetParameterType(i);
                    string del;
                    if (callParameterType == null) del = "null";
                    else del = call.GetArgumentAt(i).GetReturnName(call.GetParameterType(i));
                    if (del != null) {
                        menu.AddItem(new GUIContent($"{guiContent} / Arg {i}. {del}"), intArgument.intValue == argId, SelectCall, new SelectedIData(intArgument, argId));
                    }
                }

                currentIndex++;
            }

            return menu;
        }

        public static void SelectCall(object data) {
            var call = (SelectedIData)data;
            call.intArgument.intValue = call.id;
            call.intArgument.serializedObject.ApplyModifiedProperties();
        }
        public class SelectedIData {
            #region Fields

            public SerializedProperty intArgument;

            public int id;

            #endregion

            public SelectedIData(SerializedProperty intArgument, int id) {
                this.intArgument = intArgument;
                this.id = id;
            }
        }
        public static int GetArgumentID(int callId, int argumentIndex) => unchecked(callId + (argumentIndex + 1) * 486187739);

    }
}