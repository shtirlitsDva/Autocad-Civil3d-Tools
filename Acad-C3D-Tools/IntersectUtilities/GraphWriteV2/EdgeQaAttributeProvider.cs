using Autodesk.AutoCAD.DatabaseServices;

using IntersectUtilities.DynamicBlocks;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;

namespace IntersectUtilities.GraphWriteV2
{
    internal static class EdgeQaAttributeProvider
    {
        public static string? GetAttributes(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            System.Data.DataTable fjvTable)
        {
            var errors = new List<string>();

            try
            {
                Qa_System(ent1, endType1, ent2, endType2, fjvTable, errors);
                Qa_Dn(ent1, endType1, ent2, endType2, fjvTable, errors);
            }
            catch
            {
                // if QA fails unexpectedly, prefer not to block output
            }

            if (errors.Count == 0) return null;
            var label = string.Join(", ", errors);
            return $"[ label=\"{label}\", color=\"red\" ]";
        }

        private static void Qa_System(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            System.Data.DataTable fjvTable,
            List<string> errors)
        {
            PipeTypeEnum type1 = default;
            PipeTypeEnum type2 = default;

            if (ent1 is Polyline pl1)
                type1 = PipeScheduleV2.PipeScheduleV2.GetPipeType(pl1);
            else if (ent1 is BlockReference br1)
            {
                var sys = PropertyReader.ReadComponentSystem(br1, fjvTable, endType1);
                Enum.TryParse(sys, out type1);
            }

            if (ent2 is Polyline pl2)
                type2 = PipeScheduleV2.PipeScheduleV2.GetPipeType(pl2);
            else if (ent2 is BlockReference br2)
            {
                var sys = PropertyReader.ReadComponentSystem(br2, fjvTable, endType2);
                Enum.TryParse(sys, out type2);
            }

            if (type1 == PipeTypeEnum.Retur || type1 == PipeTypeEnum.Frem)
                type1 = PipeTypeEnum.Enkelt;
            if (type2 == PipeTypeEnum.Retur || type2 == PipeTypeEnum.Frem)
                type2 = PipeTypeEnum.Enkelt;

            if (type1 != default && type2 != default && type1 != type2)
                errors.Add("T/E");
        }

        private static void Qa_Dn(
            Entity ent1, EndType endType1,
            Entity ent2, EndType endType2,
            System.Data.DataTable fjvTable,
            List<string> errors)
        {
            var dnSet1 = new HashSet<int>();
            var dnSet2 = new HashSet<int>();

            if (ent1 is Polyline pl1)
            {
                var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl1);
                if (dn != 0) dnSet1.Add(dn);
            }
            else if (ent1 is BlockReference br1)
            {
                var dn1 = SafeParse(PropertyReader.ReadComponentDN1Str(br1, fjvTable));
                var dn2 = SafeParse(PropertyReader.ReadComponentDN2Str(br1, fjvTable));
                if (dn1 != 0) dnSet1.Add(dn1);
                if (dn2 != 0) dnSet1.Add(dn2);
            }

            if (ent2 is Polyline pl2)
            {
                var dn = PipeScheduleV2.PipeScheduleV2.GetPipeDN(pl2);
                if (dn != 0) dnSet2.Add(dn);
            }
            else if (ent2 is BlockReference br2)
            {
                var dn1 = SafeParse(PropertyReader.ReadComponentDN1Str(br2, fjvTable));
                var dn2 = SafeParse(PropertyReader.ReadComponentDN2Str(br2, fjvTable));
                if (dn1 != 0) dnSet2.Add(dn1);
                if (dn2 != 0) dnSet2.Add(dn2);
            }

            if (dnSet1.Count == 0 || dnSet2.Count == 0)
                return; // skip if insufficient data

            if (dnSet1.Count == 1 && dnSet2.Count == 1)
            {
                var a = First(dnSet1);
                var b = First(dnSet2);
                if (a != b) errors.Add("DN");
                return;
            }

            if (dnSet1.Count == 2 && dnSet2.Count == 2)
            {
                errors.Add("DN forvirring");
                return;
            }

            if (dnSet1.Count == 2)
            {
                var single = First(dnSet2);
                if (!dnSet1.Contains(single)) errors.Add("DN");
                return;
            }
            if (dnSet2.Count == 2)
            {
                var single = First(dnSet1);
                if (!dnSet2.Contains(single)) errors.Add("DN");
            }
        }

        private static int SafeParse(string s)
        {
            if (int.TryParse(s, out var v)) return v;
            return 0;
        }

        private static int First(HashSet<int> set)
        {
            foreach (var v in set) return v;
            return 0;
        }
    }
}