using System;
using System.Reflection;
using System.Runtime.Loader;

using Autodesk.AutoCAD.Runtime;

namespace NSLOAD
{
    /// <summary>
    /// Stops AutoCAD from processing the assemblies NSLOAD loads, so plugins do
    /// not have to carry a <c>NoCommands</c> marker class.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AutoCAD hooks <c>AppDomain.AssemblyLoad</c>, tags each assembly with
    /// <c>MayHaveCommands</c> (it references accoremgd) and
    /// <c>MayHaveExtensionApplication</c> (it references acdbmgd), then raises it
    /// on the public static event
    /// <c>Autodesk.AutoCAD.Runtime.ExtensionLoader.DeferredAssemblyLoad</c>.
    /// Two subscribers act on that event and between them do everything NSLOAD
    /// wants to own: one registers every <c>[CommandMethod]</c> it can find
    /// through the permanent <c>CommandClass.AddCommand</c>, the other
    /// instantiates the <c>IExtensionApplication</c> and calls
    /// <c>Initialize</c> on it.
    /// </para>
    /// <para>
    /// The event carries no cancellation, so the only way in is its backing
    /// delegate. This replaces it with a wrapper that drops assemblies loaded
    /// into an <see cref="IsolatedPluginContext"/> and forwards everything else
    /// untouched. The test is which load context the assembly is in, not when it
    /// arrived, so there is no window to race and no way to suppress an assembly
    /// NSLOAD does not own. Plugin dependencies land in the same context and are
    /// covered too. Shared assemblies go to the default context and stay visible
    /// to AutoCAD, which is what they are for.
    /// </para>
    /// <para>
    /// Consequences for the rest of NSLOAD: AutoCAD no longer builds its own
    /// instance of the plugin, so <c>PluginManager.LoadCore</c> calls
    /// <c>Initialize</c> itself on the instance <c>PluginHost</c> created — the
    /// same instance <c>TearDown</c> calls <c>Terminate</c> on. Commands exist
    /// only as the removable <c>Utils.AddCommand</c> registrations NSLOAD makes,
    /// so a second load of the same plugin in one session no longer collides.
    /// The collectible ALC can also unload, because what pinned it was AutoCAD's
    /// static table of plugin instances.
    /// </para>
    /// <para>
    /// Commands only. A plugin's <c>[LispFunction]</c> methods were registered by
    /// the same AutoCAD scan and NSLOAD's <c>CommandRegistrar</c> does not
    /// replace that, so a suppressed assembly gets no defuns. No plugin in this
    /// repo declares one.
    /// </para>
    /// <para>
    /// This composes with the identical suppressor in DevReload. Whichever
    /// installs second captures the first as its <c>original</c> and the two
    /// filters chain, each dropping only assemblies in its own context type.
    /// </para>
    /// <para>
    /// The one private member this depends on is the event's backing field. If a
    /// future AutoCAD renames it, <see cref="Install"/> throws rather than
    /// quietly leaving the scan on, and the caller reports it.
    /// </para>
    /// </remarks>
    internal static class AutoCadScanSuppressor
    {
        private const string BackingField = "m_deferredAssemblyLoadEventHandler";

        private static DeferredAssemblyLoadEventHandler? _installed;
        private static DeferredAssemblyLoadEventHandler? _original;

        /// <summary>
        /// True once the scan is suppressed. While this is false AutoCAD still
        /// registers commands and calls Initialize on its own instance, so
        /// plugins need the NoCommands marker and NSLOAD must not call
        /// Initialize a second time.
        /// </summary>
        internal static bool IsActive => _installed != null;

        internal static void Install()
        {
            if (_installed != null) return;

            FieldInfo field = Handler();
            var original = (DeferredAssemblyLoadEventHandler?)field.GetValue(null);

            DeferredAssemblyLoadEventHandler filtered = (sender, e) =>
            {
                if (AssemblyLoadContext.GetLoadContext(e.LoadedAssembly)
                        is IsolatedPluginContext)
                    return;

                original?.Invoke(sender, e);
            };

            field.SetValue(null, filtered);
            _original = original;
            _installed = filtered;
        }

        internal static void Restore()
        {
            // Anything that subscribed after us combined onto our wrapper, so the
            // field is no longer just it. Overwriting then would silently drop
            // that subscriber; leaving the wrapper in place costs nothing, since
            // this only runs as AutoCAD shuts down.
            if (_installed == null) return;

            FieldInfo field = Handler();
            if (ReferenceEquals(field.GetValue(null), _installed))
                field.SetValue(null, _original);

            _installed = null;
            _original = null;
        }

        private static FieldInfo Handler()
        {
            FieldInfo? field = typeof(ExtensionLoader).GetField(
                BackingField, BindingFlags.NonPublic | BindingFlags.Static);

            if (field == null)
                throw new InvalidOperationException(
                    $"Autodesk.AutoCAD.Runtime.ExtensionLoader.{BackingField} is gone. " +
                    "This AutoCAD version does not expose the assembly-scan hook " +
                    "NSLOAD suppresses.");

            if (field.FieldType != typeof(DeferredAssemblyLoadEventHandler))
                throw new InvalidOperationException(
                    $"Autodesk.AutoCAD.Runtime.ExtensionLoader.{BackingField} is " +
                    $"{field.FieldType.Name}, expected DeferredAssemblyLoadEventHandler.");

            return field;
        }
    }
}
