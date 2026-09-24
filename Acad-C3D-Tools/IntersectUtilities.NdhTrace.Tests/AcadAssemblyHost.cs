using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace IntersectUtilities.NdhTrace.Tests;

/// <summary>
/// THE ONE THING A TEST HOST OWES AutoCAD's MANAGED API: a place to find it.
///
/// acdbmgd.dll and accoremgd.dll are referenced with <c>Private=False</c> (they
/// must never be copied beside a plugin), so nothing puts them in a test
/// project's output and the CLR raises
/// <c>FileNotFoundException: Could not load file or assembly 'Acdbmgd'</c> the
/// first time a test touches <c>Point2d</c>. They are NOT, however, unloadable
/// outside acad.exe: with the install directory on both the managed probing
/// path and the native DLL search path, <c>Polyline</c> constructs, measures and
/// projects in a plain <c>dotnet test</c> process. Everything
/// <see cref="NdhRouteBuilder"/> asks of a centreline is in-memory geometry and
/// needs no host application, no Database and no document.
///
/// What still needs acad.exe is anything that reaches for the editor, a
/// document, a resident Database or the application services - so tests stay on
/// the geometry side of that line.
/// </summary>
internal static class AcadAssemblyHost
{
    private const string AcadDir = @"C:\Program Files\Autodesk\AutoCAD 2025";

    [ModuleInitializer]
    internal static void Install()
    {
        //The managed assemblies are mixed-mode: their native halves (acdb25.dll
        //and friends) are loaded by the OS loader, which only looks beside the
        //process unless the install directory is added to the search path.
        AddDllDirectory(AcadDir);
        SetDefaultDllDirectories(
            LoadLibrarySearchApplicationDir | LoadLibrarySearchUserDirs |
            LoadLibrarySearchSystem32 | LoadLibrarySearchDefaultDirs);

        AssemblyLoadContext.Default.Resolving += (context, name) => Find(context, name);
    }

    private static Assembly? Find(AssemblyLoadContext context, AssemblyName name)
    {
        foreach (string folder in new[] { AcadDir, Path.Combine(AcadDir, "C3D") })
        {
            string candidate = Path.Combine(folder, name.Name + ".dll");
            if (File.Exists(candidate)) return context.LoadFromAssemblyPath(candidate);
        }
        return null;
    }

    private const uint LoadLibrarySearchApplicationDir = 0x00000200;
    private const uint LoadLibrarySearchUserDirs = 0x00000400;
    private const uint LoadLibrarySearchSystem32 = 0x00000800;
    private const uint LoadLibrarySearchDefaultDirs = 0x00001000;

    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint AddDllDirectory(string newDirectory);

    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);
}
