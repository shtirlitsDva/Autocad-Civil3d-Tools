using System.Windows.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace DimensioneringV2.BBRData.Models
{
    /// <summary>
    /// Per-cell comparison state for one transfer mapping in one display row.
    /// Holds both display values and a background brush reflecting comparison result.
    /// </summary>
    internal partial class TransferCellData : ObservableObject
    {
        [ObservableProperty]
        private string _bbrValue = string.Empty;

        [ObservableProperty]
        private string _csvValue = string.Empty;

        [ObservableProperty]
        private SolidColorBrush _cellBackground;

        public TransferCellData()
        {
            _cellBackground = TransferCellBrushes.Neutral;
        }
    }

    /// <summary>
    /// Centralized brush definitions for transfer cell comparison states.
    /// All brushes are frozen for WPF performance.
    /// </summary>
    internal static class TransferCellBrushes
    {
        /// <summary>Pre-transfer: values are equal — no write needed.</summary>
        public static readonly SolidColorBrush PreTransferSame =
            CreateFrozen(0x2D, 0x4A, 0x2D);

        /// <summary>Pre-transfer: values differ — will be written.</summary>
        public static readonly SolidColorBrush PreTransferDifferent =
            CreateFrozen(0x4A, 0x4A, 0x2D);

        /// <summary>Post-transfer: write succeeded — values now match.</summary>
        public static readonly SolidColorBrush PostTransferSuccess =
            CreateFrozen(0x2D, 0x6A, 0x2D);

        /// <summary>Post-transfer: write failed — values still differ.</summary>
        public static readonly SolidColorBrush PostTransferFailure =
            CreateFrozen(0x5A, 0x2D, 0x2D);

        /// <summary>Non-1:1 groups or no comparison available.</summary>
        public static readonly SolidColorBrush Neutral =
            CreateFrozen(0x33, 0x33, 0x33);

        private static SolidColorBrush CreateFrozen(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
