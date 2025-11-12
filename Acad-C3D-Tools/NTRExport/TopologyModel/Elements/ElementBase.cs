using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using IntersectUtilities;
using IntersectUtilities.PipeScheduleV2;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Routing;
using NTRExport.SoilModel;

namespace NTRExport.TopologyModel
{
    internal abstract class ElementBase
    {
        public Handle Source { get; }
        protected Entity _entity;

        protected ElementBase(Handle src)
        {
            Source = src;
            var db = Autodesk.AutoCAD.ApplicationServices.Core.Application
                .DocumentManager.MdiActiveDocument.Database;
            var tx = db.TransactionManager.TopTransaction;
            _entity = src.Go<Entity>(db);
        }

        public abstract IReadOnlyList<TPort> Ports { get; }
        public virtual PipeSystemEnum System => system();

        private PipeSystemEnum system()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSystem(_entity),
                BlockReference br => br.GetPipeSystemEnum(),
                _ => PipeSystemEnum.Ukendt,
            };
        }

        public virtual PipeTypeEnum Type => type();

        private PipeTypeEnum type()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeType(_entity),
                BlockReference br => br.GetPipeTypeEnum(),
                _ => PipeTypeEnum.Ukendt,
            };
        }

        public virtual PipeSeriesEnum Series => series();

        private PipeSeriesEnum series()
        {
            return _entity switch
            {
                Polyline _ => PipeScheduleV2.GetPipeSeriesV2(_entity),
                BlockReference br => br.GetPipeSeriesEnum(),
                _ => PipeSeriesEnum.Undefined,
            };
        }

        public abstract int DN { get; }
        public virtual string DotLabelForTest()
        {
            return $"{Source.ToString()} / {this.GetType().Name}\n" +
                $"{DnLabel()}";
        }
        public virtual string DnLabel() => DN.ToString();
        public virtual string Material
        {
            get
            {
                return System switch
                {
                    PipeSystemEnum.Stål => "P235GH",
                    _ => "Unknown",
                };
            }
        }
        public IPipeVariant Variant =>
            Type == PipeTypeEnum.Twin ? new TwinVariant() : new SingleVariant();

        protected virtual (double zUp, double zLow) ComputeTwinOffsets(
            PipeSystemEnum ps,
            PipeTypeEnum pt,
            int dn
        )
        {
            if (!Variant.IsTwin)
                return (0.0, 0.0);

            var odMm = PipeScheduleV2.GetPipeOd(ps, dn);
            var gapMm = PipeScheduleV2.GetPipeDistanceForTwin(ps, dn, pt);
            var z = Math.Max(0.0, odMm + gapMm) / 2000.0;
            return (z, -z);
        }

        // Combined traversal + routing: emit geometry and propagate elevation/slope
        public virtual List<(TPort exitPort, double exitZ, double exitSlope)> Route(
            RoutedGraph g, Topology topo, RouterContext ctx, TPort entryPort, double entryZ, double entrySlope)
        {
            var exits = new List<(TPort exitPort, double exitZ, double exitSlope)>();
            foreach (var p in Ports)
            {
                if (ReferenceEquals(p, entryPort)) continue;
                exits.Add((p, entryZ, entrySlope));
            }
            return exits;
        }

        internal virtual void AttachPropertySet()
        {
            //Defaults to no-op
        }
    }

    #region PipeVariant
    internal interface IPipeVariant
    {
        string DnSuffix { get; }
        bool IsTwin { get; }
    }

    internal sealed class SingleVariant : IPipeVariant
    {
        public string DnSuffix => "s";
        public bool IsTwin => false;
    }

    internal sealed class TwinVariant : IPipeVariant
    {
        public string DnSuffix => "t";
        public bool IsTwin => true;
    }
    #endregion
}

