# PipePlan TODO

## Metadata Drift

- Handle polylines whose geometry was changed after bake.
- ~~Solve the case where deleting a vertex breaks `PPEDIT` or `PPDRAW Continue`.~~ Done: `PPEDIT` now owns vertex add/remove (the top-level `Add`/`Delete` modes, both with live preview), so geometry and metadata stay in sync. Deleting `C` in `A B C D E` drops the control point and its radius and re-solves the merged `B D` tangent; removing an endpoint forces the promoted neighbour's radius to 0.
- Editing geometry *outside* PipePlan (native AutoCAD grip-stretch / vertex delete) can still desync metadata — use the PipePlan-owned `Insert`/`Delete`/move instead.

## Edit Snapping

- Think through snapping behavior when editing inner vertices and inner segments, for example vertex `C` or segment `BC` in `A B C D E`.
