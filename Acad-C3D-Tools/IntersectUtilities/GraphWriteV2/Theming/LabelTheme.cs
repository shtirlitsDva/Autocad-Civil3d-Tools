namespace IntersectUtilities.GraphWriteV2.Theming;

public enum LabelStyle { Glass, Blueprint, Segmented, Fluent, Terminal }
public enum SegmentMode { Flush, Pop, Bordered }
public enum SerieFill { Filled, Outline }
public enum SeriePos { After, Before }

/// <summary>
/// The complete, persisted configuration for the GRAPHWRITEV2 node labels. One LabelTheme is what
/// <see cref="DotStyling.ThemedHtmlStyler"/> reads when it emits each node's Graphviz HTML-like label.
/// </summary>
public sealed class LabelTheme
{
    public LabelStyle Style { get; set; } = LabelStyle.Glass;
    public SegmentMode TileMode { get; set; } = SegmentMode.Pop; // only used when Style == Segmented
    public ThemeColors Colors { get; set; } = new();
    public SeriesStyle Series { get; set; } = new();
    public FontSet Fonts { get; set; } = new();
    public int Padding { get; set; } = 9; // pt, 4..14

    public LabelTheme Clone() => new()
    {
        Style = Style,
        TileMode = TileMode,
        Colors = Colors.Clone(),
        Series = Series.Clone(),
        Fonts = Fonts.Clone(),
        Padding = Padding,
    };
}

/// <summary>
/// The full label palette. The designer window only exposes six of these for direct editing
/// (Frame, Fill1, Fill2, Id, Body, Divider) plus the three Serie colors; the rest carry the values
/// of the selected preset so that the Fluent (FillLite*) and Segmented "Pop" (TileA*, Ink, IdInk)
/// styles keep working even when the visible colors are hand-tuned. All fields are persisted so a
/// saved theme round-trips losslessly regardless of which style it was last edited under.
/// </summary>
public sealed class ThemeColors
{
    public string Background { get; set; } = "#0a131c";
    public string Fill1 { get; set; } = "#15273a"; // panel gradient top
    public string Fill2 { get; set; } = "#1d3650"; // panel gradient bottom
    public string FillLite1 { get; set; } = "#243a52"; // Fluent gradient top
    public string FillLite2 { get; set; } = "#2e4865"; // Fluent gradient bottom
    public string Frame { get; set; } = "#48d6e8"; // border
    public string Divider { get; set; } = "#2f5f74";
    public string Id { get; set; } = "#7df0ff";
    public string Type { get; set; } = "#9fc3d6";
    public string Body { get; set; } = "#e3f1f8"; // "desc" in the design package
    public string SerieText { get; set; } = "#04121a"; // ink on a filled Serie plaque
    public string Serie1 { get; set; } = "#38bdf8"; // Serie I
    public string Serie2 { get; set; } = "#fbbf24"; // Serie II
    public string Serie3 { get; set; } = "#f472b6"; // Serie III
    public string TileA1 { get; set; } = "#cfe4f0"; // Segmented "Pop" light tile top
    public string TileA2 { get; set; } = "#e8f3fa"; // Segmented "Pop" light tile bottom
    public string Ink { get; set; } = "#0c1a26"; // Segmented "Pop" dark body text
    public string IdInk { get; set; } = "#0d5f7e"; // Segmented "Pop" dark id text

    public string Serie(int series) => series switch { 1 => Serie1, 2 => Serie2, 3 => Serie3, _ => Serie1 };

    public ThemeColors Clone() => (ThemeColors)MemberwiseClone();
}

public sealed class SeriesStyle
{
    public bool Show { get; set; } = true;
    public SerieFill Fill { get; set; } = SerieFill.Filled;
    public SeriePos Pos { get; set; } = SeriePos.After;

    public SeriesStyle Clone() => (SeriesStyle)MemberwiseClone();
}

public sealed class FontSet
{
    public string Id { get; set; } = "Courier New"; // monospace face, used for every text in the label

    public FontSet Clone() => (FontSet)MemberwiseClone();
}
