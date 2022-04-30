using System;
using System.Reflection;

namespace ExtendedEvents {
    public interface IArgument : IValueReturner {
        void SetValue<T>(T value);
    }

    public interface IArgument<in T> : IArgument {
        void SetValue(T value);
    }

    public interface ICachedMethod {
        MethodInfo GetMethodInfo();

        void Invoke();

        void Invoke<TArg>(TArg eventArg);
    }

    public interface ICachedMethod<TDelegate> : ICachedMethod where TDelegate : Delegate {
        TDelegate GetMethod();

        void SetMethod(TDelegate method);
    }

    public interface ICoroutineStarter {
        void StopCoroutine();
    }

    public interface IValueReturner {
        T GetValue<T>();

        T GetValue<TArg, T>(TArg eventArg);
    }

    public interface IValueReturner<out T> : IValueReturner {
        T GetValue();

        T GetValue<TArg>(TArg eventArg);
    }
}
