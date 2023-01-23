using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ExtendedEvents {
    /// <summary>
    /// Runtime ExtendedEvent invoker that can used to invoke calls with specified tag or subscribe and unsubscribe parameterless events.
    /// <para></para>
    /// Use <see cref="Invoker{TArg}"/> instead to invoke events that take CustomEventArg as a parameter (OnCollisionEnter etc).
    /// </summary>
    /// Invoking with Invoker rather that <see cref="ExtendedEvent{TTag}.Invoke(TTag)"/> Method allows you to optimize invokation process because it does not involve looking for invoker it the dictionary.
    /// Use <see cref="ExtendedEvent{TTag}.GetInvoker(TTag)"/> Method to retrieve this invoker.
    public abstract class Invoker {
        public abstract IEnumerable<T> GetValues<T>();
        public abstract IEnumerable<T> GetValues<TArg, T>(TArg eventArg);

        public abstract void Invoke();
        public abstract void Invoke<TArg>(TArg eventArg);

        public abstract IEnumerable<CachedData> GetCalls();

        public virtual void StopCoroutine() {
#if UNITY_EDITOR
            Debug.LogWarning($"{GetType()} doesn't start a coroutine");
#endif
        }

        public abstract void Add(Action value);
        public abstract void Add<T>(Action<T> value);
        public abstract void Remove(Action value);
        public abstract void Remove<T>(Action<T> value);

        protected class CombinedData : CachedData {
            private List<CachedData> calls = new List<CachedData>();


            public void Add(Action value) {
                calls.Add(GetCachedData(value));
            }

            public void Add<T>(Action<T> value) {
                calls.Add(GetCachedData(value));
            }
            public void Remove<TDelegate>(TDelegate value) where TDelegate : Delegate {
                //1 because we never touch the original
                for (int i = calls.Count - 1; i >= 0; i--) {
                    if (calls[i] is IDelegate<TDelegate> cast && cast.GetDelegate() == value) {
                        calls.RemoveAt(i);
                        break;
                    }
                }
            }

            public override Type ReturnType => typeof(void);

            public override MethodInfo GetMethodInfo() => ((Action)Invoke).GetMethodInfo();

            public override T GetValue<T>() {
                if (calls.Count == 0) return default;
                return calls[0].GetValue<T>();
            }

            public override T GetValue<TArg, T>(TArg eventArg) {
                if (calls.Count == 0) return default;
                return calls[0].GetValue<TArg, T>(eventArg);
            }

            public override void Invoke() {
                for (int i = 0; i < calls.Count; i++) {
                    calls[i].Invoke();
                }
            }

            public override void Invoke<TArg>(TArg eventArg) {
                for (int i = 0; i < calls.Count; i++) {
                    calls[i].Invoke(eventArg);
                }
            }

            protected override CachedData<TDesired> GetCachedData<TDesired>() {
                throw new NotImplementedException();
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() {
                throw new NotImplementedException();
            }

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                throw new NotImplementedException();
            }
        }

    }
}
