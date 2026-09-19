using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using System.Collections.Generic;

namespace IntersectUtilities.NdhTrace;

internal enum LegacyPortRole
{
    /// <summary>A port of the part's main run (or, on a stud or svanehals, where it sits on the main).</summary>
    Main,
    /// <summary>The port the branch pipe meets.</summary>
    Branch,
}

/// <summary>One port of a legacy component, in world coordinates.</summary>
internal readonly record struct LegacyPort(Point2d Position, LegacyPortRole Role);

/// <summary>
/// The ports of a legacy FJV component block: its nested MuffeIntern blocks,
/// the ones whose name holds "BRANCH" being branch ports. This is the rule the
/// legacy graph writer (GraphWrite.Graph.AddEntityToPOIs) uses, read here as
/// typed ports instead of graph points.
/// </summary>
internal static class LegacyPortReader
{
    public static List<LegacyPort> Read(BlockReference br, Transaction tx)
    {
        List<LegacyPort> ports = new List<LegacyPort>();
        BlockTableRecord btr = (BlockTableRecord)tx.GetObject(br.BlockTableRecord, OpenMode.ForRead);
        foreach (ObjectId id in btr)
        {
            if (tx.GetObject(id, OpenMode.ForRead) is not BlockReference nested) continue;
            if (!nested.Name.Contains("MuffeIntern")) continue;

            Point3d world = nested.Position.TransformBy(br.BlockTransform);
            ports.Add(new LegacyPort(
                new Point2d(world.X, world.Y),
                nested.Name.Contains("BRANCH") ? LegacyPortRole.Branch : LegacyPortRole.Main));
        }
        return ports;
    }
}
