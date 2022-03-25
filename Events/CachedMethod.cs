using System;
using System.Reflection;

namespace ExtendedEvents {
    /// <summary>
    /// Base class for any method in <see cref="ExtendedEvent"/>.
    /// </summary>
    public abstract class CachedMethod<TDelegate, TResult> : CachedData<TResult> where TDelegate : Delegate {
        #region Fields

        protected TDelegate method;

        private static Type[] ParameterTypes = ExtendedEvent.MakeParameterTypeArray(typeof(TDelegate));

        #endregion

        public override Type ReturnType => typeof(void);

        public TDelegate GetMethod() => method;

        public override MethodInfo GetMethodInfo() => method.Method;

        /// <summary>
        /// Use this method to set delegate during runtime.
        /// </summary>
        public void SetMethod(TDelegate method) => this.method = method;

        protected abstract CachedMethod<TDelegate, TResult> CreateInstance();

        protected override Type[] GetArgumentTypes() => ParameterTypes;

        protected override CachedData GetInstance() {
            CachedMethod<TDelegate, TResult> newInstance = CreateInstance();
            newInstance.method = method;
            return newInstance;
        }

        protected override void SetMethod(MethodInfo method) {
            if (method.IsStatic) this.method = (TDelegate)method.CreateDelegate(typeof(TDelegate));
            else this.method = (TDelegate)method.CreateDelegate(typeof(TDelegate), null);
        }
    }

    /// <summary>Fake void return type for Action delegates</summary>
    public class Void {
    }
}
