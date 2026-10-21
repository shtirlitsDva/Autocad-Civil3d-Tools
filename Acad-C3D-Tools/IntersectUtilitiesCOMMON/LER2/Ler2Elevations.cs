#if BRICSCAD
using Bricscad.ApplicationServices;
using Bricscad.EditorInput;
using Teigha.DatabaseServices;
using Teigha.Geometry;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
#endif

using IntersectUtilities.UtilsCommon;

using System.Collections.Generic;
using System.Linq;

using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.LER2
{
    /// <summary>
    /// The bodies of LER2DCI, LER2IBI and LER2ASTIK, which give the -99 vertices of LER
    /// 3D polylines an elevation, and of FLATTENPL3D and FLATTENVERTEX, which put them
    /// back at -99; shared by every head that registers them
    /// (IntersectUtilities, NorsynDrawingToolsManaged). No command attribute here: this
    /// file compiles into every project that imports IntersectUtilitiesCOMMON.
    /// </summary>
    public static class Ler2Elevations
    {
        /// <summary>LER2ASTIK's slope in promille, kept between runs.</summary>
        private static double slope = 0;

        /// <summary>
        /// Identifies and corrects vertices with placeholder elevation (-99) on 3D polylines by matching them with valid adjacent vertices.
        /// </summary>
        public static void DetectCoincidentVertici(Database localDb)
        {
            //Process all lines and detect ends, that are coincident with another vertex
            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    //The idea is to get all vertices which are located at -99.0
                    //and then check if there is another vertex at the same location
                    //but with a different elevation
                    //This is specifically developed for afløb
                    //where the stik have the elevation at their one point,
                    //but corresponding vertici of the main pipes are at -99.0

                    #region Load linework from local db
                    List<Polyline3d> localPlines3d =
                        localDb.ListOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of non-frozen 3D polies: {localPlines3d.Count}");

                    var allEndpointsAt99 = new HashSet<(Point3d loc, Polyline3d host, int idx)>();
                    var allVerticiAtElevation = new HashSet<(Point3d loc, Polyline3d host, int idx)>();

                    foreach (Polyline3d pline3d in localPlines3d)
                    {
                        var vertices = pline3d.GetVertices(tx);
                        int endIdx = vertices.Length - 1;

                        double startElevation = vertices[0].Position.Z;
                        double endElevation = vertices[endIdx].Position.Z;

                        if (startElevation.is2D()) allEndpointsAt99.Add((vertices[0].Position, pline3d, 0));
                        if (endElevation.is2D()) allEndpointsAt99.Add((vertices[endIdx].Position, pline3d, endIdx));

                        //Iterate through all vertices and detect those at -99.0
                        //skipping first and last because I don't want to catch pipes
                        //That are continuos with the pipe

                        for (int i = 1; i < endIdx; i++)
                        {
                            if (!vertices[i].Position.Z.is2D())
                            {
                                allVerticiAtElevation.Add((vertices[i].Position, pline3d, i));
                            }
                        }
                    }

                    //Analyze points
                    foreach (var verticeAt99 in allEndpointsAt99)
                    {
                        //Detect the coincident 3d location
                        var atElevation = allVerticiAtElevation.Where(
                            x => verticeAt99.loc.HorizontalEqualz(x.loc, 0.001)).FirstOrDefault();

                        if (atElevation == default) continue;

                        var vert = verticeAt99.host.GetVertices(tx)[verticeAt99.idx];
                        vert.CheckOrOpenForWrite();
                        vert.Position = atElevation.loc;
                    }
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

        /// <summary>
        /// Interpolates vertex elevations on 3D polylines by detecting segments with placeholder (-99) values and linearly interpolating between the surrounding valid vertices.
        /// </summary>
        public static void InterpolateBetweenIslands(Database localDb)
        {
            //The idea is to interpolate between islands
            //These islands are being created by the coincident vertices detection algorithm
            //Some of the vertici are still at -99.0, while their neighbors are at elevation
            //This algorithm will interpolate between these islands
            //To get rid of the -99.0 vertices

            using (Transaction tx = localDb.TransactionManager.StartTransaction())
            {
                try
                {
                    List<Polyline3d> localPlines3d =
                        localDb.ListOfType<Polyline3d>(tx, true);
                    prdDbg($"\nNr. of non-frozen 3D polies: {localPlines3d.Count}");

                    foreach (Polyline3d pl3d in localPlines3d)
                    {
                        #region Process vertices
                        PolylineVertex3d[] vertices = pl3d.GetVertices(tx);
                        //Determine if the polyline has islands
                        //This is determined by the fact that some vertices are at -99.0
                        //and some are at elevation
                        //Also, require start and end to be at elevation
                        if (vertices.Any(x => x.Position.Z.is2D()) &&
                            vertices.Any(x => x.Position.Z.is3D()) &&
                            vertices[0].Position.Z.is3D() &&
                            vertices.Last().Position.Z.is3D())
                        {
                            //Iterate vertici and detect islands
                            //The detection goes as follows:
                            //Check current vertex elevation
                            //If it is at elevation, go to the next
                            //If it is at -99.0, flag the previous as start of grave
                            //Then iterate until the next vertex is at elevation
                            //When the next vertex at elevation is detected, interpolate between the start and end
                            //of the grave.
                            //Keep track of where the iteration is
                            //And continue after the grave to trying to detect another grave
                            //Until the end of the polyline is reached

                            for (int i = 1; i < vertices.Length - 1; i++)
                            {
                                if (vertices[i].Position.Z.is3D()) continue;

                                //Start of grave
                                int graveStart = i - 1;
                                //Iterate until the next vertex is at elevation
                                for (int j = i + 1; j < vertices.Length; j++)
                                {
                                    if (vertices[j].Position.Z.is3D())
                                    {
                                        //End of grave
                                        int graveEnd = j;
                                        //Interpolation
                                        double startElevation = vertices[graveStart].Position.Z;
                                        double endElevation = vertices[graveEnd].Position.Z;
                                        double AB = pl3d.GetHorizontalLengthBetweenIdxs(graveStart, graveEnd);
                                        prdDbg(AB.ToString());
                                        double AAmark = startElevation - endElevation;
                                        double PB = 0;
                                        for (int k = graveStart; k < graveEnd + 1; k++)
                                        {
                                            //Skip first and last vertici
                                            if (k == graveStart || k == graveEnd) continue;

                                            PB += vertices[k - 1].Position.DistanceHorizontalTo(vertices[k].Position);

                                            double newElevation = startElevation - PB * (AAmark / AB);
                                            pl3d.CheckOrOpenForWrite();
                                            vertices[k].CheckOrOpenForWrite();
                                            vertices[k].Position = new Point3d(
                                                vertices[k].Position.X, vertices[k].Position.Y, newElevation);
                                        }
                                        //Continue after the grave
                                        i = j;
                                        break;
                                    }
                                }
                            }
                        }
                    }
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

        /// <summary>
        /// Asks for a 3D polyline, then for its vertices one at a time by their points, and
        /// sets each picked vertex's elevation to -99, keeping X and Y. Each vertex is its own
        /// undoable change; Enter or Esc ends the loop.
        /// </summary>
        /// <param name="doc">The drawing the drafter works in.</param>
        public static void FlattenVertices(Document doc)
        {
            const double tol = 0.001;
            Database localDb = doc.Database;
            Editor ed = doc.Editor;

            var opt = new PromptEntityOptions("\nSelect the 3D polyline: ");
            opt.SetRejectMessage("\nSelect a 3D polyline!");
            opt.AddAllowedClass(typeof(Polyline3d), true);
            PromptEntityResult chosen = ed.GetEntity(opt);
            if (chosen.Status != PromptStatus.OK) return;

            var ppo = new PromptPointOptions("\nPick a vertex to flatten to -99 [Enter to finish]: ");
            ppo.AllowNone = true;
            while (true)
            {
                PromptPointResult picked = ed.GetPoint(ppo);
                if (picked.Status != PromptStatus.OK) return;
                Point3d at = picked.Value.TransformBy(ed.CurrentUserCoordinateSystem);

                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        var p3d = (Polyline3d)tx.GetObject(chosen.ObjectId, OpenMode.ForRead);
                        PolylineVertex3d? vertex = p3d.GetVertices(tx)
                            .Where(v => v.Position.DistanceHorizontalTo(at) <= tol)
                            .OrderBy(v => v.Position.DistanceHorizontalTo(at))
                            .FirstOrDefault();
                        if (vertex == null)
                        {
                            prdDbg($"No vertex of 3D polyline {p3d.Handle} at that point: snap to the vertex.");
                            continue;
                        }

                        vertex.CheckOrOpenForWrite();
                        vertex.Position = new Point3d(vertex.Position.X, vertex.Position.Y, -99);
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
        }

        /// <summary>
        /// Flattens selected or user-picked 3D polylines by setting all vertex elevations to a fixed value (-99).
        /// </summary>
        public static void Flatten(Document doc)
        {
            Database localDb = doc.Database;
            Editor ed = doc.Editor;

            PromptSelectionResult acSSPrompt;
            acSSPrompt = ed.SelectImplied();
            SelectionSet acSSet;

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Polylines 3d
                        acSSet = acSSPrompt.Value;
                        foreach (ObjectId id in acSSet.GetObjectIds())
                        {
                            Polyline3d? p3d = id.Go<Polyline3d>(tx, OpenMode.ForWrite);
                            if (p3d == null) continue;

                            PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                            for (int i = 0; i < vertices.Length; i++)
                            {
                                vertices[i].CheckOrOpenForWrite();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, -99);
                            }
                        }
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
            else
            {
                while (true)
                {
                    var id = GetEntity(ed, "Select Plyline3d to flatten: (husk! kan også preselecte mange)", typeof(Polyline3d));
                    if (id == ObjectId.Null) return;

                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            #region Polylines 3d
                            Polyline3d? p3d = id.Go<Polyline3d>(tx, OpenMode.ForWrite);
                            if (p3d == null) { tx.Abort(); continue; }

                            PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                            for (int i = 0; i < vertices.Length; i++)
                            {
                                vertices[i].CheckOrOpenForWrite();
                                vertices[i].Position = new Point3d(
                                    vertices[i].Position.X, vertices[i].Position.Y, -99);
                            }
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
            }
        }

        /// <summary>
        /// Adjusts the elevations along selected 3D polylines based on a user-provided slope, aligning the vertices with a connected main pipe endpoint.
        /// </summary>
        public static void AdjustStik(Document doc)
        {
            prdDbg("FORUDSÆTNINGER:");
            prdDbg("1. De valgte pl3d skal have en ende vertice liggende på et hovedrør.");

            Database localDb = doc.Database;
            Editor ed = doc.Editor;

            double tol = 0.001;

            PromptSelectionResult acSSPrompt;
            acSSPrompt = ed.SelectImplied();
            SelectionSet acSSet;

            if (acSSPrompt.Status == PromptStatus.OK)
            {
                using (Transaction tx = localDb.TransactionManager.StartTransaction())
                {
                    try
                    {
                        #region Polylines 3d
                        acSSet = acSSPrompt.Value;
                        var selectedPl3ds = acSSet.GetObjectIds().Select(x => x.Go<Polyline3d>(tx)).ToHashSet();

                        List<Polyline3d> allpls = localDb.ListOfType<Polyline3d>(tx, true);
                        var notSelectedPl3ds = allpls.Where(x => !selectedPl3ds.Contains(x)).ToHashSet();

                        #region Ask for slope
                        PromptDoubleOptions pdo = new PromptDoubleOptions(
                            $"\nEnter slope in promille: Current slope <{slope.ToString("0.##")}>");
                        pdo.AllowNone = true;
                        PromptDoubleResult result = ed.GetDouble("\nEnter slope in promille: ");
                        if (result.Status == PromptStatus.None) { } //Empty clause because NONE is OK.
                        else if (((PromptResult)result).Status != PromptStatus.OK)
                        {
                            tx.Abort();
                            prdDbg("Slope entry failed!");
                            return;
                        }
                        else if (result.Status == PromptStatus.OK)
                        {
                            slope = result.Value;
                        }
                        prdDbg($"Slope: {slope.ToString("0.##")}‰");
                        #endregion

                        foreach (var p3d in selectedPl3ds)
                        {
                            if (p3d == null) continue;

                            PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                            bool found = false;
                            bool atStart = false;

                            #region Detect start or end
                            PolylineVertex3d startVert = vertices[0];
                            if (startVert.is3D() &&
                                notSelectedPl3ds.Any(x => startVert.IsOn(x, tol)))
                            {
                                found = true;
                                atStart = true;
                            }
                            PolylineVertex3d endVert = vertices.Last();
                            if (endVert.is3D() &&
                                notSelectedPl3ds.Any(x => endVert.IsOn(x, tol)))
                            {
                                found = true;
                            }
                            #endregion

                            #region Check for found
                            if (!found)
                            {
                                prdDbg($"Polyline {p3d.Handle} has no vertices on a main pipe!");
                                continue;
                            }
                            #endregion

                            p3d.CheckOrOpenForWrite();
                            if (!atStart) vertices = Enumerable.Reverse(vertices).ToArray();

                            double currentElevation = vertices[0].Position.Z;
                            for (int i = 1; i < vertices.Length; i++)
                            {
                                vertices[i].CheckOrOpenForWrite();

                                double elevationChange = slope / 1000 *
                                    (vertices[i].DistanceHorizontalTo(vertices[i - 1]));

                                prdDbg($"Current elevation: {currentElevation.ToString("0.##")}\n" +
                                    $"Elevation change: {elevationChange.ToString("0.##")}");

                                currentElevation += elevationChange;
                                prdDbg($"New elevation: {currentElevation.ToString("0.##")}");

                                vertices[i].Position = new Point3d(vertices[i].Position.X, vertices[i].Position.Y, currentElevation);
                            }
                        }
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
            else
            {
                string keyword = "Slope";
                while (true)
                {
                    string message = $"Select Polyline3d > Current slope: {slope.ToString("0.##")}‰ [Slope]:";
                    var opt = new PromptEntityOptions(message, keyword);
                    opt.SetRejectMessage("Select Polyline3d!");
                    opt.AddAllowedClass(typeof(Polyline3d), true);

                    ObjectId oid = ObjectId.Null;
                    var res = ed.GetEntity(opt);
                    if (res.Status == PromptStatus.OK)
                    {
                        oid = res.ObjectId;
                    }
                    else if (res.Status == PromptStatus.Keyword)
                    {
                        slope = GetValue(ed, "Enter slope in promille: ");
                        continue;
                    }
                    else if (res.Status == PromptStatus.Cancel) { return; }
                    if (ObjectId.Null == oid) return;

                    using (Transaction tx = localDb.TransactionManager.StartTransaction())
                    {
                        try
                        {
                            #region Polylines 3d
                            Polyline3d? p3d = oid.Go<Polyline3d>(tx, OpenMode.ForWrite);
                            if (p3d == null) { tx.Abort(); continue; }
                            List<Polyline3d> allpls = localDb.ListOfType<Polyline3d>(tx, true);
                            var notSelectedPl3ds = allpls.Where(x => x != p3d).ToHashSet();
                            PolylineVertex3d[] vertices = p3d.GetVertices(tx);

                            bool found = false;
                            bool atStart = false;

                            #region Detect start or end
                            PolylineVertex3d startVert = vertices[0];
                            if (startVert.is3D() &&
                                notSelectedPl3ds.Any(x => startVert.IsOn(x, tol)))
                            {
                                found = true;
                                atStart = true;
                            }
                            PolylineVertex3d endVert = vertices.Last();
                            if (endVert.is3D() &&
                                notSelectedPl3ds.Any(x => endVert.IsOn(x, tol)))
                            {
                                found = true;
                            }
                            #endregion

                            #region Check for found
                            if (!found)
                            {
                                prdDbg($"Polyline {p3d.Handle} has no vertices on a main pipe!");
                                tx.Abort();
                                continue;
                            }
                            #endregion

                            p3d.CheckOrOpenForWrite();
                            if (!atStart) vertices = Enumerable.Reverse(vertices).ToArray();

                            double currentElevation = vertices[0].Position.Z;
                            for (int i = 1; i < vertices.Length; i++)
                            {
                                vertices[i].CheckOrOpenForWrite();

                                double elevationChange = slope / 1000 *
                                    (vertices[i].DistanceHorizontalTo(vertices[i - 1]));

                                prdDbg($"Elevation change: {elevationChange.ToString("0.##")}, " +
                                    $"Current elevation: {currentElevation.ToString("0.##")}");

                                currentElevation += elevationChange;

                                vertices[i].Position = new Point3d(vertices[i].Position.X, vertices[i].Position.Y, currentElevation);
                            }

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
            }
        }

        /// <summary>
        /// A number from the drafter, or NaN when none is given (Dreambuild's
        /// Interaction.GetValue, which LER2ASTIK called before its body moved here).
        /// </summary>
        private static double GetValue(Editor ed, string message)
        {
            var res = ed.GetDouble(new PromptDoubleOptions(message) { AllowNone = true });
            if (res.Status == PromptStatus.OK) return res.Value;
            return double.NaN;
        }

        /// <summary>
        /// An entity of the allowed type from the drafter, asked again until one is picked,
        /// or ObjectId.Null on cancel (Dreambuild's Interaction.GetEntity, which FLATTENPL3D
        /// called before its body moved here).
        /// </summary>
        private static ObjectId GetEntity(Editor ed, string message, System.Type allowedType)
        {
            var opt = new PromptEntityOptions(message);
            opt.SetRejectMessage("Allowed type: " + allowedType.Name); // Must call this first
            opt.AddAllowedClass(allowedType, true);

            while (true)
            {
                var res = ed.GetEntity(opt);
                if (res.Status == PromptStatus.OK)
                {
                    return res.ObjectId;
                }
                else if (res.Status == PromptStatus.Cancel) return ObjectId.Null;
            }
        }
    }
}
