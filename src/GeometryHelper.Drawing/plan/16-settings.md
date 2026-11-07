# L6: settings

`DrawingSettings` holds everything a drawing may vary, as C# objects (D5). One part per layer:

| Part | Holds |
|---|---|
| `Views` | which views, the projection method (D9), sections and details asked for, the margin of a crop, the smooth angle of L1 |
| `Scales` | the allowed scales, largest first (Q4) |
| `Dimensions` | the rules switched on, the tier spacing, the text height and rounding, whether equal dimensions merge |
| `Marks` | what each kind of mark shows (Q5), its text height, its frame |
| `Sheet` | the sheet sizes allowed, the title block (Q2), the part list's columns, the revision table |
| `Layers` | the layer names, colours and linetypes of L5 |
| `Text` | the font and the heights |

## Presets

The one kind of drawing (A1) takes its look from a preset:

| Preset | For | Differs in |
|---|---|---|
| `DrawingSettings.Part` | one part | three views and sections of the part, every hole dimensioned, part mark only |
| `DrawingSettings.Assembly` | an assembly | the main part's frame, fittings dimensioned from the main part, part marks of the fittings, part list |
| `DrawingSettings.General` | many assemblies | plans and elevations, assembly marks, grid lines and levels, no part dimensions |

A preset is only a settings object; a drawing made with `Part` from an assembly is valid and draws every object given.

Settings are copied when a drawing is built, so changing them afterwards changes nothing already made.
