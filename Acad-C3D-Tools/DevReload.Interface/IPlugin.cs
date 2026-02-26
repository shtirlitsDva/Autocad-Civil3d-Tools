namespace DevReload
{
    public interface IPlugin
    {
        void Initialize();
        object CreatePaletteSet();
        void Terminate();
    }
}
