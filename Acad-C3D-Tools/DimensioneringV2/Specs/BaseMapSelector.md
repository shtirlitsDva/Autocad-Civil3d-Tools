<section>
<overview>

# Feature Spec: Base Map Selector

Adds a dropdown to the DimensioneringV2 map toolbar that lets users switch between
five base map styles: OpenStreetMap, Dark, Ortofoto, Hybrid, and Off.

</overview>
</section>

<section>
<overview>

## Base Map Options

| Option | Source | Notes |
|--------|--------|-------|
| **OpenStreetMap** | Built-in `Mapsui.Tiling.OpenStreetMap.CreateTileLayer()` | Default |
| **Dark** | CartoDB Dark Matter (`basemaps.cartocdn.com/dark_all`) | Free, no API key |
| **Ortofoto** | Datafordeler.dk WMTS (`GeoDanmarkOrto/orto_foraar_webm`) | Credentials required (stubbed) |
| **Hybrid** | Ortofoto + CartoDB `dark_only_labels` overlay | Two layers stacked |
| **Off** | No tiles, black background | `Map.BackColor = Color.Black` |

</overview>
</section>

<section>
<overview>

## Architecture

```
Services/
  BaseMapType.cs            -- Enum: OpenStreetMap, Dark, Ortofoto, Hybrid, Off
  BaseMapLayerFactory.cs    -- Static factory: creates tile layers, manages layer swap

UI/
  BaseMapOption.cs          -- Display wrapper for ComboBox binding
  MainWindowViewModel.cs    -- [Modified] Properties, change handler
  MainWindow.xaml           -- [Modified] ComboBox in top toolbar
```

### Layer Naming
- `"BaseMap"` — primary tile layer
- `"BaseMap_Labels"` — label overlay (Hybrid mode only)

### Layer Ordering (bottom to top)
BaseMap -> BaseMap_Labels -> BBR_Inactive -> BBR_Active -> Features -> Angiv

### Switching Algorithm
1. Remove layers by name predicate
2. Set `Map.BackColor`
3. `Insert(0, newLayer)` — always at index 0

</overview>
</section>

<section>
<overview>

## Tile Sources

### CartoDB Dark Matter
- URL: `https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}.png`
- Subdomains: a, b, c, d
- Attribution: "© OpenStreetMap contributors © CARTO"
- Free, no authentication

### Datafordeler.dk Ortofoto (Web Mercator WMTS)
- KVP URL: `https://services.datafordeler.dk/GeoDanmarkOrto/orto_foraar_webm/1.0.0/WMTS`
  `?username=...&password=...&SERVICE=WMTS&REQUEST=GetTile&VERSION=1.0.0`
  `&STYLE=default&FORMAT=image/jpeg&TILEMATRIXSET=DFD_GoogleMapsCompatible`
  `&TILEMATRIX={z}&TILEROW={y}&TILECOL={x}&Layer=orto_foraar_webm`
- Tile Matrix Set: `DFD_GoogleMapsCompatible` (Google Maps / OSM compatible)
- Authentication: username + password query parameters
- Credentials: stubbed, to be configured later

### CartoDB Labels Only (for Hybrid)
- URL: `https://{s}.basemaps.cartocdn.com/dark_only_labels/{z}/{x}/{y}.png`
- Light text on transparent background

</overview>
</section>

<section>
<overview>

## Technical Notes

- Uses `HttpTileSource` + `GlobalSphericalMercator` from BruTile (same pattern as existing commented Stadia Maps code)
- Mapsui 4.1.9 `LayerCollection` supports `Insert(int index, ...)` and `Remove(Func<ILayer,bool>)`
- No new NuGet packages needed
- Dropdown is functional before data load (base map is independent of features)
- If WMTS tile matrix IDs are non-numeric, fallback to `BruTile.Wmts.WmtsParser`

</overview>
</section>
