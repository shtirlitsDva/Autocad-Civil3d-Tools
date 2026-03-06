using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using SheetCreationAutomation.UI;

namespace SheetCreationAutomation.Services
{
    internal sealed class WaitOverlayPresenter : IWaitOverlayPresenter
    {
        private const int OverlayMinWidth = 760;
        private const int OverlayMaxWidth = 1100;
        private const int OverlayMinHeight = 150;
        private const int ScreenMargin = 24;

        private OverlayForm? overlayForm;

        public Action? CancelAction { get; set; }

        public void Show(string stepName, TimeSpan elapsed)
        {
            string message =
                "AUTOMATION WAIT\n" +
                $"Step: {stepName}\n" +
                $"Elapsed: {elapsed:hh\\:mm\\:ss}\n" +
                $"Updated: {DateTime.Now:HH:mm:ss}";

            RunOnCadUiContext(() =>
            {
                EnsureForm();
                if (overlayForm == null || overlayForm.IsDisposed)
                {
                    return;
                }

                overlayForm.SafeSetMessage(message, CancelAction != null);
                overlayForm.SafeShow();
            });
        }

        public void Hide()
        {
            RunOnCadUiContext(() =>
            {
                if (overlayForm == null || overlayForm.IsDisposed)
                {
                    return;
                }

                overlayForm.SafeHide();
            });
        }

        private void EnsureForm()
        {
            if (overlayForm != null && !overlayForm.IsDisposed)
            {
                return;
            }

            overlayForm = new OverlayForm();
            overlayForm.CancelClicked += (_, __) => CancelAction?.Invoke();
            IntPtr mainHandle = AcApp.MainWindow.Handle;
            Rectangle bounds = Screen.FromHandle(mainHandle).WorkingArea;

            int preferredWidth = Math.Max(OverlayMinWidth, bounds.Width / 2);
            overlayForm.Width = Math.Min(preferredWidth, OverlayMaxWidth);
            overlayForm.Height = OverlayMinHeight;
            overlayForm.StartPosition = FormStartPosition.Manual;
            overlayForm.Location = new Point(
                Math.Max(bounds.Left + ScreenMargin, bounds.Right - overlayForm.Width - ScreenMargin),
                Math.Max(bounds.Top + ScreenMargin, bounds.Top + ScreenMargin));
        }

        private static void RunOnCadUiContext(Action action)
        {
            SynchronizationContext? context = AcContext.Current;
            if (context == null || SynchronizationContext.Current == context)
            {
                action();
                return;
            }

            context.Post(_ => action(), null);
        }

        private sealed class OverlayForm : Form
        {
            private const int CancelButtonWidth = 120;
            private const int CancelButtonHeight = 36;
            private const int CancelButtonMargin = 16;

            private readonly Label messageLabel;
            private readonly Button cancelButton;

            public event EventHandler? CancelClicked;

            public OverlayForm()
            {
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                BackColor = Color.Black;
                ForeColor = Color.White;
                Opacity = 0.82;
                Width = OverlayMinWidth;
                Height = OverlayMinHeight;
                Padding = new Padding(16);

                cancelButton = new Button
                {
                    Text = "CANCEL",
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(180, 30, 30),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 10, FontStyle.Bold),
                    Width = CancelButtonWidth,
                    Height = CancelButtonHeight,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Cursor = Cursors.Hand,
                    Visible = false
                };
                cancelButton.FlatAppearance.BorderColor = Color.FromArgb(220, 60, 60);
                cancelButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 40, 40);
                cancelButton.Click += (_, __) => CancelClicked?.Invoke(this, EventArgs.Empty);

                messageLabel = new Label
                {
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11, FontStyle.Bold),
                    TextAlign = ContentAlignment.TopLeft
                };

                Controls.Add(cancelButton);
                Controls.Add(messageLabel);
            }

            public void SafeSetMessage(string message, bool showCancel)
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(() => SafeSetMessage(message, showCancel)));
                    return;
                }

                messageLabel.Text = message;
                cancelButton.Visible = showCancel;
                LayoutControls();
            }

            public void SafeShow()
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(SafeShow));
                    return;
                }

                if (!Visible)
                {
                    Show();
                }
                else
                {
                    Refresh();
                }
            }

            public void SafeHide()
            {
                if (InvokeRequired)
                {
                    BeginInvoke(new Action(SafeHide));
                    return;
                }

                if (Visible)
                {
                    Hide();
                }
            }

            private void LayoutControls()
            {
                int textWidth = Math.Max(100, ClientSize.Width - Padding.Left - Padding.Right);
                if (cancelButton.Visible)
                {
                    textWidth -= CancelButtonWidth + CancelButtonMargin;
                }

                Size measured = TextRenderer.MeasureText(
                    messageLabel.Text ?? string.Empty,
                    messageLabel.Font,
                    new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);

                int contentHeight = cancelButton.Visible
                    ? Math.Max(measured.Height, CancelButtonHeight)
                    : measured.Height;
                int desiredHeight = contentHeight + Padding.Top + Padding.Bottom + 12;
                Rectangle workArea = Screen.FromControl(this).WorkingArea;
                int maxHeight = Math.Max(OverlayMinHeight, workArea.Height - (ScreenMargin * 2));
                Height = Math.Max(OverlayMinHeight, Math.Min(desiredHeight, maxHeight));

                messageLabel.SetBounds(
                    Padding.Left,
                    Padding.Top,
                    textWidth,
                    ClientSize.Height - Padding.Top - Padding.Bottom);

                if (cancelButton.Visible)
                {
                    cancelButton.Location = new Point(
                        ClientSize.Width - CancelButtonWidth - CancelButtonMargin,
                        Padding.Top);
                }
            }
        }
    }
}
