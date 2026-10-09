# Golden filer streams

Recorded on BricsCAD V26.2 (2026-10-10) with LERImporter's `RecordingFiler` (a file filer)
over property-set objects that **Civil 3D 2025** wrote: the Svogerslev project's
`Svogerslev1_558_3DLER.dwg`, made by the LERImporter release in use before the BricsCAD port.
That release did not yet add GmlBemærkning and LerNummer to a definition, so these
definitions end with the GML properties. One token per line: the filer call and its value. Pointers carry the handle they
pointed at.

| File | Object |
|------|--------|
| `format.txt` | `AecDbScheduleDataFormat` "Standard" |
| `def-<Name>.txt` | `AecDbPropertySetDef` for each LER GML type |
| `set.txt` | one `AecDbPropertySet` (25 values) |

Each file starts with the AcDbObject part as DwgOut files it (owner, reactors, reactor
count); the AEC stream the codec handles starts after the `I32` count.

`set.txt` had its non-empty text values replaced with `value N`, because they came from a
real LER package. The layout, ids, data types and variant types are as Civil wrote them.
