using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ExtendedEvents {
    /// <summary>
    /// Base class for any data or method in <see cref="ExtendedEvent"/>.
    /// </summary>
    public abstract class CachedData {
        #region Fields

        private static readonly CachedData[] CallArrayBuilder = new CachedData[8];

        private static readonly CachedData[] ArgumentArrayBuilder = new CachedData[8];

        private static readonly Dictionary<string, CachedData> ActivatorDatabase = new Dictionary<string, CachedData>();

        #endregion

        public delegate void RefActionDelegate<T>(ref T obj);

        public delegate void RefActionDelegate<T1, in T2>(ref T1 arg1, T2 arg2);

        public delegate void RefActionDelegate<T1, in T2, in T3>(ref T1 arg1, T2 arg2, T3 arg3);

        public delegate void RefActionDelegate<T1, in T2, in T3, in T4>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4);

        public delegate void RefActionDelegate<T1, in T2, in T3, in T4, in T5>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

        public delegate void RefActionDelegate<T1, in T2, in T3, in T4, in T5, in T6>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);

        public delegate void RefActionDelegate<T1, in T2, in T3, in T4, in T5, in T6, in T7>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);

        public delegate void RefActionDelegate<T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);

        public delegate TResult RefFuncDelegate<T1, out TResult>(ref T1 arg);

        public delegate TResult RefFuncDelegate<T1, in T2, out TResult>(ref T1 arg1, T2 arg2);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, out TResult>(ref T1 arg1, T2 arg2, T3 arg3);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, in T4, out TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, in T4, in T5, out TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, in T4, in T5, in T6, out TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, in T4, in T5, in T6, in T7, out TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);

        public delegate TResult RefFuncDelegate<T1, in T2, in T3, in T4, in T5, in T6, in T7, in T8, out TResult>(ref T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);

        public virtual Type ReturnType => throw new NotImplementedException();

        protected virtual bool isCachedArgument => false;

        private interface ICachedDataID {
            int id { get; }
        }

        public static MethodInfo GetMethodInfo(string methodName) => GetActivator(methodName).GetMethodInfo();

        /// <summary>Returns true if any error occured</summary>
        public static void SetupCalls<TTag>(MonoBehaviour parent, EventCall<TTag>[] orderedCalls, out CachedData[] runtimeCalls, out Dictionary<int, CachedData> runtimeReferences) {
            CachedData parentReference = MakeCachedArgument(parent, typeof(MonoBehaviour));

            runtimeCalls = new CachedData[orderedCalls.Length];
            runtimeReferences = new Dictionary<int, CachedData>();
            runtimeReferences.Add(0, parentReference);

            //finally we setup arguments
            for (int i = 0; i < orderedCalls.Length; i++) {
                runtimeCalls[i] = GetRuntimeData(orderedCalls[i], parentReference, orderedCalls, runtimeReferences);
            }

            for (int i = 0; i < orderedCalls.Length; i++) {
                EventCall call = orderedCalls[i];

                if (!call.enabled) {
                    runtimeCalls[i] = new EmptyCall();
                }
                else if (call.delayMode == DelayMode.Pause) {
                    runtimeCalls[i] = new PauseCall(runtimeCalls[i], GetDelayData(call, runtimeReferences));
                }
            }

            foreach (var data in runtimeReferences.Values) {
                data?.UpdateReferences(runtimeReferences);
            }
        }

        protected static CachedData MakeCachedArgument(object value, Type desiredType) {
            //if we pass an object that can't be used directly (need to be cast) then we create an argument of object's type
            desiredType = value?.GetType() ?? desiredType;
            Type cachedArgumentType = typeof(CachedArgument<>).MakeGenericType(desiredType);
            ConstructorInfo ci = cachedArgumentType.GetConstructor(new Type[] { desiredType });
            return ci.Invoke(new object[] { value }) as CachedData;
        }

        protected static void UpdateReference(ref CachedData arg, Dictionary<int, CachedData> runtimeReferences) {
            if (arg is ICachedDataID wrapper) {
                if (runtimeReferences.TryGetValue(wrapper.id, out CachedData data)) {
                    arg = data;
                }
            }
        }

        protected static void UpdateReference<T>(ref CachedData<T> arg, Dictionary<int, CachedData> runtimeReferences) {
            if (arg is CachedDataID<T> wrapper) {
                if (runtimeReferences.TryGetValue(wrapper.id, out CachedData data)) {
                    arg = data?.GetCachedData<T>();
                }
            }
        }

        private static CachedData GetActivator(string methodName) {
            CachedData activator;
            if (!ActivatorDatabase.TryGetValue(methodName, out activator)) {
                MethodInfo method = ExtendedEvent.FindMethod(methodName);
                if (method != null) {
                    activator = MakeCachedData(method);
                    if (activator != null) activator.SetMethod(method);
                }
                else {
                    Type argumentType = Type.GetType(methodName);
                    if (argumentType != null) {
                        activator = MakeCachedArgument(null, argumentType);
                    }
                }

                ActivatorDatabase.Add(methodName, activator);
            }
            return activator;
        }

        private static int GetArgumentID(int callId, int argumentIndex) => unchecked(callId + (argumentIndex + 1) * 486187739);

        private static CachedData GetCachedDataID(int id, Type parameterType) {
            Type specific = typeof(CachedDataID<>).MakeGenericType(parameterType);
            ConstructorInfo ci = specific.GetConstructor(new Type[] { typeof(int) });
            return ci.Invoke(new object[] { id }) as CachedData;
        }

        private static CachedData GetCachedReturnValue(CachedData method) {
            Type specific = typeof(CachedReturnValue<>).MakeGenericType(method.ReturnType);
            ConstructorInfo ci = specific.GetConstructor(new Type[] { typeof(CachedData) });
            return ci.Invoke(new object[] { method }) as CachedData;
        }

        private static CachedData GetData(Argument argument, Type parameterType) {
            return GetCachedDataID(argument.GetIntArgument(), parameterType);
        }

        private static CachedData GetData<TTag>(Argument argument, Type parameterType, EventCall<TTag>[] orderedCalls) {
            object tag = argument.GetValue(typeof(TTag));
            if (tag == null) {
                return null;
            }
            IEquatable<TTag> tagE = tag as IEquatable<TTag>;
            for (int i = 0; i < orderedCalls.Length; i++) {
                EventCall<TTag> call = orderedCalls[i];
                if (tagE?.Equals(call.tag) ?? tag.Equals(call.tag)) {
                    return GetCachedDataID(call.id, parameterType);
                }
            }
            return null;
        }

        private static CachedData<float> GetDelayData(EventCall call) {
            if (call.delayMode != DelayMode.Wait) return null;
            if (call.delayID == 0) {
                return new CachedArgument<float>(call.delayValue);
            }
            return new CachedDataID<float>(call.delayID);
        }

        private static CachedData GetDelayData(EventCall call, Dictionary<int, CachedData> runtimeReferences) {
            switch (call.delayMode) {
                default:
                case DelayMode.NoDelay:
                    return new CachedArgument<float>(0);
                case DelayMode.Wait:
                case DelayMode.Pause:
                    if (call.delayID != 0 && runtimeReferences.TryGetValue(call.delayID, out CachedData data)) {
                        return data;
                    }
                    return new CachedArgument<float>(call.delayValue);
            }
        }

        private static CachedData GetEndData<TTag>(Argument argument, Type parameterType, CachedData parent, EventCall<TTag>[] orderedCalls) {
            switch (argument.GetArgType()) {
                default:
                    return null;
                case Argument.ArgType.Data:
                    return MakeCachedArgument(argument.GetValue(parameterType), parameterType);
                case Argument.ArgType.Parent:
                    return parent;
                case Argument.ArgType.IDReference:
                    return GetData(argument, parameterType);
                case Argument.ArgType.TagReference:
                    return GetData(argument, parameterType, orderedCalls);
                case Argument.ArgType.Method:
                    return GetMethodData(argument, parent, orderedCalls);
                case Argument.ArgType.CustomEventArg:
                    return GetEventArgReference(parameterType);
            }
        }

        private static CachedData GetEventArgReference(Type parameterType) {
            Type specific = typeof(EventArgReference<>).MakeGenericType(parameterType);
            PropertyInfo property = specific.GetProperty(nameof(EventArgReference<object>.Instance), BindingFlags.Public | BindingFlags.Static);
            return (CachedData)property.GetValue(null);
        }

        private static CachedData GetMethodData<TTag>(Argument argument, CachedData parent, EventCall<TTag>[] orderedCalls) {
            CachedData activator = GetActivator(argument.GetStringArgument());
            if (activator == null) {
                return null;
            }

            Type[] parameterTypes = activator.GetArgumentTypes();

            for (int i = 0; i < parameterTypes.Length; i++) {
                Type parameterType = parameterTypes[i];
                switch (argument.GetFuncArgType(i)) {
                    default:
                        ArgumentArrayBuilder[i] = null;
                        break;
                    case Argument.ArgType.Data:
                        ArgumentArrayBuilder[i] = MakeCachedArgument(argument.GetValue(parameterType), parameterType);
                        break;
                    case Argument.ArgType.Parent:
                        ArgumentArrayBuilder[i] = parent;
                        break;
                    case Argument.ArgType.IDReference:
                        ArgumentArrayBuilder[i] = GetData(argument, parameterType);
                        break;
                    case Argument.ArgType.TagReference:
                        ArgumentArrayBuilder[i] = GetData(argument, parameterType, orderedCalls);
                        break;
                    case Argument.ArgType.CustomEventArg:
                        ArgumentArrayBuilder[i] = GetEventArgReference(parameterType);
                        break;
                }
            }

            CachedData instance = activator.GetNew(ArgumentArrayBuilder, argument.negateBool, argument.cacheReturnValue);

            return instance;
        }

        private static CachedData GetRuntimeData<TTag>(EventCall call, CachedData parentReference, EventCall<TTag>[] orderedCalls, Dictionary<int, CachedData> runtimeReferences) {
            //first we find the method
            CachedData activator = GetActivator(call.methodName);

            if (activator == null) {
                return null;
            }

            CachedData callData;

            if (!activator.isCachedArgument) {
                //setup each argument
                Type[] parameterTypes = activator.GetArgumentTypes();

                if (call.arguments.Length != parameterTypes.Length) {
                    return null;
                }

                for (int i = 0; i < call.arguments.Length; i++) {
                    var argData = GetEndData(call.arguments[i], parameterTypes[i], parentReference, orderedCalls);
                    if (call.arguments[i].isReferencable) {
                        runtimeReferences.Add(GetArgumentID(call.id, i), argData);
                    }
                    CallArrayBuilder[i] = argData;
                }

                callData = activator.GetNew(parentReference, CallArrayBuilder, call.GetEnumeratorIndex(), GetDelayData(call));
            }
            else {
                //callData = MakeCachedArgument(call.GetArgumentAt(0), Type.GetType(call.methodName));
                callData = activator.GetCachedArgument(call.GetArgumentAt(0));
            }


            runtimeReferences.Add(call.id, callData);
            return callData;
        }

        private static CachedData MakeCachedData(MethodInfo method) {
            bool methodIsStatic = method.IsStatic;
            Type returnType = method.ReturnType;
            bool methodIsFunc = returnType != typeof(void);
            bool instanceIsStruct = !methodIsStatic && method.ReflectedType.IsValueType;

            ParameterInfo[] parameters = method.GetParameters();

            int parameterCount = parameters.Length + (methodIsStatic ? 0 : 1);

            Type specific;
            switch (parameterCount) {
                case 0:
                    //if this is parameterless instance call then we don't need to use reflection to create cached call
                    if (!methodIsFunc) return new CachedAction();

                    specific = methodIsFunc ? typeof(CachedFunc<>) : typeof(CachedAction);
                    break;
                case 1:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<>);
                        else specific = typeof(CachedRefAction<>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,>);
                        else specific = typeof(CachedRefFunc<,>);
                    }
                    break;
                case 2:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,>);
                        else specific = typeof(CachedRefAction<,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,>);
                        else specific = typeof(CachedRefFunc<,,>);
                    }
                    break;
                case 3:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,>);
                        else specific = typeof(CachedRefAction<,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,>);
                        else specific = typeof(CachedRefFunc<,,,>);
                    }
                    break;
                case 4:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,,>);
                        else specific = typeof(CachedRefAction<,,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,,>);
                        else specific = typeof(CachedRefFunc<,,,,>);
                    }
                    break;
                case 5:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,,,>);
                        else specific = typeof(CachedRefAction<,,,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,,,>);
                        else specific = typeof(CachedRefFunc<,,,,,>);
                    }
                    break;
                case 6:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,,,,>);
                        else specific = typeof(CachedRefAction<,,,,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,,,,>);
                        else specific = typeof(CachedRefFunc<,,,,,,>);
                    }
                    break;
                case 7:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,,,,,>);
                        else specific = typeof(CachedRefAction<,,,,,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,,,,,>);
                        else specific = typeof(CachedRefFunc<,,,,,,,>);
                    }
                    break;
                case 8:
                    if (!methodIsFunc) {
                        if (!instanceIsStruct) specific = typeof(CachedAction<,,,,,,,>);
                        else specific = typeof(CachedRefAction<,,,,,,,>);
                    }
                    else {
                        if (!instanceIsStruct) specific = typeof(CachedFunc<,,,,,,,,>);
                        else specific = typeof(CachedRefFunc<,,,,,,,,>);
                    }
                    break;
                default:
                    return null;
            }

            Type[] parameterTypes = new Type[parameterCount + (methodIsFunc ? 1 : 0)];

            if (!methodIsStatic) {
                parameterTypes[0] = method.ReflectedType;
            }
            for (int i = 0; i < parameters.Length; i++) {
                parameterTypes[i + (methodIsStatic ? 0 : 1)] = parameters[i].ParameterType;
            }
            if (methodIsFunc) parameterTypes[parameterTypes.Length - 1] = returnType;

            //this will fail of one of the types is it or out parameters
            try {
                specific = specific.MakeGenericType(parameterTypes);
            }
            catch {
                return null;
            }

            ConstructorInfo ci = specific.GetConstructor(Array.Empty<Type>());
            return ci.Invoke(Array.Empty<object>()) as CachedData;
        }

        public virtual MethodInfo GetMethodInfo() {
            throw new NotImplementedException();
        }

        public virtual float GetPauseDuration() => 0;

        public virtual T GetValue<T>() {
            throw new NotImplementedException();
        }

        public virtual T GetValue<T, TArg>(TArg eventArg) {
            throw new NotImplementedException();
        }

        public virtual void Invoke() {
            throw new NotImplementedException();
        }

        public virtual void Invoke<TArg>(TArg eventArg) {
            Debug.Log(GetType());
            throw new NotImplementedException();
        }

        public virtual void StopCoroutine() {
        }

        protected virtual Type[] GetArgumentTypes() {
            throw new NotImplementedException();
        }

        protected virtual CachedData GetCachedArgument(Argument argument) {
            throw new NotImplementedException();
        }

        protected CachedData<T> GetCachedData<T>() {
            if (this is CachedData<T> cast) return cast;
            return new Caster<T>(this);
        }

        protected virtual CachedData GetInstance() {
            throw new NotImplementedException();
        }

        protected virtual void SetArguments(CachedData[] arguments) {
            throw new NotImplementedException();
        }

        protected virtual void SetMethod(MethodInfo method) {
        }

        /// <summary>Method used to retrieve Referenced data when runtime data setup is finalized</summary>
        protected virtual void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
            throw new NotImplementedException();
        }

        private CachedData GetNew(CachedData parent, CachedData[] arguments, int enumeratorIndex, CachedData<float> delayData) {
            CachedData newInstance = GetInstance();
            if (enumeratorIndex >= 0) {
                newInstance = MakeEnumeratorMethod(newInstance, enumeratorIndex);
            }
            if (delayData != null) {
                newInstance = new DelayedMethod(newInstance, parent, delayData);
            }
            else if (ReturnType == typeof(IEnumerator)) {
                newInstance = new CoroutineStarter(newInstance, parent);
            }

            newInstance.SetArguments(arguments);

            return newInstance;
        }

        private CachedData GetNew(CachedData[] arguments, bool negateBool, bool cacheReturnValue) {
            CachedData newInstance = GetInstance();
            newInstance.SetArguments(arguments);

            if (negateBool) newInstance = new CachedBoolNegator(newInstance);

            if (cacheReturnValue) newInstance = GetCachedReturnValue(newInstance);

            return newInstance;
        }

        private CachedData MakeEnumeratorMethod(CachedData instance, int enumeratorIndex) {
            Type enumeratorType = GetArgumentTypes()[enumeratorIndex];
            Type foreachInvokerType;
            if (ReturnType == typeof(void)) foreachInvokerType = typeof(ForeachInvoker<>).MakeGenericType(enumeratorType);
            else foreachInvokerType = typeof(ForeachInvoker<,>).MakeGenericType(enumeratorType, ReturnType);
            ConstructorInfo fi = foreachInvokerType.GetConstructor(new Type[] { typeof(CachedData), typeof(int) });
            return fi.Invoke(new object[] { instance, enumeratorIndex }) as CachedData;
        }

        protected abstract class CachedActionBase<TDelegate> : CachedMethod<TDelegate, Void> where TDelegate : Delegate {
            public override Type ReturnType => typeof(void);

            public override Void GetValue() {
                Invoke();
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} has no return value");
#endif
                return default;
            }

            public override T GetValue<T>() {
                Invoke();
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} has no return value");
#endif
                return default;
            }

            public override Void GetValue<TArg>(TArg eventArg) {
                Invoke(eventArg);
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} has no return value");
#endif
                return default;
            }

            public override T GetValue<T, TArg>(TArg eventArg) {
                Invoke(eventArg);
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} has no return value");
#endif
                return default;
            }
        }

        protected abstract class CachedFuncBase<TDelegate, TResult> : CachedMethod<TDelegate, TResult> where TDelegate : Delegate {
            public override Type ReturnType => typeof(TResult);

            public override T GetValue<T>() {
                return TypeCaster<TResult, T>.Cast(GetValue());
            }

            public override T GetValue<T, TArg>(TArg eventArg) {
                return TypeCaster<TResult, T>.Cast(GetValue(eventArg));
            }

            public override void Invoke() => GetValue();

            public override void Invoke<TArg>(TArg eventArg) => GetValue(eventArg);
        }

        protected class CoroutineStarter : CoroutineWrapper {
            public CoroutineStarter(CachedData method, CachedData parentCaster) : base(method, parentCaster) {
            }

            public override void Invoke() {
                _coroutine = StartCoroutine(_method.GetValue<IEnumerator>());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                _coroutine = StartCoroutine(_method.GetValue<IEnumerator, TArg>(eventArg));
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
        }

        protected abstract class CoroutineWrapper : MethodWrapper {
            #region Fields

            protected Coroutine _coroutine;

            protected CachedData _parentReference;

            #endregion

            protected CoroutineWrapper(CachedData method, CachedData parentCaster) : base(method) {
                _parentReference = parentCaster;
            }

            public override void StopCoroutine() {
                if (_coroutine != null) _parentReference.GetValue<MonoBehaviour>().StopCoroutine(_coroutine);
            }

            protected Coroutine StartCoroutine(IEnumerator routine) {
                return _parentReference.GetValue<MonoBehaviour>().StartCoroutine(routine);
            }
        }

        protected class DelayedMethod : CoroutineWrapper {
            #region Fields

            private CachedData<float> _delayData;

            private bool _methodIsCoroutine;

            #endregion

            public DelayedMethod(CachedData method, CachedData parentCaster, CachedData<float> delayData) : base(method, parentCaster) {
                _delayData = delayData;

                _methodIsCoroutine = method.ReturnType == typeof(IEnumerator);
            }

            public override void Invoke() {
                _coroutine = StartCoroutine(DelayedInvoke());
            }

            protected virtual IEnumerator DelayedInvoke() {
                float delay = _delayData.GetValue<float>();
                if (delay <= 0) yield break;
                if (Time.inFixedTimeStep) {
                    float targetTime = Time.time + delay;
                    while (Time.time < targetTime) yield return new WaitForFixedUpdate();
                }
                else yield return new WaitForSeconds(delay);
                if (_methodIsCoroutine) yield return _method.GetValue<IEnumerator>();
                else _method.Invoke();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                _method.UpdateReferences(runtimeReferences);
                UpdateReference(ref _delayData, runtimeReferences);
            }
        }

        protected abstract class MethodWrapper : CachedData {
            #region Fields

            protected CachedData _method;

            #endregion

            protected MethodWrapper(CachedData method) {
                _method = method;
            }

            public override Type ReturnType => _method.ReturnType;

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override T GetValue<T>() => _method.GetValue<T>();

            public override void StopCoroutine() {
                _method.StopCoroutine();
            }

            protected override void SetArguments(CachedData[] arguments) => _method.SetArguments(arguments);
        }

        protected abstract class MethodWrapper<TValue> : CachedData<TValue> {
            #region Fields

            protected CachedData _method;

            #endregion

            protected MethodWrapper(CachedData method) {
                _method = method;
            }

            public override Type ReturnType => _method.ReturnType;

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override T GetValue<T>() => TypeCaster<TValue, T>.Cast(GetValue());

            public override void StopCoroutine() {
                _method.StopCoroutine();
            }

            protected override void SetArguments(CachedData[] arguments) => _method.SetArguments(arguments);
        }

        private class CachedAction : CachedActionBase<Action> {
            public override void Invoke() => method();

            public override void Invoke<TArg>(TArg eventArg) => method();

            protected override CachedMethod<Action, Void> CreateInstance() => new CachedAction();

            protected override void SetArguments(CachedData[] arguments) {
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
            }
        }

        private class CachedAction<T> : CachedActionBase<Action<T>> {
            #region Fields

            private CachedData<T> arg;

            #endregion

            public override void Invoke() {
                T value = arg.GetValue();

                method(
value);
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T value = arg.GetValue(eventArg);

                method(
value);
            }

            protected override CachedMethod<Action<T>, Void> CreateInstance() => new CachedAction<T>();

            protected override void SetArguments(CachedData[] arguments) {
                arg = arguments[0]?.GetCachedData<T>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2> : CachedActionBase<Action<T1, T2>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2>, Void> CreateInstance() => new CachedAction<T1, T2>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3> : CachedActionBase<Action<T1, T2, T3>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3>, Void> CreateInstance() => new CachedAction<T1, T2, T3>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3, T4> : CachedActionBase<Action<T1, T2, T3, T4>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3, T4>, Void> CreateInstance() => new CachedAction<T1, T2, T3, T4>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3, T4, T5> : CachedActionBase<Action<T1, T2, T3, T4, T5>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3, T4, T5>, Void> CreateInstance() => new CachedAction<T1, T2, T3, T4, T5>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3, T4, T5, T6> : CachedActionBase<Action<T1, T2, T3, T4, T5, T6>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3, T4, T5, T6>, Void> CreateInstance() => new CachedAction<T1, T2, T3, T4, T5, T6>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3, T4, T5, T6, T7> : CachedActionBase<Action<T1, T2, T3, T4, T5, T6, T7>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue(),
arg7.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3, T4, T5, T6, T7>, Void> CreateInstance() => new CachedAction<T1, T2, T3, T4, T5, T6, T7>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
            }
        }

        private class CachedAction<T1, T2, T3, T4, T5, T6, T7, T8> : CachedActionBase<Action<T1, T2, T3, T4, T5, T6, T7, T8>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            private CachedData<T8> arg8;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue(),
arg7.GetValue(),
arg8.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg),
arg8.GetValue(eventArg));
            }

            protected override CachedMethod<Action<T1, T2, T3, T4, T5, T6, T7, T8>, Void> CreateInstance() => new CachedAction<T1, T2, T3, T4, T5, T6, T7, T8>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
                arg8 = arguments[7]?.GetCachedData<T8>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
                UpdateReference(ref arg8, runtimeReferences);
            }
        }

        private class CachedBoolNegator : MethodWrapper<bool> {
            public CachedBoolNegator(CachedData method) : base(method) {
            }

            public override bool GetValue() => !_method.GetValue<bool>();

            public override void Invoke() => _method.Invoke();

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
        }

        private class CachedDataID<T> : CachedData<T>, ICachedDataID {
            public CachedDataID(int id) {
                this.id = id;
            }

            public int id { get; }
        }

        private class CachedFunc<TResult> : CachedFuncBase<Func<TResult>, TResult> {
            public override TResult GetValue() => method();

            public override TResult GetValue<TArg>(TArg eventArg) => method();

            protected override CachedMethod<Func<TResult>, TResult> CreateInstance() => new CachedFunc<TResult>();

            protected override void SetArguments(CachedData[] arguments) {
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
            }
        }

        private class CachedFunc<T, TResult> : CachedFuncBase<Func<T, TResult>, TResult> {
            #region Fields

            private CachedData<T> arg;

            #endregion

            public override TResult GetValue() {
                T value = arg.GetValue();

                return method(
value);
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T value = arg.GetValue(eventArg);

                return method(
value);
            }

            protected override CachedMethod<Func<T, TResult>, TResult> CreateInstance() => new CachedFunc<T, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg = arguments[0]?.GetCachedData<T>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, TResult> : CachedFuncBase<Func<T1, T2, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, TResult> : CachedFuncBase<Func<T1, T2, T3, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, T4, TResult> : CachedFuncBase<Func<T1, T2, T3, T4, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, T4, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, T4, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, T4, T5, TResult> : CachedFuncBase<Func<T1, T2, T3, T4, T5, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, T4, T5, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, T4, T5, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, T4, T5, T6, TResult> : CachedFuncBase<Func<T1, T2, T3, T4, T5, T6, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, T4, T5, T6, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, T4, T5, T6, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, T4, T5, T6, T7, TResult> : CachedFuncBase<Func<T1, T2, T3, T4, T5, T6, T7, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue(),
arg7.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, T4, T5, T6, T7, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, T4, T5, T6, T7, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
            }
        }

        private class CachedFunc<T1, T2, T3, T4, T5, T6, T7, T8, TResult> : CachedFuncBase<Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            private CachedData<T8> arg8;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
value1,
arg2.GetValue(),
arg3.GetValue(),
arg4.GetValue(),
arg5.GetValue(),
arg6.GetValue(),
arg7.GetValue(),
arg8.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg),
arg8.GetValue(eventArg));
            }

            protected override CachedMethod<Func<T1, T2, T3, T4, T5, T6, T7, T8, TResult>, TResult> CreateInstance() => new CachedFunc<T1, T2, T3, T4, T5, T6, T7, T8, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
                arg8 = arguments[7]?.GetCachedData<T8>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
                UpdateReference(ref arg8, runtimeReferences);
            }
        }

        private class CachedRefAction<T> : CachedActionBase<RefActionDelegate<T>> {
            #region Fields

            private CachedData<T> arg;

            #endregion

            public override void Invoke() {
                T value = arg.GetValue();

                method(
                ref value);
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T value = arg.GetValue(eventArg);

                method(
                ref value);
            }

            protected override CachedMethod<RefActionDelegate<T>, Void> CreateInstance() => new CachedRefAction<T>();

            protected override void SetArguments(CachedData[] arguments) {
                arg = arguments[0]?.GetCachedData<T>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2> : CachedActionBase<RefActionDelegate<T1, T2>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2>, Void> CreateInstance() => new CachedRefAction<T1, T2>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3> : CachedActionBase<RefActionDelegate<T1, T2, T3>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3, T4> : CachedActionBase<RefActionDelegate<T1, T2, T3, T4>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg),
                arg4.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3, T4>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3, T4>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3, T4, T5> : CachedActionBase<RefActionDelegate<T1, T2, T3, T4, T5>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg),
                arg4.GetValue(eventArg),
                arg5.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3, T4, T5>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3, T4, T5>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3, T4, T5, T6> : CachedActionBase<RefActionDelegate<T1, T2, T3, T4, T5, T6>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg),
                arg4.GetValue(eventArg),
                arg5.GetValue(eventArg),
                arg6.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3, T4, T5, T6>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3, T4, T5, T6>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3, T4, T5, T6, T7> : CachedActionBase<RefActionDelegate<T1, T2, T3, T4, T5, T6, T7>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue(),
                arg7.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg),
                arg4.GetValue(eventArg),
                arg5.GetValue(eventArg),
                arg6.GetValue(eventArg),
                arg7.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3, T4, T5, T6, T7>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3, T4, T5, T6, T7>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
            }
        }

        private class CachedRefAction<T1, T2, T3, T4, T5, T6, T7, T8> : CachedActionBase<RefActionDelegate<T1, T2, T3, T4, T5, T6, T7, T8>> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            private CachedData<T8> arg8;

            #endregion

            public override void Invoke() {
                T1 value1 = arg1.GetValue();

                method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue(),
                arg7.GetValue(),
                arg8.GetValue());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                method(
                ref value1,
                arg2.GetValue(eventArg),
                arg3.GetValue(eventArg),
                arg4.GetValue(eventArg),
                arg5.GetValue(eventArg),
                arg6.GetValue(eventArg),
                arg7.GetValue(eventArg),
                arg8.GetValue(eventArg));
            }

            protected override CachedMethod<RefActionDelegate<T1, T2, T3, T4, T5, T6, T7, T8>, Void> CreateInstance() => new CachedRefAction<T1, T2, T3, T4, T5, T6, T7, T8>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
                arg8 = arguments[7]?.GetCachedData<T8>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
                UpdateReference(ref arg8, runtimeReferences);
            }
        }

        private class CachedRefFunc<T, TResult> : CachedFuncBase<RefFuncDelegate<T, TResult>, TResult> {
            #region Fields

            private CachedData<T> arg;

            #endregion

            public override TResult GetValue() {
                T value = arg.GetValue();

                return method(
                ref value);
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T value = arg.GetValue(eventArg);

                return method(
                ref value);
            }

            protected override CachedMethod<RefFuncDelegate<T, TResult>, TResult> CreateInstance() => new CachedRefFunc<T, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg = arguments[0]?.GetCachedData<T>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, T4, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, T4, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, T4, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, T4, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, T4, T5, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, T4, T5, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, T4, T5, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, T4, T5, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, T4, T5, T6, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, T4, T5, T6, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, T4, T5, T6, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, T4, T5, T6, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, T4, T5, T6, T7, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, T4, T5, T6, T7, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue(),
                arg7.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, T4, T5, T6, T7, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, T4, T5, T6, T7, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
            }
        }

        private class CachedRefFunc<T1, T2, T3, T4, T5, T6, T7, T8, TResult> : CachedFuncBase<RefFuncDelegate<T1, T2, T3, T4, T5, T6, T7, T8, TResult>, TResult> {
            #region Fields

            private CachedData<T1> arg1;

            private CachedData<T2> arg2;

            private CachedData<T3> arg3;

            private CachedData<T4> arg4;

            private CachedData<T5> arg5;

            private CachedData<T6> arg6;

            private CachedData<T7> arg7;

            private CachedData<T8> arg8;

            #endregion

            public override TResult GetValue() {
                T1 value1 = arg1.GetValue();

                return method(
                ref value1,
                arg2.GetValue(),
                arg3.GetValue(),
                arg4.GetValue(),
                arg5.GetValue(),
                arg6.GetValue(),
                arg7.GetValue(),
                arg8.GetValue());
            }

            public override TResult GetValue<TArg>(TArg eventArg) {
                T1 value1 = arg1.GetValue(eventArg);

                return method(
                ref value1,
arg2.GetValue(eventArg),
arg3.GetValue(eventArg),
arg4.GetValue(eventArg),
arg5.GetValue(eventArg),
arg6.GetValue(eventArg),
arg7.GetValue(eventArg),
arg8.GetValue(eventArg));
            }

            protected override CachedMethod<RefFuncDelegate<T1, T2, T3, T4, T5, T6, T7, T8, TResult>, TResult> CreateInstance() => new CachedRefFunc<T1, T2, T3, T4, T5, T6, T7, T8, TResult>();

            protected override void SetArguments(CachedData[] arguments) {
                arg1 = arguments[0]?.GetCachedData<T1>();
                arg2 = arguments[1]?.GetCachedData<T2>();
                arg3 = arguments[2]?.GetCachedData<T3>();
                arg4 = arguments[3]?.GetCachedData<T4>();
                arg5 = arguments[4]?.GetCachedData<T5>();
                arg6 = arguments[5]?.GetCachedData<T6>();
                arg7 = arguments[6]?.GetCachedData<T7>();
                arg8 = arguments[7]?.GetCachedData<T8>();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                UpdateReference(ref arg1, runtimeReferences);
                UpdateReference(ref arg2, runtimeReferences);
                UpdateReference(ref arg3, runtimeReferences);
                UpdateReference(ref arg4, runtimeReferences);
                UpdateReference(ref arg5, runtimeReferences);
                UpdateReference(ref arg6, runtimeReferences);
                UpdateReference(ref arg7, runtimeReferences);
                UpdateReference(ref arg8, runtimeReferences);
            }
        }

        private class CachedReturnValue<TValue> : MethodWrapper<TValue> {
            #region Fields

            private TValue _value;

            private bool _needCache = true;

            #endregion

            public CachedReturnValue(CachedData method) : base(method) {
            }

            public override TValue GetValue() {
                if (_needCache) {
                    _value = _method.GetValue<TValue>();
                    _needCache = false;
                }
                return _value;
            }

            public override TValue GetValue<TArg>(TArg eventArg) {
                if (_needCache) {
                    _value = _method.GetValue<TValue, TArg>(eventArg);
                    _needCache = false;
                }
                return _value;
            }

            public override T GetValue<T, TArg>(TArg eventArg) {
                if (_needCache) {
                    _value = _method.GetValue<TValue, TArg>(eventArg);
                    _needCache = false;
                }
                return TypeCaster<TValue, T>.Cast(_value);
            }

            public override void Invoke() => _method.Invoke();

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
        }

        private class Caster<T> : CachedData<T> {
            #region Fields

            private CachedData _method;

            #endregion

            public Caster(CachedData method) {
                _method = method;
            }

            public CachedData method => _method;

            public override T GetValue() => _method.GetValue<T>();

            public override T GetValue<TArg>(TArg eventArg) => _method.GetValue<T, TArg>(eventArg);

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                _method.UpdateReferences(runtimeReferences);
            }
        }

        private class EmptyCall : CachedData {
            public override Type ReturnType => typeof(void);

            public override MethodInfo GetMethodInfo() => ((Action)Invoke).Method;

            public override T GetValue<T>() {
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} doesn't return value of type {typeof(T)}");
#endif
                return default;
            }

            public override void Invoke() {
            }

            public override void Invoke<TArg>(TArg eventArg) {
            }
        }

        private class EventArgReference<T> : CachedData<T> {
            public static EventArgReference<T> Instance { get; } = new EventArgReference<T>();

            public override Type ReturnType => typeof(T);

            public override T GetValue() {
#if UNITY_EDITOR
                Debug.LogError("No custom argument was provided");
#endif
                return default;
            }

            public override T GetValue<TArg>(TArg eventArg) => TypeCaster<TArg, T>.Cast(eventArg);

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
            }
        }

        private class ForeachInvoker<TElement> : MethodWrapper {
            #region Fields

            protected int enumeratorIndex;

            //Since CachedArgument<T> doesn't implement IData interface we need to create a special type for the substituted argument
            protected ArgumentSubstitute substitutedArgument = new ArgumentSubstitute();

            protected CachedData foreachArgument;

            #endregion

            public ForeachInvoker(CachedData method, int enumeratorIndex) : base(method) {
                this.enumeratorIndex = enumeratorIndex;
            }

            public override void Invoke() {
                foreach (var value in foreachArgument.GetValue<IEnumerable<TElement>>()) {
                    substitutedArgument.SetValue(value);
                    _method.Invoke();
                }
            }

            public override void Invoke<TArg>(TArg eventArg) {
                try {
                    foreach (var value in foreachArgument.GetValue<IEnumerable<TElement>, TArg>(eventArg)) {
                        substitutedArgument.SetValue(value);
                        _method.Invoke(eventArg);
                    }
                }
                catch (Exception e) {
                    Debug.Log(e);
                }
            }

            protected override void SetArguments(CachedData[] arguments) {
                foreachArgument = arguments[enumeratorIndex];

                CachedData[] substitutedArguments = new CachedData[arguments.Length];
                for (int i = 0; i < arguments.Length; i++) {
                    if (i != enumeratorIndex) substitutedArguments[i] = arguments[i];
                    else substitutedArguments[i] = substitutedArgument;
                }

                _method.SetArguments(substitutedArguments);
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                _method.UpdateReferences(runtimeReferences);
                UpdateReference(ref foreachArgument, runtimeReferences);
            }

            /// <summary>Argument of a foreach iterator that's get's sunstitued when it goes through a collection</summary>
            protected class ArgumentSubstitute : CachedData {
                #region Fields

                private TElement arg;

                #endregion

                public override T GetValue<T, TArg>(TArg eventArg) => TypeCaster<TElement, T>.Cast(arg);

                public void SetValue(TElement value) => this.arg = value;
            }
        }

        private class ForeachInvoker<TElement, TResult> : ForeachInvoker<TElement> {
            public ForeachInvoker(CachedData cachedMethod, int enumeratorIndex) : base(cachedMethod, enumeratorIndex) {
            }

            public override Type ReturnType => typeof(IEnumerable<TResult>);

            public override T GetValue<T>() {
                if (GetValueEnumerator() is T cast) return cast;
                return base.GetValue<T>();
            }

            private IEnumerable<TResult> GetValueEnumerator() {
                foreach (var value in foreachArgument.GetValue<IEnumerable<TElement>>()) {
                    substitutedArgument.SetValue(value);
                    yield return _method.GetValue<TResult>();
                }
            }
        }

        private class PauseCall : CachedData {
            #region Fields

            private CachedData _method;

            private CachedData _pauseData;

            #endregion

            public PauseCall(CachedData method, CachedData pauseData) {
                _method = method;
                _pauseData = pauseData;
            }

            public override Type ReturnType => _method.ReturnType;

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override float GetPauseDuration() => _pauseData.GetValue<float>();

            public override T GetValue<T>() => _method.GetValue<T>();

            public override void Invoke() => _method.Invoke();

            public override void Invoke<TArg>(TArg eventArg) => _method.Invoke(eventArg);

            public override void StopCoroutine() => _method.StopCoroutine();
        }
    }
}
