using Autodesk.AutoCAD.DatabaseServices;

using DimensioneringV2.GraphFeatures;

using IntersectUtilities;
using IntersectUtilities.UtilsCommon;

using Mapsui.Extensions;
using Mapsui.Extensions.Projections;

using NetTopologySuite.Geometries;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace DimensioneringV2.Services
{
    internal class BBRLayerService
    {
        private static BBRLayerService? _instance;
        public static BBRLayerService Instance => _instance ??= new BBRLayerService();
        private BBRLayerService() { }

        public event EventHandler? BBRDataLoaded;

        public IEnumerable<BBRMapFeature> ActiveFeatures { get; private set; } = Enumerable.Empty<BBRMapFeature>();
        public IEnumerable<BBRMapFeature> InactiveFeatures { get; private set; } = Enumerable.Empty<BBRMapFeature>();

        public void LoadBBRFeatures(Database db, Transaction tx)
        {
            var acceptedTypes = HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes();
            var allBlockTypes = CommonVariables.AllBlockTypes;

            var blockRefs = db.HashSetOfType<BlockReference>(tx, true);

            var activeList = new List<BBRMapFeature>();
            var inactiveList = new List<BBRMapFeature>();

            foreach (var br in blockRefs)
            {
                try
                {
                    var type = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Type");
                    if (string.IsNullOrEmpty(type) || !allBlockTypes.Contains(type))
                        continue;

                    var address = PropertySetManager.ReadNonDefinedPropertySetString(br, "BBR", "Adresse") ?? "";
                    var x = br.Position.X;
                    var y = br.Position.Y;

                    var geometry = new Point(x, y);
                    var feature = new BBRMapFeature(geometry, type, address, x, y);

                    if (acceptedTypes.Contains(type))
                        activeList.Add(feature);
                    else
                        inactiveList.Add(feature);
                }
                catch
                {
                }
            }

            ActiveFeatures = ReprojectFeatures(activeList);
            InactiveFeatures = ReprojectFeatures(inactiveList);

            BBRDataLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void RefreshFiltering()
        {
            var acceptedTypes = HydraulicSettingsService.Instance.Settings.GetAcceptedBlockTypes();

            var allFeatures = ActiveFeatures.Concat(InactiveFeatures).ToList();

            var activeList = new List<BBRMapFeature>();
            var inactiveList = new List<BBRMapFeature>();

            foreach (var feature in allFeatures)
            {
                if (acceptedTypes.Contains(feature.HeatingType))
                    activeList.Add(feature);
                else
                    inactiveList.Add(feature);
            }

            ActiveFeatures = activeList;
            InactiveFeatures = inactiveList;

            BBRDataLoaded?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            ActiveFeatures = Enumerable.Empty<BBRMapFeature>();
            InactiveFeatures = Enumerable.Empty<BBRMapFeature>();
        }

        private static IEnumerable<BBRMapFeature> ReprojectFeatures(IEnumerable<BBRMapFeature> features)
        {
            return features
                .Cast<Mapsui.IFeature>()
                .Project("EPSG:25832", "EPSG:3857", new DotSpatialProjection())
                .Cast<BBRMapFeature>();
        }
    }
}
