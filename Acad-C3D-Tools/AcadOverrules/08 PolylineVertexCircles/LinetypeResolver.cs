using Autodesk.AutoCAD.DatabaseServices;

namespace AcadOverrules.VertexCircles
{
    /// <summary>
    /// Turns the linetype name stored in a profile into the <see cref="ObjectId"/> that
    /// <c>SubEntityTraits.LineType</c> wants.
    ///
    /// A profile is shared across drawings, so the stored name may not be loaded in the
    /// drawing currently being viewed. That case resolves to Continuous, which is present in
    /// every drawing - the circles keep drawing, only without the pattern. The fallback is
    /// deliberate and was chosen over hiding the circles.
    /// </summary>
    internal static class LinetypeResolver
    {
        /// <summary>
        /// The id of <paramref name="name"/> in the linetype table of <paramref name="db"/>,
        /// or the id of Continuous when the name is empty, missing or unreadable.
        /// <see cref="ObjectId.Null"/> only when even Continuous cannot be resolved, which
        /// callers must read as "leave the trait alone".
        /// </summary>
        public static ObjectId Resolve(Database? db, string? name)
        {
            if (db == null) return ObjectId.Null;

            //The default costs no transaction, and it is what most profiles use.
            string wanted = (name ?? string.Empty).Trim();
            if (wanted.Length == 0 ||
                string.Equals(wanted, VertexCirclesSettings.DefaultLinetype,
                    System.StringComparison.OrdinalIgnoreCase))
                return Continuous(db);

            try
            {
                //Open-close rather than a cache: a linetype can be loaded or purged while the
                //overrule is on, and a stale id would be drawn with or, worse, be erased.
                using (Transaction tr = db.TransactionManager.StartOpenCloseTransaction())
                {
                    if (tr.GetObject(db.LinetypeTableId, OpenMode.ForRead) is not LinetypeTable table)
                        return Continuous(db);

                    if (table.Has(wanted)) return table[wanted];
                }
            }
            catch (System.Exception)
            {
                //Never let a linetype lookup break the drawing of the entity.
            }

            return Continuous(db);
        }

        private static ObjectId Continuous(Database db)
        {
            try
            {
                return SymbolUtilityServices.GetLinetypeContinuousId(db);
            }
            catch (System.Exception)
            {
                return ObjectId.Null;
            }
        }
    }
}
