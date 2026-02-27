using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

namespace DevReload
{
    /// <summary>
    /// Registers [CommandMethod]s from a loaded assembly via Utils.AddCommand
    /// and unregisters them via Utils.RemoveCommand before ALC unload.
    ///
    /// IMPORTANT: Core assemblies must include:
    ///   [assembly: CommandClass(typeof(SomeEmptyClass))]
    /// to suppress AutoCAD's built-in ExtensionLoader from auto-registering
    /// commands via CommandClass.AddCommand (a separate registry that
    /// Utils.RemoveCommand cannot clean up).
    /// </summary>
    public class CommandRegistrar
    {
        private readonly List<RegisteredCommand> _commands = new();

        private record RegisteredCommand(
            string Group, string GlobalName, CommandCallback Callback);

        public int CommandCount => _commands.Count;

        /// <summary>
        /// Scan assembly for [CommandMethod] attributes and register each
        /// with AutoCAD via Utils.AddCommand.
        /// Always scans ALL exported types — ignores [assembly: CommandClass]
        /// (that attribute is only there to suppress AutoCAD's ExtensionLoader).
        /// </summary>
        public void RegisterFromAssembly(Assembly assembly, string? defaultGroupName = null)
        {
            defaultGroupName ??= assembly.GetName().Name ?? "PLUGIN";

            // Always scan all exported types.
            // Do NOT use [assembly: CommandClass] filtering here —
            // that attribute exists only to block AutoCAD's auto-registration.
            Type[] typesToScan = assembly.GetExportedTypes();

            foreach (Type type in typesToScan)
            {
                foreach (MethodInfo method in type.GetMethods(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
                {
                    foreach (var attr in method.GetCustomAttributes<CommandMethodAttribute>())
                    {
                        string group = string.IsNullOrEmpty(attr.GroupName)
                            ? defaultGroupName : attr.GroupName;
                        string globalName = attr.GlobalName;
                        string localName = attr.LocalizedNameId ?? globalName;
                        CommandFlags flags = attr.Flags;

                        // Instance methods: create new instance per invocation
                        // (matches AutoCAD's normal behavior for [CommandMethod])
                        CommandCallback callback;
                        if (method.IsStatic)
                        {
                            var m = method;
                            callback = () => m.Invoke(null, null);
                        }
                        else
                        {
                            var t = type;
                            var m = method;
                            callback = () =>
                            {
                                var instance = Activator.CreateInstance(t);
                                m.Invoke(instance, null);
                            };
                        }

                        Utils.AddCommand(group, globalName, localName, flags, callback);
                        _commands.Add(new RegisteredCommand(group, globalName, callback));
                    }
                }
            }
        }

        /// <summary>
        /// Unregister all previously registered commands via Utils.RemoveCommand.
        /// Must be called BEFORE unloading the ALC so the collectible context
        /// can be GC'd (no dangling delegate references).
        /// </summary>
        public void UnregisterAll()
        {
            foreach (var cmd in _commands)
                Utils.RemoveCommand(cmd.Group, cmd.GlobalName);
            _commands.Clear();
        }
    }
}
