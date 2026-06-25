using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;

using NorsynDistrictZones.Editing;
using NorsynDistrictZones.Reactors;

using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: ExtensionApplication(typeof(NorsynDistrictZones.NdzExtension))]
[assembly: CommandClass(typeof(NorsynDistrictZones.NoCommands))]

namespace NorsynDistrictZones;

public class NoCommands {  }

/// <summary>
/// Plugin lifecycle. The automatic zone reactor is wired per document here in
/// <see cref="Initialize"/> and disposed in <see cref="Terminate"/> — no armed-mode
/// command; the tool is live for the session (per the design decision).
/// </summary>
public sealed class NdzExtension : IExtensionApplication
{
    private static readonly Dictionary<Document, ZoneReactor> Reactors = new();

    public void Initialize()
    {
        try
        {
            // Must be registered before any container is created/read (else proxies on reload).
            NorsynObjectsInterop.NorsynContainer.RegisterObjectFactory();
            ZoneGripOverrule.Enable();

            DocumentCollection dm = AcApp.DocumentManager;
            dm.DocumentCreated += OnDocumentCreated;
            dm.DocumentToBeDestroyed += OnDocumentToBeDestroyed;
            foreach (Document d in dm) Attach(d);

            dm.MdiActiveDocument?.Editor?.WriteMessage(
                CommandBanner.Build(typeof(NdzExtension).Assembly));
        }
        catch (System.Exception ex)
        {
            try { AcApp.DocumentManager.MdiActiveDocument?.Editor?.WriteMessage($"\nNDZ init failed:\n{ex}\n"); }
            catch { /* no active document on NETLOAD */ }
        }
    }

    public void Terminate()
    {
        try
        {
            DocumentCollection dm = AcApp.DocumentManager;
            dm.DocumentCreated -= OnDocumentCreated;
            dm.DocumentToBeDestroyed -= OnDocumentToBeDestroyed;
        }
        catch { /* shutting down */ }

        try { ZoneGripOverrule.Disable(); } catch { }

        foreach (ZoneReactor r in Reactors.Values)
        {
            try { r.Dispose(); } catch { }
        }
        Reactors.Clear();
    }

    private void OnDocumentCreated(object? sender, DocumentCollectionEventArgs e) => Attach(e.Document);

    private void OnDocumentToBeDestroyed(object? sender, DocumentCollectionEventArgs e)
    {
        if (e.Document is { } d && Reactors.TryGetValue(d, out ZoneReactor? r))
        {
            try { r.Dispose(); } catch { }
            Reactors.Remove(d);
        }
    }

    private static void Attach(Document? d)
    {
        if (d is null || Reactors.ContainsKey(d)) return;
        try { Reactors[d] = new ZoneReactor(d); } catch { }
    }
}
