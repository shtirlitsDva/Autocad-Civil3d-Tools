using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.UtilsCommon.Enums;

using System;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace IntersectUtilities.UtilsCommon.Graphs
{
    public class GraphEntity
    {
        public Entity Owner { get; }
        public Handle OwnerHandle { get; }
        public Con[] Cons { get; }
        private PSetDefs.DriGraph _driGraph = new();
        public PipelineElementType ElementType
        {
            get
            {
                switch (Owner)
                {
                    case Polyline pl:
                        return PipelineElementType.Pipe;
                    case BlockReference br:
                        return br.GetPipelineType();
                    default:
                        throw new NotImplementedException();
                }
            }
        }
        private PropertySetManager _pplPsm;
        private PSetDefs.DriPipelineData _ppl = new();
        public GraphEntity(
            Entity entity,
            PropertySetManager psm)
        {
            Owner = entity;
            OwnerHandle = Owner.Handle;
            string conString = psm.ReadPropertyString(entity, _driGraph.ConnectedEntities);
            if (conString.IsNoE()) throw new System.Exception(
                $"Constring is EMPTY! for entity: {Owner.Handle}.\nCheck connectivity.");
            Cons = Con.ParseConString(conString);

            _pplPsm = new PropertySetManager(
                Autodesk.AutoCAD.ApplicationServices.Core
                .Application.DocumentManager.MdiActiveDocument.Database,
                PSetDefs.DefinedSets.DriPipelineData);
        }
        public int LargestDn()
        {
            switch (Owner)
            {
                case Polyline pline:
                    return GetPipeDN(pline);
                case BlockReference br:
                    return Convert.ToInt32(
                        br.ReadDynamicCsvProperty(
                            DynamicProperty.DN1));
            }
            return 0;
        }

        public string Alignment =>
            _pplPsm.ReadPropertyString(Owner, _ppl.BelongsToAlignment) ?? string.Empty;

        public string TypeLabel => getTypeLabel();
        private string getTypeLabel()
        {
            return Owner switch
            {
                Polyline pl => $"Rør L{pl.Length.ToString("0.##")}",
                BlockReference br => br.ReadDynamicCsvProperty(DynamicProperty.Type),
                _ => throw new NotImplementedException(),
            };
        }
        public string SystemLabel => getSystemLabel();
        private string getSystemLabel()
        {
            PipeSystemEnum ps;
            PipeTypeEnum pt;

            switch (Owner)
            {
                case Polyline pl:
                    ps = GetPipeSystem(pl);
                    pt = GetPipeType(pl);
                    break;
                case BlockReference br:
                    ps = br.GetPipeSystemEnum();
                    pt = br.GetPipeTypeEnum();
                    break;
                default:
                    throw new NotImplementedException();
            }

            return $"{ps} {pt}";
        }
        public string DnLabel => getDnLabel();
        private string getDnLabel()
        {
            switch (Owner)
            {
                case Polyline pl:
                    return GetPipeDN(pl).ToString();
                case BlockReference br:
                    var dn1 = br.ReadDynamicCsvProperty(DynamicProperty.DN1);
                    var dn2 = br.ReadDynamicCsvProperty(DynamicProperty.DN2);
                    return dn2 == "0" ? dn1 : $"{dn1}/{dn2}";
                default:
                    throw new NotImplementedException();
            }
        }
    }
}