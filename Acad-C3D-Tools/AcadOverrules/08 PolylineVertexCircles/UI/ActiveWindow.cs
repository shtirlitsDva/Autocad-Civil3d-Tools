using System.Windows;

namespace AcadOverrules.VertexCircles.UI
{
    /// <summary>
    /// Finds the window a child dialog should be owned by. Inside AutoCAD there is no WPF
    /// <see cref="Application"/> unless something created one, so this can legitimately
    /// return null and callers must cope.
    /// </summary>
    internal static class ActiveWindow
    {
        public static Window? Find()
        {
            Application? app = Application.Current;
            if (app == null) return null;

            foreach (Window window in app.Windows)
                if (window.IsActive) return window;

            return app.MainWindow;
        }

        /// <summary>Owns and centres <paramref name="dialog"/> on the active window when there is one.</summary>
        public static void Attach(Window dialog)
        {
            Window? owner = Find();
            if (owner == null || ReferenceEquals(owner, dialog)) return;

            dialog.Owner = owner;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
    }
}
