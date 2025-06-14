namespace DatabaseBackup.Extensions
{
    public static class StringExtensions
    {
        public static string ToFormattedString(this string str, params object[] args)
        {
            return string.Format(str, args);
        }
        public static string ToFormattedString(this string str, object arg0)
        {
            return string.Format(str, arg0);
        }
        public static string ToFormattedString(this string str, object arg0, object arg1)
        {
            return string.Format(str, arg0, arg1);
        }
    }
}
