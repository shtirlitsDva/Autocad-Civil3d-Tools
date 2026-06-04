using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerAnalyseNetwork
{
    // Per-document session for LerAnalyseNetwork. Gather performs the only database
    // read (the shared 2D/3D split), then the analysis and preview run purely in
    // memory. Read-only: this command never modifies the drawing.
    internal sealed class LerAnalyseNetworkState : IDisposable
    {
        // Per-category colours: bridge (green, links two pivots within depth),
        // floating (orange, reaches one pivot within depth), out of range (red).
        private static readonly CadColor BridgeColor = CadColor.FromColorIndex(ColorMethod.ByAci, 3);      // green
        private static readonly CadColor FloatingColor = CadColor.FromColorIndex(ColorMethod.ByAci, 30);   // orange
        private static readonly CadColor OutOfRangeColor = CadColor.FromColorIndex(ColorMethod.ByAci, 1);  // red

        // The 3D pivots (drainage pipes the 2D lines connect to), drawn on request.
        private static readonly CadColor PivotColor = CadColor.FromColorIndex(ColorMethod.ByAci, 5);      // blue

        // Inspect overlay: the lifted bridge (cyan) and a riser from each -99 vertex
        // up to its new height (yellow).
        private static readonly CadColor InspectNewColor = CadColor.FromColorIndex(ColorMethod.ByAci, 4);    // cyan
        private static readonly CadColor InspectVectorColor = CadColor.FromColorIndex(ColorMethod.ByAci, 2); // yellow

        private const LineWeight ChainWeight = LineWeight.LineWeight030;
        private const LineWeight InspectWeight = LineWeight.LineWeight050;

        // One slope arrow every SlopeArrowSpacing metres along a 3D pipe (short
        // segments get one at the midpoint); the arrowheads are drawn and kept
        // screen-constant by _slopeArrows. SlopeMinFall is the smallest fall a
        // segment must have to be considered sloped (below it endpoints are flat
        // within noise and the arrow direction would be meaningless).
        private const double SlopeArrowSpacing = 10.0;
        private const double SlopeMinFall = 0.001;

        private readonly LerPreviewManager _preview = new();
        private readonly LerSlopeArrowManager _slopeArrows = new();

        private List<LerScannedPolyline> _polys = new();

        // The 3D pipes (pivots), cached from the last gather so slope arrows can be
        // drawn without another database read.
        private List<LerClassifiedLine> _targets3D = new();

        // Additive preview toggles, one per scan category, plus the slope overlay.
        private bool _showBridges = true;
        private bool _showFloating = true;
        private bool _showOutOfRange;
        private bool _showSlope;
        private bool _showPivots;

        // Scan depth from the slider: how many polylines out from each pivot the
        // scanner reaches.
        private int _scanDepth = 1;

        // Inspect view: hover any green segment to preview the whole bridge lifting.
        private readonly LerPreviewManager _hoverPreview = new();
        // Screen-constant slope arrows on the hovered bridge's lifted geometry,
        // coloured to match the lifted-pipe preview (cyan).
        private readonly LerSlopeArrowManager _hoverSlope = new(InspectNewColor);
        private bool _inspect;
        private bool _monitorHooked;
        private int _lastHoverGroup = -1;
        private List<(Point2d Xy, double Z)> _anchors = new();
        private Dictionary<ObjectId, (IReadOnlyList<Point3d> Orig, IReadOnlyList<Point3d> New)> _fixedById = new();
        private Dictionary<ObjectId, int> _bridgeOf = new();   // polyline id -> bridge group
        private List<List<ObjectId>> _bridges = new();          // bridge group -> member ids

        public LerAnalyseNetworkState(Document owner)
        {
            Owner = owner;
        }

        public Document Owner { get; }

        public bool HasComputed { get; private set; }

        public int BridgeCount { get; private set; }

        public int FloatingCount { get; private set; }

        public int OutOfRangeCount { get; private set; }

        public int PolylineCount => _polys.Count;

        // Number of 3D pipes (pivots) found in the last gather.
        public int PivotCount { get; private set; }

        // Deepest bridge any polyline can reach — the scan depth at which every
        // bridge is found. Drives the slider's default maximum. Depth-independent.
        public int MaxBridgeDepth { get; private set; }

        // Live category for one polyline at the current scan depth.
        private bool IsBridge(LerScannedPolyline p) => p.BridgeCost <= _scanDepth;
        private bool IsFloating(LerScannedPolyline p) => !IsBridge(p) && p.PivotDepth <= _scanDepth;

        public void Dispose()
        {
            UnhookMonitor();
            _slopeArrows.Dispose();
            _hoverSlope.Dispose();
            _hoverPreview.Dispose();
            _preview.Dispose();
        }

        // ---- Gather + scan (the only database read) -------------------------

        public void Gather()
        {
            if (!IsActive()) return;

            try
            {
                _preview.Clear();
                HasComputed = false;
                BridgeCount = 0;
                FloatingCount = 0;
                OutOfRangeCount = 0;
                PivotCount = 0;
                MaxBridgeDepth = 1;
                _targets3D = new();

                List<LerClassifiedLine> all = LerGather.GatherAll(Owner);

                // Only drainage polylines: layer name contains "Afløbsledning" (and is
                // not one of our generated LER_N_ outputs). Applies to BOTH classes —
                // pivots (3D) and subjects (2D).
                List<LerClassifiedLine> drainage = all
                    .Where(l => LerGather.IsTargetLayer(l.Layer))
                    .ToList();

                List<LerClassifiedLine> targets3D = drainage.Where(l => l.Kind == LerLineKind.ThreeD).ToList();
                List<LerClassifiedLine> subjects2D = drainage.Where(l => l.Kind == LerLineKind.TwoD).ToList();

                _targets3D = targets3D;
                PivotCount = targets3D.Count;

                _polys = LerAnalyseNetworkAnalyzer.Analyze(targets3D, subjects2D, out int maxBridge);
                MaxBridgeDepth = maxBridge;

                // Cache the 3D pivot endpoints for the inspect lift preview.
                _anchors = BuildAnchors(targets3D);

                HasComputed = true;
                Recount();
                RecomputeInspect();
                RenderPreview();

                SetStatus(
                    $"{targets3D.Count} 3D-rør (pivoter) · {subjects2D.Count} 2D-drænlinjer scannet. "
                    + $"Ved dybde {_scanDepth}: {BridgeCount} broer, {FloatingCount} grene.",
                    subjects2D.Count > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Analyse fejlede. Se debug output.", LerStatusKind.Error);
            }
        }

        // ---- Preview visibility ---------------------------------------------

        public void SetVisibility(bool showBridges, bool showFloating, bool showOutOfRange, bool showSlope, bool showPivots)
        {
            _showBridges = showBridges;
            _showFloating = showFloating;
            _showOutOfRange = showOutOfRange;
            _showSlope = showSlope;
            _showPivots = showPivots;
            RenderPreview();

            // Sync the hovered bridge's slope arrows to the new toggle immediately,
            // without waiting for the next cursor move.
            if (_inspect) UpdateHoverPreview(_lastHoverGroup);
        }

        // New scan depth from the slider: re-classify every polyline against the new
        // budget and re-render. A pure threshold over the precomputed scan — no BFS.
        public void SetScanDepth(int depth)
        {
            _scanDepth = depth;
            Recount();
            RecomputeInspect();
            RenderPreview();
        }

        private void Recount()
        {
            BridgeCount = 0;
            FloatingCount = 0;
            OutOfRangeCount = 0;
            foreach (LerScannedPolyline p in _polys)
            {
                if (IsBridge(p)) BridgeCount++;
                else if (IsFloating(p)) FloatingCount++;
                else OutOfRangeCount++;
            }
        }

        private void RenderPreview()
        {
            if (!IsActive()) return;
            _preview.Clear();
            if (!HasComputed) { _slopeArrows.Clear(); return; }

            List<LerPreviewItem> items = new();
            foreach (LerScannedPolyline p in _polys)
            {
                CadColor color;
                if (IsBridge(p))
                {
                    if (!_showBridges) continue;
                    color = BridgeColor;
                }
                else if (IsFloating(p))
                {
                    if (!_showFloating) continue;
                    color = FloatingColor;
                }
                else
                {
                    if (!_showOutOfRange) continue;
                    color = OutOfRangeColor;
                }

                items.Add(new LerPreviewItem(p.Points, color, ChainWeight));
            }

            // The 3D pivots themselves, on request (blue).
            if (_showPivots)
            {
                foreach (LerClassifiedLine line in _targets3D)
                    items.Add(new LerPreviewItem(line.Points, PivotColor, ChainWeight));
            }

            _preview.Show(items);
            RenderSlope();
        }

        // Slope arrows live in their own transient manager (screen-constant, resized
        // on zoom via Application.Idle), so they are rendered separately from the
        // model-space category/split polylines above.
        private void RenderSlope()
        {
            if (_showSlope) _slopeArrows.Show(Owner, BuildSlopeAnchors());
            else _slopeArrows.Clear();
        }

        // Slope arrow anchors for every 3D pipe (the ground-truth fall overlay).
        private List<LerSlopeAnchor> BuildSlopeAnchors()
        {
            List<LerSlopeAnchor> anchors = new();
            foreach (LerClassifiedLine line in _targets3D)
                AppendSlopeAnchors(anchors, line.Points);
            return anchors;
        }

        // Append one downhill arrow anchor every SlopeArrowSpacing metres along the
        // polyline (short segments get one at the midpoint), pointing toward the lower
        // end. A segment is skipped unless BOTH endpoints carry a real elevation (is3D
        // — neither the Z=0 nor the Z=-99 placeholder) and the fall exceeds SlopeMinFall.
        private static void AppendSlopeAnchors(List<LerSlopeAnchor> anchors, IReadOnlyList<Point3d> pts)
        {
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Point3d p0 = pts[i], p1 = pts[i + 1];
                if (!p0.Z.is3D() || !p1.Z.is3D()) continue;        // placeholder endpoint
                if (Math.Abs(p0.Z - p1.Z) < SlopeMinFall) continue; // flat within noise

                double dx = p1.X - p0.X, dy = p1.Y - p0.Y, dz = p1.Z - p0.Z;
                double len = Math.Sqrt(dx * dx + dy * dy);
                if (len < 1e-9) continue;                           // vertical segment

                // Unit downhill direction in plan (toward the lower endpoint).
                double sign = p1.Z < p0.Z ? 1.0 : -1.0;
                double ux = sign * dx / len, uy = sign * dy / len;

                void Emit(double t) => anchors.Add(new LerSlopeAnchor(
                    p0.X + dx * t, p0.Y + dy * t, p0.Z + dz * t, ux, uy));

                if (len < SlopeArrowSpacing)
                {
                    Emit(0.5);
                }
                else
                {
                    for (double s = SlopeArrowSpacing * 0.5; s < len; s += SlopeArrowSpacing)
                        Emit(s / len);
                }
            }
        }

        public void ClearAllPreview()
        {
            _preview.Clear();
            _slopeArrows.Clear();
            _hoverPreview.Clear();
            _hoverSlope.Clear();
            _lastHoverGroup = -1;
        }

        // ---- Fix bridges (Case 2: a chain anchored to two pivots) ------------

        // Lifts the pickfirst-selected 2D drainage polylines to 3D — the operator
        // selects the bridge polylines in the drawing first, then clicks the button.
        public void FixSelectedIslands()
        {
            if (!IsActive()) return;

            PromptSelectionResult psr = Owner.Editor.SelectImplied();
            if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
            {
                SetStatus(
                    "Vælg en eller flere 2D-polylinjer (broer) i tegningen først, og klik så igen.",
                    LerStatusKind.Warning);
                return;
            }

            LiftIslands(psr.Value.GetObjectIds(), "markerede");
        }

        // Dry-run of "Fiks alle broer": runs the same solve in memory (no write) so
        // the UI can confirm against real numbers before mutating the drawing.
        public LerGreenFixPreview PreviewAllGreen()
        {
            if (!HasComputed) return new LerGreenFixPreview(0, 0, 0, 0, 0);

            List<(ObjectId Id, IReadOnlyList<Point3d> Points)> green = new();
            foreach (LerScannedPolyline p in _polys)
            {
                if (IsBridge(p)) green.Add((p.Id, p.Points));
            }
            if (green.Count == 0) return new LerGreenFixPreview(0, 0, 0, 0, 0);

            List<LerIslandFix> fixes = LerIslandFixer.Solve(green, _anchors, out _);
            if (fixes.Count == 0) return new LerGreenFixPreview(0, 0, 0, 0, green.Count);

            int verts = 0;
            double zMin = double.MaxValue, zMax = double.MinValue;
            foreach (LerIslandFix f in fixes)
            {
                verts += f.NewPoints.Count;
                foreach (Point3d pt in f.NewPoints)
                {
                    if (pt.Z < zMin) zMin = pt.Z;
                    if (pt.Z > zMax) zMax = pt.Z;
                }
            }

            return new LerGreenFixPreview(fixes.Count, verts, zMin, zMax, green.Count - fixes.Count);
        }

        // Lifts every green (bridge) polyline at the current scan depth to 3D.
        public void FixAllGreen()
        {
            if (!IsActive()) return;
            if (!HasComputed)
            {
                SetStatus("Kør \"Indlæs og analyser\" først.", LerStatusKind.Warning);
                return;
            }

            List<ObjectId> green = new();
            foreach (LerScannedPolyline p in _polys)
            {
                if (IsBridge(p)) green.Add(p.Id);
            }
            if (green.Count == 0)
            {
                SetStatus($"Ingen grønne broer ved scanningsdybde {_scanDepth}.", LerStatusKind.Warning);
                return;
            }

            LiftIslands(green, "grønne bro");
        }

        // Shared lift core: rebuild the given 2D drainage polylines as sloped 3D
        // bridges (each chain's two ends snap to the pivot elevation they touch,
        // interior vertices interpolate linearly by plan length), keeping layer +
        // XData + property sets, then auto-reload the scan so the preview refreshes
        // and the lifted bridges become pivots for the next pass.
        private void LiftIslands(IReadOnlyList<ObjectId> ids, string label)
        {
            try
            {
                ClearAllPreview();
                int fixedCount = 0;
                List<string> warnings = new();

                using (DocumentLock docLock = Owner.LockDocument())
                using (Transaction tx = Owner.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        // The target 2D drainage polylines to lift.
                        List<(ObjectId Id, IReadOnlyList<Point3d> Points)> selected = new();
                        foreach (ObjectId id in ids)
                        {
                            if (id.IsErased) continue;
                            if (tx.GetObject(id, OpenMode.ForRead, false) is not Polyline3d pl) continue;
                            List<Point3d> pts = pl.GetVertices(tx).Select(v => v.Position).ToList();
                            if (pts.Count < 2) continue;
                            if (LerGather.Classify(pts) != LerLineKind.TwoD) continue; // already 3D
                            selected.Add((id, pts));
                        }

                        if (selected.Count == 0)
                        {
                            tx.Commit();
                            SetStatus("Ingen 2D-polylinjer at hæve.", LerStatusKind.Warning);
                            return;
                        }

                        // Every drainage 3D pipe endpoint is a candidate anchor.
                        List<(Point2d Xy, double Z)> anchors = new();
                        foreach (Polyline3d pl in Owner.Database.HashSetOfType<Polyline3d>(tx))
                        {
                            if (!LerGather.IsTargetLayer(pl.Layer)) continue;
                            List<Point3d> pts = pl.GetVertices(tx).Select(v => v.Position).ToList();
                            if (pts.Count < 2) continue;
                            if (LerGather.Classify(pts) != LerLineKind.ThreeD) continue;
                            anchors.Add((new Point2d(pts[0].X, pts[0].Y), pts[0].Z));
                            anchors.Add((new Point2d(pts[^1].X, pts[^1].Y), pts[^1].Z));
                        }

                        List<LerIslandFix> fixes = LerIslandFixer.Solve(selected, anchors, out warnings);
                        foreach (LerIslandFix fix in fixes)
                        {
                            if (LerRebuild.ReplacePolyline3d(tx, fix.Id, fix.NewPoints)) fixedCount++;
                        }

                        tx.Commit();
                    }
                    catch (System.Exception)
                    {
                        tx.Abort();
                        throw;
                    }
                }

                // The implied selection may point at erased objects; clear it.
                Owner.Editor.SetImpliedSelection(Array.Empty<ObjectId>());

                // Auto-reload: re-scan so the preview refreshes and the lifted
                // bridges count as pivots for whatever floats off them next.
                Gather();

                string msg = $"Hævet {fixedCount} {label}-polylinje(r) til 3D – genscannet.";
                if (warnings.Count > 0)
                {
                    msg += $" {warnings.Count} sprunget over – fx: {warnings[0]}";
                }
                SetStatus(msg, fixedCount > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Fiks fejlede. Se debug output.", LerStatusKind.Error);
            }
        }

        // ---- Inspect view (hover a green bridge to preview the lift) ----------

        public void SetInspect(bool on)
        {
            if (!IsActive()) return;
            _inspect = on;
            if (on)
            {
                RecomputeInspect();
                HookMonitor();
            }
            else
            {
                UnhookMonitor();
                _hoverPreview.Clear();
                _hoverSlope.Clear();
                _lastHoverGroup = -1;
            }
        }

        private static List<(Point2d Xy, double Z)> BuildAnchors(List<LerClassifiedLine> targets3D)
        {
            List<(Point2d, double)> anchors = new();
            foreach (LerClassifiedLine t in targets3D)
            {
                IReadOnlyList<Point3d> pts = t.Points;
                anchors.Add((new Point2d(pts[0].X, pts[0].Y), pts[0].Z));
                anchors.Add((new Point2d(pts[^1].X, pts[^1].Y), pts[^1].Z));
            }
            return anchors;
        }

        // Runs the same lift the buttons use, in memory, caching each green
        // polyline's destination geometry and grouping the green polylines into
        // bridges so hovering any segment can lift the whole bridge.
        private void RecomputeInspect()
        {
            _fixedById = new();
            _bridgeOf = new();
            _bridges = new();
            _lastHoverGroup = -1;
            _hoverPreview.Clear();
            _hoverSlope.Clear();
            if (!_inspect || !HasComputed) return;

            Dictionary<ObjectId, IReadOnlyList<Point3d>> orig = new();
            List<(ObjectId Id, IReadOnlyList<Point3d> Points)> green = new();
            foreach (LerScannedPolyline p in _polys)
            {
                if (!IsBridge(p)) continue;
                green.Add((p.Id, p.Points));
                orig[p.Id] = p.Points;
            }
            if (green.Count == 0) return;

            List<LerIslandFix> fixes = LerIslandFixer.Solve(green, _anchors, out _);
            List<(ObjectId Id, IReadOnlyList<Point3d> Points)> fixedSubjects = new();
            foreach (LerIslandFix f in fixes)
            {
                if (!orig.TryGetValue(f.Id, out IReadOnlyList<Point3d>? o)) continue;
                _fixedById[f.Id] = (o, f.NewPoints);
                fixedSubjects.Add((f.Id, o));
            }

            GroupBridges(fixedSubjects);
        }

        // Connected components (shared endpoints) of the fixed polylines = bridges.
        private void GroupBridges(List<(ObjectId Id, IReadOnlyList<Point3d> Points)> subjects)
        {
            LerNodeIndexer nodes = new(LerAnalyseNetworkAnalyzer.Tolerance);
            int k = subjects.Count;
            int[] a = new int[k], b = new int[k];
            for (int i = 0; i < k; i++)
            {
                IReadOnlyList<Point3d> pts = subjects[i].Points;
                a[i] = nodes.GetOrAdd(new Point2d(pts[0].X, pts[0].Y));
                b[i] = nodes.GetOrAdd(new Point2d(pts[pts.Count - 1].X, pts[pts.Count - 1].Y));
            }
            int n = nodes.Count;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) parent[i] = i;
            for (int i = 0; i < k; i++) Union(parent, a[i], b[i]);

            Dictionary<int, int> rootToGroup = new();
            for (int i = 0; i < k; i++)
            {
                int root = Find(parent, a[i]);
                if (!rootToGroup.TryGetValue(root, out int g))
                {
                    g = _bridges.Count;
                    rootToGroup[root] = g;
                    _bridges.Add(new List<ObjectId>());
                }
                _bridges[g].Add(subjects[i].Id);
                _bridgeOf[subjects[i].Id] = g;
            }
        }

        private static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
        private static void Union(int[] p, int x, int y) { int rx = Find(p, x), ry = Find(p, y); if (rx != ry) p[ry] = rx; }

        private void HookMonitor()
        {
            if (_monitorHooked) return;
            Owner.Editor.PointMonitor += OnPointMonitor;
            _monitorHooked = true;
        }

        private void UnhookMonitor()
        {
            if (!_monitorHooked) return;
            Owner.Editor.PointMonitor -= OnPointMonitor;
            _monitorHooked = false;
        }

        // Fires on every cursor move while inspect is on. Finds the bridge under the
        // cursor and, when it changes, redraws the whole-bridge lift overlay.
        private void OnPointMonitor(object? sender, PointMonitorEventArgs e)
        {
            if (!_inspect || !IsActive()) return;

            int group = -1;
            try
            {
                foreach (FullSubentityPath path in e.Context.GetPickedEntities())
                {
                    ObjectId[] ids = path.GetObjectIds();
                    if (ids.Length == 0) continue;
                    if (_bridgeOf.TryGetValue(ids[ids.Length - 1], out int g)) { group = g; break; }
                }
            }
            catch (System.Exception)
            {
                group = -1;
            }

            if (group == _lastHoverGroup) return;
            _lastHoverGroup = group;
            UpdateHoverPreview(group);
        }

        private void UpdateHoverPreview(int group)
        {
            _hoverPreview.Clear();
            if (group < 0 || group >= _bridges.Count) { _hoverSlope.Clear(); return; }

            List<LerPreviewItem> items = new();
            List<LerSlopeAnchor> slope = new();
            foreach (ObjectId id in _bridges[group])
            {
                if (!_fixedById.TryGetValue(id, out (IReadOnlyList<Point3d> Orig, IReadOnlyList<Point3d> New) f)) continue;

                // The lifted bridge polyline, plus a riser from each -99 vertex.
                items.Add(new LerPreviewItem(f.New, InspectNewColor, InspectWeight));
                int count = Math.Min(f.Orig.Count, f.New.Count);
                for (int i = 0; i < count; i++)
                {
                    items.Add(new LerPreviewItem(new[] { f.Orig[i], f.New[i] }, InspectVectorColor, ChainWeight));
                }

                // Slope arrows on the LIFTED geometry so the operator can verify the
                // projected fall direction before committing — only when the global
                // Fald option (Visning) is on.
                if (_showSlope) AppendSlopeAnchors(slope, f.New);
            }

            _hoverPreview.Show(items);
            if (slope.Count > 0) _hoverSlope.Show(Owner, slope);
            else _hoverSlope.Clear();
        }

        // ---- Status routing --------------------------------------------------

        public void SetStatus(string message, LerStatusKind kind)
        {
            if (!IsActive()) return;
            LerAnalyseNetworkRuntime.NotifyPaletteStatus(message, kind);
        }

        private bool IsActive()
        {
            return Owner == Application.DocumentManager.MdiActiveDocument;
        }
    }
}
