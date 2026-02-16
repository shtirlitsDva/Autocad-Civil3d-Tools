using System;
using System.Text;

namespace SheetCreationAutomation.Services
{
    internal static class AutomationRunLog
    {
        private static readonly object Sync = new();
        private static readonly StringBuilder Buffer = new();

        public static event Action? LogChanged;

        public static void Clear()
        {
            lock (Sync)
            {
                Buffer.Clear();
            }

            LogChanged?.Invoke();
        }

        public static void Append(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            lock (Sync)
            {
                Buffer.Append('[')
                    .Append(DateTime.Now.ToString("HH:mm:ss.fff"))
                    .Append("] ")
                    .AppendLine(message);
            }

            LogChanged?.Invoke();
        }

        public static string GetText()
        {
            lock (Sync)
            {
                return Buffer.ToString();
            }
        }
    }
}
