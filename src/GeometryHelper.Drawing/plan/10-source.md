# L0: the source

What a drawing is made from. Nothing here knows Tekla; the bridge fills it ([17-tekla-bridge.md](17-tekla-bridge.md)),
and tests build it in code.

## DrawingObject

| Member | What it holds |
|---|---|
| `Id` | a key unique in the source, stable from one run to the next (Tekla's GUID, or one given in code) |
| `Kind` | `DrawingObjectKind`: Part, Bolt, Hole, Weld, Rebar, and later others |
| `Solid` | the `GeoSolid3` drawn, in model millimetres; a hole may carry its axis and diameter instead |
| `Frame` | the object's own `GeoCoordinateSystem3` (for a Tekla part, its coordinate system: x along its length) |
| `Name`, `Mark`, `Profile`, `Material` | the texts marks and part lists show |
| `ParentId` | the assembly the object belongs to, if any |
| `IsMain` | the main part of its assembly, whose frame a drawing takes by default |
| `Attributes` | further named values for marks, the title block and the part list (string to string, ordered by name) |

Bolts and holes carry what their symbols and dimensions need: the axis, the diameter, the bolt group they belong to and
its pattern. Welds carry their type, size, the parts they join and their path. The exact members are fixed in G0 against
what Tekla's Open API gives.

## DrawingSource

- the objects, in a fixed order (by `Id`, so that the output never follows the order the model gave them in);
- the drawing's frame: by default the main object's frame, or one given;
- the texts of the title block that come from the model (project, drawing number, revision).

## Checks

- Every solid must be closed and valid (`GeoSolid3.Validate`). An object that is not is drawn as it is, logged once with
  its id, and counted in the drawing's report, never dropped in silence.
- Ids are unique; a repeated id is refused through `Guard`.
