using System;
using System.Collections.Generic;

using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;

using NTRExport.Enums;
using NTRExport.NtrConfiguration;
using NTRExport.SoilModel;

namespace NTRExport.Routing
{
    internal abstract class RoutedMember
    {
        protected RoutedMember(Handle source)
        {
            Source = source;
            Provenance = new[] { source };
        }
        public Handle Source { get; }
        public int Dn { get; set; }
        public string? Material { get; set; }
        public FlowRole Flow { get; set; } = FlowRole.Unknown;
        public IReadOnlyList<Handle> Provenance { get; init; }
            = Array.Empty<Handle>();
        public string DnSuffix { get; set; } = "s";
        public string LTG { get; init; } = "STD";
        public string Pipeline => LTG.IsNoE() ? string.Empty : " LTG=" + LTG;

        public string Last(ConfigurationData conf)
        {
            if (conf == null)
            {
                return string.Empty;
            }

            var record = Flow switch
            {
                FlowRole.Supply => conf.SupplyLast,
                FlowRole.Return => conf.ReturnLast,
                _ => null,
            };

            return record != null ? " " + record.EmitRecord() : string.Empty;
        }
    }

    internal sealed class RoutedStraight : RoutedMember
    {
        public RoutedStraight(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public SoilProfile Soil { get; set; } = SoilProfile.Default;        
    }

    internal sealed class RoutedBend : RoutedMember
    {
        public RoutedBend(Handle src) : base(src) { }
        public Point3d A { get; set; }
        public Point3d B { get; set; }
        public Point3d T { get; set; }
    }

    internal sealed class RoutedGraph
    {
        public List<RoutedMember> Members { get; } = new();
    }

    internal sealed class RoutedReducer : RoutedMember
    {
        public RoutedReducer(Handle src) : base(src) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }
        public int Dn1 { get; set; }
        public int Dn2 { get; set; }
        public string Dn1Suffix { get; set; } = "s";
        public string Dn2Suffix { get; set; } = "s";
    }

    internal sealed class RoutedTee : RoutedMember
    {
        public RoutedTee(Handle src) : base(src) { }
        public Point3d Ph1 { get; set; }
        public Point3d Ph2 { get; set; }
        public Point3d Pa1 { get; set; }
        public Point3d Pa2 { get; set; }
        public int DnBranch { get; set; }
        public string DnMainSuffix { get; set; } = "s";
        public string DnBranchSuffix { get; set; } = "s";
    }

    internal sealed class RoutedInstrument : RoutedMember
    {
        public RoutedInstrument(Handle src) : base(src) { }
        public Point3d P1 { get; set; }
        public Point3d P2 { get; set; }
        public Point3d Pm { get; set; }
        public int Dn1 { get; set; }
        public int Dn2 { get; set; }
        public string Dn1Suffix { get; set; } = "s";
        public string Dn2Suffix { get; set; } = "s";
    }
}


