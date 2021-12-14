﻿using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DuplicateFileTool
{
    internal enum MessageType { Information, Warning, Error }

    internal sealed class ErrorMessage
    {
        public MessageType Type { get; }
        public string Text { get; }
        public string Path { get; }
        public DateTime Timestamp { get; }

        public ErrorMessage(string path, string message, MessageType messageType = MessageType.Information)
        {
            Timestamp = DateTime.Now;
            Path = path ?? "";
            Text = message;
            Type = messageType;
        }

        public ErrorMessage(string message, MessageType messageType = MessageType.Information)
        {
            Timestamp = DateTime.Now;
            Path = "";
            Text = message;
            Type = messageType;
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
    }

    internal static class Data
    {
        public static long JoinToLong(uint highDWord, uint lowDWord) { return (((long)highDWord) << 32) | lowDWord; }
        public static long JoinToLong(int highDWord, int lowDWord) { return (((long)highDWord) << 32) | (uint)lowDWord; }
    }
}
