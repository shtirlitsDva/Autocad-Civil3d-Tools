using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;

using Dreambuild.AutoCAD;

using IntersectUtilities.NTS;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Exception = System.Exception;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfileV2
{
    internal class AP2_PipelineData
    {
        public AP2_PipelineData(string name)
        {
            Name = name;
        }
        [JsonInclude]
        public string Name { get; set; }
        [JsonInclude]
        public AP2_SurfaceProfileData? SurfaceProfile { get; set; } = null;
        [JsonIgnore]
        public IPipelineSizeArrayV2? SizeArray { get; set; } = null;
        [JsonIgnore]
        public AP2_HorizontalArcs HorizontalArcs { get; set; }
        [JsonIgnore]
        public AP2_ProfileViewData? ProfileView { get; set; } = null;
        [JsonInclude]
        public List<AP2_Utility> Utility { get; set; } = new();        
        public Polyline? UnfilletedPolyline { get; set; }
        public Polyline? FilletedPolyline { get; private set; } // To store the result        
        public void Serialize(string filename)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            };
            options.Converters.Add(new PolylineJsonConverter());
            options.Converters.Add(new Extents2dJsonConverter());
            var json = JsonSerializer.Serialize(this, options);
            System.IO.File.WriteAllText(filename, json);
        }
    }
}