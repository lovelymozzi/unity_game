namespace Hwi.Foundation.Core
{
    public readonly struct Option<T>
    {
        public bool HasValue { get; }
        private readonly T _value;

        private Option(bool hasValue, T value)
        {
            HasValue = hasValue;
            _value = value;
        }

        public T Value
        {
            get
            {
                if (!HasValue) throw new System.InvalidOperationException("Option is None");
                return _value;
            }
        }

        public T GetOrDefault(T fallback) => HasValue ? _value : fallback;

        public static Option<T> Some(T value) => new Option<T>(true, value);
        public static Option<T> None => new Option<T>(false, default);
    }
}
