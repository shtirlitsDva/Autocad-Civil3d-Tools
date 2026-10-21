#if BRICSCAD
using Teigha.DatabaseServices;

using AcApp = Bricscad.ApplicationServices;
#else
using Autodesk.AutoCAD.DatabaseServices;

using AcApp = Autodesk.AutoCAD.ApplicationServices;
#endif

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// Loads a linetype into the active drawing from the host's standard linetype file.
    ///
    /// <see cref="LinetypeResolver"/> draws Continuous for a linetype the drawing does not
    /// have, so a dashed default would silently draw solid in most drawings. Loading it when
    /// the user switches the feature on is what makes the default look as promised.
    /// Called from the settings window and the toggle command, never from a draw: a draw
    /// must not write to the database.
    /// </summary>
    internal static class LinetypeLoader
    {
        /// <summary>
        /// The imperial file on AutoCAD: its DASHED has a 0.5 unit dash, which reads well in
        /// these metre drawings. acadiso.lin's is 12.7 units - longer than most tangents.
        /// </summary>
#if BRICSCAD
        private const string LinetypeFile = "default.lin";
#else
        private const string LinetypeFile = "acad.lin";
#endif

        /// <summary>
        /// Makes sure <paramref name="name"/> is loaded in the active drawing. Does nothing
        /// for the empty name (the polyline's own linetype), for a name already loaded, and
        /// when there is no drawing. When the load fails the overrule draws Continuous, as it
        /// does for any linetype it cannot find.
        /// </summary>
        public static void EnsureLoaded(string? name)
        {
            string wanted = (name ?? string.Empty).Trim();
            if (wanted.Length == 0) return;

            AcApp.Document? doc = AcApp.Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            Database db = doc.Database;

            try
            {
                if (IsLoaded(db, wanted)) return;

                using (doc.LockDocument())
                    db.LoadLineTypeFile(wanted, LinetypeFile);
            }
            catch (System.Exception)
            {
                //The file is missing or does not define the name. Not worth an error: the
                //linetype can still be picked by hand from the ones the drawing has.
            }
        }

        private static bool IsLoaded(Database db, string name)
        {
            using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
            {
                var table = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
                return table.Has(name);
            }
        }
    }
}
