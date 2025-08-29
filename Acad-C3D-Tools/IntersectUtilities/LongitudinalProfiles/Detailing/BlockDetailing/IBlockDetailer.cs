using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Contract for creating detailing blocks for a given source component block.
    /// </summary>
    public interface IBlockDetailer
    {
        /// <summary>
        /// Returns true if this detailer can handle the given source block.
        /// </summary>
        bool CanHandle(BlockReference sourceBlock, BlockDetailingContext context);

        /// <summary>
        /// Creates the detailing representation for the source block on the profile view.
        /// Implementations must perform all necessary checks (e.g., station within PV range).
        /// </summary>
        void Detail(BlockReference sourceBlock, BlockDetailingContext context);
    }
}


