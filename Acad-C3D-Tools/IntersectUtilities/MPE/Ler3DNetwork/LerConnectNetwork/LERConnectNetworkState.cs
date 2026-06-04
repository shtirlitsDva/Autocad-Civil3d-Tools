using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using CadColor = Autodesk.AutoCAD.Colors.Color;
using IntersectUtilities.UtilsCommon;
using static IntersectUtilities.UtilsCommon.Utils;

namespace IntersectUtilities.MPE.Ler3DNetwork.LerConnectNetwork
{
    // Per-document session for LERConnectNetwork. Gather performs the only
    // database read and snapshots every drainage polyline as point lists;
    // network grouping, parent assignment and preview then run purely in memory.
    // Apply re-opens the affected lines by ObjectId under a document lock.
    internal sealed class LERConnectNetworkState : IDisposable
    {
        private static readonly CadColor ThreeDColor = CadColor.FromColorIndex(ColorMethod.ByAci, 3);   // green
        private static readonly CadColor TwoDColor = CadColor.FromColorIndex(ColorMethod.ByAci, 1);     // red
        private static readonly CadColor OrphanColor = CadColor.FromColorIndex(ColorMethod.ByAci, 8);   // grey
        private static readonly CadColor GroupedColor = CadColor.FromColorIndex(ColorMethod.ByAci, 3);   // green = attached child
        private static readonly CadColor UngroupedColor = CadColor.FromColorIndex(ColorMethod.ByAci, 6); // magenta = unattached child

        // Parents (3D networks) render heavy; children (2D) stay thin.
        private const LineWeight ParentWeight = LineWeight.LineWeight040;
        private const LineWeight ChildWeight = LineWeight.LineWeight000;

        private readonly LerPreviewManager _preview = new();

        private List<LerClassifiedLine> _lines = new();
        private List<LerClassifiedLine> _threeD = new();
        private List<LerClassifiedLine> _twoD = new();
        private List<LERNetwork> _networks = new();
        private readonly Dictionary<int, LERNetwork> _networkById = new();
        private readonly Dictionary<ObjectId, LERParentAssignment> _assignments = new();

        // Independent preview visibility flags, additive per checkbox. The "kind"
        // pair colours by 2D/3D (red/green); the "network" pair colours mains per
        // network and children by parent; the "grouping" pair colours 2D children
        // by whether they reached a parent hovedledning (attached) or not.
        private bool _show2D;
        private bool _show3D;
        private bool _showMains;
        private bool _showChildren;
        private bool _showGrouped;
        private bool _showUngrouped;

        // Solved connections from the last "Opdater forhåndsvisning"; reused by Apply.
        private List<LERConnectionResult> _results = new();

        public LERConnectNetworkState(Document owner)
        {
            Owner = owner;
        }

        public Document Owner { get; }

        public bool HasData => _lines.Count > 0;

        public bool HasComputed { get; private set; }

        public int ConnectedCount { get; private set; }

        public int ConflictCount { get; private set; }

        public int NoParentCount { get; private set; }

        public void Dispose()
        {
            _preview.Dispose();
        }

        // ---- Gather (the only database read) --------------------------------

        public void Gather()
        {
            if (!IsActive()) return;

            try
            {
                _preview.Clear();
                _networks.Clear();
                _networkById.Clear();
                _assignments.Clear();
                _results = new();
                HasComputed = false;
                ConnectedCount = 0;
                ConflictCount = 0;
                NoParentCount = 0;

                // Gather the whole drawing, then keep only the drainage subset
                // (LerConnectNetwork operates on the "Afløbsledning" lines).
                _lines = LerGather.GatherAll(Owner)
                    .Where(l => LerGather.IsTargetLayer(l.Layer))
                    .ToList();
                _threeD = _lines.Where(l => l.Kind == LerLineKind.ThreeD).ToList();
                _twoD = _lines.Where(l => l.Kind == LerLineKind.TwoD).ToList();

                RenderPreview();

                SetStatus(
                    $"Indlæst {_lines.Count} polylinjer på \"{LerGather.TargetLayerFragment}\"-lag: "
                    + $"{_threeD.Count} 3D, {_twoD.Count} 2D.",
                    _lines.Count > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Indlæsning fejlede. Se debug output.", LerStatusKind.Error);
            }
        }

        // ---- Update preview: group networks, match parents, solve connections -

        // Triggered by "Opdater forhåndsvisning". Recomputes everything in memory
        // from the loaded snapshot and refreshes whatever toggles are active.
        public void UpdatePreview(double distance, double permille)
        {
            if (!IsActive()) return;
            if (_lines.Count == 0)
            {
                SetStatus("Ingen polylinjer indlæst. Klik \"Indlæs / genindlæs tegning\".", LerStatusKind.Warning);
                return;
            }

            _networks = LERConnectNetworkAnalyzer.BuildNetworks(_threeD);
            _networkById.Clear();
            foreach (LERNetwork network in _networks)
            {
                _networkById[network.Id] = network;
            }

            _assignments.Clear();
            foreach (LerClassifiedLine twoD in _twoD)
            {
                LERParentAssignment? assignment = LERConnectNetworkAnalyzer.AssignParent(twoD.Points, _networks, distance);
                if (assignment != null)
                {
                    _assignments[twoD.Id] = assignment;
                }
            }

            _results = SolveAll(distance, permille);
            ConnectedCount = _results.Count(r => r.Status == LERConnectionStatus.Connected);
            ConflictCount = _results.Count(r =>
                r.Status == LERConnectionStatus.NoIntersection || r.Status == LERConnectionStatus.Degenerate);
            NoParentCount = _twoD.Count - _assignments.Count;
            HasComputed = true;

            RenderPreview();

            SetStatus(
                $"Afstand {distance:0.###} m · {_networks.Count} netværk · "
                + $"{_assignments.Count} stik tilknyttet.",
                LerStatusKind.Ok);
        }

        private List<LERConnectionResult> SolveAll(double distance, double permille)
        {
            Dictionary<ObjectId, LerClassifiedLine> twoDById = _twoD.ToDictionary(l => l.Id);

            // Cap how far a child may extend to meet its main: a child sits within
            // the check distance of a main, so a sane connection stays local.
            double maxConnect = Math.Max(2.0, distance * 10.0);

            List<LERConnectionResult> results = new();
            foreach (KeyValuePair<ObjectId, LERParentAssignment> pair in _assignments)
            {
                if (!twoDById.TryGetValue(pair.Key, out LerClassifiedLine? twoD)) continue;
                if (!_networkById.TryGetValue(pair.Value.NetworkId, out LERNetwork? parent)) continue;

                results.Add(LERConnectNetworkAnalyzer.Solve(
                    twoD.Points, twoD.Id, parent, pair.Value.ConnectAtEnd, permille, maxConnect));
            }
            return results;
        }

        // ---- Preview visibility ---------------------------------------------

        public void SetVisibility(
            bool show2D, bool show3D,
            bool showMains, bool showChildren,
            bool showGrouped, bool showUngrouped)
        {
            _show2D = show2D;
            _show3D = show3D;
            _showMains = showMains;
            _showChildren = showChildren;
            _showGrouped = showGrouped;
            _showUngrouped = showUngrouped;
            RenderPreview();
        }

        // Draws the transient preview as independent, additive contributions —
        // one per checkbox, each adding its segments in its own colour. Where
        // toggles overlap the same geometry the later contribution renders on
        // top (grouping > network > kind). Network/grouping colouring needs the
        // networks built by Opdater.
        private void RenderPreview()
        {
            if (!IsActive()) return;
            _preview.Clear();
            if (_lines.Count == 0) return;

            bool hasNetworks = _networks.Count > 0;
            List<LerPreviewItem> items = new();

            // --- Visning: by-kind colours (green 3D, red 2D) ---
            if (_show3D)
            {
                foreach (LerClassifiedLine line in _threeD)
                {
                    items.Add(new LerPreviewItem(line.Points, ThreeDColor, ParentWeight));
                }
            }
            if (_show2D)
            {
                foreach (LerClassifiedLine twoD in _twoD)
                {
                    items.Add(new LerPreviewItem(twoD.Points, TwoDColor, ChildWeight));
                }
            }

            // --- Netværk: per-network mains, children by their parent ---
            if (_showMains && hasNetworks)
            {
                foreach (LERNetwork network in _networks)
                {
                    foreach (IReadOnlyList<Point3d> member in network.MemberPoints)
                    {
                        items.Add(new LerPreviewItem(member, network.Color, ParentWeight));
                    }
                }
            }
            if (_showChildren && hasNetworks)
            {
                foreach (LerClassifiedLine twoD in _twoD)
                {
                    CadColor color = _assignments.TryGetValue(twoD.Id, out LERParentAssignment? a)
                                     && _networkById.TryGetValue(a.NetworkId, out LERNetwork? parent)
                        ? parent.Color
                        : OrphanColor;
                    items.Add(new LerPreviewItem(twoD.Points, color, ChildWeight));
                }
            }

            // --- Gruppering: children split by whether they reached a parent ---
            if (_showGrouped && HasComputed)
            {
                AddChildrenByAttachment(items, attached: true, GroupedColor);
            }
            if (_showUngrouped && HasComputed)
            {
                AddChildrenByAttachment(items, attached: false, UngroupedColor);
            }

            _preview.Show(items);
        }

        // Adds the 2D children that are (attached) matched to a parent hovedledning
        // or (else) have no parent within the check distance, in the given colour.
        private void AddChildrenByAttachment(List<LerPreviewItem> items, bool attached, CadColor color)
        {
            foreach (LerClassifiedLine twoD in _twoD)
            {
                if (_assignments.ContainsKey(twoD.Id) != attached) continue;
                items.Add(new LerPreviewItem(twoD.Points, color, ChildWeight));
            }
        }

        public void ClearAllPreview()
        {
            _preview.Clear();
        }

        // ---- Apply -----------------------------------------------------------

        // Replaces the solved stik (2D→3D) on their original layers.
        public void ApplyConnections()
        {
            if (!IsActive()) return;
            if (!HasComputed)
            {
                SetStatus("Klik \"Opdater forhåndsvisning\" først.", LerStatusKind.Warning);
                return;
            }

            int connected = ReplaceWithRebuilt(_results);
            if (connected < 0) return; // snapshot diverged; status already set

            SetStatus(
                $"Erstattet {connected} stik (2D→3D) på deres oprindelige lag. "
                + $"Sprunget over: {NoParentCount} uden forælder, {ConflictCount} uden skæring.",
                connected > 0 ? LerStatusKind.Ok : LerStatusKind.Warning);
        }

        // Shared apply core: under one document lock, refuse if the drawing
        // diverged from the Opdater snapshot, otherwise replace each connected
        // source (build new 3D line on the same layer, inherit all attached data,
        // erase the original). Returns the count created, or -1 on divergence.
        private int ReplaceWithRebuilt(List<LERConnectionResult> results)
        {
            try
            {
                ClearAllPreview();
                int count = 0;
                using (DocumentLock docLock = Owner.LockDocument())
                using (Transaction tx = Owner.Database.TransactionManager.StartTransaction())
                {
                    try
                    {
                        if (!SnapshotMatches(tx))
                        {
                            tx.Commit();
                            HasComputed = false;
                            SetStatus(
                                "Geometri ændret siden \"Opdater forhåndsvisning\". Kør Opdater igen.",
                                LerStatusKind.Warning);
                            return -1;
                        }

                        foreach (LERConnectionResult result in results)
                        {
                            if (result.Status != LERConnectionStatus.Connected || result.NewPoints == null)
                            {
                                continue;
                            }

                            // Same-layer 3D replace + XData/property-set carry-over.
                            if (LerRebuild.ReplacePolyline3d(tx, result.SourceId, result.NewPoints))
                            {
                                count++;
                            }
                        }
                        tx.Commit();
                    }
                    catch (System.Exception)
                    {
                        tx.Abort();
                        throw;
                    }
                }

                // The drawing now diverges from the snapshot; require a fresh
                // Opdater before another apply.
                HasComputed = false;
                return count;
            }
            catch (System.Exception ex)
            {
                prdDbg(ex);
                SetStatus("Operationen fejlede. Se debug output.", LerStatusKind.Error);
                return -1;
            }
        }

        // True when the live drawing still matches the snapshot the results were
        // solved from: same set of target lines, each with identical vertices.
        // Runs inside the apply document lock so no edit can slip in between.
        private bool SnapshotMatches(Transaction tx)
        {
            Dictionary<ObjectId, List<Point3d>> current = new();
            foreach (Polyline3d pl in Owner.Database.HashSetOfType<Polyline3d>(tx))
            {
                if (!LerGather.IsTargetLayer(pl.Layer)) continue;
                List<Point3d> pts = pl.GetVertices(tx).Select(v => v.Position).ToList();
                if (pts.Count < 2) continue;
                current[pl.ObjectId] = pts;
            }

            if (current.Count != _lines.Count)
            {
                return false;
            }

            foreach (LerClassifiedLine line in _lines)
            {
                if (!current.TryGetValue(line.Id, out List<Point3d>? pts)) return false;
                if (pts.Count != line.Points.Count) return false;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (pts[i].DistanceTo(line.Points[i]) > 1e-6) return false;
                }
            }

            return true;
        }

        // ---- Status routing --------------------------------------------------

        // No-op when this state's document isn't the active one, so a background
        // drawing can't clobber the palette (the palette is process-wide).
        public void SetStatus(string message, LerStatusKind kind)
        {
            if (!IsActive()) return;
            LERConnectNetworkRuntime.NotifyPaletteStatus(message, kind);
        }

        private bool IsActive()
        {
            return Owner == Application.DocumentManager.MdiActiveDocument;
        }
    }
}
