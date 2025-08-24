using Autodesk.AutoCAD.Windows;

using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.App.Contracts;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Models;
using IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.ViewModels;

using System;
using System.Drawing;
using System.IO;
using System.Text.Json;

namespace IntersectUtilities.FjernvarmeFremtidig.VejkantOffset.UI.Views
{
	internal sealed class OffsetPaletteViewModelVisualizer : IVisualizer<IntersectionVisualizationModel>
	{
		private static PaletteSet? _palette;
		private static OffsetPaletteView? _view;
		private static IntersectionVisualizationViewModel? _cachedVm;
        private readonly string _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IntersectUtilities", "VejkantOffset", "PaletteSettings.json");

        private sealed class PaletteSettings
        {
            public int X { get; set; }
            public int Y { get; set; }
            public int Width { get; set; }
            public int Height { get; set; }
            public int Dock { get; set; }
        }

        private void EnsureSettingsFolder()
        {
            var dir = Path.GetDirectoryName(_settingsPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private void ApplySavedSettings()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                var json = File.ReadAllText(_settingsPath);
                var s = JsonSerializer.Deserialize<PaletteSettings>(json);
                if (s == null) return;
                _palette!.Size = new Size(Math.Max(300, s.Width), Math.Max(200, s.Height));
                _palette.Location = new Point(Math.Max(0, s.X), Math.Max(0, s.Y));
                if (Enum.IsDefined(typeof(DockSides), s.Dock))
                    _palette.Dock = (DockSides)s.Dock;
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                if (_palette == null) return;
                EnsureSettingsFolder();
                var s = new PaletteSettings
                {
                    X = _palette.Location.X,
                    Y = _palette.Location.Y,
                    Width = _palette.Size.Width,
                    Height = _palette.Size.Height,
                    Dock = (int)_palette.Dock
                };
                var json = JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsPath, json);
            }
            catch { }
        }

		public void Show()
		{
			if (_palette == null)
			{
				_palette = new PaletteSet("Vejkant Offset")
				{
					// Remove AutoHide button to avoid accidental roll-up on Esc
					Style = PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowCloseButton
				};
				_view = new OffsetPaletteView();
				// Host WPF directly
				_palette.AddVisual("Inspector", _view);
				_palette.DockEnabled = DockSides.Left | DockSides.Right | DockSides.None;
				_palette.KeepFocus = true; // revert: keep palette focus behavior as before
				_palette.StateChanged += (s, e) =>
				{
					IntersectUtilities.UtilsCommon.Utils.prdDbg(
                        $"[Palette] StateChanged -> {_palette.WindowState}");
				};
				ApplySavedSettings();
				if (_cachedVm == null && _view?.DataContext is IntersectionVisualizationViewModel vm0)
				{
					_cachedVm = vm0;
				}
			}
			_palette.Visible = true;
			IntersectUtilities.UtilsCommon.Utils.prdDbg("[Palette] Show()");
		}

		public void Update(IntersectionVisualizationModel model)
		{
			if (_palette == null || _view == null) Show();
			var vm = _cachedVm ?? (_view?.DataContext as IntersectionVisualizationViewModel);
			if (vm != null)
			{
				vm.UpdateVisualization(model);
				IntersectUtilities.UtilsCommon.Utils.prdDbg($"[Palette] Update() - Intersections: {model?.Intersections?.Count ?? 0}, WorkingLen: {model?.WorkingLine?.DisplayLength ?? 0:0.0}");
			}
		}

		public void Hide()
		{
			if (_palette != null)
			{
				SaveSettings();
				_palette.Visible = false;
				IntersectUtilities.UtilsCommon.Utils.prdDbg("[Palette] Hide()");
			}
		}
	}
}



