using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Common
{
    internal readonly struct OpResult
    {
        public bool Ok { get; }
        public string? Error { get; }
        private OpResult(bool ok, string? error) { Ok = ok; Error = error; }
        public static OpResult Success() => new(true, null);
        public static OpResult Fail(string message) => new(false, message);
    }

    internal readonly struct OpResult<T>
    {
        public bool Ok { get; }
        public string? Error { get; }
        public T? Value { get; }
        private OpResult(bool ok, T? value, string? error) { Ok = ok; Value = value; Error = error; }
        public static OpResult<T> Success(T value) => new(true, value, null);
        public static OpResult<T> Fail(string message) => new(false, default, message);
    }
}