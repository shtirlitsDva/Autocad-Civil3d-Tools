using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    internal abstract class PipeBase : IPipe
    {
        abstract protected string Name { get; }
        private protected Dictionary<int, Dim> Sizes { get; }
        public PipeBase()
        {
            LoadDimsFromEmbeddedResource();
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
                        double.Parse(parts[4], CultureInfo.InvariantCulture)
                    );

                    dict.Add(dim.NominalDiameter, dim);
                }
            }
        }
    }
}
