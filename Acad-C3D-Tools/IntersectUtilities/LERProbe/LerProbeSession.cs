using Autodesk.AutoCAD.ApplicationServices;
using IntersectUtilities.LerPathCrawl;
using Application = Autodesk.AutoCAD.ApplicationServices.Application;

namespace IntersectUtilities.LerProbe;

internal sealed class LerProbeSession : IDisposable
{
    private static readonly List<LerProbeSession> sessions = new();
    private readonly Document document;
    private readonly LerProbeWindow window;
    private readonly LerProbeHighlight highlight;
    private bool disposed;

    private LerProbeSession(Document document, LerProbeSnapshot snapshot, LerProbeGraphic graphic)
    {
        this.document = document;
        window = CreateWindow(snapshot, graphic);
        highlight = new LerProbeHighlight(document, graphic);
        window.FormClosed += OnWindowClosed;
        window.Disposed += OnWindowDisposed;
        Application.DocumentManager.DocumentToBeDeactivated += OnDeactivated;
        Application.DocumentManager.DocumentActivated += OnActivated;
        Application.DocumentManager.DocumentToBeDestroyed += OnDocumentClosing;
    }

    private static LerProbeWindow CreateWindow(LerProbeSnapshot snapshot, LerProbeGraphic graphic)
    {
        try
        {
            return new LerProbeWindow(snapshot);
        }
        catch
        {
            graphic.Dispose();
            throw; // WinForms allocation boundary; retain ownership on failure.
        }
    }

    public static void Open(Document document, LerProbeSnapshot snapshot, LerCrawlResult<LerProbeGraphic> graphicResult)
    {
        foreach (var previous in sessions.Where(session => session.document == document).ToArray())
            previous.Dispose();
        LerProbeGraphic graphic = graphicResult.Match(value => value, error =>
        {
            document.Editor.WriteMessage($"\n{error}");
            return new LerProbeGraphic(Array.Empty<Autodesk.AutoCAD.DatabaseServices.Entity>(), false);
        });
        var session = new LerProbeSession(document, snapshot, graphic);
        sessions.Add(session);
        try
        {
            session.ShowHighlight();
            Application.ShowModelessDialog(session.window);
        }
        catch
        {
            session.Dispose();
            throw; // AutoCAD/WinForms boundary; the command guard reports it.
        }
    }

    public static void Reset()
    {
        foreach (var session in sessions.ToArray())
            session.Dispose();
    }

    private void ShowHighlight() => highlight.Show().Match(
        shown => shown,
        error => { UtilsCommon.Utils.prdDbg(error); return false; });

    private void OnActivated(object? sender, DocumentCollectionEventArgs args)
    {
        if (args.Document == document)
            ShowHighlight();
    }

    private void OnDeactivated(object? sender, DocumentCollectionEventArgs args)
    {
        if (args.Document == document)
            highlight.Hide();
    }

    private void OnDocumentClosing(object? sender, DocumentCollectionEventArgs args)
    {
        if (args.Document == document)
            Dispose();
    }

    private void OnWindowClosed(object? sender, System.Windows.Forms.FormClosedEventArgs args) => Dispose();
    private void OnWindowDisposed(object? sender, EventArgs args) => Dispose();

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        sessions.Remove(this);
        Application.DocumentManager.DocumentToBeDeactivated -= OnDeactivated;
        Application.DocumentManager.DocumentActivated -= OnActivated;
        Application.DocumentManager.DocumentToBeDestroyed -= OnDocumentClosing;
        window.FormClosed -= OnWindowClosed;
        window.Disposed -= OnWindowDisposed;
        highlight.Dispose();
        window.Close();
        window.Dispose();
    }
}
