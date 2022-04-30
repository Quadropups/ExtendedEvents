using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace ExtendedEvents {
    /// <summary>
    /// Base class taht represents event call that is used to store function to create invokable event range for <see cref="Invoker"/>.
    /// </summary>
    [Serializable]
    public abstract class EventCall {
        #region Constants

        public const int MaxArgs = 8;

        #endregion

        #region Fields

        [SerializeField] protected Definition _definition;

        [SerializeField] protected int _id;

        [SerializeField] private DelayMode _delayMode;

        [SerializeField] private string _methodName;

        [SerializeField] private bool _enabled;

        [SerializeField] private float _delayValue;

        [SerializeField] private int _delayID;

        #endregion

        protected EventCall(EventCall source) {
            _id = source._id;

            _delayMode = source._delayMode;

            _methodName = source._methodName;

            _enabled = source._enabled;

            _delayValue = source._delayValue;

            _delayID = source._delayID;

            _definition = source._definition;
        }

        public int argumentCount => arguments.Length;

        public abstract Argument[] arguments { get; }

        public int delayID => _delayID;

        public DelayMode delayMode => _delayMode;

        public float delayValue => _delayValue;

        public bool enabled => _enabled;

        public int id => _id;

        public string methodName => _methodName;

        public Type ReturnType => CachedData.GetMethodInfo(_methodName).ReturnType;

        #region Enums

        [Flags]
        public enum Definition {
            Nothing = 0,

            /// <summary>If true then this method will return negated bool value</summary>
            NegateBool = 1 << 6,
            /// <summary>If true then this method will be called once to create a cached argument</summary>
            CacheReturnValue = 1 << 7,
        }

        #endregion

        public bool negateBool => (_definition & Definition.NegateBool) != 0;

        public bool cacheReturnValue => (_definition & Definition.CacheReturnValue) != 0;


        public Argument GetArgumentAt(int index) {
            return arguments[index];
        }

        public MethodInfo GetMethodInfo() => CachedData.GetMethodInfo(_methodName);

        public Type GetParameterType(int index) {
            MethodInfo method = GetMethodInfo();
            if (method == null) return null;
            if (method.IsStatic) {
                return method.GetParameters()[index].ParameterType;
            }
            else {
                if (index == 0) return method.ReflectedType;
                else return method.GetParameters()[index - 1].ParameterType;
            }
        }

        public abstract object GetTag();

        public override string ToString() {
            return $"Type: {GetType().Name}. ID: {_id}. Method: {GetMethodInfo()?.Name}. Method signature: {_methodName}";
        }
    }

    /// <summary>
    /// A tagged event call that is used to store function to create invokable event range for <see cref="Invoker"/> with specified <see cref="TTag"/>.
    /// </summary>
    public abstract class EventCall<TTag> : EventCall {
        #region Fields

        [SerializeField] private TTag _tag;

        #endregion

        public EventCall(EventCall source, TTag tag) : base(source) {
            _tag = tag;
        }

        protected EventCall(EventCall<TTag> source) : base(source) {
            _tag = source._tag;
        }

        public TTag tag => _tag;

        public override object GetTag() => _tag;

        public IEnumerable<TTag> GetTags() {
            yield return tag;
            foreach (Argument argument in arguments) {
                if (argument.TryGetTag(out TTag argTag)) {
                    yield return argTag;
                }
            }
        }
    }

    /// <summary>
    /// A serializable tagged event call that is used to store function to create invokable event range for <see cref="Invoker"/> with specified <see cref="TTag"/>.
    /// </summary>
    [Serializable]
    public class EventCall<TTag, TArgument> : EventCall<TTag> where TArgument : Argument {
        #region Fields

        [SerializeField] protected TArgument[] _arguments;

        #endregion

        public EventCall(EventCall source, TTag tag) : base(source, tag) {
#if UNITY_EDITOR
            if (!(source.arguments is TArgument[])) Debug.LogError($"{source.arguments} can't be used as {typeof(TArgument[])}");
#endif
            _arguments = source.arguments as TArgument[];
        }

        public EventCall(EventCall<TTag, TArgument> source) : base(source) {
            _arguments = source._arguments;
        }

        public override Argument[] arguments => _arguments;
    }
}
