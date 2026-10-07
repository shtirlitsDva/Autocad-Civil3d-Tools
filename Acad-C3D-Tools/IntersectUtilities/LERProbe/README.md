# LERPROBE

Reload IntersectUtilities, run **LERPROBE**, and click a polyline inside the LER
xref. The read-only window shows its attached AEC property sets without opening
the source drawing. Lightweight, legacy 2D and 3D polylines are accepted.

The window is **modeless**: click the drawing to pan, zoom or navigate while it
stays open. Its table has Property set, Property and Value. Search filters those
three fields. Select rows and press **Ctrl+C** to copy with headings. Close,
the title-bar X or Escape while the window has focus closes it.

The complete selected polyline is drawn as a temporary topmost overlay in
**bright RGB red (255,0,0)**. If its displayed colour is red, including red shades,
the overlay uses **bright RGB blue (0,0,255)**. Displayed colours account for
ByLayer, ByBlock, layer-zero inheritance and host xref-layer colours. The red
classification uses a hue within 20 degrees of red and saturation above 15%.

The overlay follows the selected xref instance's transform. Circular arcs are
retained; a nonuniformly scaled arc is drawn as an exact native ellipse.
Closing the window clears its overlay. Re-running LERPROBE replaces the previous
window for that drawing. Switching drawings hides the overlay until its owner
is active again. Closing its drawing or unloading the plugin clears the session.
Existing native selection/highlight state is left untouched.

The header identifies xref, file, layer and handle. All attached property sets
are included, including hidden properties. Duplicate names are identified by
their set. Automatic properties evaluate in the source-object context. Missing
values/sets are explicitly reported; displayed numbers use the current culture.

All source objects are opened with `OpenMode.ForRead`. The window holds detached
preview geometry and a data snapshot; it keeps no transaction or document lock
open after the command returns. No entities, sets or values are saved to either
the host drawing or the xref.

## Validation

On 2026-10-07, 26 native Civil 3D 2025 cases passed for path/instance resolution,
RGB and ACI reds, dark red, magenta, ByLayer/ByBlock/layer-zero colours, translated
and scaled arcs, nonuniform arc-to-ellipse conversion, unchanged source geometry,
transient show/hide/disposal, a visible nonmodal window, pan/zoom with it open,
Close button cleanup, replacing a previous probe and plugin reset.

Read-only live checks on `LER_2D_7.24.9` additionally verified a blue
`Vandledning_L2` polyline receiving red, and a red `EL-Forsyningskabel-10kV`
polyline receiving blue. Both source entities remained read-only.

The property-set reader previously passed 11 native fixtures (nested xref,
multiple sets, duplicate names, Danish text, numbers, automatic length, 3D
polylines, empty sets and rejected host/non-polyline selections), and live
reads of 26 Elledning and 24 Vandledning properties.

`LerProbeModelessRegression.csx` loads the current crawl models/reader and probe
models/window/graphic builder/highlight/session as script declarations. Strip
file-scoped namespaces and put imports first. It uses a disposable database and
transient graphics. Its modeless window is closed and view changes are restored.
`LerProbeRegression.csx` covers metadata. Full mouse-wheel/drag navigation can
be checked in the drawing after reload.

API evidence: reflection against the installed 2025 assemblies verified
`Application.ShowModelessDialog(Form)`, document lifecycle events, transient
Add/Erase, RGB colours, transformed copies and ellipse conversion. Those APIs
were exercised in the running Civil 3D process.
