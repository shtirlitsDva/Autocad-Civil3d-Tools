using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Manages all fillet strategies using Strategy pattern
    /// </summary>
    public class FilletStrategyManager
    {
        private readonly List<IFilletStrategy> _strategies;
        public FilletStrategyManager()
        {
            _strategies = new List<IFilletStrategy>
            {
                new FilletStrategyLineToLine()
                // Additional strategies can be added here
            };
        }

        public void RegisterStrategy(IFilletStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            
            _strategies.Add(strategy);
        }

        public IFilletStrategy? GetStrategy(IPolylineSegment segment1, IPolylineSegment segment2)
        {
            return _strategies.FirstOrDefault(s => s.CanHandle(segment1, segment2));
        }

        public IReadOnlyList<IFilletStrategy> GetAllStrategies()
        {
            return _strategies.AsReadOnly();
        }
    }
}
