using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Windows;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Drawing = System.Drawing;

namespace IntersectUtilities.LerCompare;

internal static class LerCompareRuntime
{
    private static readonly OwnedResource<LerCompareSession> session = new(
        value => value.IsDisposed, value => value.Dispose());

    internal static CompareOption<Document> ActiveDocument()
    {
        // Null is a third-party API representation; convert it at the boundary.
        var document = AcadApp.DocumentManager.MdiActiveDocument;
        return document is null ? CompareOption<Document>.None : CompareOption<Document>.Some(document);
    }

    internal static void Show() => Guard(() => session.GetOrCreate(() => new()).Show());
    internal static void Run() => Guard(() => session.GetOrCreate(() => new()).Run());
    internal static void Refresh() => Guard(() => session.Match(value => { value.Refresh(); return true; }, () => false));
    internal static void Clear() => Guard(() => session.Match(value => { value.Clear(); return true; }, () => false));
    internal static void Zoom(CompareOption<Result> result) => Guard(() => session.Match(value => { value.Zoom(result); return true; }, () => false));
    internal static void Reset() => session.Reset();

    // Last-resort request guard at the AutoCAD/WinForms boundary.
    private static void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (System.Exception error)
        {
            session.Match(value => { if (!value.Control.IsDisposed) value.Control.Busy(false, error.Message); value.Clear(); return true; }, () => false);
            ActiveDocument().Match(document =>
            {
                document.Editor.WriteMessage($"\nLER Compare: {error.Message}\n");
                return true;
            }, () => false);
            UtilsCommon.Utils.prdDbg(error);
        }
    }
}

internal sealed class LerCompareSession : IDisposable
{
    // Separate from the retired standalone prototype, which must never be NETLOADed alongside this plugin.
    private static readonly Guid PaletteGuid = new("EAC6ACF8-3A43-4EDD-9C12-904C34433E09");
    private readonly PaletteSet palette;
    private readonly Overlay overlay = new();
    private CompareOption<Document> target = CompareOption<Document>.None;
    private CompareOption<Report> report = CompareOption<Report>.None;
    private bool disposed;

    internal CompareControl Control
    {
        get;
    }
    internal bool IsDisposed => disposed || palette.IsDisposed || Control.IsDisposed;

    internal LerCompareSession()
    {
        Control = new CompareControl();
        palette = new PaletteSet("LER Compare", "LERCOMPARE", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowAutoHideButton | PaletteSetStyles.ShowCloseButton
                | PaletteSetStyles.ShowPropertiesMenu | PaletteSetStyles.ShowTabForSingle,
            MinimumSize = new Drawing.Size(550, 550),
            Size = new Drawing.Size(780, 850),
            KeepFocus = false
        };
        palette.Add("Compare drawings", Control);
        palette.StateChanged += OnPaletteStateChanged;
        AcadApp.DocumentManager.DocumentToBeDeactivated += OnDeactivated;
        AcadApp.DocumentManager.DocumentActivated += OnActivated;
        AcadApp.DocumentManager.DocumentToBeDestroyed += OnClosing;
    }

    internal void Show()
    {
        Control.SetDocument(LerCompareRuntime.ActiveDocument().Match(document => document.Name, () => ""));
        palette.Visible = true;
        Refresh();
    }

    internal void Run()
    {
        Show();
        LerCompareRuntime.ActiveDocument().Match(document =>
        {
            var settings = Control.Settings();
            return Validate(document, settings).Match(error =>
            {
                Control.Busy(false, error);
                return false;
            }, () => Compare(document, settings));
        }, () => { Control.Busy(false, "Open the newer LER drawing first."); return false; });
    }

    private static CompareOption<string> Validate(Document document, Settings settings)
    {
        if (!document.Database.TileMode)
            return CompareOption<string>.Some("Switch to Model space before comparing.");
        if (!File.Exists(settings.OldPath))
            return CompareOption<string>.Some("Select an existing old DWG.");
        if (Path.GetFullPath(settings.OldPath).Equals(Path.GetFullPath(document.Name), StringComparison.OrdinalIgnoreCase))
            return CompareOption<string>.Some("The old drawing must differ from the active new drawing.");
        if (settings.Options.MatchRadiusMeters < settings.Options.ToleranceMeters)
            return CompareOption<string>.Some("The matching radius must be at least the geometry tolerance.");
        return CompareOption<string>.None;
    }

    private bool Compare(Document document, Settings settings)
    {
        Clear();
        target = CompareOption<Document>.None;
        report = CompareOption<Report>.None;
        Control.ClearReport();
        Control.Busy(true, "Reading Property Sets and geometry…");
        var old = DrawingReader.FileSnapshot(settings.OldPath, settings.Options.CoverageLayer);
        var current = DrawingReader.Read(document.Database, document.Name, settings.Options.CoverageLayer);
        return DrawingReader.Normalize(old, current, settings.Options, settings.UnitOverride).Match(error =>
        {
            Control.Busy(false, error);
            return false;
        }, () =>
        {
            Control.Busy(true, "Comparing routes…");
            var result = Engine.Compare(old, current, settings.Options);
            target = CompareOption<Document>.Some(document);
            report = CompareOption<Report>.Some(result);
            Control.Bind(result);
            Control.Busy(false, $"{result.OldCount:N0} old / {result.NewCount:N0} new polylines");
            Refresh();
            document.Editor.WriteMessage($"\nLER Compare: {result.Results.Count:N0} result groups. Filters combine with OR; counts overlap.\n");
            return true;
        });
    }

    internal void Refresh()
    {
        if (IsDisposed)
            return;
        Control.Filter();
        Clear();
        if (!palette.Visible)
            return;
        target.Match(document => report.Match(result =>
            LerCompareRuntime.ActiveDocument().Match(active =>
            {
                if (document != active)
                    return false;
                using var documentLock = document.LockDocument();
                var rows = Control.SelectedOnly
                    ? Control.Selected.Match(selected => new[] { selected }.AsEnumerable(), () => Enumerable.Empty<Result>())
                    : Control.VisibleResults;
                overlay.Draw(rows, Control.ShowOld, Control.ShowNew, Control.ShowVertices, result.Options.ToleranceUnits);
                return true;
            }, () => false), () => false), () => false);
    }

    internal void Clear() => overlay.Clear();

    internal void Zoom(CompareOption<Result> result) => result.Match(row => target.Match(document =>
        LerCompareRuntime.ActiveDocument().Match(active =>
        {
            if (active != document)
                return false;
            var points = row.Old.Concat(row.New).Concat(row.CandidateOld).Concat(row.CandidateNew).SelectMany(pipe => pipe.Points).ToList();
            if (points.Count == 0)
                return false;
            using var documentLock = document.LockDocument();
            using var view = document.Editor.GetCurrentView();
            double minX = points.Min(point => point.X), maxX = points.Max(point => point.X);
            double minY = points.Min(point => point.Y), maxY = points.Max(point => point.Y);
            var transform = (Matrix3d.PlaneToWorld(view.ViewDirection)
                * Matrix3d.Displacement(view.Target - Point3d.Origin)
                * Matrix3d.Rotation(-view.ViewTwist, view.ViewDirection, view.Target)).Inverse();
            var center = new Point3d((minX + maxX) / 2, (minY + maxY) / 2, 0).TransformBy(transform);
            var extent = new Extents3d(new Point3d(minX, minY, 0), new Point3d(maxX, maxY, 0));
            extent.TransformBy(transform);
            view.CenterPoint = new Point2d(center.X, center.Y);
            double aspect = view.Width / view.Height;
            view.Height = Math.Max(Math.Max(extent.MaxPoint.Y - extent.MinPoint.Y,
                (extent.MaxPoint.X - extent.MinPoint.X) / aspect) * 1.4, 2);
            view.Width = view.Height * aspect;
            document.Editor.SetCurrentView(view);
            AcadApp.UpdateScreen();
            return true;
        }, () => false), () => false), () => false);

    private void OnPaletteStateChanged(object? sender, PaletteSetStateEventArgs args)
    {
        if (IsDisposed || !palette.Visible)
            Clear();
    }

    private void OnDeactivated(object? sender, DocumentCollectionEventArgs args) => Clear();

    private void OnActivated(object? sender, DocumentCollectionEventArgs args)
    {
        if (!IsDisposed)
            Control.SetDocument(args.Document.Name);
        Clear(); // The user can restore the retained result with Show overlay.
    }

    private void OnClosing(object? sender, DocumentCollectionEventArgs args)
    {
        target.Match(document =>
        {
            if (document != args.Document)
                return false;
            Clear();
            target = CompareOption<Document>.None;
            report = CompareOption<Report>.None;
            if (!Control.IsDisposed)
                Control.ClearReport();
            return true;
        }, () => false);
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        AcadApp.DocumentManager.DocumentToBeDeactivated -= OnDeactivated;
        AcadApp.DocumentManager.DocumentActivated -= OnActivated;
        AcadApp.DocumentManager.DocumentToBeDestroyed -= OnClosing;
        palette.StateChanged -= OnPaletteStateChanged;
        target = CompareOption<Document>.None;
        report = CompareOption<Report>.None;
        try
        {
            Clear();
        }
        finally
        {
            // Do not invoke Visible on a native palette that has already been disposed.
            if (!palette.IsDisposed)
                palette.Dispose();
            if (!Control.IsDisposed)
                Control.Dispose();
        }
    }
}
