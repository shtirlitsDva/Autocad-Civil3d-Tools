<research-question>
Can we build a native ObjectARX custom object that fits the projection-label requirements
better than a Civil label (vertical text + true 4-point non-crossing leader), and can it be
COUPLED to the Civil projection to read location + description? If live coupling isn't
desirable, can we instead cache the data at creation and provide tools to update it?
</research-question>

<correction-notice>
An earlier version of this doc wrongly claimed custom entities can be created in pure .NET.
That is FALSE. Verified online (primary sources below): custom objects/entities can only be
created in C++ ObjectARX. .NET cannot define a custom object class.
</correction-notice>

<finding-0-no-custom-objects-in-dotnet>
VERIFIED FALSE that .NET can create custom objects. Evidence:
- Kean Walmsley (Autodesk), Through the Interface: "We don't have plans to expose the ability
  to define custom objects to .NET." Reasoning: the inherent complexity of custom objects is
  not lessened by exposing the C++ mechanism to .NET, and "the vast majority of use cases are
  handled by the Overrule API." Alternatives he gives: implement the object in C++ and expose
  COM/.NET interfaces, or use Overrule (which also avoids locking data in binary blobs that add
  an object-enabler dependency).
  https://keanw.com/2006/09/custom_objects_.html
- Autodesk forum consensus: "There is no option to create custom entities with C#, you have to
  use C++ and the ObjectARX kit ... you can't make custom objects via .NET." Workaround: write
  the custom entity in C++ ARX and add a managed wrapper, OR use Overruling.
  https://forums.autodesk.com/t5/net-forum/how-can-i-create-custom-entity-with-c/td-p/10204119

Conclusion: a TRUE custom object = C++ ObjectARX only.
</finding-0-no-custom-objects-in-dotnet>

<finding-1-cpp-arx-custom-entity-is-feasible>
In C++ ObjectARX a custom entity is the right tool and meets the requirements:
- Derive from `AcDbEntity`; override `subWorldDraw` (draw any polyline leader + text — full
  control over the 4-point non-crossing leader and vertical text), `subGetGeomExtents`,
  `subTransformBy`, optional grips (`subGetGripPoints`/`subMoveGripPointsAt`), and
  `dwgInFields`/`dwgOutFields` for persistence.
- Coupling to the Civil projection (both are C++ ARX mechanisms):
  - Store the source object's id as a soft pointer in `dwgOutFields`.
  - Attach a persistent reactor (soft pointer; saved in DWG, re-established on load) to get
    notified when the source changes/erases — to refresh the cache.
- Data path CONFIRMED live in the drawing: `ProfileProjectionLabel.FeatureId` →
  `CogoPoint`; `CogoPoint.RawDescription`/`FullDescription` = e.g. "K: 51,18, Mischwasser DN 400";
  projected location = the label's un-dragged `LabelLocation` (or computed via the ProfileView).
Source: ObjectARX Dev Guide (Deriving from AcDbEntity, subWorldDraw, Notification / object
references — hard vs soft pointers, persistent reactors).
</finding-1-cpp-arx-custom-entity-is-feasible>

<finding-2-integration-cost-of-cpp-in-a-dotnet-codebase>
This codebase (IntersectUtilities et al.) is .NET. Adding a C++ ARX module is a real cost:
- A separate C++ project + the ObjectARX SDK, rebuilt per AutoCAD major version.
- A managed wrapper (C++/CLI or COM) so the .NET tools can create/drive the object.
- DevReload hot-reload does NOT apply to C++ ARX — slower iteration.
- Portability: opened without our ARX module → proxy object (needs saved proxy graphics or an
  object enabler).
</finding-2-integration-cost-of-cpp-in-a-dotnet-codebase>

<finding-3-dotnet-native-alternative-overrule>
If we want to stay in .NET, the supported route (per Kean above) is the Overrule API:
- A `DrawableOverrule` (filtered by a predicate) overrides `WorldDraw` of a STANDARD host
  entity to draw our vertical text + 4-point leader. No new class is defined.
- Host = a lightweight standard entity we create per label (e.g. a `DBPoint` or a
  `BlockReference`) carrying the cached data (description, anchor, slot/text position, the 4
  leader vertices, source handle) in XData / an extension-dictionary Xrecord.
- Pros: pure .NET, hot-reloadable, no object-enabler dependency; without our plugin the host
  still shows as a plain point/block (not a broken proxy).
- Cons: an Overrule modifies behaviour of an existing entity TYPE at runtime (must be installed
  on load and filtered carefully); the "object" is a carrier+XData rather than a first-class
  custom class.
</finding-3-dotnet-native-alternative-overrule>

<finding-4-simplest-plain-geometry>
Simplest of all: a .NET command generates plain `MText` (vertical) + a `Polyline` 4-point
leader per label, grouped/owned in a per-view block. Fully portable, no proxy, trivial. Cost:
not a single selectable data-carrying object; "update" = erase + regenerate.
</finding-4-simplest-plain-geometry>

<options-summary>
| Option | Language | Custom drawn leader | Data carried | Hot-reload | Portability (no plugin) | Effort |
|---|---|---|---|---|---|---|
| C++ ARX custom entity (+ wrapper) | C++ + wrapper | yes | in object (DwgFields) | no | proxy (needs enabler/saved gfx) | high |
| .NET DrawableOverrule on carrier | .NET | yes | XData/Xrecord on carrier | yes | host shows as plain point/block | medium |
| .NET plain MText + Polyline | .NET | yes (static) | none (or XData) | yes | fully portable | low |
</options-summary>

<recommendation>
Decision is the user's. Trade-off framing:
- If a first-class custom OBJECT (carrying data, single selectable, optional grips) is a hard
  requirement → it MUST be C++ ObjectARX (+ managed wrapper). That is exactly the "objectarx
  native component" the user asked about; it is feasible and couples to Civil via reactor +
  CogoPoint.RawDescription.
- If the real goal is the VISUAL result (vertical text + 4-point non-crossing leaders) with the
  least new machinery in a .NET codebase → a `DrawableOverrule` on a carrier entity (cached
  data in XData, an UPDATE command to resync) gets there in pure .NET, hot-reloadable.
- Cached-at-creation + UPDATE command is the better data model than live reactors either way
  (these are plot-output annotations; YAGNI on auto-refresh).
</recommendation>
