using System;

namespace DimensioneringV2.UI.MapProperty
{
    public enum DisplayCategoryEnum
    {
        Grunddata,
        Beregningsforudsætninger,
        Rørtype,
        Hydraulik,
        Tryktab
    }

    [AttributeUsage(AttributeTargets.Property)]
    class DisplayCategoryAttribute : Attribute
    {
        public DisplayCategoryEnum Category;
        public DisplayCategoryAttribute(DisplayCategoryEnum category)
        {
            Category = category;
        }
    }
}
