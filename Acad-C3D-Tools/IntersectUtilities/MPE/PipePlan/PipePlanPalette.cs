using Autodesk.AutoCAD.Windows;
using System.Globalization;
using System.Windows.Forms;

namespace IntersectUtilities.MPE.PipePlan;

internal sealed class PipePlanPalette : IDisposable
{
    private readonly PaletteSet _paletteSet;
    private readonly PipePlanPaletteControl _control;

    public PipePlanPalette(PipePlanState state)
    {
        _paletteSet = new PaletteSet("PIPEPLAN")
        {
            Style = PaletteSetStyles.ShowAutoHideButton |
                    PaletteSetStyles.ShowCloseButton |
                    PaletteSetStyles.ShowPropertiesMenu,
            MinimumSize = new System.Drawing.Size(300, 420),
            Size = new System.Drawing.Size(320, 460),
            KeepFocus = false
        };

        _control = new PipePlanPaletteControl(state);
        _paletteSet.Add("Settings", _control);
        _control.UpdateFromState();
    }

    public void Dispose()
    {
        _paletteSet.Visible = false;
        _control.Dispose();
        _paletteSet.Dispose();
    }

    public void Show()
    {
        _paletteSet.Visible = true;
    }

    public void UpdateFromState()
    {
        _control.UpdateFromState();
    }

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        _control.SetStatus(message, kind);
    }
}

internal sealed class PipePlanPaletteControl : UserControl
{
    private readonly PipePlanState _state;
    private readonly ComboBox _sizeSelector = new();
    private readonly Dictionary<string, TextBox> _radiusInputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label _statusLabel = new();
    private readonly Label _selectedRadiusLabel = new();
    private readonly TextBox _straightSnapToleranceInput = new();
    private readonly Button _applyButton = new();
    private bool _isUpdating;

    public PipePlanPaletteControl(PipePlanState state)
    {
        _state = state;
        Dock = DockStyle.Fill;
        BuildUi();
    }

    public void UpdateFromState()
    {
        _isUpdating = true;

        if (_sizeSelector.Items.Count == 0)
        {
            foreach (PipeSizeOption size in _state.Sizes)
            {
                _sizeSelector.Items.Add(size);
            }

            if (_sizeSelector.Items.Count > 0)
            {
                _sizeSelector.SelectedIndex = 0;
            }
        }

        PipeSizeOption? activeSize = _state.GetSelectedSize();
        if (activeSize is not null && !Equals(_sizeSelector.SelectedItem, activeSize))
        {
            _sizeSelector.SelectedItem = activeSize;
        }

        foreach (PipeSizeOption size in _state.Sizes)
        {
            if (_radiusInputs.TryGetValue(size.Name, out TextBox? input) && input.Text != size.RadiusText)
            {
                input.Text = size.RadiusText;
            }
        }

        if (_straightSnapToleranceInput.Text != _state.StraightSnapToleranceText)
        {
            _straightSnapToleranceInput.Text = _state.StraightSnapToleranceText;
        }

        UpdateSelectedRadiusLabel();
        _isUpdating = false;
    }

    public void SetStatus(string message, PipePlanStatusKind kind)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = kind switch
        {
            PipePlanStatusKind.Ok => System.Drawing.Color.FromArgb(0, 120, 40),
            PipePlanStatusKind.Snap => System.Drawing.Color.FromArgb(30, 120, 220),
            PipePlanStatusKind.Warning => System.Drawing.Color.FromArgb(180, 120, 0),
            PipePlanStatusKind.Error => System.Drawing.Color.FromArgb(190, 30, 45),
            _ => System.Drawing.Color.FromArgb(60, 60, 60)
        };
    }

    private void BuildUi()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(12),
            AutoSize = true
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label intro = new()
        {
            AutoSize = true,
            Text = "Set the bend radius for each size, choose the active size, then run PPDRAW to draft.",
            MaximumSize = new System.Drawing.Size(280, 0)
        };
        root.Controls.Add(intro, 0, 0);

        FlowLayoutPanel selectorPanel = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };

        Label selectorLabel = new()
        {
            AutoSize = true,
            Text = "Active pipe size"
        };
        _sizeSelector.DropDownStyle = ComboBoxStyle.DropDownList;
        _sizeSelector.Width = 200;
        _sizeSelector.SelectedIndexChanged += (_, _) =>
        {
            UpdateSelectedRadiusLabel();
        };
        selectorPanel.Controls.Add(selectorLabel);
        selectorPanel.Controls.Add(_sizeSelector);
        selectorPanel.Controls.Add(_selectedRadiusLabel);
        root.Controls.Add(selectorPanel, 0, 1);

        GroupBox radiiGroup = new()
        {
            Text = "Minimum bend radius by size",
            Dock = DockStyle.Fill
        };

        TableLayoutPanel radiiTable = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = _state.Sizes.Count,
            Padding = new Padding(8)
        };
        radiiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        radiiTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        for (int i = 0; i < _state.Sizes.Count; i++)
        {
            PipeSizeOption size = _state.Sizes[i];

            Label sizeLabel = new()
            {
                AutoSize = true,
                Text = size.Name,
                Anchor = AnchorStyles.Left
            };

            TextBox radiusInput = new()
            {
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                Width = 140,
                Text = size.RadiusText
            };
            radiusInput.TextChanged += (_, _) =>
            {
                if (_isUpdating)
                {
                    return;
                }

                UpdateSelectedRadiusLabel();
            };

            _radiusInputs[size.Name] = radiusInput;
            radiiTable.Controls.Add(sizeLabel, 0, i);
            radiiTable.Controls.Add(radiusInput, 1, i);
        }

        radiiGroup.Controls.Add(radiiTable);
        root.Controls.Add(radiiGroup, 0, 2);

        FlowLayoutPanel snapPanel = new()
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            AutoSize = true,
            WrapContents = false
        };

        Label snapLabel = new()
        {
            AutoSize = true,
            Text = "Straight snap tolerance"
        };

        _straightSnapToleranceInput.Width = 120;
        _straightSnapToleranceInput.Text = _state.StraightSnapToleranceText;
        _straightSnapToleranceInput.TextChanged += (_, _) =>
        {
            if (_isUpdating)
            {
                return;
            }

            UpdateSelectedRadiusLabel();
        };

        snapPanel.Controls.Add(snapLabel);
        snapPanel.Controls.Add(_straightSnapToleranceInput);
        root.Controls.Add(snapPanel, 0, 3);

        _applyButton.AutoSize = true;
        _applyButton.Text = "Apply Settings";
        _applyButton.Click += (_, _) => ApplySettings();
        root.Controls.Add(_applyButton, 0, 4);

        Label help = new()
        {
            AutoSize = true,
            MaximumSize = new System.Drawing.Size(280, 0),
            Text = "Adjust the values, then click Apply Settings. PPDRAW starts drawing immediately after that. Blue means Ctrl straight snap is active."
        };
        root.Controls.Add(help, 0, 5);

        _statusLabel.AutoSize = true;
        _statusLabel.MaximumSize = new System.Drawing.Size(280, 0);
        _statusLabel.Text = "Set radii and run PPDRAW.";
        root.Controls.Add(_statusLabel, 0, 6);

        Controls.Add(root);
    }

    private void ApplySettings()
    {
        foreach (PipeSizeOption size in _state.Sizes)
        {
            if (_radiusInputs.TryGetValue(size.Name, out TextBox? input))
            {
                size.RadiusText = input.Text.Trim();
            }
        }

        _state.StraightSnapToleranceText = _straightSnapToleranceInput.Text.Trim();

        if (_sizeSelector.SelectedItem is PipeSizeOption selected)
        {
            _state.SetSelectedSize(selected.Name);
        }

        UpdateSelectedRadiusLabel();
        _state.RefreshDraftPreview();
        _state.SetStatus("Settings applied.", PipePlanStatusKind.Ok);
    }

    private void UpdateSelectedRadiusLabel()
    {
        PipeSizeOption? size = _state.GetSelectedSize();
        if (size is null)
        {
            _selectedRadiusLabel.Text = "Radius: -";
            return;
        }

        if (size.TryGetRadius(out double radius))
        {
            _selectedRadiusLabel.Text = $"Radius: {radius.ToString("0.###", CultureInfo.InvariantCulture)}";
        }
        else
        {
            _selectedRadiusLabel.Text = "Radius: invalid";
        }
    }
}
