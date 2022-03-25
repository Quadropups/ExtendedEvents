using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ExtendedEvents {
    /// <summary>
    /// A serializable argument that is used to store data, reference or function to be passed to <see cref="EventCall"/>'s Method's parameter as an argument.
    /// </summary>
    /// You can derive from this class to let is serialize any data type. For example <see cref="ExpandedArgument"/> derives from this class and allows you to serialize <see cref="AnimationCurve"/> and<see cref="Gradient"/>.
    /// <see cref="GetValue(Type)"/> Method is used to retrieve serialized data.
    [Serializable]
    public class Argument {
        #region Constants

        public const int ArgTypeFlag1 = 8;
        public const int ArgTypeFlag2 = 16;
        public const int ArgTypeFlag3 = 24;

        #endregion

        #region Fields

        private static readonly Dictionary<Type, TypeEnum> TypEnums = new Dictionary<Type, TypeEnum> {
            { typeof(bool), TypeEnum.Boolean},
            { typeof(int), TypeEnum.Integer},
            { typeof(char), TypeEnum.Character},
            { typeof(float), TypeEnum.Float},
            { typeof(string), TypeEnum.String},
            { typeof(Type), TypeEnum.Type},
            { typeof(Vector2), TypeEnum.Vector2},
            { typeof(Vector3), TypeEnum.Vector3},
            { typeof(Vector4), TypeEnum.Vector4},
            { typeof(Quaternion), TypeEnum.Quaternion},
            { typeof(Color), TypeEnum.Color},
            { typeof(LayerMask), TypeEnum.LayerMask},

            //{ typeof(Gradient), TypeEnum.Gradient},
            //{ typeof(AnimationCurve), TypeEnum.AnimationCurve},
            { typeof(Gradient), TypeEnum.Generic},
            { typeof(AnimationCurve), TypeEnum.Generic},

            { typeof(object), TypeEnum.Object},
            //most common object types
            { typeof(Object), TypeEnum.Object},
            { typeof(GameObject), TypeEnum.Object},
            { typeof(Transform), TypeEnum.Object},
            { typeof(Component), TypeEnum.Object},
        };

        /// <summary>Object argument. If property - target of the method</summary>
        [SerializeField] protected Object _objectArgument;

        /// <summary>String argument. If property - name of the method</summary>
        [SerializeField] protected string _stringArgument;

        /// <summary>Integer argument</summary>
        [SerializeField] protected int _intArgument;

        /// <summary>Float argument</summary>
        [SerializeField] protected float _floatArgument;

        /// <summary>Boolean argument</summary>
        [SerializeField] protected bool _boolArgument;

        /// <summary>Vector3 argument</summary>
        [SerializeField] protected Vector3 _vector3Argument;

        /// <summary>What type of property this is</summary>
        [SerializeField] private Definition _definition;

        #endregion

#if UNITY_EDITOR
        [SerializeField] private bool _editorPreviewFlag;
#endif
        public bool cacheReturnValue => (_definition & Definition.CacheReturnValue) != 0;

        public bool isReferencable {
            get {
                switch (GetArgType()) {
                    case ArgType.Data:
                    case ArgType.Method:
                        return true;
                    default:
                    case ArgType.Parent:
                    case ArgType.IDReference:
                    case ArgType.TagReference:
                    case ArgType.CustomEventArg:
                        return false;
                }
            }
        }

        public bool negateBool => (_definition & Definition.NegateBool) != 0;

        #region Enums

        public enum ArgType {
            Data,
            Method = Definition.IsMethod,
            Parent = Definition.Arg1IsParent,
            IDReference = Definition.Arg1IsIDReference,
            TagReference = Definition.Arg1IsTagReference,
            CustomEventArg = Definition.Arg1IsCustomEventArgs,
            Caster = Definition.Arg1IsCustomEventArgs,
        }

        [Flags]
        public enum Definition {
            None = 0,
            /// <summary>If true then argument is Func call</summary>
            IsMethod = 1 << 0,
            /// <summary>If true then this method will return negated bool value</summary>
            NegateBool = 1 << 6,
            /// <summary>If true then this method will be called once to create a cached argument</summary>
            CacheReturnValue = 1 << 7,
            /// <summary>If true then argument is a refernce to data  (Call or argument) in this ExtendedEvent through it's ID</summary>
            Arg1IsIDReference = 1 << ArgTypeFlag1,
            /// <summary>If true then argument is a refernce parent monobehaviour</summary>
            Arg1IsParent = 1 << ArgTypeFlag2,
            /// <summary>If true then argument is a refernce to a Call in this ExtendedEvent through it's Tag</summary>
            Arg1IsTagReference = (1 << ArgTypeFlag1) | (1 << ArgTypeFlag2),
            /// <summary>If true then argument is a refernce custom data passed by Invoke call with data</summary>
            Arg1IsCustomEventArgs = 1 << ArgTypeFlag3,
            Arg1TypeFlags = (1 << ArgTypeFlag1) | (1 << ArgTypeFlag2) | (1 << ArgTypeFlag3),
        }

        #endregion

        public static ArgType GetFuncArgType(int definition, int index) {
            bool flag1 = (definition & (1 << (index + ArgTypeFlag1))) != 0;
            bool flag2 = (definition & (1 << (index + ArgTypeFlag2))) != 0;
            bool flag3 = (definition & (1 << (index + ArgTypeFlag3))) != 0;
            if (!flag1 && !flag2 && !flag3) return ArgType.Data;
            if (flag1 && !flag2 && !flag3) return ArgType.IDReference;
            if (flag1 && flag2 && !flag3) return ArgType.TagReference;
            if (!flag1 && flag2 && !flag3) return ArgType.Parent;
            if (!flag1 && !flag2 && flag3) return ArgType.CustomEventArg;
            return ~ArgType.Data;
        }

        public static TypeEnum GetTypeEnum(Type type) {
            TypeEnum value;
            if (type == null) value = TypeEnum.Unknown;
            else if (TypEnums.TryGetValue(type, out value)) return value;
            else if (type.IsEnum) {
                if (type.GetEnumUnderlyingType() == typeof(int)) value = TypeEnum.Enum;
                else value = TypeEnum.Generic;
            }
            else if (IsObjectOrInterface(type)) value = TypeEnum.Object;
            else value = TypeEnum.Generic;
            return value;
        }

        /// <summary>Returns true if this type is derived from UnityEngine.Object or is interface that's not an IEnumerable</summary>
        private static bool IsObjectOrInterface(Type type) {
            if (typeof(Object).IsAssignableFrom(type)) return true;
            if (typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return false;
            return type.IsInterface;
        }

        public ArgType GetArgType() {
            if ((_definition & Definition.IsMethod) != 0) return ArgType.Method;
            return (ArgType)(_definition & Definition.Arg1TypeFlags);
        }

        /// <summary>Get serialized boolean field directly. Use this method only to cast Argument class instance to another type</summary>
        public bool GetBoolArgument() => _boolArgument;

        /// <summary>Get serialized float field directly. Use this method only to cast Argument class instance to another type</summary>
        public float GetFloatArgument() => _floatArgument;

        public ArgType GetFuncArgType(int index) => GetFuncArgType((int)_definition, index);

        /// <summary>Get serialized integer field directly. Use this method only to cast Argument class instance to another type</summary>
        public int GetIntArgument() => _intArgument;

        /// <summary>Get serialized object field directly. Use this method only to cast Argument class instance to another type</summary>
        public Object GetObjectArgument() => _objectArgument;

        public string GetReturnName(Type desiredType) {
            switch (GetArgType()) {
                default:
                    return null;
                case ArgType.Data:
                    return desiredType.Name;
                case ArgType.Parent:
                    return null;
                case ArgType.IDReference:
                    return null;
                case ArgType.TagReference:
                    return null;
                case ArgType.Method:
                    return CachedData.GetMethodInfo(_stringArgument).ToString();
            }
        }

        public Type GetReturnType(Type desiredType) {
            switch (GetArgType()) {
                default:
                    return null;
                case ArgType.Data:
                    return desiredType;
                case ArgType.Parent:
                    return null;
                case ArgType.IDReference:
                    return null;
                case ArgType.TagReference:
                    return null;
                case ArgType.Method:
                    return CachedData.GetMethodInfo(_stringArgument).ReturnType;
            }
        }

        /// <summary>Get serialized string field directly. Use this method only to cast Argument class instance to another type</summary>
        public string GetStringArgument() => _stringArgument;

        /// <summary>
        /// This method is used to retrieve serialized data from <see cref="Argument"/>.
        /// </summary>
        public object GetValue(Type type) {
            TypeEnum typeEnum = GetTypeEnum(type);
            switch (typeEnum) {
                case TypeEnum.Integer:
                    return _intArgument;
                case TypeEnum.Boolean:
                    return _boolArgument;
                case TypeEnum.Float:
                    return _floatArgument;
                case TypeEnum.String:
                    return _stringArgument;
                case TypeEnum.Color:
                    return new Color(_vector3Argument.x, _vector3Argument.y, _vector3Argument.z, _floatArgument);
                case TypeEnum.Object:
                    //null check is necessary to ensure that fake-null object is not passed as argument
                    return _objectArgument ? _objectArgument : null;
                case TypeEnum.LayerMask:
                    return (LayerMask)_intArgument;
                case TypeEnum.Enum:
                    return Enum.ToObject(type, _intArgument);
                case TypeEnum.Vector2:
                    return new Vector2(_vector3Argument.x, _vector3Argument.y);
                case TypeEnum.Vector3:
                    return _vector3Argument;
                case TypeEnum.Vector4:
                    return new Vector4(_vector3Argument.x, _vector3Argument.y, _vector3Argument.z, _floatArgument);
                case TypeEnum.Rect:
                    return new Rect(_vector3Argument.x, _vector3Argument.y, _vector3Argument.z, _floatArgument);
                case TypeEnum.Character:
                    return (char)_intArgument;
                case TypeEnum.Quaternion:
                    return Quaternion.Euler(_vector3Argument);
                case TypeEnum.Type:
                    return Type.GetType(_stringArgument, false);
                default:
                case TypeEnum.Generic:
                    //if this is instance of a class derived from Argument then we attempt to cast it to desired type
                    return Cast(type) ?? (_objectArgument ? _objectArgument : null);
                case TypeEnum.Unknown:
                    Debug.LogError($"Impossible to retrieve argument of type {type}");
                    return default;
            }
        }

        /// <summary>Get serialized Vector3 field directly. Use this method only to cast Argument class instance to another type</summary>
        public Vector3 GetVector3Argument() => _vector3Argument;

        public bool TryGetTag<TTag>(out TTag tag) {
            if (GetArgType() == ArgType.TagReference) {
                tag = (TTag)GetValue(typeof(TTag));
                return true;
            }
            for (int i = 0; i < 8; i++) {
                if (GetFuncArgType(i) == ArgType.TagReference) {
                    tag = (TTag)GetValue(typeof(TTag));
                    return true;
                }
            }
            tag = default;
            return false;
        }

        /// <summary>
        /// This method is used to retrieve uncommon serialized data by <see cref="GetValue(Type)"/> method. It will simply cast Argument to desired type.
        /// <para></para>
        /// If this Argument is an instance of a derived class which implements serialization of desired type, cast will be successful (return a value instead of null).
        /// </summary>
        private object Cast(Type type) {
            try {
                Type caster = typeof(TypeCaster<,>).MakeGenericType(new Type[] { GetType(), type });
                return caster.GetMethod("Cast", BindingFlags.Static | BindingFlags.Public).Invoke(null, new object[] { this });
            }
            catch {
                return null;
            }
        }
    }
}
