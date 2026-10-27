using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

#if BRICSCAD
using Bricscad.ApplicationServices;
using Bricscad.Internal;
using Teigha.Runtime;
#else
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;
#endif

namespace NSLOAD
{
    public class CommandRegistrar
    {
        private readonly List<RegisteredCommand> _commands = new();

        private record RegisteredCommand(
            string Group, string GlobalName, CommandCallback Callback, CommandInvoker Invoker);

        // What the CAD program calls for one command. After RemoveCommand,
        // BricsCAD keeps one removed callback per command group alive for good,
        // so the callback's target is this NSLOAD object and not a closure over
        // the plugin's method: Release swaps the plugin's code for a no-op, and
        // the callback BricsCAD keeps then holds nothing of the plugin's.
        private sealed class CommandInvoker
        {
            private static readonly Action Released = () => { };
            private Action _invoke;

            public CommandInvoker(Action invoke) => _invoke = invoke;

            public void Run()
            {
                try { _invoke(); }
                catch (System.Exception ex) { ReportException(ex); }
            }

            public void Release() => _invoke = Released;
        }

        public int CommandCount => _commands.Count;

        public void RegisterFromAssembly(Assembly assembly, string? defaultGroupName = null)
        {
            defaultGroupName ??= assembly.GetName().Name ?? "PLUGIN";

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

                        Action invoke;
                        if (method.IsStatic)
                        {
                            var m = method;
                            invoke = () => m.Invoke(null, null);
                        }
                        else
                        {
                            var t = type;
                            var m = method;
                            invoke = () =>
                            {
                                var instance = Activator.CreateInstance(t);
                                m.Invoke(instance, null);
                            };
                        }

                        var invoker = new CommandInvoker(invoke);
                        CommandCallback callback = invoker.Run;
                        Utils.AddCommand(group, globalName, localName, flags, callback);
                        _commands.Add(new RegisteredCommand(group, globalName, callback, invoker));
                    }
                }
            }
        }

        private static void ReportException(System.Exception ex)
        {
            var inner = ex is TargetInvocationException tie ? tie.InnerException ?? ex : ex;
            try
            {
                var doc = Application.DocumentManager.MdiActiveDocument;
                doc?.Editor.WriteMessage("\n" + inner.ToString() + "\n");
            }
            catch { }
        }

        public void UnregisterAll()
        {
            foreach (var cmd in _commands)
            {
                Utils.RemoveCommand(cmd.Group, cmd.GlobalName);
                cmd.Invoker.Release();
            }
            _commands.Clear();
        }
    }
}
