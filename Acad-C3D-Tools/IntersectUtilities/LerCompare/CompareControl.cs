using System.IO;
using Win = System.Windows.Forms;
using Drawing = System.Drawing;

namespace IntersectUtilities.LerCompare;

internal sealed record Settings(string OldPath, Options Options, CompareOption<double> UnitOverride);
internal sealed class CompareControl : Win.UserControl
{
    private readonly Win.TextBox oldPath = new() { Dock = Win.DockStyle.Fill };
    private readonly Win.TextBox newPath = new() { Dock = Win.DockStyle.Fill, ReadOnly = true };
    private readonly Win.TextBox coverageLayer = new() { Text = "GraveforespPolygon", Width = 140 };
    private readonly Win.NumericUpDown tolerance = new() { Minimum = .01m, Maximum = 10000, DecimalPlaces = 2, Value = 1, Width = 65 };
    private readonly Win.NumericUpDown radius = new() { Minimum = .01m, Maximum = 1000, DecimalPlaces = 2, Value = 1, Width = 65 };
    private readonly Win.CheckBox coverage = new() { Text = "Use coverage hatches", Checked = true, AutoSize = true };
    private readonly Win.ComboBox units = new() { DropDownStyle = Win.ComboBoxStyle.DropDownList, Width = 115 };
    private readonly Win.CheckedListBox flags = new() { CheckOnClick = true, Dock = Win.DockStyle.Fill, MultiColumn = true, ColumnWidth = 170, IntegralHeight = false };
    private readonly Win.ComboBox utility = new() { DropDownStyle = Win.ComboBoxStyle.DropDownList, Width = 135 };
    private readonly Win.TextBox search = new() { Width = 145, PlaceholderText = "Owner, handle, value…" };
    private readonly Win.CheckBox oldGeometry = new() { Text = "Old routes (grey)", Checked = true, AutoSize = true };
    private readonly Win.CheckBox newGeometry = new() { Text = "New routes", Checked = true, AutoSize = true };
    private readonly Win.CheckBox vertices = new() { Text = "Changed vertices/edges", Checked = true, AutoSize = true };
    private readonly Win.CheckBox selectedOnly = new() { Text = "Selected result only", Checked = true, AutoSize = true };
    private readonly Win.DataGridView grid = new() { Dock = Win.DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = Win.DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoGenerateColumns = false, VirtualMode = true, RowHeadersVisible = false, BackgroundColor = Drawing.SystemColors.Window };
    private readonly Win.TextBox detail = new() { Dock = Win.DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = Win.ScrollBars.Both, WordWrap = false };
    private readonly Win.Label status = new() { Dock = Win.DockStyle.Fill, AutoEllipsis = true, Text = "Open the newer LER drawing, select the older DWG, and compare." };
    private readonly Win.Button compare = new() { Text = "Compare", AutoSize = true };
    private readonly Win.Button export = new() { Text = "Export report…", AutoSize = true, Enabled = false };
    private readonly List<Change> flagValues = Enum.GetValues<Change>().Where(c => c != Change.None).ToList();
    private CompareOption<Report> report = CompareOption<Report>.None; private bool binding;
    public List<Result> VisibleResults { get; private set; } = new();
    public bool ShowOld => oldGeometry.Checked; public bool ShowNew => newGeometry.Checked; public bool ShowVertices => vertices.Checked; public bool SelectedOnly => selectedOnly.Checked;
    public CompareOption<Result> Selected => grid.SelectedRows.Count > 0 && grid.SelectedRows[0].Index < VisibleResults.Count
        ? CompareOption<Result>.Some(VisibleResults[grid.SelectedRows[0].Index]) : CompareOption<Result>.None;
    public int FilterCount => flags.Items.Count;
    public CompareControl()
    {
        Dock = Win.DockStyle.Fill;
        Font = new Drawing.Font("Segoe UI", 9);
        Padding = new Win.Padding(8);
        var root = new Win.TableLayoutPanel { Dock = Win.DockStyle.Fill, ColumnCount = 1, RowCount = 9 };
        root.ColumnStyles.Add(new Win.ColumnStyle(Win.SizeType.Percent, 100));
        foreach (float h in new[] { 56f, 60f, 32f, 22f, 112f, 28f, 44f })
            root.RowStyles.Add(new Win.RowStyle(Win.SizeType.Absolute, h));
        root.RowStyles.Add(new Win.RowStyle(Win.SizeType.Percent, 100));
        root.RowStyles.Add(new Win.RowStyle(Win.SizeType.Absolute, 32));
        Controls.Add(root);
        var paths = new Win.TableLayoutPanel { Dock = Win.DockStyle.Fill, ColumnCount = 3, RowCount = 2 };
        paths.ColumnStyles.Add(new(Win.SizeType.Absolute, 75));
        paths.ColumnStyles.Add(new(Win.SizeType.Percent, 100));
        paths.ColumnStyles.Add(new(Win.SizeType.Absolute, 75));
        paths.RowStyles.Add(new(Win.SizeType.Percent, 50));
        paths.RowStyles.Add(new(Win.SizeType.Percent, 50));
        var browse = new Win.Button { Text = "Browse…", Dock = Win.DockStyle.Fill };
        browse.Click += (_, _) => { using var d = new Win.OpenFileDialog { Filter = "AutoCAD drawing|*.dwg", FileName = oldPath.Text }; if (d.ShowDialog() == Win.DialogResult.OK) oldPath.Text = d.FileName; };
        paths.Controls.Add(new Win.Label { Text = "New DWG", AutoSize = true }, 0, 0);
        paths.Controls.Add(newPath, 1, 0);
        paths.SetColumnSpan(newPath, 2);
        paths.Controls.Add(new Win.Label { Text = "Old DWG", AutoSize = true }, 0, 1);
        paths.Controls.Add(oldPath, 1, 1);
        paths.Controls.Add(browse, 2, 1);
        root.Controls.Add(paths, 0, 0);
        var settings = Flow();
        settings.Controls.AddRange(new Win.Control[] { Label("Tolerance (cm)"), tolerance, Label("Match radius (m)"), radius, units, coverage, coverageLayer });
        root.Controls.Add(settings, 0, 1);
        units.Items.AddRange(new object[] { "Drawing units", "Override: m", "Override: mm", "Override: cm" });
        units.SelectedIndex = 0;
        var buttons = Flow();
        var clear = new Win.Button { Text = "Clear overlay", AutoSize = true };
        var show = new Win.Button { Text = "Show overlay", AutoSize = true };
        var zoom = new Win.Button { Text = "Zoom to result", AutoSize = true };
        compare.Click += (_, _) => LerCompareRuntime.ActiveDocument().Match(document => { document.SendStringToExecute("LERCOMPARE_RUN ", true, false, false); return true; }, () => false);
        clear.Click += (_, _) => LerCompareRuntime.Clear();
        show.Click += (_, _) => LerCompareRuntime.Refresh();
        zoom.Click += (_, _) => LerCompareRuntime.Zoom(Selected);
        export.Click += (_, _) => Export();
        buttons.Controls.AddRange(new Win.Control[] { compare, zoom, clear, show, export });
        root.Controls.Add(buttons, 0, 2);
        root.Controls.Add(status, 0, 3);
        foreach (var c in flagValues)
            flags.Items.Add(FlagLabel(c), c != Change.Delivery && c != Change.Identifier && c != Change.TextFormat);
        flags.ItemCheck += (_, _) => { if (!binding && IsHandleCreated && !IsDisposed) BeginInvoke(new Action(() => { if (!IsDisposed) LerCompareRuntime.Refresh(); })); };
        root.Controls.Add(flags, 0, 4);
        var filters = Flow();
        utility.Items.Add("All utilities");
        utility.SelectedIndex = 0;
        filters.Controls.AddRange(new Win.Control[] { utility, search, Label("Categories combine with OR") });
        root.Controls.Add(filters, 0, 5);
        utility.SelectedIndexChanged += (_, _) => { if (!binding) LerCompareRuntime.Refresh(); };
        search.TextChanged += (_, _) => { if (!binding) LerCompareRuntime.Refresh(); };
        var toggles = Flow();
        toggles.Controls.AddRange(new Win.Control[] { oldGeometry, newGeometry, vertices, selectedOnly });
        root.Controls.Add(toggles, 0, 6);
        foreach (var cb in new[] { oldGeometry, newGeometry, vertices, selectedOnly })
            cb.CheckedChanged += (_, _) => LerCompareRuntime.Refresh();
        var split = new Win.SplitContainer { Size = new Drawing.Size(500, 400), Dock = Win.DockStyle.Fill, Orientation = Win.Orientation.Horizontal, Panel1MinSize = 70, Panel2MinSize = 50, SplitterDistance = 220 };
        split.Panel1.Controls.Add(grid);
        split.Panel2.Controls.Add(detail);
        root.Controls.Add(split, 0, 7);
        foreach (var (name, width) in new[] { ("#", 40), ("Changes", 170), ("Utility", 90), ("Owner", 180), ("Old", 75), ("New", 75) })
            grid.Columns.Add(new Win.DataGridViewTextBoxColumn { HeaderText = name, Width = width, SortMode = Win.DataGridViewColumnSortMode.NotSortable });
        grid.CellValueNeeded += OnCellValueNeeded;
        grid.SelectionChanged += (_, _) => { ShowDetails(); if (selectedOnly.Checked && !binding) LerCompareRuntime.Refresh(); };
        grid.CellDoubleClick += (_, _) => LerCompareRuntime.Zoom(Selected);
        root.Controls.Add(new Win.Label { Text = "New green • Missing red • Status yellow • Material cyan • Geometry blue • Review magenta • Coverage orange", Dock = Win.DockStyle.Fill, Font = new Drawing.Font("Segoe UI", 8) }, 0, 8);
        // This pair is a starting suggestion; Browse works with any LER drawing.
        oldPath.Text = @"X:\117-1595 - VF - E2 - Lyngby - Dokumenter\01 Intern\02 Tegninger\06 LER\LER 2.0\7.24.9\00 Arkiv\2DLER.dwg";
    }
    private static Win.Label Label(string text) => new() { Text = text, AutoSize = true, Margin = new Win.Padding(3, 6, 3, 3) };
    private static Win.FlowLayoutPanel Flow() => new() { Dock = Win.DockStyle.Fill, AutoScroll = true, WrapContents = true };
    public void SetDocument(string path) => newPath.Text = path;
    public Settings Settings() => new(oldPath.Text, new Options { ToleranceMeters = (double)tolerance.Value / 100, MatchRadiusMeters = (double)radius.Value, UseCoverage = coverage.Checked, CoverageLayer = coverageLayer.Text.Trim() }, units.SelectedIndex == 0 ? CompareOption<double>.None : CompareOption<double>.Some(units.SelectedIndex switch { 1 => 1d, 2 => .001, 3 => .01, _ => 1d }));
    public void Busy(bool value, string text)
    {
        compare.Enabled = !value;
        status.Text = text;
        status.Refresh();
    }
    public void Bind(Report r)
    {
        binding = true;
        report = CompareOption<Report>.Some(r);
        export.Enabled = true;
        var counts = r.Counts;
        for (int i = 0; i < flagValues.Count; i++)
            flags.Items[i] = $"{FlagLabel(flagValues[i])} ({counts[flagValues[i].ToString()]:N0})";
        utility.Items.Clear();
        utility.Items.Add("All utilities");
        foreach (var u in r.Results.Select(r => r.Utility).Distinct().Order())
            utility.Items.Add(u);
        utility.SelectedIndex = 0;
        binding = false;
        Filter();
    }
    public void ClearReport()
    {
        binding = true;
        report = CompareOption<Report>.None;
        VisibleResults = new();
        grid.RowCount = 0;
        detail.Clear();
        export.Enabled = false;
        binding = false;
    }

    private void OnCellValueNeeded(object? sender, Win.DataGridViewCellValueEventArgs args)
    {
        if (args.RowIndex < 0 || args.RowIndex >= VisibleResults.Count)
            return;
        var row = VisibleResults[args.RowIndex];
        args.Value = args.ColumnIndex switch
        {
            0 => row.Number,
            1 => row.Flags,
            2 => row.Utility,
            3 => row.Owner,
            4 => row.OldHandles,
            5 => row.NewHandles,
            _ => ""
        };
    }

    public void Filter() => report.Match(current =>
    {
        int previous = Selected.Match(row => row.Number, () => -1);
        binding = true;
        try
        {
            Change mask = Change.None;
            for (int index = 0; index < flagValues.Count; index++)
                if (flags.GetItemChecked(index))
                    mask |= flagValues[index];
            string query = search.Text;
            var selectedUtility = utility.SelectedIndex > 0
                ? CompareOption<string>.Some(utility.Text) : CompareOption<string>.None;
            VisibleResults = current.Results.Where(row => (row.Flags & mask) != 0
                && selectedUtility.Match(value => row.Utility == value, () => true)
                && (query.Length == 0 || string.Join(" ", new[] { row.Owner, row.OldHandles, row.NewHandles, row.Note }
                    .Concat(row.Differences.Select(difference => difference.Property + " " + difference.OldValue + " " + difference.NewValue)))
                    .Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
            grid.SuspendLayout();
            grid.RowCount = VisibleResults.Count;
            int indexToSelect = VisibleResults.FindIndex(row => row.Number == previous);
            if (indexToSelect < 0 && VisibleResults.Count > 0)
                indexToSelect = 0;
            if (indexToSelect >= 0)
                grid.CurrentCell = grid.Rows[indexToSelect].Cells[0];
            grid.Invalidate();
        }
        finally
        {
            grid.ResumeLayout();
            binding = false;
        }
        ShowDetails();
        status.Text = $"{VisibleResults.Count:N0} / {current.Results.Count:N0} groups shown; {current.AdministrativeOnly:N0} only IDs/delivery/text format. Counts overlap.";
        return true;
    }, () => false);

    private void ShowDetails() => report.Match(current => Selected.Match(row =>
    {
        var text = new System.Text.StringBuilder();
        text.AppendLine($"Result {row.Number}: {row.Flags}");
        text.AppendLine($"Match: {row.Match}");
        text.AppendLine(row.Note);
        text.AppendLine();
        foreach (var pipe in row.CandidateOld.Concat(row.CandidateNew))
        {
            text.AppendLine($"CANDIDATE {(row.CandidateOld.Contains(pipe) ? "OLD" : "NEW")} ?{pipe.Handle}: {pipe.Layer}; identity unresolved");
            foreach (var property in pipe.Properties.OrderBy(property => property.Key))
                text.AppendLine($"  {property.Key}: {property.Value}");
            text.AppendLine();
        }
        foreach (var difference in row.Differences)
            text.AppendLine($"[{difference.Category}] {difference.Property}: {(difference.OldPresent ? difference.OldValue : "<absent>")} → {(difference.NewPresent ? difference.NewValue : "<absent>")}");
        if (row.Differences.Count == 0)
            foreach (var pipe in row.Old.Concat(row.New))
            {
                text.AppendLine($"{(row.Old.Contains(pipe) ? "OLD" : "NEW")} handle {pipe.Handle}: {pipe.Layer}");
                foreach (var property in pipe.Properties.OrderBy(property => property.Key))
                    text.AppendLine($"  {property.Key}: {property.Value}");
            }
        text.AppendLine();
        foreach (var warning in current.Warnings)
            text.AppendLine("Note: " + warning);
        detail.Text = text.ToString();
        return true;
    }, () => { detail.Text = string.Join(Environment.NewLine, current.Warnings); return false; }),
        () => { detail.Clear(); return false; });

    private void Export() => report.Match(current =>
    {
        using var dialog = new Win.SaveFileDialog
        {
            Filter = "JSON report (full geometry and Property Sets)|*.json|CSV change list|*.csv",
            FileName = "LER-comparison.json"
        };
        if (dialog.ShowDialog() != Win.DialogResult.OK)
            return false;
        try
        {
            if (dialog.FilterIndex == 2)
                current.WriteCsv(dialog.FileName);
            else
                current.WriteJson(dialog.FileName);
            return true;
        }
        catch (System.Exception error)
        {
            Win.MessageBox.Show(error.Message, "Export failed");
            return false;
        }
    }, () => false);

    private static string FlagLabel(Change flag)
    {
        if (flag == Change.New)
            return "New / unmatched";
        if (flag == Change.Missing)
            return "Missing / unmatched";
        return flag.ToString();
    }
}
