# Settings, autosave and the window

What the presenter keeps between runs and how the window behaves: global settings in a JSON file, the copies autosave
leaves beside a drawing, how a drawing is closed, the keys and the mouse, and the two standalone apps with their options. The
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
| Canvas and cursor | the wheel zoom, the object snaps, the aperture, the polar increment, what F3 does | 1.2 a notch (1.01 to 4), every snap but Nearest, 10 pixels (2 to 50), 90 degrees (above 0 to 360, POLARANG), F3 toggles them |
| Drawing defaults | the preset, sheet, orientation and projection of a new model drawing | Assembly, A2, Landscape, Third angle |
| Units and rounding | decimals of coordinates, dimensions and angles; the separators | 2, 0 and 1 decimals; thousands parted by a space, decimals by a point |
| Layers, colours and lines | the layer table written to new drawings | Visible, Hidden, Centre, Hatch, Dimension, Mark, Sheet |
| Text and fonts | the fonts, the heights and the width factor of drawing text: TEXT and MTEXT start from the text height and write the width factor, and DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write their text at the text height; what Esc in the MTEXT editor with changes does (Hidden messages) | UI font Inter, drawing font Arial, text 2.5, titles 5, width factor 0.8, Esc asks |
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
copy of each open drawing that has a file and changes not saved, beside the file: `plan.autosave1.dxf` for `plan.dxf` and
`plan.dwg.autosave1.dxf` for `plan.dwg`, so that the two open from one folder keep copies of their own, the newest the
first. `BackupsKept` copies are kept, 3 by default and 0 to 50: the last is deleted and the others move up one.
The user's file itself is never written. A drawing with a command running, or with no file yet, is left for the next time,
and the copies are not listed in the file list.

## Opening a drawing

The app's window opens a drawing in the background (`MainViewModel.OpensInBackground`, set by the window; `OpenAsync`
opens one so whatever it says): the file is read and its display list and pick index made on a worker thread, and the
drawing is shown once they are made. Meanwhile the status bar says how far it has got, "Opening plan.dxf: drawing 45 %",
with a Stop button, and the window answers: the view shown pans and zooms, another drawing open is shown from the file
list, and Esc or Stop stops the opening, the command line saying "Opening plan.dxf stopped." A command typed or started
meanwhile is not run, so that none runs on a drawing half drawn: "LINE not run: plan.dxf is still opening. Run it again once
it is shown, or press Esc to stop opening it." One drawing opens at a time; another asked for meanwhile is refused, and the
same one asked again waits with it. The extensions are told of the drawing, and its fonts set, on the window's thread
between the reading and the drawing, as before. A drawing of 1 000 000 entities that held the window for 15 s, its first
frame included, holds it now for 0.2 s at most while it opens, then 0.8 s for its first frame. A test or a screenshot opens
the drawing before `Open` returns, as before.

## DWG files

The presenter opens a DWG of AutoCAD 2000 to 2018 as it opens a DXF, read by the library's own
[DWG reader](../dxf/dwg.md), and saves one as a DWG of AutoCAD 2018, the version AutoCAD 2021 saves in.

- Ctrl+O shows Select File, its types Drawing (*.dwg) first, then DXF (*.dxf), as AutoCAD 2021's; a DWG or DXF dropped on
  the window opens; the file list lists the DWG and DXF files of the folder. A file is read as a DWG where its first bytes
  say so ("AC1032" of AutoCAD 2018), whatever its name; a DWG of a version not read, R14 among them, is said on the command
  line, "Cannot open old.dwg: The DWG version AC1014 (R14) is not read yet; …", and not opened as a recovered DXF.
- Save As (Ctrl+Shift+S, SAVEAS) shows Save Drawing As with AutoCAD 2018 Drawing (*.dwg) first and the DXF of the
  drawing's own version after it, as "AutoCAD 2013/LT2013 DXF (*.dxf)" for a new drawing; the type of the drawing's file is
  chosen first. A name ending in .dwg is written as a DWG, any other as a DXF; the DXF is written in the drawing's own
  version, since the presenter does not convert a drawing to another. Save (Ctrl+S, QSAVE) of a drawing opened from a DWG
  writes it back as a DWG.
- What the DWG writer does not write yet is left out of the file and said after "Saved plan.dwg.": "Left out of the DWG,
  not written yet: RASTERVARIABLES 1." (`CadDocument.LeftOutOfDwg` holds it). Saved as a DXF, nothing is left out.
- A DWG is attached by XATTACH, -XREF and the External References palette, and inserted by INSERT, -INSERT and the Blocks
  palette's Other drawing, as a DXF is; WBLOCK writes a DWG where the file named ends in .dwg.
- Autosave keeps its copies of a DWG as DXF files beside it, the DWG's extension kept in their name:
  `plan.dwg.autosave1.dxf` for `plan.dwg`, where `plan.dxf` beside it has `plan.autosave1.dxf`.
- A new drawing (NEW, Ctrl+N) is named Drawing1.dwg, then Drawing2.dwg, as AutoCAD names its own, so that Save As and
  QSAVE offer it as a DWG first.

With FILEDIA 0 a command asks on the command line for the file it would show a file dialog for, as AutoCAD 2021 asks:

| Command | Asks | Then |
|---|---|---|
| QSAVE of a drawing with no file, SAVEAS | Save drawing as &lt;Drawing1.dwg&gt;: | saves there; a name with no extension takes the offered one's, .dwg for a new drawing or one opened from a DWG |
| XATTACH | Enter name of file to attach: | the Attach External Reference dialog shown with the file; a file not found said, no dialog |
| IMAGEATTACH | Enter image file name to attach: | the Attach Image dialog shown with the picture |
| INSERT, the Blocks palette's Browse… | Enter name of drawing file: | the palette shown again on Other drawing with that drawing's blocks |
| WBLOCK, the Write Block dialog's Browse… | Enter name of output file: | the dialog shown again with that file |

## Closing a drawing

A drawing is closed by CLOSE on the command line or the x on its row in the file list. A command cannot close its own
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
The panel on the left lists the drawing files, and has no layouts or layers. The ribbon has, among its tabs, Insert, Annotate and View, a Plugins
tab (see [Command plugins](plugins.md)), a Settings tab, and the tabs a host adds: the model drawing adds its Drawing tab.

### The keys and the mouse

| Key | Does |
|---|---|
| Esc | closes the AutoComplete list; then cancels the command; then clears the selection |
| Space, Enter | runs the row chosen of the AutoComplete list; else ends a command or a prompt; Enter alone runs the last command again |
| Up, Down | move the row chosen of the AutoComplete list; with the list closed, recall what was typed before |
| Ctrl+O | opens a DWG or DXF (see [DWG files](#dwg-files)) |
| Ctrl+W | switches selection cycling on and off, as AutoCAD 2021 |
| Ctrl+S, Ctrl+Shift+S | saves, saves as |
| Ctrl+N | asks a host for a new drawing from a model; where no host makes them, as in GeometryHelper DXF, makes a new drawing (NEW) |
| Ctrl+Z, Ctrl+Y | undo, redo |
| Delete | erases the selection |
| F3, F7, F8, F9, F10, F11, F12 | OSNAP, GRID, ORTHO, SNAP, POLAR, OTRACK, DYN |
| F5, Ctrl+E | the next isometric plane, Left, Top, Right (SNAPISOPAIR), written "<Isoplane Top>"; a command running goes on |

The function keys, Delete and the Ctrl keys of [the defaults](editing.md#aliases-and-keys) are rows of the Shortcuts category and can be changed. F5 and Ctrl+E turn the isoplane while no shortcut is given them.

| Mouse | Does |
|---|---|
| wheel | zooms about the cursor, 1.2 times a notch; through a viewport, its view (unless its display is locked) |
| middle button dragged | pans |
| middle button double-clicked | zooms to the extents, the paper included in a layout; through a viewport, its view to model space's extents |
| left double click in a layout | inside a viewport's frame works through it (MSPACE); off every frame goes back to the paper (PSPACE); a click in another viewport's frame works through that one |
| MODEL / PAPER on the status bar, Ctrl+R | in a layout a button, raised on the paper and pressed through a viewport: MSPACE or PSPACE; Ctrl+R the next viewport; on the Model tab a label. The scale beside it opens its list: in a layout the viewport's (Scale to fit, the standard scales, Custom...), grey while the padlock after it locks the viewport's display; on the Model tab the annotation scale (CANNOSCALE) |
| left click, no command running | picks within the pickbox; Shift+click adds or removes; a click on nothing starts a box |
| click / double click in DRAWINGS | selects the row / opens it; the right-click menu has Open, Close, Close Selected Drawings, Close All Other Drawings, Close All Drawings |
| Shift+right-click, a point asked | the object snap menu; the snap chosen is taken for that point alone |
| left button dragged, no command running | with Select by dragging on, a box as above; off, the box waits for a click at its other corner (left to right a window in solid blue, right to left a crossing in dashed green) |
| cursor over an entity | drawn 2 pixels wide in its colour taken halfway to the selection colour |
| a grip clicked | makes it hot and starts the grip modes, `** STRETCH **` first (see [Grip modes](editing.md#grip-modes)); Shift+click makes several hot; dragged, stretches it where it is let go |
| left click, a command running | answers its prompt: a point taken through the snaps, with a rubber band from the base point; or objects, by a click or a box |
| right click, a command running | Enter |

While MOVE, COPY, ROTATE, SCALE or MIRROR waits for its second point, a dashed ghost of the objects follows the cursor; an
XLINE or a RAY among them is drawn across the view as the canvas draws it, moved, turned, scaled or mirrored with the rest.
ROTATE and SCALE move it by a Reference too, and an array shows every copy while its options are offered.

While a prompt waits for the point that finishes an object, the canvas draws the object that point would make, as
AutoCAD 2021 drags it, in the colour of the current layer: RECTANG asking its other corner, the cursor 30 by 20 from the
first, draws the rectangle 30 by 20, not a line to the cursor. CIRCLE, ARC, ELLIPSE, POLYGON, DONUT, XLINE, RAY, PLINE
(each segment with its widths), REVCLOUD, OFFSET (the copy on the cursor's side), STRETCH, the frame of MTEXT and the
dimensions (DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMORDINATE, DIMCONTINUE, DIMBASELINE, the whole
dimension at the cursor) do the same. The preview is made by the code that makes the object, so it is what a click there
gives; where AutoCAD shows a radius or an angle with it, as CIRCLE does, the rubber band is drawn too.

The commands that change objects preview the change at the object under the cursor. FILLET and CHAMFER, hovering the
second object, draw both objects trimmed and the arc or line between them: two lines crossing at the origin, filleted with
a radius of 12, show the arc from (0, 12) to (12, 0). TRIM, in Quick mode and in Standard, draws the piece a pick would cut away faded
over the object, and EXTEND the object run on to its edge; while Shift is held each draws what the other would do, as a
pick with Shift does. LENGTHEN DYnamic draws the object with its end at the cursor, BREAK
the piece its second point would take out, faded as TRIM's (AutoCAD's command preview, COMMANDPREVIEW, covers BREAK
with TRIM, EXTEND and LENGTHEN). HATCH, at "Pick internal point", draws the hatch Enter would
make of the areas picked so far and the one under the cursor. TEXT draws a square of the height while
its height and rotation are asked, held at the point as the justification holds the text (on the baseline to the right
of it for Left, half to each side for Center, about it for Middle, below it for the Top ones, as long as the two points
are apart for Fit), and at "Enter text" the words typed so far where the line will stand. TABLE draws the outline of the table at the cursor, MLEADER the leader and its landing, QDIM
the whole row of dimensions. ROTATE and SCALE by Reference Points move the ghost at the second point, -ARRAY's unit cell
shows every copy (AutoCAD's help describes no preview for -ARRAY; the copies are this app's own), and ALIGN moves the ghost by its first pair and turns it by its second. -ARRAY shows its copies too
while the distance between columns is shown by two points, and in a Polar array while the angle to fill and the angle
between items are asked, each measured from the centre to the cursor, and while it asks whether to rotate them.
PASTECLIP and PASTEBLOCK draw the Clipboard's objects at the cursor, held by their base point, while they ask the
insertion point, and the text of another app as the MTEXT it becomes.

### The command line

With a drawing open and no command running, the command line lists what matches the letters typed once no key has come
for 300 milliseconds, as AutoCAD 2021's AutoComplete. LA typed lists LAYER first with "alias LA" at its right, then the
commands that begin with LA, the ones used most first and the rest by name, then the system variables that begin with LA
(each marked {x}), then, below a rule, the names with LA inside: CLAYER, PLAN, POLAR. The first row is chosen: Enter or
Space runs it, the arrows move it, a click runs another and Esc closes the list and clears what is typed, as AutoCAD's does. Typing on with the
list open updates it at once.

- **Most used first.** Each command run is counted, and the counts are kept in the settings file, so that a command used
  three times comes before one used once among those that begin with the same letters.
- **Synonyms.** ROUND is offered as `ROUND (FILLET)` and runs FILLET; likewise BEVEL (CHAMFER), DELETE and REMOVE (ERASE),
  DUPLICATE (COPY), FLIP (MIRROR), TURN (ROTATE), RESIZE (SCALE), SYMBOL (INSERT) and PRINT (PLOT).
- **AutoCorrect.** A name no command has, followed by a command, is noted: LIEN typed three times, each followed by LINE,
  has LINE offered first, marked AutoCorrect, the next time LIEN is typed. The corrections are kept in the settings file.
- **Search Content.** Below the commands and variables, under a rule, come the drawing's named objects, as AutoCAD
  2021's: its blocks, layers, hatch patterns, text styles and dimension styles, in that order, each kind by name with
  the names that begin with the letters first, and the kind at the right. WAL typed in a drawing of the block WALL_TAG
  and the layers WALLS and WALL-FINISH lists `WALL_TAG Block`, `WALL-FINISH Layer` and `WALLS Layer`. A kind's keyword
  lists every name of the kind: BLOCK or INSERT, LAYER, HATCH, TEXTSTYLE or STYLE, DIMSTYLE. Choosing a layer makes it
  current through CLAYER, a block runs -INSERT with its name, which asks the insertion point, a hatch pattern starts
  HATCH with it, and a text style or a dimension style is made current. The content is searched at the command line
  only: with DYN on, the list at the cursor holds commands and variables alone, as AutoCAD's help says of the Dynamic
  Input tooltip.

AutoComplete works at the Command prompt only. AutoCAD 2021's help (Input Search Options, the command line suggestion
list, Searching for Named Objects) describes no completion at the prompts of a running command, so none is offered there.

A right click on the command line gives its menu: **Recent Commands** (the last 20 run, the newest first, each runs again,
kept in the settings file from one session to the next),
**Input Settings** (AutoComplete, AutoCorrect, Search System Variables, Search Content and Mid-string Search, ticked when
on and kept in the settings file; Delay Time…, which asks INPUTSEARCHDELAY as AutoCAD 2021's console does, `Enter new
value for INPUTSEARCHDELAY <300>:`, 100 to 10 000; and Input Search Options…), **Copy** (the text selected in the
command line), **Copy History** (every line written), **Paste** (the first line of the clipboard added to what is typed),
**Paste To CmdLine** (each line of the clipboard run in turn) and **Options…** (Global settings).

**Input Search Options** (INPUTSEARCHOPTIONS) is AutoCAD 2021's dialog of the same name: Enable AutoComplete, with
Enable mid-string search and the order of the suggestions, According to frequency of usage or Alphabetically; Enable
AutoCorrect, with Remember corrections after 2 to 10 mistypes (3 by default); Search system variables, with Separate
commands and system variables, which lists the variables last, below a rule of their own; the suggestion list's delay,
100 to 10 000 milliseconds; and Search content at command line, with the Content type list (Block, Layer, Hatch, Visual
Style, Text Style, Dim Style), each kind ticked or not and moved up or down to set the order Search Content lists them
in. An option under a switch that is off is greyed. OK keeps the settings in the settings file; a number out of range
keeps the dialog open with the reason under it. The app's drawings have no visual styles, so Visual Style finds
nothing.

-INPUTSEARCHOPTIONS asks the same on the command line, word for word as AutoCAD 2021's console: `Current settings:
AutoComplete = Y, AutoCorrect = Y, System variable = Y, Content = Y, Midstring = Y, Delay=0.30`, then `Enter an input
search Option [autoComplete/autocoRrect/System variables/conTent/Midstring/Delay]:` until Enter, each switch asked as
`Automatically complete a command as you type? [Yes/No] <Yes>:` and its kin, and Delay in seconds, `Specify the delay
time in seconds <0.30>:`, above 10 refused with `Invalid. Maximum delay is 10 seconds` and below 0.1 left as it was.
INPUTSEARCHOPTIONFLAGS holds the five switches as one number, as AutoCAD's does (checked one by one in its console): 1
AutoComplete, 2 AutoCorrect, 4 Search system variables, 8 Search content, 16 Mid-string search, 31 by default; 64 is
refused with `Requires an integer between 0 and 31.`

### The application menu and Recent Documents

The G button at the top left drops the application menu, as AutoCAD's: a search box for the commands, then New, Open,
Save, Save As, Export, Print and Close, and Options and Exit at the foot. Resting the pointer on an item with ▸ shows its
list at the right; a click on the item runs the first entry of its list.

| Item | Entries |
|---|---|
| New | Drawing (NEW); Drawing from a model, where a host makes them |
| Open | Drawing (the Open dialog, Ctrl+O) |
| Save As | Drawing (SAVEAS); Drawing Template and Other Formats greyed |
| Export | PDF (PLOT, to GeometryHelper PDF), PNG, JPG and BMP (PNGOUT, JPGOUT, BMPOUT), Block or objects to a DXF (WBLOCK), Other Formats (EXPORT) |
| Print | Plot (PLOT), Plot Preview (PREVIEW), Page Setup (PAGESETUP); Batch Plot and Manage Plot Styles greyed |
| Close | Current Drawing (CLOSE), All Drawings |

With no item under the pointer, the right of the menu shows **Recent Documents**: the pinned files first in the order they
were pinned, a rule, then the others by the date they were opened, newest first, 9 in all unless more are pinned. A file
opened by any way (the Open dialog, a file dropped on the window, the app's command line, the lists themselves) goes to
the top. The pin at the right of a row pins or unpins it. Resting the pointer on a name shows its preview picture, path,
version, date and size: only the top of the file (for `$ACADVER`) and its end (for the THUMBNAILIMAGE section) are read,
and only then, on a worker thread, so a list of large files, or of files on a slow share, shows at once and the picture
comes when read. A file chosen that is no longer on the disk is taken off the list, the command line saying `Cannot find
D:\plans\gone.dxf: it is taken off the recent files.` The list and its pins are kept in the settings file.

### The start screen

With no drawing open the canvas shows the start screen: the name of the app, Open DXF… (Ctrl+O), New, and Recent
Documents with the same files and pins as the application menu, under PINNED and RECENT headings, each row with its
preview, folder and when it was opened (`today 09:14`, `yesterday 16:40`, `2026-10-30`); the two buttons at its top right
show the files as a list or as large pictures. GeometryHelper DXF, which makes no drawing from a model, offers `New ·
Ctrl+N` (NEW) and says "Ctrl+O opens a DXF, Ctrl+N starts a new drawing." on the command line; GeometryHelper Drawing adds
"New drawing from a model… · Ctrl+N" and calls NEW "Blank drawing". The window tells the two apart by whether a host
handles `MainViewModel.NewFromModelRequested` (`MainViewModel.CanMakeFromModel`).

## The apps

There are two standalone apps, both net8.0-windows, both the same window over the same settings file:

| App | What it is | Use it to |
|---|---|---|
| GeometryHelper DXF, `GeometryHelper.Dxf.App.exe` | the presenter alone: `GeometryHelper.Dxf.Viewer` and its controls, no model drawing | open, edit and plot DXF drawings in place of AutoCAD; the program to associate with `.dxf` files |
| GeometryHelper Drawing, `GeometryHelper.Drawing.App.exe` | the presenter with the model drawing added: the Drawing tab, New drawing, Update from model and the drawing plugins | make drawings from a model (see [Model drawings](../drawing/index.md#the-app)) and edit them |

```
GeometryHelper.Dxf.App.exe plan.dxf
```

opens the window on a drawing, as double-clicking a file associated with the app does. The app reads the global settings from
the user's file and loads the command plugins of its plugin folder on the startup list: GeometryHelper DXF keeps them in
`%AppData%\GeometryHelper\Dxf`, GeometryHelper Drawing in `%AppData%\GeometryHelper\Drawing`, so that a drawing plugin is
never offered to the app that has no model drawing. The title bar and the start screen carry the app's name.

Both apps start through `CadAppHost` of `GeometryHelper.Dxf.Viewer.Wpf`, and their window is its `CadAppWindow`. GeometryHelper
Drawing derives from `CadAppHost` to add its parts: the host's protected members take its options off the command line, put
the model drawing into the view model, make a drawing before the file is opened, draw its dialogs and select what its
options name. A host of its own does the same.

### Screenshots

For a picture of the window in a test or a document:

```
GeometryHelper.Dxf.App.exe --screenshot out.png plan.dxf --size 1600 960
```

draws the window with the drawing open into a PNG, with no window shown, and exits: 0 when written, 2 for arguments or a file
it cannot read. Both apps take these options:

| Option | Does |
|---|---|
| `--size 1600 960` | the size of the picture of the window, 1 600 by 960 by default; a dialog is drawn at its own size |
| `--space "Layout1"` | shows a layout |
| `--select H1,H2` | selects entities by handle |
| `--run "MOVE;0,0"` | types lines on the command line, parted by semicolons, an empty part being Enter |
| `--cursor x,y` | puts the cursor on a point of the drawing, to show the snap and the rubber band of the prompt waiting |
| `--toggle GRID,DYN` | switches status toggles |
| `--tab Insert` | shows a ribbon tab: Insert, Annotate, View, Plugins, Settings, or a host's, as Drawing |
| `--plugin path.dll` | loads a command plugin, every file trusted, into a folder of the run under the temporary folder |
| `--layers` | draws the Layer manager instead of the window, the lines of `--run` typed first: `--space Layout1 --run MS --layers` shows its VP columns |
| `--dialog name` | draws a dialog or a menu instead of the window, the lines of `--run` typed first where it needs them |
| `--recent "a.dxf\|*b.dxf"` | fills Recent Documents, the first the newest, a file written with `*` pinned |

The dialogs are `save-changes`; `mtext-unsaved` (Multiline Text - Unsaved Changes); `options` or `settings:Canvas and cursor` (Global settings on a category); `plugins`,
`plugin-settings` and `plugin-untrusted`; `color`, `truecolor` and `truecolor-rgb` (Select Color on index 30);
`dsettings[:Tab]`; the menus `osnap-menu`, `snap-menu` (after `--run "LINE"`), `drawings-menu` and `scale-menu` (the status bar's scale list); `missing-shx` and
`missing-shx-replace`; `ptype`; `linetype`, `linetype-load`, `select-linetype`, `hatch`, `hatch-gradient`, `hatch-user`,
`hatch-solid` and `hatch-file`; `block`, `insert`, `wblock`, `attdef` and `eattedit[:Tab]`; `battman`,
`battman-edit[:Tab]`, `battman-settings` and `attdia` (on the first block with attributes); `purge` and
`purge-nonpurgeable`; `classicgroup` and `group-order` (on the first group); `style`, `style-new`,
`dimstyle`, `dimstyle-new`, `dimstyle-modify[:Tab]`, `dimstyle-override[:Tab]` and `dimstyle-compare[:Other]`;
`pastespec`; `app-menu` and `app-menu:export` (the application menu on Recent Documents, or on the list of New, Open,
Save As, Export, Print or Close); the command line's menus `cmdline-menu`, `input-settings-menu` and `recent-commands-menu`;
and `autocomplete:LA` (the AutoComplete list for LA). A popup of the running window cannot be drawn; these draw its
content alone. A name neither app knows exits with 2: `--dialog update: no such dialog.` from GeometryHelper DXF.

GeometryHelper Drawing adds its own: `--new-drawing "Test bodies" Assembly` makes a model drawing from a source with a
preset and shows it, `--tab Drawing` shows its tab, `--pick view|dimension|mark|line` selects the first entity of the
drawing that draws one, `--hand-edit` moves its first dimension 25 up by hand, and `--dialog
new|new-source|new-settings|update|settings[:Tab]|export|message|run-plugin` draws a dialog of the model drawing.

Fonts are looked for in the folder of the drawing, then in Global settings > Fonts > Font search folders in order, a bare name taking `.shx` and a full path used as it is; the list is empty by default, and AutoCAD's own Fonts folders are never looked in unless you add them there. A TrueType file is also looked for in the Windows Fonts folder. A style naming `romans.shx` is drawn as the single strokes of romans, read from the file: shapes, unifont and big fonts, every pen code; a TEXT of romans.shx `ABC123` 10 high right-aligned on x 100 is inked from 44.762 to 100, as AutoCAD puts it. A style naming Arial is drawn by the canvas. A font named in the substitution table, or in the font mapping file (an `.fmp` file of `oldfont;newfont` lines, as FONTMAP), is drawn in the font given for it. A font not found is drawn in the replacement font (as FONTALT): by default GeometryHelper Simplex (`ghsimplex`), the presenter's own single-stroke font after the Hershey Roman Simplex letters, its capitals as high as the text. Write `simplex.shx,gbcbig.shx` to give a big font for big fonts not found too. On opening, the command line lists the missing SHX files once, each with what it is drawn in: `Missing SHX files: romans.shx drawn in GeometryHelper Simplex.` The Chinese, Japanese and Korean code pages of the big fonts are registered by the app at start.

## The ribbon

The window's ribbon has the tabs Home (Draw, Modify, Annotation, Layers, Properties, Groups, Utilities, Clipboard, View, in AutoCAD 2021's order), Insert (after a host's tabs, as below), Annotate (Single Line and Multiline Text; the Linear split of Linear, Aligned, Angular, Radius and Diameter, Quick (QDIM), the Continue split of Continue and Baseline, Adjust Space (DIMSPACE) and Break (DIMBREAK), and the title Dimensions ▾, whose slide-out gives Oblique and Text Angle, DIMEDIT with Oblique or Rotate; Multileader), View (Zoom Extents, Window, Previous, In and Out, Pan, Regen; Model and Layout; the Drawings list and the Properties panel shown or hidden; Grid, Snap, Ortho, Polar and Osnap), Plugins and Settings. Home's Modify is laid out as AutoCAD 2021's panel: Move, Copy and Stretch; Rotate, Mirror and Scale; Trim, Fillet and Array, each with an arrow that drops Trim and Extend, Fillet and Chamfer, Rectangular and Polar Array; Erase, Explode and Offset by their icons alone; under them a row of Break, Join, Lengthen and Align; and its title drops Break at Point, Break, Join, Lengthen and Align, as AutoCAD's panel slides out. A split button shows the command last picked from its arrow, as AutoCAD's do: Extend picked once from Trim's arrow leaves the button reading Extend and running EXTEND until Trim is picked again. The choice lasts for the window's session. Modify's fifth column holds Draw Order: Bring to Front, Send to Back, Bring Above Objects and Send Under Objects run DRAWORDER and give it Front, Back, Above objects or Under objects once the objects are picked, and Bring Text to Front runs TEXTTOFRONT with Text. Annotation holds Text, the Linear split (Radius picked once, the button reads Radius ▾), Leader and Table (not built yet). Groups holds Group, Ungroup, Group Edit (not built yet) and Group Selection On/Off, drawn pressed while PICKSTYLE is 1 and switching it as Ctrl+Shift+A does. Utilities adds Quick Select (QSELECT). Clipboard holds the Paste split, whose top runs PASTECLIP, or the paste picked last from its arrow (Paste as Block, Paste to Original Coordinates, Paste Special), and Cut, Copy and Copy with Base Point. Every button runs a command through the same path as typing it. A thing that is not built yet (PDF and Print, Manage and Output) has no button, or one that says so; the comparison with AutoCAD 2021, tab by tab, is in the drawing plan, 72-autocad-gap.md.

Insert holds three panels, as AutoCAD 2021 parts them. Block: Insert, whose top opens the Blocks palette (INSERT) on Current drawing and whose arrow drops the gallery of the drawing's blocks, a tile for each in the palette's order (by name, letters of either case alike), "No blocks in this drawing" where it has none; a click on a tile starts INSERT placing that block at once, with the palette's options (the point asked, scale 1, no rotation), and lists it first under the palette's Recent tab. Under the tiles, Recent Blocks… and Blocks from Other Drawings… open the palette on its Recent and Other drawing tabs; Favorite Blocks… is greyed. Beside it the Edit Attribute split of Single (EATTEDIT) and Edit Attributes… (ATTEDIT), Multiple greyed. Block Definition: the Create Block split of Create Block (BLOCK) and Write Block (WBLOCK), Define Attributes (ATTDEF) and Set Base Point (BASE); Manage Attributes and Block Editor greyed. Reference: the Attach split of Attach DXF… (XATTACH) and Attach Image… (IMAGEATTACH), the other formats greyed; Clip (IMAGECLIP); External References (the palette, ER); the image frame box, which shows the drawing's IMAGEFRAME ticked and sets it, Hide Frames 0, Display and Plot Frames 1, Display but Not Plot Frames 2, as IMAGEFRAME typed (a drawing with no image refuses it); and Transparency (TRANSPARENCY); Adjust, Underlay Layers and Snap to Underlays greyed. Every button is off with no drawing open.

As the window narrows, the panels shrink as AutoCAD's do, in steps taken in turn until they fit and given back in reverse as the window widens. Home has every panel full from about 2 290 pixels. Narrower, View, Clipboard, Utilities and Groups collapse in that order, then Properties (its boxes 168 pixels wide go to 120), Layers and Draw go medium, then Annotation, Properties and Layers collapse; Modify never shrinks and Draw never collapses. At 1 600 pixels the first seven steps are taken. A medium panel keeps every button and drops the labels of its small ones, the tooltip still naming the command. A collapsed panel is one big button, its icon, name and ▾: a click drops the full panel below it, with its title bar and a pin; Esc or a click elsewhere closes it unless it is pinned, a command picked in it closes it, and near the window's right edge it shifts left to stay inside. The contextual Text Editor tab fits 1 600 pixels the same way: Spell Check and Tools collapse and Insert becomes a column of small buttons; narrower, Options goes small and Paragraph collapses. Style, Formatting and Close never shrink, and Close stays the last panel. Insert, 852 pixels wide, is full in a window 876 wide or wider; narrower, Reference and Block Definition go medium (Reference's frame box narrows from 150 to 110 pixels), then Reference collapses below 606 and Block Definition below 394; Block never shrinks. Annotate, 992 pixels wide, is full from a window 1 016 wide; narrower, Leaders, Text and Dimensions go medium, their "Style" and "Height" captions gone and their boxes 84 pixels wide instead of 118, Dimensions' small buttons by their icons; then Leaders collapses below 642 and Text below 544. Dimensions never collapses. View, 670 pixels wide, is full from a window 694 wide; narrower, Drafting and Navigate draw their small buttons by their icons (a toggle on still lit), then Interface collapses below 476 and Views below 410. Each tab's steps are the `Steps` of its `RibbonStrip`, as `View:Collapsed; Clipboard:Collapsed`.

With DYN on, dynamic input works at the cursor as AutoCAD 2021's: what is typed and what is asked show 18 pixels right of the crosshair and 14 below it, and follow it, while the command line keeps its own copy of every prompt and every key. At the Command prompt the box opens with the first key, "REC", and the AutoComplete list opens under it after the delay, ranked as the command line's, whose own list stays closed; Enter runs the row highlighted, a click on a row runs it and a click on the canvas closes it. In a command the box shows the prompt as AutoCAD words it, "Specify next point or", and a ↓ where the prompt has options: ↓ opens their menu at the cursor, one keyword a row, where the arrows move, Enter or a click picks one, Esc closes the menu alone and a key typed closes it and goes into the field. At a point prompt the box has two fields, X and Y; with a base point they are relative to it, as DYNPIFORMAT 0, so RECTANG's other corner shows 1 200.00 and 800.00 for a corner 1 200 by 800 from the first. # typed first makes a value absolute and @ relative. At a second point the fields are dimensional input instead, the two fields alone with no dimension line, extension lines or angle arc drawn: the length beside the middle of the rubber band, 8 pixels off it on its left as it runs from the base point, and the angle 50 pixels along the band from the base point, 8 pixels off it on the other side, so that neither covers the band. The box of the prompt never hides a field: where it would, as when it stands left of a cursor by the canvas's right edge, the length moves to the band's other side or the box moves up or down clear of the fields, whichever moves less. That is the next point of LINE and PLINE, the second point of ARC, of CIRCLE's 2P, of ELLIPSE's axis, of POLYGON's edge, of XLINE and RAY, of MOVE, COPY and STRETCH, of OFFSET's distance shown by two points, and of a grip stretched or moved; and, as AutoCAD 2021's pointer input takes a second or next point in polar form by default, the second and third points of CIRCLE's 3P, each from the point before, its first point X and Y as any first point, the second point of MIRROR's line, of ROTATE's and SCALE's Reference by two points, of ALIGN's destinations, of BREAK's First point, of DIST, of TEXT's Align and Fit, of DIMLINEAR's and DIMALIGNED's second extension line, of DIMCONTINUE and DIMBASELINE, the endpoints of an angle in MEASUREGEOM and DIMANGULAR, and the next points of AREA and of REVCLOUD's Polygonal. The corner of a box, as RECTANG's other corner, ZOOM's window, MTEXT's or REVCLOUD's Rectangular, and the location of a dimension line or a leader keep the X and the Y from the base point. Tab locks the field typed into, drawn with a lock, and moves to the other; a comma moves from X to Y and a < from the length to the angle. Enter takes the point from the locked fields and the cursor's value in the other, and a coordinate typed whole, "500,300" or "400<90", is relative to the base point unless # comes first. A prompt for a distance from a known point shows the length field alone, as the radius of CIRCLE or POLYGON and ELLIPSE's distance to the other axis, and a prompt for an angle from one the angle field alone, as ROTATE's and that of a grip's ROTATE mode, by the grip; a number typed shows in the field and Enter gives it to the command. SCALE's factor keeps the one box, pointer input only as AutoCAD's, and so does a grip's SCALE mode. The angle field counts from the X axis of the UCS turned by ANGBASE wherever it shows: ROTATE's new angle after Reference too, which AutoCAD's help calls "a new, absolute angle", and the next point of PLINE after an arc segment; Autodesk's help names no field counted from a reference line. The fields measure to the point the rubber band ends at, not to the cursor: with an endpoint snapped, or ortho or polar holding the band, the length and the angle are those of that point and the length stands by the middle of the band to it; a distance typed alone and Enter go along that band too, as on the command line. With ortho on, LINE's next point from (1 000, 1 000) with the cursor at (1 300, 1 400) shows 400 and 90, and 300 typed ends the line at (1 000, 1 300). A field that holds a number holds the rubber band, the preview and a click with it: with 300 locked as the length of a line from (1 000, 1 000) and the cursor at (1 300, 1 400), 500 away, the preview and a click end at (1 180, 1 240), and 250 typed at CIRCLE's radius draws the preview 250 in radius wherever the cursor is. Esc at the Command prompt clears what is typed and the list at once and writes *Cancel*; in a command it cancels it. With DYN off the cursor carries nothing and the command line does all of it. With Select by dragging off, click at one corner and click at the other for a box, or press on nothing and drag round the objects for a lasso: drawn clockwise it is a window, counter-clockwise a crossing.

F12 (DYN) shows the prompt, the cursor's coordinates or its distance and angle from the base point, and what you type beside the cursor; Global settings > Canvas and cursor > Input sets it on or off at start. Ctrl+9 hides or shows the command line; typing still reaches it while hidden. `--dialog color` and `--dialog truecolor` draw Select Color in screenshot mode, `--toggle DYN` switches dynamic input, and `--type REC` types text left unsent with its AutoComplete list, `--type {Down}` the options menu of the prompt.

The status bar starts as AutoCAD 2021's does: SNAP and ORTHO off (SNAPMODE 0, ORTHOMODE 0), POLAR, OTRACK and OSNAP on (AUTOSNAP 63, OSMODE without 16384), and DYN as Global settings say, on by default (DYNMODE 3). SNAP, GRID, ORTHO and the lineweights are each drawing's own, as AutoCAD keeps SNAPMODE, GRIDMODE, ORTHOMODE and LWDISPLAY in the drawing: a drawing opened shows SNAP and GRID, and their spacing, from its `*Active` viewport, ORTHO from its `$ORTHOMODE` and its lineweights from its `$LWDISPLAY`, and its tab shows them again whenever it is shown, while POLAR, OTRACK, OSNAP and DYN stay the window's, as AutoCAD keeps them in the registry. A drawing with a snap of 2.5 by 5 and a grid of 10 by 20 steps the cursor by 2.5 across and 5 up, and draws its grid lines 10 apart across and 20 up, every fifth the stronger (GRIDMAJOR 5); a grid spacing of nought takes the snap's, as GRIDUNIT 0 does. A drawing that has no such value takes the window's: SNAP and ORTHO off, GRID and the lineweights as Global settings say, a spacing of 10; a new drawing is given the GRID of Global settings. Switched, by the status bar, F7 to F9, the commands, the variables or Drafting Settings, each is written into the drawing shown, and the snap spacing too, as AutoCAD saves them, so that a save keeps them. OSNAP's arrow on the status bar lists the fourteen snaps; Object Snap Settings... opens Drafting Settings (DSETTINGS, DS, OS) with the Snap and Grid, Polar Tracking and Object Snap tabs, Object Snap Tracking On (F11) among them. OTRACK on the status bar, before OSNAP and on as in AutoCAD, switches object snap tracking: at a point prompt, a rest of half a second on a snap point acquires it, marked by a small cross, and the cursor is then held on the level and upright lines through it (every polar angle while POLAR is on) and where two of them cross, the lines drawn dotted. EXT, PAR, APP (or APPINT) and INS typed at a point prompt take Extension, Parallel, Apparent Intersection and Insertion for that point alone. A POINT is a node. `ObjectSnap.Override` takes snaps for the next point alone, whether OSNAP is on or not, as END typed at a prompt does; `CadSnapMarker` gives each kind's marker as strokes in pixels, 9 across.

The snap and the grid are laid from SNAPBASE and turned by SNAPANG, each drawing's own from its `*Active` viewport, as AutoCAD 2021 keeps them, and set by SETVAR or typed alone: SNAPBASE a point, SNAPANG an angle in the drawing's angle units, kept from nought up to a whole turn (400 is kept as 40, -30 as 330), GRIDMAJOR 1 to 100 ("Requires an integer between 1 and 100."), SNAPSTYL 0 or 1 ("Requires 0 or 1 only.") and SNAPISOPAIR 0 to 2. A snap of 10 turned by 30 degrees puts the cursor at (9, 4) on (8.6603, 5); ortho holds the cursor along the turned axes, the polar angles are counted from SNAPANG, the crosshair is drawn along it, and the grid is drawn through SNAPBASE along it, a major line every GRIDMAJOR; zoomed out until its lines come within 6 pixels, the grid shows its major lines alone, then 2, 5, 2 and on times them. SNAPSTYL 1 makes the snap isometric: its points lie where the 30, 90 and 150 degree lines cross, as far apart along each as the vertical spacing, which the drawing keeps halved, as AutoCAD does (SNAP Style Isometric 5 in AutoCAD keeps 8.6603 by 2.5); SNAP typed then asks the vertical spacing, and 3 keeps 5.1962 by 1.5. Ortho, the crosshair and the grid follow the plane SNAPISOPAIR names: Left on the 90 and 150 degree axes, Top on 30 and 150, Right on 30 and 90. ISOPLANE says "Current isoplane: Left", asks "Enter isometric plane setting [Left/Top/Right] <Top>:", Enter taking the next, and F5 or Ctrl+E turns to the next at any time, a command running going on, writing "<Isoplane Top>". A point typed is taken as typed, neither snapped nor held by ortho, as in AutoCAD.

The Snap and Grid tab of Drafting Settings is AutoCAD 2021's, the dialog 560 by 580 on every tab. On the left: Snap On (F9); Snap X and Y spacing (SNAPUNIT) with Equal X and Y spacing, ticked as the dialog opens where the snap and the grid are each as wide as high, which copies a spacing typed in an X box to its Y box and back; Polar distance (POLARDIST); Grid snap or PolarSnap (SNAPTYPE), and Rectangular or Isometric snap (SNAPSTYL). On the right: Grid On (F7); the dotted grid in 2D model space, the Block editor and Sheet/Layout (GRIDSTYLE 1, 2 and 4); Grid X and Y spacing (GRIDUNIT, nought taking the snap's) and Major line every (GRIDMAJOR); Adaptive grid, Allow subdivision below grid spacing, Display grid beyond Limits and Follow Dynamic UCS (GRIDDISPLAY 2, 4, 1 and 8). PolarSnap greys the snap spacing, Equal X and Y spacing and the snap style and opens Polar distance; the isometric snap greys the X boxes and Equal X and Y spacing, and the Y boxes show the full vertical spacing, twice what the drawing keeps: a snap of 8.6603 by 2.5 shows 5, and 10 typed there writes 17.3205 by 5, as SNAP Style Isometric 10 does, the grid the same way, nought kept nought. Allow subdivision is greyed, and kept, while Adaptive grid is off. Apply and OK write the values changed, the drawing's into its `*Active` viewport and SNAPTYPE, POLARDIST and GRIDSTYLE into the settings file, as AutoCAD keeps them in the registry. A box refused is edged in red, the caret put in it and the reason said at the foot, nothing written: "The snap spacing must be a number above nought.", "The polar distance must be a number, nought or above.", "The grid spacing must be a number, nought or above." and AutoCAD's "Requires an integer between 1 and 100." for Major line every; a grid spacing typed below nought is taken as its size, as AutoCAD's GRIDUNIT takes -1,1 as 1,1. With SNAP on and PolarSnap chosen, a point held on a polar angle from the base point is taken a whole number of POLARDIST from it, or of the snap X spacing where POLARDIST is nought: with 250, a line from (1 000.3, 1 000.6) to a cursor at (1 620, 1 000.9) ends at (1 500.3, 1 000.6); off a polar angle the cursor goes free and the grid is not snapped to. PolarSnap steps along the lines of object snap tracking too, from the point acquired: with 25, a cursor 37 along the level line from a point acquired is taken 25 along it.

The grid is drawn by GRIDSTYLE and GRIDDISPLAY, as AutoCAD 2021's help describes them. GRIDSTYLE 1 draws it in model space as dots where its lines would cross, 2 in the Block Editor, 4 in the layouts; the grid is drawn in model space and the Block Editor. GRIDDISPLAY without 1 keeps the grid of model space within LIMMIN and LIMMAX; without 2 it is not thinned out as the view zooms out, and not drawn at all where its lines would come within 6 pixels, as AutoCAD shows no grid too dense; 4 with 2 adds lines between as the view zooms in, a fifth of the spacing apart with GRIDMAJOR 5, so that a grid of lines 60 pixels apart is drawn every 12; 8, following the dynamic UCS, is kept but not drawn by. Object snap tracking follows the snap: its level and upright lines from a point acquired run along the axes turned by SNAPANG (with 30 degrees at 30, 120, 210 and 300), or along those of the isoplane (on the top plane at 30, 150, 210 and 330), and the polar lines from the base point are turned by SNAPANG too, as the polar angles are.

SNAP and GRID typed ask as AutoCAD 2021 does, checked in its console. SNAP: "Specify snap spacing or [ON/OFF/Aspect/Legacy/Style/Type] <10.0000>:", Aspect left out while the snap is isometric; the spacing offered is A where the X and Y spacings differ, and Enter then takes Aspect. A spacing, Enter, Aspect ("Specify horizontal spacing <10.0000>:", "Specify vertical spacing <10.0000>:"), Legacy ("Maintain legacy behavior of always snapping to the grid? [Yes/No] <No>:", kept for the window, not drawn by), Style and Type all turn SNAP on, OFF alone off; nought or less is refused with "Value must be positive and nonzero." and text with "Requires numeric distance, two points, or option keyword.", both asked again. Style asks "Enter snap grid style [Standard/Isometric] <S>:": Isometric then "Specify vertical spacing <10.0000>:", a snap of 10 by 10 with a grid of 4 by 6 turning into a snap of 8.6603 by 2.5 for 5 and a grid of 10.3923 by 3, as AutoCAD keeps them; Standard then "Specify snap spacing or [Aspect] <5.0000>:", the grid taking twice its kept Y spacing, 6 by 6. Type asks "Enter snap type [Polar/Grid] <Grid>:", and Polar "Enter polar snap tracking distance <0.0000>:" (SNAPTYPE, POLARDIST). GRID: "Specify grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect] <10.0000>:", Aspect left out while the snap is isometric, where the spacing typed is the vertical one (1 keeps 1.7321 by 0.5); 2x is twice the snap spacing, 0 or Snap takes the snap's; below nought is refused with "Value must be positive.", other text with "Requires a distance, numberX, or option keyword.". Major asks "Enter the number of grid divisions per major grid line <5>:", aDaptive "Turn adaptive behavior on [Yes/No] <Yes>:" and, where Yes, "Allow subdivision below grid spacing [Yes/No] <No>:", Limits "Display grid beyond Limits [Yes/No] <Yes>:", Follow "Follow dynamic UCS [Yes/No] <No>:", Aspect "Specify the horizontal spacing(X) <10.0000>:" and "Specify the vertical spacing(Y) <10.0000>:"; every answer but OFF turns GRID on.

The variables of the snap and the grid are set by SETVAR or typed alone, refused in AutoCAD's words. SNAPUNIT takes a point of two spacings above nought, 0,5 and -1,1 getting "Cannot set SNAPUNIT to that value." and ending; with an isometric snap it reads the Y doubled and keeps what is typed, so that 4,6 reads 4,12, as AutoCAD does. GRIDUNIT takes nought and drops a sign, -2,3 kept as 2,3. Text where a point is asked gets "Invalid 2D point.", asked again. POLARDIST takes a distance of either sign, -1 kept as -1 and stepping by the snap X spacing; text gets "Requires numeric distance or two points.". SNAPTYPE takes 0 or 1 ("Requires 0 or 1 only."), GRIDSTYLE 0 to 7, GRIDDISPLAY 0 to 15 and SNAPMODE 0 to 2 ("Requires an integer between 0 and 7.", text and 2.5 refused the same way): SNAPMODE 2, which AutoCAD takes and reads back though its help lists only 0 and 1, turns SNAP on and reads 2 until SNAP is switched. LWDISPLAY is shown as OFF or ON, "Enter new value for LWDISPLAY <OFF>:", and takes 0, 1, ON or OFF, anything else getting "Requires 0 or 1 only, or option keyword.".

## See also

- [Editing as AutoCAD does](editing.md): the shortcuts these settings replace.
- [Command plugins](plugins.md): the plugin folder and the trusted folders.
- [Opening, drawing and picking](display.md): the settings' colours and pickbox are what the canvas draws and picks with.
