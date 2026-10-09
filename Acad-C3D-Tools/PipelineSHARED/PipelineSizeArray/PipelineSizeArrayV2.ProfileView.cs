using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.Civil.DatabaseServices;

using IntersectUtilities.Collections;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;

namespace IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray
{
    public partial interface IPipelineSizeArrayV2
    {
        IPipelineSizeArrayV2 GetPartialSizeArrayForPV(ProfileView pv);

    }
}
namespace IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray
{
    public abstract partial class PipelineSizeArrayV2Base
    {
        private List<int> GetIndexesOfSizesAppearingInProfileView(
            double pvStationStart, double pvStationEnd)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < sizes.Count; i++)
            {
                SizeEntryV2 curEntry = sizes[i];
                if (pvStationStart < curEntry.EndStation &&
                    curEntry.StartStation < pvStationEnd) indexes.Add(i);
            }
            return indexes;
        }

        private SizeEntryV2[] GetArrayOfSizesForPv(ProfileView pv)
        {
            var list = GetIndexesOfSizesAppearingInProfileView(pv.StationStart, pv.StationEnd);
            SizeEntryV2[] partialAr = new SizeEntryV2[list.Count];
            for (int i = 0; i < list.Count; i++) partialAr[i] = this[list[i]];
            return partialAr;
        }

        public IPipelineSizeArrayV2 GetPartialSizeArrayForPV(ProfileView pv)
        {
            return new PipelineSizeArrayV2Partial(GetArrayOfSizesForPv(pv));
        }

    }
}
