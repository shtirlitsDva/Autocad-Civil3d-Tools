using Autodesk.AutoCAD.ApplicationServices;
using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace SheetCreationAutomation.UI
{
    internal static class PaletteExceptionGuard
    {
        private static bool _initialized;

        public static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            Dispatcher.CurrentDispatcher.UnhandledException += OnDispatcherUnhandledException;

            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            TryWriteMessage($"WPF UI exception: {e.Exception.Message}");
            e.Handled = true;
        }

        private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            TryWriteMessage($"Unobserved task exception: {e.Exception.GetBaseException().Message}");
            e.SetObserved();
        }

        private static void TryWriteMessage(string message)
        {
            try
            {
                Document? doc = Application.DocumentManager.MdiActiveDocument;
                if (doc != null)
                {
                    doc.Editor.WriteMessage($"\n[SheetAutomation] {message}");
                }
            }
            catch
            {
                // Swallow in guard path.
            }
        }
    }
}
