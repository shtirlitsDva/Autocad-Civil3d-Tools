using Norsyn.AecPropertySets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.LongitudinalProfiles.Quality;

internal static class QaCrossingBlockSelection
{
    internal static Result<T> Find<T>(IEnumerable<(T Id, string Name)> blocks, string viewName)
    {
        var matches = blocks.Where(block => string.Equals(block.Name, viewName, StringComparison.OrdinalIgnoreCase))
            .Select(block => block.Id).ToArray();
        if (matches.Length == 0) return Result<T>.Failure($"No crossing/detailing block matches view '{viewName}' in its drawing space.");
        if (matches.Length > 1) return Result<T>.Failure($"Several crossing/detailing blocks match view '{viewName}' in its drawing space.");
        return Result<T>.Success(matches[0]);
    }
}
