# Settings, autosave and the window

What the presenter keeps between runs and how the window behaves: global settings in a JSON file, the copies autosave
leaves beside a drawing, how a drawing is closed, the keys and the mouse, and the options of the standalone app. The
settings are in the core and run in a test; autosave, closing and the window belong to the controls
(`GeometryHelper.Dxf.Viewer.Wpf`), which the API reference does not list, so they are described here and not linked.

## Global settings

[`GlobalSettings`](xref:GeometryHelper.Dxf.Viewer.GlobalSettings) is what applies to the whole app and every drawing on the
machine, kept as JSON in `%AppData%\GeometryHelper\Drawing\settings.json` (`GlobalSettings.DefaultPath`). Every setting has
its default in code, so deleting the file gives the defaults back.

```csharp
GlobalSettings settings = GlobalSettings.Load(GlobalSettings.DefaultPath);   // never throws
settings.AutosaveMinutes = 5;
settings.CrosshairPercent = 20;
settings.Save(GlobalSettings.DefaultPath);                                   // the folder made where it is missing
```

`Load` never throws: no file gives the defaults; a file that does not read gives the defaults, said once in the log; and a
value that is missing keeps its default while one that is not valid falls back to its default with a Warn naming it. A file
of `"autosaveMinutes": 999` gives 10, the range being 0 to 240. A setter refuses a value out of its range, so the dialog can
say why: a wheel zoom of 0.5 would zoom out on the wheel turned in, and `WheelZoomFactor = 0.5` throws "The value has to be
from 1.01 to 4." `Clone()` makes a copy that shares nothing; `ToJson()` gives the text `Save` writes, LF line ends and two
spaces of indent, 3 214 characters for the defaults.

### The categories

The nine categories are those of the Global settings dialog, and of the JSON.

| Category | Holds | Defaults |
|---|---|---|
| Appearance | the colours of the canvas, paper, visible and hidden lines, selection and grips; the crosshair and pickbox; the grid; line weights | canvas `#000000`, paper `#FFFFFF`, hidden lines `#9AA4B1`, selection and grips `#3D9BFF`, crosshair 5 %, pickbox 5 pixels, grid off, line weights shown |
| Canvas and cursor | the wheel zoom, the object snaps, the aperture, what F3 does | 1.2 a notch (1.01 to 4), every snap but Nearest, 10 pixels (2 to 50), F3 toggles them |
| Drawing defaults | the preset, sheet, orientation and projection of a new model drawing | Assembly, A2, Landscape, Third angle |
| Units and rounding | decimals of coordinates, dimensions and angles; the separators | 2, 0 and 1 decimals; thousands parted by a space, decimals by a point |
| Layers, colours and lines | the layer table written to new drawings | Visible, Hidden, Centre, Hatch, Dimension, Mark, Sheet |
| Text and fonts | the fonts, the heights and the width factor of drawing text: TEXT and MTEXT start from the text height and write the width factor, and DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write their text at the text height | UI font Inter, drawing font Arial, text 2.5, titles 5, width factor 0.8 |
| Data sources | whether the sources of a model are asked for changes, and how often | on, every 30 seconds (5 to 3 600) |
| Shortcuts | the aliases and the keys | AutoCAD's (see [Editing](editing.md#aliases-and-keys)) |
| Save and export | the save folder, autosave, backups, the layout of an export | empty, which is the user's Documents (a new model drawing goes into its folder `GeometryHelper Drawing`), 10 minutes, 3 backups, `LayoutAndViewports` |

The defaults are AutoCAD's where it has one. Numbers show as the units say: `FormatNumber(12345.678, 2)` is `12 345.68`, and
with a comma for the thousands `12,345.68`. The thousands cannot be parted by the decimal separator: `ThousandsSeparator =
"Point"` with `DecimalSeparator = "Point"` throws. `SetShortcuts(aliases, keys)` replaces the aliases and keys, and
`ShortcutConflicts(names)` says an alias that is the name of another command ("The alias DU is the name of the command DU");
`ToShortcuts()` gives the [`CadShortcuts`](xref:GeometryHelper.Dxf.Viewer.CadShortcuts) a registry takes.
[`GlobalLayer`](xref:GeometryHelper.Dxf.Viewer.GlobalLayer) is a layer of the default table: name, colour, linetype, line weight and whether it plots.

### What the text settings reach

Once settings are applied, a command line starts TEXT and MTEXT from the text height, and gives them the width factor: TEXT
writes it as the text's width factor, and MTEXT, for a factor other than 1, opens its words with the code AutoCAD writes for it,
`\W0.8000;` for 0.8. The dimension commands DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write the words of the
dimension at the text height, in place of the dimension style's DIMTXT; where that is not the style's, it is kept as an override
of the dimension (as DIMOVERRIDE writes one), so that a CAD that draws the dimension again from its style draws its text as high as
the picture has it. With no settings applied, as in a test, the style's DIMTXT and a width factor of 1 are used.

### Where a new model drawing is saved

A model drawing made in the app is saved at once, once the settings are applied, into the save folder, or where that is empty into
`GeometryHelper Drawing` in the user's Documents, made where it is not there. It is named by its number, `B-101.dxf`, and
`B-101 (2).dxf` where that name is taken, so that no file is written over; characters a file name cannot hold become `_`. It
then has a file, which Save writes to and [autosave](#autosave) keeps copies beside. A folder that cannot be made or written to is
said on the command line, and the drawing stays without a file until Save As.

### In the dialog

The dialog edits a copy of the window's settings, with Reset to defaults, Cancel, Apply and OK. Apply gives them to the
window at once, OK applies and saves them to the file, and Cancel drops them. A value the settings refuse shows why and is not
taken, and a shortcut that clashes shows with whom; Apply and OK wait until every one is put right. Applied at once are the
snaps, the grid, the coordinates of the status bar, and the aliases and keys of every command line open; the canvas takes
the rest.

## Autosave

Every minute the window looks at the clock, and where `AutosaveMinutes` have passed (10 by default, 0 for never) it saves a
copy of each open drawing that has a file and changes not saved, beside the file: `plan.autosave1.dxf` for `plan.dxf`, the
newest the first. `BackupsKept` copies are kept, 3 by default and 0 to 50: the last is deleted and the others move up one.
The user's file itself is never written. A drawing with a command running, or with no file yet, is left for the next time,
and the copies are not listed in the file list.

## Closing a drawing

A drawing is closed by CLOSE on the command line, Ctrl+W, or the x on its row in the file list. A command cannot close its own
document while it runs, so CLOSE raises `CloseRequested` and the window closes the document once the command has ended.

- A command running is ended first, as Esc would end it.
- With changes not saved the window asks "Save changes to plan.dxf?", and Save saves it (asking where, for a drawing with no
  file), Don't save closes it, and Cancel keeps it open. A save that fails keeps it open too.
- The command line of the drawing lets its plugins go, and the extensions are told it is closed.
- The file stays in the file list where it is on disk; the next drawing open is shown, or the start screen where none is left.
- Closing the window asks once, for all the drawings not saved.

## The window

The window is laid out as AutoCAD's own: a ribbon of tabs and panels with the layer box and a properties box, Properties on the
right, the command line and the status bar at the bottom, and the Model and layout tabs at the bottom left of the canvas.
The panel on the left lists the drawing files, and has no layouts or layers. The ribbon has, among its tabs, Annotate and View, a Plugins
tab (see [Command plugins](plugins.md)), a Settings tab, and the tabs a host adds: the model drawing adds its Drawing tab.

### The keys and the mouse

| Key | Does |
|---|---|
| Esc | cancels the command; then clears the selection |
| Space, Enter | ends a command or a prompt; Enter alone runs the last command again |
| Ctrl+O, Ctrl+W | opens a DXF, closes the drawing |
| Ctrl+S, Ctrl+Shift+S | saves, saves as |
| Ctrl+N | asks a host for a new drawing from a model |
| Ctrl+Z, Ctrl+Y | undo, redo |
| Delete | erases the selection |
| F3, F7, F8, F9, F10, F11, F12 | OSNAP, GRID, ORTHO, SNAP, POLAR, OTRACK, DYN |

The function keys, Delete and the Ctrl keys of [the defaults](editing.md#aliases-and-keys) are rows of the Shortcuts category and can be changed.

| Mouse | Does |
|---|---|
| wheel | zooms about the cursor, 1.2 times a notch |
| middle button dragged | pans |
| middle button double-clicked | zooms to the extents, the paper included in a layout |
| left click, no command running | picks within the pickbox; Shift+click adds or removes; a click on nothing starts a box |
| click / double click in DRAWINGS | selects the row / opens it; the right-click menu has Open, Close, Close Selected Drawings, Close All Other Drawings, Close All Drawings |
| Shift+right-click, a point asked | the object snap menu; the snap chosen is taken for that point alone |
| left button dragged, no command running | with Select by dragging on, a box as above; off, the box waits for a click at its other corner (left to right a window in solid blue, right to left a crossing in dashed green) |
| cursor over an entity | drawn 2 pixels wide in its colour taken halfway to the selection colour |
| a grip dragged | drags that grip |
| left click, a command running | answers its prompt: a point taken through the snaps, with a rubber band from the base point; or objects, by a click or a box |
| right click, a command running | Enter |

While MOVE, COPY, ROTATE, SCALE or MIRROR waits for its second point, a dashed ghost of the objects follows the cursor.

## The app

```
GeometryHelper.Drawing.App.exe plan.dxf
```

opens the window on a drawing. The app reads the global settings from the user's file, loads the command plugins of the
plugin folder and the startup list, and adds the model drawing's tab and its sources (see
[Model drawings](../drawing/index.md#the-app)). For a picture of the window in a test or a document:

```
GeometryHelper.Drawing.App.exe --screenshot out.png plan.dxf --size 1600 960
```

draws the window with the drawing open into a PNG, with no window shown, and exits: 0 when written, 2 for arguments or a file
it cannot read. `--space "Layout1"` shows a layout, `--select H1,H2` selects entities, `--run "MOVE;0,0"` types lines on the
command line, parted by semicolons, `--cursor x,y` puts the cursor on a point to show the snap and the rubber band, `--toggle`
switches the status toggles, and `--layers` draws the Layer manager instead.

Fonts are looked for in the folder of the drawing, then in Global settings > Fonts > Font search folders in order, a bare name taking `.shx` and a full path used as it is; the list is empty by default, and AutoCAD's own Fonts folders are never looked in unless you add them there. A TrueType file is also looked for in the Windows Fonts folder. A style naming `romans.shx` is drawn as the single strokes of romans, read from the file: shapes, unifont and big fonts, every pen code; a TEXT of romans.shx `ABC123` 10 high right-aligned on x 100 is inked from 44.762 to 100, as AutoCAD puts it. A style naming Arial is drawn by the canvas. A font named in the substitution table, or in the font mapping file (an `.fmp` file of `oldfont;newfont` lines, as FONTMAP), is drawn in the font given for it. A font not found is drawn in the replacement font (as FONTALT): by default GeometryHelper Simplex (`ghsimplex`), the presenter's own single-stroke font after the Hershey Roman Simplex letters, its capitals as high as the text. Write `simplex.shx,gbcbig.shx` to give a big font for big fonts not found too. On opening, the command line lists the missing SHX files once, each with what it is drawn in: `Missing SHX files: romans.shx drawn in GeometryHelper Simplex.` The Chinese, Japanese and Korean code pages of the big fonts are registered by the app at start.

## The ribbon

The window's ribbon has the tabs Home (Draw, Modify, Annotation, Layers, Properties, Utilities, View), Annotate (Single Line and Multiline Text; Linear, Aligned, Angular, Radius and Diameter dimensions; Multileader), View (Zoom Extents, Window, Previous, In and Out, Pan, Regen; Model and Layout; the Drawings list and the Properties panel shown or hidden; Grid, Snap, Ortho, Polar and Osnap), Plugins and Settings. Every button runs a command through the same path as typing it. A thing that is not built yet (Paste, PDF and Print, Insert, Manage and Output) has no button, or one that says so; the comparison with AutoCAD 2021, tab by tab, is in the drawing plan, 72-autocad-gap.md.

At a point prompt the box beside the cursor has two fields: X and Y, or the distance and angle from the base point. Tab moves from one to the other, and a number typed there is locked, drawn darker with a lock. Enter takes the point from the locked fields and the cursor's value in the other. A coordinate typed whole goes to the command line as typed. With Select by dragging off, click at one corner and click at the other for a box, or press on nothing and drag round the objects for a lasso: drawn clockwise it is a window, counter-clockwise a crossing.

F12 (DYN) shows the prompt, the cursor's coordinates or its distance and angle from the base point, and what you type beside the cursor; Global settings > Canvas and cursor > Input sets it on or off at start. Ctrl+9 hides or shows the command line; typing still reaches it while hidden. `--dialog color` and `--dialog truecolor` draw Select Color in screenshot mode, and `--toggle DYN` switches dynamic input.

OSNAP's arrow on the status bar lists the fourteen snaps; Object Snap Settings... opens Drafting Settings (DSETTINGS, DS, OS) with the Snap and Grid, Polar Tracking and Object Snap tabs, Object Snap Tracking On (F11) among them. OTRACK on the status bar, before OSNAP and on as in AutoCAD, switches object snap tracking: at a point prompt, a rest of half a second on a snap point acquires it, marked by a small cross, and the cursor is then held on the level and upright lines through it (every polar angle while POLAR is on) and where two of them cross, the lines drawn dotted. EXT, PAR, APP (or APPINT) and INS typed at a point prompt take Extension, Parallel, Apparent Intersection and Insertion for that point alone. A POINT is a node. `ObjectSnap.Override` takes snaps for the next point alone, whether OSNAP is on or not, as END typed at a prompt does; `CadSnapMarker` gives each kind's marker as strokes in pixels, 9 across.

## See also

- [Editing as AutoCAD does](editing.md): the shortcuts these settings replace.
- [Command plugins](plugins.md): the plugin folder and the trusted folders.
- [Opening, drawing and picking](display.md): the settings' colours and pickbox are what the canvas draws and picks with.
