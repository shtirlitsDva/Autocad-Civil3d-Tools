# NDZ Session Handoff — 2026-06-25

<status>
The Norsyn District Zones (NDZ) plugin work from this session is **COMPLETE and pushed**.
Working tree clean. Nothing is half-finished. This doc exists so a fresh agent can pick up
*new* NDZ work without re-deriving the environment quirks and the one open code smell.
</status>

<what-shipped-this-session>
Four commits on `master`, all on `origin/master`:

| Commit | What |
|---|---|
| `758b7d86` | Robust zone-split via fixed-grid OverlayNG snap-rounding (`Topology/NdzGeometry.cs`) + reactor message on invalid cut |
| `be057745` | `NDZTEXTSIZE` command — global, per-user remembered label height (`GlobalSettings.cs`, `%APPDATA%\Norsyn\NorsynDistrictZones\settings.json`) |
| `338e306b` | Fail-loud pricing — label shows `re-export DIM (no FL/SL data)` instead of a silent-wrong total |
| `5ef64347` | User guide `docs/district-zones-user-guide.md` |

Two earlier user commits (`87038b01` "goinghome", `b5d5f5e5` "not u again") carried the
reflection command announcer (`CommandBanner` + `[CommandSummary]`) and the NDZDEMO removal.

Do **not** re-summarise the contents here — read the commits and the user guide directly.
</what-shipped-this-session>

<authoritative-context-read-these-first>
The full state lives in durable artifacts, not in this doc:

- **Project memory:** `~/.claude/projects/<this-repo>/memory/ndz-project-overview.md`
  — what NDZ is, architecture decisions, full key-files list, build/ALC requirements.
- **Split-bug post-mortem:** `…/memory/ndz-split-precision-bug.md` — root cause
  (sub-µm UTM precision vs. JTS Polygonizer) + the fixed-PrecisionModel OverlayNG fix.
- **Feedback:** `…/memory/feedback_prefer_builtin_precision_over_hacks.md` — prefer
  standard NTS precision over epsilon tricks; sub-mm distortion OK, model stability is the priority.
- **User guide:** `docs/district-zones-user-guide.md` — the end-user procedure (8 commands).
- **The code:** `Acad-C3D-Tools/NorsynDistrictZones/` — file-scoped namespaces, ImplicitUsings.
</authoritative-context-read-these-first>

<environment-quirks-not-written-in-code>
These will bite a fresh agent that doesn't know them:

1. **Two AutoCAD instances.** Work in **your own** Civil instance (start via `acad_start`, pid
   varies). **Never** drive the user's instance. Pass `pid` explicitly to every acd-mcp /
   devreload call — both instances load `Acd.Mcp`, so an omitted pid is ambiguous.
2. **Pause immediately on restart.** If the user says they need to restart Civil, stop all
   acd-mcp / devreload activity at once — a reload mid-restart corrupts the dev loop.
3. **Multi-ALC selection trap.** After `devreload_reload`, multiple stale copies of the
   `NorsynDistrictZones` assembly coexist in-process. Select the live one by
   `cands.FirstOrDefault(a => a.GetType("NorsynDistrictZones.Topology.NdzGeometry") != null)` —
   `FirstOrDefault(name == "NorsynDistrictZones")` may grab a stale copy → NullReferenceException.
4. **Mixed-mode interop must be shared.** `NorsynObjectsInterop` is C++/CLI and CANNOT load
   into an isolated DevReload ALC. The csproj copies it + `Ijwhost.dll` to bin and declares it
   in `SharedAssemblies.Config.json` (shared + mixedMode). All three are required or
   `new NorsynContainer()` throws FileNotFoundException at runtime.
5. **Xref inner-layer prefix.** Pipes inside the attached Xref carry layers prefixed
   `Fjernvarme DIM|FJV-…`. Strip the `|`-prefix before matching `FJV-` (PipeTypeTranslator does this).
6. **Repo is mounted at both** `C:\Users\…\Desktop\GitHub\shtirlitsDva\…` and `X:\GitHub\shtirlitsDva\…`
   (same tree via subst). DevReload references it via `X:`.
</environment-quirks-not-written-in-code>

<dev-loop-entry-points>
- Build + load into running Civil: `devreload_reload(name="NorsynDistrictZones", pid=…)`.
- List instances / pids: `acad_list_instances`.
- Run real commands (fires the CommandEnded reactor): `acad_send_command` / `SendStringToExecute`.
  Command-line coords are locale-independent: `.` = decimal, `,` = separator.
- Run C# against the live drawing: `autocad_script_execute(code, pid=…)` (load `/acd-mcp:script` first).
- Live test drawing used this session: `NDZ_test.dwg`.
</dev-loop-entry-points>

<one-open-code-smell-not-a-task>
The renderer (`Acad/ZoneRenderer.cs`) **re-applies child appearance (layer/color/transparency)
on every rebuild** because the old `NorsynContainer` clone dropped child appearance. The user
mentioned the NorsynContainer interop was since updated so child layer/color now **persist**.
That MAY make the re-apply workaround removable. This was flagged (rule-3) but **NOT requested** —
do not remove it without asking the user and verifying empirically that appearance survives a
save/reload round-trip without the workaround.
</one-open-code-smell-not-a-task>

<deferred-by-user>
DimV2 export-side XData write (phase P12) for FL/SL identity is **done and verified**
(2162/2162 pipes carry `NORSYN_NHS_PIPE`). NDZ already reads it. The fail-loud path
(`re-export DIM (no FL/SL data)`) is the user-chosen behavior for *old* exports that lack it —
not a bug to "fix".
</deferred-by-user>

<suggested-skills>
- **`/acd-mcp:start`** then **`/acd-mcp:script`** — before any live AutoCAD C# probing/editing.
  Load the flavor skill first (hard rule); pass `pid` for the right instance.
- **`/devreload:acd-agentic-dev`** — for the build→load→test dev loop.
- **`/research`** — if touching NTS geometry/precision again; test candidates empirically on the
  live drawing rather than trusting docs (GeometryPrecisionReducer was empirically rejected here).
- **`/revdiff:revdiff`** — to show the user any non-trivial diff/plan for annotation (launch the
  Monitor with `persistent: true`, never a timeout — see the user's review-procedure rules).
</suggested-skills>

<how-the-user-works>
Rookie programmer who wants the "why" explained, pushes for industry best practices, rejects
quick hacks and over-engineering, and wants code smells reported (not silently fixed). Commit /
push only when explicitly asked. Company is **Norsyn** ("DRI" is the old name — never use it in
new identifiers). See the memory files for the full set.
</how-the-user-works>
