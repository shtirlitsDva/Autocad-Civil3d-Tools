using Mapsui;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.UI.Wpf;
using Mapsui.Widgets;

using System;
using System.Collections.Generic;

namespace DimensioneringV2.UI
{
    internal sealed class MapWidgetManager
    {
        private readonly Dictionary<Type, IWidget> _widgets = new();

        internal MapWidgetManager(Map map, MapControl mapControl)
        {
            Map = map;
            MapControl = mapControl;
        }

        private Map Map { get; }
        private MapControl MapControl { get; }

        internal T Register<T>(T widget, ISkiaWidgetRenderer renderer) where T : IWidget
        {
            var type = typeof(T);
            if (_widgets.ContainsKey(type))
                throw new InvalidOperationException($"Widget {type.Name} already registered.");

            _widgets[type] = widget;
            Map.Widgets.Enqueue(widget);
            MapControl.Renderer.WidgetRenders[type] = renderer;
            return widget;
        }

        internal T Get<T>() where T : IWidget
        {
            return (T)_widgets[typeof(T)];
        }
    }
}
