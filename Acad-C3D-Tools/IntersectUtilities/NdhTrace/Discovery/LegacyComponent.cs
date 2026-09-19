using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using System.Collections.Generic;
using System.Linq;

namespace IntersectUtilities.NdhTrace;

/// <summary>
/// A legacy component block as the connection reading needs it. The pipeline
/// properties are a snapshot taken before anything in the reading can repair
/// them (the graph writer fills in a svanehals's missing BelongsToAlignment).
/// </summary>
internal sealed record LegacyComponent(
    BlockReference Block,
    string Handle,
    string Navn,
    string Type,
    string SysNavn,
    LegacyPartRole Role,
    string BelongsTo,
    string BranchesOffTo,
    IReadOnlyList<ComponentPort> Ports)
{
    /// <summary>Where the branch pipe meets the part: the middle of its branch ports, else its insertion.</summary>
    public Point2d BranchPort
    {
        get
        {
            List<ComponentPort> branch = Ports.Where(p => p.Role == ComponentPortRole.Branch).ToList();
            if (branch.Count == 0) return new Point2d(Block.Position.X, Block.Position.Y);
            return new Point2d(branch.Average(p => p.Position.X), branch.Average(p => p.Position.Y));
        }
    }

    /// <summary>Whether any port, or the insertion, lies within <paramref name="tol"/> of <paramref name="p"/>.</summary>
    public bool Touches(Point2d p, double tol) =>
        new Point2d(Block.Position.X, Block.Position.Y).GetDistanceTo(p) <= tol ||
        Ports.Any(x => x.Position.To2d().GetDistanceTo(p) <= tol);
}

internal static class LegacyComponentReader
{
    /// <summary>Every legacy component block of the drawing, welds excepted, service connections included.</summary>
    public static List<LegacyComponent> Read(Database fjvDb, Transaction tx, PropertySetHelper psh)
    {
        List<LegacyComponent> result = new List<LegacyComponent>();
        foreach (BlockReference br in fjvDb.GetFjvBlocks(tx, true, false))
        {
            string type = br.ReadDynamicCsvProperty(DynamicProperty.Type, false);
            result.Add(new LegacyComponent(
                br,
                br.Handle.ToString(),
                br.RealName(),
                type,
                br.ReadDynamicCsvProperty(DynamicProperty.SysNavn, false),
                LegacyPartRoles.Of(type),
                psh.Pipeline.ReadPropertyString(br, psh.PipelineDef.BelongsToAlignment),
                psh.Pipeline.ReadPropertyString(br, psh.PipelineDef.BranchesOffToAlignment),
                ComponentPorts.Read(br, tx)));
        }
        return result;
    }
}
