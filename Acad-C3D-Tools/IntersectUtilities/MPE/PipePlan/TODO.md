# PipePlan TODO

## Metadata Drift

- Handle polylines whose geometry was changed after bake.
- Solve the case where deleting a vertex breaks `PPEDIT` or `PPDRAW Continue`.
- Define what deleting `C` means in a control path like `A B C D E`.
- Add PipePlan-owned delete commands so geometry and metadata stay in sync.

## Edit Snapping

- Think through snapping behavior when editing inner vertices and inner segments, for example vertex `C` or segment `BC` in `A B C D E`.
