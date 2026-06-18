<graphviz-alternatives-research>

Research target: replace the Graphviz rendering layer used by `GRAPHWRITEV2`
(`Acad-C3D-Tools/IntersectUtilities/GraphWriteV2/GraphWriteV2Command.cs`) with
something that allows **richer node visuals** than Graphviz HTML-like labels,
while keeping the product: an HTML page with clickable
`ahk://ACCOMSelectByHandle/{handle}` links + the QA error table, plus a PDF as a
secondary static visual.

<hard-requirements>
1. Clickable `ahk://...` links in the **HTML** view. An AutoHotKey listener
   catches the click and selects the entity back in AutoCAD. This is the primary,
   interactive artifact — the one people actually use.
2. A QA error table (already HTML — trivially preserved by anything HTML-based).
3. Richer node content than Graphviz HTML-like labels (which only support a tiny
   subset: <table>, <font>, <b>/<i>, <img>, <hr> — no CSS, no rounded corners,
   no gradients, no real flexbox/grid layout).

EXPLICITLY NOT required: clickable links in the PDF. Making links clickable in a
PDF is genuinely hard and nobody asks for it — the PDF exists only as a static
picture for people who still want a PDF the old way. It does NOT need to be
interactive. This single relaxation removes the biggest porting risk and opens
the field wide.
</hard-requirements>

<the-real-decision-axis>
Graphviz fuses two concerns that every modern tool separates:

  LAYOUT ENGINE  -> computes node x/y positions + edge routes
  RENDERER       -> draws nodes/edges to SVG/HTML/PDF

"Richer visuals" is a RENDERER property, delivered by SVG <foreignObject>, which
embeds arbitrary HTML+CSS inside a node. Graphviz cannot do this; every SVG/HTML
renderer can. So the question is really: which (layout engine, renderer) pair?

With the PDF-link requirement gone, the clean architecture is ONE render, not
two: produce a single rich HTML page (links live here), then print THAT SAME page
to PDF with headless Edge for the static artifact:

  msedge.exe --headless=new --print-to-pdf="C:\Temp\MyGraph.pdf" "C:\Temp\MyGraph.html"

Edge is already installed and its path is already in the codebase
(GraphWriteV2Command.cs:300), so the PDF half costs almost nothing. The PDF
inherits whatever richness the HTML has; it simply won't be clickable — which is
exactly what was asked. This also fixes a latent bug in today's code: it renders
TWICE (`dot -Tpdf` AND `dot -Tsvg`), so the PDF and HTML are laid out
independently and can drift; print-the-HTML makes them identical by construction.
</the-real-decision-axis>

<candidate-1-d2>
D2 (Terrastruct) — https://github.com/terrastruct/d2 , https://d2lang.com

What it is: a modern declarative diagram language (text -> diagram), spiritually
the closest "drop-in for DOT": we keep generating a text description, D2 lays it
out and renders it. Single Go binary CLI — same shell-out model we already use
for `dot`, so the smallest change to the command's structure.

Fit against requirements:
- Layout: pluggable engines — Dagre (default, free), ELK (free), TALA (paid).
  ELK gives cleaner orthogonal routing and fewer crossings than `dot`.
- Richer nodes: built-in shape/style vocabulary far beyond Graphviz (rounded
  corners, shadows, fills, gradients, multiple shape classes, sql_table and
  class shapes), PLUS Markdown text blocks rendered as real HTML foreignObject.
  Much nicer than Graphviz, though NOT "arbitrary HTML per node".
- Themes: ships official dark themes — could replace the current CSS invert()
  hack with a real dark palette.
- Links: native `link` attribute on shapes -> clickable `<a>` in the SVG/HTML.
- PDF: native PDF export, OR just headless-print the SVG/HTML. Either way no
  link concern now.

Verdict: lowest-effort, highest-architectural-similarity option, and the
relaxed constraint removes its only real question mark (PDF link survival no
longer matters). Best first spike.
</candidate-1-d2>

<candidate-2-d3-plus-elkjs>
D3.js + ELK.js (or Dagre) layout, rendered to SVG with HTML <foreignObject>.

What it is: ELK.js (or Dagre) takes a JSON graph and returns node coordinates;
we render each node as an SVG element whose label is a <foreignObject> holding
arbitrary HTML+CSS. Wrap each node in `<a href="ahk://...">` for the HTML click.
PDF = headless-Edge print of that same page.

Fit against requirements:
- Richer nodes: MAXIMUM. Anything CSS can do — flexbox, grid, status pills for QA
  codes, badges, gradients, rounded corners, icons — lives in the node. This is
  the ceiling for "rich visual experience".
- Layout: ELK.js is the most capable free hierarchical engine (2.7M weekly npm
  downloads, actively maintained; fewer edge crossings + orthogonal routing than
  `dot`). Dagre is the simpler near-drop-in for DOT-style layered layouts.
- Links: native `<a>` in HTML — clickable in the browser view. PDF inherits the
  visuals, non-clickable (fine).
- PDF: headless-Edge print of the one HTML page; HTML and PDF identical.

Cost / risks:
- Most engineering effort: we own the rendering code (node templates, edge
  drawing, the SVG/HTML scaffold). ELK.js is powerful but complex to configure
  (keep the Java ELK API docs handy).
- Adds a JS toolchain / Node step to the build-and-run pipeline.

Verdict: the option that actually maximizes "rich visual experience". Highest
ceiling, highest cost. Choose if D2's fixed vocabulary proves too limiting.
</candidate-2-d3-plus-elkjs>

<candidate-3-cytoscape-js>
Cytoscape.js — https://js.cytoscape.org

What it is: a graph-specialized JS library with strong INTERACTIVITY (pan/zoom,
selection, hover, expand/collapse, layouts incl. dagre/elk via extensions) and
its own declarative stylesheet. Now much more attractive because the HTML view is
the primary artifact and the PDF can be second-class.

Fit against requirements:
- Richer nodes: GOOD in the interactive HTML. Core nodes render to <canvas>,
  styled via Cytoscape's own property set (shapes, images, compound/parent nodes)
  — richer than Graphviz, and the `cytoscape-node-html-label` extension overlays
  arbitrary HTML on nodes for fully custom content.
- Links: clicks are JS event handlers — wire a node click to navigate to
  `ahk://ACCOMSelectByHandle/{handle}` (or run the AHK trigger). Works great in
  HTML; this is the model Cytoscape is built for.
- Interactivity bonus: free pan/zoom/search/highlight that Graphviz SVG never
  had — a genuinely better review experience for big networks.

Cost / risks:
- The PDF is the weak spot: canvas export (PNG/JPG, or SVG via `cytoscape-svg`)
  does NOT include the `node-html-label` overlay, so the PDF would show simpler
  nodes than the HTML. Acceptable under the new "PDF is a static old-timer
  artifact" framing, but worth stating plainly.
- Two render models (interactive canvas for HTML, separate export for PDF) — less
  unified than the print-the-HTML approach of candidates 1 and 2.

Verdict: best choice if you want the HTML view to become genuinely INTERACTIVE.
Ranked third only because the PDF is less faithful, which the new constraints
make tolerable.
</candidate-3-cytoscape-js>

<honorable-mentions>
- ELK.js standalone (layout only) + our own minimal SVG renderer: same engine as
  candidate 2 without D3; viable if we want layout-quality wins with less library
  surface.
- Mermaid: easy and popular, but styling is LESS flexible than D2 and not richer
  than Graphviz HTML labels in practice. Does not advance the goal — reject.
- yFiles for HTML (commercial) / GoJS (commercial): the richest node templating
  AND first-class layout, least custom code — at a licensing cost. Quote only if
  the open-source spikes disappoint.
</honorable-mentions>

<comparison-table>
| Option            | Node richness      | Layout quality | Links in HTML | Interactive HTML | PDF (static)        | Effort | Cost |
|-------------------|--------------------|----------------|---------------|------------------|---------------------|--------|------|
| Graphviz (today)  | Low (HTML labels)  | OK (dot)       | Yes           | No               | Yes (dot -Tpdf)     | —      | Free |
| D2                | Medium-High        | High (ELK)     | Yes (SVG)     | No (static SVG)  | Yes (native/print)  | Low    | Free |
| D3 + ELK.js       | MAX (HTML/CSS)     | High (ELK)     | Yes           | Optional         | Yes (headless print)| High   | Free |
| Cytoscape.js      | High (interactive) | High (ext.)    | Yes (JS)      | YES (native)     | Simpler nodes       | Medium | Free |
| yFiles / GoJS     | MAX                | High           | Yes           | Yes              | Yes (built-in)      | Low    | $$$  |
</comparison-table>

<recommendation>
The PDF-link constraint is gone, so pick by how INTERACTIVE you want the HTML:

A. Want a better-looking but still STATIC diagram, smallest change to the command:
   -> D2 (candidate 1). Keep the shell-out architecture, swap `dot` for `d2`, get
      ELK layout + dark themes + markdown nodes. Print the SVG/HTML to PDF.
      Lowest effort, big visual upgrade. Recommended FIRST spike.

B. Want the maximum static node richness (arbitrary HTML/CSS per node):
   -> D3 + ELK.js + foreignObject (candidate 2). Highest ceiling, highest cost.
      Escalate here only if D2's fixed vocabulary is too limiting.

C. Want the HTML to become genuinely INTERACTIVE (pan/zoom/search/highlight,
   click-to-select):
   -> Cytoscape.js (candidate 3). Accept a simpler-looking PDF.

Suggested path: spike D2 first (a day at most). If its styling satisfies the
"richer visuals" goal, ship it — it's the least disruptive to the existing
command. If you find yourself wanting per-node custom HTML or interactivity,
escalate to B or C respectively.
</recommendation>

<sources>
- https://alternativeto.net/software/graphviz/
- https://gist.github.com/jaime-olivares/bcd578c263943f5679720f56f6c40101
- https://npmtrends.com/dagre-layout-vs-diagram-js-vs-elkjs-vs-graphviz
- https://terrastruct.com/blog/post/diagram-layout-engines-crossing-minimization/index.html
- https://github.com/terrastruct/d2
- https://d2lang.com/
- https://deepwiki.com/terrastruct/d2-docs/3.5-layout-engines-and-export-system
- https://js.cytoscape.org/
- https://github.com/kaluginserg/cytoscape-node-html-label
- https://blog.tomsawyer.com/python-graph-visualization-libraries
</sources>

</graphviz-alternatives-research>
