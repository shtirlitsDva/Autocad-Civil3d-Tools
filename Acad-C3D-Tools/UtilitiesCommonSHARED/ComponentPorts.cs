using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.UtilsCommon
{
    /// <summary>What a component's port is for, as its MuffeIntern block's name says.</summary>
    public enum ComponentPortRole
    {
        /// <summary>The name says neither MAIN nor BRANCH: a plain port (a pipe end, a reducer end).</summary>
        Neutral,
        /// <summary>The name holds MAIN: a port of the main run (a stud's or svanehals's seat on the main).</summary>
        Main,
        /// <summary>The name holds BRANCH: the port the branch pipe meets.</summary>
        Branch,
    }

    /// <summary>
    /// One port of an FJV component block, in the coordinates of the space the
    /// block is inserted in. <paramref name="Tag"/> is the MuffeIntern block's name.
    /// </summary>
    public readonly record struct ComponentPort(Point3d Position, ComponentPortRole Role, string Tag);

    /// <summary>
    /// The ports of an FJV component block: the nested blocks whose name holds
    /// "MuffeIntern" (inside an xref the name is qualified, "xref|MuffeIntern",
    /// which the rule reads the same), placed by the block's transform. The one
    /// reader of that convention; every tool that needs a component's ports
    /// asks it.
    /// </summary>
    public static class ComponentPorts
    {
        private const string PortBlockName = "MuffeIntern";
        private static readonly RXClass BlockReferenceClass = RXObject.GetClass(typeof(BlockReference));

        public static List<ComponentPort> Read(BlockReference component, Transaction tx)
        {
            List<ComponentPort> ports = new List<ComponentPort>();
            BlockTableRecord btr = (BlockTableRecord)tx.GetObject(component.BlockTableRecord, OpenMode.ForRead);
            foreach (ObjectId id in btr)
            {
                //Only block references are opened; the block's curves are skipped by class.
                if (!id.ObjectClass.IsDerivedFrom(BlockReferenceClass)) continue;
                BlockReference nested = (BlockReference)tx.GetObject(id, OpenMode.ForRead);
                string name = nested.Name ?? "";
                if (!name.Contains(PortBlockName, StringComparison.OrdinalIgnoreCase)) continue;

                ports.Add(new ComponentPort(
                    nested.Position.TransformBy(component.BlockTransform), RoleOf(name), name));
            }
            return ports;
        }

        private static ComponentPortRole RoleOf(string name) =>
            name.Contains("BRANCH", StringComparison.OrdinalIgnoreCase) ? ComponentPortRole.Branch :
            name.Contains("MAIN", StringComparison.OrdinalIgnoreCase) ? ComponentPortRole.Main :
            ComponentPortRole.Neutral;
    }
}
