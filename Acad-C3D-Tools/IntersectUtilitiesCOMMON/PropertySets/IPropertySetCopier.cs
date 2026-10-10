#if BRICSCAD
using Teigha.DatabaseServices;
#else
using Autodesk.AutoCAD.DatabaseServices;
#endif

namespace IntersectUtilities
{
    /// <summary>
    /// Copies every property set on one entity to another, with its values. A host
    /// brings its own: IntersectUtilities the AEC API (CivilPropertySetCopier),
    /// NorsynDrawingTools on BricsCAD the AEC filer stream (AecPropertySetCopier).
    /// </summary>
    public interface IPropertySetCopier
    {
        /// <summary>
        /// Attaches to <paramref name="target"/> each set <paramref name="source"/> has,
        /// with the same values. Both entities are in the same database, the target
        /// already appended, and the caller's top transaction holds both. Throws when a
        /// set cannot be copied; the caller aborts the transaction.
        /// </summary>
        /// <param name="source">The entity whose sets are copied.</param>
        /// <param name="target">The entity that receives them.</param>
        void CopyAll(Entity source, Entity target);
    }
}
