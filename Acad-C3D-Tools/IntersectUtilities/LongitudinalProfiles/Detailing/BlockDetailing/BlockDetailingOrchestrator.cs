using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;

namespace IntersectUtilities.LongitudinalProfiles.Detailing.BlockDetailing
{
    public sealed class BlockDetailingOrchestrator
    {
        private readonly List<IBlockDetailer> _detailers;

        public BlockDetailingOrchestrator(IEnumerable<IBlockDetailer>? detailers = null)
        {
            _detailers = (detailers ?? GetDefaultDetailers()).ToList();
        }

        public void DetailAll(IEnumerable<BlockReference> sourceBlocks, BlockDetailingContext context)
        {
            foreach (var br in sourceBlocks)
            {
                foreach (var detailer in _detailers)
                {
                    if (!detailer.CanHandle(br, context))
                        continue;
                    detailer.Detail(br, context);
                    break;
                }
            }
        }

        private static IEnumerable<IBlockDetailer> GetDefaultDetailers()
        {
            // Order matters if handlers overlap. Place special cases first.
            yield return new BueRorDetailer();
            yield return new GenericComponentDetailer();
        }
    }
}


