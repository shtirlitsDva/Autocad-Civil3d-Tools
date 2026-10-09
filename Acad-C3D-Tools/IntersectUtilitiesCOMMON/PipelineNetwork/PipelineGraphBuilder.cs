#if BRICSCAD
using Bricscad.ApplicationServices;
using Teigha.DatabaseServices;
using Teigha.Geometry;
using Application = Bricscad.ApplicationServices.Application;
using Entity = Teigha.DatabaseServices.Entity;
using Oid = Teigha.DatabaseServices.ObjectId;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Application = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Entity = Autodesk.AutoCAD.DatabaseServices.Entity;
using Oid = Autodesk.AutoCAD.DatabaseServices.ObjectId;
#endif

using GroupByCluster;

using IntersectUtilities.PipelineNetworkSystem.PipelineSizeArray;
using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.DataManager.CsvData;
using IntersectUtilities.UtilsCommon.Enums;
using IntersectUtilities.UtilsCommon.Graphs;

using MoreLinq;

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using static IntersectUtilities.PipeScheduleV2.PipeScheduleV2;
using static IntersectUtilities.UtilsCommon.Utils;

using DataTable = System.Data.DataTable;

namespace IntersectUtilities.PipelineNetworkSystem
{
    
    public class PipelineGraphBuilder
    {
        public GraphCollection<IPipelineV2> BuildPipelineGraphs(IEnumerable<IPipelineV2> pipelines)
        {
            GraphCollection<IPipelineV2> graphs = new(new List<Graph<IPipelineV2>>());

            var groups = pipelines.GroupConnected((x, y) => x.IsConnectedTo(y, 0.05));
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var maxDNQuery = group.MaxByEnumerable(x => x.GetMaxDN());

                IPipelineV2 entryPipeline;
                if (maxDNQuery.Count() > 1)
                {//Multiple candidates for MAXDN found

                    #region Case 1
                    // Case 1.)
                    // Two alignments with same max DN
                    // But one of them is connected at both ends
                    // This is not the entry pipeline
                    // The other is only connected on one end

                    entryPipeline = maxDNQuery.Where(x => !AreBothEndsConnected(x, group, x.GetMaxDN())).FirstOrDefault();
                    if (entryPipeline == default) entryPipeline = maxDNQuery.First();

                    bool AreBothEndsConnected(IPipelineV2 source, IEnumerable<IPipelineV2> other, int endDn)
                    {
                        source.CreateSizeArray();

                        var startSize = source.PipelineSizes.Sizes.First();
                        bool startConnected = true;
                        if (startSize.DN == endDn)
                        {
                            Point3d sp = source.StartPoint;
                            startConnected = other.Where(x => x.Name != source.Name).Any(x =>
                            {
                                Point3d testP = x.GetClosestPointTo(sp, false);
                                return testP.DistanceHorizontalTo(sp) < 0.05;
                            });
                        }

                        var endSize = source.PipelineSizes.Sizes.Last();
                        bool endConnected = true;
                        if (endSize.DN == endDn)
                        {
                            Point3d ep = source.EndPoint;
                            endConnected = other.Where(x => x.Name != source.Name).Any(x =>
                            {
                                Point3d testP = x.GetClosestPointTo(ep, false);
                                return testP.DistanceHorizontalTo(ep) < 0.05;
                            });
                        }

                        return startConnected && endConnected;
                    }
                    #endregion
                }
                else entryPipeline = maxDNQuery.First();

                group.Remove(entryPipeline);
                var root = new Node<IPipelineV2>(entryPipeline);
                Graph<IPipelineV2> graph = new(root, n => n.Name, n => n.Label);
                graphs.Add(graph);

                var stack = new Stack<Node<IPipelineV2>>();
                stack.Push(root);

                while (stack.Count > 0)
                {
                    var node = stack.Pop();
                    var children = group.Where(x => x.IsConnectedTo(node.Value, 0.05)).ToList();
                    foreach (var child in children)
                    {
                        var childNode = new Node<IPipelineV2>(child);
                        node.AddChild(childNode);
                        stack.Push(childNode);
                        group.Remove(child);
                    }
                }
            }
            return graphs;
        }
    }

}
