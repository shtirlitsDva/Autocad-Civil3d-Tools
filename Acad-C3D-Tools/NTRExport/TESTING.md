# NTR Routing Test Plan (AutoCAD Core Console)

## Goals
- Headless integration tests via AutoCAD Core Console (accoreconsole.exe)
- Validate DWG → Topology → Routing (macros) → NTR output
- Gradually add routing macros with corresponding sample drawings and assertions

## Prerequisites
- AutoCAD Core Console installed. Set environment variable `ACCORE_PATH` to the full path of `accoreconsole.exe`.
- Built `NTRExport.dll` (Debug/Release). Tests will `NETLOAD` this DLL.
- Sample DWG assets under `NTRExport.ConsoleTests/Assets/` (not committed if proprietary).

## How tests run
1. Copy a sample DWG into a temp folder
2. Generate a script `.scr` that:
   - NETLOADs `NTRExport.dll`
   - Runs `NTREXPORT`
   - QUITs AutoCAD
3. Launch `accoreconsole.exe` with `/i temp.dwg /s script.scr`
4. Read the produced `.ntr` next to the DWG
5. Assert presence/structure of key records (RO/BOG/RED/TEE) and REF/LAST tokens

## Initial Test Matrix (grows over time)
- Preinsulated elbow (90°)
  - Expect 3-part expansion: RO → BOG → RO around elbow
  - Validate `REF=` tokens and `DN=DNxx.s`
- Twin reducer spacing
  - Expect `RED` plus additional RO/BOG elements if spacing delta requires S-bends
- Tee branching
  - Expect `TEE` with correct `DNH`/`DNA` and suffixes; optional re-spacing straights/bends nearby
- F/Y transition (bonded ↔ twin)
  - Expect multi-piece expansion for split/merge

## Assertions
- File exists and non-empty
- Regex checks for record presence/counts
- Optional golden diff (normalize whitespace)

## Local run
- Build solution (ensure `NTRExport.dll` present)
- Set `ACCORE_PATH`
- From `NTRExport.ConsoleTests` directory, run the test runner (Debug/Release) with no args to execute all tests.

## Notes
- Tests skip gracefully if a required DWG asset is missing (printed as SKIPPED)
- Console logs from AutoCAD are captured for diagnostics
