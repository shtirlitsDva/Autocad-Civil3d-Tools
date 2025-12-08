using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DimensioneringV2.Common
{
    /// <summary>
    /// Property definition for recursive sum calculation.
    /// Supports two usage patterns:
    /// <list type="number">
    /// <item><description>Connected → Supplied: Getter reads source property (e.g., NumberOfBuildingsConnected), 
    /// Setter writes to different target property (e.g., NumberOfBuildingsSupplied)</description></item>
    /// <item><description>Same property: Getter/Setter both operate on same property (e.g., KarFlowHeatSupply).
    /// Value must be pre-calculated at leaf edges before the recursive sum runs.</description></item>
    /// </list>
    /// </summary>
    /// <param name="Getter">Reads the initial value (only called at leaf edges)</param>
    /// <param name="Setter">Writes the accumulated sum (called on all edges)</param>
    internal readonly record struct SumProperty<T>(Func<T, double> Getter, Action<T, double> Setter);
}
