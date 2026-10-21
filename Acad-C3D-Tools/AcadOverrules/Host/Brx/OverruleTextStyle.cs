using Teigha.GraphicsInterface;

namespace AcadOverrules
{
    /// <summary>
    /// The text style the overrules draw their labels with. BricsCAD's constructor has no
    /// strikethrough flag; otherwise the arguments are AutoCAD's (Host\Acad).
    /// </summary>
    internal static class OverruleTextStyle
    {
        public static TextStyle Arial(double textSize) =>
            new TextStyle(
                "Arial", "Arial", textSize, 0.0, 0.0, 0.0,
                false, false, false, false, false, "MyStd");
    }
}
