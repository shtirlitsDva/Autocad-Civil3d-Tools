using System.Drawing;
using System.Windows.Forms;

namespace IntersectUtilities.PlanDetailing.LineTypes
{
    /// <summary>
    /// The text prompt of IntersectUtilities' CCLS, CCLX and CCLXS heads. The linetype
    /// code itself is IntersectUtilitiesCOMMON's LineTypes\ (LinetypePreset,
    /// LinetypeRecords, LinetypeCommands), shared with NorsynDrawingToolsManaged.
    /// </summary>
    internal static class LineTypes
    {
        private sealed class StringInputForm : Form
        {
            private readonly TextBox inputTextBox;

            public string InputText => inputTextBox.Text;

            public StringInputForm(string title, string prompt, string defaultValue)
            {
                Text = title;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                StartPosition = FormStartPosition.CenterScreen;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                KeyPreview = true;

                int margin = 12;
                int spacing = 8;
                int textBoxWidth = 360;

                var promptLabel = new Label
                {
                    AutoSize = true,
                    Text = prompt,
                    Location = new Point(margin, margin)
                };

                inputTextBox = new TextBox
                {
                    Location = new Point(margin, promptLabel.Bottom + spacing),
                    Width = textBoxWidth,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    Text = defaultValue
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new Size(90, 28),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                var cancelButton = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Size = new Size(90, 28),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };

                AcceptButton = okButton;
                CancelButton = cancelButton;

                int buttonsTop = inputTextBox.Bottom + spacing + 4;
                cancelButton.Location = new Point(margin + textBoxWidth - cancelButton.Width, buttonsTop);
                okButton.Location = new Point(cancelButton.Left - okButton.Width - spacing, buttonsTop);

                ClientSize = new Size(margin + textBoxWidth + margin, buttonsTop + okButton.Height + margin);

                Controls.Add(promptLabel);
                Controls.Add(inputTextBox);
                Controls.Add(okButton);
                Controls.Add(cancelButton);

                Shown += (sender, e) =>
                {
                    inputTextBox.SelectAll();
                    inputTextBox.Focus();
                };
            }
        }

        internal static string? PromptForString(string title, string prompt, string defaultValue = "")
        {
            using (var form = new StringInputForm(title, prompt, defaultValue))
            {
                return form.ShowDialog() == DialogResult.OK ? form.InputText : null;
            }
        }
    }
}
