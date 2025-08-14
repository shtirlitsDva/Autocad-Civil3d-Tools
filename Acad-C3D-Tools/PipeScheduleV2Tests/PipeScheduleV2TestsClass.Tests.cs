using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;

using System;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;

namespace PipeScheduleV2Tests
{
    public partial class PipeScheduleV2TestsClass
    {
        private static void SkipIfScheduleMissing()
        {
            try
            {
                var list = GetPipeTypes();
                if (list == null || !list.Any()) throw new Ps2SkipException("Pipe types not loaded (CSV missing?)");
            }
            catch
            {
                throw new Ps2SkipException("Pipe types not loaded (CSV missing?)");
            }
        }

        [Ps2Test]
        private static void GetPipeSystem_FromLayer_String()
        {
            var system = GetPipeSystem("FJV-FREM-DN200");
            if (system == IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Ukendt)
                throw new Exception($"Expected known system, got {system}");
        }

        [Ps2Test]
        private static void GetPipeDN_FromLayer_String()
        {
            int dn = GetPipeDN("FJV-RETUR-DN250");
            if (dn != 250) throw new Exception($"Expected 250, got {dn}");
        }

        [Ps2Test]
        private static void GetPipeType_FromLayer_String()
        {
            var type = GetPipeType("FJV-FREM-DN200");
            if (type == IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Ukendt)
                throw new Exception($"Expected known type, got {type}");
        }

        		[Ps2Test]
		private static void GetPipeKOd_BySystemDnTypeSeries()
		{
			SkipIfScheduleMissing();
			double kod = GetPipeKOd(IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål, 20, IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Enkelt, IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.S1);
			if (Math.Abs(kod - 90.0) > 0.001) throw new Exception($"Expected kOd≈90 for DN20 S1 Enkelt Stål, got {kod}");
		}

        [Ps2Test]
        private static void GetOffset_And_StdLength()
        {
            SkipIfScheduleMissing();
            double off = GetOffset(20, IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål, IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Enkelt, IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.S1, 7.5, 0);
            if (Math.Abs(off - 0.7) > 1e-6) throw new Exception($"Expected 0.7, got {off}");
            double offWide = GetOffset(20, IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål, IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Enkelt, IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.S1, 8.0, 0);
            if (Math.Abs(offWide - 0.85) > 1e-6) throw new Exception($"Expected 0.85, got {offWide}");
        }

        [Ps2Test]
        private static void Entity_Based_Parsing_And_Series()
        {
            SkipIfScheduleMissing();
            var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
            var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
            if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

            var system = GetPipeSystem(pl);
            if (system == IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Ukendt) throw new Exception($"Expected known system from entity, got {system}");
            int dn = GetPipeDN(pl);
            if (dn <= 0) throw new Exception($"Expected DN>0 from entity, got {dn}");
            var type = GetPipeType(pl);
            if (type == IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Ukendt)
                throw new Exception($"Expected known type from entity, got {type}");

            var series = GetPipeSeriesV2(pl);
            if (series == IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.Undefined)
                throw new Exception("Expected a defined series from entity.");
        }

        [Ps2Test]
        private static void GetLabel_From_Entity_NotEmpty()
        {
            SkipIfScheduleMissing();
            var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
            var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
            if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

            string label = GetLabel(pl);
            if (string.IsNullOrWhiteSpace(label)) throw new Exception("Label is empty.");
        }
    }
}


