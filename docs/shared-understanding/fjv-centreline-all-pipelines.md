<fjv-centreline-all-pipelines>

Shared understanding for extending DRAWBONDEDCL (bonded only) to ALL FJV
pipelines. Agreed with the owner 2026-09-11 via revdiff review.

Purpose: the command makes a Centreline polyline that a drafter traces with
NDHPIPE (NorsynDrawingTools). A Centreline must therefore be continuous where
one NDHPIPE Pipeline would be continuous, including across a PipeType change.

</fjv-centreline-all-pipelines>

<decisions>

- Command renamed DRAWBONDEDCL -> DRAWFJVCL. The old name is removed.
- ONE output layer for every Centreline: `0-FJV-CL` (twin and bonded).
- The helper layer `0-FJV-CL-RUN` stays for now. It holds the chained runs
  that are not a Centreline (frem/retur runs, and runs that could not be
  typed).
- Twin Centreline = twin pipes chained through their components, same rule as
  the bonded runs: continuous through components, straight through at tees, a
  branch starts a new polyline.
- Bonded Centreline = the trimmed bisector of frem and retur (unchanged).
- The Centreline logic moves out of the command class into its own module
  (`PlanDetailing/FjvCentreline.cs`). The command only does I/O.
- The chord fallback for a component without an internal port-to-port path
  stays as it is (reported, not silent).

</decisions>

<transitions>

A transition (Overgang) is a component whose FJV component Type is `Y-Model`,
`F-Model` or `H-Model`. It is the Composition boundary between twin and bonded.

- Its internal geometry does NOT join runs. The twin run stops at the twin
  port; the frem and retur runs stop at their bonded ports.
  Why: the F block's spine stub is not on a port path, so its inner node had
  only 2 edges and the chaining turned one bonded run onto the spine; with
  twin pipes in the graph that run would continue into the twin pipe.
- The bisector ends where the pair stops being side by side (the corridor
  filter). This is correct for a bisector; the gap is bridged by a join.
- Join rule: extend the twin Centreline end and the bonded Centreline end
  along their end tangents and join at the intersection (the same miter rule
  as the bisector stitch step).
  - Y-rør: collinear tangents -> straight join through the Y.
  - F-model: tangents at 90 degrees -> corner where the bonded centre axis
    crosses the twin spine, which is the NDHPIPE F-rør corner
    (NorsynDrawingTools `f-model-transition.md`).
- The joined result is ONE polyline from twin through the transition into
  bonded.
- A join that does not meet within a sane reach is NOT made. The transition is
  reported by handle. No silent fallback.

</transitions>

<not-in-scope>

- Bonded branch Centrelines do not reach the main Centreline at a bonded tee
  (existing behaviour, unchanged).
- Old `0-FJV-CL-ENKELT` geometry from earlier DRAWBONDEDCL runs is not erased
  by the new command.

</not-in-scope>
