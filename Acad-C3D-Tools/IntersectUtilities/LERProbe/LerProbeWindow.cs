using System.Drawing;
using System.Windows.Forms;

namespace IntersectUtilities.LerProbe;

internal sealed class LerProbeWindow : Form
{
    private readonly LerProbeSnapshot snapshot;
    private readonly DataGridView grid;
    private readonly Label status;

    public LerProbeWindow(LerProbeSnapshot snapshot)
    {
        this.snapshot = snapshot;
        Text = $"LERPROBE — {snapshot.Layer} — {snapshot.Handle}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(760, 480);
        Size = new Size(1120, 760);
        Font = new System.Drawing.Font("Segoe UI", 9F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowInTaskbar = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 4
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var identity = new TextBox
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control, Height = 70, WordWrap = false, TabStop = false,
            Text = $"Xref: {snapshot.XrefNames}\r\nLayer: {snapshot.Layer}    Handle: {snapshot.Handle}\r\nFile: {snapshot.SourceFile}"
        };
        layout.Controls.Add(identity, 0, 0);

        var searchPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchPanel.Controls.Add(new Label { Text = "Search:", AutoSize = true, Margin = new Padding(0, 5, 8, 0) }, 0, 0);
        var search = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 10) };
        searchPanel.Controls.Add(search, 1, 0);
        layout.Controls.Add(searchPanel, 0, 1);

        grid = new DataGridView
        {
            Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false,
            AllowUserToDeleteRows = false, AllowUserToOrderColumns = true,
            AutoGenerateColumns = false, RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = true,
            ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText,
            BackgroundColor = SystemColors.Window, BorderStyle = BorderStyle.FixedSingle,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        AddColumn("Property set", nameof(LerProbeProperty.PropertySet), 18);
        AddColumn("Property", nameof(LerProbeProperty.Property), 30);
        AddColumn("Value", nameof(LerProbeProperty.Value), 52);
        layout.Controls.Add(grid, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        status = new Label { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 12, 8, 4) };
        footer.Controls.Add(status, 0, 0);
        var close = new Button { Text = "Close", AutoSize = true, DialogResult = DialogResult.Cancel, Margin = new Padding(0, 8, 0, 0) };
        footer.Controls.Add(close, 1, 0);
        CancelButton = close;
        close.Click += (_, _) => Close();
        layout.Controls.Add(footer, 0, 3);
        Controls.Add(layout);
        search.TextChanged += (_, _) => Filter(search.Text);
        Shown += (_, _) => search.Focus();
        Filter(string.Empty);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void AddColumn(string title, string property, float weight) => grid.Columns.Add(
        new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = property, FillWeight = weight, MinimumWidth = 85 });

    private void Filter(string text)
    {
        var rows = snapshot.Properties.Where(row =>
            row.PropertySet.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
            row.Property.Contains(text, StringComparison.CurrentCultureIgnoreCase) ||
            row.Value.Contains(text, StringComparison.CurrentCultureIgnoreCase)).ToList();
        grid.DataSource = rows;
        int unavailable = snapshot.Properties.Count(row => row.Unavailable);
        status.Text = snapshot.PropertySetCount == 0
            ? "This polyline has no attached property sets."
            : $"{snapshot.PropertySetCount} property sets · {rows.Count}/{snapshot.Properties.Count} properties · Select rows and press Ctrl+C to copy.";
        if (unavailable > 0)
            status.Text += $"  {unavailable} values unavailable.";
    }
}
