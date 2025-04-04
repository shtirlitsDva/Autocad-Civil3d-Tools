using IntersectUtilities.PipelineNetworkSystem;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles
{
    class AP_PipelineData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public AP_SurfaceProfileData? SurfaceProfile { get; set; } = null;
        public IPipelineSizeArrayV2? PipelineSizes { get; set; } = null;
        public double[][]? HorizontalArcs { get; set; } = null;
        public double[][]? Utility { get; set; } = null;
        public AP_ProfileViewData? ProfileView { get; set; } = null;
        public AP_PipelineData(string name)
        {
            Name = name;
        }
    }
    class AP_SurfaceProfileData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public double[][]? ProfilePoints { get; set; }
        public AP_SurfaceProfileData(string name)
        {
            Name = name;
        }
    }
    class AP_ProfileViewData
    {
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public double[] Origin { get; set; } = [0, 0];
        [JsonInclude]
        public double ElevationAtOrigin { get; set; } = 0;
        public AP_ProfileViewData(string name)
        {
            Name = name;
        }
    }
}