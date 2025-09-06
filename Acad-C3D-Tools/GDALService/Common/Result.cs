using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GDALService.Common
{
    internal readonly struct Result
    {
        public StatusCode Status { get; }
        public string? Error { get; }
        public bool Ok => Status == StatusCode.Success;

        public Result(StatusCode status, string? error = null)
        { Status = status; Error = error; }

        public static Result Success() => new(StatusCode.Success);
        public static Result Fail(StatusCode status, string? error = null) => new(status, error);
    }
    internal readonly struct Result<T>
    {
        public StatusCode Status { get; }
        public string? Error { get; }
        public T? Value { get; }
        public bool Ok => Status == StatusCode.Success;

        public Result(StatusCode status, T? value = default, string? error = null)
        { Status = status; Value = value; Error = error; }

        public static Result<T> Success(T value) => new(StatusCode.Success, value);
        public static Result<T> Fail(StatusCode status, string? error = null) => new(status, default, error);
    }
}
