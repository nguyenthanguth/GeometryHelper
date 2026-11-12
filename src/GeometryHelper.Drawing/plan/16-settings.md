# L6: settings

There are two kinds of settings, kept apart:

| | Global settings (A9) | Drawing settings (presets) |
|---|---|---|
| Apply to | the whole app, every drawing on the machine | one drawing, when it is made and when it is updated |
| Examples | canvas background, selection colour, fonts, units, shortcuts, how DXF is exported | which views, which scales, dimension rules, what marks show |
| Where in the UI | the Global settings dialog (ribbon › Settings, command `OPTIONS`) | the Drawing settings dialog, and the preset chosen when a drawing is made |
| Stored | a JSON file of the user's (D13) | in the drawing document (L7); presets are C# objects (D5) |
| After a change | **Apply**: the UI changes at once; **OK**: saved; **Cancel**: dropped; **Reset to defaults** | **Apply**: the drawing is made again, hand edits kept |

## Global settings

The dialog is in the UI design (nine categories). Part A owns the shared ones; Drawing defaults, Data sources, the DXF layout part of Save and export, the hidden lines colour and the dimension decimals belong to Part B: since 2026-11-12 the presenter shows none of them (they stay in `GlobalSettings`, in the JSON file), and the Tekla drawing app shows them in its own settings. Its categories and defaults:

| Category | Settings, for example | Default |
|---|---|---|
| Appearance | Dark/Light mode; **canvas background**; paper colour; visible lines on a dark background; hidden lines; selected objects; grips | Dark; **canvas #000000 (black)**; white paper; white visible lines; hidden lines #9AA4B1; selection and grips #3D9BFF |
| Canvas and cursor | crosshair size, pickbox, grid, show lineweights, zoom speed | crosshair 5 %, pickbox 5 px, grid off, lineweights on |
| Drawing defaults | the preset used for a new drawing, sheet size, projection method (D9) | Assembly preset, A2, third angle |
| Units and rounding | unit (mm), decimal places, thousands separator | mm, 0 places for dimensions, a space |
| Layers, colours and lines | name, colour and linetype of each layer written (L5) | as [15-output-dxf.md](15-output-dxf.md) |
| Text and fonts | UI font, drawing text font and heights | Inter (D11) for the UI; drawing text settled in G0 |
| Data sources | connected sources (Tekla, IFC), checking for model changes | checking on |
| Shortcuts | command aliases and shortcut keys (A12) | AutoCAD's, see [50-ui.md](50-ui.md) |
| Save and export | save folder, DXF export layout (D10), DXF version (D8) | layout and viewports; AC1027 |

Rules:

- every setting has a default in code, so deleting the JSON file returns to the defaults;
- values read in are checked (valid colours, numbers in range); a bad value falls back to the default with a Warn in the
  log, and never stops the app;
- the colour settings of the UI are the design tokens (the canvas background is `color/bg/canvas`), so a change in the
  dialog changes the very resource the UI uses;
- the core (L0 to L7) takes its settings as an object passed in and never reads the file itself, so it runs without a UI.

## Drawing settings

`DrawingSettings` holds everything a drawing may vary, as C# objects (D5). One part per layer:

| Part | Holds |
|---|---|
| `Views` | which views, the sections and details asked for, the margin of a crop, the depth, the smooth angle of L1 |
| `Scales` | the allowed scales, largest first (Q4) |
| `Dimensions` | the rules switched on, the tier spacing, the text height and rounding, whether equal dimensions merge |
| `Marks` | what each kind of mark shows (Q5), its text height, its frame |
| `Sheet` | the sheet size, the title block (Q2), the part list's columns, the revision table |

## Presets

The one kind of drawing (A1) takes its look from a preset:

| Preset | For | Differs in |
|---|---|---|
| `DrawingSettings.Part` | one part | three views and sections of the part, every hole dimensioned, part mark only |
| `DrawingSettings.Assembly` | an assembly | the main part's frame, fittings dimensioned from the main part, part marks of the fittings, part list |
| `DrawingSettings.General` | many assemblies | plans and elevations, assembly marks, grid lines and levels, no part dimensions |

A preset is only a settings object; a drawing made with `Part` from an assembly is valid and draws every object given.
Settings are copied into the document when a drawing is made, so changing a preset afterwards changes nothing already
made.
