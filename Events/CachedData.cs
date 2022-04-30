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

        public abstract Type ReturnType { get; }

        protected virtual bool isCachedArgument => false;

        private interface ICachedDataID {
            CachedData GetEndData();
        }

        public static Type GetCachedDataType(string methodName) => GetActivator(methodName)?.GetType();

        public static MethodInfo GetMethodInfo(string methodName) => GetActivator(methodName)?.GetMethodInfo();

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
                    runtimeCalls[i] = new PauseCall(runtimeCalls[i], GetPauseDelayData(call, runtimeReferences));
                }
            }

            foreach (var data in runtimeReferences.Values) {
                data?.UpdateReferences(runtimeReferences);
            }
        }

        protected static CachedData<TDesired> GetCachedDataMethod<TCachedData, T, TDesired>(TCachedData data) where TCachedData : CachedData<T> {
            if (data is CachedData<TDesired> cast) return cast;

            if (!CasterGetter<T, TDesired>.IsCastable && CasterGetter<TCachedData, TDesired>.IsCastable) {
                return new CachedArgument<TCachedData>(data).GetCachedData<TDesired>();
            }
            return null;
        }

        protected static CachedData<TDesired> GetCachedDataMethod<TCachedData, T, TDesired>(TCachedData data, CachedData underlyingData) where TCachedData : CachedData<T> {
            CachedData<TDesired> result = GetCachedDataMethod<TCachedData, T, TDesired>(data);
            if (result == null) result = underlyingData.GetCachedDataUnderlying<TDesired>();
            return result;
        }

        protected static CachedData MakeCachedArgument(object value, Type desiredType) {
            //if we pass an object that can't be used directly (need to be cast) then we create an argument of object's type
            desiredType = value?.GetType() ?? desiredType;
            Type cachedArgumentType = typeof(CachedArgument<>).MakeGenericType(desiredType);
            ConstructorInfo ci = cachedArgumentType.GetConstructor(new Type[] { desiredType });
            return ci.Invoke(new object[] { value }) as CachedData;
        }

        protected static void UpdateReference(ref CachedData arg) {
            if (arg is ICachedDataID idReference) {
                arg = idReference.GetEndData();
            }
        }

        protected static void UpdateReference<T>(ref CachedData<T> arg) {
            if (arg is CachedDataID<T> idReference) {
                arg = idReference.GetEndData();
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

        private static CachedData GetCachedDataID(int id, Type parameterType, Dictionary<int, CachedData> runtimeReferences) {
            Type specific = typeof(CachedDataID<>).MakeGenericType(parameterType);
            ConstructorInfo ci = specific.GetConstructor(new Type[] { typeof(int), typeof(Dictionary<int, CachedData>) });
            return ci.Invoke(new object[] { id, runtimeReferences }) as CachedData;
        }

        private static CachedData GetCachedReturnValue(CachedData method) {
            if (method.ReturnType == typeof(void)) {
#if UNITY_EDITOR
                Debug.LogError($"{method.GetMethodInfo()} doesn't return value and can't be cached");
#endif
                return method;
            }
            Type specific = typeof(CachedReturnValue<>).MakeGenericType(method.ReturnType);
            ConstructorInfo ci = specific.GetConstructor(new Type[] { typeof(CachedData) });
            return ci.Invoke(new object[] { method }) as CachedData;
        }

        private static CachedData GetData(Argument argument, Type parameterType, Dictionary<int, CachedData> runtimeReferences) {
            return GetCachedDataID(argument.GetIntArgument(), parameterType, runtimeReferences);
        }

        private static CachedData GetData<TTag>(Argument argument, Type parameterType, EventCall<TTag>[] orderedCalls, Dictionary<int, CachedData> runtimeReferences) {
            object tag = argument.GetValue(typeof(TTag));
            if (tag == null) {
                return null;
            }
            IEquatable<TTag> tagE = tag as IEquatable<TTag>;
            for (int i = 0; i < orderedCalls.Length; i++) {
                EventCall<TTag> call = orderedCalls[i];
                if (tagE?.Equals(call.tag) ?? tag.Equals(call.tag)) {
                    return GetCachedDataID(call.id, parameterType, runtimeReferences);
                }
            }
            return null;
        }

        private static CachedData GetEndData<TTag>(Argument argument, Type parameterType, CachedData parent, EventCall<TTag>[] orderedCalls, Dictionary<int, CachedData> runtimeReferences) {
            switch (argument.GetArgType()) {
                default:
                    return null;
                case Argument.ArgType.Data:
                    return MakeCachedArgument(argument.GetValue(parameterType), parameterType);
                case Argument.ArgType.Parent:
                    return parent;
                case Argument.ArgType.IDReference:
                    return GetData(argument, parameterType, runtimeReferences);
                case Argument.ArgType.TagReference:
                    return GetData(argument, parameterType, orderedCalls, runtimeReferences);
                case Argument.ArgType.Method:
                    return GetMethodData(argument, parent, orderedCalls, runtimeReferences);
                case Argument.ArgType.CustomEventArg:
                    return GetEventArgReference(parameterType);
            }
        }

        private static CachedData GetEventArgReference(Type parameterType) {
            Type specific = typeof(EventArgReference<>).MakeGenericType(parameterType);
            PropertyInfo property = specific.GetProperty(nameof(EventArgReference<object>.Instance), BindingFlags.Public | BindingFlags.Static);
            return (CachedData)property.GetValue(null);
        }

        private static CachedData GetMethodData<TTag>(Argument argument, CachedData parent, EventCall<TTag>[] orderedCalls, Dictionary<int, CachedData> runtimeReferences) {
            CachedData activator = GetActivator(argument.GetStringArgument());
            if (activator == null) {
                return null;
            }

            if (!activator.isCachedArgument) {
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
                            ArgumentArrayBuilder[i] = GetData(argument, parameterType, runtimeReferences);
                            break;
                        case Argument.ArgType.TagReference:
                            ArgumentArrayBuilder[i] = GetData(argument, parameterType, orderedCalls, runtimeReferences);
                            break;
                        case Argument.ArgType.CustomEventArg:
                            ArgumentArrayBuilder[i] = GetEventArgReference(parameterType);
                            break;
                    }
                }

                return activator.GetNew(ArgumentArrayBuilder, argument.negateBool, argument.cacheReturnValue);
            }
            else {
                Type parameterType = activator.ReturnType;

                if (argument.GetFuncArgType(0) == Argument.ArgType.Data) {
                    return MakeCachedArgument(argument.GetValue(parameterType), parameterType);
                }
                else {
                    return null;
                }
            }
        }

        private static CachedData<float> GetPauseDelayData(EventCall call, Dictionary<int, CachedData> runtimeReferences) {
            if (call.delayID != 0) {
                if (runtimeReferences.TryGetValue(call.delayID, out CachedData data)) {
                    return data.GetCachedData<float>();
                }
                return null;
            }
            return new CachedArgument<float>(call.delayValue);
        }

        private static CachedData GetRuntimeData<TTag>(EventCall call, CachedData parentReference, EventCall<TTag>[] orderedCalls, Dictionary<int, CachedData> runtimeReferences) {
            //first we find the method
            CachedData activator = GetActivator(call.methodName);

            if (activator == null) {
                return null;
            }

            if (!activator.isCachedArgument) {
                //setup each argument
                Type[] parameterTypes = activator.GetArgumentTypes();

                if (call.arguments.Length != parameterTypes.Length) {
                    return null;
                }

                for (int i = 0; i < call.arguments.Length; i++) {
                    CachedData argData = GetEndData(call.arguments[i], parameterTypes[i], parentReference, orderedCalls, runtimeReferences);
                    if (call.arguments[i].isReferencable) {
                        runtimeReferences.Add(GetArgumentID(call.id, i), argData);
                    }
                    CallArrayBuilder[i] = argData;
                }

                CachedData callData = activator.GetNew(parentReference, CallArrayBuilder, call, GetWaitDelayData(call, runtimeReferences));
                runtimeReferences.Add(call.id, callData);
                return callData;
            }
            else {
                if (call.arguments.Length > 0 && call.arguments[0].isReferencable) {
                    CachedData argumentData = GetEndData(call.arguments[0], activator.ReturnType, parentReference, orderedCalls, runtimeReferences);
                    runtimeReferences.Add(call.id, argumentData);
                    return argumentData;
                }
                else {
                    return null;
                }
            }
        }

        private static CachedData<float> GetWaitDelayData(EventCall call, Dictionary<int, CachedData> runtimeReferences) {
            if (call.delayMode != DelayMode.Wait) return null;
            if (call.delayID == 0) {
                return new CachedArgument<float>(call.delayValue);
            }
            return new CachedDataID<float>(call.delayID, runtimeReferences);
        }

        private static CachedData MakeCachedData(MethodInfo method) {
            bool methodIsStatic = method.IsStatic;
            Type returnType = method.ReturnType;
            bool methodIsFunc = returnType != typeof(void);
            bool instanceIsStruct = !methodIsStatic && method.DeclaringType.IsValueType;

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

        public abstract MethodInfo GetMethodInfo();

        public virtual float GetPauseDuration() => 0;

        public virtual float GetPauseDuration<TArg>(TArg eventArg) => 0;

        public abstract T GetValue<T>();

        public abstract T GetValue<TArg, T>(TArg eventArg);

        public abstract void Invoke();

        public abstract void Invoke<TArg>(TArg eventArg);

        public virtual void SetValue<T>(T value) {
        }

        public virtual void StopCoroutine() {
        }

        protected virtual Type[] GetArgumentTypes() {
            throw new NotImplementedException();
        }

        protected virtual CachedData GetCachedArgument(Argument argument) {
            throw new NotImplementedException();
        }

        protected abstract CachedData<TDesired> GetCachedData<TDesired>();

        protected abstract CachedData<TDesired> GetCachedDataUnderlying<TDesired>();

        protected virtual CachedData GetInstance() {
            throw new NotImplementedException();
        }

        protected abstract void SetArguments(CachedData[] arguments);

        protected virtual void SetMethod(MethodInfo method) {
        }

        /// <summary>Method used to retrieve Referenced data when runtime data setup is finalized</summary>
        protected abstract void UpdateReferences(Dictionary<int, CachedData> runtimeReferences);

        private CachedData GetNew(CachedData parent, CachedData[] arguments, EventCall call, CachedData<float> delayData) {
            CachedData newInstance = GetInstance();

            if (delayData != null) {
                newInstance = new DelayedMethod(newInstance, parent, delayData);
            }
            else if (ReturnType == typeof(IEnumerator)) {
                newInstance = new CoroutineStarter(newInstance, parent);
            }

            newInstance.SetArguments(arguments);

            if (call.negateBool) newInstance = new CachedBoolNegator(newInstance);

            if (call.cacheReturnValue) newInstance = GetCachedReturnValue(newInstance);

            return newInstance;
        }

        private CachedData GetNew(CachedData[] arguments, bool negateBool, bool cacheReturnValue) {
            CachedData newInstance = GetInstance();
            newInstance.SetArguments(arguments);

            if (negateBool) newInstance = new CachedBoolNegator(newInstance);

            if (cacheReturnValue) newInstance = GetCachedReturnValue(newInstance);

            return newInstance;
        }

        protected abstract class CachedActionBase<TDelegate> : CachedMethod<TDelegate, Void>, ICachedMethod<TDelegate> where TDelegate : Delegate {
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

            public override T GetValue<TArg, T>(TArg eventArg) {
                Invoke(eventArg);
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} has no return value");
#endif
                return default;
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CachedActionBase<TDelegate>, Void, TDesired>(this);
        }

        protected abstract class CachedFuncBase<TDelegate, TResult> : CachedMethod<TDelegate, TResult>, ICachedMethod<TDelegate>, IValueReturner<TResult> where TDelegate : Delegate {
            public override Type ReturnType => typeof(TResult);

            public override T GetValue<T>() {
                return TypeCaster<TResult, T>.Cast(GetValue());
            }

            public override T GetValue<TArg, T>(TArg eventArg) {
                return TypeCaster<TResult, T>.Cast(GetValue(eventArg));
            }

            public override void Invoke() => GetValue();

            public override void Invoke<TArg>(TArg eventArg) => GetValue(eventArg);

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CachedFuncBase<TDelegate, TResult>, TResult, TDesired>(this);
        }

        /// <summary>
        /// Base class for any method in <see cref="ExtendedEvent"/>.
        /// </summary>
        protected abstract class CachedMethod<TDelegate, TResult> : CachedData<TResult> where TDelegate : Delegate {
            #region Fields

            protected TDelegate method;

            private static Type[] ParameterTypes = ExtendedEvent.MakeParameterTypeArray(typeof(TDelegate));

            #endregion

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

        protected class Caster<T> : CachedData<T> {
            #region Fields

            private CachedData _method;

            #endregion

            public Caster(CachedData method) {
                _method = method;
            }

            public override Type ReturnType => throw new NotImplementedException();

            public override MethodInfo GetMethodInfo() {
                throw new NotImplementedException();
            }

            public override T GetValue() => _method.GetValue<T>();

            public override T1 GetValue<T1>() {
                throw new NotImplementedException();
            }

            public override T GetValue<TArg>(TArg eventArg) => _method.GetValue<TArg, T>(eventArg);

            public override T1 GetValue<TArg, T1>(TArg eventArg) {
                throw new NotImplementedException();
            }

            public override void Invoke() {
                throw new NotImplementedException();
            }

            public override void Invoke<TArg>(TArg eventArg) {
                throw new NotImplementedException();
            }

            protected override CachedData<T1> GetCachedDataUnderlying<T1>() {
                throw new NotImplementedException();
            }

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                _method.UpdateReferences(runtimeReferences);
            }
        }

        protected static class CasterGetter<T, TResult> {
            #region Fields

            public static bool IsCastable;

            public static Func<CachedData<T>, CachedData<TResult>> GetCaster;

            #endregion

            static CasterGetter() {
                IsCastable = GetIsCastableBool();
                try {
                    Type directCasterType = typeof(DirectCaster<,>).MakeGenericType(typeof(T), typeof(TResult));
                    MethodInfo getNewMethod = directCasterType.GetMethod("GetNew", BindingFlags.Static | BindingFlags.Public);
                    GetCaster = (Func<CachedData<T>, CachedData<TResult>>)getNewMethod.CreateDelegate(typeof(Func<CachedData<T>, CachedData<TResult>>));

                }
                catch {
                    GetCaster = (o) => new Caster<TResult>(o);
                }
            }

            /// <summary>Determines whether an instance of a specified type c can be casted to a variable of the current type using TypeCaster.</summary>
            private static bool GetIsCastableBool() {
                //Certain types can't be used as generic arguments of a generic type (pointers for example). Rather than overly complicating the code we just use try-catch
                Type caster;
                try {
                    caster = typeof(TypeCaster<,>).MakeGenericType(new Type[] { typeof(T), typeof(TResult) });
                }
                catch {
                    return false;
                }
                try {
                    return (bool)caster.GetProperty("IsValid", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                }
                catch {
                    return false;
                }
            }
        }

        protected class CoroutineStarter : CoroutineWrapper {
            public CoroutineStarter(CachedData method, CachedData parentCaster) : base(method, parentCaster) {
            }

            public override IEnumerator GetValue() => _method.GetValue<IEnumerator>();

            public override IEnumerator GetValue<TArg>(TArg eventArg) => _method.GetValue<TArg, IEnumerator>(eventArg);

            public override void Invoke() {
                _coroutine = StartCoroutine(_method.GetValue<IEnumerator>());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                _coroutine = StartCoroutine(_method.GetValue<TArg, IEnumerator>(eventArg));
            }
        }

        protected abstract class CoroutineWrapper : CachedData<IEnumerator>, IValueReturner<IEnumerator>, ICachedMethod, ICoroutineStarter {
            #region Fields

            protected CachedData _method;

            protected Coroutine _coroutine;

            protected CachedData _parentReference;

            #endregion

            protected CoroutineWrapper(CachedData method, CachedData parentCaster) {
                _method = method;
                _parentReference = parentCaster;
            }

            public override Type ReturnType => typeof(IEnumerator);

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override void StopCoroutine() {
                if (_coroutine != null) _parentReference.GetValue<MonoBehaviour>().StopCoroutine(_coroutine);
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CoroutineWrapper, IEnumerator, TDesired>(this, _method);

            protected override void SetArguments(CachedData[] arguments) => _method.SetArguments(arguments);

            protected Coroutine StartCoroutine(IEnumerator routine) {
                return _parentReference.GetValue<MonoBehaviour>().StartCoroutine(routine);
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
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

            public override IEnumerator GetValue() => DelayedInvoke();

            public override IEnumerator GetValue<TArg>(TArg eventArg) => DelayedInvoke(eventArg);

            public override void Invoke() {
                _coroutine = StartCoroutine(DelayedInvoke());
            }

            public override void Invoke<TArg>(TArg eventArg) {
                _coroutine = StartCoroutine(DelayedInvoke(eventArg));
            }

            protected virtual IEnumerator DelayedInvoke() {
                float delay = _delayData.GetValue<float>();
                if (delay >= 0) {
                    if (Time.inFixedTimeStep) {
                        float targetTime = Time.time + delay;
                        while (Time.time < targetTime) yield return new WaitForFixedUpdate();
                    }
                    else yield return new WaitForSeconds(delay);
                }

                if (_methodIsCoroutine) yield return _method.GetValue<IEnumerator>();
                else _method.Invoke();
            }

            protected virtual IEnumerator DelayedInvoke<TArg>(TArg eventArg) {
                float delay = _delayData.GetValue<TArg, float>(eventArg);
                if (delay >= 0) {
                    if (Time.inFixedTimeStep) {
                        float targetTime = Time.time + delay;
                        while (Time.time < targetTime) yield return new WaitForFixedUpdate();
                    }
                    else yield return new WaitForSeconds(delay);
                }

                if (_methodIsCoroutine) yield return _method.GetValue<TArg, IEnumerator>(eventArg);
                else _method.Invoke();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                _method.UpdateReferences(runtimeReferences);
                UpdateReference(ref _delayData);
            }
        }

        protected class DirectCaster<TDerived, T> : CachedData<T> where TDerived : T {
            #region Fields

            private CachedData<TDerived> _method;

            #endregion

            private DirectCaster(CachedData<TDerived> method) {
                _method = method;
            }

            public override Type ReturnType => throw new NotImplementedException();

            public static DirectCaster<TDerived, T> GetNew(CachedData<TDerived> method) => new DirectCaster<TDerived, T>(method);

            public override MethodInfo GetMethodInfo() {
                throw new NotImplementedException();
            }

            public override T GetValue() => _method.GetValue();

            public override T1 GetValue<T1>() {
                throw new NotImplementedException();
            }

            public override T GetValue<TArg>(TArg eventArg) => _method.GetValue<TArg>(eventArg);

            public override T1 GetValue<TArg, T1>(TArg eventArg) {
                throw new NotImplementedException();
            }

            public override void Invoke() {
                throw new NotImplementedException();
            }

            public override void Invoke<TArg>(TArg eventArg) {
                throw new NotImplementedException();
            }

            protected override CachedData<T1> GetCachedDataUnderlying<T1>() {
                throw new NotImplementedException();
            }

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                throw new NotImplementedException();
            }
        }

        /// <summary>Fake void return type for Action delegates</summary>
        protected class Void {
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
                UpdateReference(ref arg);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
                UpdateReference(ref arg8);
            }
        }

        private class CachedBoolNegator : CachedData<bool>, IValueReturner<bool> {
            #region Fields

            private CachedData _method;

            #endregion

            public CachedBoolNegator(CachedData method) {
                _method = method;
            }

            public override Type ReturnType => typeof(bool);

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override bool GetValue() => !_method.GetValue<bool>();

            public override bool GetValue<TArg>(TArg eventArg) => !_method.GetValue<TArg, bool>(eventArg);

            public override void Invoke() => _method.Invoke();

            public override void Invoke<TArg>(TArg eventArg) => _method.Invoke(eventArg);

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CachedBoolNegator, bool, TDesired>(this, _method);

            protected override void SetArguments(CachedData[] arguments) => _method.SetArguments(arguments);

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
        }

        private class CachedDataID<T> : CachedData<T>, ICachedDataID {
            #region Fields

            private int id;

            private Dictionary<int, CachedData> runtimeReferences;

            #endregion

            public CachedDataID(int id, Dictionary<int, CachedData> runtimeReferences) {
                this.id = id;
                this.runtimeReferences = runtimeReferences;
            }

            public override Type ReturnType => throw new NotImplementedException();

            public CachedData<T> GetEndData() {
                if (runtimeReferences.TryGetValue(id, out CachedData data)) {
                    return data.GetCachedData<T>();
                }
                return null;
            }

            public override MethodInfo GetMethodInfo() {
                throw new NotImplementedException();
            }

            public override T GetValue() {
                throw new NotImplementedException();
            }

            public override T1 GetValue<T1>() {
                throw new NotImplementedException();
            }

            public override T GetValue<TArg>(TArg eventArg) {
                throw new NotImplementedException();
            }

            public override T1 GetValue<TArg, T1>(TArg eventArg) {
                throw new NotImplementedException();
            }

            public override void Invoke() {
                throw new NotImplementedException();
            }

            public override void Invoke<TArg>(TArg eventArg) {
                throw new NotImplementedException();
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => this as CachedData<TDesired>;

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                throw new NotImplementedException();
            }

            CachedData ICachedDataID.GetEndData() {
                if (runtimeReferences.TryGetValue(id, out CachedData data)) {
                    return data;
                }
                return null;
            }
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
                UpdateReference(ref arg);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
                UpdateReference(ref arg8);
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
                UpdateReference(ref arg);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
                UpdateReference(ref arg8);
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
                UpdateReference(ref arg);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
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
                UpdateReference(ref arg1);
                UpdateReference(ref arg2);
                UpdateReference(ref arg3);
                UpdateReference(ref arg4);
                UpdateReference(ref arg5);
                UpdateReference(ref arg6);
                UpdateReference(ref arg7);
                UpdateReference(ref arg8);
            }
        }

        private class CachedReturnValue<TValue> : CachedArgument<TValue> {
            #region Fields

            private CachedData _method;

            private bool _needCache = true;

            #endregion

            public CachedReturnValue(CachedData method) : base(default) {
                _method = method;
            }

            public override TValue GetValue() {
                if (_needCache) {
                    value = _method.GetValue<TValue>();
                    _needCache = false;
                }
                return value;
            }

            public override TValue GetValue<TArg>(TArg eventArg) {
                if (_needCache) {
                    value = _method.GetValue<TArg, TValue>(eventArg);
                    _needCache = false;
                }
                return value;
            }

            public override T GetValue<TArg, T>(TArg eventArg) {
                if (_needCache) {
                    value = _method.GetValue<TArg, TValue>(eventArg);
                    _needCache = false;
                }
                return TypeCaster<TValue, T>.Cast(value);
            }

            public override void Invoke() => GetValue();

            public override void Invoke<TArg>(TArg eventArg) => GetValue(eventArg);

            public override void SetValue(TValue value) {
                _needCache = false;
                base.SetValue(value);
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => GetCachedDataMethod<CachedReturnValue<TValue>, TValue, TDesired>(this, _method);

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) => _method.UpdateReferences(runtimeReferences);
        }

        private class EmptyCall : CachedData<Void> {
            public override Type ReturnType => typeof(void);

            public override MethodInfo GetMethodInfo() => ((Action)Invoke).Method;

            public override T GetValue<T>() {
#if UNITY_EDITOR
                Debug.LogError($"Method {GetMethodInfo()} doesn't return value of type {typeof(T)}");
#endif
                return default;
            }

            public override Void GetValue() => default;

            public override T GetValue<TArg, T>(TArg eventArg) => GetValue<T>();

            public override Void GetValue<TArg>(TArg eventArg) => default;

            public override void Invoke() {
            }

            public override void Invoke<TArg>(TArg eventArg) {
            }

            protected override CachedData<T1> GetCachedDataUnderlying<T1>() {
                throw new NotImplementedException();
            }

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
                throw new NotImplementedException();
            }
        }

        private class EventArgReference<T> : CachedData<T> {
            public static EventArgReference<T> Instance { get; } = new EventArgReference<T>();

            public override Type ReturnType => typeof(T);

            public override MethodInfo GetMethodInfo() {
                throw new NotImplementedException();
            }

            public override T GetValue() {
#if UNITY_EDITOR
                Debug.LogError("No custom argument was provided");
#endif
                return default;
            }

            public override T1 GetValue<T1>() {
                throw new NotImplementedException();
            }

            public override T GetValue<TArg>(TArg eventArg) => TypeCaster<TArg, T>.Cast(eventArg);

            public override T1 GetValue<TArg, T1>(TArg eventArg) {
                throw new NotImplementedException();
            }

            public override void Invoke() {
                throw new NotImplementedException();
            }

            public override void Invoke<TArg>(TArg eventArg) {
                throw new NotImplementedException();
            }

            protected override CachedData<TDesired> GetCachedDataUnderlying<TDesired>() => this as CachedData<TDesired>;

            protected override void SetArguments(CachedData[] arguments) {
                throw new NotImplementedException();
            }

            protected override void UpdateReferences(Dictionary<int, CachedData> runtimeReferences) {
            }
        }

        private class PauseCall : CachedData<Void> {
            #region Fields

            private CachedData _method;

            private CachedData<float> _pauseData;

            #endregion

            public PauseCall(CachedData method, CachedData<float> pauseData) {
                _method = method;
                _pauseData = pauseData;
            }

            public override Type ReturnType => _method.ReturnType;

            public override MethodInfo GetMethodInfo() => _method.GetMethodInfo();

            public override float GetPauseDuration() => _pauseData.GetValue();

            public override float GetPauseDuration<TArg>(TArg eventArg) => _pauseData.GetValue<TArg>(eventArg);

            public override T GetValue<T>() => _method.GetValue<T>();

            public override Void GetValue() => _method.GetValue<Void>();

            public override T GetValue<TArg, T>(TArg eventArg) => _method.GetValue<TArg, T>(eventArg);

            public override Void GetValue<TArg>(TArg eventArg) => _method.GetValue<TArg, Void>(eventArg);

            public override void Invoke() => _method.Invoke();

            public override void Invoke<TArg>(TArg eventArg) => _method.Invoke(eventArg);

            public override void StopCoroutine() => _method.StopCoroutine();

            protected override CachedData<T1> GetCachedDataUnderlying<T1>() {
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
