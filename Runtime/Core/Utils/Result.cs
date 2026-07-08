namespace Hwi.Foundation.Core
{
    public readonly struct Result<T>
    {
        public bool IsOk { get; }
        public string Error { get; }
        private readonly T _value;

        private Result(bool ok, T value, string error)
        {
            IsOk = ok;
            _value = value;
            Error = error;
        }

        public T Value
        {
            get
            {
                if (!IsOk) throw new System.InvalidOperationException($"Result is Failure: {Error}");
                return _value;
            }
        }

        public static Result<T> Success(T value) => new Result<T>(true, value, null);
        public static Result<T> Failure(string error) => new Result<T>(false, default, error);
    }
}
