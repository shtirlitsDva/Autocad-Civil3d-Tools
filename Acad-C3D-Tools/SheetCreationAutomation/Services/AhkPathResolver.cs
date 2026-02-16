using System.IO;
using System.Linq;

namespace SheetCreationAutomation.Services
{
    internal static class AhkPathResolver
    {
        private static readonly string[] CandidateRoots =
        {
            @"X:\AutoCAD DRI - 01 Civil 3D\AHK",
            @"J:\Norsyn\AutoCAD DRI - 01 Civil 3D\AHK"
        };

        public static string? ResolveAhkRoot()
        {
            return CandidateRoots.FirstOrDefault(Directory.Exists);
        }
    }
}
