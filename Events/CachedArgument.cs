using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExtendedEvents {
    /// <summary>
    /// Data value that is used by <see cref="CachedMethod{TDelegate, TResult}"/> as method parameters' arguments.
    /// </summary>
    public class CachedArgument<TValue> : CachedData<TValue>, IValueReturner<TValue>, IArgument<TValue> {
        #region Fields

        protected TValue value;

        #endregion

        public CachedArgument(TValue arg) {
            this.value = arg;
        }

        public override Type ReturnType => typeof(TValue);

        protected override bool isCachedArgument => true;

        public override MethodInfo GetMethodInfo() => ((Func<TValue>)GetValue).Method;

        public override TValue GetValue() => value;

        public override TValue GetValue<TArg>(TArg eventArg) => GetValue();

        public override void Invoke() {
        }

        public override void Invoke<TArg>(TArg eventArg) {
        }

        public override void SetValue<T>(T value) => SetValue(TypeCaster<T, TValue>.Cast(value));

        /// <summary>
        /// Use this method to set <see cref="CachedArgument{TValue}"/> during runtime.
        /// </summary>
        public virtual void SetValue(TValue value) => this.value = value;

        protected override Type[] GetArgumentTypes() {
            throw new NotImplementedException();
        }

        protected override CachedData GetCachedArgument(Argument argument) {
            return MakeCachedArgument(argument.GetValue(typeof(TValue)), typeof(TValue));
        }

        protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CachedArgument<TValue>, TValue, TDesired>(this);

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
