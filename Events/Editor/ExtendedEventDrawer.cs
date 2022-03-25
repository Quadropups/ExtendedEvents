using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using static ExtendedEvents.Argument;
using static ExtendedEvents.EventCall;
using Object = UnityEngine.Object;

namespace ExtendedEvents {

    [CustomPropertyDrawer(typeof(ExtendedEvent), true)]
    [CustomPropertyDrawer(typeof(ExtendedEventAttribute), true)]
    public class ExtendedEventDrawer : PropertyDrawer {
        #region Constants

        public const string ArgumentDefitionFieldName = "_definition";
        public const string ArgumentsFieldName = "_arguments";
        //Argument fields
        public const string BoolArgumentFieldName = "_boolArgument";
        public const string CallDefinionFieldName = "_definition";
        public const string CallsFieldName = "_calls";
        public const string DelayIDFieldName = "_delayID";
        public const string DelayModeFieldName = "_delayMode";
        //Call fields (delay)
        public const string DelayValueFieldName = "_delayValue";
        //Call fields (misc)
        public const string EnabledFieldName = "_enabled";
        //Editor fields
        public const string FloatArgumentFieldName = "_floatArgument";
        public const string FuncPreviewFieldName = "_editorPreviewFlag";
        public const string IDFieldName = "_id";
        public const string IntArgumentFieldName = "_intArgument";
        //Call fields (arguments)
        public const string MethodNameFieldName = "_methodName";
        public const string ObjectArgumentFieldName = "_objectArgument";
        //ExtendedEvent fields
        public const string ParentFieldName = "_parent";
        public const string StringArgumentFieldName = "_stringArgument";
        public const string TagFieldName = "_tag";
        public const string Vector3ArgumentFieldName = "_vector3Argument";
        private const float ElementSpacing = 6;
        private const float MinLabelWidth = 12;
        private const float MarkerLabelWidth = 8;

        #endregion

        #region Fields

        private static readonly Color ColorOff = new Color(1f, 0.6f, 0.6f);

        private static readonly Color ColorOn = new Color(0.5f, 1, 0.5f);

        private static readonly Color ColorPause = new Color(1, 1, 0.4f);

        private static readonly Color ColorCoroutine = new Color(1, 0.6f, 1);

        private static readonly Color ColorError = Color.red;

        private static readonly Color ColorObjectIsAsset = new Color(0.8f, 0.8f, 1f);

        private static readonly string[] TypeNameReplaced = new string[] { ", Version=", ", Culture=", ", PublicKeyToken=", ", Assembly-CSharp", ", mscorlib", ", UnityEngine." };

        private static readonly string[] TypeNameReplacing = new string[] { "", "", "", "", "", ", UnityEngine" };

        private static string FixNameFrom;

        private static string FixNameTo;

        private static GUIStyle ButtonLeft = MakeButtonLeftStyle();

        private static string ExposedTypePath = null;

        private static string ExposedMethodSignaturePath = null;

        private static bool ShowNonPublicMethods = false;

        private static List<DrawerDefiniton> _AllDrawers;

        private static List<string> _AllTypeNames;

        private static string CurrentTypeName = "";

        private static List<Type> GenericArgumentBuilder = new List<Type>();

        private static string[] _LayerNames;

        private static int refIndex;

        private static int insertIndex;

        private static List<CallMap> CopiedEvents = new List<CallMap>();

        private static string CopiedTag;

        private static ArgumentMap CopiedArgument;

        private static Type CopiedType;

        private static Type PropertyType;

        private static Dictionary<MethodInfo, MethodCache> CachedReflection = new Dictionary<MethodInfo, MethodCache>();

        private static FieldInfo PropertyAttributeField = typeof(PropertyDrawer).GetField("m_Attribute", BindingFlags.Instance | BindingFlags.NonPublic);

        private static FieldInfo PropertyFieldInfoField = typeof(PropertyDrawer).GetField("m_FieldInfo", BindingFlags.Instance | BindingFlags.NonPublic);

        private static Action<ArgumentDrawer, MethodInfo, ParameterInfo, Type, Attribute> ArgumentDrawerSetupDelegate = MakeArgumentDrawerSetupDelegate();

        private static Dictionary<Type, TypeCache> CachedTypes = new Dictionary<Type, TypeCache>();

        private static GUIContent CustomEventArg = new GUIContent("Event arg", "Custom event argument");

        private ExtendedEventAttribute settings = new ExtendedEventAttribute();

        private Type _EventsType;

        private Type _TagType;

        private Type _ArgumentType;

        private PropertyDrawer _tagDrawer;

        private string labelText;

        private SerializedProperty listenersArray;

        private SerializedProperty parentProperty;

        //State:
        private ReorderableList reorderableList;

        private int lastSelectedIndex;

        private Dictionary<string, State> states = new Dictionary<string, State>();

        private HashSet<int> RepeatingIDs = new HashSet<int>();

        #endregion

        public ExtendedEventDrawer() {
            ExposedTypePath = null;
            ExposedMethodSignaturePath = null;
        }

        public ExtendedEventDrawer(Type tagType, Type argumentType) {
            ExposedTypePath = null;
            ExposedMethodSignaturePath = null;

            _TagType = tagType;
            _ArgumentType = argumentType;
        }

        private static List<DrawerDefiniton> AllDrawers {
            get {
                if (_AllDrawers == null) {
                    _AllDrawers = new List<DrawerDefiniton>();

                    Assembly[] assemblies = new[] { Assembly.Load("Assembly-CSharp-Editor"), Assembly.Load("Assembly-CSharp") };

                    foreach (var assembly in assemblies) {
                        foreach (Type type in assembly.GetTypes()) {

                            if (!typeof(PropertyDrawer).IsAssignableFrom(type) && !typeof(ArgumentDrawer).IsAssignableFrom(type)) continue;

                            foreach (var attribute in type.GetCustomAttributes<CustomParameterDrawer>()) {
                                if (attribute.type == null) {
                                    Debug.LogError($"{attribute} type must not be null");
                                    continue;
                                }
                                _AllDrawers.Add(new DrawerDefiniton(attribute, type));
                            }
                        }
                    }
                }
                return _AllDrawers;
            }
        }

        private static List<string> AllTypeNames {
            get {
                if (_AllTypeNames == null) {
                    _AllTypeNames = new List<string>();

                    IEnumerable<Type> types =
                    from a in AppDomain.CurrentDomain.GetAssemblies()
                    from t in a.GetTypes()
                    select t;

                    foreach (Type t in types) {
                        _AllTypeNames.Add(GetSerializableTypeName(t));
                    }

                    _AllTypeNames.Sort();
                }
                return _AllTypeNames;
            }
        }

        private static string[] LayerNames {
            get {
                if (_LayerNames == null) {
                    _LayerNames = new string[32];
                    for (int i = 0; i < _LayerNames.Length; ++i) {
                        _LayerNames[i] = LayerMask.LayerToName(i);
                        if (string.IsNullOrEmpty(_LayerNames[i])) _LayerNames[i] = $"Layer {i}";
                    }
                }
                return _LayerNames;
            }
        }

        /// <summary>Type of ExtendedEvent.Argument used in this ExtendedEvent</summary>
        private Type ArgumentType {
            get {
                GetEventsUnderlyingTypes();
                return _ArgumentType;
            }
        }

        /// <summary>Type of Tag used in this ExtendedEvent</summary>
        private PropertyDrawer tagDrawer {
            get {
                GetEventsUnderlyingTypes();
                return _tagDrawer;
            }
        }

        /// <summary>Type of Tag used in this ExtendedEvent</summary>
        private Type TagType {
            get {
                GetEventsUnderlyingTypes();
                return _TagType;
            }
        }

        #region Enums

        enum ContextOperation {
            Nothing,
            Dublicate,
            Add,
            PasteOne,
            DeleteOne,
            CopyID,
            PasteID,
            RandomizeID,
        }

        enum ArgumentOperation {
            Nothing,
            ToggleModifyFlag,
            PasteEventID,
            Copy,
            CopyID,
            Paste,
            MakeCachedArgument,
            SetEnumerator,
            CopyParameterType,
            CopyArgumentType,
            TogglePreview,
            ToggleIntAsEnum,
        }

        public enum DelayModeInstantOnly {
            NoDelay,
        }

        private enum ParameterTypeEnum {
            /// <summary>Represents serializable type</summary>
            Specific = 0,
            /// <summary>Represents unserializable generic parameter</summary>
            GenericParameter = 1,
            /// <summary>Represents unserializable open constructed type parameter (SomeType&lt;T&gt;)</summary>
            OpenConstructedType = 2,
        }

        #endregion

        public static void ArgumentField(Rect position, SerializedProperty property, GUIContent label, Type parameterType, bool canBeNull) {
            var parameterTypeEnum = GetTypeEnum(parameterType);

            switch (parameterTypeEnum) {
                case TypeEnum.Float:
                    FloatField(position, property.FindPropertyRelative(FloatArgumentFieldName), label);
                    break;
                case TypeEnum.Boolean:
                    BoolField(position, property.FindPropertyRelative(BoolArgumentFieldName), label, "true", "false");
                    break;
                case TypeEnum.Generic:
                case TypeEnum.Object:
                    GenericField(position, property.FindPropertyRelative(ObjectArgumentFieldName), label, parameterType, canBeNull);
                    break;
                case TypeEnum.Color:
                    ColorField(position, label, property, true, true, false);
                    break;
                case TypeEnum.Vector2:
                    Vector2Field(position, property.FindPropertyRelative(Vector3ArgumentFieldName), label);
                    break;
                case TypeEnum.Vector3:
                    Vector3Field(position, property.FindPropertyRelative(Vector3ArgumentFieldName), label);
                    break;
                case TypeEnum.Quaternion:
                    QuaternionField(position, property.FindPropertyRelative(Vector3ArgumentFieldName), label);
                    break;
                case TypeEnum.Vector4:
                case TypeEnum.Rect:
                    Vector4Field(position, property.FindPropertyRelative(Vector3ArgumentFieldName), property.FindPropertyRelative(FloatArgumentFieldName), label);
                    break;
                case TypeEnum.String:
                    StringField(position, label, property.FindPropertyRelative(StringArgumentFieldName));
                    break;
                case TypeEnum.Type:
                    TypeField(position, property.FindPropertyRelative(StringArgumentFieldName), label);
                    break;
                case TypeEnum.Character:
                    CharField(position, property.FindPropertyRelative(IntArgumentFieldName));
                    break;
                case TypeEnum.LayerMask:
                    LayerMaskFieldField(position, property.FindPropertyRelative(IntArgumentFieldName), label);
                    break;
                case TypeEnum.Integer:
                    IntField(position, label, property.FindPropertyRelative(IntArgumentFieldName));
                    break;
                case TypeEnum.Enum:
                    EnumField(position, property.FindPropertyRelative(IntArgumentFieldName), label, parameterType);
                    break;

            }
        }

        public static void BoolField(Rect position, SerializedProperty property, GUIContent label, string onText, string offText) {
            float nameOffset = EditorGUIUtility.labelWidth + 16;
            Rect valueNameRect = DivideRect(ref position, nameOffset, position.width - nameOffset);
            string toggleName = property.boolValue ? onText : offText;
            GUI.Label(valueNameRect, new GUIContent(toggleName, toggleName));

            property.boolValue = EditorGUI.Toggle(position, label, property.boolValue);
        }

        public static GenericMenu BuildPopupListForComponents(GameObject gameObject, SerializedProperty objectArgument, Type desiredType) {
            List<Object> returnList = new List<Object>();
            var menu = new GenericMenu();
            returnList.Add(gameObject);
            returnList.AddRange(gameObject.GetComponents<Component>());
            returnList.RemoveAll(o => !o);
            for (int i = 0; i < returnList.Count; i++) {
                if (!desiredType.IsAssignableFrom(returnList[i].GetType())) {
                    returnList.RemoveAt(i);
                    i--;
                }
            }
            returnList.Insert(0, null);
            foreach (var obj in returnList) {
                menu.AddItem(new GUIContent(obj ? obj.GetType().Name : "None"), objectArgument.objectReferenceValue == obj, SelectObject, new PropertyValue<Object>(objectArgument, obj));
            }
            return menu;
        }

        public static GenericMenu BuildPopupListForType(SerializedProperty argumentBase, Type desiredType) {
            var target = argumentBase.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue;
            var stringArgument = argumentBase.FindPropertyRelative(StringArgumentFieldName);
            var menu = new GenericMenu();
            {
                Type copiedType = GetCopiedType();
                if (copiedType != null) menu.AddItem(new GUIContent($"{copiedType.Name} (copied type)"), desiredType == copiedType, SelectType, new TypeContextData(stringArgument, copiedType));

                var primitivesPrefix = "Primitives and structs" + "/ ";
                menu.AddItem(new GUIContent(primitivesPrefix + typeof(void).Name), desiredType == typeof(void), SelectType, new TypeContextData(stringArgument, typeof(void)));
                TypeEnum[] supportedTypes = (TypeEnum[])Enum.GetValues(typeof(TypeEnum));
                foreach (var typeEnum in supportedTypes) {
                    Type argType;
                    switch (typeEnum) {
                        case TypeEnum.Generic:
                            argType = typeof(object);
                            break;
                        case TypeEnum.Enum:
                            argType = typeof(Enum);
                            break;
                        case TypeEnum.Float:
                            argType = typeof(float);
                            break;
                        case TypeEnum.Integer:
                            argType = typeof(int);
                            break;
                        case TypeEnum.Character:
                            argType = typeof(char);
                            break;
                        case TypeEnum.String:
                            argType = typeof(string);
                            break;
                        case TypeEnum.Boolean:
                            argType = typeof(bool);
                            break;
                        case TypeEnum.Vector2:
                            argType = typeof(Vector2);
                            break;
                        case TypeEnum.Vector3:
                            argType = typeof(Vector3);
                            break;
                        case TypeEnum.Vector4:
                            argType = typeof(Vector4);
                            break;
                        case TypeEnum.Rect:
                            argType = typeof(Rect);
                            break;
                        case TypeEnum.Color:
                            argType = typeof(Color);
                            break;
                        case TypeEnum.Quaternion:
                            argType = typeof(Quaternion);
                            break;
                        case TypeEnum.LayerMask:
                            argType = typeof(LayerMask);
                            break;
                        case TypeEnum.Type:
                            argType = typeof(Type);
                            break;
                        default:
                            continue;
                    }
                    menu.AddItem(new GUIContent(primitivesPrefix + GetDisplayTypeName(argType)), desiredType == argType, SelectType, new TypeContextData(stringArgument, argType));
                }
            }
            if (CopiedArgument?.objectArgument != null) {
                var objectArgumentType = CopiedArgument.objectArgument.GetType();
                var objectTypes = GetTypes(objectArgumentType);
                foreach (var genericType in objectTypes) {
                    menu.AddItem(new GUIContent(objectArgumentType.Name + "/ " + genericType.Name), desiredType == genericType, SelectType, genericType);
                }
            }
            menu.AddItem(new GUIContent(typeof(Object).Name), desiredType == typeof(Object), SelectType, new TypeContextData(stringArgument, typeof(Object)));
            menu.AddItem(new GUIContent(typeof(GameObject).Name), desiredType == typeof(GameObject), SelectType, new TypeContextData(stringArgument, typeof(GameObject)));
            if (target) {
                Object[] objectArray;
                if (target is Component targetComponent) {
                    objectArray = targetComponent.GetComponents<Component>();
                }
                else if (target is GameObject targetGameobject) {
                    objectArray = targetGameobject.GetComponents<Component>();
                }
                else {
                    objectArray = new Object[] { target };
                }
                foreach (var uObject in objectArray) {
                    var objectArgumentType = uObject.GetType();
                    var objectTypes = GetTypes(objectArgumentType);
                    foreach (var genericType in objectTypes) {
                        menu.AddItem(new GUIContent(objectArgumentType.Name + "/ " + genericType.Name), desiredType == genericType, SelectType, genericType);
                    }
                }
            }
            return menu;
        }

        public static void CharField(Rect position, SerializedProperty intArgument) {
            char charValue = (char)intArgument.intValue;
            string stringValue = EditorGUI.TextField(position, charValue.ToString());
            if (stringValue.Length > 0) charValue = stringValue[0];
            intArgument.intValue = charValue;
        }

        public static void ClearArray(object data) {
            SerializedProperty listenersArray = (SerializedProperty)data;
            listenersArray.ClearArray();
            listenersArray.serializedObject.ApplyModifiedProperties();
        }

        public static void ColorField(Rect position, GUIContent label, SerializedProperty argumentProperty, bool showEyedropper, bool showAlpha, bool hdr) {
            SerializedProperty vector3Property = argumentProperty.FindPropertyRelative(Vector3ArgumentFieldName);
            SerializedProperty floatProperty = argumentProperty.FindPropertyRelative(FloatArgumentFieldName);

            EditorGUI.BeginChangeCheck();

            Vector3 vector3Value = vector3Property.vector3Value;
            Color color = new Color(vector3Value.x, vector3Value.y, vector3Value.z, floatProperty.floatValue);
            color = EditorGUI.ColorField(position, label, color, showEyedropper, showAlpha, hdr);

            if (EditorGUI.EndChangeCheck()) {
                vector3Property.vector3Value = new Vector3(color.r, color.g, color.b);
                floatProperty.floatValue = color.a;
            }
        }

        /// <summary>Draws parameter label and reduces the size of the rect accordingly </summary>
        public static void DrawLabel(ref Rect position, GUIContent label) {
            if (label == GUIContent.none) return;
            Rect labelRect = DivideRect(ref position, EditorGUIUtility.labelWidth, position.width - EditorGUIUtility.labelWidth, true);
            EditorGUI.LabelField(labelRect, label);
        }

        public static void EnumField(Rect position, SerializedProperty intProperty, GUIContent label, Type enumType) {
            EditorGUI.BeginProperty(position, label, intProperty);
            EditorGUI.BeginChangeCheck();

            Enum targetEnum = (Enum)Enum.ToObject(enumType, intProperty.intValue);

            Enum enumNew = GetTypeCache(enumType).IsFlags ? EditorGUI.EnumFlagsField(position, label, targetEnum) : EditorGUI.EnumPopup(position, label, targetEnum);
            int newValue = (int)Convert.ChangeType(enumNew, enumType);

            if (EditorGUI.EndChangeCheck()) {
                intProperty.intValue = newValue;
            }
            EditorGUI.EndProperty();
        }

        public static void FloatField(Rect position, SerializedProperty property, GUIContent label) {
            property.floatValue = EditorGUI.FloatField(position, label, property.floatValue);
        }

        public static void GenericField(Rect position, SerializedProperty objectArgument, GUIContent label, Type desiredType, bool canBeNull) {
            //static classes are not serializable
            if (desiredType.IsAbstract && desiredType.IsSealed) {
                EditorGUI.LabelField(position, new GUIContent("Static", "Static classes are not serializable!"));
                return;
            }
            var targetGameObject = GetGameObject(objectArgument.objectReferenceValue);
            if (targetGameObject) {
                GenericMenu menu = BuildPopupListForComponents(targetGameObject, objectArgument, desiredType);
                if (menu.GetItemCount() > 1) {
                    Rect compNameRect = DivideRect(ref position, position.width - 20, 20);
                    if (GUI.Button(compNameRect, new GUIContent(">"), EditorStyles.miniButton)) {
                        menu.DropDown(compNameRect);
                    }
                }
            }
            Color backgroundColor = GUI.backgroundColor;
            if (!canBeNull && !objectArgument.objectReferenceValue) {
                GUI.backgroundColor = ColorError;
            }
            ObjectField(position, objectArgument, label, desiredType);
            GUI.backgroundColor = backgroundColor;
        }

        public static int GetArgumentID(int callId, int argumentIndex) => unchecked(callId + (argumentIndex + 1) * 486187739);

        public static string GetDisplayMethodName(MethodInfo mi) {
            if (mi == null) return "null";
            var methodName = mi.Name;
            var parameters = mi.GetParameters();

            if (mi.IsSpecialName) return GetSimpleMethodName(mi);

            var args = new StringBuilder();

            if (mi.IsGenericMethod) {
                Type[] genericArguments = mi.GetGenericArguments();
                args.Append('<');
                for (int i = 0; i < genericArguments.Length; i++) {
                    args.Append(GetDisplayTypeName(genericArguments[i]));
                    if (i < genericArguments.Length - 1) args.Append(',');
                }
                args.Append('>');
            }

            args.Append("(");

            for (int i = 0; i < parameters.Length; i++) {
                args.Append(GetDisplayTypeName(parameters[i].ParameterType));
                if (i != parameters.Length - 1) args.Append(", ");
            }

            args.Append(")");

            var returnType = mi.ReturnType;
            string returnTypeName = returnType != typeof(void) ? $"{GetDisplayTypeName(returnType)} " : null;

            return $"{returnTypeName}{methodName}{args}";
        }

        public static string GetDisplayTypeName(Type t) {
            if (t == null) return "null";
            if (t == typeof(object)) return "object";
            if (t == typeof(int)) return "int";
            if (t == typeof(float)) return "float";
            if (t == typeof(string)) return "string";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(char)) return "char";
            if (t == typeof(uint)) return "uint";
            if (t == typeof(long)) return "long";
            if (t == typeof(double)) return "double";
            string name = t.ToString();
            if (name.StartsWith("System.Collections.Generic.")) name = name.Remove(0, "System.Collections.Generic.".Length);
            else if (name.StartsWith("System.Collections.")) name = name.Remove(0, "System.Collections.".Length);
            else if (name.StartsWith("System.")) name = name.Remove(0, "System.".Length);
            //else if (name.StartsWith("ExtendedEvents.")) name = name.Remove(0, "ExtendedEvents.".Length);
            name = name.Replace("UnityEngine.", "");

            if (name.Contains("`1[")) {
                name = name.Replace("System.Object", "object");
                name = name.Replace("System.Int32", "int");
                name = name.Replace("System.Single", "float");
                name = name.Replace("System.String", "string");
                name = name.Replace("System.Boolean", "bool");
                name = name.Replace("System.Char", "char");
                name = name.Replace("System.UInt32", "uint");
                name = name.Replace("System.Int64", "long");
                name = name.Replace("System.Double", "double");
                name = name.Replace("[]", "!");
                name = name.Replace("`1[", "<");
                name = name.Replace("]", ">");
                name = name.Replace("!", "[]");
            }
            return name;
        }

        public static string GetFormattedMethodName(string methodType, string groupName, string targetName, string methodName) {
            char? targetSlash = (targetName != null) ? '/' : default(char?);
            char? methodTypeSlash = (methodType != null) ? '/' : default(char?);
            char? groupSlash = (groupName != null) ? '/' : default(char?);
            return $"{targetName}{targetSlash}{methodType}{methodTypeSlash}{groupName}{groupSlash}{methodName}";
        }

        public static GameObject GetGameObject(Object target) {
            if (!target) return null;
            if (target is Component targetComponent) {
                return targetComponent.gameObject;
            }
            else if (target is GameObject targetGameObject) {
                return targetGameObject;
            }
            return null;
        }

        public static string GetSerializableTypeName(Type type) {
            if (type == null) return typeof(object).FullName;
            string typeName = type.AssemblyQualifiedName;
            if (typeName == null) {
                return type.FullName ?? type.Name;
            }
            for (int i = 0; i < TypeNameReplaced.Length; i++) {
                string key = TypeNameReplaced[i];
                while (typeName.Contains(key)) {
                    int startIndex = typeName.IndexOf(key);
                    int count = typeName.Length - startIndex;
                    for (int j = startIndex + key.Length; j < typeName.Length; j++) {
                        if (typeName[j] == ']' || typeName[j] == ',') {
                            count = j - startIndex;
                            break;
                        }
                    }
                    if (count > 0) {
                        string newTypeName = typeName.Replace(typeName.Substring(startIndex, count), TypeNameReplacing[i]);
                        if (ExtendedEvent.GetType(typeName) == null) break;
                        typeName = newTypeName;
                    }
                }
            }

            //last resort attempt at serialization. In 99.9% of cases this shouldn't happen
            if (ExtendedEvent.GetType(typeName) == null) typeName = type.AssemblyQualifiedName;
            return typeName;
        }

        //
        public static string GetSimpleMethodName(MethodInfo mi) {
            var methodName = mi.Name;
            if (mi.IsSpecialName) {
                if (methodName.Contains("get_")) {
                    methodName = $"{methodName.Replace("get_", "")} {{ get; }}";
                }
                else if (methodName.Contains("set_")) {
                    methodName = $"{methodName.Replace("set_", "")} {{ set; }}";
                }
                else if (methodName.StartsWith("add_")) {
                    methodName = $"add {methodName.Substring(4)}";
                }
                else if (methodName.StartsWith("remove_")) {
                    methodName = $"remove {methodName.Substring(7)}";
                }
                else if (methodName == "op_Implicit" || methodName == "op_Explicit") {
                    methodName = $"{GetDisplayTypeName(mi.ReturnType)}({mi.GetParameters()[0].ParameterType.Name})";
                }
                else {
                    string op = null;
                    switch (methodName) {
                        case "op_Equality":
                            op = "==";
                            break;
                        case "op_Inequality":
                            op = "!=";
                            break;
                        case "op_GreaterThan":
                            op = ">";
                            break;
                        case "op_LessThan":
                            op = "<";
                            break;
                        case "op_GreaterThanOrEqual":
                            op = ">=";
                            break;
                        case "op_LessThanOrEqual":
                            op = "<=";
                            break;
                        case "op_BitwiseAnd":
                            op = "&&";
                            break;
                        case "op_BitwiseOr":
                            op = "||";
                            break;
                        case "op_Addition":
                            op = "+";
                            break;
                        case "op_Subtraction":
                            op = "-";
                            break;
                        case "op_Division":
                            op = "÷";
                            break;
                        case "op_Modulus":
                            op = "%";
                            break;
                        case "op_Multiply":
                            op = "*";
                            break;
                        case "op_LeftShift":
                            op = "<<";
                            break;
                        case "op_RightShift":
                            op = ">>";
                            break;
                        case "op_ExclusiveOr":
                            op = "^";
                            break;
                        case "op_UnaryNegation":
                            op = "-";
                            break;
                        case "op_UnaryPlus":
                            op = "+";
                            break;
                        case "op_LogicalNot":
                            op = "!";
                            break;
                        case "op_OnesComplement":
                            op = "~";
                            break;
                        case "op_False":
                            op = "false";
                            break;
                        case "op_True":
                            op = "true";
                            break;
                        case "op_Increment":
                            op = "++";
                            break;
                        case "op_Decrement":
                            op = "--";
                            break;
                    }
                    var parameters = mi.GetParameters();
                    if (parameters.Length == 1) {
                        methodName = $"{op}({GetDisplayTypeName(parameters[0].ParameterType)})";
                    }
                    else if (parameters.Length == 2) {
                        methodName = $"{GetDisplayTypeName(parameters[0].ParameterType)} {op} {GetDisplayTypeName(parameters[1].ParameterType)}";
                    }
                }
            }

            if (mi.ReturnType != typeof(void)) methodName = $"{GetDisplayTypeName(mi.ReturnType)} {methodName}";

            return methodName;
        }

        public static TypeCache GetTypeCache(Type type) {
            if (type == null) return null;
            TypeCache cache;
            if (CachedTypes.TryGetValue(type, out cache)) return cache;
            else {
                cache = new TypeCache(type);
                CachedTypes.Add(type, cache);
                return cache;
            }
        }

        public static IEnumerable<Type> GetTypes(Type type) {
            // is there any base type?
            if (type == null) {
                yield break;
            }
            yield return type;
            // return all implemented or inherited interfaces
            foreach (var i in type.GetInterfaces()) {
                yield return i;
            }

            // return all inherited types
            var currentBaseType = type.BaseType;
            while (currentBaseType != null && currentBaseType != typeof(object) && currentBaseType != typeof(Object)) {
                yield return currentBaseType;
                currentBaseType = currentBaseType.BaseType;
            }
        }

        public static object GetValue(SerializedProperty prop) {
            object obj = null;
            if (prop == null) return null;
            switch (prop.propertyType) {
                case SerializedPropertyType.Integer:
                    obj = prop.intValue;
                    break;
                case SerializedPropertyType.LayerMask:
                    obj = (LayerMask)prop.intValue;
                    break;
                case SerializedPropertyType.Enum:
                    obj = prop.intValue;
                    break;
                case SerializedPropertyType.Boolean:
                    obj = prop.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    obj = prop.floatValue;
                    break;
                case SerializedPropertyType.String:
                    obj = prop.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    obj = prop.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    obj = prop.objectReferenceValue;
                    break;
                case SerializedPropertyType.Vector2:
                    obj = prop.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    obj = prop.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    obj = prop.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    obj = prop.rectValue;
                    break;
                case SerializedPropertyType.ArraySize:
                    obj = prop.arraySize;
                    break;
                case SerializedPropertyType.Character:
                    obj = (char)prop.intValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    obj = prop.animationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    obj = prop.boundsValue;
                    break;
                case SerializedPropertyType.Gradient:
                    obj = GetGradientValue(prop);
                    break;
                case SerializedPropertyType.Quaternion:
                    obj = prop.quaternionValue;
                    break;
                case SerializedPropertyType.Generic:
                    obj = GetTargetObjectOfProperty(prop);
                    break;
            }

            return obj;
        }

        public static object GetTargetObjectOfProperty(SerializedProperty prop) {
            string path = prop.propertyPath.Replace(".Array.data[", "[");
            object obj = prop.serializedObject.targetObject;
            var elements = path.Split('.');
            foreach (string element in elements) {
                if (element.Contains("[")) {
                    string elementName = element.Substring(0, element.IndexOf("["));
                    int index = Convert.ToInt32(element.Substring(element.IndexOf("[")).Replace("[", "").Replace("]", ""));
                    obj = GetValue_Imp(obj, elementName, index);
                }
                else {
                    obj = GetValue_Imp(obj, element);
                }
            }
            return obj;
        }

        private static object GetValue_Imp(object source, string name) {
            if (source == null) return null;

            var type = source.GetType();

            while (type != null) {
                FieldInfo f = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                if (f != null) return f.GetValue(source);
                type = type.BaseType;
            }
            return null;
        }

        private static object GetValue_Imp(object source, string name, int index) {
            IEnumerable enumerable = GetValue_Imp(source, name) as IEnumerable;
            if (enumerable == null) return null;
            IEnumerator enm = enumerable.GetEnumerator();

            for (int i = 0; i <= index; i++) {
                if (!enm.MoveNext()) return null;
            }
            return enm.Current;
        }

        public static void IntField(Rect position, GUIContent label, SerializedProperty property) {
            property.intValue = EditorGUI.IntField(position, label, property.intValue);
        }

        public static void LayerMaskFieldField(Rect position, SerializedProperty property, GUIContent label) {
            property.intValue = EditorGUI.MaskField(position, label, property.intValue, LayerNames);
        }

        public static void ObjectField(Rect position, SerializedProperty property, GUIContent label, Type desiredType) {
            EditorGUI.BeginProperty(position, GUIContent.none, property);

            EditorGUI.BeginChangeCheck();
            if (property.objectReferenceValue && !desiredType.IsCastableFrom(property.objectReferenceValue.GetType())) GUI.backgroundColor = ColorError;

            Object result = EditorGUI.ObjectField(position, label, property.objectReferenceValue, typeof(Object), true);
            if (EditorGUI.EndChangeCheck()) {
                bool applyChange = false;
                if (result) {
                    if (desiredType.IsCastableFrom(result.GetType())) applyChange = true;
                    else {
                        if (typeof(Component).IsAssignableFrom(desiredType) || desiredType.IsInterface) {
                            if (result is MonoBehaviour resMono) result = resMono.GetComponent(desiredType);
                            else if (result is GameObject resGo) result = resGo.GetComponent(desiredType);
                            if (result) applyChange = true;
                        }
                        else {
                            Component[] components = Array.Empty<Component>();
                            if (result is MonoBehaviour resMono) components = resMono.GetComponents<Component>();
                            else if (result is GameObject resGo) components = resGo.GetComponents<Component>();
                            result = null;
                            foreach (var c in components) {
                                if (desiredType.IsCastableFrom(c.GetType())) {
                                    result = c;
                                    break;
                                }
                            }
                        }
                        if (result) applyChange = true;
                    }
                }
                else applyChange = true;

                if (result && !desiredType.IsCastableFrom(result.GetType())) {
                    Debug.LogWarning($"{result} can't be cast to {desiredType}");
                    applyChange = false;
                }

                if (applyChange) property.objectReferenceValue = result;
            }

            EditorGUI.EndProperty();
        }

        public static void RandomizeIDs(object data) {
            SerializedProperty listenersArray = (SerializedProperty)data;
            for (int i = 0; i < listenersArray.arraySize; i++) {
                var current = listenersArray.GetArrayElementAtIndex(i);
                current.FindPropertyRelative(IDFieldName).intValue = GenerateID(listenersArray);
            }
            listenersArray.serializedObject.ApplyModifiedProperties();
        }

        public static void SetValue(SerializedProperty prop, object obj) {
            if (prop == null) return;
            switch (prop.propertyType) {
                case SerializedPropertyType.ArraySize:
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.LayerMask:
                case SerializedPropertyType.Enum:
                    prop.intValue = TypeCasterUtility.Cast<int>(obj);
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = TypeCasterUtility.Cast<bool>(obj);
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = TypeCasterUtility.Cast<float>(obj);
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = TypeCasterUtility.Cast<string>(obj);
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = TypeCasterUtility.Cast<Color>(obj);
                    break;
                case SerializedPropertyType.Generic:
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = obj as Object;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = TypeCasterUtility.Cast<Vector2>(obj);
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = TypeCasterUtility.Cast<Vector3>(obj);
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = TypeCasterUtility.Cast<Vector4>(obj);
                    break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = TypeCasterUtility.Cast<Rect>(obj);
                    break;
                case SerializedPropertyType.Character:
                    prop.intValue = TypeCasterUtility.Cast<char>(obj);
                    break;
                case SerializedPropertyType.AnimationCurve:
                    prop.animationCurveValue = TypeCasterUtility.Cast<AnimationCurve>(obj);
                    break;
                case SerializedPropertyType.Bounds:
                    prop.boundsValue = TypeCasterUtility.Cast<Bounds>(obj);
                    break;
                case SerializedPropertyType.Gradient:
                    SetGradientValue(prop, TypeCasterUtility.Cast<Gradient>(obj));
                    break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = TypeCasterUtility.Cast<Quaternion>(obj);
                    break;
            }
        }

        public static void TypeField(Rect position, SerializedProperty property, GUIContent label) {
            Rect labelRect = DivideRect(ref position, EditorGUIUtility.labelWidth, position.width - EditorGUIUtility.labelWidth, true);
            EditorGUI.LabelField(labelRect, label);
            DrawTypeSelector(position, property, ExtendedEvent.GetType(property.stringValue), property.stringValue, SetTypeValue);
        }

        private static void CallArgumentOperation(object data) {
            ((ArgumentContextData)data).Execute();
        }

        private static void CallContextOperation(object data) {
            ((CallContextData)data).Execute();
        }

        private static bool CheckMethodForFunc(MethodInfo method, Type parameterType) {
            if (parameterType == null) return true;

            if (!parameterType.IsCastableFrom(method.ReturnType, true)) return false;

            return true;
        }

        private static void ClearEventFunction(object source) {
            ((ExtendedEventFunction)source).Clear();
        }

        private static void DrawPreviewer(Rect position, SerializedProperty property, MethodCache method) {
            EditorGUIUtility.labelWidth = GetLabelWidth(position);
            EditorGUIUtility.DrawColorSwatch(position, Color.magenta);
            DrawLabel(ref position, new GUIContent("p", "preview window"));

            if (method != null) {
                ParameterCache[] parameters = method.GetParameters(false);

                object[] arguments;
                switch (parameters.Length) {
                    default:
                    case 0:
                        arguments = Array.Empty<object>();
                        break;
                    case 1:
                        object arg = GetFuncArgument(property, parameters[0].ParameterType.type);
                        arguments = new object[] { arg };
                        break;
                    case 2:
                        object arg1 = GetFuncArgument(property, parameters[0].ParameterType.type);
                        object arg2 = GetFuncArgument(property, parameters[1].ParameterType.type);
                        arguments = new object[] { arg1, arg2 };
                        break;
                }

                object result = null;
                Exception exception = null;
                try {
                    result = method.method.Invoke(method.IsStatic ? null : GetFuncArgument(property, method.ReflectedType.type), arguments);
                }
                catch (Exception e) {
                    exception = e;
                }

                if (exception != null) {
                    EditorGUIUtility.DrawColorSwatch(position, new Color(1f, 0.5f, 0.5f));
                    GUI.Label(position, new GUIContent(exception.InnerException.GetType().Name, exception.ToString()));
                }
                else if (result == null) {
                    if (IsObjectOrInterface(method.ReturnType.type)) EditorGUI.ObjectField(position, null, typeof(Object), true);
                    else GUI.Label(position, "null");
                }
                else if (result is Object ro) EditorGUI.ObjectField(position, ro, typeof(Object), true);
                else if (result is Color rc) EditorGUI.ColorField(position, rc);
                else if (result is AnimationCurve ra) {
                    AnimationCurve raNew = new AnimationCurve(ra.keys);
                    EditorGUI.CurveField(position, raNew);
                }
                else if (result is Gradient rg) {
                    Gradient rgNew = new Gradient();
                    rgNew.colorKeys = rg.colorKeys;
                    rgNew.alphaKeys = rg.alphaKeys;
                    rgNew.mode = rg.mode;
                    EditorGUI.GradientField(position, rgNew);
                }
                else {
                    string text = result.ToString();
                    GUI.TextField(position, text);
                }
            }
            else {
                EditorGUI.LabelField(position, new GUIContent("---", "Method is missing"));
            }
        }

        private static void DrawTypeSelector(Rect propertyRect, SerializedProperty property, Type reflectedType, string reflectedTypeName, GenericMenu.MenuFunction2 func) {
            Color backgroundColor = GUI.backgroundColor;

            GUI.backgroundColor = (reflectedType != null || string.IsNullOrEmpty(property.stringValue)) ? backgroundColor : ColorError;

            if (ExposedTypePath == null || ExposedTypePath != property.propertyPath) {
                if (GUI.Button(propertyRect, new GUIContent(reflectedType != null ? reflectedTypeName : (string.IsNullOrEmpty(reflectedTypeName) ? "Type" : $"<{reflectedTypeName}>"), reflectedTypeName), ButtonLeft)) {
                    ExposedTypePath = property.propertyPath;
                    CurrentTypeName = reflectedTypeName;
                }
            }
            else {
                Rect selectTypeRect = DivideRect(ref propertyRect, propertyRect.width - 20, 20);

                CurrentTypeName = EditorGUI.TextField(propertyRect, CurrentTypeName);

                if (GUI.Button(selectTypeRect, new GUIContent(">"), EditorStyles.miniButton)) {
                    if (CurrentTypeName.Length >= 2) {
                        GenericMenu selectTypeMenu = new GenericMenu();
                        bool addSeparator = false;

                        Type inputType = ExtendedEvent.GetType(CurrentTypeName);
                        if (inputType != null) {
                            selectTypeMenu.AddItem(new GUIContent(GetSerializableTypeName(inputType)), true, func, new PropertyValue<string>(property, GetSerializableTypeName(inputType)));
                            addSeparator = true;
                        }

                        int maxCount = 990;
                        foreach (var t in AllTypeNames) {
                            if (t.IndexOf(CurrentTypeName, 0, StringComparison.CurrentCultureIgnoreCase) != -1) {
                                if (addSeparator) {
                                    selectTypeMenu.AddSeparator("");
                                    addSeparator = false;
                                }
                                selectTypeMenu.AddItem(new GUIContent(t), t == GetSerializableTypeName(reflectedType), func, new PropertyValue<string>(property, t));
                                maxCount--;
                                if (maxCount <= 0) {
                                    Debug.LogWarning("Too many matches, try better specify type name");
                                    break;
                                }
                            }
                        }
                        selectTypeMenu.DropDown(selectTypeRect);
                    }
                    else {
                        ExposedTypePath = null;
                        CurrentTypeName = "";
                    }

                }
            }

            GUI.backgroundColor = backgroundColor;
        }

        private static void ExposeMethodSignature(object userData) {
            string path = ((SerializedProperty)userData).propertyPath;
            if (ExposedMethodSignaturePath == path) ExposedMethodSignaturePath = null;
            else ExposedMethodSignaturePath = path;
        }

        private static MethodCache FindArgumentFunc(SerializedProperty argument) {
            MethodInfo method = ExtendedEvent.FindMethod(argument.FindPropertyRelative(StringArgumentFieldName).stringValue);

            return FindMethod(method);
        }

        private static MethodCache FindMethod(MethodInfo method) {
            if (method == null) return null;
            MethodCache cache;
            if (CachedReflection.TryGetValue(method, out cache)) return cache;
            else {
                cache = new MethodCache(method);
                CachedReflection.Add(method, cache);
                return cache;
            }
        }

        private static MethodCache FindMethod(SerializedProperty call) {
            var methodName = call.FindPropertyRelative(MethodNameFieldName);

            MethodInfo method = ExtendedEvent.FindMethod(methodName.stringValue);

            return FindMethod(method);
        }

        /*
        private static void InsertAtIndex(SerializedProperty calls, int index) {
            uint id = GenerateID(calls);
            calls.InsertArrayElementAtIndex(index);
            calls.GetArrayElementAtIndex(index).FindPropertyRelative(kID).longValue = id;
        }
        */
        private static int GenerateID(SerializedProperty calls) {
            System.Random random = new System.Random();
            //return random.Next(int.MinValue, int.MaxValue);
            int id = 0;
            for (int i = 0; i < calls.arraySize; i++) {
                if (i == 0) id = random.Next(int.MinValue, int.MaxValue);
                if (id == calls.GetArrayElementAtIndex(i).FindPropertyRelative(IDFieldName).intValue) i = -1;
            }
            return id;
        }

        private static ArgType GetArgType(Argument.Definition definition) {
            if ((definition & Argument.Definition.IsMethod) != 0) return ArgType.Method;
            return (ArgType)(definition & Argument.Definition.Arg1TypeFlags);
        }

        private static Type[] GetArgumentTypes(SerializedProperty call, MethodCache method) {

            SerializedProperty arguments = call.FindPropertyRelative(ArgumentsFieldName);

            Type[] argumentTypes = new Type[arguments.arraySize];
            if (method != null) {
                TypeCache[] parameters = method.GetParameterTypes(true);
                for (int i = 0; i < parameters.Length; i++) {
                    argumentTypes[i] = parameters[i].type;
                }
            }
            else {
                string signature = call.FindPropertyRelative(MethodNameFieldName).stringValue;
                if (!string.IsNullOrEmpty(signature)) {
                    string[] data = signature.Split(';');

                    if (data.Length == 1) {
                        var type = ExtendedEvent.GetType(data[0]) ?? typeof(object);
                        argumentTypes = new Type[] { (type.IsAbstract && type.IsSealed) ? typeof(object) : type };
                    }
                    else {
                        int parameterCount = data.Length - 2;

                        if (parameterCount < 0) {
                            Debug.LogError("Argument count doesn't match method signature");
                        }
                        else {
                            for (int i = 2; i < data.Length; i++) {
                                if (data[i][data[i].Length - 1] == '*') {
                                    parameterCount = i - 2;
                                    break;
                                }
                            }

                            bool methodIsStatic = parameterCount == arguments.arraySize;

                            if (!methodIsStatic) {
                                argumentTypes[0] = ExtendedEvent.GetType(data[0]);
                            }

                            for (int i = 0; i < parameterCount; i++) {
                                argumentTypes[i + (methodIsStatic ? 0 : 1)] = ExtendedEvent.GetType(data[i + 2]);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < argumentTypes.Length; i++) {
                if (argumentTypes[i] == null) argumentTypes[i] = typeof(object);
            }

            return argumentTypes;
        }

        private static Type GetCopiedType() {
            Type type = ExtendedEvent.GetType(EditorGUIUtility.systemCopyBuffer);
            if (type == null) type = ExtendedEvent.GetType($"UnityEngine.{EditorGUIUtility.systemCopyBuffer}, UnityEngine");
            return type;
        }

        private static Vector2Int GetDataAddress(SerializedProperty calls, int id) {
            for (int i = 0; i < calls.arraySize; i++) {
                var call = calls.GetArrayElementAtIndex(i);
                int callID = call.FindPropertyRelative(IDFieldName).intValue;
                if (id == callID) return new Vector2Int(i, -1);
                for (int j = 0; j < call.FindPropertyRelative(ArgumentsFieldName).arraySize; j++) {
                    if (id == GetArgumentID(callID, j)) return new Vector2Int(i, j);
                }
            }
            return new Vector2Int(-1, -1);
        }

        private static PropertyInfo GetDefiningProperty(MethodInfo method) {
            if (method == null) return null;
            if (!method.IsSpecialName) return null;
            var methodParameters = method.GetParameters();
            if (methodParameters.Length > 1) return null;
            var takesArg = methodParameters.Length == 1;
            var hasReturn = method.ReturnType != typeof(void);
            if (takesArg == hasReturn) return null;

            PropertyInfo pi = null;

            Type baseType = method.ReflectedType;
            do {
                if (takesArg && !hasReturn) {
                    pi = baseType.GetProperties().FirstOrDefault(prop => prop.GetSetMethod() == method);
                }
                else {
                    pi = baseType.GetProperties().FirstOrDefault(prop => prop.GetGetMethod() == method);
                }
                baseType = baseType.BaseType;
            }
            while (pi == null && baseType != typeof(object) && baseType != null);

            return pi;
        }

        private static string GetDisplayFuncName(string funcName) {
            int parserIndex = funcName.IndexOf(';');
            if (parserIndex != -1) funcName = funcName.Substring(0, parserIndex);
            return funcName;
        }

        private static DrawerDefiniton GetDrawerDefinition(ParameterInfo parameter, Type type, Attribute attribute) {
            DrawerDefiniton current = null;

            PropertyInfo property = null;
            if (parameter != null) {
                property = GetDefiningProperty((MethodInfo)parameter.Member);
            }

            foreach (var d in AllDrawers) {
                if (d.key.isParameter) {
                    if (parameter != null) {
                        if (!d.key.type.IsAssignableFrom(parameter.Member.ReflectedType)) continue;
                        if (property == null && !string.IsNullOrEmpty(d.key.methodName) && d.key.methodName != parameter.Member.Name) continue;
                        if (!string.IsNullOrEmpty(d.key.parameterName) && d.key.parameterName != (property != null ? property.Name : parameter.Name)) continue;
                        if (d.key.parameterType != null && d.key.parameterType != parameter.ParameterType) continue;
                    }
                    else continue;
                }
                else {
                    if (d.key.useForChildren) {
                        if (!d.key.type.IsAssignableFrom(type)) continue;
                    }
                    else {
                        if (d.key.type != type) continue;
                    }
                }

                if (current == null) current = d;
                else {
                    if (d.key.isParameter && !current.key.isParameter) current = d;
                    else if (current.key.type.IsAssignableFrom(d.key.type)) current = d;
                    else if (d.key.isParameter && current.key.isParameter) {

                        int GetAttributeValidityIndex(CustomParameterDrawer a) {
                            int index = 0;
                            if (!string.IsNullOrEmpty(a.parameterName)) index += 2;
                            if (a.parameterType != null) index += 1;
                            return index;
                        }

                        int ci = GetAttributeValidityIndex((CustomParameterDrawer)current.key);
                        int di = GetAttributeValidityIndex((CustomParameterDrawer)d.key);
                        if (di > ci) current = d;
                    }
                }
            }

            return current;
        }

        private static Type GetDrawerType(ParameterInfo parameter, Type type, out Attribute attribute) {

            attribute = null;
            if (parameter != null) attribute = GetParameterAttribute<PropertyAttribute>(parameter);
            if (attribute == null) attribute = type.GetCustomAttribute<PropertyAttribute>();
            if (attribute != null) type = attribute.GetType();

            return GetDrawerDefinition(parameter, type, attribute)?.drawerType;
        }

        private static int GetElementIndex(SerializedProperty prop) {
            return Mathf.Clamp(Mathf.FloorToInt(refIndex), 0, Mathf.Max(0, prop.arraySize - 1));
        }

        private static string GetEmptyMethodName(TypeCache type) {
            if (type == null) return "No function";
            else return type.DisplayName;
        }

        private static int GetForeachIndex(SerializedProperty call, SerializedProperty arguments) {
            int foreachIndex = -1;
            EventCall.Definition argumentSpecialFlags = (EventCall.Definition)call.FindPropertyRelative(CallDefinionFieldName).intValue;

            if (argumentSpecialFlags != 0) {
                for (int i = 0; i < arguments.arraySize; i++) {
                    if ((argumentSpecialFlags & (EventCall.Definition)(1 << i)) != 0) {
                        foreachIndex = i;
                        break;
                    }
                }
            }
            return foreachIndex;
        }

        private static object GetFuncArgument(SerializedProperty prop, Type type) {
            TypeEnum typeEnum = GetTypeEnum(type);
            switch (typeEnum) {
                default:
                case TypeEnum.Unknown:
                    return null;
                case TypeEnum.Object:
                case TypeEnum.Generic:
                    return TypeCasterUtility.Cast(prop.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue, type);
                case TypeEnum.Integer:
                    return prop.FindPropertyRelative(IntArgumentFieldName).intValue;
                case TypeEnum.LayerMask:
                    return (LayerMask)prop.FindPropertyRelative(IntArgumentFieldName).intValue;
                case TypeEnum.Character:
                    return (char)prop.FindPropertyRelative(IntArgumentFieldName).intValue;
                case TypeEnum.Enum:
                    return Enum.ToObject(type, prop.FindPropertyRelative(IntArgumentFieldName).intValue);
                case TypeEnum.Float:
                    return prop.FindPropertyRelative(FloatArgumentFieldName).floatValue;
                case TypeEnum.Vector2:
                    return (Vector2)prop.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value;
                case TypeEnum.Vector3:
                    return prop.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value;
                case TypeEnum.Quaternion:
                    return Quaternion.Euler(prop.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value);
                case TypeEnum.Color:
                    Vector3 vc = prop.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value;
                    return new Color(vc.x, vc.y, vc.z, prop.FindPropertyRelative(FloatArgumentFieldName).floatValue);
                case TypeEnum.Vector4:
                case TypeEnum.Rect:
                    Vector3 v4 = prop.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value;
                    return new Vector4(v4.x, v4.y, v4.z, prop.FindPropertyRelative(FloatArgumentFieldName).floatValue);
                case TypeEnum.Boolean:
                    return prop.FindPropertyRelative(BoolArgumentFieldName).boolValue;
                case TypeEnum.String:
                    return prop.FindPropertyRelative(StringArgumentFieldName).stringValue;
                case TypeEnum.Type:
                    return ExtendedEvent.GetType(prop.FindPropertyRelative(StringArgumentFieldName).stringValue);
            }
        }

        private static Gradient GetGradientValue(SerializedProperty prop) {
            PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (propertyInfo == null) return null;

            return propertyInfo.GetValue(prop, null) as Gradient;
        }

        private static GUIContent GetLabel(string text, Rect rect) {
            float width = rect.width;
            if (width < 36) return GUIContent.none;
            EditorGUIUtility.labelWidth = Mathf.Max(width * 0.3f, MinLabelWidth);
            return new GUIContent(text, text);
        }

        private static float GetLabelWidth(Rect position) => Mathf.Min(64, position.width / 8);

        private static Color GetObjectFieldColor(SerializedProperty property) {
            if (property.objectReferenceValue) {
                GameObject go = null;
                if (property.objectReferenceValue is Component c) go = c.gameObject;
                else if (property.objectReferenceValue is GameObject g) go = g;
                if (go && !go.scene.IsValid()) return ColorObjectIsAsset;
                else return GUI.backgroundColor;
            }
            return GUI.backgroundColor;
        }

        private static IEnumerable<ValidMethodMap> GetOrderedMethods(List<ValidMethodMap> methods) {
            IEnumerable<ValidMethodMap> orderedMethods = methods.
                OrderBy(e => e.methodInfo.IsSpecialName ? 0 : 1).
                ThenBy(e => e.methodInfo.ReturnType == typeof(void) ? 0 : 1).
                ThenBy(e => e.methodInfo.Name);
            return orderedMethods;
        }

        private static T GetParameterAttribute<T>(ParameterInfo parameter) where T : Attribute {
            if (!((MethodInfo)parameter.Member).IsSpecialName) {
                return parameter.GetCustomAttribute<T>();
            }
            else {
                T a = parameter.Member.GetCustomAttribute<T>();
                if (a == null) a = GetDefiningProperty((MethodInfo)parameter.Member)?.GetCustomAttribute<T>();
                return a;
            }
        }

        private static ParameterTypeEnum GetParameterType(MethodInfo method, int index) {
            ParameterTypeEnum parameterTypeEnum = ParameterTypeEnum.Specific;
            if (method.IsGenericMethod) {
                var methodDefinition = method.GetGenericMethodDefinition();
                Type methodDefinitionParameter;
                if (!method.IsStatic && index == 0) {
                    methodDefinitionParameter = methodDefinition.ReflectedType;
                }
                else {
                    methodDefinitionParameter = methodDefinition.GetParameters()[index - (method.IsStatic ? 0 : 1)].ParameterType;
                }

                if (methodDefinitionParameter.IsGenericParameter) {
                    parameterTypeEnum = ParameterTypeEnum.GenericParameter;
                }
                else if (methodDefinitionParameter.ContainsGenericParameters) {
                    parameterTypeEnum = ParameterTypeEnum.OpenConstructedType;
                }
            }
            return parameterTypeEnum;
        }

        private static void GetRects(Rect rect, out Rect enabledRect, out Rect tagRect, out Rect delayModeRect, out Rect methodRect, out Rect dataRect) {
            float totalHeight = rect.height;

            rect.height = EditorGUIUtility.singleLineHeight;
            Rect row1 = rect;

            tagRect = DivideRect(ref row1, 25, 75, true);
            enabledRect = DivideRect(ref tagRect, 15, tagRect.width - 15, true);
            methodRect = DivideRect(ref row1, 1, 2);
            delayModeRect = row1;

            rect.y = row1.yMax + EditorGUIUtility.standardVerticalSpacing;
            dataRect = rect;
        }

        private static string GetSerializableMethodName(MethodInfo method) {
            StringBuilder sb = new StringBuilder();

            if (method.IsStatic || (GetTypeEnum(method.DeclaringType) == GetTypeEnum(method.ReflectedType))) {
                sb.Append(GetSerializableTypeName(method.DeclaringType));
            }
            else {
                sb.Append(GetSerializableTypeName(method.ReflectedType));
            }

            sb.Append(';');

            sb.Append(method.Name);

            foreach (var p in method.GetParameters()) {
                sb.Append(';');
                sb.Append(GetSerializableTypeName(p.ParameterType));
            }

            Type[] genericArguments = method.GetGenericArguments();
            if (genericArguments.Length > 0) {

                MethodInfo definition = method.GetGenericMethodDefinition();
                ParameterInfo[] definitionParameters = definition.GetParameters();
                Type[] definitionGenericArguments = definition.GetGenericArguments();

                sb.Append(';');
                foreach (var p in definitionParameters) {
                    Type type = p.ParameterType;
                    if (type.IsGenericParameter) {
                        if (type == definitionGenericArguments[0]) sb.Append('T');
                        else if (type == definitionGenericArguments[1]) sb.Append('U');
                        else if (type == definitionGenericArguments[2]) sb.Append('V');
                        else if (type == definitionGenericArguments[3]) sb.Append('W');
                    }
                    else if (type.ContainsGenericParameters) {
                        sb.Append('t');
                    }
                    else {
                        sb.Append('s');
                    }
                }
                sb.Append('*');

                foreach (var t in genericArguments) {
                    sb.Append(';');
                    sb.Append(GetSerializableTypeName(t));
                }
            }
            return sb.ToString();
        }

        //private static string GetCallFormattedName(EventCall call) => $"{call.target} {call.methodName}";
        private static IEnumerable<Type> GetTypesAndGenericArguments(Type type) {
            // return all inherited types
            var currentBaseType = type;
            while (currentBaseType != null) {
                yield return currentBaseType;
                if (currentBaseType.IsGenericType) {
                    foreach (var c in currentBaseType.GetGenericArguments()) yield return c;
                }
                currentBaseType = currentBaseType.BaseType;
            }

            // return all implemented or inherited interfaces
            foreach (var i in type.GetInterfaces()) {
                yield return i;
                if (i.IsGenericType) {
                    foreach (var c in i.GetGenericArguments()) yield return c;
                }
            }
        }

        //
        private static void GetValidMethods(ref List<ValidMethodMap> validMethods, IEnumerable<MethodInfo> methods, Object target, Type desiredReturnType) {

            foreach (var method in methods) {
                if (method.IsGenericMethod) {
                    {
                        var vmm = new ValidMethodMap();
                        vmm.target = target;
                        vmm.methodInfo = method;
                        vmm.methodName = $"{GetDisplayMethodName(method)} / {GetDisplayMethodName(vmm.methodInfo)}";
                        validMethods.Add(vmm);
                    }

                    var genericArguments = method.GetGenericArguments();
                    //this approach is not suitable for methods with multiple generic arguments
                    if (genericArguments.Length != 1) continue;


                    Type genericArgument = genericArguments[0];

                    //is true if this generic method is serializable
                    bool serializable = desiredReturnType != null;
                    //if this method has at least one generic parameter that is used as an argument
                    if (!serializable) {
                        foreach (var p in method.GetParameters()) {
                            if (p.ParameterType == genericArgument) {
                                serializable = true;
                                break;
                            }
                        }
                    }

                    if (!serializable) continue;

                    HashSet<Type> possibleTypes = new HashSet<Type>();
                    if (desiredReturnType != null) possibleTypes.UnionWith(GetTypesAndGenericArguments(desiredReturnType));
                    possibleTypes.UnionWith(genericArgument.GetGenericParameterConstraints());
                    if (target) possibleTypes.UnionWith(GetTypesAndGenericArguments(target.GetType()));
                    Type bufferType = GetCopiedType();
                    if (bufferType != null) {
                        possibleTypes.Add(bufferType);
                        //possibleTypes.UnionWith(GetTypesAndGenericArguments(bufferType));
                    }
                    if (CopiedArgument?.objectArgument != null) {
                        possibleTypes.UnionWith(GetTypesAndGenericArguments(CopiedArgument.objectArgument.GetType()));
                    }
                    //possibleTypes.Add(typeof(object));
                    //possibleTypes.UnionWith(GetSerializableTypes());

                    foreach (var substitutedType in possibleTypes) {
                        MethodInfo specificMethod = null;

                        bool methodIsValid = true;

                        try {
                            specificMethod = method.MakeGenericMethod(substitutedType);
                        }
                        catch (ArgumentException) {
                            methodIsValid = false;
                        }

                        if (methodIsValid && method.IsStatic) {
                            var methodParameters = specificMethod.GetParameters();
                            if (target && methodParameters.Length >= 1) methodIsValid = methodParameters[0].ParameterType.IsAssignableFrom(target.GetType());
                        }

                        if (!methodIsValid) continue;

                        if (!CheckMethodForFunc(specificMethod, desiredReturnType)) continue;

                        var vmm = new ValidMethodMap();
                        vmm.target = target;
                        vmm.methodInfo = specificMethod;
                        vmm.methodName = $"{GetDisplayMethodName(method)} / {GetDisplayMethodName(vmm.methodInfo)}";
                        validMethods.Add(vmm);
                    }

                }
                else {

                    if (!CheckMethodForFunc(method, desiredReturnType)) continue;

                    var vmm = new ValidMethodMap();
                    vmm.target = target;
                    vmm.methodInfo = method;
                    vmm.methodName = GetDisplayMethodName(method);
                    validMethods.Add(vmm);
                }
            }
        }

        private static bool IsObjectOrInterface(Type type) {
            if (typeof(Object).IsAssignableFrom(type)) return true;
            return type.IsInterface;
        }

        private static void LabelField(Rect position, GUIContent label, GUIContent label2) {
            DrawLabel(ref position, label);
            EditorGUI.LabelField(position, label);
        }

        private static Action<ArgumentDrawer, MethodInfo, ParameterInfo, Type, Attribute> MakeArgumentDrawerSetupDelegate() {
            MethodInfo method = typeof(ArgumentDrawer).GetMethod("Setup", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(MethodInfo), typeof(ParameterInfo), typeof(Type), typeof(Attribute) }, null);
            return (Action<ArgumentDrawer, MethodInfo, ParameterInfo, Type, Attribute>)method.CreateDelegate(typeof(Action<ArgumentDrawer, MethodInfo, ParameterInfo, Type, Attribute>));
        }

        private static GUIStyle MakeButtonLeftStyle() {
            GUIStyle style = new GUIStyle(EditorStyles.miniButton);
            style.alignment = TextAnchor.MiddleLeft;
            return style;
        }

        private static object MakeDrawer(MethodInfo method, ParameterInfo parameter, Type type) {
            Type drawerType = GetDrawerType(parameter, type, out Attribute attribute);
            if (drawerType == null) return null;
            return MakePropertyDrawer(method, parameter, type, attribute, drawerType);
        }

        private static Type MakeGenericEnumerableType(Type type) => typeof(IEnumerable<>).MakeGenericType(new[] { type });

        private static Type MakeGenericIListType(Type type) => typeof(IList<>).MakeGenericType(new[] { type });

        private static object MakePropertyDrawer(MethodInfo method, ParameterInfo parameter, Type type, Attribute attribute, Type drawerType) {
            object drawer = drawerType.GetConstructor(Type.EmptyTypes).Invoke(Array.Empty<object>());

            if (drawer is ArgumentDrawer argumentDrawer) {
                ArgumentDrawerSetupDelegate(argumentDrawer, method, parameter, type, attribute);
            }
            else if (drawer is PropertyDrawer) {
                PropertyFieldInfoField.SetValue(drawer, typeof(DrawerField<>).MakeGenericType(type).GetField("field", BindingFlags.NonPublic | BindingFlags.Static));
                if (attribute is PropertyAttribute) PropertyAttributeField.SetValue(drawer, attribute);
            }
            return drawer;
        }

        private static PropertyDrawer MakeTagDrawer(Type type, ArgumentAttribute attribute) {
            DrawerDefiniton drawerDefinition = GetDrawerDefinition(null, attribute.GetType(), attribute);
            if (drawerDefinition == null) return null;
            return MakePropertyDrawer(null, null, type, attribute, drawerDefinition.drawerType) as PropertyDrawer;
        }

        private static bool PropertyIsArgument(SerializedProperty property) => property.FindPropertyRelative(ObjectArgumentFieldName) != null;

        private static void QuaternionField(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.BeginChangeCheck();
            Vector3Field(position, property, label);
            if (EditorGUI.EndChangeCheck()) {
                property.vector3Value = new Vector3(
                    Mathf.Clamp(property.vector3Value.x, 0, 360),
                    Mathf.Clamp(property.vector3Value.y, 0, 360),
                    Mathf.Clamp(property.vector3Value.z, 0, 360)
                    );
            }
        }

        private static void SelectCall(object data) {
            var call = (SelectedIData)data;
            call.intArgument.intValue = call.id;
            call.intArgument.serializedObject.ApplyModifiedProperties();
        }

        private static void SelectObject(object data) {
            var op = (PropertyValue<Object>)data;
            op.property.objectReferenceValue = op.value;
            op.property.serializedObject.ApplyModifiedProperties();
        }

        private static void SelectType(object data) {
            var op = (TypeContextData)data;
            op.stringArgument.stringValue = GetSerializableTypeName(op.type);
            op.stringArgument.serializedObject.ApplyModifiedProperties();
        }

        private static void SetArgumentType(object data) {
            PropertyValue<Argument.Definition> pv = (PropertyValue<Argument.Definition>)data;

            pv.property.intValue &= (int)~(Argument.Definition.IsMethod | Argument.Definition.Arg1TypeFlags);
            pv.property.intValue |= (int)pv.value;

            if ((pv.value & Argument.Definition.IsMethod) == 0) {
                pv.property.intValue &= (int)~Argument.Definition.NegateBool;
                pv.property.intValue &= (int)~Argument.Definition.CacheReturnValue;
            }
            if ((pv.value & (Argument.Definition.IsMethod | Argument.Definition.Arg1TypeFlags)) != 0) {
                pv.property.intValue &= (int)~Argument.Definition.NegateBool;
                pv.property.intValue &= (int)~Argument.Definition.CacheReturnValue;
            }

            pv.property.serializedObject.ApplyModifiedProperties();
        }

        private static void SetEventFunction(object source) {
            ((ExtendedEventFunction)source).Assign();
        }

        private static void SetFuncArgumentType(object data) {
            PropertyValue<Vector2Int> pv = (PropertyValue<Vector2Int>)data;


            pv.property.intValue &= ~pv.value.y;
            pv.property.intValue |= pv.value.x;

            pv.property.serializedObject.ApplyModifiedProperties();
        }

        private static void SetGradientValue(SerializedProperty prop, Gradient gradient) {
            PropertyInfo propertyInfo = typeof(SerializedProperty).GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (propertyInfo == null)
                return;

            propertyInfo.SetValue(prop, gradient, null);
        }

        private static void SetInt(object data) {
            var pair = (PropertyValue<int>)data;
            pair.property.intValue = pair.value;
            pair.property.serializedObject.ApplyModifiedProperties();
        }

        private static void SetMethodReflectedTypeName(object source) {
            SerializedProperty methodName = ((PropertyValue<string>)source).property;
            string value = ((PropertyValue<string>)source).value;

            string[] data = methodName.stringValue.Split(';');

            if (data.Length >= 2) {
                StringBuilder sb = new StringBuilder(methodName.stringValue);

                sb.Remove(0, data[0].Length).Insert(0, value);
                methodName.stringValue = sb.ToString();

            }
            else {
                methodName.stringValue = value;
            }
            methodName.serializedObject.ApplyModifiedProperties();

            ExposedTypePath = null;
        }

        private static void SetTypeValue(object source) {
            SerializedProperty typeField = ((PropertyValue<string>)source).property;
            string value = ((PropertyValue<string>)source).value;

            typeField.stringValue = value;

            typeField.serializedObject.ApplyModifiedProperties();

            ExposedTypePath = null;
        }

        private static void StringField(Rect position, GUIContent label, SerializedProperty property) {
            property.stringValue = EditorGUI.TextField(position, label, property.stringValue);
        }

        private static void ToggleFlag<T>(SerializedProperty property, T flag) where T : Enum {
            var definition = property.FindPropertyRelative(ArgumentDefitionFieldName);

            int flagInt = (int)(object)flag;
            bool flagIsSet = (definition.intValue & flagInt) != 0;

            if (flagIsSet) definition.intValue &= ~flagInt;
            else definition.intValue |= flagInt;
        }

        private static void ToggleShowNonPublicMethodsFlag() {
            ShowNonPublicMethods = !ShowNonPublicMethods;
        }

        private static void Vector2Field(Rect position, SerializedProperty property, GUIContent label) {
            bool isVector3 = property.propertyType == SerializedPropertyType.Vector3;
            Vector2 vector2 = isVector3 ? (Vector2)property.vector3Value : property.vector2Value;
            if (position.width > 150) {
                if (isVector3) property.vector3Value = EditorGUI.Vector2Field(position, label, vector2);
                else property.vector2Value = EditorGUI.Vector2Field(position, label, vector2);
            }
            else {
                Rect[] rects = DivideRect(position, 1, 1);
                if (isVector3) property.vector3Value = new Vector2(EditorGUI.FloatField(rects[0], vector2.x), EditorGUI.FloatField(rects[1], vector2.y));
                else property.vector2Value = new Vector2(EditorGUI.FloatField(rects[0], vector2.x), EditorGUI.FloatField(rects[1], vector2.y));
            }
        }

        private static void Vector3Field(Rect position, SerializedProperty property, GUIContent label) {
            Vector3 vector3 = property.vector3Value;
            if (position.width > 200) property.vector3Value = EditorGUI.Vector3Field(position, label, vector3);
            else {
                DrawLabel(ref position, label);

                Rect[] rects = DivideRect(position, 1, 1, 1);
                property.vector3Value = new Vector3(
                    EditorGUI.FloatField(rects[0], vector3.x),
                    EditorGUI.FloatField(rects[1], vector3.y),
                    EditorGUI.FloatField(rects[2], vector3.z)
                    );
            }
        }

        private static void Vector4Field(Rect position, SerializedProperty vector3Property, SerializedProperty floatProperty, GUIContent label) {
            Rect wRect = DivideRect(ref position, 3, 1);
            Vector3Field(position, vector3Property, label);
            floatProperty.floatValue = EditorGUI.FloatField(wRect, GetLabel("w", wRect), floatProperty.floatValue);
        }

        public void DrawArgumentContext(SerializedProperty call, int argumentIndex, Type parameterType, bool isEnumerable) {
            SerializedProperty argument = call.FindPropertyRelative(ArgumentsFieldName).GetArrayElementAtIndex(argumentIndex);

            var previewFunc = argument.FindPropertyRelative(FuncPreviewFieldName);

            SerializedProperty objectArgument = argument.FindPropertyRelative(ObjectArgumentFieldName);
            SerializedProperty definitionArgument = argument.FindPropertyRelative(ArgumentDefitionFieldName);

            Argument.Definition argumentDefinition = (Argument.Definition)definitionArgument.intValue;
            ArgType argumentTypeEnum = GetArgType(argumentDefinition);

            bool isCachedArgument = (argumentDefinition & Argument.Definition.CacheReturnValue) != 0;
            bool isNegated = (argumentDefinition & Argument.Definition.NegateBool) != 0;

            var parameterTypeEnum = GetTypeEnum(parameterType);

            GenericMenu context = new GenericMenu();

            ArgumentContextData contextData = new ArgumentContextData(call, argumentIndex);

            Type copiedType = GetCopiedType();

            context.AddItem(new GUIContent("Argument type / Data"), argumentTypeEnum == ArgType.Data, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.None));
            context.AddSeparator("Argument type /");
            context.AddItem(new GUIContent("Argument type / Func"), argumentTypeEnum == ArgType.Method, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.IsMethod));
            if (settings.parented) {
                context.AddSeparator("Argument type /");
                context.AddItem(new GUIContent("Argument type / Parent"), argumentTypeEnum == ArgType.Parent, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.Arg1IsParent));
            }
            context.AddSeparator("Argument type /");
            context.AddItem(new GUIContent("Argument type / ID Reference"), argumentTypeEnum == ArgType.IDReference, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.Arg1IsIDReference));
            context.AddItem(new GUIContent("Argument type / Tag Reference"), argumentTypeEnum == ArgType.TagReference, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.Arg1IsTagReference));
            context.AddSeparator("Argument type /");
            context.AddItem(new GUIContent("Argument type / Custom Event Args"), argumentTypeEnum == ArgType.CustomEventArg, SetArgumentType, new PropertyValue<Argument.Definition>(definitionArgument, Argument.Definition.Arg1IsCustomEventArgs));

            if (argumentTypeEnum == ArgType.Method) {
                if (isNegated || typeof(bool).IsCastableFrom(parameterType, false)) {
                    context.AddSeparator("Argument type /");
                    context.AddItem(new GUIContent("Argument modifiers / Negate bool"), isNegated, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.ToggleModifyFlag));
                }
            }

            if (isCachedArgument || argumentTypeEnum == ArgType.Method) {
                context.AddSeparator("");
                context.AddItem(new GUIContent("Cache argument"), isCachedArgument, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.MakeCachedArgument));

            }

            context.AddSeparator("");

            context.AddItem(new GUIContent("Make iterator"), isEnumerable, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.SetEnumerator));

            if (parameterTypeEnum == TypeEnum.Integer && CopiedEvents.Count == 1) {
                context.AddSeparator("");
                context.AddItem(new GUIContent("Paste event id"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.PasteEventID));
            }

            context.AddSeparator("");
            context.AddItem(new GUIContent("Copy argument"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.Copy));
            context.AddItem(new GUIContent($"Copy argument ID: {GetArgumentID(call.FindPropertyRelative(IDFieldName).intValue, argumentIndex)}"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.CopyID));
            if (CopiedArgument != null)
                context.AddItem(new GUIContent("Paste argument"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.Paste));
            context.AddSeparator("");
            if (copiedType != parameterType)
                context.AddItem(new GUIContent($"Copy parameter type {GetDisplayTypeName(parameterType)}"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.CopyParameterType));

            Type argumentType = parameterType;
            if (argumentTypeEnum != ArgType.IDReference) {
                if (argumentTypeEnum != ArgType.Method) argumentType = FindArgumentFunc(argument)?.ReturnType.type ?? parameterType;
                else if (parameterTypeEnum == TypeEnum.Object && objectArgument.objectReferenceValue) argumentType = objectArgument.objectReferenceValue.GetType();
            }

            if (argumentType != parameterType)
                context.AddItem(new GUIContent($"Copy argument type {GetDisplayTypeName(argumentType)}"), false, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.CopyArgumentType));

            if (argumentTypeEnum == ArgType.Method) {
                context.AddSeparator("");
                if (previewFunc.boolValue) context.AddItem(new GUIContent("Preview"), previewFunc.boolValue, CallArgumentOperation, contextData.SetOperation(ArgumentOperation.TogglePreview));
                else context.AddDisabledItem(new GUIContent("Preview"), previewFunc.boolValue);
            }
            context.ShowAsContext();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            RestoreState(property);

            float height = 0f;
            if (reorderableList != null) {
                height = reorderableList.GetHeight();
            }
            return height;
        }

        public void OnGUI(Rect position) {
            if (listenersArray == null || !listenersArray.isArray)
                return;

            if (reorderableList != null) {
                var oldIdentLevel = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                reorderableList.DoList(position);
                EditorGUI.indentLevel = oldIdentLevel;
            }
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {

            if (property.serializedObject.isEditingMultipleObjects) return;

            Rect foldoutRect = position;
            foldoutRect.height = EditorGUIUtility.singleLineHeight;

            EditorGUI.BeginProperty(position, label, property);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label);
            EditorGUI.EndProperty();

            if (property.isExpanded) {
                State state = RestoreState(property);

                if (attribute is ExtendedEventAttribute settings) this.settings = settings;

                HashSet<int> allIDs = new HashSet<int>();
                for (int i = 0; i < listenersArray.arraySize; i++) {
                    int id = listenersArray.GetArrayElementAtIndex(i).FindPropertyRelative(IDFieldName).intValue;
                    if (!allIDs.Add(id)) RepeatingIDs.Add(id);
                }

                Rect callMenuRect = position;
                callMenuRect.yMin += EditorGUIUtility.singleLineHeight;

                Event evt = Event.current;
                if (!property.serializedObject.isEditingMultipleObjects && evt.type == EventType.MouseDown && evt.button == 1 && position.Contains(evt.mousePosition)) {

                    float mouseY = evt.mousePosition.y - (position.yMin + EditorGUIUtility.singleLineHeight);
                    float currentY = 0;
                    for (int i = 0; i < listenersArray.arraySize; i++) {
                        float callHeight = GetElementHeight(i);
                        if (i == listenersArray.arraySize - 1 || mouseY <= currentY + callHeight) {
                            refIndex = i;
                            insertIndex = i;
                            if (mouseY > (currentY + callHeight * 0.5f)) insertIndex++;
                            break;
                        }
                        currentY += callHeight;
                    }

                    //refIndex = (e.mousePosition.y - (position.yMin + 16)) / GetSingleHeight();

                    GenericMenu context = new GenericMenu();

                    CallContextData contextData = new CallContextData(listenersArray, GetElementIndex(listenersArray));

                    if (listenersArray.arraySize > 0) {
                        int tagCount = GetTagCount(listenersArray);
                        string tag = GetTagName(listenersArray);
                        string copyOneText = tagCount > 1 ? "Copy 1 event" : "Copy";
                        context.AddItem(new GUIContent(copyOneText), false, CopyOne, listenersArray);
                        if (tagCount > 1) {
                            context.AddItem(new GUIContent($"Copy {tagCount} events with tag \"{tag}\""), false, CopyTag, listenersArray);
                        }
                        if (listenersArray.arraySize > tagCount) {
                            context.AddItem(new GUIContent($"Copy {listenersArray.arraySize} (all) events"), false, CopyAll, listenersArray);
                        }
                        context.AddSeparator("");
                        context.AddItem(new GUIContent("Dublicate"), false, CallContextOperation, contextData.SetOperation(ContextOperation.Dublicate));
                        context.AddItem(new GUIContent("Delete"), false, CallContextOperation, contextData.SetOperation(ContextOperation.DeleteOne));
                        if (CopiedEvents.Count == 1) {
                            context.AddItem(new GUIContent("Paste"), false, CallContextOperation, contextData.SetOperation(ContextOperation.PasteOne));
                        }
                    }
                    else {
                        context.AddDisabledItem(new GUIContent("Copy"));
                    }
                    context.AddSeparator("");

                    string addInsertText = listenersArray.arraySize > 0 ? "Insert" : "Add";
                    if (CopiedEvents.Count > 0) {
                        string addText;
                        if (CopiedEvents.Count == 1) {
                            addText = $"{addInsertText} 1 event \"{CopiedEvents[0].methodName}\"";
                        }
                        else {
                            if (CopiedTag != null) addText = $"{addInsertText} {CopiedEvents.Count} events with tag \"{CopiedTag}\"";
                            else addText = $"{addInsertText} {CopiedEvents.Count} events";
                        }

                        context.AddItem(new GUIContent(addText), false, CallContextOperation, contextData.SetOperation(ContextOperation.Add));
                    }
                    else {
                        context.AddDisabledItem(new GUIContent(addInsertText));
                    }
                    if (listenersArray.arraySize > 0) {
                        context.AddSeparator("");
                        context.AddItem(new GUIContent($"Copy call ID: {property.FindPropertyRelative(CallsFieldName).GetArrayElementAtIndex(GetElementIndex(listenersArray)).FindPropertyRelative(IDFieldName).intValue}"), false, CallContextOperation, contextData.SetOperation(ContextOperation.CopyID));
                        if (int.TryParse(EditorGUIUtility.systemCopyBuffer, out int temp)) context.AddItem(new GUIContent($"Paste ID: {temp}"), false, CallContextOperation, contextData.SetOperation(ContextOperation.PasteID));
                        context.AddItem(new GUIContent("Randomize ID"), false, CallContextOperation, contextData.SetOperation(ContextOperation.RandomizeID));
                    }

                    context.ShowAsContext();
                }

                labelText = label.text;

                //EditorGUI.BeginProperty(position, GUIContent.none, listenersArray);
                OnGUI(position);
                //EditorGUI.EndProperty();
                state.lastSelectedIndex = lastSelectedIndex;
            }
        }

        protected virtual void DrawEventHeader(Rect headerRect) {
            headerRect.height = 16;

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && Event.current.button == 1 && headerRect.Contains(evt.mousePosition)) {
                GenericMenu context = new GenericMenu();

                if (listenersArray.arraySize > 1) {
                    context.AddItem(new GUIContent("Randomize IDs"), false, RandomizeIDs, listenersArray);
                    context.AddSeparator("");
                }
                if (listenersArray.arraySize > 0) {
                    context.AddItem(new GUIContent("Clear"), false, ClearArray, listenersArray);
                    context.AddSeparator("");
                }
                context.AddItem(new GUIContent("Show NonPublic Methods"), ShowNonPublicMethods, ToggleShowNonPublicMethodsFlag);
                context.AddSeparator("");
                //context.AddItem(new GUIContent("Fix name"), FixNameWindowActive, ToggleFixNameWindow);
                //context.AddSeparator("");
                context.AddItem(new GUIContent("Copy type"), false, CopyEventsTypeName);
                context.ShowAsContext();
            }

            //FixMethodNames = EditorGUI.Toggle(headerRect, FixMethodNames);

            if (settings.parented) {
                DrawIsParentField(headerRect, new GUIContent(labelText), false);
            }
            else {
                EditorGUI.LabelField(headerRect, new GUIContent(labelText));
            }
        }

        private void AddEventListener(ReorderableList list) {

            if (listenersArray.hasMultipleDifferentValues) {
                //When increasing a multi-selection array using Serialized Property
                //Data can be overwritten if there is mixed values.
                //The Serialization system applies the Serialized data of one object, to all other objects in the selection.
                //We handle this case here, by creating a SerializedObject for each object.
                //Case 639025.
                foreach (var targetObject in listenersArray.serializedObject.targetObjects) {
                    var temSerialziedObject = new SerializedObject(targetObject);
                    var listenerArrayProperty = temSerialziedObject.FindProperty(listenersArray.propertyPath);
                    listenerArrayProperty.arraySize += 1;
                    listenerArrayProperty.GetArrayElementAtIndex(listenerArrayProperty.arraySize - 1).FindPropertyRelative(IDFieldName).intValue = GenerateID(listenerArrayProperty);
                    temSerialziedObject.ApplyModifiedProperties();
                }
                listenersArray.serializedObject.SetIsDifferentCacheDirty();
                listenersArray.serializedObject.Update();
                list.index = list.serializedProperty.arraySize - 1;

            }
            else {
                ReorderableList.defaultBehaviours.DoAddButton(list);
                var newElement = listenersArray.GetArrayElementAtIndex(listenersArray.arraySize - 1);
                newElement.FindPropertyRelative(IDFieldName).intValue = GenerateID(listenersArray);
                if (listenersArray.arraySize == 1) newElement.FindPropertyRelative(EnabledFieldName).boolValue = true;
            }

            lastSelectedIndex = list.index;
            var pListener = listenersArray.GetArrayElementAtIndex(list.index);
            if (pListener.FindPropertyRelative(ArgumentsFieldName).arraySize == 0) new ExtendedEventFunction(pListener, null, null).Clear();
        }

        private void AddFunctionForScript(GenericMenu menu, SerializedProperty listener, ValidMethodMap method, string targetName, bool addGroupName, MethodCache currentMethod) {
            if (!settings.parented && method.methodInfo.ReturnType == typeof(IEnumerator)) {
                return;
            }

            string groupName = null;
            string methodType = null;
            if (addGroupName) {
                PropertyInfo definingProperty = GetDefiningProperty(method.methodInfo);
                var methodGroupAttribute = ((MemberInfo)definingProperty ?? method.methodInfo).GetCustomAttribute<MethodGroupAttribute>();
                groupName = methodGroupAttribute?.groupName;

                if (method.methodInfo.IsSpecialName) {
                    if (method.methodInfo.Name.Contains("get_")) methodType = "Getters";
                    if (method.methodInfo.Name.Contains("set_")) methodType = "Setters";
                    else if (method.methodInfo.Name.StartsWith("op_")) methodType = "Operators";
                    else if (method.methodInfo.Name.StartsWith("add_") || method.methodInfo.Name.StartsWith("remove_")) methodType = "Delegates";
                }
                else {
                    if (method.methodInfo.ReturnType == typeof(void)) methodType = "Actions";
                    else if (method.methodInfo.ReturnType == typeof(IEnumerator)) methodType = "Coroutines";
                    else methodType = "Functions";
                }
            }

            // find the current event target...
            Object listenerTarget;

            if (PropertyIsArgument(listener)) {
                listenerTarget = listener.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue;
            }
            else {
                var argumentsArray = listener.FindPropertyRelative(ArgumentsFieldName);
                if (argumentsArray.arraySize > 0) listenerTarget = argumentsArray.GetArrayElementAtIndex(0).FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue;
                else listenerTarget = null;
            }

            var isCurrentlySet = ((!method.target) || listenerTarget == method.target) && currentMethod?.method == method.methodInfo;
            //we can add static and other shit here
            string path = GetFormattedMethodName(methodType, groupName, targetName, method.methodName);

            GenericMenu.MenuFunction2 func;
            object data;
            if (PropertyIsArgument(listener)) {
                func = FuncPopupDrawer.SetArgFunction;
                data = new FuncPopupDrawer.GetArgFunction(listener, method.target, method.methodInfo);
            }
            else {
                func = SetEventFunction;
                data = new ExtendedEventFunction(listener, method.target, method.methodInfo);
            }

            menu.AddItem(new GUIContent(path), isCurrentlySet, func, data);
        }

        private void AddMethodsToMenu(GenericMenu menu, SerializedProperty listener, List<ValidMethodMap> methods, string targetName, bool addGroupName, MethodCache currentMethod) {
            // Note: sorting by a bool in OrderBy doesn't seem to work for some reason, so using numbers explicitly.
            IEnumerable<ValidMethodMap> orderedMethods = GetOrderedMethods(methods);
            foreach (var validMethod in orderedMethods) {
                AddFunctionForScript(menu, listener, validMethod, targetName, addGroupName, currentMethod);
            }
        }

        private GenericMenu BuildDataList(GenericMenu menu, SerializedProperty calls, SerializedProperty id, Type desiredType) {
            for (int i = 0; i < calls.arraySize; i++) {
                var call = calls.GetArrayElementAtIndex(i);
                var arguments = call.FindPropertyRelative(ArgumentsFieldName);
                MethodCache callMethod = FindMethod(call);
                TypeCache callType = GetTypeCache(ExtendedEvent.GetType(call.FindPropertyRelative(MethodNameFieldName).stringValue));

                string callName = null;
                if (callMethod != null) callName = $"{callMethod.DisplayName}";
                else if (callType != null) callName = $"{callType.DisplayName}";

                string guiContent = $"{TagToString(call)} / Call {i}. {callName}";

                string addCallName = null;
                if (arguments.arraySize > 0) addCallName = $" / {callName}";

                if (callMethod != null || callType != null) {
                    Type callReturnType = callMethod?.ReturnType.type ?? callType.type;
                    string callReturnTypeDisplayName = callMethod?.DisplayName ?? callType.DisplayName;

                    string argName = null;
                    if (desiredType.IsCastableFrom(callReturnType, false)) argName = callReturnTypeDisplayName;
                    else if (desiredType.IsCastableFrom(callReturnType, true)) argName = $"{callReturnTypeDisplayName} as {GetDisplayTypeName(desiredType)}";
                    if (argName != null) {
                        menu.AddItem(new GUIContent(arguments.arraySize != 0 ? $"{guiContent}{addCallName}" : guiContent),
                            id.intValue == call.FindPropertyRelative(IDFieldName).intValue,
                            SetInt, new PropertyValue<int>(id, call.FindPropertyRelative(IDFieldName).intValue));
                    }
                }

                if (callType != null) continue;

                Type[] argumentTypes = GetArgumentTypes(call, callMethod);


                for (int j = 0; j < arguments.arraySize; j++) {
                    var arg = arguments.GetArrayElementAtIndex(j);

                    var refPropertyFlag = arg.FindPropertyRelative(ArgumentDefitionFieldName);
                    bool isMethod = ((Argument.Definition)refPropertyFlag.intValue & Argument.Definition.IsMethod) != 0;
                    bool isReference = ((Argument.Definition)refPropertyFlag.intValue & Argument.Definition.Arg1IsIDReference) != 0;
                    string argName = null;

                    if (isMethod && !isReference) {
                        var argMethod = FindArgumentFunc(arg);
                        if (argMethod != null) {
                            if (desiredType.IsCastableFrom(argMethod.ReturnType.type, false)) argName = argMethod.DisplayName;
                            else if (desiredType.IsCastableFrom(argMethod.ReturnType.type, true)) argName = $"{argMethod.DisplayName} as {GetDisplayTypeName(desiredType)}";
                        }
                    }
                    else {
                        Type argType = argumentTypes[j];
                        if (argType != null) {
                            if (desiredType.IsCastableFrom(argType, false)) argName = GetDisplayTypeName(argType);
                            else if (desiredType.IsCastableFrom(argType, true)) argName = $"{GetDisplayTypeName(argType)} as {GetDisplayTypeName(desiredType)}";
                        }
                    }

                    int argumentID = GetArgumentID(call.FindPropertyRelative(IDFieldName).intValue, j);
                    if (argName != null && (!isReference || isMethod)) {
                        menu.AddItem(new GUIContent($"{guiContent} / Arg {j}. {argName}"),
                        id.intValue == argumentID,
                        SetInt, new PropertyValue<int>(id, argumentID));
                    }
                }
            }
            return menu;
        }

        private GenericMenu BuildPopupList(Object target, Type reflectedType, SerializedProperty listener, Type desiredType, MethodCache currentMethod, string desiredMethodName) {
            // find the current event target...
            var methodName = listener.FindPropertyRelative(PropertyIsArgument(listener) ? StringArgumentFieldName : MethodNameFieldName);
            //var parameterName = listener.FindPropertyRelative(kParameterNamePath);

            var menu = new GenericMenu();

            GenericMenu.MenuFunction2 func;
            object data;

            if (PropertyIsArgument(listener)) {
                func = FuncPopupDrawer.ClearArgFunction;
                data = new FuncPopupDrawer.GetArgFunction(listener, null, null);
            }
            else {
                func = ClearEventFunction;
                data = new ExtendedEventFunction(listener, null, null);
            }
            menu.AddItem(new GUIContent(GetEmptyMethodName(GetTypeCache(reflectedType))), string.IsNullOrEmpty(methodName.stringValue), func, data);

            menu.AddSeparator("");

            //special case for components... we want all the game objects targets there!
            if (!target) {
                GeneratePopUpForType(menu, null, reflectedType, null, listener, desiredType, currentMethod, desiredMethodName);
                return menu;
            }
            if (reflectedType != null) {
                GeneratePopUpForType(menu, null, reflectedType, target != null ? reflectedType.Name : null, listener, desiredType, currentMethod, desiredMethodName);
                GeneratePopUpForType(menu, target, reflectedType, target != null ? reflectedType.Name : null, listener, desiredType, currentMethod, desiredMethodName);

            }

            if (!target) return menu;
            var targetGameObject = GetGameObject(target);
            if (targetGameObject) GeneratePopUpForType(menu, targetGameObject, typeof(GameObject), targetGameObject.GetType().Name, listener, desiredType, currentMethod, desiredMethodName);
            Object[] objects;
            List<string> duplicateNames;
            if (targetGameObject) {
                objects = targetGameObject.GetComponents<Component>();
                duplicateNames = objects.Select(c => c.GetType().FullName).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            }
            else {
                objects = new Object[] { target };
                duplicateNames = new List<string>();
            }
            foreach (Object o in objects) {
                HashSet<Type> castedTypes = new HashSet<Type>();

                Type type = o.GetType();
                foreach (MethodInfo caster in TypeCasterUtility.GetCasters(type)) {
                    Type castedType = caster.ReturnType;

                    if (castedType.IsAssignableFrom(type)) {
                        continue;
                    }

                    switch (GetTypeEnum(castedType)) {
                        default:
                            continue;
                        case TypeEnum.Object:
                        case TypeEnum.Generic:
                            break;
                    }
                    if (ArgumentType.IsAssignableFrom(castedType)) {
                        continue;
                    }
                    castedTypes.Add(castedType);
                }

                var name = duplicateNames.Contains(type.FullName) ? $"{type.Name} id: {o.GetInstanceID()}" : type.Name;
                GeneratePopUpForType(menu, o, type, (objects.Length + castedTypes.Count > (targetGameObject ? 0 : 1)) ? name : null, listener, desiredType, currentMethod, desiredMethodName);


                foreach (var castedType in castedTypes) {
                    GeneratePopUpForType(menu, o, castedType, $"{name} as {castedType.Name}", listener, desiredType, currentMethod, desiredMethodName);
                }


            }

            return menu;
        }

        private void CopyAll(object data) {
            SerializedProperty listenersArray = (SerializedProperty)data;

            CopiedEvents.Clear();
            CopiedTag = null;

            for (int i = 0; i < listenersArray.arraySize; i++) {
                var currentElement = listenersArray.GetArrayElementAtIndex(i);
                var map = new CallMap(currentElement);
                CopiedEvents.Add(map);
            }
        }

        private void CopyEventsTypeName() {
            EditorGUIUtility.systemCopyBuffer = GetSerializableTypeName(_EventsType);
        }

        private void CopyOne(object data) {
            SerializedProperty listenersArray = (SerializedProperty)data;

            CopiedEvents.Clear();

            SerializedProperty selectedProperty = listenersArray.GetArrayElementAtIndex(GetElementIndex(listenersArray));
            CopiedTag = null;

            CopiedEvents.Add(new CallMap(selectedProperty));
        }

        private void CopyTag(object data) {
            SerializedProperty listenersArray = (SerializedProperty)data;

            CopiedEvents.Clear();


            SerializedProperty selectedProperty = listenersArray.GetArrayElementAtIndex(GetElementIndex(listenersArray));
            CopiedTag = TagToString(selectedProperty);

            SerializedProperty selectedTagProperty = selectedProperty.FindPropertyRelative(TagFieldName);

            for (int i = 0; i < listenersArray.arraySize; i++) {
                var currentElement = listenersArray.GetArrayElementAtIndex(i);
                var map = new CallMap(currentElement);
                if (selectedTagProperty == null || SerializedProperty.DataEquals(selectedTagProperty, currentElement.FindPropertyRelative(TagFieldName))) CopiedEvents.Add(map);
            }
        }

        private void DelayModeField(Rect position, SerializedProperty property) {
            Enum delayModeValue;
            if (settings.parented) delayModeValue = (DelayMode)property.intValue;
            else delayModeValue = (DelayModeInstantOnly)property.intValue;

            EditorGUI.BeginProperty(position, GUIContent.none, property);
            EditorGUI.BeginChangeCheck();
            Enum enumNew = EditorGUI.EnumPopup(position, GUIContent.none, delayModeValue);

            if (EditorGUI.EndChangeCheck()) {
                property.intValue = (int)(object)enumNew;
            }
            EditorGUI.EndProperty();
        }

        private void DrawArgument(Rect position, SerializedProperty argument, GUIContent label, SerializedProperty callBase, ParameterCache parameterInfo, Type parameterType, bool allowDrawer, bool isEnumerable, bool drawReferenceFields, bool delayIDfield) {

            //var displayedArgTypeName = argument.FindPropertyRelative(ArgumentDisplayTypeFieldName);
            var previewFunc = argument.FindPropertyRelative(FuncPreviewFieldName);
            //Type displayedArgType = ExtendedEvent.GetType(displayedArgTypeName.stringValue) ?? parameterType;

            SerializedProperty objectArgument = argument.FindPropertyRelative(ObjectArgumentFieldName);
            SerializedProperty boolArgument = argument.FindPropertyRelative(BoolArgumentFieldName);
            SerializedProperty intArgument = argument.FindPropertyRelative(IntArgumentFieldName);
            SerializedProperty stringArgument = argument.FindPropertyRelative(StringArgumentFieldName);
            SerializedProperty floatArgument = argument.FindPropertyRelative(FloatArgumentFieldName);
            SerializedProperty vector3Argument = argument.FindPropertyRelative(Vector3ArgumentFieldName);
            SerializedProperty definitionArgument = argument.FindPropertyRelative(ArgumentDefitionFieldName);

            Argument.Definition argumentDefinition = (Argument.Definition)definitionArgument.intValue;
            ArgType argumentType = GetArgType(argumentDefinition);

            //memorize default bg color
            Color backgroundColor = GUI.backgroundColor;

            GUI.backgroundColor = Color.gray;
            //Rect horSepRect = new Rect(position.x - EditorGUIUtility.standardVerticalSpacing / 2, position.y, EditorGUIUtility.standardVerticalSpacing / 2, position.height);
            //GUI.Box(horSepRect, GUIContent.none);
            Rect verSepRect = new Rect(position.x, position.y - ElementSpacing / 2, position.width, EditorGUIUtility.standardVerticalSpacing / 2);
            GUI.Box(verSepRect, GUIContent.none);

            GUI.backgroundColor = backgroundColor;

            if (argumentType == ArgType.Method) {
                bool isCachedArgument = (argumentDefinition & Argument.Definition.CacheReturnValue) != 0;
                bool isNegated = (argumentDefinition & Argument.Definition.NegateBool) != 0;

                if (isCachedArgument) {
                    Rect rect = DivideRect(ref position, MarkerLabelWidth, position.width - MarkerLabelWidth, true);
                    EditorGUIUtility.DrawColorSwatch(rect, Color.green);
                    EditorGUI.LabelField(rect, new GUIContent("*", "Cached"));
                }
                if (isNegated) {
                    Rect rect = DivideRect(ref position, MarkerLabelWidth, position.width - MarkerLabelWidth, true);
                    EditorGUIUtility.DrawColorSwatch(rect, Color.red);
                    EditorGUI.LabelField(rect, new GUIContent("!", "Not"));
                }
            }

            EditorGUIUtility.labelWidth = GetLabelWidth(position);

            Type desiredType = parameterType;
            if (isEnumerable) desiredType = MakeGenericEnumerableType(desiredType);

            switch (argumentType) {
                case ArgType.Data:
                    DrawEndArgument(position, argument, label, parameterInfo, parameterType, allowDrawer);
                    break;
                case ArgType.Method:
                    DrawLabel(ref position, label);

                    MethodCache func = FindArgumentFunc(argument);

                    if (argument.FindPropertyRelative(FuncPreviewFieldName).boolValue) {
                        float rectSize = Mathf.Min(position.width / 5, 64);
                        Rect previewRect = DivideRect(ref position, position.width - rectSize, rectSize);
                        DrawPreviewer(previewRect, argument, func);
                    }

                    if (func != null) {

                        ParameterCache[] parameters = func.GetParameters(true);
                        TypeCache[] parameterTypes = func.GetParameterTypes(true);

                        Rect methodRect = DivideRectVertical(ref position, EditorGUIUtility.singleLineHeight);

                        Rect[] dataRects = new Rect[parameters.Length];
                        for (int i = 0; i < dataRects.Length; i++) {
                            dataRects[i] = position;
                            dataRects[i].height = GetArgumentFuncParameterHeight(argument, drawReferenceFields, GetFuncArgType((int)argumentDefinition, i));
                            if (i > 0) dataRects[i].y = dataRects[i - 1].yMax + ElementSpacing;
                        }

                        DrawMethodSelector(methodRect, argument, objectArgument.objectReferenceValue, desiredType, delayIDfield);

                        if (delayIDfield) return;

                        for (int i = 0; i < parameters.Length; i++) {
                            DrawFuncArgumentField(dataRects[i], argument, func.GetParameterLabels()[i], parameters[i], parameterTypes[i].type, func != null, i, drawReferenceFields);
                        }
                    }
                    else {
                        Rect targetRect = DivideRect(ref position, 1, 3, true);
                        DrawEndArgument(targetRect, argument, GUIContent.none, null, typeof(object), false);
                        DrawMethodSelector(position, argument, objectArgument.objectReferenceValue, desiredType, delayIDfield);
                    }

                    GUI.backgroundColor = backgroundColor;
                    break;
                case ArgType.Parent:
                    DrawIsParentField(position, label, true);
                    break;
                case ArgType.IDReference:
                    DrawIDField(position, label, listenersArray, intArgument, desiredType, drawReferenceFields, true, false);
                    break;
                case ArgType.TagReference:
                    DrawTagReferenceField(position, label, argument, parameterInfo, allowDrawer);
                    break;
                case ArgType.CustomEventArg:
                    DrawCustomEventArgsField(position, label);
                    break;
                default:
                    DrawUnknownArgumentTypeField(position, label);
                    break;
            }
            EditorGUIUtility.labelWidth = 0;
        }

        private void DrawCall(Rect rect, int index, bool isactive, bool drawReferenceField, Type desiredType, bool delayIDfield) {
            Color backgroundColorMem = GUI.backgroundColor;

            var pListener = listenersArray.GetArrayElementAtIndex(index);

            // find the current event target...
            var enabled = pListener.FindPropertyRelative(EnabledFieldName);
            var tag = pListener.FindPropertyRelative(TagFieldName);
            var arguments = pListener.FindPropertyRelative(ArgumentsFieldName);

            var delayMode = pListener.FindPropertyRelative(DelayModeFieldName);
            var delayValue = pListener.FindPropertyRelative(DelayValueFieldName);
            var delayID = pListener.FindPropertyRelative(DelayIDFieldName);

            Rect enabledRect, tagRect, delayModeRect, methodRect, dataRect;


            GetRects(rect, out enabledRect, out tagRect, out delayModeRect, out methodRect, out dataRect);

            if (!drawReferenceField) methodRect.xMin = enabledRect.xMin;

            Color baseColor;
            if (!enabled.boolValue) baseColor = ColorOff;
            else baseColor = GUI.backgroundColor;

            int callid = pListener.FindPropertyRelative(IDFieldName).intValue;
            if (callid == 0 || RepeatingIDs.Contains(callid)) baseColor = ColorError;

            GUI.backgroundColor = baseColor;
            if (drawReferenceField && tag != null) {
                EditorGUI.BeginProperty(tagRect, GUIContent.none, tag);

                if (tagDrawer == null) {
                    EditorGUI.PropertyField(tagRect, tag, GUIContent.none);
                }
                else {
                    tagDrawer.OnGUI(tagRect, tag, GUIContent.none);
                }
                EditorGUI.EndProperty();
            }
            else delayModeRect.xMin = tagRect.xMin;

            GUI.backgroundColor = baseColor;
            if (drawReferenceField) {
                EditorGUI.BeginProperty(enabledRect, GUIContent.none, enabled);
                enabled.boolValue = EditorGUI.Toggle(enabledRect, enabled.boolValue);
                EditorGUI.EndProperty();

                if (!settings.parented && delayMode.intValue == 0) {
                    methodRect.xMin = delayModeRect.xMin;
                }
                else {
                    DelayMode delayModeValue = (DelayMode)delayMode.intValue;
                    if (delayModeValue > DelayMode.NoDelay) {
                        if (!parentProperty.objectReferenceValue) GUI.backgroundColor = ColorError;
                        else if (enabled.boolValue && delayModeValue == DelayMode.Pause) GUI.backgroundColor = ColorPause;
                    }


                    if (delayModeValue > 0) {
                        Rect valueRect = DivideRect(ref delayModeRect, 40, 100);
                        Rect selectorRect = DivideRect(ref valueRect, valueRect.width - 20, 20);

                        if (delayID.intValue == 0) {
                            EditorGUI.BeginProperty(valueRect, GUIContent.none, delayValue);
                            delayValue.floatValue = EditorGUI.FloatField(valueRect, delayValue.floatValue);
                            delayValue.floatValue = Mathf.Clamp(delayValue.floatValue, 0, float.MaxValue);
                            EditorGUI.EndProperty();
                        }
                        else {
                            DrawIDField(valueRect, GUIContent.none, listenersArray, delayID, typeof(float), true, false, true);
                        }


                        EditorGUI.BeginProperty(selectorRect, GUIContent.none, delayID);

                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Value"), delayID.intValue == 0, SetInt, new PropertyValue<int>(delayID, 0));
                        menu.AddSeparator("");
                        if (GUI.Button(selectorRect, new GUIContent(">"), EditorStyles.miniButton)) {
                            BuildDataList(menu, listenersArray, delayID, typeof(float)).DropDown(selectorRect);
                        }
                        EditorGUI.EndProperty();
                    }
                    DelayModeField(delayModeRect, delayMode);
                }
            }

            baseColor = Color.white;


            if (arguments.serializedObject.isEditingMultipleObjects) return;

            GUI.backgroundColor = baseColor;
            //only allow argument if we have a valid target / method

            MethodCache method = null;

            method = FindMethod(pListener);

            Type[] parameterTypes = GetArgumentTypes(pListener, method) ?? new Type[] { typeof(object) };
            Object target = null;
            if (arguments.arraySize > 0) {
                var arg0 = arguments.GetArrayElementAtIndex(0);
                target = arg0.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue;
            }

            DrawMethodSelector(methodRect, pListener, target, desiredType, delayIDfield);

            if (delayIDfield) return;

            if (arguments.serializedObject.isEditingMultipleObjects) return;

            Rect[] dataRects = new Rect[arguments.arraySize];
            for (int i = 0; i < dataRects.Length; i++) {
                dataRects[i] = dataRect;
                dataRects[i].height = GetArgumentHeight(arguments.GetArrayElementAtIndex(i), drawReferenceField);
                if (i > 0) dataRects[i].y = dataRects[i - 1].yMax + ElementSpacing;
            }

            int foreachIndex = GetForeachIndex(pListener, arguments);

            int count = Mathf.Min(arguments.arraySize, parameterTypes.Length, method?.GetParameterLabels().Length ?? arguments.arraySize);
            for (int i = 0; i < arguments.arraySize; i++) {
                if (i >= count) {
                    EditorGUI.LabelField(dataRects[i], new GUIContent("Amount of arguments is higher than amount of method parameters"));
                    continue;
                }

                var argument = arguments.GetArrayElementAtIndex(i);
                bool isEnumerable = foreachIndex == i;

                Event evt = Event.current;
                if (evt.type == EventType.MouseDown && evt.button == 1 && dataRects[i].Contains(evt.mousePosition)) DrawArgumentContext(pListener, i, parameterTypes[i], isEnumerable);

                if (isEnumerable) {
                    Rect foreachMarkerRect = DivideRect(ref dataRects[i], MarkerLabelWidth, dataRects[i].width - MarkerLabelWidth, true);
                    EditorGUIUtility.DrawColorSwatch(foreachMarkerRect, Color.yellow);
                    EditorGUI.LabelField(foreachMarkerRect, new GUIContent("f", "foreach"));
                }


                Color backgroundColor = GUI.backgroundColor;

                GUIContent parameterLabel;
                if (method != null) parameterLabel = method.GetParameterLabels()[i];
                else parameterLabel = GetTypeCache(parameterTypes[i]).Label;

                if (i < parameterTypes.Length) DrawArgument(dataRects[i], argument, parameterLabel, pListener, method?.GetParameters(true)[i], parameterTypes[i], method != null, isEnumerable, drawReferenceField, delayIDfield);
            }

            GUI.backgroundColor = backgroundColorMem;
        }

        private void DrawCustomEventArgsField(Rect position, GUIContent label) {
            EditorGUIUtility.labelWidth = GetLabelWidth(position);
            DrawLabel(ref position, label);
            EditorGUIUtility.DrawColorSwatch(position, new Color(0.7f, 0.7f, 1));
            EditorGUI.LabelField(position, CustomEventArg);
        }

        private void DrawEndArgument(Rect position, SerializedProperty property, GUIContent label, ParameterCache parameterInfo, Type parameterType, bool allowDrawer) {
            EditorGUIUtility.labelWidth = GetLabelWidth(position);
            EditorGUI.BeginProperty(position, GUIContent.none, property);

            var parameterTypeEnum = GetTypeEnum(parameterType);

            Color backgroundColor = GUI.backgroundColor;

            object customDrawer = null;
            if (allowDrawer) {
                if (parameterInfo != null) customDrawer = parameterInfo?.Drawer;
                if (customDrawer == null) customDrawer = GetTypeCache(parameterType)?.Drawer;
            }

            //drawing argument
            if (parameterTypeEnum == TypeEnum.Unknown) {
                LabelField(position, label, new GUIContent("Unknown parameter", $"Unknown parameter type {parameterType}"));
            }
            else if (customDrawer is PropertyDrawer customPropertyDrawer) {
                try {
                    switch (parameterTypeEnum) {
                        case TypeEnum.Integer:
                        case TypeEnum.Enum:
                        case TypeEnum.Character:
                        case TypeEnum.LayerMask:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(IntArgumentFieldName), label);
                            break;
                        case TypeEnum.Boolean:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(BoolArgumentFieldName), label);
                            break;
                        case TypeEnum.Float:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(FloatArgumentFieldName), label);
                            break;
                        case TypeEnum.String:
                        case TypeEnum.Type:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(StringArgumentFieldName), label);
                            break;
                        case TypeEnum.Object:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(ObjectArgumentFieldName), label);
                            break;
                        case TypeEnum.Vector2:
                        case TypeEnum.Vector3:
                        case TypeEnum.Quaternion:
                            customPropertyDrawer.OnGUI(position, property.FindPropertyRelative(Vector3ArgumentFieldName), label);
                            break;
                        case TypeEnum.Color:
                        case TypeEnum.Vector4:
                        case TypeEnum.Rect:
                        case TypeEnum.Generic:
                            LabelField(position, label, new GUIContent("Invalid drawer", $"Drawer of type {customPropertyDrawer.GetType()} is not supported for parameter of type {parameterType}. Derive your drawer from {nameof(ArgumentDrawer)}"));
                            break;
                    }
                }
                catch (Exception e) {
                    LabelField(position, label, new GUIContent(e.GetType().Name, e.ToString()));
                    Debug.LogError(e);
                }

            }
            else if (customDrawer is ArgumentDrawer customArgumentDrawer) {
                switch (parameterTypeEnum) {
                    default:
                        customArgumentDrawer.OnGUI(position, property, label);
                        break;
                    case TypeEnum.Generic:
                        if (parameterType.IsCastableFrom(ArgumentType)) customArgumentDrawer.OnGUI(position, property, label);
                        else {
                            LabelField(position, label, new GUIContent("Cast missing", $"{ArgumentType} can't be cast to {parameterType}. Cast operator or custom method must be defined"));
                        }
                        break;
                }
            }
            //use default drawer
            else {
                ArgumentField(position, property, label, parameterType, parameterInfo != null);
            }
            GUI.backgroundColor = backgroundColor;

            EditorGUIUtility.labelWidth = 0;
            EditorGUI.EndProperty();
        }

        private void DrawEventListener(Rect rect, int index, bool isactive, bool isfocused) {
            DrawCall(rect, index, isactive, true, null, false);
        }

        private void DrawFuncArgumentField(Rect position, SerializedProperty property, GUIContent label, ParameterCache parameterInfo, Type parameterType, bool allowDrawer, int argIndex, bool drawReferenceFields) {

            SerializedProperty methodDefinition = property.FindPropertyRelative(ArgumentDefitionFieldName);
            Argument.ArgType argType = GetFuncArgType(methodDefinition.intValue, argIndex);

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && Event.current.button == 1 && position.Contains(evt.mousePosition)) {
                GenericMenu context = new GenericMenu();

                int mask = (1 << (argIndex + ArgTypeFlag1)) | (1 << (argIndex + ArgTypeFlag2)) | (1 << (argIndex + ArgTypeFlag3));

                context.AddItem(new GUIContent("Data"), argType == ArgType.Data, SetFuncArgumentType, new PropertyValue<Vector2Int>(methodDefinition, new Vector2Int(0, mask)));
                context.AddSeparator("");
                context.AddItem(new GUIContent("ID Reference"), argType == ArgType.IDReference, SetFuncArgumentType, new PropertyValue<Vector2Int>(methodDefinition, new Vector2Int(1 << (argIndex + ArgTypeFlag1), mask)));
                context.AddItem(new GUIContent("Tag Reference"), argType == ArgType.TagReference, SetFuncArgumentType, new PropertyValue<Vector2Int>(methodDefinition, new Vector2Int((1 << (argIndex + ArgTypeFlag1)) | (1 << (argIndex + ArgTypeFlag2)), mask)));
                if (settings.parented) {
                    context.AddSeparator("");
                    context.AddItem(new GUIContent("Parent"), argType == ArgType.Parent, SetFuncArgumentType, new PropertyValue<Vector2Int>(methodDefinition, new Vector2Int(1 << (argIndex + ArgTypeFlag2), mask)));
                }
                context.AddSeparator("");
                context.AddItem(new GUIContent("Custom Event Args"), argType == ArgType.CustomEventArg, SetFuncArgumentType, new PropertyValue<Vector2Int>(methodDefinition, new Vector2Int(1 << (argIndex + ArgTypeFlag3), mask)));
                context.ShowAsContext();
            }

            switch (argType) {
                case ArgType.Data:
                    TypeEnum typeEnum = GetTypeEnum(parameterType);

                    if (typeEnum == TypeEnum.String) {
                        EditorGUI.BeginDisabledGroup(true);
                        StringField(position, GUIContent.none, property.FindPropertyRelative(StringArgumentFieldName));
                        EditorGUI.EndDisabledGroup();
                    }
                    else DrawEndArgument(position, property, label, parameterInfo, parameterType, allowDrawer);
                    break;
                case ArgType.Parent:
                    DrawIsParentField(position, label, true);
                    break;
                case ArgType.IDReference:
                    DrawIDField(position, label, listenersArray, property.FindPropertyRelative(IntArgumentFieldName), parameterType, drawReferenceFields, true, false);
                    break;
                case ArgType.TagReference:
                    DrawTagReferenceField(position, label, property, parameterInfo, allowDrawer);
                    break;
                default:
                    DrawUnknownArgumentTypeField(position, label);
                    break;
                case ArgType.CustomEventArg:
                    DrawCustomEventArgsField(position, label);
                    break;
            }
        }

        private void DrawIDField(Rect position, GUIContent label, SerializedProperty calls, SerializedProperty id, Type desiredType, bool drawReferenceFields, bool drawSelector, bool delayIDfield) {
            GUI.Box(position, GUIContent.none);

            string buttonContent = null;
            string buttonTooltip = null;

            SerializedProperty referencedCall = null;
            SerializedProperty referencedArgument = null;
            Vector2Int dataAddress = GetDataAddress(calls, id.intValue);
            if (dataAddress.x >= 0) referencedCall = calls.GetArrayElementAtIndex(dataAddress.x);
            if (dataAddress.y >= 0) referencedArgument = referencedCall.FindPropertyRelative(ArgumentsFieldName).GetArrayElementAtIndex(dataAddress.y);

            Color backgroundColor = GUI.backgroundColor;

            if (referencedCall == null) {
                DrawLabel(ref position, label);
                GUI.backgroundColor = ColorError;
                buttonContent = $"<{id.intValue}>";
            }
            else {
                Type returnType;
                if (referencedArgument == null) {
                    MethodCache callMethod = FindMethod(referencedCall);
                    TypeCache callType = GetTypeCache(ExtendedEvent.GetType(referencedCall.FindPropertyRelative(MethodNameFieldName).stringValue));
                    returnType = callMethod?.ReturnType.type ?? callType?.type;
                    if (returnType != null) {
                        MethodInfo referencedMethod = ExtendedEvent.FindMethod(referencedCall.FindPropertyRelative(MethodNameFieldName).stringValue);
                        Type referencedType = ExtendedEvent.GetType(referencedCall.FindPropertyRelative(MethodNameFieldName).stringValue);

                        buttonContent = referencedMethod?.Name ?? referencedType?.Name;
                        buttonTooltip = $"{TagToString(referencedCall)} / {buttonContent}";
                    }
                }
                else {
                    var propertyFlag = referencedArgument.FindPropertyRelative(ArgumentDefitionFieldName);
                    bool isMethod = ((Argument.Definition)propertyFlag.intValue & Argument.Definition.IsMethod) != 0;
                    if (isMethod) {
                        returnType = FindArgumentFunc(referencedArgument)?.ReturnType.type;
                        if (returnType != null) {
                            buttonContent = GetDisplayFuncName(referencedArgument.FindPropertyRelative(StringArgumentFieldName).stringValue);
                        }
                    }
                    else {

                        if (FindMethod(referencedCall) != null) returnType = FindMethod(referencedCall)?.GetParameterTypes(true)[dataAddress.y].type;
                        else returnType = null;
                        if (returnType != null) {
                            buttonContent = $"{GetDisplayTypeName(returnType)} argument";
                        }
                    }
                    if (returnType != null) {
                        buttonTooltip = $"{TagToString(referencedCall)} / Arg {dataAddress.y} / {buttonContent}";
                    }
                }


                if (returnType == null) {
                    GUI.backgroundColor = ColorError;
                    buttonContent = $"<Missing {buttonContent}>";
                    buttonTooltip = buttonContent;
                }

            }
            Rect buttonRect = position;

            if (drawReferenceFields && referencedCall != null) {
                buttonRect = DivideRect(ref position, position.width - GetLabelWidth(position), GetLabelWidth(position));
                buttonRect.height = EditorGUIUtility.singleLineHeight;

                if (referencedArgument == null) {
                    DrawLabel(ref position, label);
                    DrawCall(position, dataAddress.x, false, false, desiredType, delayIDfield);
                }
                else {
                    ParameterCache parameter;
                    Type type;
                    MethodCache callMethod = FindMethod(referencedCall);
                    if (callMethod != null) {
                        parameter = callMethod.GetParameters(true)[dataAddress.y];
                        type = callMethod.GetParameterTypes(true)[dataAddress.y].type;
                    }
                    else {
                        parameter = null;
                        type = ExtendedEvent.GetType(referencedCall.FindPropertyRelative(MethodNameFieldName).stringValue);
                    }
                    if (type != null) DrawArgument(position, referencedArgument, label, referencedCall, parameter, type, true, GetForeachIndex(referencedCall, referencedCall.FindPropertyRelative(ArgumentsFieldName)) == dataAddress.y, false, delayIDfield);
                    else EditorGUI.LabelField(position, "Method missing");
                }

            }

            if (referencedCall == null || drawSelector) {
                GUIContent buttonGUIcontent = new GUIContent(buttonContent, buttonTooltip);
                GenericMenu menu = new GenericMenu();
                if (GUI.Button(buttonRect, buttonGUIcontent, ButtonLeft)) {
                    menu.AddSeparator("");
                    BuildDataList(menu, calls, id, desiredType).DropDown(buttonRect);
                }
            }

            GUI.backgroundColor = backgroundColor;

        }

        private void DrawIsParentField(Rect position, GUIContent label, bool disabled) {
            EditorGUIUtility.labelWidth = GetLabelWidth(position);
            DrawLabel(ref position, label);
            Color backgroundColor = GUI.backgroundColor;
            if (!parentProperty.objectReferenceValue) {
                GUI.backgroundColor = ColorError;
            }
            if (disabled) EditorGUI.BeginDisabledGroup(true);
            EditorGUI.ObjectField(position, parentProperty, GUIContent.none);
            if (disabled) EditorGUI.EndDisabledGroup();
            GUI.backgroundColor = backgroundColor;
        }

        private void DrawLabelColorSwatch(Rect position, Color color) {
            Rect labelRect = position;
            labelRect.xMax = labelRect.xMin + EditorGUIUtility.labelWidth;
            EditorGUIUtility.DrawColorSwatch(labelRect, color);
        }

        private void DrawMethodSelector(Rect propertyRect, SerializedProperty property, Object target, Type desiredType, bool simplified) {

            if (EditorGUI.showMixedValue) {
                GUI.Button(propertyRect, new GUIContent("Editing multiple methods is not supported"), EditorStyles.miniButton);
                return;
            }

            SerializedProperty methodSignature = property.FindPropertyRelative(PropertyIsArgument(property) ? StringArgumentFieldName : MethodNameFieldName);

            EditorGUI.BeginProperty(propertyRect, GUIContent.none, methodSignature);

            MethodCache method = FindMethod(ExtendedEvent.FindMethod(methodSignature.stringValue));

            Type reflectedType;
            string reflectedTypeName;

            Color methodColor;

            Color backgroundColor = GUI.backgroundColor;
            string methodName;
            GUIContent methodButtonContent;
            if (method != null) {
                reflectedType = method.ReflectedType.type;
                reflectedTypeName = method.ReflectedType.Label.text;

                var colorizer = method.GetCustomAttribute<ColorizeMethodAttribute>();

                if (colorizer != null) methodColor = colorizer.color;
                else if (method.ReturnType.type == typeof(IEnumerator)) {
                    if (!parentProperty.objectReferenceValue) methodColor = ColorError;
                    else methodColor = ColorCoroutine;
                }
                else methodColor = backgroundColor;

                methodName = method.Name;
                methodButtonContent = method.Label;

                if (method.containsReferenceParameters) {
                    methodButtonContent = new GUIContent($"INVALID {methodButtonContent.text}", $"Method {method.DisplayName} contains ref or out parameters");
                    methodColor = ColorError;
                }
                else if(method.GetCustomAttribute<ObsoleteAttribute>() != null) {
                    methodButtonContent = new GUIContent($"[OBSOLETE] {methodButtonContent.text}", $"[OBSOLETE] {methodButtonContent.tooltip}");
                    methodColor = ColorError;
                }
                else if (desiredType != null && !desiredType.IsCastableFrom(method.ReturnType.type, true)) {
                    methodButtonContent = new GUIContent(method.Label.text, $"Return type {method.ReturnType.DisplayName} of method {method.Name} can't be cast to paramter type {GetDisplayTypeName(desiredType)}");
                    methodColor = ColorError;
                }

            }
            else {
                string[] data = methodSignature.stringValue.Split(';');

                reflectedType = ExtendedEvent.GetType(data[0]);
                reflectedTypeName = data[0];

                switch (data.Length) {
                    case 1:
                        methodName = GetEmptyMethodName(GetTypeCache(reflectedType));
                        methodColor = backgroundColor;
                        break;
                    default:
                        methodName = $"<{data[1]}>";
                        methodColor = ColorError;
                        break;
                }

                if (reflectedType != null) {
                    if (data.Length != 1) methodButtonContent = new GUIContent(methodName, $"{reflectedType.Name} does not define method {methodName} with specified parameters");
                    else methodButtonContent = new GUIContent(methodName, methodName);
                }
                else {
                    if (data.Length >= 2) methodButtonContent = new GUIContent(methodName, $"Type {data[1]} does not exist");
                    else methodButtonContent = new GUIContent(methodName, $"Method type was not specified");
                    reflectedType = typeof(object);
                }

            }

            bool methodIsExposed = property.propertyPath == ExposedMethodSignaturePath;

            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 1 && propertyRect.Contains(evt.mousePosition)) {

                GenericMenu context = new GenericMenu();

                context.AddItem(new GUIContent("Expose method signature"), methodIsExposed, ExposeMethodSignature, property);

                context.ShowAsContext();
            }

            if (methodIsExposed) {
                EditorGUI.BeginChangeCheck();

                string newMethodName = EditorGUI.TextField(propertyRect, GUIContent.none, methodSignature.stringValue);

                if (EditorGUI.EndChangeCheck()) {
                    MethodInfo newMethod = ExtendedEvent.FindMethod(newMethodName);
                    if (newMethod != null) {
                        if (!PropertyIsArgument(property)) new ExtendedEventFunction(property, target, newMethod).Assign();
                        else new FuncPopupDrawer.GetArgFunction(property, target, newMethod).Assign();

                        ExposedMethodSignaturePath = null;
                    }
                }
            }
            else {
                if (!simplified) {
                    Rect reflectedTypeRect = DivideRect(ref propertyRect, 1, 4, true);
                    DrawTypeSelector(reflectedTypeRect, methodSignature, reflectedType, reflectedTypeName, SetMethodReflectedTypeName);
                }

                GUI.backgroundColor = methodColor;

                if (!simplified) {
                    Rect selectFunctionRect = DivideRect(ref propertyRect, propertyRect.width - 20, 20);
                    if (GUI.Button(selectFunctionRect, new GUIContent(">"), EditorStyles.miniButton)) {
                        BuildPopupList(target, reflectedType, property, desiredType, method, methodName).DropDown(propertyRect);
                    }
                }

                if (GUI.Button(propertyRect, methodButtonContent, EditorStyles.popup)) {
                    BuildPopupList(target, reflectedType, property, desiredType, method, null).DropDown(propertyRect);
                }

                GUI.backgroundColor = backgroundColor;
            }

            EditorGUI.EndProperty();
        }

        private void DrawTagReferenceField(Rect position, GUIContent label, SerializedProperty property, ParameterCache parameterInfo, bool allowDrawer) {
            GUI.Box(position, GUIContent.none);
            DrawEndArgument(position, property, label, parameterInfo, _TagType, allowDrawer);
        }

        private void DrawUnknownArgumentTypeField(Rect position, GUIContent label) {
            EditorGUIUtility.DrawColorSwatch(position, ColorError);
            EditorGUI.LabelField(position, "Unknown argument type");
        }

        private void EndDragChild(ReorderableList list) {
            lastSelectedIndex = list.index;
        }

        private void GeneratePopUpForType(GenericMenu menu, Object target, Type targetType, string targetName, SerializedProperty listener, Type desiredType, MethodCache currentMethod, string desiredMethodName) {
            // find the methods on the behaviour that match the signature
            List<ValidMethodMap> methods = new List<ValidMethodMap>();

            var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            if (!ShowNonPublicMethods) bindingFlags &= ~BindingFlags.NonPublic;
            List<MethodInfo> instanceMethods = new List<MethodInfo>();
            instanceMethods = targetType.GetMethods(bindingFlags).ToList();
            instanceMethods.RemoveAll(m => m.GetParameters().Length > MaxArgs - 1);

            //componentMethods.RemoveAll(m => CheckHidden(m, staticOnly));

            List<MethodInfo> staticMethods = new List<MethodInfo>();
            Type baseType = targetType;
            do {
                staticMethods.AddRange(baseType.GetMethods(BindingFlags.Public | BindingFlags.Static));
                baseType = baseType.BaseType;
            }
            while (baseType != null);

            staticMethods.RemoveAll(m => m.GetParameters().Length > MaxArgs);
            if (target) {
                staticMethods.RemoveAll(m => m.GetParameters().Length == 0);
                staticMethods.RemoveAll(m => !m.ContainsGenericParameters && !m.GetParameters()[0].ParameterType.IsCastableFrom(targetType, true));
            }

            List<MethodInfo> allMethods = new List<MethodInfo>(instanceMethods.Union(staticMethods));
            allMethods.RemoveAll(m => m.GetCustomAttribute<ObsoleteAttribute>() != null);
            allMethods.RemoveAll(m => m.GetCustomAttribute<HiddenAttribute>() != null);
            allMethods.RemoveAll(m => m.GetParameters().FirstOrDefault(p => p.ParameterType.IsByRef || p.ParameterType.IsPointer) != null);

            if (PropertyIsArgument(listener)) {
                allMethods.RemoveAll(m => m.ReturnType == typeof(void));
            }

            if (desiredMethodName != null) {
                allMethods.RemoveAll(m => !m.Name.StartsWith(desiredMethodName));
            }

            GetValidMethods(ref methods, allMethods, target, desiredType);

            //check if there is a suggested method to replace this Obsolete method
            ObsoleteAttribute obsoleteAttribute = currentMethod?.GetCustomAttribute<ObsoleteAttribute>();
            if (obsoleteAttribute != null && !string.IsNullOrEmpty(obsoleteAttribute.Message)) {
                MethodCache suggestedMethod = FindMethod(ExtendedEvent.FindMethod(obsoleteAttribute.Message));
                if (suggestedMethod != null) {
                    var vmm = new ValidMethodMap();
                    vmm.target = target;
                    vmm.methodInfo = suggestedMethod.method;
                    vmm.methodName = $"Suggested: {GetDisplayMethodName(vmm.methodInfo)}";

                    AddFunctionForScript(menu, listener, vmm, null, false, suggestedMethod);
                }
            }

            if (methods.Count > 0) {

                //menu.AddItem(new GUIContent(targetName + "/ "), false, null);
                AddMethodsToMenu(menu, listener, methods, targetName, true, currentMethod);
            }
        }

        private float GetArgumentFuncParameterHeight(SerializedProperty argument, bool drawReferenceFields, ArgType defition) {
            float height;
            switch (defition) {
                default:
                    height = EditorGUIUtility.singleLineHeight;
                    break;
                case ArgType.IDReference:
                    height = GetReferenceHeight(argument, drawReferenceFields);
                    break;
            }
            return height;
        }

        private float GetArgumentHeight(SerializedProperty argument, bool drawReferenceFields) {
            float height;

            SerializedProperty definitionArgument = argument.FindPropertyRelative(ArgumentDefitionFieldName);
            Argument.Definition argumentDefinition = (Argument.Definition)definitionArgument.intValue;
            ArgType argumentType = GetArgType(argumentDefinition);
            switch (argumentType) {
                default:
                    height = EditorGUIUtility.singleLineHeight;
                    break;
                case ArgType.Method:
                    MethodCache argFunc = FindArgumentFunc(argument);
                    if (argFunc == null) {
                        height = EditorGUIUtility.singleLineHeight;
                    }
                    else {
                        height = EditorGUIUtility.singleLineHeight;
                        for (int i = 0; i < argFunc.GetParameters(true).Length; i++) {
                            height += GetArgumentFuncParameterHeight(argument, drawReferenceFields, GetFuncArgType((int)argumentDefinition, i));
                            if (i > 0) height += ElementSpacing;
                        }
                    }
                    break;
                case ArgType.IDReference:
                    height = GetReferenceHeight(argument, drawReferenceFields);
                    break;
            }

            return height;
        }

        private float GetCallHeight(SerializedProperty call, bool drawReferenceFields) {
            float height = EditorGUIUtility.singleLineHeight;
            SerializedProperty arguments = call.FindPropertyRelative(ArgumentsFieldName);
            for (int i = 0; i < arguments.arraySize; i++) {
                SerializedProperty argument = arguments.GetArrayElementAtIndex(i);
                height += GetArgumentHeight(argument, drawReferenceFields);
                if (i > 0) height += ElementSpacing;
            }
            return height;
        }

        private float GetElementHeight(int index) {
            return GetCallHeight(listenersArray.GetArrayElementAtIndex(index), true) + ElementSpacing;
        }

        //private static bool FixMethodNames = false;
        private void GetEventsUnderlyingTypes() {
            if (_TagType != null) return;
            _EventsType = fieldInfo.FieldType;
            Type t = _EventsType;
            FieldInfo callsField = null;
            while (t != null) {
                callsField = t.GetField(CallsFieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                if (callsField != null) break;
                t = t.BaseType;
            }

            Type callType = callsField.FieldType.GetElementType();
            _TagType = callType.BaseType.GetField(TagFieldName, BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
            _ArgumentType = callType.GetField(ArgumentsFieldName, BindingFlags.Instance | BindingFlags.NonPublic).FieldType.GetElementType();

            /*
            Type tagDrawerType = (attribute as ExtendedEventAttribute)?.drawerType;

            if (tagDrawerType != null) {
                if (tagDrawerType == typeof(PropertyDrawer))
                    _tagDrawer = GetTypeDrawer(_tagType) as PropertyDrawer;
                else if (typeof(PropertyDrawer).IsAssignableFrom(tagDrawerType))
                    _tagDrawer = MakePropertyDrawer(null, null, _tagType, null, tagDrawerType) as PropertyDrawer;
                else Debug.LogError($"{tagDrawerType} is not a {nameof(PropertyDrawer)}");
            }
            */
            if (!settings.useDefaultDrawer) {
                ArgumentAttribute attribute = fieldInfo.GetCustomAttribute<ArgumentAttribute>();
                if (attribute != null) _tagDrawer = MakeTagDrawer(_TagType, attribute);
                else _tagDrawer = GetTypeCache(_TagType)?.Drawer as PropertyDrawer;
            }
        }

        private float GetReferenceHeight(SerializedProperty argument, bool drawReferenceFields) {
            float height;
            if (!drawReferenceFields) {
                height = EditorGUIUtility.singleLineHeight;
            }
            else {
                SerializedProperty intArgument = argument.FindPropertyRelative(IntArgumentFieldName);
                Vector2Int dataAddress = GetDataAddress(listenersArray, intArgument.intValue);
                if (dataAddress.x >= 0) {
                    SerializedProperty referencedCall = listenersArray.GetArrayElementAtIndex(dataAddress.x);

                    if (dataAddress.y == -1) {
                        height = GetCallHeight(referencedCall, false);
                    }
                    else {
                        SerializedProperty referencedArgument = referencedCall.FindPropertyRelative(ArgumentsFieldName).GetArrayElementAtIndex(dataAddress.y);
                        height = GetArgumentHeight(referencedArgument, false);

                    }
                }
                else {
                    height = EditorGUIUtility.singleLineHeight;
                }
            }
            return height;
        }

        private State GetState(SerializedProperty prop) {
            State state;
            string key = prop.propertyPath;
            states.TryGetValue(key, out state);
            // ensure the cached SerializedProperty is synchronized (case 974069)
            if (state == null || state.reorderableList.serializedProperty.serializedObject != prop.serializedObject) {
                if (state == null) state = new State();

                SerializedProperty listenersArray = prop.FindPropertyRelative(CallsFieldName);
                state.reorderableList = new ReorderableList(prop.serializedObject, listenersArray, true, true, true, true);
                state.reorderableList.drawHeaderCallback = DrawEventHeader;
                state.reorderableList.drawElementCallback = DrawEventListener;
                state.reorderableList.onSelectCallback = SelectEventListener;
                state.reorderableList.onReorderCallback = EndDragChild;
                state.reorderableList.onAddCallback = AddEventListener;
                state.reorderableList.onRemoveCallback = RemoveButton;
                state.reorderableList.elementHeightCallback = GetElementHeight;
                // Two standard lines with standard spacing between and extra spacing below to better separate items visually.
                states[key] = state;
            }
            return state;
        }

        private int GetTagCount(SerializedProperty property) {
            SerializedProperty selectedTagProperty = property.GetArrayElementAtIndex(GetElementIndex(property)).FindPropertyRelative(TagFieldName);
            int count = 0;
            for (int i = 0; i < property.arraySize; i++) {
                var currentElement = property.GetArrayElementAtIndex(i);
                if (selectedTagProperty == null || SerializedProperty.DataEquals(selectedTagProperty, currentElement.FindPropertyRelative(TagFieldName))) count++;
            }
            return count;
        }

        private string GetTagName(SerializedProperty property) {
            SerializedProperty selectedElement = property.GetArrayElementAtIndex(GetElementIndex(property));
            return TagToString(selectedElement);
        }

        private void ObjectField(Rect position, SerializedProperty property) {
            Object objectRef = property.objectReferenceValue;
            if (!objectRef || !(objectRef is Component)) {
                EditorGUI.ObjectField(position, property, GUIContent.none);
            }
            else {
                EditorGUI.BeginChangeCheck();
                Object newObject = EditorGUI.ObjectField(position, objectRef, typeof(Object), true);
                if (EditorGUI.EndChangeCheck()) {
                    if (newObject is GameObject go) {
                        Component[] components = go.GetComponents<Component>();
                        //perfect match
                        foreach (var component in components) {
                            if (component.GetType() == objectRef.GetType()) {
                                property.objectReferenceValue = component;
                                return;
                            }
                        }
                        //match
                        foreach (var component in components) {
                            if (objectRef.GetType().IsAssignableFrom(component.GetType())) {
                                property.objectReferenceValue = component;
                                return;
                            }
                        }
                        //last resort
                        foreach (var component in components) {
                            if (component.GetType().IsAssignableFrom(objectRef.GetType())) {
                                property.objectReferenceValue = component;
                                return;
                            }
                        }
                    }
                    property.objectReferenceValue = newObject;
                }
            }
        }

        private void RemoveButton(ReorderableList list) {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            lastSelectedIndex = list.index;
        }

        private State RestoreState(SerializedProperty property) {
            /*
            if (listenersArray != null) {
                int elementIndex = GetElementIndex(listenersArray);
                int insertIndex = GetInsertIndex(listenersArray);
                int originalSize = listenersArray.arraySize;
            }
            */
            State state = GetState(property);

            listenersArray = state.reorderableList.serializedProperty;
            reorderableList = state.reorderableList;
            lastSelectedIndex = state.lastSelectedIndex;
            reorderableList.index = lastSelectedIndex;

            parentProperty = property.FindPropertyRelative(ParentFieldName);

            return state;
        }

        private void SelectEventListener(ReorderableList list) {
            lastSelectedIndex = list.index;
        }

        private string TagToString(SerializedProperty callProperty) {
            object tag = GetValue(callProperty.FindPropertyRelative(TagFieldName));
            if (tag == null) return "null";
            if (tag is int && TagType.IsEnum) tag = Enum.ToObject(TagType, tag);
            ITagToString converter = GetTypeCache(tag.GetType())?.Drawer as ITagToString;
            return converter?.TagToString(tag) ?? tag.ToString() ?? "null";
        }

        private void ToggleEnlistArgumentsFlag(object data) {
            SerializedProperty property = (SerializedProperty)data;
            property.boolValue = !property.boolValue;
            property.serializedObject.ApplyModifiedProperties();
        }

        /*
        private void ToggleFixNameWindow() {
            FixNameWindowActive = !FixNameWindowActive;
        }
        */
        struct ValidMethodMap {
            #region Fields

            public Object target;

            public MethodInfo methodInfo;

            public string methodName;

            #endregion
        }

        public struct ExtendedEventFunction {
            #region Fields

            private readonly SerializedProperty listener;

            private readonly Object target;

            private readonly MethodInfo method;

            #endregion

            public ExtendedEventFunction(SerializedProperty listener, Object target, MethodInfo method) {
                this.listener = listener;
                this.target = target;
                this.method = method;
            }

            public void Assign() {
                // find the current event target...
                var methodName = listener.FindPropertyRelative(MethodNameFieldName);

                methodName.stringValue = GetSerializableMethodName(method);

                ParameterInfo[] argParams;
                if (!method.IsStatic) {
                    List<ParameterInfo> argParamList = new List<ParameterInfo>();
                    argParamList.Add(null);
                    argParamList.AddRange(method.GetParameters());
                    argParams = argParamList.ToArray();
                }
                else {
                    argParams = method.GetParameters();
                }
                var argumentArray = listener.FindPropertyRelative(ArgumentsFieldName);
                argumentArray.arraySize = argParams.Length;

                for (int i = 0; i < argumentArray.arraySize; i++) {
                    var argument = argumentArray.GetArrayElementAtIndex(i);
                    ParameterInfo parameter = argParams[i];
                    Type parameterType = parameter?.ParameterType ?? method.ReflectedType;

                    ParameterTypeEnum parameterTypeEnum = GetParameterType(method, i);

                    //argument.FindPropertyRelative(ParameterTypeFlagsFieldName).intValue = (int)parameterTypeEnum;

                    var argTypeEnum = GetTypeEnum(parameterType);
                    switch (argTypeEnum) {
                        case TypeEnum.Object:
                            var objectArgument = argument.FindPropertyRelative(ObjectArgumentFieldName);
                            bool isMethod = ((Argument.Definition)argument.FindPropertyRelative(ArgumentDefitionFieldName).intValue & Argument.Definition.IsMethod) != 0;
                            if (objectArgument.objectReferenceValue && !isMethod) {
                                if (parameterType.IsInstanceOfType(objectArgument)) break;
                                if (parameterType.IsInstanceOfType(target)) objectArgument.objectReferenceValue = target;
                            }
                            break;
                    }
                }

                listener.serializedObject.ApplyModifiedProperties();
            }

            public void Clear() {
                // find the current event target...
                var methodName = listener.FindPropertyRelative(MethodNameFieldName);

                string[] data = methodName.stringValue.Split(';');
                Type serializedType = ExtendedEvent.GetType(data[0]);

                methodName.stringValue = (data.Length > 1 && serializedType != null) ? GetSerializableTypeName(serializedType) : string.Empty;

                var arguments = listener.FindPropertyRelative(ArgumentsFieldName);
                arguments.arraySize = 1;
                //arguments.GetArrayElementAtIndex(0).FindPropertyRelative(ParameterTypeFieldName).stringValue = GetSerializableTypeName(typeof(object));
                arguments.GetArrayElementAtIndex(0).FindPropertyRelative(ArgumentDefitionFieldName).boolValue = false;

                listener.serializedObject.ApplyModifiedProperties();
            }
        }

        struct ArgumentContextData {
            #region Fields

            public SerializedProperty call;

            public int index;

            public ArgumentOperation contextOperation;

            #endregion

            public ArgumentContextData(SerializedProperty call, int index) {
                this.call = call;
                this.index = index;
                this.contextOperation = ArgumentOperation.Nothing;
            }

            public void Execute() {
                SerializedProperty argument = call.FindPropertyRelative(ArgumentsFieldName).GetArrayElementAtIndex(index);

                var refPropertyFlag = argument.FindPropertyRelative(ArgumentDefitionFieldName);
                var arguments = call.FindPropertyRelative(ArgumentsFieldName);
                SerializedProperty argumentSpecialFlags = call.FindPropertyRelative(CallDefinionFieldName);

                switch (contextOperation) {
                    case ArgumentOperation.MakeCachedArgument:
                        ToggleFlag(argument, Argument.Definition.CacheReturnValue);
                        break;
                    case ArgumentOperation.SetEnumerator:
                        bool isCached = (argumentSpecialFlags.intValue & (1 << index)) != 0;
                        argumentSpecialFlags.intValue &= ~(int)EventCall.Definition.ArgIsEnumeratorAll;
                        if (!isCached) argumentSpecialFlags.intValue |= 1 << index;
                        break;
                    case ArgumentOperation.ToggleModifyFlag:
                        ToggleFlag(argument, Argument.Definition.NegateBool);
                        break;
                    case ArgumentOperation.PasteEventID:
                        var refPasteID = argument.FindPropertyRelative(IntArgumentFieldName);
                        refPasteID.intValue = CopiedEvents[0].id;
                        break;
                    case ArgumentOperation.Copy:
                        CopiedArgument = new ArgumentMap(argument);
                        break;
                    case ArgumentOperation.CopyID:
                        EditorGUIUtility.systemCopyBuffer = GetArgumentID(call.FindPropertyRelative(IDFieldName).intValue, index).ToString();
                        break;
                    case ArgumentOperation.Paste:
                        CopiedArgument.PasteValues(argument);
                        break;
                    case ArgumentOperation.CopyParameterType:
                        MethodCache method = FindMethod(call);
                        if (method != null) EditorGUIUtility.systemCopyBuffer = method.GetParameterTypes(true)[index].SerializableTypeName;
                        break;
                    case ArgumentOperation.CopyArgumentType:
                        MethodCache func = FindArgumentFunc(argument);
                        if (func != null) EditorGUIUtility.systemCopyBuffer = func.ReturnType.SerializableTypeName;
                        break;
                    case ArgumentOperation.TogglePreview:
                        argument.FindPropertyRelative(FuncPreviewFieldName).boolValue = !argument.FindPropertyRelative(FuncPreviewFieldName).boolValue;
                        break;
                }
                argument.serializedObject.ApplyModifiedProperties();
            }

            public ArgumentContextData SetOperation(ArgumentOperation contextOperation) {
                this.contextOperation = contextOperation;
                return this;
            }
        }

        struct TypeContextData {
            #region Fields

            public SerializedProperty stringArgument;

            public Type type;

            #endregion

            public TypeContextData(SerializedProperty stringArgument, Type type) {
                this.stringArgument = stringArgument;
                this.type = type;
            }
        }

        struct CallContextData {
            #region Fields

            public SerializedProperty listenersArray;

            public ContextOperation contextOperation;

            public int elementIndex;

            #endregion

            public CallContextData(SerializedProperty listenersArray, int elementIndex) {
                this.listenersArray = listenersArray;
                this.elementIndex = elementIndex;
                this.contextOperation = ContextOperation.Nothing;
            }

            public void Execute() {
                switch (contextOperation) {
                    case ContextOperation.Dublicate:
                        var temp = new CallMap(listenersArray.GetArrayElementAtIndex(elementIndex));
                        listenersArray.InsertArrayElementAtIndex(elementIndex + 1);
                        //when we dublicate event we always generate a fresh new id
                        temp.PasteValues(listenersArray.GetArrayElementAtIndex(elementIndex + 1), GenerateID(listenersArray));
                        break;
                    case ContextOperation.Add:
                        int index = Mathf.Min(listenersArray.arraySize, insertIndex);
                        for (int i = 0; i < CopiedEvents.Count; i++) {
                            listenersArray.InsertArrayElementAtIndex(i + index);
                            //if we insert event list into an empty array then we just use copied IDs, otherwise generate new if ID is already occupied
                            CopiedEvents[i].PasteValues(listenersArray.GetArrayElementAtIndex(i + index), GenerateID(listenersArray));
                        }
                        break;
                    case ContextOperation.PasteOne:
                        bool wasEmpty = listenersArray.arraySize == 0;
                        //when we paste one event, we use copied id if array was empty, otherwise we keep old id
                        int newID = wasEmpty ? GenerateID(listenersArray) : listenersArray.GetArrayElementAtIndex(elementIndex).FindPropertyRelative(IDFieldName).intValue;
                        if (wasEmpty) listenersArray.arraySize += 1;
                        CopiedEvents[0].PasteValues(listenersArray.GetArrayElementAtIndex(elementIndex), newID);
                        break;
                    case ContextOperation.DeleteOne:
                        listenersArray.DeleteArrayElementAtIndex(elementIndex);
                        break;
                    case ContextOperation.CopyID:
                        EditorGUIUtility.systemCopyBuffer = listenersArray.GetArrayElementAtIndex(elementIndex).FindPropertyRelative(IDFieldName).intValue.ToString();
                        break;
                    case ContextOperation.PasteID:
                        listenersArray.GetArrayElementAtIndex(elementIndex).FindPropertyRelative(IDFieldName).intValue = int.Parse(EditorGUIUtility.systemCopyBuffer);
                        break;
                    case ContextOperation.RandomizeID:
                        listenersArray.GetArrayElementAtIndex(elementIndex).FindPropertyRelative(IDFieldName).intValue = GenerateID(listenersArray);
                        break;
                }
                listenersArray.serializedObject.ApplyModifiedProperties();
            }

            public CallContextData SetOperation(ContextOperation contextOperation) {
                this.contextOperation = contextOperation;
                return this;
            }
        }

        public class CallMap {
            #region Fields

            public string methodName;

            public bool definition;

            public bool enabled;

            public GenericPropertyMap tag;

            public int delayMode;

            public float delayValue;

            public int delayID;

            public int id;

            private ArgumentMap[] arguments;

            #endregion

            public CallMap(SerializedProperty from) {

                enabled = from.FindPropertyRelative(EnabledFieldName).boolValue;
                //tag = GetValue(from.FindPropertyRelative(TagFieldName));
                tag = new GenericPropertyMap(from.FindPropertyRelative(TagFieldName));
                methodName = from.FindPropertyRelative(MethodNameFieldName).stringValue;
                definition = from.FindPropertyRelative(CallDefinionFieldName).boolValue;
                delayMode = from.FindPropertyRelative(DelayModeFieldName).intValue;
                delayValue = from.FindPropertyRelative(DelayValueFieldName).floatValue;
                delayID = from.FindPropertyRelative(DelayIDFieldName).intValue;
                id = from.FindPropertyRelative(IDFieldName).intValue;

                var argumentsFrom = from.FindPropertyRelative(ArgumentsFieldName);
                arguments = new ArgumentMap[argumentsFrom.arraySize];

                for (int i = 0; i < arguments.Length; i++) {
                    arguments[i] = new ArgumentMap(argumentsFrom.GetArrayElementAtIndex(i));
                }
            }

            public void PasteValues(SerializedProperty to, int id) {
                to.FindPropertyRelative(EnabledFieldName).boolValue = enabled;
                //SetValue(to.FindPropertyRelative(TagFieldName), tag);
                tag.PasteValues(to.FindPropertyRelative(TagFieldName));
                to.FindPropertyRelative(MethodNameFieldName).stringValue = methodName;
                to.FindPropertyRelative(CallDefinionFieldName).boolValue = definition;
                to.FindPropertyRelative(DelayModeFieldName).intValue = delayMode;
                to.FindPropertyRelative(DelayValueFieldName).floatValue = delayValue;
                to.FindPropertyRelative(DelayIDFieldName).intValue = delayID;
                to.FindPropertyRelative(IDFieldName).intValue = id;

                var argumentsTo = to.FindPropertyRelative(ArgumentsFieldName);
                argumentsTo.arraySize = arguments.Length;

                for (int i = 0; i < arguments.Length; i++) {
                    arguments[i].PasteValues(argumentsTo.GetArrayElementAtIndex(i));
                }
            }
        }

        public class ParameterCache {
            #region Fields

            private readonly Attribute[] attributes;

            #endregion

            public ParameterCache(ParameterInfo parameter) {
                this.parameter = parameter;
                this.ParameterType = GetTypeCache(parameter.ParameterType);
                this.attributes = parameter.GetCustomAttributes(true).
                    Union(parameter.ParameterType.GetCustomAttributes(true)).
                    Select(a => (Attribute)a).ToArray();

                this.Label = new GUIContent(parameter.Name, $"{GetDisplayTypeName(parameter.ParameterType)} {parameter.Name}");

                Drawer = MakeDrawer((MethodInfo)parameter.Member, parameter, parameter.ParameterType);
            }

            public object Drawer { get; }

            public GUIContent Label { get; }

            public ParameterInfo parameter { get; }

            public TypeCache ParameterType { get; }

            public T GetCustomAttribute<T>() where T : Attribute => attributes.FirstOrDefault(a => a is T) as T;
        }

        public class PropertyValue<T> {
            #region Fields

            public SerializedProperty property;

            public T value;

            #endregion

            public PropertyValue(SerializedProperty property, T value) {
                this.property = property;
                this.value = value;
            }
        }

        public class TypeCache {
            public TypeCache(Type type) {
                this.type = type;
                this.Label = new GUIContent(type.Name, GetDisplayTypeName(type));
                this.TypeEnum = GetTypeEnum(type);
                this.Drawer = MakeDrawer(null, null, type);

                this.IsFlags = ((FlagsAttribute)Attribute.GetCustomAttribute(type, typeof(FlagsAttribute))) != null;

                this.DisplayName = GetDisplayTypeName(type);
                this.SerializableTypeName = GetSerializableTypeName(type);
            }

            public string DisplayName { get; }

            public object Drawer { get; }

            public bool IsFlags { get; }

            public GUIContent Label { get; }

            public string SerializableTypeName { get; }

            //public static implicit operator Type(TypeCache c) => c.type;
            public Type type { get; }

            public TypeEnum TypeEnum { get; }
        }

        //static Dictionary<Type, List<ValidFuncMap>> FuncCollections = new Dictionary<Type, List<ValidFuncMap>>();
        protected class State {
            #region Fields

            public ReorderableList reorderableList;

            public int lastSelectedIndex;

            #endregion
        }

        public class GenericPropertyMap {
            private object value;
            private Dictionary<string, object> values = new Dictionary<string, object>();

            public GenericPropertyMap(SerializedProperty from) {
                value = GetValue(from);
                if (value != null) {
                    return;
                }
                foreach (var p in GetProperties(from)) {
                    values.TryAdd(p.name, GetValue(p));
                }
            }
            private static IEnumerable<SerializedProperty> GetProperties(SerializedProperty property) {
                IEnumerator enumerator = property.GetEnumerator();
                while (enumerator.MoveNext()) {
                    var prop = enumerator.Current as SerializedProperty;
                    if (prop == null) continue;
                    yield return prop;
                }
            }

            public void PasteValues(SerializedProperty to) {
                if (value != null || values.Count == 0) {
                    SetValue(to, value);
                    return;
                }
                foreach (var p in GetProperties(to)) {
                    if (values.TryGetValue(p.name, out object value)) {
                        SetValue(p, value);
                    }
                }
            }
        }

        private class ArgumentMap {
            #region Fields

            public bool funcPreviewFlag;

            //arguments
            public Object objectArgument;

            //public string argumentTypeName;
            public ParameterTypeEnum parameterType;

            public int intArgument;

            public float floatArgument;

            public string stringArgument;

            public bool boolArgument;

            public Vector3 vector3Argument;

            public int methodDefinition;

            private Dictionary<string, object> values = new Dictionary<string, object>();

            #endregion

            //public bool genericFlag;
            public ArgumentMap(SerializedProperty from) {
                boolArgument = from.FindPropertyRelative(BoolArgumentFieldName).boolValue;
                floatArgument = from.FindPropertyRelative(FloatArgumentFieldName).floatValue;
                intArgument = from.FindPropertyRelative(IntArgumentFieldName).intValue;
                objectArgument = from.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue;
                stringArgument = from.FindPropertyRelative(StringArgumentFieldName).stringValue;
                vector3Argument = from.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value;

                methodDefinition = from.FindPropertyRelative(ArgumentDefitionFieldName).intValue;

                //argumentTypeName = from.FindPropertyRelative(ParameterTypeFieldName).stringValue;
                //parameterType = (ParameterTypeEnum)from.FindPropertyRelative(ParameterTypeFlagsFieldName).intValue;

                funcPreviewFlag = from.FindPropertyRelative(FuncPreviewFieldName).boolValue;


                foreach (var p in GetExpandedProperties(from)) {
                    values.Add(p.name, GetValue(p));
                }
            }

            private static IEnumerable<SerializedProperty> GetExpandedProperties(SerializedProperty argument) {
                var current = argument.FindPropertyRelative(FuncPreviewFieldName);
                var next = argument.Copy();
                next.Next(false);
                while (current.Next(false)) {
                    if (current.propertyPath == next.propertyPath) break;
                    yield return current;
                }
            }

            public void PasteValues(SerializedProperty to) {
                to.FindPropertyRelative(BoolArgumentFieldName).boolValue = boolArgument;
                to.FindPropertyRelative(FloatArgumentFieldName).floatValue = floatArgument;
                to.FindPropertyRelative(IntArgumentFieldName).intValue = intArgument;
                to.FindPropertyRelative(ObjectArgumentFieldName).objectReferenceValue = objectArgument;
                to.FindPropertyRelative(Vector3ArgumentFieldName).vector3Value = vector3Argument;
                to.FindPropertyRelative(StringArgumentFieldName).stringValue = stringArgument;

                to.FindPropertyRelative(ArgumentDefitionFieldName).intValue = methodDefinition;

                to.FindPropertyRelative(FuncPreviewFieldName).boolValue = funcPreviewFlag;

                foreach (var p in GetExpandedProperties(to)) {
                    if (values.TryGetValue(p.name, out object value)) {
                        SetValue(p, value);
                    }
                }
            }
        }

        private class DrawerDefiniton {
            public DrawerDefiniton(CustomParameterDrawer attribute, Type drawerType) {
                this.key = attribute;
                this.drawerType = drawerType;
            }

            public Type drawerType { get; }

            public CustomParameterDrawer key { get; }
        }

        private static class DrawerField<T> {
            #region Fields

            private static T field;

            #endregion
        }

        private class FuncPopupDrawer {
            public static void ClearArgFunction(object source) {
                ((GetArgFunction)source).Clear();
            }

            public static void SetArgFunction(object source) {
                ((GetArgFunction)source).Assign();
            }

            public struct GetArgFunction {
                #region Fields

                private readonly SerializedProperty argument;

                private readonly Object target;

                private readonly MethodInfo method;

                #endregion

                public GetArgFunction(SerializedProperty argument, Object target, MethodInfo method) {
                    this.argument = argument;
                    this.target = target;
                    this.method = method;
                }

                public void Assign() {
                    // find the current event target...
                    var listenerTarget = argument.FindPropertyRelative(ObjectArgumentFieldName);
                    var stringArgument = argument.FindPropertyRelative(StringArgumentFieldName);
                    //var argTypeProperty = argument.FindPropertyRelative(MethodDefitionFieldName);

                    listenerTarget.objectReferenceValue = target;
                    stringArgument.stringValue = GetSerializableMethodName(method);

                    var previewAttribute = method.GetCustomAttribute<FuncPreviewAttribute>();
                    argument.FindPropertyRelative(FuncPreviewFieldName).boolValue = previewAttribute != null;

                    argument.serializedObject.ApplyModifiedProperties();
                }

                public void Clear() {
                    // find the current event target...
                    var methodName = argument.FindPropertyRelative(StringArgumentFieldName);
                    methodName.stringValue = null;

                    var methodIsStatic = argument.FindPropertyRelative(BoolArgumentFieldName);
                    methodIsStatic.boolValue = false;

                    //var objectArgument = argument.FindPropertyRelative(ObjectArgumentFieldName);
                    //objectArgument.objectReferenceValue = null;

                    argument.serializedObject.ApplyModifiedProperties();
                }
            }
        }

        private class MethodCache {
            #region Fields

            private readonly ParameterCache[] parameters;

            private readonly ParameterCache[] argumentParameters;

            private readonly TypeCache[] parameterTypes;

            private readonly TypeCache[] argumentParameterTypes;

            private readonly GUIContent[] parameterLabels;

            private readonly Attribute[] attributes;

            #endregion

            public MethodCache(MethodInfo method) {
                this.method = method;
                IsStatic = method.IsStatic;

                attributes = method.GetCustomAttributes(typeof(Attribute), true).
                    Union(GetDefiningProperty(method)?.GetCustomAttributes(typeof(Attribute), true) ?? Array.Empty<object>()).
                    Select(a => (Attribute)a).ToArray();

                ReflectedType = GetTypeCache(method.ReflectedType);

                ReturnType = GetTypeCache(method.ReturnType);
                IsGenericMethod = method.IsGenericMethod;
                genericArguments = method.GetGenericArguments();

                Name = method.Name;
                Label = new GUIContent(GetSimpleMethodName(method), GetDisplayMethodName(method));
                DisplayName = GetDisplayMethodName(method);


                parameters = method.GetParameters().Select(p => new ParameterCache(p)).ToArray();

                if (method.IsStatic) {
                    argumentParameters = parameters;
                }
                else {
                    List<ParameterCache> list = new List<ParameterCache>();
                    list.Add(null);
                    list.AddRange(parameters);
                    argumentParameters = list.ToArray();
                }

                parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
                argumentParameterTypes = argumentParameters.Select(p => p?.ParameterType ?? GetTypeCache(method.ReflectedType)).ToArray();

                parameterLabels = argumentParameters.Select(p => p?.Label ?? GetTypeCache(method.ReflectedType).Label).ToArray();

                foreach (var type in parameterTypes) {
                    if (type.type.IsByRef || type.type.IsPointer) {
                        containsReferenceParameters = true;
                        break;
                    }
                }
            }

            public string DisplayName { get; }

            public bool IsGenericMethod { get; }

            public bool IsStatic { get; }

            public GUIContent Label { get; }

            public MethodInfo method { get; }

            public string Name { get; }

            public TypeCache ReflectedType { get; }

            public TypeCache ReturnType { get; }

            public bool containsReferenceParameters { get; }


            private Type[] genericArguments { get; }

            public T GetCustomAttribute<T>() where T : Attribute => attributes.FirstOrDefault(a => a is T) as T;

            public Type[] GetGenericArguments() => genericArguments;

            public GUIContent[] GetParameterLabels() => parameterLabels;

            public ParameterCache[] GetParameters(bool useForArgumentArray) => useForArgumentArray ? argumentParameters : parameters;

            public TypeCache[] GetParameterTypes(bool useForArgumentArray) => useForArgumentArray ? argumentParameterTypes : parameterTypes;
        }

        private class MethodDefinition {
            #region Fields

            public MethodInfo method;

            public Type[] ParameterTypes;

            public bool IsGenericMethod;

            #endregion

            public static List<MethodDefinition> Make(List<MethodInfo> methods) {
                List<MethodDefinition> list = new List<MethodDefinition>();
                for (int i = 0; i < methods.Count; i++) {
                    var def = new MethodDefinition();
                    def.method = methods[i];
                    def.IsGenericMethod = methods[i].IsGenericMethod;
                    def.ParameterTypes = methods[i].GetParameters().Select(x => x.ParameterType).ToArray();

                    list.Add(def);
                }
                return list;
            }
        }

        private class SelectedIData {
            #region Fields

            public SerializedProperty intArgument;

            public int id;

            #endregion

            public SelectedIData(SerializedProperty intArgument, int id) {
                this.intArgument = intArgument;
                this.id = id;
            }
        }

        private static Rect[] DivideRect(Rect rect, params float[] widths) {
            float totalWidth = 0;
            Rect[] rects = new Rect[widths.Length];
            for (int i = 0; i < widths.Length; i++) totalWidth += widths[i];
            for (int i = 0; i < widths.Length; i++) {
                var newRect = rect;
                newRect.width *= widths[i] / totalWidth;
                if (i != 0) {
                    newRect.width -= EditorGUIUtility.standardVerticalSpacing;
                    newRect.x = rects[i - 1].xMax + EditorGUIUtility.standardVerticalSpacing;
                }
                rects[i] = newRect;
            }
            return rects;
        }

        private static Rect DivideRect(ref Rect rect, float width1, float width2, bool newRectIsLeft = false) {
            float totalWidth = width1 + width2;
            Rect rect1 = rect;
            Rect rect2 = rect;
            rect1.width *= width1 / totalWidth;
            rect2.xMin = rect1.xMax + EditorGUIUtility.standardVerticalSpacing;
            if (newRectIsLeft) {
                rect = rect2;
                return rect1;
            }
            else {
                rect = rect1;
                return rect2;
            }
        }

        private static Rect DivideRectVertical(ref Rect rect, float height) {
            Rect newRect = rect;
            newRect.height = height == 0 ? EditorGUIUtility.singleLineHeight : height;
            rect.yMin = newRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            return newRect;
        }

    }
}