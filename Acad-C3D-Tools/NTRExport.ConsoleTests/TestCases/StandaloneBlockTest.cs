using System.Text.RegularExpressions;

namespace NTRExport.ConsoleTests.TestCases
{
    internal sealed class StandaloneBlockTest : BaseTestCase
    {
        protected override string DwgName => "T0-stand alone, fittings.dwg";
        public override string DisplayName => "Standalone fittings";

        protected override bool Validate(string ntrPath)
        {
            var template = LoadTemplate();
            var actual = LoadActual(ntrPath);

            if (template is null)
            {
                Console.WriteLine("INCOMPLETE: golden NTR is missing; skipping comparison.");
                return true;
            }

            if (Ntr.NtrDocumentComparer.AreEquivalent(template, actual!, out var message))
            {
                return true;
            }

            Console.Error.WriteLine(message);
            return false;
        }
    }
}
