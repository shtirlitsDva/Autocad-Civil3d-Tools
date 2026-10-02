<ppdraw-bonded-pairs>

Shared understanding for drawing BONDED (enkelt) steel pipes with PPDRAW by
drawing the middle axis. Agreed with the owner 2026-10-02 via a grilling
session.

Purpose: a bonded pair is two polylines (frem and retur). The drafter draws
ONE centreline; PipePlan fillets it and offsets the two pipes from it, so the
inner pipe of every bend sits exactly on the design bending radius.

</ppdraw-bonded-pairs>

<scope>

- PPDRAW enters pair mode when the active layer is a STEEL `FJV-FREM-DNxx` or
  `FJV-RETUR-DNxx` (set by NSPalette). FREM and RETUR mean the same thing: the
  drafter always draws the centreline.
- AluPex Frem/Retur are unchanged: they still draw one polyline each.
- In scope: PPDRAW new and Continue, PPEDIT (move, Add, Delete, Radius, Flip),
  Tangent, PPCOLLAPSE, PPSETTINGS (min x column).
- Elastic bending only, exactly like twin. No components: elbows, buerør and
  Y/F/H transitions are placed by hand or by NDHPIPE. No Straight mode.

</scope>

<geometry>

- The centreline control points are filleted by PipePlanSolver at
  R_inner + c/2; frem and retur are the parallel offsets at ± c/2 (bulges
  are kept, so the arcs stay concentric). The offset routine moves out of
  PipePlanDE into PipePlan and both features share it.
- R_inner is the PPSETTINGS design radius ⌈round(R_min) · 1.2⌉ of
  "Stål Enkelt" for the DN (DN100 → 69 m). Every radius the drafter types or
  reads (Radius keyword, PPEDIT Radius, Add, R labels) is the INNER pipe
  radius. No lower floor, as for twin.
- c-c = kOd(DN, series) + min x.
  - min x is a per-DN PPSETTINGS value for Stål Enkelt. Its default is
    NorsynDrawingTools' default: 300 mm (DN <= 150), 400 mm (DN <= 450),
    450 mm above. Overrides are stored in PipePlan's own NOD dictionary.
  - It is NOT synced with NDH. NDH keeps its value in a closed binary
    ObjectARX object (NOD/NORSYN/SETTINGS); reading it from .NET was rejected
    as too fragile. A later NDH export may replace the default.
- Series at draw time: NSPalette's current series, S2 when it cannot be read.
  The pipe width is kOd of that series.
- Every re-solve (PPEDIT, Continue, PPCOLLAPSE) recomputes c-c from the
  members' current width and the current min x. A change is reported
  ("Afstand 525 → 550 mm").
- Frem is LEFT of the drawing direction, retur right (same as NDHPIPE and
  PDDRAW). Flip swaps them while drawing; it is disabled during Continue;
  PPEDIT has a Flip keyword that swaps the pair's layers.

</geometry>

<entities>

- Three polylines per run:
  - centreline on `0-FJV-PP-CL`: non-plottable, dashed, colour 6, width 0.
    Not `0-FJV-CL`: DRAWFJVCL erases every polyline there.
  - frem on `FJV-FREM-DNxx`, retur on `FJV-RETUR-DNxx`, width kOd. A missing
    layer is created the way NSPalette creates it.
- All three carry the SAME authoring data (control points, inner radii, flip,
  the c-c used, system/DN) and one shared run token. One writer, one
  transaction, one undo step.
- Membership: a polyline is a member only when its token matches AND its
  geometry matches the authoring data. A member that is missing or fails the
  check is rebuilt from the valid ones and its handle is reported. With no
  valid member left the command refuses and reports the handles.
- Rigid transforms: when all three members match the authoring data under ONE
  shared transform (move, rotate, mirror), the next PipePlan touch adopts it.
  A mirror inverts Flip. After a COPY the touched run gets a new token. Fewer
  than three members under a non-identity transform are never adopted; they
  are plain polylines (a single copied pipe must not grow into a pair).

</entities>

<ux>

- Preview: the centreline carries the status colour (green straights, dark
  green arcs, blue snap, red infeasible), the R labels (inner radius) and the
  fillet markers. Frem red / retur blue at full kOd width, about 55 %
  transparent. Infeasible: centreline and both pipes red, sharp mitered.
- Prompt: `Næste punkt [Radius/Default/Tangent (off)/Flip]`; Flip from the
  first point.
- Status: `Stål Enkelt DN100 · S2 · c-c 525 mm · R 69 m (inderrør) · Frem
  venstre`.
- PPEDIT: picking any member highlights all three; handles sit on the
  centreline control polygon.
- Tangent: bonded snaps to bonded only, any DN; hovering any member marks the
  centreline end. Twin still snaps to twin only.
- PPCOLLAPSE: the sagitta is measured on the centreline.
- PPSETTINGS: same table, a `min x [mm]` column editable on Stål Enkelt rows
  only, and a read-only `c-c [mm]` column for NSPalette's current series.

</ux>

<not-in-scope>

- PPCONVERT and auto-convert for pairs (parked). Picking an unmanaged
  FREM/RETUR polyline in PPEDIT / Continue / PPCOLLAPSE gives a message.
- Twin ↔ bonded transitions, reducers inside a pair, components.
- In-situ bending below DN100 is ignored, as for twin: the elastic radius
  applies at every DN.

</not-in-scope>

<testing>

- The membership and transform cases (COPY of one pipe, COPY of all three,
  erase a member, grip-edit a member, MOVE, ROTATE, MIRROR) are tested in the
  AutoCAD instance `acd-mcp-110484`.

</testing>
