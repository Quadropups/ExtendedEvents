using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor.Timeline.Actions;
using UnityEngine;

namespace ExtendedEvents {
    #region Enums

    /// <summary>
    /// Call's Delay mode.
    /// <para></para>
    /// Note: Wait and Pause calls can be only invoked on a parented ExtendedEvent because their invokation invokves starting a coroutine.
    /// </summary>
    public enum DelayMode {
        NoDelay,
        Wait,
        Pause,
    }

    /// <summary>
    /// Type that are natively serializable by <see cref="Argument"/>.
    /// </summary>
    public enum TypeEnum {
        /// <summary>Represents type that failed to deserialize from typename</summary>
        Unknown = -2,
        /// <summary>Represents any unserializable type that can only be retrieved using method or property</summary>
        Generic = -1,
        /// <summary>Integer property</summary>
        Integer = 0,
        /// <summary>Boolean property</summary>
        Boolean = 1,
        /// <summary>Float property</summary>
        Float = 2,
        /// <summary>String property</summary>
        String = 3,
        /// <summary>Color property</summary>
        Color = 4,
        /// <summary>Represents System.Object, interfaces and types derived from UnityEngine.Object</summary>
        Object = 5,
        /// <summary>LayerMask property</summary>
        LayerMask = 6,
        /// <summary>Enumeration property (int-based)</summary>
        Enum = 7,
        /// <summary>2D vector property</summary>
        Vector2 = 8,
        /// <summary>3D vector property</summary>
        Vector3 = 9,
        /// <summary>4D vector property</summary>
        Vector4 = 10,
        /// <summary>Rect property</summary>
        Rect = 11,
        //ArraySize = 12,

        /// <summary>Character property</summary>
        Character = 13,
        /// <summary>AnimationCurve property</summary>
        //AnimationCurve = 14,

        //Bounds = 15,

        /// <summary>Gradient property</summary>
        //Gradient = 16,

        /// <summary>Quaternion property</summary>
        Quaternion = 17,
        //ExposedReference = 18,
        //FixedBufferSize = 19,
        //Vector2Int = 20,
        //Vector3Int = 21,
        //RectInt = 22,
        //BoundsInt = 23,
        //ManagedReference = 24

        /// <summary>Type property</summary>
        Type = 26,
    }

    #endregion

    /// <summary>
    ///Base class for ExtendedEvent. 
    /// </summary>
    public abstract class ExtendedEvent {
        #region Fields

        [NonSerialized] protected CachedData[] _runtimeCalls;

        [NonSerialized] protected Dictionary<int, CachedData> _dataReferences;

        [SerializeField] protected MonoBehaviour _parent;

        #endregion

        public MonoBehaviour parent => _parent;

        /// <summary>Dictionary that contains all EventCalls and Argumets using ids for calls and "id + index" for arguments as keys</summary>
        protected Dictionary<int, CachedData> dataReferences {
            get {
                if (needSetup) Setup();
                return _dataReferences;
            }
        }

#if UNITY_EDITOR
        private int timeSinceStartup = 0;
#endif
        protected bool needSetup {
            get {
#if UNITY_EDITOR
                //this is done to keep runtime data be updated in edit mode
                if (!Application.isPlaying) {
                    int timeInt = (int)UnityEditor.EditorApplication.timeSinceStartup;
                    if (timeSinceStartup != timeInt) {
                        timeSinceStartup = timeInt;
                        return true;
                    }
                }
#endif

                return _dataReferences == null;
            }
        }

        protected CachedData[] runtimeCalls {
            get {
                if (needSetup) Setup();
                return _runtimeCalls;
            }
        }

        protected abstract EventCall[] serializedCalls { get; }

        public static MethodInfo FindMethod(string signature) {

            MethodInfo mi = null;

            string[] data = signature.Split(';');

            //method definition is made up of the following data entries which are separated by ';' character:
            //[method_name] - method name
            //[reflected_type] - type that defines this method
            //[type_1]...[type_n] - array of method parameter types
            //[ttt*] - array of chars that defines wat kind of parameter this is (specific or generic), this data entry always ends with '*' char - only for generic methods
            //[type_1]...[type_n] - array of method generic arguments - only for generic methods

            int parameterCount = data.Length - 2;
            int genericCount = 0;

            if (parameterCount < 0) {
                return null;
            }

            //look if method is generic
            for (int i = 2; i < data.Length; i++) {
                //if we on a data entry that ends with '*' character then after it generic arguments are defined
                if (data[i][data[i].Length - 1] == '*') {
                    parameterCount = i - 2;
                    genericCount = data.Length - i - 1;
                    break;
                }
            }

            bool methodIsGeneric = genericCount > 0;

            Type type;
            try {
                type = Type.GetType(data[0], true);
            }
            catch {
                return null;
            }

            Type[] parameterTypes = new Type[parameterCount];

            for (int i = 0; i < parameterCount; i++) {
                Type parameterType;
                try {
                    parameterType = Type.GetType(data[i + 2], true);
                }
                catch {
                    return null;
                }

                if (!methodIsGeneric) {
                    parameterTypes[i] = parameterType;
                }
                else {
                    switch (data[data.Length - genericCount - 1][i]) {
                        default:
                        //specific serializable type
                        case 's':
                            parameterTypes[i] = parameterType;
                            break;
                        //generic unserializable type (generic argument 1)
                        case 'T':
                            parameterTypes[i] = typeof(TParameter);
                            break;
                        //generic unserializable type (generic argument 2)
                        case 'U':
                            parameterTypes[i] = typeof(UParameter);
                            break;
                        //generic unserializable type (generic argument 3)
                        case 'V':
                            parameterTypes[i] = typeof(VParameter);
                            break;
                        //generic unserializable type (generic argument 4)
                        case 'W':
                            parameterTypes[i] = typeof(WParameter);
                            break;
                        //unserializable generic or non-generic open constructed type
                        case 't':
                            if (parameterType == null) parameterTypes[i] = typeof(OpenNonGenericType);
                            else if (parameterType.IsGenericType) parameterTypes[i] = parameterType.GetGenericTypeDefinition();
                            else parameterTypes[i] = typeof(OpenNonGenericType);
                            break;
                    }
                }
            }

            mi = FindMethod(type, data[1], parameterTypes, methodIsGeneric);

            //if method is generic then we need to make a specific method from it's generic definition
            if (methodIsGeneric) {

                Type[] genericArguments = new Type[genericCount];

                for (int i = 0; i < genericCount; i++) {
                    Type genericArgument = Type.GetType(data[i + 2 + parameterCount + 1]);
                    genericArguments[i] = genericArgument;
                }

                try {
                    mi = mi.MakeGenericMethod(genericArguments);
                }
                catch (Exception e) {
#if UNITY_EDITOR
                    if (Application.isPlaying) Debug.LogError(e);
#endif
                    return null;
                }

            }

            return mi;
        }

        public static int GetArgumentID(int callId, int argumentIndex) => unchecked(callId + (argumentIndex + 1) * 486187739);

        //we have to use this method because "ExtendedEvent" is defined in Assembly-CSharp but "ExtendedEventDrawer" is defined in Assembly-CSharp-Editor
        public static Type GetType(string typeName) => Type.GetType(typeName);

        public static Type[] MakeParameterTypeArray(Type delegateType) {
            MethodInfo invokeMethod = delegateType.GetMethod("Invoke");
            var types = invokeMethod.GetParameters().Select(p => p.ParameterType).ToArray();
            if (types.Length > 0 && types[0].IsByRef) types[0] = types[0].GetElementType();
            return types;
        }

        private static MethodInfo FindMethod(Type type, string name, Type[] types, bool methodIsGeneric) {
            MethodInfo method = null;
            Type baseType = type;
            do {
                BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                try {
                    if (!methodIsGeneric) {
                        try {
                            method = baseType.GetMethod(name, bindingFlags, null, types, null);
                        }
                        catch (AmbiguousMatchException) {
                            MethodInfo[] methods = baseType.GetMethods(bindingFlags);
                            for (int i = 0; i < methods.Length; i++) {
                                var m = methods[i];
                                if (m.Name != name) continue;
                                if (m.IsGenericMethod) continue;
                                ParameterInfo[] parameters = m.GetParameters();
                                if (parameters.Length != types.Length) continue;
                                bool parametersMatch = true;
                                for (int j = 0; j < parameters.Length; j++) {
                                    Type expectedParameter = types[j];
                                    Type actualParameter = parameters[j].ParameterType;
                                    //if paramter is not generic we only check if types match
                                    if (actualParameter == expectedParameter) continue;
                                    parametersMatch = false;
                                    break;
                                }
                                if (parametersMatch) {
                                    method = m;
                                    break;
                                }
                            }
                        }
                    }
                    else {
                        MethodInfo[] methods = baseType.GetMethods(bindingFlags);
                        for (int i = 0; i < methods.Length; i++) {
                            var m = methods[i];
                            if (m.Name != name) continue;
                            if (!m.IsGenericMethod) continue;
                            ParameterInfo[] parameters = m.GetParameters();
                            Type[] genericArguments = m.GetGenericArguments();
                            if (parameters.Length != types.Length) continue;
                            bool parametersMatch = true;
                            for (int j = 0; j < parameters.Length; j++) {
                                Type expectedParameter = types[j];
                                Type actualParameter = parameters[j].ParameterType;
                                //if parameter is generic parameter or open constructed type we do special check
                                if (actualParameter.ContainsGenericParameters) {
                                    //if it is generic parameter
                                    if (actualParameter.IsGenericParameter) {
                                        if (expectedParameter == typeof(TParameter) && actualParameter == genericArguments[0]) continue;
                                        else if (expectedParameter == typeof(UParameter) && actualParameter == genericArguments[1]) continue;
                                        else if (expectedParameter == typeof(VParameter) && actualParameter == genericArguments[2]) continue;
                                        else if (expectedParameter == typeof(WParameter) && actualParameter == genericArguments[3]) continue;
                                    }
                                    //if it's a generic unconstructed type
                                    else {
                                        //if both are generic or nongeneric
                                        if (actualParameter.IsGenericType == expectedParameter.IsGenericType) {
                                            //if it's a typical generic type then we compare generic definitions
                                            if (actualParameter.IsGenericType) {
                                                if (actualParameter.GetGenericTypeDefinition() == expectedParameter) continue;
                                            }
                                            //if it's an open constructed non-generic type (usually array) we do special check
                                            else {
                                                if (expectedParameter == typeof(OpenNonGenericType)) continue;
                                            }
                                        }
                                    }
                                }
                                //if paramter is not generic we only check if types match
                                else if (actualParameter == expectedParameter) continue;
                                parametersMatch = false;
                                break;
                            }
                            if (parametersMatch) {
                                method = m;
                                break;
                            }
                        }
                    }
                }
                catch (Exception e) {
#if UNITY_EDITOR
                    Debug.LogError(e);
#endif
                    return null;
                }
                if (method != null) break;
                baseType = baseType.BaseType;
            } while (baseType != typeof(object) && baseType != null);

            return method;
        }

        public CachedData GetData(int id) {
            if (dataReferences.TryGetValue(id, out CachedData data)) return data;
            return null;
        }

        public Action GetDelegate(int id) {
            var data = GetData(id);
            if (data != null) return data.Invoke;
            return null;
        }

        public Action<TArg> GetDelegate<TArg>(int id) {
            var data = GetData(id);
            if (data != null) return data.Invoke<TArg>;
            return null;
        }

        public IEnumerable<EventCall> GetSerializedCalls() => serializedCalls;

        public TValue GetValue<TValue>(int id) {
            if (dataReferences.TryGetValue(id, out CachedData data)) return data.GetValue<TValue>();
            return default;
        }

        public IEnumerator InvokeCoroutine(CachedData[] calls, int start, int count) {
            for (int i = 0; i < count; i++) {
                CachedData call = calls[start + i];

                float delay = call.GetPauseDuration();
                if (delay > 0) {
                    if (Time.inFixedTimeStep) {
                        float targetTime = Time.time + delay;
                        while (Time.time < targetTime) yield return new WaitForFixedUpdate();
                    }
                    else yield return new WaitForSeconds(delay);
                }
                call.Invoke();
            }
        }

        public IEnumerator InvokeCoroutine<TArg>(CachedData[] calls, int start, int count, TArg eventArg) {
            for (int i = 0; i < count; i++) {
                CachedData call = calls[start + i];

                float delay = call.GetPauseDuration(eventArg);
                if (delay > 0) {
                    if (Time.inFixedTimeStep) {
                        float targetTime = Time.time + delay;
                        while (Time.time < targetTime) yield return new WaitForFixedUpdate();
                    }
                    else yield return new WaitForSeconds(delay);
                }
                call.Invoke(eventArg);
            }
        }

        /// <summary>Stops coroutine with specified key for current event</summary>
        public void StopCoroutine(int key) {
            GetData(key)?.StopCoroutine();
        }

        protected abstract void Setup();

        protected class DelayedInvokerInternal : InvokerInternal, ICoroutineStarter {
            #region Fields

            private readonly ExtendedEvent events;

            private Coroutine _coroutine;

            #endregion

            public DelayedInvokerInternal(ExtendedEvent events, CachedData[] calls, RangeInt range) : base(calls, range) {
                this.events = events;
            }

            public override void Invoke() {
                _coroutine = events.parent.StartCoroutine(events.InvokeCoroutine(calls, range.start, range.length));
            }
            public override void Invoke<TArg>(TArg eventArg) {
                _coroutine = events.parent.StartCoroutine(events.InvokeCoroutine(calls, range.start, range.length, eventArg));
            }

            public override void StopCoroutine() {
                if (_coroutine != null) events.parent.StopCoroutine(_coroutine);
            }
        }

        protected class EmptyInvoker : Invoker {
            private CombinedData combinedData = new CombinedData();

            public override IEnumerable<CachedData> GetCalls() => Array.Empty<CachedData>();

            public override IEnumerable<T> GetValues<T>() => Array.Empty<T>();

            public override IEnumerable<T> GetValues<TArg, T>(TArg eventArg) => Array.Empty<T>();

            public override void Invoke() => combinedData.Invoke();

            public override void Invoke<TArg>(TArg eventArg) => combinedData.Invoke(eventArg);

            public override void Add(Action value) => combinedData.Add(value);
            public override void Add<T>(Action<T> value) => combinedData.Add(value);
            public override void Remove(Action value) => combinedData.Remove(value);
            public override void Remove<T>(Action<T> value) => combinedData.Remove(value);
        }

        protected class InvokerInternal : Invoker {
            #region Fields

            protected RangeInt range;

            protected CachedData[] calls;

            #endregion

            public InvokerInternal(CachedData[] calls, RangeInt range) {
                this.calls = calls;
                this.range = range;
            }

            public override IEnumerable<CachedData> GetCalls() {
                for (int i = range.start; i < range.end; i++) {
                    yield return calls[i];
                }
            }

            public override IEnumerable<T> GetValues<T>() {
                for (int i = range.start; i < range.end; i++) {
                    CachedData call = calls[i];
                    yield return call.GetValue<T>();
                }
            }

            public override IEnumerable<T> GetValues<TArg, T>(TArg eventArg) {
                for (int i = range.start; i < range.end; i++) {
                    CachedData call = calls[i];
                    yield return call.GetValue<TArg, T>(eventArg);
                }
            }

            public override void Invoke() {
                for (int i = range.start; i < range.end; i++) {
                    calls[i].Invoke();
                }
            }

            public override void Invoke<TArg>(TArg eventArg) {
                for (int i = range.start; i < range.end; i++) {
                    calls[i].Invoke(eventArg);
                }
            }

            private CombinedData combinedData {
                get {
                    if (calls[range.end - 1] is CombinedData data) {
                        return data;
                    }
                    CachedData[] newCalls = new CachedData[range.length + 1];
                    for (int i = 0; i < newCalls.Length - 1; i++) {
                        newCalls[i] = calls[range.start + i];
                    }
                    data = new CombinedData();
                    newCalls[newCalls.Length - 1] = data;
                    calls = newCalls;
                    range = new RangeInt(0, newCalls.Length);
                    return data;
                }
            }

            public override void Add(Action value) => combinedData.Add(value);
            public override void Add<T>(Action<T> value) => combinedData.Add(value);
            public override void Remove(Action value) => combinedData.Remove(value);
            public override void Remove<T>(Action<T> value) => combinedData.Remove(value);
        }

        /// <summary>Represents non-generic open constructed type (such as generic array)</summary>
        private sealed class OpenNonGenericType {
        }

        /// <summary>Represents unserializable T parameter (first generic argument of a generic method)</summary>
        private sealed class TParameter {
        }

        /// <summary>Represents unserializable U parameter (second generic argument of a generic method)</summary>
        private sealed class UParameter {
        }

        /// <summary>Represents unserializable U parameter (third generic argument of a generic method)</summary>
        private sealed class VParameter {
        }

        /// <summary>Represents unserializable U parameter (fourth generic argument of a generic method)</summary>
        private sealed class WParameter {
        }
    }

    /// <summary>
    ///ExtendedEvent class that can be used to fully operate extended events. 
    /// <para></para>
    /// It is recommended to operate this class instead of <see cref="ExtendedEvent{TTag, TArgument}"/> because knowing <see cref="Argument"/> type is not needed during runtime.
    /// </summary>
    public abstract class ExtendedEvent<TTag> : ExtendedEvent {
        #region Fields

        protected static readonly bool TagIsIComparable = typeof(IComparable<TTag>).IsAssignableFrom(typeof(TTag));

        protected EventCall<TTag>[] _orderedCalls;

        protected Dictionary<TTag, Invoker> _invokers;

        #endregion

        private Dictionary<TTag, Invoker> invokers {
            get {
                if (needSetup) Setup();
                return _invokers;
            }
        }

        private static int GetCount(EventCall<TTag>[] calls, int index) {
            int count = 1;
            var tag = calls[index].tag;
            index++;

            IEquatable<TTag> tagE = tag as IEquatable<TTag>;
            for (; index < calls.Length; index++) {
                if (tagE?.Equals(calls[index].tag) ?? Equals(tag, calls[index].tag)) count++;
                else break;
            }
            return count;
        }

        public void Add(TTag tag, Action del) => GetInvoker(tag).Add(del);

        public void Add<TArg>(TTag tag, Action<TArg> del) => GetInvoker(tag).Add(del);

        public IEnumerable<CachedData> GetCalls(TTag tag) => GetInvoker(tag).GetCalls();

        public CachedData GetData(TTag tag) {
            for (int i = 0; i < _orderedCalls.Length; i++) {
                var call = _orderedCalls[i];
                if (call.tag.Equals(tag)) return _runtimeCalls[i];
            }
            return null;
        }

        public Action GetDelegate(TTag tag) => GetInvoker(tag).Invoke;

        /// <summary>Will get invoker with specified tag. If none exists new will be created</summary>
        public Invoker GetInvoker(TTag tag) {
            if (invokers.TryGetValue(tag, out Invoker invoker)) return invoker;
            invoker = new EmptyInvoker();
            invokers[tag] = invoker;
            return invoker;
        }

        private Invoker GetInvokerInternal(TTag tag) {
            IEquatable<TTag> tagE = tag as IEquatable<TTag>;

            int index = 0;
            int count = 0;
            bool pausable = false;

            for (int i = 0; i < _orderedCalls.Length; i++) {
                if (tagE?.Equals(_orderedCalls[i].tag) ?? Equals(tag, _orderedCalls[i].tag)) {
                    if (count == 0) index = i;
                    count++;
                    pausable = pausable || _orderedCalls[i].delayMode == DelayMode.Pause;
                }
                else {
                    if (count > 0) break;
                }
            }

            if (count == 0) return null;

            RangeInt range = new RangeInt(index, count);

            if (pausable) {
                return new DelayedInvokerInternal(this, _runtimeCalls, range);
            }
            else {
                return new InvokerInternal(_runtimeCalls, range);
            }
        }

        protected void SetupSerializedInvokers() {
            _invokers = new Dictionary<TTag, Invoker>();

            foreach (TTag tag in _orderedCalls.Select(t => t.tag).Distinct()) {
                invokers[tag] = GetInvokerInternal(tag);
            }
        }


        public new EventCall<TTag>[] GetSerializedCalls() => serializedCalls as EventCall<TTag>[];

        public IEnumerable<KeyValuePair<TTag, Invoker>> GetTagInvokers() {
            if (TagIsIComparable) return invokers.OrderBy(pair => pair.Key);
            else return invokers.OrderBy(pair => pair.Key.GetHashCode());
        }

        public TValue GetValue<TValue>(TTag tag) {
            CachedData data = GetData(tag);
            if (data != null) return data.GetValue<TValue>();
            return default;
        }

        public TValue GetValue<TValue, TArg>(TTag tag, TArg eventArg) {
            CachedData data = GetData(tag);
            if (data != null) return data.GetValue<TArg, TValue>(eventArg);
            return default;
        }

        public IEnumerable<TValue> GetValues<TValue>(TTag tag) => GetInvoker(tag).GetValues<TValue>();

        public IEnumerable<TValue> GetValues<TValue, TArg>(TTag tag, TArg eventArg) => GetInvoker(tag).GetValues<TArg, TValue>(eventArg);

        public bool HasEventsWithTag(TTag tag) => invokers.ContainsKey(tag);

        public void Invoke(TTag tag) => GetInvoker(tag).Invoke();

        public void Invoke<TArg>(TTag tag, TArg eventArg) => GetInvoker(tag).Invoke(eventArg);

        public void Remove(TTag tag, Action del) => GetInvoker(tag).Remove(del);

        public void Remove<TArg>(TTag tag, Action<TArg> del) => GetInvoker(tag).Remove(del);

        /// <summary>Stops coroutine with specified tag for current event</summary>
        public void StopCoroutine(TTag tag) {
            if (invokers.TryGetValue(tag, out Invoker invoker)) {
                invoker.StopCoroutine();
            }
        }





        private class Void {
        }
    }

    /// <summary>
    ///Serializable ExtendedEvent type. 
    /// </summary>
    [Serializable]
    public class ExtendedEvent<TTag, TArgument> : ExtendedEvent<TTag> where TArgument : Argument {
        #region Fields

        [SerializeField] private EventCall<TTag, TArgument>[] _calls;

        #endregion

        public ExtendedEvent(MonoBehaviour parent, IEnumerable<EventCall<TTag, TArgument>> calls) {
            _parent = parent;
            _orderedCalls = _calls = MakeRuntimeCalls(calls);
        }

        protected override EventCall[] serializedCalls => _calls;

        protected static EventCall<TTag, TArgument>[] MakeRuntimeCalls(IEnumerable<EventCall<TTag, TArgument>> calls) {
            if (calls == null) return Array.Empty<EventCall<TTag, TArgument>>();
            IEnumerable<EventCall<TTag, TArgument>> orderedCalls;
            if (TagIsIComparable) orderedCalls = calls.OrderBy(call => call.tag);
            else orderedCalls = calls.OrderBy(call => call.tag.GetHashCode());
            return orderedCalls.ToArray();
        }

        public new EventCall<TTag, TArgument>[] GetSerializedCalls() => serializedCalls as EventCall<TTag, TArgument>[];

        protected override void Setup() {

            if (_orderedCalls == null) _orderedCalls = MakeRuntimeCalls(_calls);

            CachedData.SetupCalls(_parent, _orderedCalls, out _runtimeCalls, out _dataReferences);

            SetupSerializedInvokers();
        }
    }
}
