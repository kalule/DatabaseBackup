using Dawn;
using System.Diagnostics;

namespace DatabaseBackup.Extensions
{
    public static class GuardExtensions
    {
        [DebuggerStepThrough]
        public static ref readonly Guard.ArgumentInfo<TValue> NotDefault<TValue>(in this Guard.ArgumentInfo<TValue> argument, string message = null)
        {
            if (!argument.HasValue() || argument.Value == null || argument.Value.Equals(default(TValue))) // Check whether the value is null or empty
            {
                throw Guard.Fail(new ArgumentNullException(argument.Name, message ?? $"'{argument.Name}' cannot be null or empty."));
            }

            return ref argument;
        }
    }
}
