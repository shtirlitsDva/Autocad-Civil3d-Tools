using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    /// <summary>
    /// Manages all triage strategies using Strategy pattern
    /// </summary>
    internal class TriageStrategyManager
    {
        private readonly List<ITriageStrategy> _strategies;
        internal TriageStrategyManager()
        {
            _strategies = new List<ITriageStrategy>
            {
                new TriageStrategyArcLineS2Short(),
                new TriageStrategyLineArcS1Short(),
            };
        }

        internal void RegisterStrategy(ITriageStrategy strategy)
        {
            if (strategy == null)
                throw new ArgumentNullException(nameof(strategy));
            
            _strategies.Add(strategy);
        }

        internal ITriageStrategy? GetStrategy(
            IFilletStrategy filletStrategy, FilletFailureReason failureReason)
        {
            return _strategies.FirstOrDefault(s => s.CanHandle(filletStrategy, failureReason));
        }

        internal IReadOnlyList<ITriageStrategy> GetAllStrategies()
        {
            return _strategies.AsReadOnly();
        }        
    }
}
