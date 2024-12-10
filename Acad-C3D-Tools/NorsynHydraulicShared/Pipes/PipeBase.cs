using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace NorsynHydraulicCalc.Pipes
{
    public abstract class PipeBase : IPipe
    {
        abstract protected string Name { get; }
        abstract protected PipeType PipeType { get; }
        protected double Roughness_m { get => _roughness_m; }
        private double _roughness_m;
        abstract protected string DimName { get; }
        abstract protected int OrderingPriority { get; }
        abstract protected double PricePerStk { get; }
        private protected Dictionary<int, Dim>? Sizes { get; private set; }
        private PipeBase() { }
        public PipeBase(double roughness_mm)
        {
            _roughness_m = roughness_mm / 1000;
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
        internal IEnumerable<Dim> GetAllDimsSorted()
        {
            return Sizes
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => kvp.Value);
        }
        public Dim GetDim(int dia) => Sizes[dia];
        private void LoadDimsFromEmbeddedResource()
        {
            Sizes = new Dictionary<int, Dim>();

            // Load the dictionary from an embedded resource
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();

            var resourceName =
                assembly.GetManifestResourceNames()
                .Where(x => x.EndsWith(".Pipes.Sizes." + Name + "Sizes.csv"))
                .FirstOrDefault();

            if (resourceName == null)
                throw new Exception("Resource not found: " + resourceName);

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

                    var rgb = new int[3]
                    {
                        int.Parse(parts[5]), // R
                        int.Parse(parts[6]), // G
                        int.Parse(parts[7])  // B
                    };

                    var dim = new Dim(
                        int.Parse(parts[0]), // Nominal Diameter
                        double.Parse(parts[1], CultureInfo.InvariantCulture), // Outer Diameter
                        double.Parse(parts[3], CultureInfo.InvariantCulture), // Inner Diameter
                        double.Parse(parts[2], CultureInfo.InvariantCulture), // Wall Thickness
                        double.Parse(parts[4], CultureInfo.InvariantCulture), // Cross Section Area
                        Roughness_m,
                        DimName,
                        PipeType,
                        OrderingPriority,
                        rgb,
                        double.Parse(parts[8], CultureInfo.InvariantCulture), // Price per meter
                        PricePerStk // Price per piece
                    );

                    Sizes.Add(dim.NominalDiameter, dim);
                }
            }
        }
    }
}
