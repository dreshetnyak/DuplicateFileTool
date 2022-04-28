using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DuplicateFileTool
{
    internal class Logger : IDisposable
    {
        public enum Target { Void, Debug, File, Callback }
        public Target LogTarget { get; }
        public Action<string> LogCallback { get; }
        public string LogFilePath { get; }

        private CancellationTokenSource QueueTaskCancellation { get; set; }
        private Task LogQueueTask { get; set; }
        private BlockingCollection<(DateTime MessageTime, string MessageText)> MessageQueue { get; }

        #region Constructor and Disposal
        public Logger(Target logTarget = Target.Void, Action<string> logCallback = null, string logFilePath = null)
        {
            LogTarget = logTarget;
            LogCallback = logCallback;
            LogFilePath = logFilePath ?? Path.Combine(Utility.GetAssemblyLocation(), "Logs");
            MessageQueue = new BlockingCollection<(DateTime MessageTime, string MessageText)>();
            QueueTaskCancellation = new CancellationTokenSource();
            var cancellationToken = QueueTaskCancellation.Token;
            LogQueueTask = Task.Run(() => LogQueue(cancellationToken), cancellationToken);
        }

        public void Dispose()
        {
            if (QueueTaskCancellation == null) 
                return;
            QueueTaskCancellation.Cancel();
            QueueTaskCancellation = null;

            if (LogQueueTask == null)
                return;
            try { LogQueueTask.Wait(5000); }
            catch { /* ignore */ }
            LogQueueTask = null;
        }

        #endregion

        public void Write(string message)
        {
            MessageQueue.Add((DateTime.Now, message));
        }

        private void LogQueue(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                WriteLog(MessageQueue.Take(cancellationToken));
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        private void WriteLog((DateTime MessageTime, string MessageText) messageData)
        {
            switch (LogTarget)
            {
                case Target.Debug:
                    Debug.WriteLine(GetMessage(messageData));
                    break;
                case Target.File:
                    AppendToLogFile(messageData);
                    break;
                case Target.Callback:
                    CallLogCallback(GetMessage(messageData));
                    break;
                case Target.Void:
                    break;
            }
        }

        private static string GetMessage((DateTime MessageTime, string MessageText) messageData)
        {
            var (messageTime, messageText) = messageData;
            return messageTime != default
                ? messageTime.ToString("HH:mm:ss.fff") + messageText
                : messageText;
        }

        private void CallLogCallback(string message)
        {
            try { LogCallback?.Invoke(message); }
            catch { /* ignore */ }
        }

        private void AppendToLogFile((DateTime MessageTime, string MessageText) messageData)
        {
            if (!Utility.MakeSureDirectoryExists(LogFilePath))
                return;
            try { File.AppendAllText(Path.Combine(LogFilePath, $"{messageData.MessageTime:yyyy-MM-dd}.log"), GetMessage(messageData)); }
            catch { /* ignore */ }
        }
    }
}