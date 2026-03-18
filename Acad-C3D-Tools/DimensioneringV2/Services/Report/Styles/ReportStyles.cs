using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using System.Globalization;

namespace DimensioneringV2.Services.Report.Styles;

/// <summary>
/// Centralized constants and helpers for consistent PDF styling.
/// </summary>
internal static class ReportStyles
{
    // Locale for numeric formatting (comma decimal, dot thousands)
    public static readonly CultureInfo DaDk = CultureInfo.GetCultureInfo("da-DK");

    // Page setup
    public static readonly PageSize PageSizeA4 = PageSizes.A4;
    public const float MarginLeft = 25f;
    public const float MarginRight = 20f;
    public const float MarginTop = 20f;
    public const float MarginBottom = 20f;

    // Fonts
    public const string FontFamily = "Segoe UI";
    public const float FontSizeTitle = 22f;
    public const float FontSizeH1 = 16f;
    public const float FontSizeH2 = 13f;
    public const float FontSizeH3 = 11f;
    public const float FontSizeBody = 9f;
    public const float FontSizeSmall = 8f;
    public const float FontSizeFooter = 7.5f;

    // Colors
    public const string ColorPrimary = "#1B4F72";
    public const string ColorSecondary = "#2E86C1";
    public const string ColorHeaderBg = "#D4E6F1";
    public const string ColorAlternateRowBg = "#F2F3F4";
    public const string ColorBorderLight = "#D5D8DC";
    public const string ColorPass = "#27AE60";
    public const string ColorFail = "#E74C3C";

    // Table helpers
    public const float TableCellPadding = 4f;
    public const float SectionSpacing = 12f;
}
