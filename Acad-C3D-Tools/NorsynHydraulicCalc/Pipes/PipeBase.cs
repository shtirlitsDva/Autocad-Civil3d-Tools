using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal abstract class PipeBase : IPipe
    {
        abstract protected string Name { get; }
        abstract protected PipeType PipeType { get; }
        abstract protected double Roughness_m { get; }
        abstract protected string DimName { get; }
        private protected Dictionary<int, Dim> Sizes { get; }
        public PipeBase()
        {
            LoadDimsFromEmbeddedResource();
        }
        internal IEnumerable<Dim> GetDimsRange(int minNS, int maxNS)
        {
            //Up to the coder to get the right keys
            return Sizes
                .Where(kvp => kvp.Key >= minNS && kvp.Key <= maxNS)
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value);
        }
        private void LoadDimsFromEmbeddedResource()
        {
            var dict = new Dictionary<int, Dim>();

            // Load the dictionary from an embedded resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var resourceName = "NorsynHydraulicCalc.Pipes.Sizes." + Name + "Sizes.csv";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new System.IO.StreamReader(stream))
            {
                string line;
                bool headerSkipped = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!headerSkipped)
                    {
                        headerSkipped = true;
                        continue;
                    }

                    var parts = line.Split(';');

                    var dim = new Dim(
                        int.Parse(parts[0]),
                        double.Parse(parts[1], CultureInfo.InvariantCulture),
                        double.Parse(parts[2], CultureInfo.InvariantCulture),
                        double.Parse(parts[3], CultureInfo.InvariantCulture),
                        double.Parse(parts[4], CultureInfo.InvariantCulture),
                        Roughness_m,
                        DimName
                    );

                    dict.Add(dim.NominalDiameter, dim);
                }
            }
        }
    }
}
