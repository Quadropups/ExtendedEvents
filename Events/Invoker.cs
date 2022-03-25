using System;
using System.Collections.Generic;

namespace ExtendedEvents {
    /// <summary>
    /// Runtime ExtendedEvent invoker that can used to invoke calls with specified tag or subscribe and unsubscribe parameterless events.
    /// <para></para>
    /// Use <see cref="Invoker{TArg}"/> instead to invoke events that take CustomEventArg as a parameter (OnCollisionEnter etc).
    /// </summary>
    /// Invoking with Invoker rather that <see cref="ExtendedEvent{TTag}.Invoke(TTag)"/> Method allows you to optimize invokation process because it does not involve looking for invoker it the dictionary.
    /// Use <see cref="ExtendedEvent{TTag}.GetInvoker(TTag)"/> Method to retrieve this invoker.
    public abstract class Invoker : InvokerBase {
        public event Action del;
        public virtual IEnumerable<T> GetValues<T>() => Array.Empty<T>();

        public abstract void Invoke();

        protected void InvokeDelegates() => del?.Invoke();
    }

    /// <summary>
    /// Runtime ExtendedEvent invoker that can used to invoke calls with specified tag and CustomEventArg or subscribe and unsubscribe events that take specified CustomEventArg as a parameter .
    /// <para></para>
    /// Use <see cref="Invoker"/> instead to invoke events that take no custom parameters.
    /// </summary>
    /// Invoking with Invoker rather that <see cref="ExtendedEvent{TTag}.Invoke{TArg}(TTag, TArg)"/> Method allows you to optimize invokation process because it does not involve looking for invoker it the dictionary.
    /// Use <see cref="ExtendedEvent{TTag}.GetInvoker{TArg}(TTag)"/> Method to retrieve this invoker.
    public abstract class Invoker<TArg> : InvokerBase {
        public event Action<TArg> del;

        public virtual IEnumerable<T> GetValues<T>(TArg eventArg) => Array.Empty<T>();

        public abstract void Invoke(TArg eventArg);

        protected void InvokeDelegates(TArg eventArg) => del?.Invoke(eventArg);
    }

    /// <summary>
    /// Base class for Invoker.
    /// </summary>
    public abstract class InvokerBase {
        public virtual IEnumerable<CachedData> GetCalls() => Array.Empty<CachedData>();

        public virtual void StopCoroutine() {
        }
    }
}
