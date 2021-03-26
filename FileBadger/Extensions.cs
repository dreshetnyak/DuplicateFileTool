using System;

namespace FileBadger
{
    internal static class Extensions
    {
        public static T[] SubArray<T>(this T[] source, int offset, int length = -1)
        {
            if (length == -1)
                length = source.Length - offset;
            var resultArray = new T[length];
            Array.Copy(source, offset, resultArray, 0, length);
            return resultArray;
        }

        public static bool ByteArrayEquals(this byte[] arrayLeft, byte[] arrayRight)
        {
            if (arrayLeft.Length != arrayRight.Length)
                return false;

            // ReSharper disable once LoopCanBeConvertedToQuery
            for (var index = 0; index < arrayLeft.Length; index++)
            {
                if (arrayLeft[index] != arrayRight[index])
                    return false;
            }

            return true;
        }

        public static string SubstringAfterLast(this string str, char ch)
        {
            var chIdx = str.LastIndexOf(ch);
            return chIdx != -1
                ? str.Substring(chIdx + 1)
                : string.Empty;
        }

        public static DateTime ToDateTime(this Win32.FILETIME fileTime)
        {
            return DateTime.FromFileTime(Data.JoinToLong(fileTime.dwHighDateTime, fileTime.dwLowDateTime));
        }

        public static bool IsInvalidHandle(this IntPtr handle)
        {
            return handle == IntPtr.Zero || handle == Win32.INVALID_HANDLE_VALUE;
        }

        public static bool IsValidHandle(this IntPtr handle)
        {
            return handle != IntPtr.Zero && handle != Win32.INVALID_HANDLE_VALUE;
        }
    }

    internal static class Data
    {
        public static long JoinToLong(uint highDWord, uint lowDWord) { return (((long)highDWord) << 32) | lowDWord; }
        public static long JoinToLong(int highDWord, int lowDWord) { return (((long)highDWord) << 32) | (uint)lowDWord; }
    }
}
