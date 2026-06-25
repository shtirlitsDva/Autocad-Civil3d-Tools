# PipeScheduleV2 ↔ NorsynHydraulicShared — Translation Research

<purpose>
Standalone record of the research into how the two pipe-classification systems relate, why translating
between them loses information, and what a central translator must capture. Produced for the Norsyn
District Zones (NDZ) project. Source-verified against the three repos on 2026-06-25.
</purpose>

<the-two-systems>

<nhs-norsynhydraulicshared>
Used for **hydraulics and pricing**. Shared-source (net8.0), pure POCO.
- `PipeType` enum — `DimensioneringV2/src/NorsynHydraulicShared/Enums.cs`:
  `Stål, PertFlextraFL, PertFlextraSL, AquaTherm11, AluPEXFL, AluPEXSL, Kobber, Pe, FibreFlexSL, FibreFlexFL`
- **The FL/SL distinction is encoded in the enum name** (FL = Fordelingsledning / distribution,
  SL = Stikledning / service line). This matters because pricing differs:
  - distribution: `Length × Price_m`
  - service line: `Length × Price_m + Price_stk` (per-fitting surcharge)
- `Dim` struct carries `Price_m` (from embedded CSVs) and `Price_stk` (hardcoded per type).
</nhs-norsynhydraulicshared>

<psv2-pipeschedulev2>
Used for **AutoCAD layer naming / drawing**. One big class.
- `PipeScheduleV2.cs` — `Autocad-Civil3d-Tools/Acad-C3D-Tools/IntersectUtilities/PipeScheduleV2/PipeScheduleV2.cs`
- Enums in `Autocad-Civil3d-Tools/Acad-C3D-Tools/UtilitiesCommonSHARED/Enums.cs`:
  - `PipeSystemEnum`: `Ukendt, Stål, Kobberflex, AluPex, PertFlextra, PertPIPE, AquaTherm11, PE, FibreFlex`
  - `PipeTypeEnum`: `Ukendt, Twin, Frem, Retur, Enkelt`
  - `PipeSeriesEnum`: `S1, S2, S3, Undefined`
- **Supply/return lives in `PipeTypeEnum` (Frem/Retur/Twin), NOT FL/SL.** PSv2 has **no concept of
  distribution-vs-service** at all.
</psv2-pipeschedulev2>

</the-two-systems>

<the-current-translation>

Exactly one translation exists, and it is one-way (NHS → PSv2), private to the DimV2 export:
`DimensioneringV2/src/DimensioneringV2.AutoCAD/Services/Drawing/AutoCadDrawingSink.cs`,
method `TranslatePipeTypeToSystem(PipeType)`:

```
Stål            → PipeSystemEnum.Stål
PertFlextraFL   ┐
PertFlextraSL   ┴→ PipeSystemEnum.PertFlextra      ← FL/SL collapsed
AluPEXFL        ┐
AluPEXSL        ┴→ PipeSystemEnum.AluPex           ← FL/SL collapsed
Kobber          → PipeSystemEnum.Kobberflex
AquaTherm11     → PipeSystemEnum.AquaTherm11
Pe              → PipeSystemEnum.PE
FibreFlexFL     ┐
FibreFlexSL     ┴→ PipeSystemEnum.FibreFlex        ← FL/SL collapsed
```

The export then derives the layer with:
`PipeScheduleV2.GetPipeTypeByAvailability(system, dn)` (picks Twin if available, else Frem) and builds
`FJV-<PipeTypeEnum>-<SystemString><DN>`. The `Dim.Family.Name` string (e.g. `"PertFlextraFL"`) is just
`PipeType.ToString()`, re-parsed via `Enum.Parse<PipeType>` at export time.

</the-current-translation>

<information-loss>

The translation is **lossy and not round-trippable**:

1. **FL/SL collapse** (the critical one): `PertFlextraFL`/`SL`, `AluPEXFL`/`SL`, `FibreFlexFL`/`SL` each map
   to a single `PipeSystemEnum`. Once exported, the layer name cannot tell distribution from service.
2. **No reverse map exists** anywhere (PSv2 → NHS). Recovering NHS `PipeType` from a layer name is
   impossible for the collapsed families without external FL/SL guidance.
3. **Historically masked, now exposed**: FL and SL used to be different pipe *types*, so the system implied
   the distinction. New pipe types are identical for FL and SL, so the layer name no longer carries it.

**Consequence for NDZ pricing**: service-line `Price_stk` cannot be applied reliably, because we cannot
identify service lines from the exported drawing. → tracked as the project's critical blocker.

</information-loss>

<all-known-mapping-tables>

Every pipe-type lookup table found (each is a single-owner candidate to centralise):

| Where | Mapping |
|---|---|
| `AutoCadDrawingSink.cs` `TranslatePipeTypeToSystem` | NHS `PipeType` → PSv2 `PipeSystemEnum` (lossy) |
| `PipeScheduleV2.cs` `systemDict` | CSV token (`DN/ALUPEX/CU/PRTFLEXL/PRTPIPE/AQTHRM11/PE/FIBREFLEX`) → `PipeSystemEnum` |
| `PipeScheduleV2.cs` `systemDictReversed` | `PipeSystemEnum` → CSV token |
| `PipeScheduleV2.cs` `lineTypePrefixDict` | `PipeSystemEnum` → linetype prefix (`ST/AP/CU/PRT/PRTP/AT/PE/FF`) |
| `PipeScheduleV2.cs` `_sizePrefixes` | `PipeSystemEnum` → size prefix (`DN` vs `Ø`) |
| `PipeScheduleV2.cs` `availableStdLengths` | `PipeSystemEnum` → standard coil lengths |
| `PipeRegistry.cs` (DimV2.Hydraulics) | NHS `PipeType` ↔ domain `PipeFamily` (name = `PipeType.ToString()`) |

The layer-name parser regex (`FJV-(?<TYPE>…)-(?<DATATYPE>…)(?<DN>\d+)`) lives in `PipeScheduleV2.cs`.

</all-known-mapping-tables>

<full-correspondence-needed>

A complete, round-trippable mapping must store the FL/SL bit explicitly (it cannot be derived):

| NHS PipeType | Segment (FL/SL) | PSv2 PipeSystemEnum | PSv2 PipeTypeEnum (by DN/availability) |
|---|---|---|---|
| Stål | n/a | Stål | Twin if available else Frem |
| PertFlextraFL | FL | PertFlextra | per availability |
| PertFlextraSL | SL | PertFlextra | per availability |
| AluPEXFL | FL | AluPex | per availability |
| AluPEXSL | SL | AluPex | per availability |
| Kobber | n/a | Kobberflex | per availability |
| AquaTherm11 | n/a | AquaTherm11 | per availability |
| Pe | n/a | PE | per availability |
| FibreFlexFL | FL | FibreFlex | per availability |
| FibreFlexSL | SL | FibreFlex | per availability |

</full-correspondence-needed>

<dependency-and-build-notes>

- **NHS** is shared-source, net8.0, pure POCO — easily referenced by any net8.0(-windows) project.
- **PSv2** is a single `.cs` file currently consumed by **linking the source** (`<Compile Include>`),
  e.g. DimV2.AutoCAD links it directly. Its enums live in `UtilitiesCommonSHARED` (also shared via
  projitems). This linking is why there's "one copy" today but it is fragile for cross-system logic.
- Frameworks: IntersectUtilities = net8.0-windows10.0.26100; NHS/DimV2 domain = net8.0; NorsynObjects = C++.
- A deeper examination of whether PSv2 can be split into a pure core (enums + schedule + classification)
  vs an AutoCAD adapter — and where the single shared translator should live — is the subject of a
  follow-up architecture study (in progress); its recommendation will be folded into the NDZ plan.

</dependency-and-build-notes>

<bottom-line>
Translating NHS↔PSv2 is straightforward at the *material* level but **irreversibly drops FL/SL** today.
A central translator must (1) own the single mapping (no duplicated switch), (2) keep FL/SL as stored
data rather than deriving it, and (3) be the home for the eventual service-line-discrimination fix.
</bottom-line>
