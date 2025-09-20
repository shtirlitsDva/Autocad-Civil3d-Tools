using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using IntersectUtilities.UtilsCommon;

namespace IntersectUtilities.PlanProduction
{
    internal struct ViewFrameDrawing
    {
        private static Regex regex = new Regex(@"^(?<number>\d{2,3})\s?(?<name>.*)?");
        public int Number { get; }
        public string VejNavn { get; }
        public string FileName { get; }
        public ViewFrameDrawing(string name, string fileName)
        {
            var match = regex.Match(name);
            if (!match.Success) throw new ArgumentException($"Invalid name format: {name}!");

            Number = int.Parse(match.Groups["number"].Value);
            string rawName = match.Groups["name"].Value;
            VejNavn = rawName.IsNoE() ? name : rawName;
            
            FileName = fileName;
        }
    }
}
