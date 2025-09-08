using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon.Enums;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    /// <summary>
    /// Contract for creating detailing blocks for a given source component block.
    /// </summary>
    public interface IBlockDetailer
    {
        /// <summary>
        /// Creates the detailing representation for the source block on the profile view.
        /// Implementations must perform all necessary checks (e.g., station within PV range).
        /// </summary>
        void Detail(BlockReference sourceBlock, BlockDetailingContext context, PipelineElementType elementType);
    }
}


