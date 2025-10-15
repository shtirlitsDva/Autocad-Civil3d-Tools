using IntersectUtilities.UtilsCommon.Enums;

using NTRExport.Enums;
using NTRExport.Geometry;
using NTRExport.TopologyModel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTRExport.Ntr
{
    internal class NtrMapper
    {
        public NtrGraph Map(Topology topo)
        {
            var g = new NtrGraph();

            foreach (var e in topo.Elements)
            {
                switch (e)
                {
                    case TPipe p:
                        g.Members.Add(new NtrPipe
                        {
                            A = p.A.Node.Pos,
                            B = p.B.Node.Pos,
                            Dn = p.Dn,
                            Material = p.Material,
                            Provenance = [p.Source]
                        });
                        break;

                    // Bend-like elements → BOG
                    case TFitting f when f.Kind is PipelineElementType.Kedelrørsbøjning
                                              or PipelineElementType.PræisoleretBøjning90gr
                                              or PipelineElementType.Bøjning45gr
                                              or PipelineElementType.Bøjning30gr
                                              or PipelineElementType.Bøjning15gr
                                              or PipelineElementType.PræisoleretBøjningVariabel
                                              or PipelineElementType.Buerør:
                        var ends = f.Ports.Take(2).ToArray();
                        var a = ends[0].Node.Pos; var b = ends[1].Node.Pos;
                        var t = new Pt2((a.X + b.X) / 2, (a.Y + b.Y) / 2); // stub; replace with true elbow point
                        g.Members.Add(new NtrBend { A = a, B = b, T = t, Dn = InferMainDn(topo, f), Provenance = new[] { f.Source } });
                        break;

                    // Tee-like elements → TEE
                    case TFitting f when f.Kind is PipelineElementType.Svejsetee
                                              or PipelineElementType.PreskoblingTee
                                              or PipelineElementType.Muffetee
                                              or PipelineElementType.LigeAfgrening
                                              or PipelineElementType.AfgreningMedSpring
                                              or PipelineElementType.AfgreningParallel
                                              or PipelineElementType.Stikafgrening:
                        var mains = f.Ports.Where(x => x.Role == PortRole.Main).Take(2).ToArray();
                        var br = f.Ports.First(x => x.Role == PortRole.Branch);
                        g.Members.Add(new NtrTee
                        {
                            Ph1 = mains[0].Node.Pos,
                            Ph2 = mains[1].Node.Pos,
                            Pa1 = br.Node.Pos,
                            Pa2 = br.Node.Pos, // Pa2 resolved later if you want
                            Dn = InferMainDn(topo, f),
                            DnBranch = InferBranchDn(topo, f),
                            Provenance = [f.Source]
                        });
                        break;

                    // Reducer → RED
                    case TFitting f when f.Kind == PipelineElementType.Reduktion:
                        var pr = f.Ports.Take(2).ToArray();
                        g.Members.Add(new NtrReducer
                        {
                            P1 = pr[0].Node.Pos,
                            P2 = pr[1].Node.Pos,
                            Dn1 = InferDn1(topo, f),
                            Dn2 = InferDn2(topo, f),
                            Material = null,
                            Provenance = [f.Source]
                        });
                        break;

                    // Valves → ARM
                    case TFitting f when f.Kind is PipelineElementType.Engangsventil
                                              or PipelineElementType.PræisoleretVentil
                                              or PipelineElementType.PræventilMedUdluftning:
                        var pv = f.Ports.Take(2).ToArray();
                        var p1 = pv[0].Node.Pos; var p2 = pv[1].Node.Pos;
                        var pm = new Pt2((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                        g.Members.Add(new NtrInstrument
                        {
                            P1 = p1,
                            P2 = p2,
                            Pm = pm,
                            Dn1 = InferMainDn(topo, f),
                            Dn2 = InferMainDn(topo, f),
                            Provenance = [f.Source]
                        });
                        break;

                    // Material change / Svanehals / F/Y models → RO stub or no-op
                    case TFitting f when f.Kind is PipelineElementType.Materialeskift
                                              or PipelineElementType.Svanehals:
                        var pp = f.Ports.Take(2).ToArray();
                        g.Members.Add(new NtrPipe
                        {
                            A = pp[0].Node.Pos,
                            B = pp[1].Node.Pos,
                            Dn = InferMainDn(topo, f),
                            Provenance = [f.Source]
                        });
                        break;

                    case TFitting f when f.Kind is PipelineElementType.F_Model
                                              or PipelineElementType.Y_Model:
                        g.Members.Add(new NtrStub { Provenance = [f.Source] });
                        break;

                    // Welding → ignore
                    case TFitting f when f.Kind == PipelineElementType.Svejsning:
                        break;
                }
            }
            return g;
        }

        // DN inference stubs you’ll wire to your schedule/segment tree
        private static int InferMainDn(Topology t, TFitting f) => 200;
        private static int InferBranchDn(Topology t, TFitting f) => 100;
        private static int InferDn1(Topology t, TFitting f) => 200;
        private static int InferDn2(Topology t, TFitting f) => 100;
    }
}
