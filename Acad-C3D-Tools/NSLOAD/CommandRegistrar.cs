using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autodesk.AutoCAD.Internal;
using Autodesk.AutoCAD.Runtime;

namespace NSLOAD
{
    public class CommandRegistrar
    {
        private readonly List<RegisteredCommand> _commands = new();

        private record RegisteredCommand(
            string Group, string GlobalName, CommandCallback Callback);

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

        public void UnregisterAll()
        {
            foreach (var cmd in _commands)
                Utils.RemoveCommand(cmd.Group, cmd.GlobalName);
            _commands.Clear();
        }
    }
}
