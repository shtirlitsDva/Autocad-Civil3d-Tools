using DevReload;

namespace DevReloadTest
{
    public class TestPlugin : IPlugin
    {
        public void Initialize()
        {
            // Subscribe to events or perform one-time setup here
        }

        public object CreatePaletteSet()
        {
            return new TestPaletteSet();
        }

        public void Terminate()
        {
            // Unsubscribe from events or cleanup here
        }
    }
}
