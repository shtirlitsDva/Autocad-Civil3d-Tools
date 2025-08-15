using Autodesk.AutoCAD.DatabaseServices;

using System;
using System.Linq;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

namespace PipeScheduleV2Tests
{
	public partial class PipeScheduleV2TestsClass
	{
		[Ps2Test]
		private static void GetPipeOd_And_Id_From_Entity()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			double od = GetPipeOd(pl);
			double id = GetPipeId(pl);
			if (od <= 0 || id <= 0 || !(od > id)) throw new Exception($"Invalid OD/ID values: od={od}, id={id}");
		}

		[Ps2Test]
		private static void GetLayerColor_From_Entity_NotZero()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			short color = GetLayerColor(pl);
			if (color == 0) throw new Exception("Layer color is zero.");
		}

		[Ps2Test]
		private static void GetTrenchWidth_From_System_Type_Series()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			var system = GetPipeSystem(pl);
			var type = GetPipeType(pl, true);
			var series = GetPipeSeriesV2(pl);
			if (series == IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.Undefined)
				throw new Ps2SkipException("Series undefined for registry entity.");

			double width = GetTrenchWidth(20, system, type, series);
			if (width <= 0) throw new Exception($"Invalid trench width: {width}");
		}

		[Ps2Test]
		private static void GetBuerorMinRadius_Default_And_Isoplus()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			double rDefault = GetBuerorMinRadius(pl);
			double rIso = GetBuerorMinRadius(pl, "Isoplus", 12);
			if (rDefault <= 0 || rIso <= 0) throw new Exception($"Invalid bueror radius: default={rDefault}, isoplus={rIso}");
		}

		[Ps2Test]
		private static void GetLineTypeLayerPrefix_Staal_Returns_ST()
		{
			var p = GetLineTypeLayerPrefix(IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål);
			if (!string.Equals(p, "ST", StringComparison.Ordinal)) throw new Exception($"Expected 'ST', got '{p}'");
		}

		[Ps2Test]
		private static void ListAllDns_Contains_RegistryDn()
		{
			SkipIfScheduleMissing();
			var list = ListAllDnsForPipeSystemType(
				IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål,
				IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Enkelt
			);
			if (list == null || !list.Any() || !list.Contains(20))
				throw new Exception("DN list does not contain 20 for Stål Enkelt.");
		}

		[Ps2Test]
		private static void PerType_SmokeTest_Core_Methods()
		{
			SkipIfScheduleMissing();
			foreach (var pt in GetPipeTypes())
			{
				var types = pt.GetAvailableTypes()?.ToArray();
				if (types == null || types.Length == 0) continue;
				foreach (var t in types)
				{
					var dns = pt.ListAllDnsForPipeType(t)?.ToArray();
					if (dns == null || dns.Length == 0) continue;
					int dn = dns[0];
					var seriesList = pt.GetAvailableSeriesForType(t)?.ToArray();
					var series = (seriesList != null && seriesList.Length > 0) ? seriesList[0] : IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.S1;

					_ = pt.GetPipeOd(dn);
					_ = pt.GetPipeId(dn);
					_ = pt.GetPipeKOd(dn, t, series);
					_ = pt.GetMinElasticRadius(dn, t);
					_ = pt.GetFactorForVerticalElasticBending(dn, t);
					_ = pt.GetPipeStdLength(dn, t);
					_ = pt.GetTrenchWidth(dn, t, series);
					_ = pt.GetSizeColor(dn, t);
					_ = pt.GetDefaultLengthForDnAndType(dn, t);
					_ = pt.GetOffset(dn, t, series);
				}
			}
		}

		[Ps2Test]
		private static void GetPipeStdLength_From_Entity_Positive()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			double l = GetPipeStdLength(pl);
			if (l <= 0) throw new Exception($"Invalid std length: {l}");
		}

		[Ps2Test]
		private static void GetPipeMinElasticRadii_Horizontal_Vertical_Positive()
		{
			SkipIfScheduleMissing();
			var reg = PipeScheduleV2EntityRegistry.GetRegistryPath();
			var pl = PipeScheduleV2EntityRegistry.GetPolylineByRegistryKey(reg, "DN20_ENKELT");
			if (pl == null) throw new Ps2SkipException("Registry polyline 'DN20_ENKELT' not found. Add it to CSV.");

			double rH = GetPipeMinElasticRadiusHorizontalCharacteristic(pl, considerInSituBending: false);
			double rV = GetPipeMinElasticRadiusVerticalCharacteristic(pl);
			if (rH <= 0 || rV <= 0) throw new Exception($"Invalid min elastic radii: H={rH}, V={rV}");
		}

		[Ps2Test]
		private static void GetColorForDim_From_Layer_NotZero()
		{
			int c = GetColorForDim("FJV-FREM-DN20");
			if (c == 0) throw new Exception("Size color is zero.");
		}

		[Ps2Test]
		private static void GetPipeTypeByAvailability_NotUkendt()
		{
			SkipIfScheduleMissing();
			var t = GetPipeTypeByAvailability(IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål, 20);
			if (t == IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Ukendt) throw new Exception("Type by availability is Ukendt.");
		}

		[Ps2Test]
		private static void GetOffset_UnknownDn_Returns_Default()
		{
			double off = GetOffset(999, IntersectUtilities.UtilsCommon.Utils.PipeSystemEnum.Stål, IntersectUtilities.UtilsCommon.Utils.PipeTypeEnum.Enkelt, IntersectUtilities.UtilsCommon.Utils.PipeSeriesEnum.S1, 7.5, 0);
			if (Math.Abs(off - 100) > 1e-6) throw new Exception($"Expected default 100 for unknown DN, got {off}");
		}
	}
}