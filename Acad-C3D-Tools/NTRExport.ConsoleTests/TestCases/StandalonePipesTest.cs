using System.Text.RegularExpressions;

namespace NTRExport.ConsoleTests.TestCases
{
    internal sealed class StandalonePipesTest : BaseTestCase
    {
        protected override string DwgName => "T0-stand alone, pipes and fittings.dwg";
        public override string DisplayName => "Standalone pipes";

        protected override bool Validate(string ntrPath)
        {
            var template = LoadTemplate();
            var actual = LoadActual(ntrPath);

            if (Ntr.NtrDocumentComparer.AreEquivalent(template, actual, out var message))
            {
                return true;
            }

            Console.Error.WriteLine(message);
            return false;
        }
    }
}
