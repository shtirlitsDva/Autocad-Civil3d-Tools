using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2.PipelineDataClasses
{
    internal class AP2_SurfaceProfileDataDTO
    {
        public string Name { get; set; }
        public double[][] SurfaceProfile { get; set; } = new();

        public AP2_SurfaceProfileDataDTO(AP2_SurfaceProfileData data)
        {
            Name = data.Name;
            SurfaceProfile = data.GetSimplifiedProfileDTO();
        }
    }
}
