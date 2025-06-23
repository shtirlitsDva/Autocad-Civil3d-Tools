using DimensioneringV2.Geometry;
using DimensioneringV2.GraphModelRoads;
using dbg = IntersectUtilities.UtilsCommon.Utils.DebugHelper;
using utils = IntersectUtilities.UtilsCommon.Utils;

using QuikGraph;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using IntersectUtilities.UtilsCommon;
using Graph = DimensioneringV2.GraphModelRoads.Graph;

namespace DimensioneringV2.GraphFeatures
{
    internal class GraphTranslator
    {
        /// <summary>
        /// Compresses the graph and translates it
        /// Maybe this should be split up into two methods
        /// </summary>
        public static List<List<AnalysisFeature>> TranslateGraph(Graph originalGraph)
        {
            List<List<AnalysisFeature>> allFeatures = new();

            HashSet<SegmentNode> visited = new();

            foreach (var subgraph in originalGraph.ConnectedComponents)
            {
                List<AnalysisFeature> nodes = new List<AnalysisFeature>();

                SegmentNode root = subgraph.RootNode;
                Point2D ep;
                var degree = originalGraph.GetPointDegree(root.StartPoint);
                if (degree == 1) ep = root.StartPoint;
                else ep = root.EndPoint;

                //dbg.CreateDebugLine(ep.To3d(), utils.ColorByName("green"));

                Stack <(SegmentNode seg, Point2D entry)> stack = new();
                stack.Push((root, ep)); // seed the stack with the root node

                List<SegmentNode> originalNodes = new();
                bool startNew = false;

                while (stack.Count > 0)
                {
                    var node = stack.Pop();

                    if (visited.Contains(node.seg)) continue;
                    visited.Add(node.seg);

                    if (startNew)
                    {
                        originalNodes = new();
                        startNew = false;
                    }
                    originalNodes.Add(node.seg);

                    node.seg.MakePointStart(node.entry);
                    Point2D exitPt = node.seg.GetOtherEnd(node.entry);
                    degree = originalGraph.GetPointDegree(exitPt);

                    switch (degree)
                    {
                        case 1: //Reached a leafnode
                            {
                                startNew = true;
                                break;
                            }
                        case 2: //Intermediate node
                            {
                                foreach (SegmentNode neighbor in node.seg.Neighbors)
                                {
                                    if (!visited.Contains(neighbor) && neighbor.HasPoint(exitPt))
                                    {
                                        stack.Push((neighbor, exitPt));
                                    }
                                }
                                break;
                            }
                        case > 2:
                            {
                                foreach (SegmentNode neighbor in node.seg.Neighbors)
                                {
                                    if (!visited.Contains(neighbor) && neighbor.HasPoint(exitPt))
                                    {
                                        stack.Push((neighbor, exitPt));
                                    }
                                }
                                startNew = true;
                                break;
                            }
                        case 0:
                            {
                                dbg.CreateDebugLine(
                                    node.seg.StartPoint.To3d(), utils.ColorByName("red"));
                                dbg.CreateDebugLine(
                                    node.seg.EndPoint.To3d(), utils.ColorByName("red"));
                                utils.prdDbg("Point has degree 0!\n" +
                                    $"{node.ToString()}");
                                throw new Exception("DBG: Point has degree 0!");
                            }
                    }

                    if (startNew)
                    {
                        //Translate geometry
                        var lines = originalNodes.Select(n => n.ToLineString()).ToList();
                        NetTopologySuite.Geometries.Geometry geometry;
                        if (lines.Count > 1)
                        {
                            var merger = new NetTopologySuite.Operation.Linemerge.LineMerger();
                            merger.Add(lines);
                            var merged = merger.GetMergedLineStrings();

                            if (merged.Count > 1)
                            {

                                foreach (var item in originalNodes)
                                {
                                    dbg.CreateDebugLine(
                                        item.StartPoint.To3d(), utils.ColorByName("red"));
                                    dbg.CreateDebugLine(
                                        item.EndPoint.To3d(), utils.ColorByName("cyan"));
                                }
                                utils.prdDbg("Merging returned multiple linestrings!");
                                throw new Exception("DBG: Merging returned multiple linestrings!");
                            }
                            geometry = merged[0];
                            //geometry = sequenced;
                        }
                        else geometry = lines[0];

                        //Translate building data if any
                        Dictionary<string, object> attributes = new()
                        {
                            { "id_lokalId", "" },
                            { "Name", "" },
                            { "Adresse", "" },
                            { "BygningsAnvendelseNyTekst", "" },
                            { "Opførelsesår", 0 },
                            { "BeregningsAreal", 0.0 },
                            { "KælderAreal", 0.0 },
                            { "VarmeType", "" },
                            { "VarmeInstallation", "" },
                            { "OpvarmningsMiddel", "" },
                            { "InstallationOgBrændsel", "" },
                            { "Vejnavn", "" },
                            { "Husnummer", "" },
                            { "Postnr", "" },
                            { "By", "" },
                            { "SpecifikVarmeForbrug", 0.0 },
                            { "EstimeretVarmeForbrug", 0.0 },
                            { "AntalEnheder", 0 },
                            { "VarmeDistrikt", "" },
                            { "IsBuildingConnection", false },
                            { "IsRootNode", false },
                        };

                        if (originalNodes.Any(x => x.IsBuildingConnection))
                        {
                            var buildingConnection = originalNodes.First(x => x.IsBuildingConnection);
                            var building = buildingConnection.BuildingId.Go<BlockReference>(
                                Application.DocumentManager.MdiActiveDocument.Database.TransactionManager.TopTransaction);
                            if (building == null)
                            {
                                Application.DocumentManager.MdiActiveDocument.Editor.SetImpliedSelection(
                                    [buildingConnection.BuildingId]);
                                throw new Exception(
                                    "Building connection node does not have a block reference!" +
                                    "\n" +
                                    $"{buildingConnection.BuildingId}");
                            }
                            IntersectUtilities.BBR bbr = new IntersectUtilities.BBR(building);
                            
                            attributes["id_lokalId"] = bbr.id_lokalId;
                            attributes["Name"] = bbr.Name;
                            attributes["Adresse"] = bbr.Adresse;
                            attributes["BygningsAnvendelseNyTekst"] = bbr.BygningsAnvendelseNyTekst;
                            attributes["Opførelsesår"] = bbr.Opførelsesår;
                            attributes["BeregningsAreal"] = bbr.SamletBoligareal + bbr.SamletErhvervsareal;
                            attributes["KælderAreal"] = bbr.KælderAreal;
                            attributes["VarmeType"] = bbr.Type;
                            attributes["VarmeInstallation"] = bbr.VarmeInstallation;
                            attributes["OpvarmningsMiddel"] = bbr.OpvarmningsMiddel;
                            attributes["InstallationOgBrændsel"] = bbr.InstallationOgBrændsel;
                            attributes["Vejnavn"] = bbr.Vejnavn;
                            attributes["Vejklasse"] = bbr.Vejklasse;
                            attributes["Husnummer"] = bbr.Husnummer;
                            attributes["Postnr"] = bbr.Postnr;
                            attributes["By"] = bbr.By;
                            attributes["SpecifikVarmeForbrug"] = bbr.SpecifikVarmeForbrug;
                            attributes["EstimeretVarmeForbrug"] = bbr.EstimeretVarmeForbrug;
                            attributes["AntalEnheder"] = bbr.AntalEnheder;
                            attributes["VarmeDistrikt"] = bbr.DistriktetsNavn;
                            attributes["IsBuildingConnection"] = true;
                        }

                        if (originalNodes.Any(x => x.IsRoot))
                        {
                            attributes["IsRootNode"] = true;
                        }

                        AnalysisFeature fn = new AnalysisFeature(geometry, attributes);
                        nodes.Add(fn);
                    }
                }

                allFeatures.Add(nodes);
            }

            return allFeatures;
        }
    }
}