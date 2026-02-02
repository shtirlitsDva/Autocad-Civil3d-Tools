using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;

using Dreambuild.AutoCAD;

using IntersectUtilities.UtilsCommon;
using IntersectUtilities.UtilsCommon.Enums;

using System;
using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities
{
    public partial class Intersect : IExtensionApplication
    {
#if DEBUG
        private class MyData
        {
            public int Number { get; set; }
            public string? Text { get; set; }
        }

        [CommandMethod("testing", CommandFlags.UsePickSet)]
        public void testing()
        {
            DocumentCollection docCol = Application.DocumentManager;
            Database localDb = docCol.MdiActiveDocument.Database;
            Editor ed = docCol.MdiActiveDocument.Editor;
            Document doc = docCol.MdiActiveDocument;
            CivilDocument civilDoc = CivilApplication.ActiveDocument;
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    #region Test mleader props
                    //var mldrId = Interaction.GetEntity("Select MLeader: ", typeof(MLeader));
                    //MLeader mldr = mldrId.Go<MLeader>(tx);

                    ////DebugHelper.CreateDebugLine(mldr.TextLocation, ColorByName("red"));
                    ////DebugHelper.CreateDebugLine(mldr.BlockPosition, ColorByName("yellow"));
                    
                    //prdDbg($"{string.Join(", ", mldr.GetLeaderIndexes().ToArray().Select(x => x.ToString()))}");
                    //DebugHelper.CreateDebugLine(mldr.GetFirstVertex(0), ColorByName("green"));
                    //DebugHelper.CreateDebugLine(mldr.GetLastVertex(0), ColorByName("red"));
                    #endregion
                    #region Test fillet #2
                    //// 1) Select branch centerline (any WCS line)
                    //var peBranch = new PromptEntityOptions("\nSelect branch centerline (Line): ");
                    //peBranch.SetRejectMessage("\nOnly Line.");
                    //peBranch.AddAllowedClass(typeof(Line), true);
                    //var brSel = ed.GetEntity(peBranch);
                    //if (brSel.Status != PromptStatus.OK) return;

                    //// 2) Select main center point M (DBPoint)
                    //var peMain = new PromptEntityOptions("\nSelect main center (DBPoint): ");
                    //peMain.SetRejectMessage("\nOnly DBPoint.");
                    //peMain.AddAllowedClass(typeof(DBPoint), true);
                    //var mSel = ed.GetEntity(peMain);
                    //if (mSel.Status != PromptStatus.OK) return;

                    //// 3) Radius R and stub length L
                    //var pR = ed.GetDouble(new PromptDoubleOptions("\nEnter fillet radius R (>0): ") { AllowNegative = false, AllowZero = false });
                    //if (pR.Status != PromptStatus.OK) return;
                    //var pL = ed.GetDouble(new PromptDoubleOptions("\nEnter stub length L (>0): ") { AllowNegative = false, AllowZero = false });
                    //if (pL.Status != PromptStatus.OK) return;
                    //double R = pR.Value, L = pL.Value;

                    //const double tol = 1e-9;

                    //using (var tr = localDb.TransactionManager.StartTransaction())
                    //{
                    //    var br = (Line)tr.GetObject(brSel.ObjectId, OpenMode.ForRead);
                    //    var mp = (DBPoint)tr.GetObject(mSel.ObjectId, OpenMode.ForRead);

                    //    // 4) Geometry setup
                    //    var A0 = br.StartPoint; var A1 = br.EndPoint; var Mpt = mp.Position;
                    //    if ((A1 - A0).Length < tol) { ed.WriteMessage("\nInvalid branch line."); return; }
                    //    if (Math.Abs(A0.Z - A1.Z) > 1e-6 || Math.Abs(Mpt.Z - A0.Z) > 1e-6)
                    //    { ed.WriteMessage("\nAll inputs must lie in the same WCS Z plane."); return; }

                    //    Vector2d To2D(Point3d p) => new Vector2d(p.X, p.Y);
                    //    var A0v = To2D(A0);
                    //    var A1v = To2D(A1);
                    //    var Mv = To2D(Mpt);

                    //    // Unit direction along branch
                    //    var u3 = (A1 - A0).GetNormal();
                    //    var u = new Vector2d(u3.X, u3.Y).GetNormal();
                    //    if (u.Length < 0.5) u = (A1v - A0v).GetNormal();

                    //    // Projection-based side selection (orientation-free)
                    //    double s = (Mv - A0v).DotProduct(u);
                    //    var P = A0v + s * u;                 // foot of perpendicular from M to branch
                    //    var w = Mv - P;
                    //    double dLineToM = w.Length;
                    //    if (dLineToM < 1e-12) { ed.WriteMessage("\nAmbiguous: main center lies on the branch line."); return; }
                    //    var n = w / dLineToM;                // unit normal from branch toward M

                    //    // Along-branch side of M (independent of endpoint order)
                    //    double signU = Math.Sign((Mv - A0v).DotProduct(u));
                    //    if (signU == 0) signU = Math.Sign((Mv - A1v).DotProduct(u));
                    //    if (signU == 0) signU = 1;           // fallback

                    //    // 6) Offset branch by R toward M side: centers lie on this line
                    //    var OR0 = A0v + R * n;               // a point on the offset line parallel to branch

                    //    // Required |MO| for stub length: A = sqrt(R^2 + L^2)
                    //    double A = Math.Sqrt(R * R + L * L);

                    //    // 7) Projection of M onto the offset line L_R = OR0 + t*u
                    //    double sProj = (Mv - OR0).DotProduct(u);
                    //    var Q = OR0 + sProj * u;
                    //    double dPerp = Math.Abs((Mv - OR0).DotProduct(n)); // distance from M to L_R
                    //    if (dPerp > A + 1e-9) { ed.WriteMessage("\nNo solution: dist(M, offset line) > sqrt(R^2+L^2)."); return; }

                    //    // 8) Two center candidates along L_R at distance h from Q
                    //    double h = Math.Sqrt(Math.Max(0.0, A * A - dPerp * dPerp));
                    //    var O2D_1 = Q + h * u;
                    //    var O2D_2 = Q - h * u;

                    //    // Order centers so the first matches along-branch side w.r.t Q
                    //    Vector2d first = (Math.Sign((O2D_1 - Q).DotProduct(u)) == signU) ? O2D_1 : O2D_2;
                    //    Vector2d second = (first == O2D_1) ? O2D_2 : O2D_1;

                    //    // Try a center and return deterministic pick
                    //    (bool ok, Point3d O, Point3d Bp, Point3d C, Line stub, Arc arc) Build(Vector2d O2D)
                    //    {
                    //        var O = new Point3d(O2D.X, O2D.Y, A0.Z);
                    //        // B' = O - R*n (perpendicular foot to branch)
                    //        var Bp = new Point3d(O.X - R * n.X, O.Y - R * n.Y, O.Z);

                    //        // Tangent points from M to circle (O,R) using power-of-a-point
                    //        Vector2d OM = new Vector2d(Mpt.X - O.X, Mpt.Y - O.Y);
                    //        double d = OM.Length; if (d <= R + 1e-9) return (false, default, default, default, null, null);

                    //        Vector2d rhat = OM / d;
                    //        Vector2d nhat = new Vector2d(-rhat.Y, rhat.X);
                    //        var Ptan = new Point3d(
                    //            O.X + (R * R / (d * d)) * (Mpt.X - O.X),
                    //            O.Y + (R * R / (d * d)) * (Mpt.Y - O.Y),
                    //            O.Z);
                    //        double k = (R / d) * Math.Sqrt(d * d - R * R);

                    //        var C1 = new Point3d(Ptan.X + k * nhat.X, Ptan.Y + k * nhat.Y, O.Z);
                    //        var C2 = new Point3d(Ptan.X - k * nhat.X, Ptan.Y - k * nhat.Y, O.Z);

                    //        (Point3d C, Line stub, Arc arc)? TryC(Point3d Ccand)
                    //        {
                    //            var t = new Vector2d(Mpt.X - Ccand.X, Mpt.Y - Ccand.Y);
                    //            if (Math.Abs(t.Length - L) > 1e-6 || t.Length < 1e-12) return null; // exact stub length
                    //            if (t.DotProduct(n) <= 0) return null;                               // correct side across normal
                    //            double tu = t.DotProduct(u);
                    //            if (Math.Sign(tu) != signU) return null;                              // correct along-branch direction

                    //            // Minor arc from B' to C
                    //            double Ang(Point3d Pn) => Math.Atan2(Pn.Y - O.Y, Pn.X - O.X);
                    //            double aB = Ang(Bp), aC = Ang(Ccand);
                    //            double Norm(double a) { a %= 2 * Math.PI; if (a < 0) a += 2 * Math.PI; return a; }
                    //            double sweep = Norm(aC - aB);
                    //            if (sweep > Math.PI) { var tmp = aB; aB = aC; aC = tmp; }

                    //            var arc = new Arc(O, R, aB, aC);
                    //            var stub = new Line(Ccand, Mpt);
                    //            return (Ccand, stub, arc);
                    //        }

                    //        var s1 = TryC(C1);
                    //        var s2 = TryC(C2);
                    //        if (s1 == null && s2 == null) return (false, default, default, default, null, null);

                    //        // If both valid, pick the one with smaller |sweep| deterministically
                    //        (Point3d C, Line stub, Arc arc)? pick = s1 ?? s2;
                    //        if (s1 != null && s2 != null)
                    //        {
                    //            double abs1 = Math.Abs(s1.Value.arc.EndAngle - s1.Value.arc.StartAngle);
                    //            double abs2 = Math.Abs(s2.Value.arc.EndAngle - s2.Value.arc.StartAngle);
                    //            if (abs2 < abs1) pick = s2.Value;
                    //        }
                    //        return (true, O, Bp, pick.Value.C, pick.Value.stub, pick.Value.arc);
                    //    }

                    //    var got = Build(first);
                    //    if (!got.ok) got = Build(second);
                    //    if (!got.ok) { ed.WriteMessage("\nNo admissible solution for given inputs."); return; }

                    //    // 10) Create entities
                    //    var bt = (BlockTable)tr.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    //    var btr = (BlockTableRecord)tr.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);

                    //    btr.AppendEntity(got.arc); tr.AddNewlyCreatedDBObject(got.arc, true);
                    //    btr.AppendEntity(got.stub); tr.AddNewlyCreatedDBObject(got.stub, true);

                    //    tr.Commit();

                    //    ed.WriteMessage($"\nO=({got.O.X:F6},{got.O.Y:F6})  B'=({got.Bp.X:F6},{got.Bp.Y:F6})  C=({got.C.X:F6},{got.C.Y:F6})");
                    //}
                    #endregion
                    #region Test fillet #1
                    //// --- INPUTS (hard-coded for your test) --------------------------------------------
                    //// R=20, L=5, M=(40,15). Branch line lies on x-axis through origin.
                    //// We'll still ask you to select the branch line, but we verify it is y=0 and passes near (0,0).
                    //const double R = 20.0;           // expected
                    //const double L = 5.0;            // expected
                    //var M = new Point3d(40.0, 15.0, 0.0); // expected

                    //prdDbg($"Inputs: R={R}, L={L}, M=({M.X},{M.Y})  [expected R=20, L=5, M=(40,15)]");

                    //// --- 1) Select branch centerline --------------------------------------------------
                    //var peBranch = new PromptEntityOptions("\nSelect branch centerline (Line on x-axis through origin): ");
                    //peBranch.SetRejectMessage("\nOnly Line.");
                    //peBranch.AddAllowedClass(typeof(Line), true);
                    //var brSel = ed.GetEntity(peBranch);
                    //if (brSel.Status != PromptStatus.OK) return;

                    //using (var tr = localDb.TransactionManager.StartTransaction())
                    //{
                    //    var br = (Line)tr.GetObject(brSel.ObjectId, OpenMode.ForRead);

                    //    // --- 2) Validate branch lies on y=0 -------------------------------------------
                    //    var A0 = br.StartPoint; var A1 = br.EndPoint;
                    //    prdDbg($"Branch endpoints A0=({A0.X},{A0.Y})  A1=({A1.X},{A1.Y})  [expected Y≈0]");

                    //    const double tol = 1e-9;
                    //    if (Math.Abs(A0.Y) > 1e-6 || Math.Abs(A1.Y) > 1e-6)
                    //    { prdDbg("Error: branch not on y=0."); return; }

                    //    // --- 3) Unit direction along branch and its left normal -----------------------
                    //    var u3 = (A1 - A0).GetNormal();
                    //    var u = new Vector2d(u3.X, u3.Y).GetNormal();         // expected ~ (1,0)
                    //    var nL = new Vector2d(-u.Y, u.X);                      // expected left normal ~ (0,1)
                    //    prdDbg($"u=({u.X:F6},{u.Y:F6})  nL=({nL.X:F6},{nL.Y:F6})  [expected u≈(1,0), nL≈(0,1)]");

                    //    // --- 4) Decide side automatically: where M lies relative to branch direction --
                    //    // cross(u, M - A0) > 0 => M is to the "left" of u -> choose left normal
                    //    var MA0 = new Vector2d(M.X - A0.X, M.Y - A0.Y);
                    //    double cross = u.X * MA0.Y - u.Y * MA0.X;
                    //    prdDbg($"cross = {cross:F6}  [expected >0 since M.y=15 above branch]");
                    //    if (Math.Abs(cross) < 1e-12) { prdDbg("Ambiguous side: M collinear with branch."); return; }
                    //    var n = cross > 0 ? nL : -nL;                           // expected n=(0,1)
                    //    prdDbg($"Chosen normal n=({n.X:F6},{n.Y:F6})  [expected (0,1)]");

                    //    // --- 5) Build offset branch line L_R (centers lie on it) ----------------------
                    //    // Any point on L_R: OR0 = A0 + R*n  -> expected y=R = 20
                    //    var A0v = new Vector2d(A0.X, A0.Y);
                    //    var OR0 = A0v + R * n;                                  // expected (A0.x, 20)
                    //    prdDbg($"OR0 on offset line = ({OR0.X:F6},{OR0.Y:F6})  [expected y=20]");

                    //    // --- 6) Required |MO| from stub length: A = sqrt(R^2 + L^2) -------------------
                    //    double A = Math.Sqrt(R * R + L * L);                    // expected sqrt(425)=20.615528...
                    //    prdDbg($"A = sqrt(R^2+L^2) = {A:F9}  [expected 20.615528128]");

                    //    // --- 7) Project M to offset line to get foot Q and perpendicular distance -----
                    //    var Mv = new Vector2d(M.X, M.Y);
                    //    double sProj = (Mv - OR0).DotProduct(u);
                    //    var Q = OR0 + sProj * u;                                 // expected Q=(40,20)
                    //    double dPerp = (Mv - Q).Length;                          // expected 5
                    //    prdDbg($"Q=({Q.X:F6},{Q.Y:F6}), dPerp={dPerp:F9}  [expected Q=(40,20), dPerp=5]");

                    //    if (dPerp > A + 1e-9) { prdDbg("No solution: dist(M, L_R) > A."); return; }

                    //    // --- 8) Centers are at distance h along L_R from Q ----------------------------
                    //    double h = Math.Sqrt(Math.Max(0.0, A * A - dPerp * dPerp)); // expected sqrt(425-25)=20
                    //    var O2D_1 = Q + h * u;                                     // expected (60,20)
                    //    var O2D_2 = Q - h * u;                                     // expected (20,20)
                    //    prdDbg($"h={h:F9}  O1=({O2D_1.X:F6},{O2D_1.Y:F6})  O2=({O2D_2.X:F6},{O2D_2.Y:F6})  [expected O1=(60,20), O2=(20,20)]");

                    //    // --- 9) Prefer the center on the same x-side as the branch "to the left of M" -
                    //    // For this test we expect LEFT of M, so pick O with X <= M.X i.e., O2=(20,20)
                    //    Vector2d O2D = (O2D_2.X <= M.X) ? O2D_2 : O2D_1;
                    //    var O = new Point3d(O2D.X, O2D.Y, 0.0);
                    //    prdDbg($"Chosen O=({O.X:F6},{O.Y:F6})  [expected (20,20)]");

                    //    // --- 10) B' is perpendicular foot from O to branch (shift back by R along n) --
                    //    var Bp = new Point3d(O.X - R * n.X, O.Y - R * n.Y, 0.0);   // expected (20,0)
                    //    prdDbg($"B' (branch tangency) = ({Bp.X:F6},{Bp.Y:F6})  [expected (20,0)]");

                    //    // --- 11) Compute tangent point(s) from M to circle (O,R)  ---------------------
                    //    // CORRECT FORMULA (fixes earlier bug):
                    //    // Let d=|OM|, rhat=(M-O)/d, nhat=left_perp(rhat).
                    //    // P = O + (R^2/d^2)*(M-O) is the projection of O onto the tangent chord.
                    //    // k = (R/d)*sqrt(d^2 - R^2).
                    //    // C± = P ± k * nhat.
                    //    Vector2d OM = new Vector2d(M.X - O.X, M.Y - O.Y);
                    //    double d = OM.Length;                                     // expected d=A=20.6155
                    //    prdDbg($"d=|OM| = {d:F9}  [expected 20.615528128]");

                    //    if (d <= R + 1e-9) { prdDbg("No external tangents: d <= R."); return; }

                    //    Vector2d rhat = OM / d;
                    //    Vector2d nhat = new Vector2d(-rhat.Y, rhat.X);
                    //    var P = new Point3d(
                    //        O.X + (R * R / (d * d)) * (M.X - O.X),
                    //        O.Y + (R * R / (d * d)) * (M.Y - O.Y), 0.0);          // expected P lies on line OM
                    //    double k = (R / d) * Math.Sqrt(d * d - R * R);            // expected k=(R/d)*L
                    //    var C1 = new Point3d(P.X + k * nhat.X, P.Y + k * nhat.Y, 0.0);
                    //    var C2 = new Point3d(P.X - k * nhat.X, P.Y - k * nhat.Y, 0.0);

                    //    // Distances to M should be exactly L
                    //    double CM1 = Math.Sqrt(Math.Pow(M.X - C1.X, 2) + Math.Pow(M.Y - C1.Y, 2));
                    //    double CM2 = Math.Sqrt(Math.Pow(M.X - C2.X, 2) + Math.Pow(M.Y - C2.Y, 2));

                    //    prdDbg($"P=({P.X:F9},{P.Y:F9}), k={k:F9}");
                    //    prdDbg($"C1=({C1.X:F9},{C1.Y:F9}), |C1M|={CM1:F9}  [expected |C1M|=5]");
                    //    prdDbg($"C2=({C2.X:F9},{C2.Y:F9}), |C2M|={CM2:F9}  [expected |C2M|=5]");
                    //    // For this test with O=(20,20), expected tangency points are ~ (40,20) and (37.64705882, 10.58823529)

                    //    // --- 12) Pick the tangency on the "toward M" side of the branch ----------------
                    //    // Use projection of stub vector on the chosen normal n (should be >0).
                    //    Vector2d t1 = new Vector2d(M.X - C1.X, M.Y - C1.Y);
                    //    Vector2d t2 = new Vector2d(M.X - C2.X, M.Y - C2.Y);
                    //    double p1 = t1.DotProduct(n);
                    //    double p2 = t2.DotProduct(n);
                    //    prdDbg($"Projections: t1·n={p1:F9}, t2·n={p2:F9}  [expect >0 for the correct one]");

                    //    Point3d C;
                    //    if (p1 > 0 && Math.Abs(CM1 - L) < 1e-6) C = C1;
                    //    else if (p2 > 0 && Math.Abs(CM2 - L) < 1e-6) C = C2;
                    //    else { prdDbg("No admissible tangent point meets side and length."); return; }

                    //    prdDbg($"Chosen C=({C.X:F9},{C.Y:F9})  [expected one of (40,20) or (37.64705882,10.58823529)]");

                    //    // --- 13) Build entities: arc from B' to C, and stub C->M -----------------------
                    //    double Ang(Point3d Pnt) => Math.Atan2(Pnt.Y - O.Y, Pnt.X - O.X);
                    //    double aB = Ang(Bp);                     // start at B'
                    //    double aC = Ang(C);                      // end at C

                    //    // Make minor arc
                    //    double Norm(double a) { a %= 2 * Math.PI; if (a < 0) a += 2 * Math.PI; return a; }
                    //    double sweep = Norm(aC - aB);
                    //    if (sweep > Math.PI) { var tmp = aB; aB = aC; aC = tmp; }

                    //    var arc = new Arc(O, R, aB, aC);
                    //    var stub = new Line(C, M);

                    //    var bt = (BlockTable)tr.GetObject(localDb.BlockTableId, OpenMode.ForRead);
                    //    var btr = (BlockTableRecord)tr.GetObject(localDb.CurrentSpaceId, OpenMode.ForWrite);
                    //    btr.AppendEntity(arc); tr.AddNewlyCreatedDBObject(arc, true);
                    //    btr.AppendEntity(stub); tr.AddNewlyCreatedDBObject(stub, true);

                    //    // Optional markers:
                    //    // btr.AppendEntity(new DBPoint(Bp)); tr.AddNewlyCreatedDBObject(Bp, true);
                    //    // btr.AppendEntity(new DBPoint(O));  tr.AddNewlyCreatedDBObject(O,  true);
                    //    // btr.AppendEntity(new DBPoint(C));  tr.AddNewlyCreatedDBObject(C,  true);

                    //    tr.Commit();

                    //    prdDbg($"RESULT  O=({O.X:F6},{O.Y:F6})  B'=({Bp.X:F6},{Bp.Y:F6})  C=({C.X:F6},{C.Y:F6})");
                    //    prdDbg("Done.");
                    //}
                    #endregion
                    #region Test intersection points of vectors
                    //var pls = localDb.HashSetOfType<Polyline>(tx);
                    //foreach (var p in pls)
                    //{
                    //    for (int i = 0; i < p.NumberOfVertices; i++)
                    //    {
                    //        if (p.GetSegmentType(i) != SegmentType.Arc) continue;

                    //        var arc = p.GetArcSegment2dAt(i);

                    //        var s = arc.StartPoint;
                    //        var e = arc.EndPoint;
                    //        var c = arc.Center;

                    //        var rs = s - c;
                    //        var re = e - c;

                    //        var ts = new Vector2d(-rs.Y, rs.X);
                    //        var te = new Vector2d(-re.Y, re.X);

                    //        var denom = ts.X * te.Y - ts.Y * te.X;

                    //        if (Math.Abs(denom) < 1e-9) 
                    //        {
                    //            prdDbg($"Parallel tangents! {denom} {ts} {te}");
                    //            continue;
                    //        }

                    //        var es = e - s;
                    //        var l = (es.X * te.Y - es.Y * te.X) / denom;

                    //        var inter = s + ts.MultiplyBy(l);

                    //        var dp = new DBPoint(inter.To3d());
                    //        dp.AddEntityToDbModelSpace(localDb);

                    //        DebugHelper.CreateDebugLine(s.To3d(), inter.To3d());
                    //        DebugHelper.CreateDebugLine(e.To3d(), inter.To3d());
                    //    }
                    //}
                    #endregion
                    #region Test transform LHN dim
                    //var pls = localDb.HashSetOfType<Polyline>(tx, true);
                    //foreach (var pl in pls)
                    //{
                    //    var str = PropertySetManager.ReadNonDefinedPropertySetString(
                    //        pl, "FJV_dimensionering_VERIFI", "PipeType");

                    //    str = str.Replace("DN", "");

                    //    var dn = Convert.ToInt32(str);

                    //    var layer = PipeScheduleV2.PipeScheduleV2.GetLayerName(
                    //        dn, PipeSystemEnum.Stål, PipeTypeEnum.Twin);

                    //    localDb.CheckOrCreateLayer(layer);

                    //    pl.CheckOrOpenForWrite();
                    //    pl.Layer = layer;
                    //}                    
                    #endregion
                    #region Test PVI grades
                    //var id = Interaction.GetEntity("Select profile: ", typeof(Profile));
                    //if (id == Oid.Null) { tx.Abort(); return; }
                    //var p = id.Go<Profile>(tx);
                    //double GI = double.NaN;
                    //double GO = double.NaN;
                    //foreach (ProfilePVI pvi in p.PVIs)
                    //{
                    //    try { GI = pvi.GradeIn; }
                    //    catch { GI = double.NaN; }
                    //    try { GO = pvi.GradeOut; }
                    //    catch { GO = double.NaN; }

                    //    prdDbg($"Type:{pvi.PVIType}; GI:{GI}; GO:{GO};");
                    //}
                    #endregion
                    #region Test GetClosestPoint(p, v3d, bool) to see what happens outside of range
                    //{
                    //    var ent1 = Interaction.GetEntity("Select polyline: ", typeof(Polyline));
                    //    if (ent1 == Oid.Null) { tx.Abort(); return; }

                    //    var p = Interaction.GetPoint("Select point: ");
                    //    if (p.IsNull()) { tx.Abort(); return; }

                    //    var pl = ent1.Go<Polyline>(tx);
                    //    var cp = pl.GetClosestPointTo(p, -Vector3d.XAxis, false);

                    //    DebugHelper.CreateDebugLine(p, cp, ColorByName("red"));

                    //    //Findings are truly horrific
                    //    //Using a vector as the direction does not at all work as expected
                    //}
                    #endregion
                    #region Find out AssemblyResolve subscribers
                    {
                        //AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                        //{
                        //    var stack = new StackTrace();
                        //    Console.WriteLine("AssemblyResolve triggered for: " + args.Name);
                        //    Console.WriteLine(stack);

                        //    return null; // Let others handle it
                        //};

                        //Assembly.Load("System.Drawing.Test");
                    }
                    #endregion
                    #region Test alignment intersection with pl3d at very close distance and large coords
                    ////var mpgs = localDb.HashSetOfType<MPolygon>(tx);
                    //Alignment al = localDb.ListOfType<Alignment>(tx).First();
                    ////Polyline pline = al.GetPolyline().Go<Polyline>(tx);

                    //var pl3ds = localDb.HashSetOfType<Polyline3d>(tx);

                    //foreach (var pl3d in pl3ds)
                    //{
                    //    //using (Curve c1 = al.GetProjectedCurve(new Plane(), Vector3d.ZAxis))
                    //    //using (Curve c2 = pl3d.GetProjectedCurve(new Plane(), Vector3d.ZAxis))
                    //    //using (Point3dCollection p3dcol = new Point3dCollection())
                    //    //{
                    //    //    c1.IntersectWith(
                    //    //        c2, Autodesk.AutoCAD.DatabaseServices.Intersect.OnBothOperands,
                    //    //        new Plane(),
                    //    //        p3dcol, IntPtr.Zero, IntPtr.Zero);

                    //    //    foreach (Point3d p in p3dcol)
                    //    //    {
                    //    //        DebugHelper.CreateDebugLine(p, ColorByName("red"));

                    //    //        var tp1 = c1.GetClosestPointTo(p, false);
                    //    //        var tp2 = c2.GetClosestPointTo(p, false);

                    //    //        using (Line tl1 = new Line(p, tp1))
                    //    //        using (Line tl2 = new Line(p, tp2))
                    //    //        using (Line tl3 = new Line(tp1, tp2))
                    //    //        {
                    //    //            if (
                    //    //                //tl1.Length < Tolerance.Global.EqualPoint &&
                    //    //                //tl2.Length < Tolerance.Global.EqualPoint &&
                    //    //                tl3.Length < Tolerance.Global.EqualPoint
                    //    //                ) prdDbg("Correct intersection result!");
                    //    //            else
                    //    //            {
                    //    //                prdDbg(
                    //    //                    //$"1: {tl1.Length} > {Tolerance.Global.EqualPoint} -> {tl1.Length > Tolerance.Global.EqualPoint}\n" +
                    //    //                    //$"2: {tl2.Length} > {Tolerance.Global.EqualPoint} -> {tl2.Length > Tolerance.Global.EqualPoint}\n" +
                    //    //                    $"3: {tl3.Length} > {Tolerance.Global.EqualPoint} -> {tl3.Length > Tolerance.Global.EqualPoint}");
                    //    //                prdDbg("Wrong intersection result!");
                    //    //            }
                    //    //        }
                    //    //    }
                    //    //}

                    //    var pnts = al.IntersectWithValidation(pl3d);

                    //    prdDbg(pnts.Count);
                    //}

                    //var line = NTSConversion.ConvertPlineToNTSLineString(pline);
                    //foreach (MPolygon mpg in mpgs)
                    //{
                    //    var pgn = NTSConversion.ConvertMPolygonToNTSPolygon(mpg);
                    //    var result = pgn.Intersects(line);
                    //    prdDbg($"{mpg.Handle} intersects: {result}");
                    //}
                    //pline.CheckOrOpenForWrite();
                    //pline.Erase(true);
                    #endregion
                    #region Test XRecord Has data entry
                    //var store = localDb.FlexDataStore();
                    //var hasData = store.Has("MyData");
                    //prdDbg($"Has data: {hasData}");
                    //store.RemoveEntry("MyData");
                    //hasData = store.Has("MyData");
                    //prdDbg($"Has data: {hasData}");
                    #endregion
                    #region Test XRecord remove data entry
                    //var store = localDb.FlexDataStore();
                    //store.RemoveEntry("MyData");
                    #endregion
                    #region Test XRecord binary data load
                    //var store = localDb.FlexDataStore();
                    //var obj = store.GetObject<MyData>("MyData");
                    //if (obj == null) prdDbg("Object is null!");
                    //prdDbg($"Number: {obj.Number}, Text: {obj.Text}");
                    #endregion
                    #region Test XRecord binary data save
                    //var store = localDb.FlexDataStore();
                    //var data = new MyData() { Number = 5, Text = "Hello" };
                    //store.SetObject("MyData", data);
                    #endregion
                    #region Test database filename
                    //var file = localDb.Filename;
                    //var oFile = localDb.OriginalFileName;
                    //prdDbg($"File: {file}\nOriginal: {oFile}");
                    #endregion
                    #region Test layer names list and xrefs
                    //LayerTable lt = tx.GetObject(localDb.LayerTableId, OpenMode.ForRead) as LayerTable;
                    //foreach (var id in lt)
                    //{
                    //    LayerTableRecord ltr = id.Go<LayerTableRecord>(tx);
                    //    if (ltr.Name.Contains("0-KOMPONENT-HATCH"))
                    //    {
                    //        prdDbg(ltr.Name);
                    //        ltr.UpgradeOpen();
                    //        ltr.IsFrozen = !ltr.IsFrozen;
                    //    }
                    //}
                    #endregion
                    #region Debug component properties
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect block to read parameter: ");
                    //peo.SetRejectMessage("\nNot a block!");
                    //peo.AddAllowedClass(typeof(BlockReference), true);
                    //PromptEntityResult per = ed.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //SetDynBlockPropertyObject(br, "PIPESIZE", 80);
                    //SetDynBlockPropertyObject(br, "SYSNAVN", "PRTFLEX");
                    //br.AttSync();
                    #endregion
                    #region Test geometry of geojson
                    //string path = string.Empty;
                    //OpenFileDialog dialog = new OpenFileDialog()
                    //{
                    //    Title = "Choose geojson file: ",
                    //    DefaultExt = "geojson",
                    //    Filter = "geojson files (*.geojson)|*.geojson|All files (*.*)|*.*",
                    //    FilterIndex = 0
                    //};
                    //if (dialog.ShowDialog() == true)
                    //{
                    //    path = dialog.FileName;
                    //}
                    //else return;
                    //string json = File.ReadAllText(path);
                    //var reader = new NetTopologySuite.IO.GeoJsonReader();
                    //var geos = reader.Read<FeatureCollection>(json);
                    //foreach (IFeature geo in geos)
                    //{
                    //    prdDbg($"{geo.Geometry.Centroid}");
                    //}
                    #region Test writing polygon linerings
                    //var geoJsonObj = JsonNode.Parse(json);
                    //var features = geoJsonObj["features"].AsArray();
                    //foreach (var feature in features)
                    //{
                    //    string handle = feature["properties"]["Handle"].ToString();
                    //    //if (handle != "2978D") continue;
                    //    try
                    //    {
                    //        var geometry = feature["geometry"];
                    //        //var geom = reader.Read<NetTopologySuite.Geometries.Geometry>(geometry.ToString());
                    //        var coordinates = geometry["coordinates"].AsArray();
                    //        foreach (JsonArray sequence in coordinates.AsArray())
                    //        {
                    //            Polyline pline = new Polyline(sequence.Count);
                    //            for (int i = 0; i < sequence.Count; i++)
                    //            {
                    //                var point = sequence[i];
                    //                var val = point.Deserialize<double[]>();
                    //                pline.AddVertexAt(
                    //                    i, new Point2d(val), 0, 0, 0);
                    //            }
                    //            pline.AddEntityToDbModelSpace(localDb);
                    //        }
                    //    }
                    //    catch (System.Exception ex)
                    //    {
                    //        prdDbg($"{handle}: {ex.Message}");
                    //        //throw;
                    //    }
                    //} 
                    #endregion
                    #endregion
                    #region Test reference equality
                    //PromptSelectionResult acSSPrompt;
                    //acSSPrompt = ed.SelectImplied();
                    //SelectionSet acSSet;
                    //if (acSSPrompt.Status == PromptStatus.OK)
                    //{
                    //    acSSet = acSSPrompt.Value;
                    //    var selectedPl3ds = acSSet.GetObjectIds().Select(x => x.Go<Polyline3d>(tx, OpenMode.ForWrite)).ToHashSet();
                    //    HashSet<Polyline3d> allpls = localDb.HashSetOfType<Polyline3d>(tx, true);
                    //    var notSelectedPl3ds = allpls.ExceptWhere(x => selectedPl3ds.Contains(x)).ToHashSet();
                    //    prdDbg(
                    //        $"All pls: {allpls.Count}: {string.Join(", ", allpls.Select(x => x.Handle))},\n" +
                    //        $"Selected: {selectedPl3ds.Count}: {string.Join(", ", selectedPl3ds.Select(x => x.Handle))},\n" +
                    //        $"Not selected: {notSelectedPl3ds.Count}: {string.Join(", ", notSelectedPl3ds.Select(x => x.Handle))}");
                    //}
                    #endregion
                    #region Test deferred execution
                    //List<int> ints = new List<int>() { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16 };
                    //int start = 0;
                    //int end = 5;
                    //int limit = 10;
                    //IEnumerable<int> GetRange(List<int> list, int startGR, int endGR)
                    //{
                    //    for (int i = startGR; i < endGR; i++)
                    //    {
                    //        yield return list[i];
                    //    }
                    //}
                    //IEnumerable<int> query = GetRange(ints, start, end).Where(x => x < limit);
                    //void FirstMethod(IEnumerable<int> query1, ref int start1, ref int end1, ref int limit1)
                    //{
                    //    start1 = 4;
                    //    end1 = 9;
                    //    limit1 = 8;
                    //    SecondMethod(query1);
                    //    start1 = 9;
                    //    end1 = 14;
                    //    limit1 = 16;
                    //    SecondMethod(query1);
                    //}
                    //void SecondMethod(IEnumerable<int> query2)
                    //{
                    //    prdDbg(string.Join(", ", query2));
                    //}
                    //FirstMethod(query, ref start, ref end, ref limit);
                    #endregion
                    #region Test reading profile view style name
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect profile view:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a profile view!");
                    //promptEntityOptions1.AddAllowedClass(typeof(ProfileView), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId pvId = entity1.ObjectId;
                    //var pv = pvId.Go<ProfileView>(tx);
                    //prdDbg(pv.StyleName);
                    #endregion
                    #region Test alignment intersection with MPolygon
                    //var mpgs = localDb.HashSetOfType<MPolygon>(tx);
                    //Alignment al = localDb.ListOfType<Alignment>(tx).First();
                    //Polyline pline = al.GetPolyline().Go<Polyline>(tx);
                    //var line = NTSConversion.ConvertPlineToNTSLineString(pline);
                    //foreach (MPolygon mpg in mpgs)
                    //{
                    //    var pgn = NTSConversion.ConvertMPolygonToNTSPolygon(mpg);
                    //    var result = pgn.Intersects(line);
                    //    prdDbg($"{mpg.Handle} intersects: {result}");
                    //}
                    //pline.CheckOrOpenForWrite();
                    //pline.Erase(true);
                    #endregion
                    #region Test MPolygon to Polygon conversion
                    //var mpgs = localDb.HashSetOfType<MPolygon>(tx);
                    //foreach (MPolygon mpg in mpgs)
                    //{
                    //    var pgn = NTSConversion.ConvertMPolygonToNTSPolygon(mpg);
                    //    prdDbg($"Converted MPolygon {mpg.Handle} to Polygon area {pgn.Area} m²");
                    //}
                    #endregion
                    #region Test new DRO
                    //DataReferencesOptions dro = new DataReferencesOptions();
                    //prdDbg($"{dro.ProjectName}, {dro.EtapeName}");
                    //Application.ShowModelessDialog(new TestSuiteForm());
                    //for (int itemCount = 1; itemCount <= 8; itemCount++)
                    //{
                    //    var form = new StringGridForm(GenerateRandomStrings(itemCount, 5, 12));
                    //    form.ShowDialog();
                    //}
                    //string GenerateRandomString(int length)
                    //{
                    //    var random = new Random();
                    //    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
                    //    var stringChars = new char[length];
                    //    for (int i = 0; i < length; i++)
                    //    {
                    //        stringChars[i] = chars[random.Next(chars.Length)];
                    //    }
                    //    return new string(stringChars);
                    //}
                    //IEnumerable<string> GenerateRandomStrings(int count, int minLength, int maxLength)
                    //{
                    //    var random = new Random();
                    //    var strings = new List<string>();
                    //    for (int i = 0; i < count; i++)
                    //    {
                    //        int length = random.Next(minLength, maxLength + 1);
                    //        strings.Add(GenerateRandomString(length));
                    //    }
                    //    return strings;
                    //}
                    #endregion
                    #region Get points from profile
                    //var pId = Interaction.GetEntity("Select profile: ", typeof(Profile), false);
                    //if (pId == Oid.Null) { tx.Abort(); return; }
                    //Profile p = pId.Go<Profile>(tx);
                    //var pvId = Interaction.GetEntity("Select profile view: ", typeof(ProfileView), false);
                    //if (pvId == Oid.Null) { tx.Abort(); return; }
                    //ProfileView pv = pvId.Go<ProfileView>(tx);
                    //var ss = pv.StationStart;
                    //var se = pv.StationEnd;
                    //List<Point3d> points = new List<Point3d>();
                    ////iterate over length of profile view with a step of 5
                    //for (double i = ss; i < se; i += 5)
                    //{
                    //    double X = 0;
                    //    double Y = 0;
                    //    pv.FindXYAtStationAndElevation(i, p.ElevationAt(i), ref X, ref Y);
                    //    points.Add(new Point3d(X, Y, 0));
                    //}
                    //File.WriteAllText(@"C:\Temp\points.txt", string.Join(
                    //    ";", points.Select(x => 
                    //    $"({x.X.ToString("F2", CultureInfo.InvariantCulture)},{x.Y.ToString("F2", CultureInfo.InvariantCulture)})")));
                    #endregion
                    #region Test PipeScheduleV2
                    //var pls = localDb.GetFjvPipes(tx, true);
                    //HashSet<string> pods = new HashSet<string>();
                    //Stopwatch sw = Stopwatch.StartNew();
                    //foreach (var p in pls)
                    //{
                    //    pods.Add($"DN{PipeScheduleV2.PipeScheduleV2.GetPipeDN(p)} - " +
                    //        $"Rp: {PipeScheduleV2.PipeScheduleV2.GetBuerorMinRadius(p).ToString("F2")}");
                    //}
                    //sw.Stop();
                    //prdDbg($"Time v2: {sw.Elapsed}");
                    //prdDbg(string.Join("\n", pods.OrderByAlphaNumeric(p => p)));
                    //pods.Clear();
                    //sw = Stopwatch.StartNew();
                    //foreach (var p in pls)
                    //{
                    //    pods.Add($"DN{GetPipeDN(p)} - " +
                    //        $"Rp: {GetBuerorMinRadius(p).ToString("F2")}");
                    //}
                    //sw.Stop();
                    //prdDbg($"Time v1: {sw.Elapsed}");
                    //prdDbg(string.Join("\n", pods.OrderByAlphaNumeric(p => p)));
                    #endregion
                    #region Dump pipeschedule data
                    //PipeScheduleV2.PipeScheduleV2.ListAllPipeTypes();
                    #endregion
                    #region Test new PipeSizeArrays
                    //string projectName = "PVF2";
                    //string etapeName = "06.21.04";
                    //System.Data.DataTable dt = CsvData.FK;
                    //// open the xref database
                    //Database alDb = new Database(false, true);
                    //alDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Alignments"),
                    //    System.IO.FileShare.Read, false, string.Empty);
                    //Transaction alTx = alDb.TransactionManager.StartTransaction();
                    //try
                    //{
                    //    var ents = localDb.GetFjvEntities(tx, false, false);
                    //    var als = alDb.HashSetOfType<Alignment>(alTx);
                    //    PipelineNetwork pn = new PipelineNetwork();
                    //    pn.CreatePipelineNetwork(ents, als);
                    //    pn.CreatePipelineGraph();
                    //    pn.CreateSizeArraysAndPrint();
                    //}
                    //catch (System.Exception ex)
                    //{
                    //    alTx.Abort();
                    //    alTx.Dispose();
                    //    alDb.Dispose();
                    //    prdDbg(ex);
                    //    throw;
                    //}
                    //alTx.Abort();
                    //alTx.Dispose();
                    //alDb.Dispose();
                    #endregion
                    #region Testing tolerance when comparing points
                    //PromptEntityOptions peo1 = new PromptEntityOptions("\nSelect first point: ");
                    //peo1.SetRejectMessage("\nNot a DBPoint!");
                    //peo1.AddAllowedClass(typeof(DBPoint), false);
                    //PromptEntityResult per1 = editor.GetEntity(peo1);
                    //DBPoint p1 = per1.ObjectId.Go<DBPoint>(tx);
                    //PromptEntityOptions peo2 = new PromptEntityOptions("\nSelect second point: ");
                    //peo2.SetRejectMessage("\nNot a DBPoint!");
                    //peo2.AddAllowedClass(typeof(DBPoint), false);
                    //PromptEntityResult per2 = editor.GetEntity(peo2);
                    //DBPoint p2 = per2.ObjectId.Go<DBPoint>(tx);
                    //Tolerance tol = new Tolerance(1e-3, 2.54 * 1e-3);
                    //prdDbg(p1.Position.IsEqualTo(p2.Position, tol) + 
                    //    " -> Dist: " + p1.Position.DistanceTo(p2.Position));
                    #endregion
                    #region Martins opgave
                    //HashSet<DBPoint> points = localDb.HashSetOfType<DBPoint>(tx);
                    //CivSurface surface = localDb
                    //    .HashSetOfType<TinSurface>(tx)
                    //    .FirstOrDefault() as CivSurface;
                    //foreach (DBPoint point in points)
                    //{
                    //    double depthToTop =
                    //        PropertySetManager.ReadNonDefinedPropertySetDouble(
                    //            point, "GSMeasurement", "Depth");
                    //    double depthCl = depthToTop + 0.1143 / 2;
                    //    double surfaceElev = surface.FindElevationAtXY(point.Position.X, point.Position.Y);
                    //    double clElevation = surfaceElev - depthCl;
                    //    point.UpgradeOpen();
                    //    point.Position = new Point3d(point.Position.X, point.Position.Y, clElevation);
                    //}
                    #endregion
                    #region Testing pl3d merging
                    ////List<Polyline3d> pls = localDb.ListOfType<Polyline3d>(tx);
                    ////Polyline3d pl = pls.Where(x => x.GetVertices(tx).Length > 4).FirstOrDefault();
                    ////HashSet<DBPoint> points = localDb.HashSetOfType<DBPoint>(tx);
                    ////foreach (DBPoint p in points)
                    ////{
                    ////    Line l = new Line(p.Position, pl.GetClosestPointTo(p.Position, false));
                    ////    l.AddEntityToDbModelSpace(localDb);
                    ////}
                    ////This is for testing ONLY
                    ////The supplied pl3d must be already overlapping
                    ////If you try to merge non - overlapping pl3ds, it will exit with infinite loop
                    //Tolerance tolerance = new Tolerance(1e-3, 2.54 * 1e-3);
                    //List<Polyline3d> pls = localDb.ListOfType<Polyline3d>(tx);
                    ////var pl = pls.First();
                    ////var vertices = pl.GetVertices(tx);
                    ////for (int i = 0; i < vertices.Length; i++)
                    ////{
                    ////    prdDbg(vertices[i].Position);
                    ////}
                    //var mypl3ds = pls.Select(x => new LER2.MyPl3d(x, tolerance)).ToList();
                    //LER2.MyPl3d seed = mypl3ds[0];
                    //var others = mypl3ds.Skip(1);
                    //Polyline3d merged = new Polyline3d(
                    //    Poly3dType.SimplePoly, seed.Merge(others), false);
                    //merged.AddEntityToDbModelSpace(localDb);
                    //foreach (Polyline3d item in pls)
                    //{
                    //    item.UpgradeOpen();
                    //    item.Erase();
                    //}
                    #endregion
                    #region Writing vertex values of poly3d
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect object: ");
                    //peo.SetRejectMessage("\nNot a Polyline3d!");
                    //peo.AddAllowedClass(typeof(Polyline3d), false);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //Polyline3d pl3d = per.ObjectId.Go<Polyline3d>(tx);
                    //PolylineVertex3d[] verts = pl3d.GetVertices(tx);
                    //string result = "";
                    //for (int i = 0; i < verts.Length; i++)
                    //{
                    //    Point3d p = verts[i].Position;
                    //    result += $"[{p.X.ToString("F5")} {p.Y.ToString("F5")} {p.Z.ToString("F5")}]";
                    //}
                    //prdDbg(result);
                    #endregion
                    #region Testing value of Tolerance
                    //prdDbg("EqualPoint: " + Tolerance.Global.EqualPoint); //2.54e-08
                    //prdDbg("EqualVector: " + Tolerance.Global.EqualVector); //1e-08
                    //prdDbg(Tolerance.Global.ToString());
                    #endregion
                    #region Test extension dictionary
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect object: ");
                    //peo.SetRejectMessage("\nNot a DBObject!");
                    //peo.AddAllowedClass(typeof(DBObject), false);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //DBObject obj = per.ObjectId.Go<DBObject>(tx);
                    //Oid extId = obj.ExtensionDictionary;
                    //if (extId != Oid.Null)
                    //{
                    //    DBDictionary extDict = extId.Go<DBDictionary>(tx);
                    //    foreach (DBDictionaryEntry item in extDict)
                    //    {
                    //        prdDbg(item.Key);
                    //    }
                    //}else prdDbg("No extension dictionary found!");
                    #endregion
                    #region Test arc sample points
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);
                    //foreach(BlockReference br in brs)
                    //{
                    //    BlockTableRecord btr = br.BlockTableRecord.GetObject(OpenMode.ForRead) as BlockTableRecord;
                    //    if (btr == null) continue;
                    //    foreach (Oid id in btr)
                    //    {
                    //        Entity member = id.Go<Entity>(tx);
                    //        if (member == null) continue;
                    //        switch (member)
                    //        {
                    //            case Arc arcOriginal:
                    //                {
                    //                    Arc arc = (Arc)arcOriginal.Clone();
                    //                    arc.CheckOrOpenForWrite();
                    //                    arc.TransformBy(br.BlockTransform);
                    //                    double length = arc.Length;
                    //                    double radians = length / arc.Radius;
                    //                    int nrOfSamples = (int)(radians / 0.1);
                    //                    if (nrOfSamples < 3)
                    //                    {
                    //                        DBPoint p = new DBPoint(arc.StartPoint);
                    //                        p.AddEntityToDbModelSpace(localDb);
                    //                        p = new DBPoint(arc.EndPoint);
                    //                        p.AddEntityToDbModelSpace(localDb);
                    //                        p = new DBPoint(arc.GetPointAtDist(arc.Length/2));
                    //                        p.AddEntityToDbModelSpace(localDb);
                    //                    }
                    //                    else
                    //                    {
                    //                        Curve3d geCurve = arc.GetGeCurve();
                    //                        PointOnCurve3d[] samples = geCurve.GetSamplePoints(nrOfSamples);
                    //                        for (int i = 0; i < samples.Length; i++)
                    //                        {
                    //                            DBPoint p = new DBPoint(samples[i].Point);
                    //                            p.AddEntityToDbModelSpace(localDb);
                    //                        }
                    //                    }
                    //                }
                    //                continue;
                    //            default:
                    //                prdDbg(member.GetType().ToString());
                    //                break;
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test alignments connection
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect first alignment: ");
                    //peo.SetRejectMessage("\nNot an alignment!");
                    //peo.AddAllowedClass(typeof(Alignment), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //Alignment al1 = per.ObjectId.Go<Alignment>(tx);
                    //peo = new PromptEntityOptions("\nSelect second alignment: ");
                    //peo.SetRejectMessage("\nNot an alignment!");
                    //peo.AddAllowedClass(typeof(Alignment), true);
                    //per = editor.GetEntity(peo);
                    //Alignment al2 = per.ObjectId.Go<Alignment>(tx);
                    //// Get the start and end points of the alignments
                    //Point3d thisStart = al1.StartPoint;
                    //Point3d thisEnd = al1.EndPoint;
                    //Point3d otherStart = al2.StartPoint;
                    //Point3d otherEnd = al2.EndPoint;
                    //double tol = 0.05;
                    //// Check if any of the endpoints of this alignment are on the other alignment
                    //if (IsOn(al2, thisStart, tol) || IsOn(al2, thisEnd, tol))
                    //    prdDbg("Connected!");
                    //// Check if any of the endpoints of the other alignment are on this alignment
                    //else if (IsOn(al1, otherStart, tol) || IsOn(al1, otherEnd, tol))
                    //    prdDbg("Connected!");
                    //else prdDbg("Not connected!");
                    //bool IsOn(Alignment al, Point3d point, double tolerance)
                    //{
                    //    //double station = 0;
                    //    //double offset = 0;
                    //    //try
                    //    //{
                    //    //    alignment.StationOffset(point.X, point.Y, tolerance, ref station, ref offset);
                    //    //}
                    //    //catch (Exception) { return false; }
                    //    Polyline pline = al.GetPolyline().Go<Polyline>(
                    //        al.Database.TransactionManager.TopTransaction, OpenMode.ForWrite);
                    //    Point3d p = pline.GetClosestPointTo(point, false);
                    //    pline.Erase(true);
                    //    //prdDbg($"{offset}, {Math.Abs(offset)} < {tolerance}, {Math.Abs(offset) <= tolerance}, {station}");
                    //    // If the offset is within the tolerance, the point is on the alignment
                    //    if (Math.Abs(p.DistanceTo(point)) <= tolerance) return true;
                    //    // Otherwise, the point is not on the alignment
                    //    return false;
                    //}
                    #endregion
                    #region Print lineweights enum
                    //foreach (string name in Enum.GetNames(typeof(LineWeight)))
                    //{
                    //    prdDbg(name);
                    //}
                    #endregion
                    #region Test pline to polygon
                    //var plines = localDb.HashSetOfType<Polyline>(tx);
                    //foreach (Polyline pline in plines)
                    //{
                    //    var points = pline.GetSamplePoints();
                    //    for (int i = 0; i < points.Count-1; i++)
                    //    {
                    //        var p1 = points[i];
                    //        var p2 = points[i+1];
                    //        Line line = new Line(p1.To3D(), p2.To3D());
                    //        line.AddEntityToDbModelSpace(localDb);
                    //        DBPoint p = new DBPoint(p1.To3D());
                    //        p.AddEntityToDbModelSpace(localDb);
                    //    }
                    //    DBPoint p3 = new DBPoint(points.Last().To3D());
                    //    p3.AddEntityToDbModelSpace(localDb);
                    //    List<Point2d> fsPoints = new List<Point2d>();
                    //    List<Point2d> ssPoints = new List<Point2d>();
                    //    double halfKOd = GetPipeKOd(pline, true) / 1000.0 / 2;
                    //    for (int i = 0; i < points.Count; i++)
                    //    {
                    //        Point3d samplePoint = points[i].To3D();
                    //        var v = pline.GetFirstDerivative(samplePoint);
                    //        var v1 = v.GetPerpendicularVector().GetNormal();
                    //        var v2 = v1 * -1;
                    //        fsPoints.Add((samplePoint + v1 * halfKOd).To2D());
                    //        ssPoints.Add((samplePoint + v2 * halfKOd).To2D());
                    //    }
                    //    List<Point2d> points = new List<Point2d>();
                    //    points.AddRange(fsPoints);
                    //    ssPoints.Reverse();
                    //    points.AddRange(ssPoints);
                    //    points.Add(fsPoints[0]);
                    //    points = points.SortAndEnsureCounterclockwiseOrder();
                    //}
                    #endregion
                    #region Test sampling
                    //var hatches = localDb.HashSetOfType<Hatch>(tx);
                    //foreach (Hatch hatch in hatches)
                    //{
                    //    for (int i = 0; i < hatch.NumberOfLoops; i++)
                    //    {
                    //        HatchLoop loop = hatch.GetLoopAt(i);
                    //        if (loop.IsPolyline)
                    //        {
                    //            List<BulgeVertex> bvc = loop.Polyline.ToList();
                    //            Point2dCollection points = new Point2dCollection();
                    //            DoubleCollection dc = new DoubleCollection();
                    //            var pointsBvc = bvc.GetSamplePoints();
                    //            foreach (var item in pointsBvc)
                    //            {
                    //                DBPoint p = new DBPoint(item.To3D());
                    //                p.AddEntityToDbModelSpace(localDb);
                    //            }
                    //        }
                    //        else
                    //        {
                    //            HashSet<Point2d> points = new HashSet<Point2d>(
                    //                new Point2dEqualityComparer());
                    //            DoubleCollection dc = new DoubleCollection();
                    //            Curve2dCollection curves = loop.Curves;
                    //            foreach (Curve2d curve in curves)
                    //            {
                    //                switch (curve)
                    //                {
                    //                    case LineSegment2d l2d:
                    //                        points.Add(l2d.StartPoint);
                    //                        points.Add(l2d.EndPoint);
                    //                        continue;
                    //                    case CircularArc2d ca2d:
                    //                        double sPar = ca2d.GetParameterOf(ca2d.StartPoint);
                    //                        double ePar = ca2d.GetParameterOf(ca2d.EndPoint);
                    //                        double length = ca2d.GetLength(sPar, ePar);
                    //                        double radians = length / ca2d.Radius;
                    //                        int nrOfSamples = (int)(radians / 0.25);
                    //                        if (nrOfSamples < 3)
                    //                        {
                    //                            points.Add(ca2d.StartPoint);
                    //                            points.Add(curve.GetSamplePoints(3)[1]);
                    //                            points.Add(ca2d.EndPoint);
                    //                        }
                    //                        else
                    //                        {
                    //                            Point2d[] samples = ca2d.GetSamplePoints(nrOfSamples);
                    //                            foreach (Point2d p2d in samples) points.Add(p2d);
                    //                        }
                    //                        //Point2dCollection pointsCol = new Point2dCollection();
                    //                        foreach (var item in points.SortAndEnsureCounterclockwiseOrder())
                    //                        {
                    //                            DBPoint p = new DBPoint(item.To3D());
                    //                            p.AddEntityToDbModelSpace(localDb);
                    //                        }
                    //                        continue;
                    //                    default:
                    //                        break;
                    //                }
                    //            }
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test hatch loop retreival
                    //int nrOfSamples = (int)(2 * Math.PI / 0.25);
                    //Point2dCollection points = new Point2dCollection(nrOfSamples);
                    //DoubleCollection dc = new DoubleCollection(nrOfSamples);
                    //Circle circle = new Circle(new Point3d(), new Vector3d(0,0,1), 0.22);
                    //Curve3d curve = circle.GetGeCurve();
                    //PointOnCurve3d[] samplePs = curve.GetSamplePoints(nrOfSamples);
                    //foreach (var item in samplePs)
                    //{
                    //    Point3d p3d = item.GetPoint();
                    //    points.Add(new Point2d(p3d.X, p3d.Y));
                    //    dc.Add(0);
                    //}
                    //Hatch hatch = new Hatch();
                    //hatch.AppendLoop(HatchLoopTypes.Default, points, dc);
                    //hatch.AddEntityToDbModelSpace(localDb);
                    //hatch.SetDatabaseDefaults();
                    //hatch.EvaluateHatch(true);
                    #endregion
                    #region Test view frame numbers
                    //var vfs = localDb.ListOfType<ViewFrame>(tx);
                    //if (vfs != null)
                    //{
                    //    foreach (var vf in vfs)
                    //    {
                    //        DBObjectCollection dboc1 = new DBObjectCollection();
                    //        vf.Explode(dboc1);
                    //        foreach (var item in dboc1)
                    //        {
                    //            if (item is BlockReference br)
                    //            {
                    //                DBObjectCollection dboc2 = new DBObjectCollection();
                    //                br.Explode(dboc2);
                    //                foreach (var item2 in dboc2)
                    //                {
                    //                    if (item2 is Polyline pline)
                    //                        prdDbg($"EndParam: {pline.EndParam} - {(int)pline.EndParam}");
                    //                }
                    //            }
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test stikafgreninger DN2
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect block to read parameter: ");
                    //peo.SetRejectMessage("\nNot a block!");
                    //peo.AddAllowedClass(typeof(BlockReference), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //string pathToCatalogue =
                    //    @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv";
                    //if (!File.Exists(pathToCatalogue))
                    //    throw new System.Exception("ComponentData cannot access " + pathToCatalogue + "!");
                    //System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(pathToCatalogue, "DynKomps");
                    //string dn1 = PropertyReader.ReadComponentDN1Str(br, dt);
                    ////prdDbg(dn1);
                    ////string dn2 = PropertyReader.ReadComponentDN2Str(br, dt);
                    ////prdDbg(dn2);
                    //prdDbg(PropertyReader.GetDynamicPropertyByName(br, "DN2").Value.ToString());
                    #endregion
                    #region Test viewport orientation
                    //string blockName = "Nordpil2";
                    //BlockTableRecord paperspace = 
                    //    localDb.BlockTableId.Go<BlockTable>(tx)
                    //    [BlockTableRecord.PaperSpace].Go<BlockTableRecord>(
                    //        tx, OpenMode.ForWrite);
                    //BlockTable bt = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //Oid btrId = bt[blockName];
                    //var br = new BlockReference(new Point3d(808,326,0), btrId);
                    //paperspace.AppendEntity(br);
                    //tx.AddNewlyCreatedDBObject(br, true);
                    //DBDictionary layoutDict = localDb.LayoutDictionaryId.Go<DBDictionary>(tx);
                    //var enumerator = layoutDict.GetEnumerator();
                    //while (enumerator.MoveNext())
                    //{
                    //    DBDictionaryEntry item = enumerator.Current;
                    //    prdDbg(item.Key);
                    //    if (item.Key == "Model")
                    //    {
                    //        prdDbg("Skipping model...");
                    //        continue;
                    //    }
                    //    Layout layout = item.Value.Go<Layout>(tx);
                    //    BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(tx);
                    //    foreach (Oid id in layBlock)
                    //    {
                    //        if (id.IsDerivedFrom<Viewport>())
                    //        {
                    //            Viewport vp = id.Go<Viewport>(tx);
                    //            //Truncate doubles to whole numebers for easier comparison
                    //            int centerX = (int)vp.CenterPoint.X;
                    //            int centerY = (int)vp.CenterPoint.Y;
                    //            if (centerX == 424 && centerY == 222)
                    //            {
                    //                prdDbg("Found main viewport!");
                    //                br.Rotation = vp.TwistAngle;
                    //            }
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test getting versions
                    //string pathToCatalogue = 
                    //    @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv";
                    //if (!File.Exists(pathToCatalogue))
                    //    throw new System.Exception("ComponentData cannot access " + pathToCatalogue + "!");
                    //System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(pathToCatalogue, "DynKomps");
                    //string blockName = "RED KDLR";
                    //var btr = localDb.GetBlockTableRecordByName(blockName);
                    //string version = "";
                    //foreach (Oid oid in btr)
                    //{
                    //    if (oid.IsDerivedFrom<AttributeDefinition>())
                    //    {
                    //        var atdef = oid.Go<AttributeDefinition>(tx);
                    //        if (atdef.Tag == "VERSION") { version = atdef.TextString; break; }
                    //    }
                    //}
                    //if (version.IsNoE()) version = "1";
                    //if (version.Contains("v")) version = version.Replace("v", "");
                    //int blockVersion = Convert.ToInt32(version);
                    //var query = dt.AsEnumerable()
                    //    .Where(x => x["Navn"].ToString() == blockName)
                    //    .Select(x => x["Version"].ToString())
                    //    .Select(x => { if (x == "") return "1"; else return x; })
                    //    .Select(x => Convert.ToInt32(x.Replace("v", "")))
                    //    .OrderBy(x => x);
                    //if (query.Count() == 0)
                    //{
                    //    throw new System.Exception($"Block {blockName} is not present in FJV Dynamiske Komponenter.csv!");
                    //}
                    //int maxVersion = query.Max();
                    //prdDbg(blockVersion == maxVersion);
                    #endregion
                    #region Test polyline parameter and segments and locations
                    ////Conclusion: parameter at point, if truncated, will give vertex idx
                    //#region Ask for point
                    ////message for the ask for point prompt
                    //string message = "Select location to test: ";
                    //var opt = new PromptPointOptions(message);
                    //Point3d location = Algorithms.NullPoint3d;
                    //do
                    //{
                    //    var res = editor.GetPoint(opt);
                    //    if (res.Status == PromptStatus.Cancel)
                    //    {
                    //        tx.Abort();
                    //        return;
                    //    }
                    //    if (res.Status == PromptStatus.OK) location = res.Value;
                    //}
                    //while (location == Algorithms.NullPoint3d);
                    //#endregion
                    //#region Get pipes
                    //HashSet<Polyline> pls = localDb.GetFjvPipes(tx);
                    //if (pls.Count == 0)
                    //{
                    //    prdDbg("No DH pipes in drawing!");
                    //    tx.Abort();
                    //    return;
                    //}
                    //#endregion
                    //Polyline pl = pls
                    //        .MinBy(x => location.DistanceHorizontalTo(
                    //            x.GetClosestPointTo(location, false))
                    //        ).FirstOrDefault();
                    //prdDbg(pl.GetParameterAtPoint(location));
                    #endregion
                    #region Test getting angle between segments
                    //string message = "Select location to place elbow: ";
                    //var opt = new PromptPointOptions(message);
                    //Point3d location = Point3d.Origin;
                    //var res = editor.GetPoint(opt);
                    //if (res.Status == PromptStatus.Cancel)
                    //{
                    //    tx.Abort();
                    //    return;
                    //}
                    //if (res.Status == PromptStatus.OK) location = res.Value;
                    //else { tx.Abort(); return; }
                    //HashSet<Polyline> pls = localDb.GetFjvPipes(tx);
                    //Polyline pl = pls
                    //        .MinBy(x => location.DistanceHorizontalTo(
                    //            x.GetClosestPointTo(location, false))
                    //        ).FirstOrDefault();
                    //int idx = pl.GetIndexAtPoint(location);
                    //if (idx == -1 || idx == 0 || idx == pl.NumberOfVertices - 1) { tx.Abort(); return; }
                    //var sg1 = pl.GetLineSegmentAt(idx);
                    //var sg2 = pl.GetLineSegmentAt(idx - 1);
                    //prdDbg(sg1.Direction.GetAngleTo(sg2.Direction).ToDegrees());
                    //prdDbg(sg1.Direction.CrossProduct(sg2.Direction));
                    #endregion
                    #region Test block values
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect block to read parameter: ");
                    //peo.SetRejectMessage("\nNot a block!");
                    //peo.AddAllowedClass(typeof(BlockReference), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //var pc = br.DynamicBlockReferencePropertyCollection;
                    //foreach (DynamicBlockReferenceProperty prop in pc)
                    //{
                    //    prdDbg(prop.PropertyName + " " + prop.UnitsType);
                    //}
                    //SetDynBlockPropertyObject(br, "DN", 200.ToString());
                    #endregion
                    #region Test dynamic reading of parameters
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect block to read parameter: ");
                    //peo.SetRejectMessage("\nNot a block!");
                    //peo.AddAllowedClass(typeof(BlockReference), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //System.Data.DataTable dt = CsvReader.ReadCsvToDataTable(
                    //                    @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    //prdDbg(br.ReadDynamiskCsvProperty(DynamiskProperty.DN1, dt));
                    #endregion
                    #region Test sideloaded nested block location
                    //Database fremDb = new Database(false, true);
                    //fremDb.ReadDwgFile(@"X:\AutoCAD DRI - 01 Civil 3D\Dev\15 DynBlockSideloaded\BlockDwg.dwg",
                    //    FileOpenMode.OpenForReadAndAllShare, false, null);
                    //Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                    //HashSet<BlockReference> allBrs = fremDb.HashSetOfType<BlockReference>(fremTx);
                    //foreach (var br in allBrs)
                    //{
                    //    BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(fremTx);
                    //    foreach (Oid id in btr)
                    //    {
                    //        if (!id.IsDerivedFrom<BlockReference>()) continue;
                    //        BlockReference nestedBr = id.Go<BlockReference>(fremTx);
                    //        if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                    //        Point3d wPt = nestedBr.Position;
                    //        wPt = wPt.TransformBy(br.BlockTransform);
                    //        //Line line = new Line(new Point3d(), wPt);
                    //        //line.AddEntityToDbModelSpace(localDb);
                    //    }
                    //}
                    //fremTx.Abort();
                    //fremTx.Dispose();
                    //fremDb.Dispose();
                    #endregion
                    #region Test nested block location in dynamic blocks
                    //                    var list = localDb.HashSetOfType<BlockReference>(tx);
                    //                    foreach (var br in list)
                    //                    {
                    //                        BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    //                        foreach (Oid id in btr)
                    //{
                    //                            if (!id.IsDerivedFrom<BlockReference>()) continue;
                    //                            BlockReference nestedBr = id.Go<BlockReference>(tx);
                    //                            if (!nestedBr.Name.Contains("MuffeIntern")) continue;
                    //                            Point3d wPt = nestedBr.Position;
                    //                            wPt = wPt.TransformBy(br.BlockTransform);
                    //                            Line line = new Line(new Point3d(), wPt);
                    //                            line.AddEntityToDbModelSpace(localDb);
                    //                        }
                    //                        //DBObjectCollection objs = new DBObjectCollection();
                    //                        //br.Explode(objs);
                    //                        //foreach (var item in objs)
                    //                        //{
                    //                        //    if (item is BlockReference nBr)
                    //                        //    {
                    //                        //        Line line = new Line(new Point3d(), nBr.Position);
                    //                        //        line.AddEntityToDbModelSpace(localDb);
                    //                        //    }
                    //                        //}
                    //                    }
                    #endregion
                    #region Test constant attribute, constant attr is attached to BlockTableRecord and not BR
                    //PromptEntityOptions peo = new PromptEntityOptions("Select a BR: ");
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //prdDbg(br.GetAttributeStringValue("VERSION"));
                    //foreach (Oid oid in br.AttributeCollection)
                    //{
                    //    AttributeReference ar = oid.Go<AttributeReference>(tx);
                    //    prdDbg($"Name: {ar.Tag}, Text: {ar.TextString}");
                    //}
                    //BlockTableRecord btr = br.BlockTableRecord.Go<BlockTableRecord>(tx);
                    //foreach (Oid oid in btr)
                    //{
                    //    if (oid.IsDerivedFrom<AttributeDefinition>())
                    //    {
                    //        AttributeDefinition attDef = oid.Go<AttributeDefinition>(tx);
                    //        if (attDef.Tag == "VERSION")
                    //        {
                    //            prdDbg($"Constant attribute > Name: {attDef.Tag}, Text: {attDef.TextString}");
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test enum list
                    //StringBuilder sb = new StringBuilder();
                    //HashSet<int> nums = new HashSet<int>()
                    //{
                    //    1, 2, 3, 4, 5, 6, 7, 8
                    //};
                    //foreach (var num in nums)
                    //{
                    //    string f = ((Graph.EndType)num).ToString();
                    //    foreach (var xum in nums)
                    //    {
                    //        string s = ((Graph.EndType)xum).ToString();
                    //        sb.AppendLine($"{f}-{s}");
                    //    }
                    //}
                    //OutputWriter(@"C:\Temp\EntTypeEnum.txt", sb.ToString(), true);
                    #endregion
                    #region test regex
                    //List<string> list = new List<string>()
                    //{
                    //    "0*123*232",
                    //    "234*12*0",
                    //    "0*115*230",
                    //    "000*115*230",
                    //    "0*0*0",
                    //    "255*255*255",
                    //    "231*0*98"
                    //};
                    //foreach (string s in list)
                    //{
                    //    var color = UtilsCommon.Utils.ParseColorString(s);
                    //    if (color == null) prdDbg($"Parsing of string {s} failed!");
                    //    else prdDbg($"Parsing of string {s} success!");
                    //}
                    #endregion
                    #region Create points at vertices
                    //var meter = new ProgressMeter();
                    //string pointLayer = "0-MARKER-POINT";
                    //localDb.CheckOrCreateLayer(pointLayer);
                    //meter.Start("Gathering elements...");
                    //var ids = QuickSelection.SelectAll("LWPOLYLINE")
                    //    .QWhere(x => x.Layer.Contains("Etape"));
                    //meter.SetLimit(ids.Count());
                    //ids.QForEach(x =>
                    //{
                    //    var pline = x as Polyline;
                    //    var vertNumber = pline.NumberOfVertices;
                    //    for (int i = 0; i < vertNumber; i++)
                    //    {
                    //        Point3d vertLocation = pline.GetPoint3dAt(i);
                    //        DBPoint point = new DBPoint(vertLocation);
                    //        point.AddEntityToDbModelSpace(localDb);
                    //        point.Layer = pointLayer;
                    //    }
                    //});
                    #endregion
                    #region Test clean 3d poly
                    //PromptEntityOptions peo = new PromptEntityOptions("\nSelect pline 3d: ");
                    //peo.SetRejectMessage("\nNot a Polyline3d!");
                    //peo.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //Polyline3d pline = per.ObjectId.Go<Polyline3d>(tx);
                    //List<int> verticesToRemove = new List<int>();
                    //PolylineVertex3d[] vertices = pline.GetVertices(tx);
                    //for (int i = 0; i < vertices.Length - 2; i++)
                    //{
                    //    PolylineVertex3d vertex1 = vertices[i];
                    //    PolylineVertex3d vertex2 = vertices[i + 1];
                    //    PolylineVertex3d vertex3 = vertices[i + 2];
                    //    Vector3d vec1 = vertex1.Position.GetVectorTo(vertex2.Position);
                    //    Vector3d vec2 = vertex2.Position.GetVectorTo(vertex3.Position);
                    //    if (vec1.IsCodirectionalTo(vec2, Tolerance.Global)) verticesToRemove.Add(i + 1);
                    //}
                    //Point3dCollection p3ds = new Point3dCollection();
                    //for (int i = 0; i < vertices.Length; i++)
                    //{
                    //    if (verticesToRemove.Contains(i)) continue;
                    //    PolylineVertex3d v = vertices[i];
                    //    p3ds.Add(v.Position);
                    //}
                    //Polyline3d nyPline = new Polyline3d(Poly3dType.SimplePoly, p3ds, false);
                    //nyPline.AddEntityToDbModelSpace(localDb);
                    //nyPline.Layer = pline.Layer;
                    //pline.CheckOrOpenForWrite();
                    //pline.Erase(true);
                    #endregion
                    #region Test redefine
                    //string blockName = "SH LIGE";
                    //string blockPath = @"X:\AutoCAD DRI - 01 Civil 3D\DynBlokke\Symboler.dwg";
                    //using (var blockDb = new Database(false, true))
                    //{
                    //    // Read the DWG into a side database
                    //    blockDb.ReadDwgFile(blockPath, FileOpenMode.OpenForReadAndAllShare, true, "");
                    //    Transaction blockTx = blockDb.TransactionManager.StartTransaction();
                    //    Oid sourceMsId = SymbolUtilityServices.GetBlockModelSpaceId(blockDb);
                    //    Oid destDbMsId = SymbolUtilityServices.GetBlockModelSpaceId(localDb);
                    //    BlockTable sourceBt = blockTx.GetObject(blockDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //    ObjectIdCollection idsToClone = new ObjectIdCollection();
                    //    idsToClone.Add(sourceBt[blockName]);
                    //    IdMapping mapping = new IdMapping();
                    //    blockDb.WblockCloneObjects(idsToClone, destDbMsId, mapping, DuplicateRecordCloning.Replace, false);
                    //    blockTx.Commit();
                    //    blockTx.Dispose();
                    //}
                    //var existingBlocks = localDb.HashSetOfType<BlockReference>(tx);
                    //foreach (var existingBlock in existingBlocks)
                    //{
                    //    if (existingBlock.RealName() == blockName)
                    //    {
                    //        existingBlock.ResetBlock();
                    //        var props = existingBlock.DynamicBlockReferencePropertyCollection;
                    //        foreach (DynamicBlockReferenceProperty prop in props)
                    //        {
                    //            if (prop.PropertyName == "Type") prop.Value = "200x40";
                    //        }
                    //        existingBlock.RecordGraphicsModified(true);
                    //    }
                    //}
                    #endregion
                    #region Test dynamic properties
                    //PromptEntityOptions peo = new PromptEntityOptions("Select a BR: ");
                    //PromptEntityResult per = editor.GetEntity(peo);
                    //BlockReference br = per.ObjectId.Go<BlockReference>(tx);
                    //DynamicBlockReferencePropertyCollection props = br.DynamicBlockReferencePropertyCollection;
                    //foreach (DynamicBlockReferenceProperty property in props)
                    //{
                    //    prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                    //    if (property.PropertyName == "Type")
                    //    {
                    //        property.Value = "Type 2";
                    //    }
                    //}
                    ////Construct pattern which matches the parameter definition
                    //Regex variablePattern = new Regex(@"{\$(?<Parameter>[a-zæøåA-ZÆØÅ0-9_:-]*)}");
                    //stringToTry = ConstructStringByRegex(stringToTry);
                    //prdDbg(stringToTry);
                    ////Test if a pattern matches in the input string
                    //string ConstructStringByRegex(string stringToProcess)
                    //{
                    //    if (variablePattern.IsMatch(stringToProcess))
                    //    {
                    //        //Get the first match
                    //        Match match = variablePattern.Match(stringToProcess);
                    //        //Get the first capture
                    //        string capture = match.Captures[0].Value;
                    //        //Get the parameter name from the regex match
                    //        string parameterName = match.Groups["Parameter"].Value;
                    //        //Read the parameter value from BR
                    //        string parameterValue = ReadDynamicPropertyValue(br, parameterName);
                    //        //Replace the captured group in original string with the parameter value
                    //        stringToProcess = stringToProcess.Replace(capture, parameterValue);
                    //        //Recursively call current function
                    //        //It runs on the string until no more captures remain
                    //        //Then it returns
                    //        stringToProcess = ConstructStringByRegex(stringToProcess);
                    //    }
                    //    return stringToProcess;
                    //}
                    //string ReadDynamicPropertyValue(BlockReference block, string propertyName)
                    //{
                    //    DynamicBlockReferencePropertyCollection props = block.DynamicBlockReferencePropertyCollection;
                    //    foreach (DynamicBlockReferenceProperty property in props)
                    //    {
                    //        //prdDbg($"Name: {property.PropertyName}, Units: {property.UnitsType}, Value: {property.Value}");
                    //        if (property.PropertyName == propertyName)
                    //        {
                    //            switch (property.UnitsType)
                    //            {
                    //                case DynamicBlockReferencePropertyUnitsType.NoUnits:
                    //                    return property.Value.ToString();
                    //                case DynamicBlockReferencePropertyUnitsType.Angular:
                    //                    double angular = Convert.ToDouble(property.Value);
                    //                    return angular.ToDegrees().ToString("0.##");
                    //                case DynamicBlockReferencePropertyUnitsType.Distance:
                    //                    double distance = Convert.ToDouble(property.Value);
                    //                    return distance.ToString("0.##");
                    //                case DynamicBlockReferencePropertyUnitsType.Area:
                    //                    double area = Convert.ToDouble(property.Value);
                    //                    return area.ToString("0.00");
                    //                default:
                    //                    return "";
                    //            }
                    //        }
                    //    }
                    //    return "";
                    //}
                    #endregion
                    #region QA pipe lengths
                    //System.Data.DataTable komponenter = CsvReader.ReadCsvToDataTable(
                    //        @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);
                    //prdDbg($"Block count: {brs.Count}");
                    //double totalLength = 0;
                    //int antal = 0;
                    //foreach (BlockReference br in brs)
                    //{
                    //    if (br.RealName() == "SVEJSEPUNKT" ||
                    //        ReadStringParameterFromDataTable(br.RealName(), komponenter, "Navn", 0) == null) continue;
                    //    DBObjectCollection objs = new DBObjectCollection();
                    //    br.Explode(objs);
                    //    if (br.RealName().Contains("RED KDLR"))
                    //    {
                    //        BlockReference br1 = null;
                    //        BlockReference br2 = null;
                    //        foreach (DBObject obj in objs)
                    //        {
                    //            if (obj is BlockReference muffe1 && br1 == null) br1 = muffe1;
                    //            else if (obj is BlockReference muffe2 && br1 != null) br2 = muffe2;
                    //        }
                    //        double dist = br1.Position.DistanceHorizontalTo(br2.Position);
                    //        totalLength += dist;
                    //        antal++;
                    //    }
                    //    else
                    //    {
                    //        foreach (DBObject obj in objs)
                    //        {
                    //            if (br.RealName() == "PA TWIN S3") prdDbg(obj.GetType().Name);
                    //            if (obj is Line line) totalLength += line.Length;
                    //        }
                    //        antal++;
                    //    }
                    //}
                    //prdDbg($"Samlet længde af {antal} komponenter: {totalLength}");
                    //HashSet<Profile> profiles = localDb.HashSetOfType<Profile>(tx);
                    //double totalProfLength = 0;
                    //foreach (Profile profile in profiles)
                    //{
                    //    if (profile.Name.Contains("MIDT"))
                    //        totalProfLength += profile.Length;
                    //}
                    //prdDbg($"Profiles: {totalProfLength.ToString("0.###")}");
                    //#region Read surface from file
                    //CivSurface surface = null;
                    //try
                    //{
                    //    surface = localDb
                    //        .HashSetOfType<TinSurface>(tx)
                    //        .FirstOrDefault() as CivSurface;
                    //}
                    //catch (System.Exception)
                    //{
                    //    throw;
                    //}
                    //if (surface == null)
                    //{
                    //    editor.WriteMessage("\nSurface could not be loaded!");
                    //    tx.Abort();
                    //    return;
                    //}
                    //#endregion
                    //HashSet<Polyline> plines = localDb.GetFjvPipes(tx).Where(x => GetPipeDN(x) != 999).ToHashSet();
                    //prdDbg(plines.Count.ToString());
                    //double totalPlineLength = 0;
                    //double totalFlLength = 0;
                    //foreach (Polyline pline in plines)
                    //{
                    //    totalPlineLength += pline.Length;
                    //    Oid flOid = FeatureLine.Create(pline.Handle.ToString(), pline.Id);
                    //    FeatureLine fl = flOid.Go<FeatureLine>(tx);
                    //    fl.AssignElevationsFromSurface(surface.Id, true);
                    //    totalFlLength += fl.Length3D;
                    //}
                    //prdDbg($"Pls: {totalPlineLength.ToString("0.###")}, Fls: {totalFlLength.ToString("0.###")}");
                    #endregion
                    #region Test buerør
                    ////PromptEntityOptions peo = new PromptEntityOptions("Select pline");
                    ////PromptEntityResult per = editor.GetEntity(peo);
                    ////Polyline pline = per.ObjectId.Go<Polyline>(tx);
                    //HashSet<Polyline> plines = localDb.HashSetOfType<Polyline>(tx);
                    //foreach (Polyline pline in plines)
                    //{
                    //    for (int j = 0; j < pline.NumberOfVertices - 1; j++)
                    //    {
                    //        //Guard against already cut out curves
                    //        if (j == 0 && pline.NumberOfVertices == 2) { break; }
                    //        double b = pline.GetBulgeAt(j);
                    //        Point2d fP = pline.GetPoint2dAt(j);
                    //        Point2d sP = pline.GetPoint2dAt(j + 1);
                    //        double u = fP.GetDistanceTo(sP);
                    //        double radius = u * ((1 + b.Pow(2)) / (4 * Math.Abs(b)));
                    //        double minRadius = GetPipeMinElasticRadius(pline);
                    //        //If radius is less than minRadius a buerør is detected
                    //        //Split the pline in segments delimiting buerør and append
                    //        if (radius < minRadius)
                    //        {
                    //            prdDbg($"Buerør detected {fP.ToString()} and {sP.ToString()}.");
                    //            prdDbg($"R: {radius}, minR: {minRadius}");
                    //            Line line = new Line(new Point3d(0, 0, 0), pline.GetPointAtDist(pline.Length / 2));
                    //            line.AddEntityToDbModelSpace(localDb);
                    //        }
                    //    }
                    //}
                    #endregion
                    #region Test location point of BRs
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx);
                    //BlockReference br = brs.Where(x => x.Handle.ToString() == "4E2A23").FirstOrDefault();
                    //prdDbg($"{br != default}");
                    //Database alsDB = new Database(false, true);
                    //alsDB.ReadDwgFile(@"X:\022-1226 Egedal - Krogholmvej, Etape 1 - Dokumenter\" +
                    //                  @"01 Intern\02 Tegninger\01 Autocad\Alignment\Alignment - Etape 1.dwg",
                    //    System.IO.FileShare.Read, false, string.Empty);
                    //using (Transaction alsTx = alsDB.TransactionManager.StartTransaction())
                    //{
                    //    HashSet<Alignment> als = alsDB.HashSetOfType<Alignment>(alsTx);
                    //    Alignment al = als.Where(x => x.Name == "05 Sigurdsvej").FirstOrDefault();
                    //    if (al != default)
                    //    {
                    //        Point3d brLoc = al.GetClosestPointTo(br.Position, false);
                    //        double station = 0;
                    //        double offset = 0;
                    //        al.StationOffset(brLoc.X, brLoc.Y, ref station, ref offset);
                    //        prdDbg($"S: {station}, O: {offset}");
                    //    }
                    //    alsTx.Abort();
                    //}
                    //alsDB.Dispose();
                    #endregion
                    #region Test exploding alignment
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select an alignment: ");
                    //promptEntityOptions1.SetRejectMessage("\n Not an alignment!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Alignment), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId alId = entity1.ObjectId;
                    //Alignment al = alId.Go<Alignment>(tx);
                    ////DBObjectCollection objs = new DBObjectCollection();
                    //////First explode
                    ////al.Explode(objs);
                    //////Explodes to 1 block
                    ////Entity firstExplode = (Entity)objs[0];
                    ////Second explode
                    ////objs = new DBObjectCollection();
                    ////firstExplode.Explode(objs);
                    ////prdDbg($"Subsequent block exploded to number of items: {objs.Count}.");
                    //List<Oid> explodedObjects = new List<Oid>();
                    //ObjectEventHandler handler = (s, e) =>
                    //{
                    //    explodedObjects.Add(e.DBObject.ObjectId);
                    //};
                    //localDb.ObjectAppended += handler;
                    //editor.Command("_explode", al.ObjectId);
                    //localDb.ObjectAppended -= handler;
                    //prdDbg(explodedObjects.Count.ToString());
                    ////Assume block reference is the only item
                    //Oid bId = explodedObjects.First();
                    //explodedObjects.Clear();
                    //localDb.ObjectAppended += handler;
                    //editor.Command("_explode", bId);
                    //localDb.ObjectAppended -= handler;
                    //prdDbg(explodedObjects.Count.ToString());
                    #endregion
                    #region Test size arrays
                    //Alignment al;
                    //#region Select alignment
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions("\n Select an alignment: ");
                    //promptEntityOptions1.SetRejectMessage("\n Not an alignment!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Alignment), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId profileId = entity1.ObjectId;
                    //al = profileId.Go<Alignment>(tx);
                    //#endregion
                    //#region Open fremtidig db
                    //DataReferencesOptions dro = new DataReferencesOptions();
                    //string projectName = dro.ProjectName;
                    //string etapeName = dro.EtapeName;
                    //#region Read CSV
                    //System.Data.DataTable dynBlocks = default;
                    //try
                    //{
                    //    dynBlocks = CsvReader.ReadCsvToDataTable(
                    //            @"X:\AutoCAD DRI - 01 Civil 3D\FJV Dynamiske Komponenter.csv", "FjvKomponenter");
                    //}
                    //catch (System.Exception ex)
                    //{
                    //    prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                    //    prdDbg(ex);
                    //    throw;
                    //}
                    //if (dynBlocks == default)
                    //{
                    //    prdDbg("Reading of FJV Dynamiske Komponenter.csv failed!");
                    //    throw new System.Exception("Failed to read FJV Dynamiske Komponenter.csv");
                    //}
                    //#endregion
                    //// open the xref database
                    //Database fremDb = new Database(false, true);
                    //fremDb.ReadDwgFile(GetPathToDataFiles(projectName, etapeName, "Fremtid"),
                    //    System.IO.FileShare.Read, false, string.Empty);
                    //Transaction fremTx = fremDb.TransactionManager.StartTransaction();
                    //var ents = fremDb.GetFjvEntities(fremTx, dynBlocks);
                    //var allCurves = ents.Where(x => x is Curve).ToHashSet();
                    //var allBrs = ents.Where(x => x is BlockReference).ToHashSet();
                    //PropertySetManager psmPipeLineData = new PropertySetManager(
                    //    fremDb,
                    //    PSetDefs.DefinedSets.DriPipelineData);
                    //PSetDefs.DriPipelineData driPipelineData =
                    //    new PSetDefs.DriPipelineData();
                    //#endregion
                    //try
                    //{
                    //    #region GetCurvesAndBRs from fremtidig
                    //    HashSet<Curve> curves = allCurves.Cast<Curve>()
                    //        .Where(x => psmPipeLineData
                    //        .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                    //        .ToHashSet();
                    //    HashSet<BlockReference> brs = allBrs.Cast<BlockReference>()
                    //        .Where(x => psmPipeLineData
                    //        .FilterPropetyString(x, driPipelineData.BelongsToAlignment, al.Name))
                    //        .ToHashSet();
                    //    prdDbg($"Curves: {curves.Count}, Components: {brs.Count}");
                    //    #endregion
                    //    PipelineSizeArray sizeArray = new PipelineSizeArray(al, curves, brs);
                    //    prdDbg(sizeArray.ToString());
                    //}
                    //catch (System.Exception ex)
                    //{
                    //    fremTx.Abort();
                    //    fremTx.Dispose();
                    //    fremDb.Dispose();
                    //    prdDbg(ex);
                    //    throw;
                    //}
                    //fremTx.Abort();
                    //fremTx.Dispose();
                    //fremDb.Dispose();
                    #endregion
                    #region RXClass to String test
                    //prdDbg("Line: " + RXClass.GetClass(typeof(Line)).Name);
                    //prdDbg("Spline: " + RXClass.GetClass(typeof(Spline)).Name);
                    //prdDbg("Polyline: " + RXClass.GetClass(typeof(Polyline)).Name);
                    #endregion
                    #region Paperspace to modelspace test
                    ////BlockTable blockTable = tx.GetObject(localDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    ////BlockTableRecord paperSpace = tx.GetObject(blockTable[BlockTableRecord.PaperSpace], OpenMode.ForRead)
                    ////    as BlockTableRecord;
                    //DBDictionary layoutDict = localDb.LayoutDictionaryId.Go<DBDictionary>(tx);
                    //var enumerator = layoutDict.GetEnumerator();
                    //while (enumerator.MoveNext())
                    //{
                    //    DBDictionaryEntry item = enumerator.Current;
                    //    prdDbg(item.Key);
                    //    if (item.Key == "Model")
                    //    {
                    //        prdDbg("Skipping model...");
                    //        continue;
                    //    }
                    //    Layout layout = item.Value.Go<Layout>(tx);
                    //    //ObjectIdCollection vpIds = layout.GetViewports();
                    //    BlockTableRecord layBlock = layout.BlockTableRecordId.Go<BlockTableRecord>(tx);
                    //    foreach (Oid id in layBlock)
                    //    {
                    //        if (id.IsDerivedFrom<Viewport>())
                    //        {
                    //            Viewport viewport = id.Go<Viewport>(tx);
                    //            //Truncate doubles to whole numebers for easier comparison
                    //            int centerX = (int)viewport.CenterPoint.X;
                    //            int centerY = (int)viewport.CenterPoint.Y;
                    //            if (centerX == 422 && centerY == 442)
                    //            {
                    //                prdDbg("Found profile viewport!");
                    //                Extents3d ext = viewport.GeometricExtents;
                    //                Polyline pl = new Polyline(4);
                    //                pl.AddVertexAt(0, new Point2d(ext.MinPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(1, new Point2d(ext.MinPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(2, new Point2d(ext.MaxPoint.X, ext.MaxPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.AddVertexAt(3, new Point2d(ext.MaxPoint.X, ext.MinPoint.Y), 0.0, 0.0, 0.0);
                    //                pl.Closed = true;
                    //                pl.SetDatabaseDefaults();
                    //                pl.PaperToModel(viewport);
                    //                pl.Layer = "0-NONPLOT";
                    //                pl.AddEntityToDbModelSpace<Polyline>(localDb);
                    //            }
                    //        }
                    //    }
                    //}
                    #endregion
                    #region ProfileProjectionLabel testing
                    //List<ProfileProjectionLabel> labels = new();

                    // Get the block table for the current database
                    //var blockTable = (BlockTable)tx.GetObject(localDb.BlockTableId, OpenMode.ForRead);

                    // Get the model space block table record
                    //var modelSpace = (BlockTableRecord)
                    //    tx.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                    //RXClass theClass = RXObject.GetClass(typeof(ProfileProjectionLabel));

                    // Loop through the entities in model space
                    //foreach (Oid oid in modelSpace)
                    //{
                    //     Look for entities of the correct type
                    //    if (oid.ObjectClass.IsDerivedFrom(theClass))
                    //    {
                    //        var entity = (ProfileProjectionLabel)tx.GetObject(oid, OpenMode.ForRead);
                    //        labels.Add(entity);
                    //    }
                    //}

                    //foreach (var label in labels)
                    //{

                    //    try
                    //    {
                    //        double x = label.LabelLocation.X; //<-- throws here
                    //    }
                    //    catch (System.Exception ex)
                    //    {

                    //        label.UpgradeOpen();
                    //        label.Erase(true);
                    //    }
                    //}
                    #endregion
                    #region PropertySets testing 1
                    ////IntersectUtilities.ODDataConverter.ODDataConverter.testing();
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect entity to list rxobject:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a p3d!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                    //prdDbg(CogoPoint.GetClass(typeof(CogoPoint)).Name);
                    #endregion
                    #region Print all values of all ODTable's fields
                    ////PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    ////    "\nSelect entity to list OdTable:");
                    ////promptEntityOptions1.SetRejectMessage("\n Not a p3d!");
                    ////promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    ////PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    ////if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    ////Autodesk.AutoCAD.DatabaseServices.ObjectId entId = entity1.ObjectId;
                    //HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx, true)
                    //    .Where(x => x.Layer == "AFL_ledning_faelles").ToHashSet();
                    //foreach (Polyline3d item in p3ds)
                    //{
                    //    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //    using (Records records
                    //           = tables.GetObjectRecords(0, item.Id, Autodesk.Gis.Map.Constants.OpenMode.OpenForRead, false))
                    //    {
                    //        int count = records.Count;
                    //        prdDbg($"Tables total: {count.ToString()}");
                    //        for (int i = 0; i < count; i++)
                    //        {
                    //            Record record = records[i];
                    //            int recordCount = record.Count;
                    //            prdDbg($"Table {record.TableName} has {recordCount} fields.");
                    //            for (int j = 0; j < recordCount; j++)
                    //            {
                    //                MapValue value = record[j];
                    //                prdDbg($"R:{i + 1};V:{j + 1} : {value.StrValue}");
                    //            }
                    //        }
                    //    } 
                    //}
                    #endregion
                    #region Test removing colinear vertices
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect polyline to list parameters:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;
                    //Polyline pline = plineId.Go<Polyline>(tx);
                    //RemoveColinearVerticesPolyline(pline);
                    #endregion
                    #region Test polyline parameter and vertices
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //    "\nSelect polyline to list parameters:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId plineId = entity1.ObjectId;
                    //Polyline pline = plineId.Go<Polyline>(tx);
                    //for (int i = 0; i < pline.NumberOfVertices; i++)
                    //{
                    //    Point3d p3d = pline.GetPoint3dAt(i);
                    //    prdDbg($"Vertex: {i}, Parameter: {pline.GetParameterAtPoint(p3d)}");
                    //}
                    #endregion
                    #region List all gas stik materialer
                    //Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //HashSet<Polyline3d> p3ds = localDb.HashSetOfType<Polyline3d>(tx)
                    //                                  .Where(x => x.Layer == "GAS-Stikrør" ||
                    //                                              x.Layer == "GAS-Stikrør-2D")
                    //                                  .ToHashSet();
                    //HashSet<string> materials = new HashSet<string>();
                    //foreach (Polyline3d p3d in p3ds)
                    //{
                    //    materials.Add(ReadPropertyToStringValue(tables, p3d.Id, "GasDimOgMat", "Material"));
                    //}
                    //var ordered = materials.OrderBy(x => x);
                    //foreach (string s in ordered) prdDbg(s);
                    #endregion
                    #region ODTables troubles
                    //try
                    //{
                    //    Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //    StringCollection names = tables.GetTableNames();
                    //    foreach (string name in names)
                    //    {
                    //        prdDbg(name);
                    //        Autodesk.Gis.Map.ObjectData.Table table = null;
                    //        try
                    //        {
                    //            table = tables[name];
                    //            FieldDefinitions defs = table.FieldDefinitions;
                    //            for (int i = 0; i < defs.Count; i++)
                    //            {
                    //                if (defs[i].Name.Contains("DIA") ||
                    //                    defs[i].Name.Contains("Dia") ||
                    //                    defs[i].Name.Contains("dia")) prdDbg(defs[i].Name);
                    //            }
                    //        }
                    //        catch (Autodesk.Gis.Map.MapException e)
                    //        {
                    //            var errCode = (Autodesk.Gis.Map.Constants.ErrorCode)(e.ErrorCode);
                    //            prdDbg(errCode.ToString());
                    //            MapApplication app = HostMapApplicationServices.Application;
                    //            FieldDefinitions tabDefs = app.ActiveProject.MapUtility.NewODFieldDefinitions();
                    //            tabDefs.AddColumn(
                    //                FieldDefinition.Create("Diameter", "Diameter of crossing pipe", DataType.Character), 0);
                    //            tabDefs.AddColumn(
                    //                FieldDefinition.Create("Alignment", "Alignment name", DataType.Character), 1);
                    //            tables.RemoveTable("CrossingData");
                    //            tables.Add("CrossingData", tabDefs, "Table holding relevant crossing data", true);
                    //            //tables.UpdateTable("CrossingData", tabDefs);
                    //        }
                    //    }
                    //}
                    //catch (Autodesk.Gis.Map.MapException e)
                    //{
                    //    var errCode = (Autodesk.Gis.Map.Constants.ErrorCode)(e.ErrorCode);
                    //    prdDbg(errCode.ToString());
                    //}
                    #endregion
                    #region ChangeLayerOfXref
                    //string path = @"X:\0371-1158 - Gentofte Fase 4 - Dokumenter\01 Intern\02 Tegninger\01 Autocad\Autocad\02 Sheets\5.5\";
                    //var fileList = File.ReadAllLines(path + "fileList.txt").ToList();
                    //foreach (string name in fileList)
                    //{
                    //    prdDbg(name);
                    //}
                    //foreach (string name in fileList)
                    //{
                    //    prdDbg(name);
                    //    string fileName = path + name;
                    //    prdDbg(fileName);
                    //    using (Database extDb = new Database(false, true))
                    //    {
                    //        extDb.ReadDwgFile(fileName, System.IO.FileShare.ReadWrite, false, "");
                    //        using (Transaction extTx = extDb.TransactionManager.StartTransaction())
                    //        {
                    //            BlockTable bt = extTx.GetObject(extDb.BlockTableId, OpenMode.ForRead) as BlockTable;
                    //            foreach (oid oid in bt)
                    //            {
                    //                BlockTableRecord btr = extTx.GetObject(oid, OpenMode.ForWrite) as BlockTableRecord;
                    //                if (btr.Name.Contains("_alignment"))
                    //                {
                    //                    var ids = btr.GetBlockReferenceIds(true, true);
                    //                    foreach (oid brId in ids)
                    //                    {
                    //                        BlockReference br = brId.Go<BlockReference>(extTx, OpenMode.ForWrite);
                    //                        prdDbg(br.Name);
                    //                        if (br.Layer == "0") { prdDbg("Already in 0! Skipping..."); continue; }
                    //                        prdDbg("Was in: :" + br.Layer);
                    //                        br.Layer = "0";
                    //                        prdDbg("Moved to: " + br.Layer);
                    //                        System.Windows.Forms.Application.DoEvents();
                    //                    }
                    //                }
                    //            }
                    //            extTx.Commit();
                    //        }
                    //        extDb.SaveAs(extDb.Filename, DwgVersion.Current);
                    //    }
                    //}
                    #endregion
                    #region List blocks scale
                    //HashSet<BlockReference> brs = localDb.HashSetOfType<BlockReference>(tx, true);
                    //foreach (BlockReference br in brs)
                    //{
                    //    prdDbg(br.ScaleFactors.ToString());
                    //}
                    #endregion
                    #region Gather alignment names
                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als.OrderBy(x => x.Name))
                    //{
                    //    editor.WriteMessage($"\n{al.Name}");
                    //}
                    #endregion
                    #region Test ODTables from external database
                    //Tables odTables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //StringCollection curDbTables = new StringCollection();
                    //Database curDb = HostApplicationServices.WorkingDatabase;
                    //StringCollection allDbTables = odTables.GetTableNames();
                    //Autodesk.Gis.Map.Project.AttachedDrawings attachedDwgs =
                    //    HostMapApplicationServices.Application.ActiveProject.DrawingSet.AllAttachedDrawings;
                    //int directDWGCount = HostMapApplicationServices.Application.ActiveProject.DrawingSet.DirectDrawingsCount;
                    //foreach (String name in allDbTables)
                    //{
                    //    Autodesk.Gis.Map.ObjectData.Table table = odTables[name];
                    //    bool bTableExistsInCurDb = true;
                    //    for (int i = 0; i < directDWGCount; ++i)
                    //    {
                    //        Autodesk.Gis.Map.Project.AttachedDrawing attDwg = attachedDwgs[i];
                    //        StringCollection attachedTables = attDwg.GetTableList(Autodesk.Gis.Map.Constants.TableType.ObjectDataTable);
                    //    }
                    //    if (bTableExistsInCurDb)
                    //        curDbTables.Add(name);
                    //}
                    //editor.WriteMessage("Current Drawing Object Data Tables Names :\r\n");
                    //foreach (String name in curDbTables)
                    //{
                    //    editor.WriteMessage(name + "\r\n");
                    //}
                    #endregion
                    #region Test description field population
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //"\nSelect test subject:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a polyline3d!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId lineId = entity1.ObjectId;
                    //Entity ent = lineId.Go<Entity>(tx);
                    //Tables tables = HostMapApplicationServices.Application.ActiveProject.ODTables;
                    //#region Read Csv Data for Layers and Depth
                    ////Establish the pathnames to files
                    ////Files should be placed in a specific folder on desktop
                    //string pathKrydsninger = "X:\\AutoCAD DRI - 01 Civil 3D\\Krydsninger.csv";
                    //string pathDybde = "X:\\AutoCAD DRI - 01 Civil 3D\\Dybde.csv";
                    //System.Data.DataTable dtKrydsninger = CsvReader.ReadCsvToDataTable(pathKrydsninger, "Krydsninger");
                    //System.Data.DataTable dtDybde = CsvReader.ReadCsvToDataTable(pathDybde, "Dybde");
                    //#endregion
                    ////Populate description field
                    ////1. Read size record if it exists
                    //MapValue sizeRecord = Utils.ReadRecordData(
                    //    tables, lineId, "SizeTable", "Size");
                    //int SizeTableSize = 0;
                    //string sizeDescrPart = "";
                    //if (sizeRecord != null)
                    //{
                    //    SizeTableSize = sizeRecord.Int32Value;
                    //    sizeDescrPart = $"ø{SizeTableSize}";
                    //}
                    ////2. Read description from Krydsninger
                    //string descrFromKrydsninger = ReadStringParameterFromDataTable(
                    //    ent.Layer, dtKrydsninger, "Description", 0);
                    ////2.1 Read the formatting in the description field
                    //List<(string ToReplace, string Data)> descrFormatList = null;
                    //if (descrFromKrydsninger.IsNotNoE())
                    //    descrFormatList = FindDescriptionParts(descrFromKrydsninger);
                    ////Finally: Compose description field
                    //List<string> descrParts = new List<string>();
                    ////1. Add custom size
                    //if (SizeTableSize != 0) descrParts.Add(sizeDescrPart);
                    ////2. Process and add parts from format bits in OD
                    //if (descrFromKrydsninger.IsNotNoE())
                    //{
                    //    //Interpolate description from Krydsninger with format setting, if they exist
                    //    if (descrFormatList != null && descrFormatList.Count > 0)
                    //    {
                    //        for (int i = 0; i < descrFormatList.Count; i++)
                    //        {
                    //            var tuple = descrFormatList[i];
                    //            string result = ReadDescriptionPartsFromOD(tables, ent, tuple.Data, dtKrydsninger);
                    //            descrFromKrydsninger = descrFromKrydsninger.Replace(tuple.ToReplace, result);
                    //        }
                    //    }
                    //    //Add the description field to parts
                    //    descrParts.Add(descrFromKrydsninger);
                    //}
                    //string description = "";
                    //if (descrParts.Count == 1) description = descrParts[0];
                    //else if (descrParts.Count > 1)
                    //    description = string.Join("; ", descrParts);
                    //editor.WriteMessage($"\n{description}");
                    #endregion
                    #region GetDistance
                    //PromptEntityOptions promptEntityOptions1 = new PromptEntityOptions(
                    //"\nSelect line:");
                    //promptEntityOptions1.SetRejectMessage("\n Not a line!");
                    //promptEntityOptions1.AddAllowedClass(typeof(Line), true);
                    //PromptEntityResult entity1 = editor.GetEntity(promptEntityOptions1);
                    //if (((PromptResult)entity1).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId lineId = entity1.ObjectId;
                    //PromptEntityOptions promptEntityOptions2 = new PromptEntityOptions(
                    //"\nSelect p3dpoly:");
                    //promptEntityOptions2.SetRejectMessage("\n Not a p3dpoly!");
                    //promptEntityOptions2.AddAllowedClass(typeof(Polyline3d), true);
                    //PromptEntityResult entity2 = editor.GetEntity(promptEntityOptions2);
                    //if (((PromptResult)entity2).Status != PromptStatus.OK) return;
                    //Autodesk.AutoCAD.DatabaseServices.ObjectId poly3dId = entity2.ObjectId;
                    //Line line = lineId.Go<Line>(tx);
                    //Polyline3d p3d = poly3dId.Go<Polyline3d>(tx);
                    //double distance = line.GetGeCurve().GetDistanceTo(
                    //    p3d.GetGeCurve());
                    //editor.WriteMessage($"\nDistance: {distance}.");
                    //editor.WriteMessage($"\nIs less than 0.1: {distance < 0.1}.");
                    //if (distance < 0.1)
                    //{
                    //    PointOnCurve3d[] intPoints = line.GetGeCurve().GetClosestPointTo(
                    //                                 p3d.GetGeCurve());
                    //    //Assume one intersection
                    //    Point3d result = intPoints.First().Point;
                    //    editor.WriteMessage($"\nDetected elevation: {result.Z}.");
                    //}
                    #endregion
                    #region CleanMtexts
                    //HashSet<MText> mtexts = localDb.HashSetOfType<MText>(tx);
                    //foreach (MText mText in mtexts)
                    //{
                    //    string contents = mText.Contents;
                    //    contents = contents.Replace(@"\H3.17507;", "");
                    //    mText.CheckOrOpenForWrite();
                    //    mText.Contents = contents;
                    //} 
                    #endregion
                    #region Test PV start and end station
                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als)
                    //{
                    //    ObjectIdCollection pIds = al.GetProfileIds();
                    //    Profile p = null;
                    //    foreach (oid oid in pIds)
                    //    {
                    //        Profile pt = oid.Go<Profile>(tx);
                    //        if (pt.Name == $"{al.Name}_surface_P") p = pt;
                    //    }
                    //    if (p == null) return;
                    //    else editor.WriteMessage($"\nProfile {p.Name} found!");
                    //    ProfileView[] pvs = localDb.ListOfType<ProfileView>(tx).ToArray();
                    //    foreach (ProfileView pv in pvs)
                    //    {
                    //        editor.WriteMessage($"\nName of pv: {pv.Name}.");
                    #region Test finding of max elevation
                    //double pvStStart = pv.StationStart;
                    //double pvStEnd = pv.StationEnd;
                    //int nrOfIntervals = 100;
                    //double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                    //HashSet<double> elevs = new HashSet<double>();
                    //for (int i = 0; i < nrOfIntervals + 1; i++)
                    //{
                    //    double testEl = p.ElevationAt(pvStStart + delta * i);
                    //    elevs.Add(testEl);
                    //    editor.WriteMessage($"\nElevation at {i} is {testEl}.");
                    //}
                    //double maxEl = elevs.Max();
                    //editor.WriteMessage($"\nMax elevation of {pv.Name} is {maxEl}.");
                    //pv.CheckOrOpenForWrite();
                    //pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    //pv.ElevationMax = Math.Ceiling(maxEl); 
                    #endregion
                    //}
                    //}
                    #endregion
                    #region Test station and offset alignment
                    //#region Select point
                    //PromptPointOptions pPtOpts = new PromptPointOptions("");
                    //// Prompt for the start point
                    //pPtOpts.Message = "\nEnter location to test the alignment:";
                    //PromptPointResult pPtRes = editor.GetPoint(pPtOpts);
                    //Point3d selectedPoint = pPtRes.Value;
                    //// Exit if the user presses ESC or cancels the command
                    //if (pPtRes.Status != PromptStatus.OK) return;
                    //#endregion
                    //HashSet<Alignment> als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als)
                    //{
                    //    double station = 0;
                    //    double offset = 0;
                    //    al.StationOffset(selectedPoint.X, selectedPoint.Y, ref station, ref offset);
                    //    editor.WriteMessage($"\nReported: ST: {station}, OS: {offset}.");
                    //} 
                    #endregion
                    #region Test assigning labels and point styles
                    //oid cogoPointStyle = civilDoc.Styles.PointStyles["LER KRYDS"];
                    //CogoPointCollection cpc = civilDoc.CogoPoints;
                    //foreach (oid cpOid in cpc)
                    //{
                    //    CogoPoint cp = cpOid.Go<CogoPoint>(tx, OpenMode.ForWrite);
                    //    cp.StyleId = cogoPointStyle;
                    //}
                    #endregion
                    #region Profile style and PV elevation
                    //CivilDocument cDoc = CivilDocument.GetCivilDocument(localDb);
                    //var als = localDb.HashSetOfType<Alignment>(tx);
                    //foreach (Alignment al in als)
                    //{
                    //    var pIds = al.GetProfileIds();
                    //    var pvIds = al.GetProfileViewIds();
                    //    Profile pSurface = null;
                    //    foreach (Oid oid in pIds)
                    //    {
                    //        Profile pt = oid.Go<Profile>(tx);
                    //        if (pt.Name == $"{al.Name}_surface_P") pSurface = pt;
                    //    }
                    //    if (pSurface == null)
                    //    {
                    //        //AbortGracefully(
                    //        //    new[] { xRefLerTx, xRefSurfaceTx },
                    //        //    new[] { xRefLerDB, xRefSurfaceDB },
                    //        //    $"No profile named {alignment.Name}_surface_P found!");
                    //        prdDbg($"No surface profile {al.Name}_surface_P found!");
                    //        tx.Abort();
                    //        return;
                    //    }
                    //    else prdDbg($"\nProfile {pSurface.Name} found!");
                    //    foreach (ProfileView pv in pvIds.Entities<ProfileView>(tx))
                    //    {
                    //        #region Determine profile top and bottom elevations
                    //        double pvStStart = pv.StationStart;
                    //        double pvStEnd = pv.StationEnd;
                    //        int nrOfIntervals = (int)((pvStEnd - pvStStart) / 0.25);
                    //        double delta = (pvStEnd - pvStStart) / nrOfIntervals;
                    //        HashSet<double> topElevs = new HashSet<double>();
                    //        for (int j = 0; j < nrOfIntervals + 1; j++)
                    //        {
                    //            double topTestEl = 0;
                    //            try
                    //            {
                    //                topTestEl = pSurface.ElevationAt(pvStStart + delta * j);
                    //            }
                    //            catch (System.Exception)
                    //            {
                    //                editor.WriteMessage($"\nTop profile at {pvStStart + delta * j} threw an exception! " +
                    //                    $"PV: {pv.StationStart}-{pv.StationEnd}.");
                    //                continue;
                    //            }
                    //            topElevs.Add(topTestEl);
                    //        }
                    //        double maxEl = topElevs.Max();
                    //        double minEl = topElevs.Min();
                    //        prdDbg($"\nElevations of PV {pv.Name}> Max: {Math.Round(maxEl, 2)} | Min: {Math.Round(minEl, 2)}");
                    //        //Set the elevations
                    //        pv.CheckOrOpenForWrite();
                    //        pv.ElevationRangeMode = ElevationRangeType.UserSpecified;
                    //        pv.ElevationMax = Math.Ceiling(maxEl);
                    //        pv.ElevationMin = Math.Floor(minEl) - 3.0;
                    //        #endregion
                    //        Oid sId = cDoc.Styles.ProfileViewStyles["PROFILE VIEW L TO R 1:250:100"];
                    //        pv.CheckOrOpenForWrite();
                    //        pv.StyleId = sId;
                    //    }
                    //    //Set profile style
                    //    localDb.CheckOrCreateLayer("0_TERRAIN_PROFILE", 34);
                    //    Oid profileStyleId = cDoc.Styles.ProfileStyles["Terræn"];
                    //    pSurface.CheckOrOpenForWrite();
                    //    pSurface.StyleId = profileStyleId;
                    //}
                    #endregion
                }
                catch (System.Exception ex)
                {
                    tx.Abort();
                    prdDbg(ex);
                    return;
                }
                tx.Commit();
            }
        }
#endif
    }
}
