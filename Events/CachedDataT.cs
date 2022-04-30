namespace ExtendedEvents {
    public abstract class CachedData<T> : CachedData {
        public override TDesired GetValue<TDesired>() => TypeCaster<T, TDesired>.Cast(GetValue());

        public abstract T GetValue();

        public override TDesired GetValue<TArg, TDesired>(TArg eventArg) => TypeCaster<T, TDesired>.Cast(GetValue(eventArg));

        public abstract T GetValue<TArg>(TArg eventArg);

        protected override CachedData<TDesired> GetCachedData<TDesired>() {
            return GetCachedDataUnderlying<TDesired>() ?? CasterGetter<T, TDesired>.GetCaster(this);
        }
    }
}
