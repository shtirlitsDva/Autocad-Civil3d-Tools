using DimensioneringV2.Services;

namespace DimensioneringV2.UI
{
    internal class BaseMapOption
    {
        public BaseMapType Type { get; }
        public string DisplayName { get; }

        public BaseMapOption(BaseMapType type, string displayName)
        {
            Type = type;
            DisplayName = displayName;
        }
    }
}
