using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DuplicateFileTool.Properties;

namespace DuplicateFileTool
{
    internal enum MessageType { Information, Warning, Error }

    internal sealed class ErrorMessage
    {
        public MessageType Type { get; }
        public string TypeName { get; }
        public string Text { get; }
        public string Path { get; }
        public DateTime Timestamp { get; }

        public ErrorMessage(string path, string message, MessageType messageType = MessageType.Information)
        {
            Timestamp = DateTime.Now;
            Path = path ?? "";
            Text = message;
            Type = messageType;
            TypeName = GetTypeName(messageType);
        }

        public ErrorMessage(string message, MessageType messageType = MessageType.Information)
        {
            Timestamp = DateTime.Now;
            Path = "";
            Text = message;
            Type = messageType;
            TypeName = GetTypeName(messageType);
        }

        private static string GetTypeName(MessageType type)
        {
            return type switch
            {
                MessageType.Information => Resources.Ui_Errors_Type_Information,
                MessageType.Warning => Resources.Ui_Errors_Type_Warning,
                MessageType.Error => Resources.Ui_Errors_Type_Error,
                _ => Resources.Ui_Errors_Type_Unknown
            };
        }
    }

    internal static class Utility
    {
        public static string GetAssemblyLocation()
        {
            const string prefix = "file:///";
            var executingAssemblyLocation = Assembly.GetExecutingAssembly().CodeBase;
            if (executingAssemblyLocation.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                executingAssemblyLocation = executingAssemblyLocation.Substring(prefix.Length).Replace("/", "\\");
            return Path.GetDirectoryName(executingAssemblyLocation);
        }

        public static bool MakeSureDirectoryExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            if (FileSystem.DirectoryExists(path))
                return true;

            try { Directory.CreateDirectory(path); }
            catch { return false; }

            return true;
        }
    }

    internal static class ExtensionMethods
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

        public static string SubstringBeforeLast(this string str, char ch)
        {
            var chIdx = str.LastIndexOf(ch);
            return chIdx != -1
                ? str.Substring(0, chIdx)
                : str;
        }

        public static string SubstringAfterLast(this string str, char ch)
        {
            var chIdx = str.LastIndexOf(ch);
            return chIdx != -1
                ? str.Substring(chIdx + 1)
                : string.Empty;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Count(this string src, char ch)
        {
            var count = 0;
            for (var charIndex = 0; charIndex < src.Length; charIndex++)
            {
                if (src[charIndex] == ch)
                    count++;
            }

            return count;
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

        public static ImageSource ToImageSource(this Bitmap bitmap)
        {
            var handle = bitmap.GetHbitmap();
            try
            {
                return Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                Win32.DeleteObject(handle);
            }
        }

        public static string GetExceptionMessageForCulture(this int lastError, CultureInfo culture)
        {
            var messageBuffer = IntPtr.Zero;

            try
            {
                var dwChars = Win32.FormatMessage(
                    Win32.FORMAT_MESSAGE_ALLOCATE_BUFFER | Win32.FORMAT_MESSAGE_FROM_SYSTEM| Win32.FORMAT_MESSAGE_IGNORE_INSERTS,
                    IntPtr.Zero, (uint)lastError, GetLangId(culture), ref messageBuffer, 0, IntPtr.Zero);

                if (dwChars == 0)
                    return new Win32Exception(lastError).Message;

                var errorMessage = Marshal.PtrToStringUni(messageBuffer);
                return !string.IsNullOrEmpty(errorMessage) 
                    ? errorMessage.TrimEnd(' ', '.', '\r', '\n')
                    : new Win32Exception(lastError).Message;
            }
            finally
            {
                if (messageBuffer != IntPtr.Zero)
                    Win32.LocalFree(messageBuffer);
            }
        }

        private static uint GetLangId(CultureInfo culture)
        {
            var cultureName = culture.Name;
            if (string.IsNullOrEmpty(cultureName))
                return MakeLangId(Win32.LANG_NEUTRAL, Win32.SUBLANG_NEUTRAL);
            cultureName = cultureName.Substring(0, 2);
            return cultureName switch
            {
                "en" => MakeLangId(Win32.LANG_ENGLISH, Win32.SUBLANG_ENGLISH_US),
                "es" => MakeLangId(Win32.LANG_SPANISH, Win32.SUBLANG_SPANISH),
                "ru" => MakeLangId(Win32.LANG_RUSSIAN, Win32.SUBLANG_RUSSIAN_RUSSIA),
                _ => MakeLangId(Win32.LANG_NEUTRAL, Win32.SUBLANG_NEUTRAL)
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MakeLangId(uint langId, uint subLangId)
        {
            return (subLangId << 10) | langId;
        }
    }

    internal static class Data
    {
        public static long JoinToLong(uint highDWord, uint lowDWord) { return (((long)highDWord) << 32) | lowDWord; }
        public static long JoinToLong(int highDWord, int lowDWord) { return (((long)highDWord) << 32) | (uint)lowDWord; }
    }
}
