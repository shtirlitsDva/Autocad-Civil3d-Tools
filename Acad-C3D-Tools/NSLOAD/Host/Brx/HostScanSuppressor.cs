using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

using Bricscad.ApplicationServices;

namespace NSLOAD
{
    /// <summary>
    /// Stops BricsCAD from processing the assemblies NSLOAD loads, so NSLOAD owns
    /// their commands and their Initialize. BricsCAD counterpart of the AutoCAD
    /// file of the same name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// BricsCAD subscribes the private static
    /// <c>Bricscad.ApplicationServices.AssemblyLoader.OnLoad</c> directly to
    /// <c>AppDomain.AssemblyLoad</c>. For every assembly that references BrxMgd it
    /// registers each <c>[CommandMethod]</c> permanently and instantiates the
    /// <c>IExtensionApplication</c>, calling <c>Initialize</c> on it - the two
    /// things NSLOAD wants to own, exactly as on AutoCAD.
    /// </para>
    /// <para>
    /// The handler is static, so an equal delegate can be built and unsubscribed.
    /// It is replaced with a wrapper that drops assemblies loaded into an
    /// <see cref="IsolatedPluginContext"/> and forwards everything else, so the
    /// test is the load context, not timing. Shared assemblies and a native
    /// group's managed companions go to the default context and stay visible to
    /// BricsCAD, which is what they are for. The wrapper lands at the end of the
    /// invocation list rather than BricsCAD's original slot; nothing depends on
    /// the order of AssemblyLoad subscribers.
    /// </para>
    /// <para>
    /// This composes with the identical suppressor in DevReload: each wrapper
    /// forwards to the handler it removed, and the second to install finds the
    /// first one's wrapper in place of BricsCAD's handler and refuses (it cannot
    /// prove the scan is the one it unsubscribed), which the caller reports.
    /// </para>
    /// <para>
    /// COPIED from DevReload's <c>DevReload.BricsCadScanSuppressor</c> (DevReload
    /// dd05a34). The two copies are maintained by hand: a fix to one belongs in
    /// the other too.
    /// </para>
    /// <para>
    /// Depends on two private members: <c>AssemblyLoader.OnLoad</c> and the
    /// runtime's <c>AssemblyLoadContext.AssemblyLoad</c> event field (read to
    /// prove BricsCAD's handler is really subscribed before removing it). If
    /// either is gone, <see cref="Install"/> throws rather than quietly leaving
    /// the scan on, and the caller reports it.
    /// </para>
    /// </remarks>
    internal static class HostScanSuppressor
    {
        private const string LoaderType = "Bricscad.ApplicationServices.AssemblyLoader";
        private const string LoaderMethod = "OnLoad";

        private static AssemblyLoadEventHandler? _installed;
        private static AssemblyLoadEventHandler? _original;

        /// <summary>
        /// True once the scan is suppressed. While this is false BricsCAD still
        /// registers commands and calls Initialize on its own instance, so
        /// NSLOAD must not call Initialize a second time.
        /// </summary>
        internal static bool IsActive => _installed != null;

        internal static void Install()
        {
            if (_installed != null) return;

            AssemblyLoadEventHandler original = BricsCadHandler();
            if (!IsSubscribed(original))
                throw new InvalidOperationException(
                    $"{LoaderType}.{LoaderMethod} is not subscribed to AppDomain.AssemblyLoad. " +
                    "This BricsCAD version scans assemblies some other way, or another " +
                    "loader has already taken the scan over.");

            AssemblyLoadEventHandler filtered = (sender, e) =>
            {
                if (AssemblyLoadContext.GetLoadContext(e.LoadedAssembly)
                        is IsolatedPluginContext)
                    return;

                original(sender, e);
            };

            AppDomain.CurrentDomain.AssemblyLoad -= original;
            AppDomain.CurrentDomain.AssemblyLoad += filtered;
            _original = original;
            _installed = filtered;
        }

        internal static void Restore()
        {
            if (_installed == null) return;

            AppDomain.CurrentDomain.AssemblyLoad -= _installed;
            AppDomain.CurrentDomain.AssemblyLoad += _original;
            _installed = null;
            _original = null;
        }

        private static AssemblyLoadEventHandler BricsCadHandler()
        {
            Type? loader = typeof(Application).Assembly.GetType(LoaderType);
            MethodInfo? onLoad = loader?.GetMethod(
                LoaderMethod, BindingFlags.NonPublic | BindingFlags.Static,
                new[] { typeof(object), typeof(AssemblyLoadEventArgs) });

            if (onLoad == null)
                throw new InvalidOperationException(
                    $"{LoaderType}.{LoaderMethod} is gone. This BricsCAD version " +
                    "does not expose the assembly-scan hook NSLOAD suppresses.");

            return (AssemblyLoadEventHandler)Delegate.CreateDelegate(
                typeof(AssemblyLoadEventHandler), onLoad);
        }

        // AppDomain.AssemblyLoad forwards to this static event on .NET 8.
        private static bool IsSubscribed(AssemblyLoadEventHandler handler)
        {
            FieldInfo? field = typeof(AssemblyLoadContext).GetField(
                "AssemblyLoad", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
                throw new InvalidOperationException(
                    "AssemblyLoadContext.AssemblyLoad field not found on this runtime.");

            var current = field.GetValue(null) as Delegate;
            return current?.GetInvocationList().Contains(handler) == true;
        }
    }
}
