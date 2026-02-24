namespace DimensioneringV2.UI
{
    public class ForbrugerRow
    {
        public string Adresse { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public double BBRAreal { get; set; }
        public double Effekt { get; set; }
        public double Aarsforbrug { get; set; }
        public double Stiklaengde { get; set; }
        public string DN { get; set; } = string.Empty;
        public double Tryktab { get; set; }
    }
}
