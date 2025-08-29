OOP detailing framework overview

- BlockDetailingContext: carries all required services/parameters (alignment, PV window, sampling function, station/offset delegate, helpers).
- IBlockDetailer: handler interface per component group.
- BlockDetailerBase: shared helpers (station/offset, insertion point, attribute setting, source refs).
- GenericComponentDetailer: handles most components (LEFTSIZE/RIGHTSIZE rules).
- BueRorDetailer: handles BUEROR1/BUEROR2 (mid-station, length).
- BlockDetailingOrchestrator: dispatches to the first handler that can process a source block.

Grouping strategy

- Generic components: everything except reductions, welds, bue rør.
- Bue rør: BUEROR1/BUEROR2 special case.
- Future groups can be added by creating new IBlockDetailer implementations and registering them in the orchestrator.


