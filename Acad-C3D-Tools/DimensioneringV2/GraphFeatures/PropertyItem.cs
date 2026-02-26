namespace DimensioneringV2.GraphFeatures
{
    internal class PropertyItem
    {
        public string Name { get; }
        public string? Value { get; }
        public string Category { get; }
        public int CategoryOrder { get; }
        public PropertyItem(string name, string? value, string category = "", int categoryOrder = 0)
        {
            Name = name;
            Value = value;
            Category = category;
            CategoryOrder = categoryOrder;
        }
    }
}
