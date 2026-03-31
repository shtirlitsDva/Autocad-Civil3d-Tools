using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.Civil.DatabaseServices;

using Dreambuild.AutoCAD;

using IntersectUtilities.LongitudinalProfiles.AutoProfileV2;
using IntersectUtilities.NTS;
using IntersectUtilities.PipelineNetworkSystem;
using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Union;

using System;
using System.Collections.Generic;
using System.Linq;

using BlockReference = Autodesk.AutoCAD.DatabaseServices.BlockReference;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using ObjectIdCollection = Autodesk.AutoCAD.DatabaseServices.ObjectIdCollection;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;

namespace IntersectUtilities
{
    internal static class AutoProfileV2PipelineCollector
    {
        public static void ClearDebugLayer(Database localDb, string devLyr)
        {
            using Transaction tx = localDb.TransactionManager.StartTransaction();
            var ents = localDb.HashSetOfType<Entity>(localDb.TransactionManager.TopTransaction)
                .Where(x => x.Layer == devLyr);

            foreach (var ent in ents)
            {
                ent.UpgradeOpen();
                ent.Erase(true);
            }

            tx.Commit();
        }

        public static HashSet<AP2_PipelineData> Collect(
            Database localDb,
            Transaction tx,
            Database fjvDb,
            Transaction fjvTx,
            PropertySetManager psm,
            PropertySetHelper pshFjv,
            PSetDefs.DriCrossingData dcd,
            Action<object> log)
        {
            var alignments = localDb.HashSetOfType<Alignment>(tx);
            if (alignments.Count == 0)
            {
                log("No Alignments found in the drawing");
                return [];
            }

            var ents = fjvDb.GetFjvEntities(fjvTx);
            var entsByAlignment = ents
                .GroupBy(x => pshFjv.Pipeline.ReadPropertyString(
                    x, pshFjv.PipelineDef.BelongsToAlignment))
                .ToDictionary(x => x.Key, x => x.ToList());

            var polylinesGroupedByAlignment = alignments
                .Select(al => new
                {
                    Alignment = al,
                    Entities = ents
                        .Where(x => x is Polyline)
                        .Cast<Polyline>()
                        .Where(p => pshFjv.Pipeline.ReadPropertyString(
                            p, pshFjv.PipelineDef.BelongsToAlignment) == al.Name)
                        .ToList()
                })
                .Where(x => x.Entities.Any())
                .ToDictionary(x => x.Alignment, x => x.Entities);

            var detailingBlockDict = localDb.ListOfType<BlockReference>(tx)
                .Where(x => x.RealName().EndsWith("_PV"))
                .OrderBy(x => x.RealName())
                .ToDictionary(x => x.RealName().Replace("_PV", ""), x => x);

            var selectedAlignments = SelectAlignments(alignments);
            var pipelines = new HashSet<AP2_PipelineData>();

            foreach (var alignment in selectedAlignments.OrderBy(x => x.Name))
            {
                log($"Processing {alignment.Name}");
                System.Windows.Forms.Application.DoEvents();

                var pipelineData = new AP2_PipelineData(alignment.Name);
                pipelineData.ProfileView = BuildProfileViewData(alignment, tx, pipelineData);
                pipelineData.SizeArray = BuildSizeArray(alignment, entsByAlignment);
                pipelineData.SurfaceProfile = BuildSurfaceProfileData(alignment, tx, pipelineData);
                pipelineData.HorizontalArcs = BuildHorizontalArcs(
                    alignment, polylinesGroupedByAlignment, pipelineData);
                pipelineData.Utility = BuildUtilities(
                    alignment.Name, pipelineData.ProfileView.ProfileView, pipelineData, detailingBlockDict, psm, dcd, tx);

                pipelineData.GenerateAvoidanceGeometryForUtilities();
                pipelineData.GenerateAvoidancePolygonsForUtilities();
                pipelineData.MergeAvoidancePolygonsForUtilities();

#if DEBUG
                foreach (var utility in pipelineData.Utility)
                {
                    if (utility.MergedAvoidancePolygon != null)
                    {
                        var mpoly = NTSConversion.ConvertNTSPolygonToMPolygon(utility.MergedAvoidancePolygon);                        
                        mpoly.Layer = "AutoProfileTest";
                        mpoly.Color = ColorByName("yellow");
                        mpoly.AddEntityToDbModelSpace(localDb);
                    }
                    else
                    {
                        if (utility.AvoidancePolygon == null) continue;
                        var mpoly = NTSConversion.ConvertNTSPolygonToMPolygon(utility.AvoidancePolygon);
                        mpoly.Layer = "AutoProfileTest";
                        mpoly.Color = ColorByName("yellow");
                        mpoly.AddEntityToDbModelSpace(localDb);
                    }
                }
#endif
                pipelines.Add(pipelineData);
            }

            return pipelines;
        }

        private static HashSet<Alignment> SelectAlignments(HashSet<Alignment> alignments)
        {
            var names = alignments
                .Select(x => x.Name)
                .OrderBy(x => x)
                .Prepend("All");

            string choice = StringGridFormCaller.Call(names, "Vælg alignment:");
            if (choice == null) return [];

            return choice == "All"
                ? alignments
                : alignments.Where(x => x.Name == choice).ToHashSet();
        }

        private static AP2_ProfileViewData BuildProfileViewData(
            Alignment alignment,
            Transaction tx,
            AP2_PipelineData pipelineData)
        {
            var profileViews = alignment.GetProfileViewIds().Entities<ProfileView>(tx).ToList();
            if (profileViews.Count != 1)
                throw new SystemException($"Alignment {alignment.Name} has more than one profile view!");

            var profileView = profileViews[0];
            return new AP2_ProfileViewData(profileView.Name, profileView, pipelineData);
        }

        private static IPipelineSizeArrayV2 BuildSizeArray(
            Alignment alignment,
            IReadOnlyDictionary<string, List<Entity>> entsByAlignment)
        {
            if (!entsByAlignment.TryGetValue(alignment.Name, out var entList))
                throw new System.Exception($"No entities found for alignment {alignment.Name}!");

            IPipelineV2 pipeline = PipelineV2Factory.Create(entList, alignment);
            if (pipeline == null)
                throw new System.Exception($"No pipeline could be built for alignment {alignment.Name}!");

            var sizeArray = PipelineSizeArrayFactory.CreateSizeArray(pipeline);
            if (sizeArray == null)
                throw new System.Exception($"No pipeline size array could be built for {alignment.Name}!");

            IntersectUtilities.UtilsCommon.Utils.prdDbg(sizeArray);

            return sizeArray;
        }

        private static AP2_SurfaceProfileData BuildSurfaceProfileData(
            Alignment alignment,
            Transaction tx,
            AP2_PipelineData pipelineData)
        {
            ObjectIdCollection profileIds = alignment.GetProfileIds();
            if (profileIds.Count == 0)
                throw new System.Exception($"Alignment {alignment.Name} has no profiles!");

            Profile? surfaceProfile = null;
            foreach (Oid profileId in profileIds)
            {
                Profile candidate = profileId.Go<Profile>(tx);
                if (candidate.Name.EndsWith("surface_P"))
                {
                    surfaceProfile = candidate;
                    break;
                }
            }

            if (surfaceProfile == null)
                throw new System.Exception($"No surface profile found for {alignment.Name}!");

            return new AP2_SurfaceProfileData(surfaceProfile.Name, surfaceProfile, pipelineData);
        }

        private static AP2_HorizontalArcs BuildHorizontalArcs(
            Alignment alignment,
            IReadOnlyDictionary<Alignment, List<Polyline>> polylinesGroupedByAlignment,
            AP2_PipelineData pipelineData)
        {
            if (!polylinesGroupedByAlignment.TryGetValue(alignment, out var polylines))
                return new AP2_HorizontalArcs(pipelineData);

            var ents = polylines.Cast<Entity>().ToList();
            IPipelineV2 pipeline = PipelineV2Factory.Create(ents, alignment);
            if (pipeline == null)
                throw new System.Exception($"No pipeline could be rebuilt for horizontal arc extraction for {alignment.Name}.");

            var horizontalArcs = new List<AP2_HorizontalArc>();
            foreach (Polyline pl in polylines.OrderBy(pipeline.GetPolylineMiddleStation))
            {
                for (int i = 0; i < pl.NumberOfVertices; i++)
                {
                    if (pl.GetSegmentType(i) != SegmentType.Arc) continue;

                    var arc = pl.GetArcSegmentAt(i);
                    double[] stations =
                    [
                        pipeline.GetStationAtPoint(arc.StartPoint),
                        pipeline.GetStationAtPoint(arc.EndPoint)
                    ];
                    horizontalArcs.Add(new AP2_HorizontalArc(stations.Min(), stations.Max(), pipelineData));
                }
            }

            return new AP2_HorizontalArcs(
                horizontalArcs.OrderBy(x => x.StartStation).ToList(), pipelineData);
        }

        private static List<AP2_Utility> BuildUtilities(
            string alignmentName,
            ProfileView profileView,
            AP2_PipelineData pipelineData,
            IReadOnlyDictionary<string, BlockReference> detailingBlockDict,
            PropertySetManager psm,
            PSetDefs.DriCrossingData dcd,
            Transaction tx)
        {
            if (!detailingBlockDict.TryGetValue(alignmentName, out var blockReference))
                throw new System.Exception($"No detailing block found for {alignmentName}!");

            BlockTableRecord blockTableRecord = blockReference.BlockTableRecord.Go<BlockTableRecord>(tx);
            Matrix3d transform = blockReference.BlockTransform;
            var geometryFactory = new GeometryFactory();
            var polygons = new List<Polygon>();

            foreach (Oid id in blockTableRecord)
            {
                if (id.IsDerivedFrom<BlockReference>()) continue;
                if (!(id.IsDerivedFrom<Polyline>() || id.IsDerivedFrom<Arc>() || id.IsDerivedFrom<Circle>())) continue;

                var ent = id.Go<Entity>(tx);
                bool isRelocatable = psm.ReadPropertyBool(ent, dcd.CanBeRelocated);
                if (isRelocatable) continue;

                var extents = ent.GeometricExtents;
                extents.TransformBy(transform);

                polygons.Add(
                    geometryFactory.CreatePolygon(
                    [
                        new Coordinate(extents.MinPoint.X, extents.MinPoint.Y),
                        new Coordinate(extents.MaxPoint.X, extents.MinPoint.Y),
                        new Coordinate(extents.MaxPoint.X, extents.MaxPoint.Y),
                        new Coordinate(extents.MinPoint.X, extents.MaxPoint.Y),
                        new Coordinate(extents.MinPoint.X, extents.MinPoint.Y)
                    ]));
            }

            Geometry union = CascadedPolygonUnion.Union(polygons.ToArray());
            var envelopes = new List<Geometry>();

            if (union == null || union.IsEmpty)
            {
                return [];
            }
            if (union is Polygon singlePolygon)
            {
                envelopes.Add(singlePolygon.Envelope);
            }
            else if (union is MultiPolygon multiPolygon)
            {
                foreach (Polygon polygon in multiPolygon.Geometries)
                {
                    envelopes.Add(polygon.Envelope);
                }
            }

            double station = 0.0;
            double elevation = 0.0;
            var utilities = new List<AP2_Utility>();

            foreach (var envelope in envelopes.OrderBy(x =>
            {
                double s = 0.0;
                double e = 0.0;
                profileView.FindStationAndElevationAtXY(x.Coordinates[0].X, x.Coordinates[0].Y, ref s, ref e);
                return s;
            }))
            {
                var coords = envelope.Coordinates;
                double x = (coords[0].X + coords[2].X) / 2.0;
                double y = (coords[0].Y + coords[2].Y) / 2.0;

                profileView.FindStationAndElevationAtXY(x, y, ref station, ref elevation);
                utilities.Add(new AP2_Utility(envelope, station, pipelineData));
            }

            return utilities;
        }
    }
}
