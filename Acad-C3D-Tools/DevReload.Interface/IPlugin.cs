namespace DevReload
{
    public interface IPlugin
    {
        void Terminate();
    }

    public interface IPluginPalette
    {
        object CreatePaletteSet();
    }
}
