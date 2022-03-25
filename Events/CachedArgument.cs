using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ExtendedEvents {

    /// <summary>
    /// Data value that is used by <see cref="CachedMethod{TDelegate, TResult}"/> as method parameters' arguments.
    /// </summary>
    public class CachedArgument<TValue> : CachedData<TValue> {
        #region Fields

        private TValue value;

        #endregion

        public CachedArgument(TValue arg) {
            this.value = arg;
        }

        protected override bool isCachedArgument => true;

        public override Type ReturnType => typeof(TValue);

        public override MethodInfo GetMethodInfo() => ((Func<TValue>)GetValue).Method;

        public override T GetValue<T>() {
            return TypeCaster<TValue, T>.Cast(GetValue());
        }

        public override TValue GetValue() => value;

        public override T GetValue<T, TArg>(TArg customArg) => GetValue<T>();

        public override TValue GetValue<TArg>(TArg customArg) => GetValue();

        public override void Invoke() {
#if UNITY_EDITOR
            Debug.LogError($"{typeof(CachedArgument<TValue>)} can't be invoked");
#endif
        }

        public override void Invoke<TArg>(TArg eventArg) {
#if UNITY_EDITOR
            Debug.LogError($"{typeof(CachedArgument<TValue>)} can't be invoked");
#endif
        }

        /// <summary>
        /// Use this method to set <see cref="CachedArgument{TValue}"/> during runtime.
        /// </summary>
        public void SetValue(TValue value) => this.value = value;

        protected override Type[] GetArgumentTypes() {
            throw new NotImplementedException();
        }

        protected override CachedData GetCachedArgument(Argument argument) {
            return MakeCachedArgument(argument.GetValue(typeof(TValue)), typeof(TValue));
        }

        protected override CachedData GetInstance() {
            throw new NotImplementedException();
        }

        protected override void SetArguments(CachedData[] arguments) {
            throw new NotImplementedException();
        }

        protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
        }
    }
}
