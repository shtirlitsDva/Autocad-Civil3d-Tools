<feature>
NorsynDistrictZones — per-area price & length breakdown export (Excel).
</feature>

<goal>
Let the user export, to a single Excel workbook, a per-zone breakdown of pipe
lengths and prices. One styled table per zone (area), stacked down one worksheet,
each with a zone subtotal, ending in a grand total. Replaces nothing — it is a new
read-only report alongside NDZEXPORTACAD (geometry/GeoJSON).
</goal>

<single-pricing-source-principle>
The breakdown MUST be computed by the same pricing path that produces the model-space
zone label (PriceCalculator). The bug fixed just before this feature was two pricing
paths disagreeing; a report that sums independently would re-create it. Therefore
PriceCalculator.PriceZone is extended to RETAIN its per-(NHS type, DN) line detail, and
ZonePrice.Total becomes the sum of those lines. Label, GeoJSON, and this export all read
the one ZonePrice. The workbook's grand total is identical to the sum of the zone labels
(verified target: 219.847.448 on the Kronenstrasse drawing).
</single-pricing-source-principle>

<columns>
Exactly as in the user's reference screenshot (Danish headers):
- Rørdimension   — friendly dimension label, e.g. "AluPEX 040", "DN 050"
- Længde [m]     — total clipped length inside the zone; da-DK, 2 decimals (1.531,53)
- Rørpris [kr]   — total pipe cost = Σ(PricePerMeter × length); da-DK N0 (8.446.373)
- Antal stik     — count of Stikledning pipes in the group (integer)
- Stikpris [kr]  — total fitting cost = Σ PricePerFitting over those stik; da-DK N0
- I alt [kr]     — Rørpris + Stikpris; da-DK N0; BOLD

No unit-price columns (no DKK/m, no DKK/stik). Because every money column is a TOTAL,
collapsing FL/SL into one system-level row stays additive and exact.
</columns>

<row-grouping>
One row per (display-system, DN), sorted by display name then DN ascending.
Display name collapses the NHS FL/SL axis: AluPEXFL and AluPEXSL both render as
"AluPEX" (a Fællesstikledning is an SL-typed pipe shown under "AluPEX"). The pricing
key remains the authoritative NHS type+DN; the collapse is DISPLAY ONLY and never
feeds a price. Antal stik counts only the Stikledning-role pipes within the group.
</row-grouping>

<friendly-names>
NDZ-local map NHS pipe type → display system name (NDZ has no PipeScheduleV2 reference,
and PipeScheduleV2.GetSizePrefix returns "Ø"/"DN", not "AluPEX"). Confirmed by screenshot:
Stål → "DN", AluPEXFL/SL → "AluPEX". Best-guess (NEEDS USER CONFIRMATION) for the rest:
PertFlextraFL/SL → "PertFlextra", Kobber → "Cu", AquaTherm11 → "AquaTherm", Pe → "PE",
FibreFlexFL/SL → "FibreFlex". Label format: `{name} {DN:000}` (zero-padded to 3 digits).
</friendly-names>

<workbook-layout>
One worksheet. Per zone, top-to-bottom: a header band carrying the zone number+name,
the column header row, the data rows, then a zone subtotal row. A blank spacer separates
zones. After the last zone, a grand-total row. Tables are styled (borders, header fill,
banded rows). The header band uses the zone's own ColorArgb fill (the same color shown in
model space), so the report is visually keyed to the drawing. Money N0, length 2-dp, all
da-DK. Header text bold; "I alt" column bold.
</workbook-layout>

<modules>
- PriceCalculator (Pricing): + ZoneLine record, + ZonePrice.Lines; PriceZone builds lines,
  Total = Σ lines. Existing consumers (label, GeoJSON) untouched (new field appended).
- PipeDisplayName (Pricing, new): NHS type → friendly system name; Label(type, dn).
- PriceBreakdown (Pricing, new): ZonePrice → ordered display rows (group Lines by
  display-system+DN). Pure, testable, no AutoCAD.
- PriceBreakdownWorkbook (Acad/Export, new): ClosedXML writer. Input = ordered list of
  (zone number, name, ColorArgb, rows, subtotal) + grand total + catalog name; output =
  saved .xlsx. No AutoCAD dependency (pure data in).
- NdzCommands: + NDZEXPORTPRISER — SaveFileDialog, read pipes once, price every zone via
  ZoneService.PriceFace, project to rows, hand to the workbook writer.
- NorsynDistrictZones.csproj: + ClosedXML PackageReference (NOT Office Interop).
</modules>

<scope-decisions>
- All zones in the drawing (matches NDEXPORTACAD), not a selection.
- Excel only (no CSV side-car) for v1.
- Provisional/missing data: a zone with unstamped pipes (AnyProvisional) or a catalog
  miss is still listed; such conditions are surfaced on the AutoCAD command line at price
  time (existing behaviour) — TBD whether to also annotate the workbook. Flagged, not built.
</scope-decisions>

<open-questions>
- Friendly names for non-AluPEX/DN systems (see friendly-names) — confirm strings.
- Should a provisional/incomplete zone be visually flagged in the workbook, or is the
  command-line warning enough?
- Command name NDZEXPORTPRISER acceptable?
</open-questions>
