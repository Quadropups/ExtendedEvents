using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtendedEvents {

    [CustomParameterDrawer(typeof(GetValueAttribute))]
    [CustomParameterDrawer(typeof(ExtendedEvent), "GetValue", "id")]
    public class GetValueDrawer : ArgumentDrawer {
        private static readonly Color ColorOff = new Color(1f, 0.6f, 0.6f);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            SerializedProperty intProperty = GetIntProperty(property);

            Color backgroundColor = GUI.backgroundColor;

            ExtendedEvent refEvents = TypeCasterUtility.Cast<ExtendedEvent>(GetMethodTarget(property));

            if (refEvents == null) {
                GUI.backgroundColor = Color.red;
                base.OnGUI(position, property, label);
                GUI.backgroundColor = backgroundColor;
                return;
            }
            CachedData data = refEvents.GetData(intProperty.intValue);

            string eventName = GetDataAttributeDrawer.GetEventName(data, intProperty.intValue,typeof(CachedData));
            string eventTooltip = eventName;
            if (refEvents.GetData(intProperty.intValue) != null) {
                Type desiredType = (parameterInfo.Member as MethodInfo)?.ReturnType;
                if (!desiredType.IsCastableFrom(data.ReturnType, true) &&
                        !(typeof(Object).IsAssignableFrom(data.ReturnType) &&
                        typeof(Object).IsAssignableFrom(desiredType))) {
                    GUI.backgroundColor = ColorOff;
                    eventName = $"Type mismath {eventName}";
                    eventTooltip = $"{data.ReturnType} can't be cast to {desiredType}";
                }
            }
            GUIContent buttonContentCall = new GUIContent(eventName, eventTooltip);
            if (GUI.Button(position, buttonContentCall, EditorStyles.popup)) {
                BuildPopupListForEvents(intProperty, parameterInfo, refEvents).DropDown(position);
            }

            GUI.backgroundColor = backgroundColor;
        }

        private GenericMenu BuildPopupListForEvents(SerializedProperty intArgument, ParameterInfo parameter, ExtendedEvent events) {
            var menu = new GenericMenu();
            var calls = events.GetSerializedCalls();

            Type desiredType = (parameter.Member as MethodInfo)?.ReturnType;

            int currentIndex = 0;

            foreach (var call in calls) {
                string callName = $"{GetDataAttributeDrawer.GetDisplayMethodName(call?.GetMethodInfo())}";

                object tag = call.GetTag();
                string tagName = "null";
                if (tag != null) {
                    ITagToString converter = ExtendedEventDrawer.GetTypeCache(tag.GetType())?.Drawer as ITagToString;
                    tagName = converter?.TagToString(tag) ?? tag.ToString();
                }

                string guiContent = $"{tagName} / Call {currentIndex}. {callName}";

                if (desiredType.IsCastableFrom(call.ReturnType, true)) menu.AddItem(new GUIContent(call.argumentCount != 0 ? $"{guiContent} / {callName}" : guiContent), intArgument.intValue == call.id, GetDataAttributeDrawer.SelectCall, new GetDataAttributeDrawer.SelectedIData(intArgument, call.id));
                for (int i = 0; i < call.argumentCount; i++) {

                    if (!desiredType.IsCastableFrom(call.GetArgumentAt(i).GetReturnType(call.GetParameterType(i)), true)) continue;

                    var argId = GetDataAttributeDrawer.GetArgumentID(call.id, i);
                    string del = call.GetArgumentAt(i).GetReturnName(call.GetParameterType(i));
                    if (del != null) {
                        menu.AddItem(new GUIContent($"{guiContent} / Arg {i}. {del}"), intArgument.intValue == argId, GetDataAttributeDrawer.SelectCall, new GetDataAttributeDrawer.SelectedIData(intArgument, argId));
                    }
                }

                currentIndex++;
            }

            return menu;
        }

    }
}