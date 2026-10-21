using Autodesk.AutoCAD.GraphicsInterface;

namespace AcadOverrules
{
    /// <summary>
    /// The text style the overrules draw their labels with. AutoCAD's constructor takes a
    /// strikethrough flag that BricsCAD's lacks; the BricsCAD twin is Host\Brx.
    /// </summary>
    internal static class OverruleTextStyle
    {
        public static TextStyle Arial(double textSize) =>
            new TextStyle(
                "Arial", "Arial", textSize, 0.0, 0.0, 0.0,
                false, false, false, false, false, false, "MyStd");
    }
}
