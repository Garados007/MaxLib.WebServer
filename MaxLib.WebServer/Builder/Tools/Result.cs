using System;

namespace MaxLib.WebServer.Builder.Tools
{
    public readonly struct Result<T>
    {
        private readonly T value;

        private readonly bool hasValue;

        public Result(T value)
        {
            this.value = value;
            this.hasValue = true;
        }

        public bool HasValue => hasValue;

        public T Value
        {
            get
            {
                if (!hasValue)
                    throw new NotSupportedException("value doesn't exists");
                else return value;
            }
        }

        public Result<U> Map<U>(Func<T, U> mapper)
        {
            if (hasValue)
                return new Result<U>(mapper(value));
            else return new Result<U>();
        }

        public Result<U> AndThen<U>(Func<T, Result<U>> func)
        {
            if (hasValue)
                return func(value);
            else return new Result<U>();
        }
    }
}