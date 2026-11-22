# Settings, autosave and the window

What the presenter keeps between runs and how the window behaves: global settings in a JSON file, the copies autosave
leaves beside a drawing, how a drawing is closed, the keys and the mouse, and the two standalone apps with their options. The
settings are in the core and run in a test; autosave, closing and the window belong to the controls
(`GeometryHelper.Cad.Wpf`), which the API reference does not list, so they are described here and not linked.

## Global settings

[`GlobalSettings`](xref:GeometryHelper.Cad.GlobalSettings) is what applies to the whole app and every drawing on the
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
spaces of indent, 7 984 characters for the defaults.

### The categories

The presenter's Global settings dialog has eight categories, none of them about drawings made from a model. The JSON keeps the
settings of those, which a host that makes model drawings reads and neither that dialog nor the presenter shows or applies: the preset,
sheet, orientation and projection of a new drawing, the model-change check and its interval, the DXF layout and opening the
folder after an export, the colour of hidden lines, the decimals of dimensions and the height of titles, and the font, height and
width factor of drawing text. A file that holds them keeps them, and OK writes them back as they were.

| Category | Holds | Defaults |
|---|---|---|
| Appearance | the colours of the canvas, paper, visible lines, selection and grips; the crosshair, pickbox and grip size; the grid; line weights | canvas `#000000`, paper `#FFFFFF`, selection and grips `#3D9BFF`, crosshair 5 %, pickbox 5 pixels, grip size 5 (a square 15 pixels across; 1 to 255), grid off, line weights shown |
| Canvas and cursor | the wheel zoom and its direction, the object snaps, the aperture, the polar increment, what F3 does | 1.5 a notch (1.01 to 4, ZOOMWHEEL turns it round; a file saved with 1.2 keeps 1.2), every snap but Nearest, 10 pixels (2 to 50), 90 degrees (above 0 to 360, POLARANG), F3 toggles them |
| Units and rounding | decimals of coordinates and angles; the separators | 2 and 1 decimals; thousands parted by a space, decimals by a point |
| Text and fonts | the UI font, chosen from the font families installed on Windows (each shown in its own face) or typed; what Esc in the MTEXT editor with changes does (Hidden messages); the replacement font of a font not found (FONTALT), whether the Missing Fonts dialog is shown on opening a drawing, the font mapping file (FONTMAP) and the fonts drawn in others | UI font Inter, Esc asks, `ghsimplex`, the dialog on, no mapping file, no substitutions |
| Folder preference | the font search folders and the support file search folders (linetypes, hatch patterns and AutoLISP files), each in order, with the default Fonts, Hatches, Linetypes, Lisps and DrawingTemplates folders next to the dll shown read-only under them (a Plugins folder is beside them too, but it holds no search path: see [Command plugins](plugins.md#where-a-plugin-lives)); the Python the PYTHON palette, PYRUN and PYLOAD run scripts with (a `python.exe` or its folder, with the Python found beside it; see [Python in the app](python.md#finding-python)) | none; Python found automatically |
| Security | where AutoLISP and Python code may be loaded from: SECURELOAD, the trusted folders (TRUSTEDPATHS), LEGACYCODESEARCH and the files answered Always Load (see [The Security page](#the-security-page)) | SECURELOAD 1, no trusted folder but the app's own, LEGACYCODESEARCH 0, none |
| Shortcuts | the aliases and the keys | AutoCAD's (see [Editing](editing.md#aliases-and-keys)) |
| Save and export | the save folder, autosave (how often, and its format, DXF or DWG); Template settings: the template folder and the default template for QNEW (see [Drawing templates](#drawing-templates)); Other programs: whether other programs may connect and drive the app (see [Driving the app from another program](remote.md#the-setting)) | empty, which is the user's Documents, 10 minutes, DXF; template folder empty, which is the `DrawingTemplates` folder next to the dll, no default template for QNEW, which is the built-in metric one; other programs allowed to connect |
| Drawing text (file only) | the font a style's missing font is drawn in where the replacement is not found either, the heights and the width factor of drawing text: TEXT and MTEXT start from the text height and write the width factor, and DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write their text at the text height | drawing font Arial, text 2.5, titles 5, width factor 0.8 |

Folder preference lists the folders you add, each editable, and under them the default folder next to `GeometryHelper.Cad.dll`
for each kind, read-only, marked "(default)" with its full path ("(default) not found" where the folder is missing) and an Open
folder button; they are never saved into the settings file. A line under each list says the order. The default folders are
`Fonts`, `Hatches`, `Linetypes`, `Lisps`, `Plugins` and `DrawingTemplates` beside the dll (the dll's folder, not the exe's, so that an app which embeds it, such as
a host that makes model drawings, finds the same ones). Put SHX fonts and big fonts, `.ttf`, `.ttc` and `.otf` files in `Fonts`, `.pat` files
in `Hatches`, `.lin` files in `Linetypes` and AutoLISP files (`.lsp`, and the `.dcl` files they use) in `Lisps`. A font, a pattern file or a linetype file is looked for in three places, in this
order: (1) the folders you set in Folder preference, in order; (2) the default folder of its kind next to the dll and its subfolders (below); (3) the built-in
font (GeometryHelper Simplex), patterns (48) and linetypes (39). A folder that does not exist is passed over without a word. Each default folder is read with its subfolders, at any depth, in one order: depth first, a folder's own files first by name (letters of either case the same, then as written), then its subfolders in the same order, so `Fonts\Company\abc.shx` is found, a file at the top level comes before one in a subfolder, and of two files of one name (two `mine.lin`, a `sheet.dwt` in `DrawingTemplates` and in `DrawingTemplates\Old`) the first in that order wins and the other is named in one line of the debug log. A subfolder that is a junction or a symbolic link, or is hidden or a system folder, is not entered; one that cannot be read (access denied) is passed over and the walk goes on. The folders you add in Folder preference are read as they were, without their subfolders. The
folder of the drawing is not looked in for fonts, patterns or linetypes (xrefs and images are still found relative to the
drawing). TrueType fonts installed in Windows are found as before, first: a style naming a typeface is drawn by the canvas, and a
style naming a `.ttf` file finds it in the Windows Fonts folder; for a typeface Windows does not have, a `.ttf`, `.ttc` or `.otf`
file whose family name is that typeface is looked for in the folders of (1) and then in `Fonts` of (2), and drawn from it with no
installing (and it is not listed as missing); the regular face of a family is preferred to its bold or italic one. Each folder is
listed again at each look and the family names of a file are read once, while its size and time stay as they were. `Fonts` holds
`ghsimplex.shx`, the built-in font as a compiled SHX unifont file of 4 840 bytes, to use elsewhere (put it in AutoCAD's Fonts folder
and a style naming `ghsimplex.shx` draws there as it does here; AutoCAD 2021 gives the same text boxes to four decimals);
`Hatches` holds
`GeometryHelper.pat` and `Linetypes` holds `GeometryHelper.lin`, the 48 patterns and the 39 linetypes of the built-in sets in
AutoCAD's `.pat` and `.lin` formats, as models for your own files; the name `ghsimplex`, `GeometryHelper.pat` or `GeometryHelper.lin` always
means the built-in font or set, so editing the copy changes nothing, and a test keeps the files equal to the font and the sets. The linetypes that carry the names AutoCAD users type (BORDER, CENTER, DASHDOT, DASHED, DIVIDE, DOT, HIDDEN and PHANTOM, each with its `2` and `X2`, and GAS_LINE) have the dash lengths AutoCAD gives them, so that a drawing looks the same in both programs: the file is in millimetres (CENTER `31.75, -6.35, 6.35, -6.35`, HIDDEN `6.35, -3.175`), and loading one into a drawing whose MEASUREMENT is 0 writes it in inches (CENTER `1.25, -0.25, 0.25, -0.25`, HIDDEN `0.25, -0.125`), as AutoCAD takes `acad.lin` for such a drawing and `acadiso.lin` for a metric one. A linetype the drawing has already is never changed, and the `ACAD_ISO` types stay in millimetres in both. `Lisps` ships with one sample routine, `GH-DrawingInfo.lsp`,: every `.lsp` file in it, at any depth, is loaded into each drawing when it opens, after `acaddoc.lsp` and before the Startup Suite, in the order of the walk (the folder's own files by name, then its subfolders), and the folder itself is on the AutoLISP search path after the support folders (see [The Lisps folder](lisp.md#the-lisps-folder)); it is under the program folder, so SECURELOAD always trusts it. `Plugins` holds command plugins and no search path: every `.dll` directly in it, and every folder below it that holds `.dll` files as one plugin, is loaded once when the app starts, in the order of the walk, with no question, as [Command plugins](plugins.md#where-a-plugin-lives) says; the Plugin manager shows it first among the trusted folders. It ships with one sample plugin, the subfolder `DrawingInfo`, whose command `GHDWGINFO` writes what the drawing holds ([the sample plugin](plugins.md#the-sample-plugin)). `DrawingTemplates` is where a drawing saved as a `.dwt` is
put to be used as a template, and ships with the sample `GeometryHelper-mm.dwt` (see [Drawing templates](#drawing-templates)). Each folder has a
`README.txt` that says the same.

The crosshair size (`CrosshairPercent`, CURSORSIZE) is a share of the canvas: at 5 % each line is 5 % of the larger side of
the canvas long, centred on the cursor; at 100 % the lines are infinite, as AutoCAD's, and run from edge to edge of the
canvas (through a viewport, of its frame) wherever the cursor is.

On the Shortcuts page a Keys cell takes the keys pressed in it, as the shortcut key box of AutoCAD's Customize User
Interface does: Ctrl+Shift+D pressed shows `Ctrl+Shift+D` in place of the command's keys, Backspace or Delete clears them,
and Esc gives back those it had when the cell was entered. A key with Ctrl or Alt, or F1 to F24, is taken; a letter alone or
with Shift alone is typing and is not. Aliases are still typed, parted by commas. **Import acad.pgp…**, left of Reset to
AutoCAD defaults, reads the aliases of a `.pgp` file you pick as AutoCAD does (`ALIAS, *COMMAND`, a hyphen kept, `;` starting
a comment, external commands passed over) and merges them, the file's winning: a file of `ZZ, *ZOOM`, `L, *PLINE`,
`C, *CIRCLE`, `QQ, *NOSUCHCOMMAND` and one line of nonsense says "acad.pgp: 1 alias added, 1 changed, 1 unchanged; 1 skipped,
its command not in this app; 1 line not read." An alias of the file that is the name of another command of the app is left
out and counted ("1 alias left out, the name of a command"): a name wins over an alias where a word is typed, so it could
not be used, and the page would show it in red and keep Apply and OK off. AutoCAD 2021's acad.pgp has one, `DXFOUT, *SAVEAS`,
as DXFOUT is a command of its own here. After an import that changed something Apply and OK are on, as they are with any
edit that leaves nothing in red. No acad.pgp is shipped; nothing is taken until Apply or OK.
Each command of the page has a short description of what it does
(`DIVIDE`, "Points or blocks at equal parts of an object").
The defaults are AutoCAD's where it has one. Numbers show as the units say: `FormatNumber(12345.678, 2)` is `12 345.68`, and
with a comma for the thousands `12,345.68`. The thousands cannot be parted by the decimal separator: `ThousandsSeparator =
"Point"` with `DecimalSeparator = "Point"` throws. `SetShortcuts(aliases, keys)` replaces the aliases and keys, and
`ShortcutConflicts(names)` says an alias that is the name of another command ("The alias DU is the name of the command DU");
`ToShortcuts()` gives the [`CadShortcuts`](xref:GeometryHelper.Cad.CadShortcuts) a registry takes.

### What the text settings reach

Once settings are applied, a command line starts TEXT and MTEXT from the text height, and gives them the width factor: TEXT
writes it as the text's width factor, and MTEXT, for a factor other than 1, opens its words with the code AutoCAD writes for it,
`\W0.8000;` for 0.8. The dimension commands DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write the words of the
dimension at the text height, in place of the dimension style's DIMTXT; where that is not the style's, it is kept as an override
of the dimension (as DIMOVERRIDE writes one), so that a CAD that draws the dimension again from its style draws its text as high as
the picture has it. With no settings applied, as in a test, the style's DIMTXT and a width factor of 1 are used.

### In the dialog

The dialog edits a copy of the window's settings, with Reset to defaults, Cancel, Apply and OK. Apply gives them to the
window at once, OK applies and saves them to the file, and Cancel drops them. A value the settings refuse shows why and is not
taken, and a shortcut that clashes shows with whom; Apply and OK wait until every one is put right. Applied at once are the
snaps, the grid, the coordinates of the status bar, and the aliases and keys of every command line open; the canvas takes
the rest.

### The Security page

Security, marked NEW in the list, sets where AutoLISP and Python code may be loaded from, as AutoCAD 2021's Security
Options and its Trusted Locations do, in their words. [Trusted folders and SECURELOAD](lisp.md#trusted-folders-and-secureload)
has the rule each setting feeds.

- **SECURELOAD**: three radios, `Least secure setting- Load executable files from all locations without displaying a
  warning.` (0), `Load executable files from Trusted locations, and display a warning for other locations.` (1, the
  default) and `Most secure setting-- Load executable files from Trusted locations only.  All other executable files will
  be ignored.` (2).
- **Trusted folders for executable files:** the folder of the app first, greyed and marked as always trusted with every
  folder below it, then the entries of TRUSTEDPATHS as they are kept. Add folder… shows the Windows folder picker, which can choose a folder you cannot write in, such as one of Program Files, and adds it; Remove,
  Move up and Move down work on the entry chosen, and Include subfolders adds or takes off its `\...`, so that `D:\Lisp`
  becomes `D:\Lisp\...` and trusts the folders below it too. An entry whose folder is missing, or that does not start at a
  drive or a share, is marked "(not found)" and trusts nothing.
- **When searching for executable files:** `Exclude the Start In or drawing folders (recommended)` (LEGACYCODESEARCH 0)
  and `Include the Start In and drawing folders` (1).
- **Files always loaded: 3** counts the files answered Always Load, and Forget empties the list, so each is asked about
  again; AutoCAD gives no way to take such an answer back.

A folder the app can write in, as most of a user's own folders are, asks first, as AutoCAD's Options does: **Trusted File
Search Path - Security Concern**, `The location specified is not read-only.  What do you want to do?`, `Specifying a
writable folder can leave your computer vulnerable to malicious code.`, with Cancel, which adds nothing, and Continue. The
app finds out by making an empty file in the folder and deleting it on the spot. SETVAR and `setvar` of TRUSTEDPATHS never
ask, nor does AutoCAD.

The page applies with Apply and OK as the others do, Forget included, and Cancel leaves everything as it was; Reset to
defaults gives SECURELOAD 1, no trusted folder and LEGACYCODESEARCH 0 back and keeps the files always loaded, which are the
user's answers. A SETVAR of the three variables at the command line, and a file answered Always Load, are saved to the
settings file at once. They are kept under `security`:

```json
"security": {
  "secureLoad": 1,
  "trustedPaths": "D:\\Lisp\\...;D:\\Python",
  "legacyCodeSearch": false,
  "alwaysLoaded": [ "<SHA-256 of the file's bytes>|C:\\Users\\you\\Downloads\\tools.lsp" ]
}
```

The warning a file outside the trusted folders brings at SECURELOAD 1 is AutoCAD's, **Security - Unsigned Executable
File**, 616 pixels wide over the main window: the file's name, `Unknown Publisher`, its folder, `None` for the issuer and
its date, then Always Load, Load Once and Do Not Load, the last the default. Its link `How do I decide what files to load?`
opens [that section](lisp.md#trusted-folders-and-secureload). The same window asks for a Python file run by PYRUN, loaded
by PYLOAD or picked with the palette's Run file and Load.

## Autosave

Every minute the window looks at the clock, and where `AutosaveMinutes` have passed (10 by default, 0 for never) it saves a
copy of each open drawing that has a file and changes not saved, beside the file: `plan.autosave.dxf` for `plan.dxf` and
`plan.dwg.autosave.dxf` for `plan.dwg`, so that the two open from one folder keep copies of their own. There is one copy of
a drawing, written over at each autosave; there are no numbered copies and no setting for how many are kept (Global
settings had Backups kept, which is gone; a settings file that still has `backupsKept` is read as before and the value
dropped). `AutosaveFormat` (Global settings, Save and export, Saving, **Autosave format**) is DXF by default, or DWG: the copy
is then `plan.autosave.dwg` (`plan.dwg.autosave.dwg` for `plan.dwg`), written by the presenter's DWG writer in the version
the drawing was last saved as, or AutoCAD 2018 for a drawing never saved as a DWG; a drawing the DWG writer refuses gets no
copy that time, and the log says so. The numbered copies older versions wrote (`plan.autosave1.dxf`) are not written or
deleted any more, and the Drawing Recovery Manager still lists and opens them, after the one copy of their drawing.
The user's file itself is never written. A drawing with a command running, or with no file yet, is left for the next time,
and the copies are not listed in the file list.

## Opening a drawing

The app's window opens a drawing in the background (`MainViewModel.OpensInBackground`, set by the window; `OpenAsync`
opens one so whatever it says): the file is read and its display list and pick index made on a worker thread, and the
drawing is shown once they are made. Meanwhile the status bar's [progress meter](#the-progress-meter) shows how far it has
got, "Opening plan.dxf" and its bar, with a Stop button (`OpeningText` still says "Opening plan.dxf: drawing 45 %"), and
the window answers: the view shown pans and zooms, another drawing open is shown from the file
list, and Esc or Stop stops the opening, the command line saying "Opening plan.dxf stopped." A command typed or started
meanwhile is not run, so that none runs on a drawing half drawn: "LINE not run: plan.dxf is still opening. Run it again once
it is shown, or press Esc to stop opening it." One drawing opens at a time; another asked for meanwhile is refused, and the
same one asked again waits with it. The extensions are told of the drawing, and its fonts set, on the window's thread
between the reading and the drawing, as before. A drawing of 1 000 000 entities that held the window for 15 s, its first
frame included, holds it now for 0.2 s at most while it opens, then 0.8 s for its first frame. A test or a screenshot opens
the drawing before `Open` returns, as before.

Esc and Stop are answered within a few milliseconds, whatever the opening is doing. `CadDocument.Open(path, progress,
cancellationToken)` looks at its token every 4 096 records as the text of a DXF is walked, its handles scanned and its
entities of model space listed, and the display is looked at every 4 096 entities from the list of them on; a DWG is looked
at before each page of it is unpacked, before each object is read, before each entity and object is written as DXF and every
4 096 records as that DXF is walked, so that it is stopped as a DXF is (it was looked at once before it was read and not again
until its display was made: a DWG of 300 000 entities, 1.6 s to read, stopped 100, 400 and 1 000 ms in answered 1 351, 1 180 and 545 ms late,
and answers 1 ms late now, 31 ms at the very end). On a DXF of 1 000 000 entities (77 MB) the time from Esc
to the window free, asked at 20 to 2 000 ms into the opening, was up to 0.74 s, and 0.4 to 0.5 s while the file was read,
and is 59 ms at most now. Opened and drawn to the end, the drawing takes the time it took (open and display 5.3 to 5.6 s, 8.2 to 8.6 s under .NET
Framework 4.8), and the display list is the one it was, item for item.

OPEN is the Open dialog of Ctrl+O, with or without a drawing, and with FILEDIA 0 it asks on the command line, `Enter name of
drawing to open:` (as AutoCAD 2021's resources word it): a name with no extension is looked for as `.dwg` and then as `.dxf`, one
with no folder in the folder of the drawing shown and then in the folder the app runs in, and the drawing is opened as Ctrl+O opens
one once the command has ended; `Cannot find the specified drawing file.` where there is none. OPENURL and DXFIN are its aliases.
DRAWINGRECOVERY (DRM) lists on the command line the copies [autosave](#autosave) keeps, those beside the drawings of the Drawings
list and of the drawings open, by folder, drawing and age, the newest first (the one copy before the numbered ones of older versions), `  1  plan.autosave.dxf  12 KB  saved 2026-11-13
08:12`, and "Enter number of the backup file to open <none>:" opens the one chosen as a drawing of its own; a number out of the list
says `Requires a number from 1 to 3.` and asks again. In the window it shows the Drawing Recovery Manager palette instead: the
Backup Files, each with the drawing it is of, when it was saved and its size, Details of the one picked, and Open (or a
double-click) opening it while the palette stays; `No backup files were found beside the drawings listed.` where there are
none. Its thumbnails and AutoCAD's RECOVERALL, which also recovers the external references, are not built; RECOVER repairs a damaged file. QVDRAWING (QVD) shows the Drawings list
and QVDRAWINGCLOSE (QVDC) hides it, QVLAYOUT (QVL) shows the Model and layout tabs at the foot of the window and QVLAYOUTCLOSE
(QVLC) hides them: the presenter's own quick views, with no thumbnails of the drawings or the layouts, which AutoCAD draws.

## The progress meter

The status bar has a progress meter, as AutoCAD's: left of the coordinates, what runs, a bar and the percent, and taking
no room while nothing runs. Opening a drawing in the background shows "Opening plan.dwg", the reading of the file and the
drawing of it from 0 to 100 %, with Stop; SAVE, SAVEAS and QSAVE show "Saving plan.dwg" while the DXF or DWG is written.
The window draws the status bar ten times a second at most while a save runs on its thread
(`MainViewModel.RepaintsProgress`, set by the window), the mouse and keys held back meanwhile.

A command, a plugin or the host starts a meter of its own for any long work, as AutoCAD's ProgressMeter: `Start(label)`,
then `SetLimit(n)` and `MeterProgress()` once a step, or `Report(share)` with a share of 0 to 1, and `Stop()` or a `using`
block, which stops it even where the work throws. A meter given neither is indeterminate and sweeps.

```csharp
using (CadProgressMeter meter = context.Progress.Start("Checking blocks"))   // or model.Progress
{
    meter.SetLimit(blocks.Count);
    foreach (var block in blocks) { Check(block); meter.MeterProgress(); }
}
```

- The meter last started and not yet stopped is shown; one started inside another, as a plugin's during SAVE, takes its
  place, and the outer one comes back when it stops.
- Meters may be moved on any thread. They count in whole percent, and the status bar is told only when the percent
  shown changes: a meter moved 100 000 times tells it 100 times.
- `CadDocument.Open(path, progress)`, `CadDocument.SaveAs(path, progress)` and `CadDrawingFile.Open(path, progress)`
  take any `IProgress<double>`, a meter among them, and tell it a share from 0 to 1, in thousandths, never going back and
  ending at 1. A DWG of 100 000 lines tells it about a hundred times in whole percent as its objects are read or written;
  a DXF, read or written at once from its bytes, tells 0 and 1. Told or not, the files read and written are the same to
  the byte, and the times the same: that DWG saved in 0.67 s and opened in 0.6 s, before and after.

## DWG files

The presenter opens a DWG of AutoCAD 2000 to 2018 as it opens a DXF, read by the library's own
[DWG reader](../dxf/dwg.md), and saves one as a DWG of AutoCAD 2018, the version AutoCAD 2021 saves in.

- Ctrl+O shows Select File, its types Drawing (*.dwg) first, then DXF (*.dxf), as AutoCAD 2021's; a DWG or DXF dropped on the DRAWINGS list opens (dropped on the drawing area it is inserted as a block, as AutoCAD 2021 inserts it: `-INSERT` of the file, then the insertion point; with no drawing open it opens);
  and other files go as on the window (a `.lsp`, `.fas` or `.vlx` loads as a typed `(LOAD "...")`, a `.scr` runs
  as SCRIPT, a `.arx`, `.dbx` or `.dvb` is refused, and any other file gets the Drag and Drop box, as
  [AutoLISP](lisp.md#dropping-a-file-on-the-window) says); the file list lists the DWG and DXF files of the folder. A file is read as a DWG where its first bytes
  say so ("AC1032" of AutoCAD 2018), whatever its name; a DWG of a version not read, R14 among them, is said on the command
  line, "Cannot open old.dwg: The DWG version AC1014 (R14) is not read yet; …", and not opened as a recovered DXF.
- Save As (Ctrl+Shift+S, SAVEAS) shows Save Drawing As with AutoCAD 2018 Drawing (*.dwg) first and a DXF of each version after it,
  the newest first, as AutoCAD 2021 names them: AutoCAD 2018 DXF, AutoCAD 2013/LT2013 DXF, 2010/LT2010, 2007/LT2007,
  2004/LT2004, 2000/LT2000 and R12/LT2 (*.dxf); the type of the drawing's file is chosen first, the DXF of the version it was last
  saved as or of its own version, and DWG for a drawing opened from a DWG and for a new one. The type chosen is what is written:
  `CadDocument.SaveAs(path, version, progress)` turns the drawing into that version, as `DxfDatabase.Save(path, version)`
  does, and Save (Ctrl+S, QSAVE) writes it so again (`CadDocument.SaveVersion`), as AutoCAD's QSAVE keeps the format of the
  last Save As. An R12 DXF is what AutoCAD writes of the drawing as R12 (see the drawing guide): the hatches, multileaders, texts of
  several lines and the rest R12 lacks are the lines, texts and polylines they break into, and said after the save. A name ending in .dwg is written as a DWG, of AutoCAD 2018 or, where the type of 2013, 2010, 2007, 2004 or 2000 was chosen (the list holds
  a DWG type for each of those six, then the DXF types), of that version, any other name as a DXF; a DXF type chosen for a
  name ending in .dwg is refused, "Cannot save a.dwg: A DWG is written as AutoCAD 2018 (AC1032), 2013 (AC1027), 2010 (AC1024),
  2007 (AC1021), 2004 (AC1018) or 2000 (AC1015) only, not as a DXF of AC1024." (the DXF of 2018 is the DWG of 2018) QSAVE writes the DWG of the version last chosen again. Save of a
  drawing opened from a DWG writes it back as a DWG. A drawing of R14, which the list has no type for, is offered "DXF (*.dxf)"
  as well, its own. With FILEDIA 0 SAVEAS asks on the command line what the dialog would, in AutoCAD 2021's words: "Current file
  format: AutoCAD 2018 Drawing" and "Enter file format [R14(LT98&LT97)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/2013(LT2013)/2018(LT2018)/Dxf/Template/Standards]
  &lt;2018&gt;:", Dxf as the default for a drawing that is a DXF. 2018, 2013, 2010, 2007, 2004 and 2000 (or their LT names, or the listed form) are the DWG of that version; R14
  and Standards are refused with a line ("AutoCAD R14(LT98/LT97) Drawing (*.dwg) is not written: a DWG is saved as
  AutoCAD 2018, 2013, 2010, 2007, 2004 or 2000 (2018, 2013, 2010, 2007, 2004, 2000).") and the command ends; Template saves a
  drawing template (see [Drawing templates](#drawing-templates)). Dxf asks its settings, "Current DXF settings: Precision= 16 Format= ASCII
  Preview= No Version= 2018" and "Enter decimal places of accuracy (0 to 16) or [Version] &lt;16&gt;:", where Version asks "Enter version
  [R12(LT2)/2000(LT2000)/2004(LT2004)/2007(LT2007)/2010(LT2010)/2013(LT2013)/2018(LT2018)] &lt;2018&gt;:" (R14 refused, "R14 is not written: a
  DXF is saved as R12 or R2000 and later."); the places of accuracy are not set (coordinates are written in full, said for a
  number under 16), and Binary, Preview and select Objects, which AutoCAD offers there, are not. Then "Save drawing as
  &lt;Drawing1.dwg&gt;:" for the file, a name of another extension than the format's given its own (.dwg or .dxf), and a name of the
  other (.dxf for the DWG, .dwg for a DXF) refused. QSAVE of a drawing with no file asks the same.
- DXFOUT writes a DXF copy of the drawing and leaves the drawing as it was (its file, its version, whether it has changes not
  saved): the window's Create DXF File dialog listing the DXF types (`CadDocument.SaveCopy(path, version)`), or with FILEDIA 0
  "Enter DXF file name &lt;Drawing1&gt;:" then the settings as above, Enter taking the drawing's own version (2018 for a drawing that is a DWG).
- What the version could not hold is said after "Saved plan.dxf.", a sentence each (`CadDocument.LeftOutOfDxf`): "Left out of
  the drawing, which AC1015 cannot hold as it is: 3DSOLID 2, VISUALSTYLE 24." or "Replaced by the block each is drawn with,
  written as AC1015 cannot write it: ACAD_TABLE 2.".
- What the DWG writer does not write yet is left out of the file and said after "Saved plan.dwg.": "Left out of the DWG,
  not written yet: RASTERVARIABLES 1." (`CadDocument.LeftOutOfDwg` holds it). Saved as a DXF of the drawing's own version,
  nothing is left out.
- A DWG is attached by XATTACH, -XREF and the External References palette, and inserted by INSERT, -INSERT and the Blocks
  palette's Other drawing, as a DXF is; WBLOCK writes a DWG where the file named ends in .dwg.
- Autosave keeps its one copy of a DWG as a DXF file beside it (or a DWG, with Autosave format DWG), the DWG's extension kept
  in its name: `plan.dwg.autosave.dxf` for `plan.dwg`, where `plan.dxf` beside it has `plan.autosave.dxf`.
- A new drawing (NEW, QNEW, Ctrl+N) is named Drawing1.dwg, then Drawing2.dwg, as AutoCAD names its own, so that Save As and
  QSAVE offer it as a DWG first. Of the presenter's own template (Open with no Template - Metric, what QNEW starts where no
  default template is set) it has the layer 0 and no other, as AutoCAD's Open with no Template - Metric has; a drawing's
  layers come from its template, not from a list in Global settings (there is none), so put the layers you want in a `.dwt`
  in the `DrawingTemplates` folder. The current layer is 0, and a drawing opened from a file is left as it is. A new
  drawing of a template file has what the template has instead (see [Drawing templates](#drawing-templates)).

### Drawing templates

A template is a drawing whose layers, styles, blocks, layouts with their page setups and variables every new drawing takes.
AutoCAD's `.dwt` is a DWG with that extension; a `.dwg` or a `.dxf` is as good a template.

- **NEW** shows Select Template (FILEDIA 1): the templates of the template folder listed, each a `.dwt`, `.dwg` or `.dxf` by
  name with what it is, the first selected and its description (the text its Template Options kept) under the list, Open to
  start from the one selected (a double click too), Browse… for a file in another folder (Drawing Template, Drawing and DXF
  types), and Open with no Template - Metric or Imperial. The new drawing is untitled, Drawing1.dwg and on, takes the
  template as it is and is listed under Open drawings until it is
  saved. The metric one has millimetres, ISO-25 and limits of 420 by 297; the imperial one inches, the dimension style
  Standard (arrowheads and text 0.18, four places) and limits of 12 by 9. A template that cannot be read is said on the
  command line and starts nothing; Cancel starts nothing. The application menu's New › Drawing shows the same dialog; with
  no drawing open NEW needs none. With no dialog to show, NEW starts the drawing QNEW does.
- With FILEDIA 0 NEW asks "Enter template file name or [. (for none)] <acadiso.dwt>:" (the default template of Global
  settings where one is set): the file as typed, else in the template folder, `.dwt` given to a name with no extension,
  Enter taking the default, `acadiso` and `acad` standing for the built-in metric and imperial templates where no file of
  the name is found, and a period asking "Enter measurement [English/Metric] <Metric>:" for a drawing of no template. A file
  not found is said, `"office.dwt": Can't find file.`
- **New from template** (the start screen's button right of New, and Ctrl+Shift+N, with no drawing open or with one) runs NEW
  with Select Template opened in the template folder, `DrawingTemplates` where none is named. The dialog is shown with one
  template there as with many, and with none it lists nothing and says how to make one (save a drawing as `.dwt` and put it
  there). The new drawing is Drawing1.dwg, untitled, with the template's layers, styles, blocks, layouts and units.
- **QNEW** (and the New button of the quick access bar, Ctrl+N and the start screen's Blank drawing) starts the drawing
  from the default template of Global settings (Save and export > Template settings > Default template for QNEW) without
  asking, the built-in metric template where none is set; one set but not found is said, "The default template office.dwt
  for QNEW is not found; the built-in template is used.", and the built-in one used.
- **SAVEAS to a template**: Save Drawing As lists AutoCAD Drawing Template (*.dwt) after the DWG types, and the
  application menu's Save As › Drawing Template opens it with that type chosen, in the template folder (made where it is
  missing) and the drawing's name given `.dwt`. A name ending in `.dwt` saved with the DWG type is a template too. Template
  Options asks the description and the measurement (English or Metric, the drawing's own offered; New Layer Notification
  is shown greyed, the presenter keeps no reconciled layers); OK writes a DWG of AutoCAD 2018 with the description in its
  template section and MEASUREMENT as chosen, and the drawing is then known by that file, as AutoCAD's is (Save writes it
  again, the description kept; `CadDocument.SaveAsTemplate`, `CadDocument.TemplateDescription`, `CadTemplate.DescriptionOf`).
  With FILEDIA 0, SAVEAS Template asks "Save drawing as <Drawing1.dwt>:" ("other.dwg" becomes "other.dwt"), "Enter template
  description:" (Enter for none) and "Enter measurement [English/Metric] <Metric>:".
- The template folder is the one of Global settings (Save and export > Template settings > Template folder, TEMPLATEPATH).
  Where it is empty it is the `DrawingTemplates` folder beside the dll, whose subfolders are listed too (by the walk above, a template of the same file name below the top is not listed twice), which the build copies with a `README.txt` and the
  sample template `GeometryHelper-mm.dwt` (below), and which Global settings > Folder preference lists with Open folder: the folder Select Template starts
  in, where Save As › Drawing Template saves, and where a template named with no folder (typed at NEW with FILEDIA 0, or
  the default template for QNEW) is looked for. To make a template, save a drawing as `.dwt` (SAVEAS, Files of type:
  Drawing Template, or the application menu's Save As › Drawing Template) and it is there to use. The folder
  `%AppData%\GeometryHelper\Drawing\Template`, where templates were kept before, is taken in its place only while it holds a
  template and `DrawingTemplates` holds none; the first template saved into `DrawingTemplates` ends that. A program folder
  you cannot write to (Program Files) cannot be saved into: name a folder of your own in Template folder. The built-in
  templates need no folder.
- **The sample template**, `GeometryHelper-mm.dwt` in `DrawingTemplates`, is listed by Select Template with its description
  ("GeometryHelper metric sample: millimetres, layers for a Tekla Structures detail ...") and starts an untitled drawing for a
  detail drawn at 1:1 in millimetres and printed on A3: MEASUREMENT 1, INSUNITS 4, distances to two decimal places, limits of
  42 000 by 29 700 (an A3 sheet at 1:100), LTSCALE and PSLTSCALE 1, nothing drawn, the current layer 0. Its layers (AutoCAD
  colour index, lineweight in millimetres): 0; Axis 1, CENTER, 0.18; Outline 7, 0.50; Thin 8, 0.18; Hidden 6, HIDDEN, 0.25;
  Dimension 3, 0.18; Text 2, 0.25; Hatch 9, 0.13; Rebar 4, 0.35; Title block 7, 0.35; Viewport 8, not plotted; Defpoints 7,
  not plotted; the linetypes are those of `GeometryHelper.lin`, so CENTER is `31.75, -6.35, 6.35, -6.35` and HIDDEN `6.35, -3.175`. Its text styles are Standard, Notes and Titles in `ghsimplex.shx`
  (height 0, width factor 0.8) and Arial in `arial.ttf`; no font of Autodesk is named. Its dimension styles are GH, the
  current one (arrowheads closed and filled 2.5, text 2.5 in Standard above the line, extension lines offset 1.5 and
  extended 1.5, no decimals, DIMLUNIT 2, every colour and lineweight by layer), GH-Annotative (GH made annotative) and
  Standard, given the look of GH so that none is in inches. Its multileader style GH (text and arrowheads 2.5) is current,
  and its layouts are Model and A3, a sheet of 420 by 297 millimetres on its side. The file is written by the library, not by
  hand: `CadTemplate.CreateSample()` makes the drawing and `SaveAsTemplate` writes it, and a test keeps the shipped file equal
  to it in every layer, style, layout and variable (handles and times are not compared). To write it again, set the
  environment variable `GEOMETRYHELPER_WRITE_SAMPLE_TEMPLATE` to the full path of the file and run the test
  `TheSampleTemplateIsWrittenWhereTheVariableSays` of `SampleTemplateTests`. AutoCAD 2021 opens it with AUDIT finding no
  errors; where it has no `ghsimplex.shx` on its path it says `Substituting [simplex.shx] for [ghsimplex.shx].` and goes
  on, and with the font beside the drawing it says nothing.

With FILEDIA 0 a command asks on the command line for the file it would show a file dialog for, as AutoCAD 2021 asks:

| Command | Asks | Then |
|---|---|---|
| QSAVE of a drawing with no file, SAVEAS | Enter file format [...] &lt;2018&gt;:, for Dxf its settings, then Save drawing as &lt;Drawing1.dwg&gt;: | saves there; a name with no extension takes the format's: .dwg for 2018, .dxf for Dxf; Template asks the file, a description and the measurement, and saves a .dwt |
| NEW | Enter template file name or [. (for none)] &lt;acadiso.dwt&gt;: | starts the drawing from that template, or of none (a period) |
| DXFOUT | Enter DXF file name &lt;Drawing1&gt;:, then the DXF settings | writes the copy, the drawing keeps its file |
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

CLOSEALL closes every drawing the window has open, as Close All Drawings of the file list does: the commands running are ended, and
where drawings have changes not saved the window asks once for all of them (one asked alone gets its own question). Save
saves each in turn, Don't save closes them as they are, and Cancel, or a save that fails, closes none. SAVEALL saves every open
drawing that has changes, each to its file in the order of the files (a drawing with no file asks its Save As), leaves the drawing that
was shown shown, and says `Saved 3 drawings.` or `No drawing has changes to save.`; it asks nothing else. Both do their work once the
command has ended, as CLOSE does, and both are the window's own commands, so a session with no window does not know them.

## The window

The window is laid out as AutoCAD's own: a ribbon of tabs and panels with the layer box and a properties box, Properties on the
right, the command line and the status bar at the bottom, and the Model and layout tabs at the bottom left of the canvas.
A tab clicked, LAYOUT Set, TILEMODE and CTAB are one undo step each, named as AutoCAD 2021's U says it (LAYOUT for a tab and LAYOUT Set, TILEMODE, CTAB): U goes back to the space shown before and REDO forward,
with the view each space had kept and no copy of the drawing (see [Editing](editing.md#model-space-through-a-viewport)). The Undo and Redo
buttons, the canvas menu and Ctrl+Z take a wheel turn made less than 80 milliseconds before as a step first.
The panel on the left lists the drawing files, and has no layouts or layers. The ribbon has, among its tabs, Insert, Annotate and View, a Plugins
tab that shows only while a plugin has added a panel to it (see [Command plugins](plugins.md)), a Settings tab, and the tabs a host adds.

### The keys and the mouse

| Key | Does |
|---|---|
| Esc | closes the AutoComplete list; then cancels the command; then clears the selection |
| Space, Enter | runs the row chosen of the AutoComplete list; else ends a command or a prompt; Enter alone runs the last command again |
| Up, Down | move the row chosen of the AutoComplete list; with the list closed, recall what was typed before |
| Ctrl+O | opens a DWG or DXF (see [DWG files](#dwg-files)) |
| Ctrl+W | switches selection cycling on and off, as AutoCAD 2021 |
| Ctrl+S, Ctrl+Shift+S | saves, saves as |
| Ctrl+N | runs the new document an extension offers (`MainViewModel.NewDocumentOffer`); where none does, as in GeometryHelper CAD, makes a new drawing (NEW) |
| Ctrl+Shift+N | starts a new drawing from a template: Select Template opened in the `DrawingTemplates` folder (see [Drawing templates](#drawing-templates)), with or without a drawing open |
| Ctrl+Z, Ctrl+Y | undo, redo |
| Delete | erases the selection |
| F2 | the text window (TEXTSCR): the command history in a resizable list with Copy; F2 or Esc inside it closes it |
| F3, F7, F8, F9, F10, F11, F12 | OSNAP, GRID, ORTHO, SNAP, POLAR, OTRACK, DYN |
| F5, Ctrl+E | the next isometric plane, Left, Top, Right (SNAPISOPAIR), written "<Isoplane Top>"; a command running goes on |
| Ctrl+0 | the clean screen (CLEANSCREENON, CLEANSCREENOFF): the ribbon, Properties and the Drawings list hidden, the command line kept; pressed again, shown as they were |

The function keys, Delete and the Ctrl keys of [the defaults](editing.md#aliases-and-keys) are rows of the Shortcuts category and can be changed. F5 and Ctrl+E turn the isoplane while no shortcut is given them.

| Mouse | Does |
|---|---|
| wheel | zooms about the cursor, 1.2 times a notch (ZOOMWHEEL 1 turns the direction round); through a viewport, its view (unless its display is locked). In model space, on the paper and through a viewport alike, the rolls made before the view is still are one gesture: one entry of the ZOOM Previous list of the space or the viewport, and one undo step named ZOOM, which U and Ctrl+Z take back and REDO and Ctrl+Y put again |
| middle button dragged | pans, recorded as the wheel is, a step named PAN |
| middle button double-clicked | zooms to the extents, the paper included in a layout; through a viewport, its view to model space's extents; one ZOOM Previous entry and one undo step, as ZOOM Extents typed |
| left double click in a layout | anywhere inside a viewport's frame (the box the first click opened ends) works through it (MSPACE); off every frame goes back to the paper (PSPACE); a click in another viewport's frame works through that one |
| MODEL / PAPER on the status bar, Ctrl+R | in a layout a button, raised on the paper and pressed through a viewport: MSPACE or PSPACE; Ctrl+R the next viewport; on the Model tab a label. The scale beside it opens its list: in a layout the viewport's (Scale to fit, the standard scales, Custom...), grey while the padlock after it locks the viewport's display; on the Model tab the annotation scale (CANNOSCALE) |
| left click, no command running | picks within the pickbox; Shift+click adds or removes; a click on nothing starts a box, the implied window: the command line asks "Specify opposite corner or [Fence/WPolygon/CPolygon]:", and DYN shows it with two fields, the X and the Y of the cursor itself. The next click, or a point typed, ends the box; F, WP or CP turns it into a fence or a polygon whose first point is the corner, asking its next points until Enter; Esc cancels it, "*Cancel*" after the prompt in the history. Its corners take no object snap |
| click / double click in DRAWINGS | selects the row / opens it; the right-click menu is AutoCAD 2021's file tab menu: New..., Open..., Save, Save As..., Save All; Close, Close Selected Drawings (shown only with several rows chosen), Close All, Close All Other Drawings; Copy Full File Path, Open File Location. Save, Save As and Close act on the row clicked and are grey where its file is not open (Open... opens it); the last two are grey for a drawing never saved; Close All Other Drawings is grey unless two drawings are open; a right click on the empty part of the list offers New..., Open..., Save All and Close All |
| Shift+right-click, a point or objects asked | the object snap menu (shown at "Select objects:" and at a prompt for one object as at a point, where the modifiers pick at the point they give and a snap takes the click); the snap chosen is taken for that point alone. Its snaps are named and grouped as in AutoCAD 2021's menu: Endpoint, Midpoint, Intersection, Apparent Intersect and Extension; Center, Geometric Center, Quadrant and Tangent; Perpendicular, Parallel, Node, Insert and Nearest. Each item takes AutoCAD's access key while the menu is open: F From, k Temporary track point, t Mid Between 2 Points and Point Filters (the key moves between the two), E Endpoint, M Midpoint, I Intersection, A Apparent Intersect, x Extension, C Center, Q Quadrant, g Tangent, P Perpendicular, l Parallel, d Node, s Insert, r Nearest, N None, O Osnap Settings...; Geometric Center has none, as in AutoCAD 2021's menu. Point Filters parts .X, .Y and .Z from .XY, .XZ and .YZ with a line, as AutoCAD's does. The right-click menu of a block reference takes R for Reset Block |
| left button dragged, no command running | with Select by dragging on, a box as above; off, the box waits for a click at its other corner. Left to right it is a window, blue a quarter strong over the drawing with a solid edge; right to left a crossing, green with an edge dashed 4 pixels on and 2 off; both edged in the crosshair's colour, as AutoCAD 2021 draws them. While the box or a lasso is dragged the cursor is the cross alone, the pickbox coming back once it ends, and a box carries AutoCAD's cursor badge 12 pixels right of the cursor and 21 above: a square 9 pixels across filled blue for a window, dashed round a green middle for a crossing. SCALE carries its own badge, a square with a smaller one filled in its lower left corner, while it runs. The box of "Select objects:" W, C or BOX is drawn the same way and alone, with no rubber band from its first corner |
| cursor over an entity | drawn 2 pixels wide in its colour taken halfway to the selection colour |
| cursor over an object with a hyperlink, no command running | a tooltip beside the cursor: the link's text, then `CTRL + click to follow link`; Ctrl+click follows the link and selects nothing (see [Drawing properties, hyperlinks, GROUPEDIT, ATTREDEF and MEASUREGEOM Quick](editing.md#drawing-properties-hyperlinks-groupedit-attredef-and-measuregeom-quick)) |
| a grip clicked | makes it hot and starts the grip modes, `** STRETCH **` first (see [Grip modes](editing.md#grip-modes)); Shift+click makes several hot; dragged, stretches it where it is let go. The cursor resting 400 ms on a polyline's vertex or segment-middle grip, the end of a line or an arc, the middle of an arc, the point of a spline or the grip of a hatch opens its menu (Stretch Vertex, Add Vertex, Remove Vertex; Stretch, Add Vertex, Convert to Arc or Convert to Line; Stretch, Lengthen; Stretch, Radius; Stretch, Hatch Scale, Hatch Angle, Origin Point; see [Hover menus](editing.md#hover-menus)) |
| left click, a command running | answers its prompt: a point taken through the snaps, with a rubber band from the base point; or objects, by a click or a box. At "Select objects:" a click on nothing starts the implied window inside the command, as AutoCAD 2021 asks it there: "Select objects: Specify opposite corner or [Fence/WPolygon/CPolygon]:", the next click or a point typed ending the box, F, WP or CP turning it into a fence or a polygon from the corner; what it takes is gathered ("2 found") and "Select objects:" is asked again. Its corners take no object snap; Esc cancels the command |
| right click, no command running | AutoCAD 2021's shortcut menus, as its SHORTCUTMENU 11 has them. With nothing selected the Default menu: Repeat and the command run last, Clipboard (Cut, Copy, Copy with Base Point, Paste, Paste as Block, Paste to Original Coordinates), Undo, Redo, Pan, Zoom, Quick Select..., QuickCalc (the palette, QUICKCALC), Find... and Options.... With objects selected the Edit menu: Repeat, the object menu AutoCAD merges in for the objects selected, Clipboard, Erase, Move, Copy Selection, Scale, Rotate, Draw Order (Bring to Front, Send to Back, Bring Above Objects, Send Under Objects), Group (Group, Ungroup), Add Selected (ADDSELECTED), Select Similar (SELECTSIMILAR), Deselect All, Quick Select..., QuickCalc, Find..., Properties and Quick Properties (QUICKPROPERTIES). With cells of a table selected, the cell menu. The Group submenu's Add to Group and Remove from Group start GROUPEDIT with the option ready, and an object with a hyperlink adds a Hyperlink submenu (Open, Copy Hyperlink, Edit Hyperlink…) before Clipboard. Each item takes the access key of AutoCAD's menu file; the items AutoCAD has and the presenter does not (SteeringWheels) are grey, "Not built yet.", and AutoCAD's Action Recorder submenu is left out, as the recorder will not be built; Convert PS to MS (Convert MS to PS on the paper) starts CHSPACE The object menu is that of AutoCAD 2021's menu file for the kind of object, OBJECT_ for one object and OBJECTS_ for several of one kind, none for objects of two kinds or of a kind with none (a line): for one text Edit... (in place), for one MTEXT Mtext Edit..., for dimensions Dimension Style (the drawing's first six styles, each set on them), Precision (0 to 0.000000, as Properties sets it) Remove Style Overrides (DIMOVERRIDE's Clear on them), Edit Constraint and Dimension Name Format (live for a dimension that shows a dimensional constraint, grey with the reason for any other: Edit Constraint starts -PARAMETERS on its name and leaves "Enter expression" to be typed, Name Format gives the words Value, Name or Name and Expression, one undo step); Dimension Style also has Save as New Style... (-DIMSTYLE Restore of the one dimension, then Save asking the name on the command line) and Other... (a menu of the styles beyond the first six), for one polyline Polyline (Edit Polyline, Close, Open, Join, Reverse, Width, Curve Fit, Spline Fit, Decurve, each PEDIT on it), for one hatch Hatch Edit..., Set Origin, Set Boundary (HATCHSETBOUNDARY) and Generate Boundary (HATCHGENERATEBOUNDARY), for one block reference Reset Block and Block Editor (and Edit Attribute... where it has attributes), for one xref Edit Xref In-place (REFEDIT), for xrefs Open Xref and External References..., for viewports Display Viewport Objects and Display Locked (Yes, No) and Remove Viewport Overrides for All Layers, for one table AutoCAD's table menu (Size Columns Equally and Size Rows Equally giving each the mean, one undo step, Remove All Property Overrides, Table Indicator Color...), for one spline Spline (Display Fit Points, Display Control Vertices, Add Fit Point, Add Fit Kink, Add Control Vertex, Delete Control Vertex, Elevate Order, Convert to Polyline, Join, Reverse Direction and Close, each SPLINEDIT with that option on it), for viewports also Viewport Clip (VPCLIP), for images Image (Adjust, Clip for one image, grey for several as IMAGECLIP clips one, Transparency, External References); Annotative Object Scale (Add Current Scale, Delete Current Scale and Add/Delete Scales... run AIOBJECTSCALEADD, AIOBJECTSCALEREMOVE and OBJECTSCALE on the objects selected; Synchronize Multiple-scale Positions is grey: the position each scale keeps is not read), Table Style (Save as New Table Style... and Set As Table in Current Table Style are grey: a table style keeps no starting table), Export..., Maximize Viewport, Shade plot, 3D Edit Bar and the rest the presenter does not build are grey. A right click that SHORTCUTMENU gives no menu is Enter |
| right click, a command running | Enter where the prompt offers no options, as at "Specify first point:" or "Select objects:"; where it offers some, the Command menu: Enter, Cancel, Recent Input (the points typed at prompts for a point, or the numbers typed at prompts for a distance, an angle or a whole number, written in the drawing's units (100.0000,0.0000 at LUNITS 2 and LUPREC 4), and at Select objects the sets answered, "Selection set (2 items)", newest first, ten at most, each chosen as if typed or selected again; shown once there are any), the prompt's options ("Close" and "Undo" at LINE's third point, each chosen as if typed), Osnap Overrides (the Shift+right-click menu's items, grey where no point or object is asked), Pan and Zoom, which run 'PAN and 'ZOOM inside the command (grey in ZOOM, as AutoCAD's menu greys them there), QuickCalc, which opens the QuickCalc palette beside the command and leaves its prompt as it was, and SteeringWheels, grey. While a grip edit runs, the Grips menu: Enter (the next mode), Stretch, Move, Rotate, Scale, Mirror, Base Point, Copy, Reference (in ROTATE and SCALE), Undo and Exit |

SHORTCUTMENU, set by SETVAR or typed alone and kept in the settings file as AutoCAD keeps it in the registry, chooses the
menus as AutoCAD 2021's bits do, 0 to 31 ("Requires an integer between 0 and 31."), 11 at start: 1 the Default menu, 2
the Edit menu (and the cell menu), 4 the Command menu at every prompt, 8 the Command menu where the prompt offers
options, 16 a menu only for a right click held a quarter of a second or longer, a shorter one Enter. SHORTCUTMENU 15
gives "Specify first point:" the Command menu; 10 makes a right click with nothing selected Enter, running the last
command again.

PAN, ZOOM, CAL, SETVAR and the settings ORTHO, POLAR, OSNAP, GRID and SNAP run inside a command, typed with an apostrophe
at any prompt but one for text, or (Pan and Zoom) chosen from the Command menu, as AutoCAD 2021 runs 'PAN and 'ZOOM: their prompts are written after ">>"
(">>Specify first corner:"), and once they end "Resuming LINE command." is written and the command's prompt asked
again, what "Select objects:" gathered still gathered and the last point as it was. 'ZOOM inside a transparent PAN
nests in it, each resumed in turn; 'ZOOM at ZOOM's own prompt gets ZOOM's "Requires a distance, numberX, or option
keyword."; 'SETVAR asks ">>Enter variable name or [?]:" and its value, and 'ORTHO, 'POLAR, 'GRID and 'SNAP do what they do at the Command prompt, while 'OSNAP opens Drafting Settings on the Object Snap tab and the command resumes (it does not toggle the object snaps: F3 and the status bar do); 'CAL is the third: its prompts read ">>>> Expression:", it writes no value, and once the command resumes the value is
typed at the prompt that was waiting (a point as 11,21,0, a distance as 7); another command typed with an apostrophe gets
"** That command may not be invoked transparently **", the text then going to the prompt; Esc ends the command interrupted
too. 'ZOOM at the Command prompt is ZOOM. DIST, ID, LAYER, COLOR, LINETYPE, LWEIGHT, UNITS, QUICKCALC, DSETTINGS and TEXTSCR
run inside a command the same way (`'DIST` asks `>>Specify first point:`, `'ID` `>>Specify point:`; a transparent ID, like
a transparent DIST, leaves the point the command is drawn from where it was), and the name of any system variable with an
apostrophe asks its value as SETVAR does: `'PDMODE` asks `>>Enter new value for PDMODE <0>:`. A command that shows a dialog
runs its command-line form while it waits, as AutoCAD's console does: 'LAYER asks -LAYER's options (`>>Enter an option
[Set/New/ON/...]:`), 'COLOR, 'LINETYPE, 'LWEIGHT and 'UNITS those of -COLOR, -LINETYPE, -LWEIGHT and -UNITS. A fifth command
inside four is "Commands may not be nested more than 4 deep", the text going to the prompt. MEASUREGEOM
is not transparent, as in AutoCAD.

A point typed at "Select objects:" picks as a click does, written after the prompt: "Select objects: 50,0" then
"1 found"; one on nothing starts the window, "Select objects: 150,150" then "Specify opposite corner:", as AutoCAD 2021's console does.

While MOVE, COPY, ROTATE, SCALE or MIRROR waits for its second point, a copy of the objects follows the cursor, each in
its own colour, as AutoCAD 2021 drags them, with a rubber band dashed amber from the base point, and no square marks the
base point, as none of AutoCAD's prompts marks one; an XLINE or a RAY among
them is drawn across the view as the canvas draws it, moved, turned, scaled or mirrored with the rest. ROTATE and SCALE
move it by a Reference too, and an array shows every copy while its options are offered. Where the objects are to leave
their place, at MOVE's second point and at ROTATE's and SCALE's angle and factor without Copy, they are drawn faded where
they stand, a fifth of their colour, rather than highlighted: a white line on black shows a grey of 51 while SCALE asks
its factor, and its copy at the cursor white. COPY and MIRROR leave them as they are. SCALE's factor is the distance from
the base point to the cursor, as in AutoCAD, so with the cursor 0.5 from the base point the copy is half the size and
the box at the cursor shows 0.50 alone, not a distance and an angle.

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
shows the cell alone, a rectangle from the first corner to the cursor, as AutoCAD's corner prompt draws it, and ALIGN moves the ghost by its first pair and turns it by its second. -ARRAY shows its copies too
while the distance between columns is shown by two points, and in a Polar array while the angle to fill and the angle
between items are asked, each measured from the centre to the cursor, and while it asks whether to rotate them.
PASTECLIP and PASTEBLOCK draw the Clipboard's objects at the cursor, held by their base point, while they ask the
insertion point, and the text of another app as the MTEXT it becomes. The objects are drawn once and moved with the
cursor, a text, an MTEXT and a dimension's words moved as a line is, by adding to the numbers of their letters, and an
image alone drawn anew at each move: a Clipboard of 6 000 lines takes under 1 ms a move, and one of 5 000 TEXTs and
1 000 MTEXTs, with 300 lines and 20 dimensions, 12 ms where laying the texts out at each move took 60 to 70.

### The command line

With a drawing open and no command running, the command line lists what matches the letters typed once no key has come
for 300 milliseconds, as AutoCAD 2021's AutoComplete. LA typed lists LAYER first with "alias LA" at its right, then the
commands that begin with LA, the ones used most first and the rest by name, then the system variables that begin with LA
(each marked {x}), then, below a rule, the names with LA inside: CLAYER, PLAN, POLAR. The first row is chosen: Enter or
Space runs it, the arrows move it, a click runs another and Esc closes the list and clears what is typed, as AutoCAD's does. Typing on with the
list open updates it at once. The list shows five rows at a time, at the command line and under the box of dynamic input
alike, the rest scrolled to with the wheel, and the row the arrows choose is scrolled into sight: A typed shows ARC,
ATTDEF, ALIGN, AREA and ARRAY, the others below.

- **Most used first.** Each command run is counted, and the counts are kept in the settings file, so that a command used
  three times comes before one used once among those that begin with the same letters.
- **Synonyms.** ROUND is offered as `ROUND (FILLET)` and runs FILLET; likewise BEVEL (CHAMFER), DELETE and REMOVE (ERASE),
  DUPLICATE (COPY), FLIP (MIRROR), TURN (ROTATE), RESIZE (SCALE), SYMBOL (INSERT) and PRINT (PLOT).
- **AutoCorrect.** A name no command has, followed by a command, is noted: LIEN typed three times, each followed by LINE,
  has LINE offered first, marked AutoCorrect, the next time LIEN is typed. The corrections are kept in the settings file.
- **Search Content.** Below the commands and variables, under a rule, come the drawing's named objects, as AutoCAD
  2021's: its blocks, layers, hatch patterns, text styles and dimension styles, in that order, each kind under a header
  row with its count at the right (Blocks, Layers, Hatch Patterns, Text Styles, Dimension Styles) and its names indented
  below by name, those that begin with the letters first. WAL typed in a drawing of the block WALL_TAG and the layers
  WALLS and WALL-FINISH lists `Blocks 1` over `WALL_TAG`, and `Layers 2` over `WALL-FINISH` and `WALLS`. Every kind
  opens expanded; a click on its header, or Enter or Space with the header chosen (the arrows walk over the headers
  too), collapses it to the one row and keeps the list open, and again expands it. A kind collapsed stays collapsed in
  the lists after while the app runs. The pointer over a hatch pattern shows its thumbnail beside the list, 96 pixels
  square, drawn by the app from its own pattern lines (design CmdContentSection). A kind's keyword
  lists every name of the kind: BLOCK or INSERT, LAYER, HATCH, TEXTSTYLE or STYLE, DIMSTYLE. Choosing a layer makes it
  current through CLAYER, a block runs -INSERT with its name, which asks the insertion point, a hatch pattern starts
  HATCH with it, and a text style or a dimension style is made current. The content is searched at the command line
  only: with DYN on, the list at the cursor holds commands and variables alone, as AutoCAD's help says of the Dynamic
  Input tooltip.

AutoComplete works at the Command prompt only. AutoCAD 2021's help (Input Search Options, the command line suggestion
list, Searching for Named Objects) describes no completion at the prompts of a running command, so none is offered there.

The history above the input shows three lines by default, the newest at the bottom, as AutoCAD 2021's command line does.
Drag the top edge of the command line (the pointer turns to the up-down arrow, and the edge lights blue with a short
handle) to show more or fewer lines: the history follows the pointer in whole lines, a tip beside it says "5 lines", and
the count is kept in the settings file (`input` › `historyLines`, 3 by default, 0 to 50) when the button is let go. At
0 lines the history goes and the input stands alone, the cross moved to its left; the top edge stays, so a drag up brings
the lines back. The history shows at most the lines that fit in half the height of the drawing area, the canvas and the
command line together, counted again when the window is resized: a count kept larger than fits shows as many as fit, and
the kept count is not changed. The wheel scrolls it three lines a notch and a thin bar at its right shows when there are
more lines than fit; a new line scrolls it to the end only where it was at the end, so a reader who scrolled up is not
moved. The lines are not cut with an ellipsis: a long one runs past the right edge. Drag to select across lines (Shift+click
extends, a double click takes a word, Ctrl+A takes all) and press Ctrl+C or choose Copy of the right-click menu, which is
the input's menu; Esc with lines selected lets them go. The history takes the keyboard only when clicked, and a key typed
there (a letter, Enter, Space, Esc with nothing selected) goes back to the input and is typed or run there. F2, the text
window, still shows the whole history.

A right click on the command line gives its menu: **Recent Commands** (the last 20 run, the newest first, each runs again,
kept in the settings file from one session to the next),
**Input Settings** (AutoComplete, AutoCorrect, Search System Variables, Search Content and Mid-string Search, ticked when
on and kept in the settings file; Delay Time…, which asks INPUTSEARCHDELAY as AutoCAD 2021's console does, `Enter new
value for INPUTSEARCHDELAY <300>:`, 100 to 10 000; and Input Search Options…), **Copy** (the text selected in the
command line), **Copy History** (every line written), **Paste** (the first line of the clipboard added to what is typed),
**Paste To CmdLine** (each line of the clipboard run in turn) and **Options…** (Global settings).

**Input Search Options** (INPUTSEARCHOPTIONS) is AutoCAD 2021's dialog of the same name: Enable AutoComplete, with
Enable mid-string search and the order of the suggestions, According to frequency of usage or Alphabetically; Enable
AutoCorrect, with Remember corrections after 1 to 50 mistypes (3 by default), as AutoCAD 2021's dialog takes; Search
system variables, with Separate commands and system variables, which lists the variables last under a collapsed header
of their own, `System Variables` and their count, below a rule: as AutoCAD 2021's help says, "you need to press Tab to
display the list", so Tab expands the header and chooses the first variable, and each new list starts collapsed again;
the suggestion list's delay, 100 to 10 000 milliseconds; and Search content at command line, with the Content type list (Block, Layer, Hatch, Visual
Style, Text Style, Dim Style), each kind ticked or not and moved up or down to set the order Search Content lists them
in. An option under a switch that is off is greyed. OK keeps the settings in the settings file; a number out of range
keeps the dialog open with the reason under it, in AutoCAD 2021's words: `Invalid input. Mistypes value must be between 1
and 50`, `Invalid input. Delay time must be between 100 and 10000 milliseconds`. The app's drawings have no visual styles, so Visual Style finds
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

The G button at the top left drops the application menu, as AutoCAD's: a search box for the commands (by name, then by description), then New, Open,
Save, Save As, Import, Export, Print and Close, and Options and Exit at the foot. Resting the pointer on an item with ▸
shows its list at the right; a click on the item runs the first entry of its list, and a click on Export runs EXPORTPDF.

| Item | Entries |
|---|---|
| New | Drawing (NEW, Select Template); the entry of the new document an extension offers, where one does |
| Open | Drawing (the Open dialog, Ctrl+O) |
| Save As | Drawing (SAVEAS), Drawing Template (SAVEAS with the template type chosen), Other Formats (EXPORT, as Export's) |
| Import | Other Formats (IMPORT): AutoCAD 2021's PDF and DGN have no command here and are left out |
| Export | DWF and DWFx (EXPORTDWF, EXPORTDWFX, which say there is no writer), PDF (EXPORTPDF), PNG, JPG and BMP (PNGOUT, JPGOUT, BMPOUT), Block or objects to a DXF (WBLOCK), Other Formats (EXPORT, which writes Bitmap, Metafile, Encapsulated PS, Block and DXX Extract); AutoCAD 2021's 3D DWF, DGN and FBX have no command here and are left out |
| Print | Plot (PLOT), Batch Plot (PUBLISH), Plot Preview (PREVIEW), Page Setup (PAGESETUP), Manage Plot Styles (STYLESMANAGER) |
| Close | Current Drawing (CLOSE), All Drawings |

With no item under the pointer, the right of the menu shows **Recent Documents**: the pinned files first in the order they
were pinned, a rule, then the others by the date they were opened, newest first, 9 in all unless more are pinned. A file
opened by any way (the Open dialog, a file dropped on the window, the app's command line, the lists themselves) goes to
the top. The pin at the right of a row pins or unpins it. Resting the pointer on a name shows its preview picture, path,
version, date and size: only the top of the file (for `$ACADVER`) and its end (for the THUMBNAILIMAGE section) are read,
and only then, on a worker thread, so a list of large files, or of files on a slow share, shows at once and the picture
comes when read. A file chosen that is no longer on the disk is taken off the list, the command line saying `Cannot find
D:\plans\gone.dxf: it is taken off the recent files.` The list and its pins are kept in the settings file.

The search box lists, below what is typed, first the commands and system variables whose name holds it, as the command
line's AutoComplete orders them, then, under a rule, the commands whose name and description hold every word of it: "bevel
corner" finds CHAMFER (`Bevel the corner of two lines`) and "damaged" finds RECOVER (`Open a damaged drawing and repair
it`), the description being the one the Shortcuts page of Global settings shows at the right of each command. A command
with no description is found by its name alone; at most 100 rows are listed, and none where no drawing is open. Enter, or
a click, runs the first row or the one clicked, and the menu closes. AUTOCOMPLETEDELAY, AutoCAD 2021's alias of the
variable INPUTSEARCHDELAY, and AUTOCOMPLETEMODE, its alias of -INPUTSEARCHOPTIONS, are typed as in AutoCAD (both checked
in its console).

The search box of the title bar ("Type a keyword or phrase") lists the same commands under the box as it is typed, by name and description, and Enter or a click starts the first row or the one clicked and empties the box; Esc empties it, and it lists nothing where no drawing is open. It searches the presenter's own commands only: AutoCAD's box also searches help and its community, which are not here. In code `MainViewModel.TitleSearchText`, `TitleSearchResults` and `RunTitleSearchResult`.

### The Manage tab

The Manage tab, between View and Output, has AutoCAD 2021's panels for what the presenter has. **CAD Standards**
holds Layer Translator, Check and Configure, in AutoCAD's order; Check (CHECKSTANDARDS) and Configure (STANDARDS) are parked: their buttons are greyed and the tooltip says the feature is not built yet. AutoCAD's
Action Recorder panel is not here at all: it will not be built. **Customization** has User Interface (CUI) and Edit Aliases
(ALIASEDIT), which both open Global settings on Shortcuts, where the aliases and the keys are kept, and Tool Palettes
(TOOLPALETTES, see [Tool Palettes](#tool-palettes)). **Applications** has five buttons, as the design draws them: Load Manager,
large, which runs APPLOAD and shows Load/Unload Applications, one table where an AutoLISP file or a plugin is loaded, unloaded
and kept for every start (see [AutoLISP](lisp.md#appload)), and is greyed where the window cannot show that dialog
(`ManageRibbonViewModel.CanLoadApplication`); then three small buttons with text, Load Lisp (LISPLOAD, the Windows Open dialog
for `.lsp` files), Load Plugin (NETLOAD, the Open dialog for a plugin `.dll`) and Load Python (PYLOAD, the Open dialog for a
`.py` file); then Run Script (SCRIPT), large. **Cleanup** has Purge (PURGE) and Find Non-Purgeable Items, both large (the second starts PURGE with its
window on the named items in use, the view the window's own list calls Find Non-Purgeable Items), then Overkill
(OVERKILL), Audit (AUDIT) and Recover (RECOVER). CUI, ALIASEDIT, APPLOAD and LISPLOAD are commands of the window, not of
`CadCommandRegistry.CreateDefault()`: APPLOAD says `Load/Unload Applications cannot be shown here.` where the
window has no such dialog, LISPLOAD says `LISPLOAD needs a file dialog, which the window cannot show here.` where it has no Open
dialog, and a `.dll` given with no plugin manager gets `No plugin manager is in use: applications cannot be loaded.` Layer Translator, the first button of CAD Standards, runs LAYTRANS (see the layers guide).

### DesignCenter

ADCENTER (aliases ADC, DC and DCENTER), or a key given ADCENTER in
Global settings › Shortcuts shows AutoCAD 2021's DesignCenter as a palette beside the window, left open while the drawings
change; ADCCLOSE closes it. Ctrl+2 is the DRAWINGS list in the presenter, so no key is given ADCENTER at
first; give it one (Ctrl+Alt+2, or Ctrl+2 itself) and the key shows the palette when it is closed and closes it when it is open.
The tabs are **Folders** (the desktop, the documents folder and the drives, each folder opened to its folders and its
`.dwg`, `.dws`, `.dwt` and `.dxf` files), **Open Drawings** (the drawings open in the presenter, by title) and **History**
(the drawings DesignCenter showed, latest first, 24 at most, kept for the session). A drawing opens in the tree to the nine
kinds of content AutoCAD lists, **Blocks, Dimstyles, Layers, Layouts, Linetypes, Multileaderstyles, Tablestyles, Textstyles**
and **Xrefs**; the list on the right shows what the row selected holds (the folders and drawings of a folder, the kinds of a
drawing, or the things of a kind), each with a picture where there is one (a block drawn, a drawing's model space) and its
description, and under it the preview and the description of the first row selected. **Load…** shows a file in the tree,
**Back**, **Forward** and **Up** walk the places visited, and the Tree, Preview and Description buttons hide the parts; Search
and Favorites are greyed.

A double click does the first of the right-click menu's items. A block: **Insert Block** starts INSERT with the block (and
the file it is in, read without opening it) chosen, the insertion point asked on the command line; **Insert Block…** shows the
Blocks palette on that file for the insertion options; **Add Block Definition** copies the definition into the drawing without
placing it. A layer, linetype, text, dimension, multileader or table style, or layout: **Add Layer(s)**, **Load Linetype(s)**,
**Add Textstyle(s)**, **Add Dimstyle(s)**, **Add Multileaderstyle(s)**, **Add Tablestyle(s)** and **Add Layout(s)** copy the
rows selected into the drawing shown as one undo step named ADCENTER, a thing the drawing has by the name kept as it is (the
layer WALL of the drawing keeps its colour whatever the library's WALL has) and a layout of a name it has taking `Layout3-Plan`
as the template command names it; a layer brings its linetype, a dimension style the text style, linetypes and arrowhead
blocks it draws by, a block the layers, styles and blocks its objects use. An external reference: **Attach Xref…** shows the
Attach External Reference dialog on its file. A drawing file: **Open in Application Window**, **Insert as Block…**, **Attach as
Xref…** and **Load into Content Area**. The result is said under the list (`1 layer added, 1 kept as the drawing has them.`).
A row dragged onto the canvas does the same where it is dropped: a block is placed at the drop point (scale 1, no rotation), a
drawing file inserted there as a block, and layers, styles, linetypes and layouts copied in. The content is read from the
files on disk, so a drawing open with unsaved changes is dragged as it was saved; a drawing not saved yet is browsed from its
open state but cannot be dragged. The same is done from code with
[`CadDesignCenterDrawing`](xref:GeometryHelper.Cad.CadDesignCenterDrawing) (`Open`, `Of`, `Items`) and
[`CadDesignCenter.Add`](xref:GeometryHelper.Cad.CadDesignCenter.Add*).

The right-click menu of a drawing file, or of the **Blocks** row of a drawing, adds **Create Tool Palette of Blocks**, and that
of blocks adds **Create Tool Palette**: a tool palette named for the drawing, with a block tool for each block (every block
of the drawing, or those selected) and the drawing as their source, as described under [Tool Palettes](#tool-palettes).

### Tool Palettes

TOOLPALETTES (alias TP), the Tool Palettes button of the Manage tab, or a key given TOOLPALETTES in Global settings ›
Shortcuts shows AutoCAD 2021's tool palettes as a window beside the drawing (340 by 640 at the window's right edge, which
can be moved and resized), left open while the drawings change; TOOLPALETTESCLOSE closes it. Ctrl+3 is the ribbon's in the
presenter, so no key is given TOOLPALETTES at first; give it one (Ctrl+Alt+3, or Ctrl+3 itself) and the key shows the palettes
when they are closed and closes them when they are open. The palettes are a floating window: AutoCAD's docking, anchoring,
auto-hide and transparency are not offered, and neither is its CUSTOMIZE dialog for palette groups.

The window has a tab for each palette along its right edge, the name of the one shown over the list, and a **⋯** button that
opens the palette menu. A palette lists **tools**, each a block, a hatch or a command, and the **text** lines and
**separators** that part them. A click runs a tool (a click with Ctrl or Shift only selects it; Enter runs the one selected):

- A **command tool** runs a command line: a command's name or alias, then the answers to its first prompts, parted by
  spaces or semicolons (`CIRCLE 0,0 50` draws the circle at once; a leading `^C^C` and an underscore, as a menu macro of
  AutoCAD writes them, are left off). It is the command typed, so its prompts are the command's own.
- A **hatch tool** starts HATCH with the tool's type (pattern, lines of the user's, solid or gradient), pattern, angle, scale,
  spacing and association. HATCH asks the point inside the area as it always does, and the drawing's own hatch settings are put
  back when the command ends.
- A **block tool** starts INSERT with the block, the drawing it comes from, and the scale and rotation of the tool, asking
  the insertion point on the command line (and the scale and rotation where the tool has *Prompt for scale* or *Prompt for
  rotation*), and explodes it where *Explode* is on. A block the drawing has already is placed as it is, the question of
  redefining it never asked; otherwise the block is brought in from the tool's drawing, and a drawing that cannot be found is
  said on the command line.

Each of the three can set the **overrides** of AutoCAD's Tool Properties: a layer, a colour (ByLayer, ByBlock or 1 to 255), a
linetype and a lineweight, each **(Current)** to use the drawing's own. They are the current layer, colour, linetype and
lineweight while the tool's command runs, and the drawing's are put back when it ends, however it ends (Esc included). A layer
the drawing lacks is made, in the undo step of the command; a layer that is frozen, a colour that is not an index or a
linetype that is not loaded is said on the command line and the current one used. A block or hatch tool **dragged** out of the
palette onto the canvas places the block at the drop point (scale and rotation as the tool has them) or hatches the area
there; a command tool is not dropped, as in AutoCAD.

**Making tools.** Select objects of the drawing and drag one of them out of the canvas onto the palette (a press on a selected
object, dragged, with the palettes open and no command running; it is the box drag it always was otherwise). A block reference
becomes a block tool (the block, the drawing's file as its source, the reference's scale), a hatch a hatch tool (its pattern
or fill, angle and scale), and a line, circle, arc, polyline, ellipse, spline, point, text, multiline text, multileader or
dimension a command tool for the command that draws it (`LINE`, `CIRCLE`, `DIMLINEAR` and the like); each takes the object's
layer, colour, linetype and lineweight as its overrides, a true colour being left unset. An object no tool draws is left out
and said on the command line (`2 tools added to Draw. 1 not drawn by any tool.`). Blocks dragged from DesignCenter onto the
palette become block tools of their drawing; a drawing file dropped is one tool that inserts the whole drawing as a block.

**The menus.** Right-click a tool for **Cut**, **Copy**, **Paste**, **Delete**, **Rename…** and **Properties…**: the Tool
Properties dialog, with the name and description, the command line, or the block with its drawing, scale and rotation, or the
hatch's type, pattern, angle and scale, and the overrides (a line of text has only its words). Delete, Ctrl+X, Ctrl+C and
Ctrl+V work on the tools selected, F2 renames one. Right-click the palette, a tab or the **⋯** button for **New Palette…**,
**Rename Palette…**, **Delete Palette** (which asks first), **Add Text…**, **Add Separator**, **Paste**, **View Options…**
(icon only, icon with text or list view; the icon size, 16 to 96 pixels; for the current palette or for all of them),
**Import Palette…**, **Export Palette…** and **Export All Palettes…**. A tool dragged within the palette is moved before the
one it is dropped on.

**The starting palettes.** With no file of its own the window starts with six palettes the presenter makes, from its own icons
and patterns: **Draw** (Line, Polyline, Circle, Arc, Rectangle, Ellipse, Spline, Point), **Modify** (Move, Copy, Rotate,
Scale, Mirror, Offset, Trim, Extend, Fillet, Chamfer, Stretch, Explode, Erase), **Annotation** (Single-line and Multiline Text,
Table, the dimensions Linear, Aligned, Angular, Radius, Diameter, Baseline and Continue, and Multileader), **Hatches** (ANSI31
to ANSI38, a solid fill, a linear gradient and ten materials), **Architectural Hatches** (AR-B816, AR-BRSTD, AR-CONC,
AR-HBONE, AR-PARQ1, AR-RROOF, AR-RSHKE and AR-SAND, at their size in millimetres) and **ISO Hatches** (ISO02W100 to
ISO15W100). No file, picture or icon of AutoCAD's is used, and its `.atc` files are not read.

**The file.** The palettes are kept in `ToolPalettes.json` beside the settings (`%AppData%\GeometryHelper\Drawing\`, the folder
of `MainViewModel.SettingsPath`), written after every change: UTF-8 JSON, two spaces of indent, LF. A file that is missing or does
not read gives the starter palettes, said once in the log; a value that is not valid falls back to its default with a Warn
naming it, and a tool of a kind this version does not know is left out. **Export Palette…** writes the same format (a `.ghtp`
file, one palette or all) and **Import Palette…** reads it, adding the palettes after the last with their names made unique
(`Draw (2)`, `Draw (3)`).

| Member | Meaning |
|---|---|
| `format`, `version`, `active` | `"GeometryHelper.ToolPalettes"`, `1`, the place of the palette shown, from 0 |
| `palettes[]` | `name`, `view` (`icons`, `iconsAndText` or `list`), `iconSize` (16 to 96), `tools[]` |
| every tool | `kind` (`command`, `block`, `hatch`, `text` or `separator`), `name` (the words of a text), `description` |
| command | `command` (the command line), `icon` (a name of the presenter's icons: `Line`, `Circle`, `Dimension` and the others of the ribbon) |
| block | `file` (the drawing; none for the drawing open), `block`, `scale`, `angle` (degrees), `promptForScale`, `promptForRotation`, `explode` |
| hatch | `hatchType` (`pattern`, `userDefined`, `solid` or `gradient`), `pattern`, `angle` (degrees), `scale`, `spacing`, `gradient` (`GR_LINEAR` and the others), `associative` |
| overrides of a command, block or hatch | `layer`, `color` (`ByLayer`, `ByBlock`, `1` to `255`), `linetype`, `lineWeight` (hundredths of a millimetre; -1 ByLayer, -2 ByBlock, -3 default); a member left out is the drawing's current one |

From code, [`CadToolPalettes`](xref:GeometryHelper.Cad.CadToolPalettes) (`CreateStarter`, `Load`, `Save`, `Add`,
`Rename`, `Remove`, `Move`, `Copy`, `Cut`, `Paste`, `Import`, `Export`, `ToJson`, `TryParse`),
[`CadToolPalette`](xref:GeometryHelper.Cad.CadToolPalette), [`CadTool`](xref:GeometryHelper.Cad.CadTool)
(`ForCommand`, `ForBlock`, `ForHatch`, `ForText`, `ForSeparator`, `FromObject`) and
[`CadCommandSession.StartTool`](xref:GeometryHelper.Cad.CadCommandSession.StartTool*) do the same without the window.

### The Parametric tab

The Parametric tab, between Annotate and View, has three panels, laid out as AutoCAD 2021's (see [The ribbon
](#the-ribbon)). **Geometric** has Auto Constrain (AUTOCONSTRAIN), the twelve constraints as tiles in
three rows of four (Coincident, Collinear, Concentric, Fix; Parallel, Perpendicular, Horizontal, Vertical; Tangent, Smooth,
Symmetric, Equal), then Show/Hide (CONSTRAINTBAR), Show All and Hide All of the constraint bars, and a launcher that opens
Constraint Settings on its Geometric tab. **Dimensional** has the Linear split (Linear, Horizontal and Vertical: DCLINEAR,
DCHORIZONTAL, DCVERTICAL), Aligned (DCALIGNED), the tiles Radius, Diameter, Angular and Convert (DCCONVERT), then Show/Hide, Show All
and Hide All of the dynamic constraints (DCDISPLAY), a launcher that opens Constraint Settings on its Dimensional tab and, under
its title Dimensional ▾, the two constraint modes. **Manage** has Delete Constraints (DELCONSTRAINT) and Parameters Manager
(PARAMETERS); Constraint Settings is no longer a button, the two launchers replace it. A button runs its command as a button of
Home does; Show All and Hide All of Geometric work on the bars of the working space directly. The constraint bars are drawn by the canvas beside each constrained object shown
(`CadCanvas`), a right click on an icon opening Delete, Hide and Hide All. The Constraint Settings dialog
(`ConstraintSettingsViewModel`) and the Parameters Manager palette (`ParametersPaletteViewModel`, which follows the document
shown) are shown through functions the workspace sets, as the other dialogs are; the screenshot mode shows them with
`--dialog constraints[:0|1|2]` and `--dialog parameters`, and the tab with `--tab Parametric`. The commands behind it are in
[Parametric constraints](editing.md#parametric-constraints).

### The PYTHON palette

PYTHON shows a palette under the canvas, as wide as the canvas and with the command line below it, and PYTHONCLOSE or the ×
in its header hides it. It is a Python console for the drawing shown: a transcript above, an input box below, a toolbar
(Run file for PYRUN, Load for PYLOAD, Stop, Clear, Reset console) and a status line with the Python it uses. Enter runs a
complete entry and Shift+Enter adds a line; each drawing keeps its own transcript and names, and the palette switches
with the drawing shown. Its top edge is dragged to change its height, as the command line's is, from 3 lines to half the
drawing area, and the height is kept for the session only. A host that embeds the presenter in a program of its own gets
neither the palette nor Python unless it sets `CadAppHost.EnablePython`, which GeometryHelper CAD itself does. Where no Python can be found the palette says what to install or set, with a
button to Global settings, and nothing else changes. [Python in the app](python.md) has the rest: finding Python, the
module a script imports, the undo step, Esc and the errors.

### The start screen

With no drawing open the canvas shows the start screen: the name of the app, Open DXF… (Ctrl+O), New, and Recent
Documents with the same files and pins as the application menu, under PINNED and RECENT headings, each row with its
preview, folder and when it was opened (`today 09:14`, `yesterday 16:40`, `2026-10-30`); the two buttons at its top right
show the files as a list or as large pictures. GeometryHelper CAD, which has no extension, offers `New ·
Ctrl+N` (NEW), then `New from template · Ctrl+Shift+N` (Select Template in the `DrawingTemplates` folder), and says "Ctrl+O opens a DXF, Ctrl+N starts a new drawing." on the command line. An extension may offer a new
document of its own, a `CadNewDocumentOffer` (its menu name, button text, tooltip, start-screen line, welcome line and what
running it does) returned by `CadWorkspaceExtension.NewDocumentOffer`: the start screen then adds its button and calls NEW
"Blank drawing", the New entry of the application menu lists it, and Ctrl+N runs it. A host that makes model drawings offers "New
drawing from a model… · Ctrl+N" this way. The window tells the two apart by `MainViewModel.NewDocumentOffer` (`HasNewDocumentOffer`).

### A panel of an extension beside the DRAWINGS list

An extension may add a panel of its own between the DRAWINGS list and the canvas: it sets
`CadWorkspaceExtension.SidePanelContent` (a WPF element, or a view model the window has a template for) and overrides
`SidePanelHeader` for the title above it. The window shows the panel of the first extension that has content
(`MainViewModel.SidePanelExtension`, `IsSidePanelShown`), 320 pixels wide, with an edge the user drags to change the width
(160 to 1 200 pixels); `IsSidePanelVisible` hides and shows it. An app with no such extension looks as it always did: the
DRAWINGS list is 288 pixels wide and the canvas starts at 288. The start screen is 760 wide, or as wide as the canvas where
that is less.

### What an extension adds to the menu, Properties and the DRAWINGS header

Three more seams let an extension's own objects be edited as the presenter's are, with no window of its own. Each is a
member of `CadWorkspaceExtension` that adds nothing until it is overridden.

- **The right-click menu.** `CanvasMenuItems(document, handles)` returns `CadExtensionMenuItem`s for the objects selected:
  `new CadExtensionMenuItem("Fit to sheet", () => Fit())`, a grey one with `isEnabled: false`, a ticked one with
  `isChecked: true`, a submenu with `new CadExtensionMenuItem("Change representation", items)`, and
  `CadExtensionMenuItem.Separator()`. The items are first in the Edit menu, before the presenter's own, drawn in the presenter's
  menu style, and asked each time the menu opens with objects selected and no command running; with nothing selected, the
  Default menu is the presenter's alone. A group of bars picked, with an extension that adds "Properties..." and "Select in
  model", gives a menu that begins with those two, a separator, and goes on with Clipboard, Isolate, Erase and the rest.
- **Rows of Properties that can be changed.** A row made as `new CadPropertyRow("Scale", "1:50", text => Change(text))` is a
  box typed into, one made with the list of its choices as a third argument a drop-down, and
  `CadPropertyRow.Check("Hidden lines", true, text => Change(text))` a check box, whose setter is given "Yes" or "No". The
  palette calls the setter on Enter, on leaving the box and on a choice, as for its own rows. The setter returns null where it
  made the change, and the rows are read again; it returns the words of its refusal otherwise, which are written on the command
  line while the row goes back to its value. A setter that throws is a refusal in the exception's words and a Warn in the log.
  A setter whose action makes the extension ask for its rows again (`RefreshFromExtensions`) has the rows read once, when the
  setter is done. An extension whose `CanvasMenuItems` or `DescribeSelection` throws is passed over for that call, with a
  Warn in the log; the presenter's own items and rows, and the other extensions', are shown.
  A row made as before, with `isReadOnly`, or a check made with a null setter, is only read.
- **A button in the header of DRAWINGS.** `FileListButtons` returns `CadExtensionButton`s, each an icon (a path drawn as the
  presenter's own icons are, 2 wide on a square of 24: `Geometry.Parse("M20 11a8 8 0 1 0-2.3 5.7M20 5v6h-6")` is the refresh),
  a tooltip and an action. They stand left of the presenter's Refresh and Open, and are read as the extension is added.

A command plugin loaded at run time reaches the same places without these seams, which take WPF types: it describes its
ribbon tabs, Properties rows, DRAWINGS buttons and items, canvas items and object snaps with the types of
`GeometryHelper.Cad.Plugins`, and the window draws them, as [command plugins](plugins.md#what-a-plugin-adds-to-the-window)
describes. A plugin's Properties provider is asked after the extensions, its canvas items come after theirs, and its
DRAWINGS buttons stand first in the header.

### The contextual tabs and the owner contract

The Hatch Editor, Image, PDF Underlay and Array tabs open for the objects selected only where their [owner](owners.md) lets
them be edited as what they are: an object with no owner, or whose owner answers `Free` or `Edit`. A hatch, an image, a PDF
underlay or an array whose owner answers `MoveOnly` or `ReadOnly`, as the cut circles of a group of bars drawn as hatches do,
opens no tab, and one that no owner is registered for, being `ReadOnly` as well, opens none. Two hatches selected, one free and
one the owner only lets move, open the Hatch Editor over the free one.

### Docking and sizing the panels

DRAWINGS and PROPERTIES are each docked left or right of the canvas. Dragging a panel's title shows, past 8 pixels, a band on
the side it would dock on: the left half of the window docks it left, the right half right. An extension's panel goes with
DRAWINGS, on its canvas side. Dragging a panel's inner edge makes it wider or narrower: DRAWINGS and PROPERTIES 180 to 600
pixels, an extension's panel 160 to 1 200, none growing past what leaves the canvas 400. Both are kept in the settings file
(`GlobalSettings.FileListWidth`, `PropertiesWidth`, `SidePanelWidth`, `FileListOnRight`, `PropertiesOnLeft`) when the panel
is let go, so the window opens with the panels where they were left; Reset to defaults of Global settings, once applied,
puts DRAWINGS back left at 288, PROPERTIES right at 312 and an extension's panel at 320.

An extension that keeps its own record of a drawing's file overrides `DocumentSaved(document, path)`: it is called after every
save that reaches the file (Save, Save As, QSAVE, Ctrl+S, Save All) with the document and the file's full path, so that
a host can stamp its record of the drawing with the time of the save. It is not called for a save that failed or was refused.

`MainViewModel.Reload(path)` opens an open document again from its file, in the same row of the list, for a host that rewrote
the file while it is open. The extensions are told the old document is closed and the new one opened; the view of each space
the user zoomed or panned and the space that was current are kept, a view still fitted to the drawing is fitted to the new one,
and the undo history and selection are gone with the old document. A document with changes not saved is kept as it is and the
command line says so, unless `discardChanges` is true; a file that cannot be read leaves the old document as it was.

A host that keeps its drawings in a folder calls `MainViewModel.ShowFolder(folder)`: the DXF and DWG files of the folder
become rows of the DRAWINGS list with none opened, and a file deleted since is taken off. To name such a file before it is
opened, an extension overrides `DescribeFile(path)` as well as `DescribeFile(document)`; the list asks the second for an open
file first. It is asked often, so it answers from memory, and the host calls `RefreshFromExtensions()` when what it would say
changed. A row no extension names says what the file is: "DWG · 392 entities · 1 layout" for a `.dwg`, "DXF · …" for a
`.dxf`, under a folder headed "· DWG", "· DXF" or "· DWG/DXF".

## The apps

The standalone app is net8.0-windows:

| App | What it is | Use it to |
|---|---|---|
| GeometryHelper CAD, `GeometryHelper.Cad.App.exe` | the presenter alone: `GeometryHelper.Cad` and its controls, no model drawing | open, edit and plot DXF drawings in place of AutoCAD; the program to associate with `.dxf` files |

```
GeometryHelper.Cad.App.exe plan.dxf
```

opens the window on a drawing, as double-clicking a file associated with the app does. The app reads the global settings from
the user's file and loads the command plugins of the `Plugins` folder beside the app and of the startup list: GeometryHelper CAD keeps the startup list in
`%AppData%\GeometryHelper\Dxf`; the app gets its plugins from its own folder. The title bar and the start screen carry the app's name.

The app starts through `CadAppHost` of `GeometryHelper.Cad.Wpf`, and its window is `CadAppWindow`. A host that makes model
drawings derives from `CadAppHost` to add its parts: the host's protected members take its options off the command line, put
the model drawing into the view model, make a drawing before the file is opened, draw its dialogs and select what its
options name. A host of its own does the same.

### The icon and the file association

GeometryHelper CAD has an icon of its own, a white set square and an orange point on a blue tile, at 16, 24, 32, 48 and 256
pixels: the exe carries it, and the window shows it on the taskbar and in Alt+Tab (`CadAppHost.WindowIcon`). A drawing file
associated with the app shows a page with the same tile on it. Both are written from their vector drawing by
`src/GeometryHelper.Cad.App/Icons/make-icons.ps1` into `app.ico` and `drawing-file.ico`; the second is copied beside the exe.

```
GeometryHelper.Cad.App.exe --register
GeometryHelper.Cad.App.exe --unregister
```

`--register` associates .dxf and .dwg files with the app for the user alone, under `HKEY_CURRENT_USER\Software\Classes`, so
no administrator is asked for:

| Key | Value |
|---|---|
| `GeometryHelper.Drawing` | `Drawing (GeometryHelper CAD)`, the type Explorer shows |
| `GeometryHelper.Drawing\DefaultIcon` | `drawing-file.ico` beside the exe (the exe's own icon where the file is missing) |
| `GeometryHelper.Drawing\shell\open\command` | `"…\GeometryHelper.Cad.App.exe" "%1"` |
| `.dxf\OpenWithProgids`, `.dwg\OpenWithProgids` | `GeometryHelper.Drawing`, so the app is in the Open with list |
| `.dxf`, `.dwg` | `GeometryHelper.Drawing`, only where no program has the extension |

An extension another program has (AutoCAD's `.dwg`, say) is left to it, and the app is one choice of Open with; Windows keeps
the choice "Always use this app" itself. It writes one line, `.dxf and .dwg files open with GeometryHelper CAD: …`, and
exits with 0, or with 1 and the reason where the registry refuses. `--unregister` takes out the ProgID and its Open with
entries, and the default of an extension only where it is the app's; what other programs wrote stays. Run it again after
moving the app, since the command names the exe where it was. A file opened from Explorer starts a new window of the app,
one per file, even while the app runs. A host built on `CadAppHost` takes the same two switches and writes the same ProgID, so
the app registered last is the one that opens the files.

### Crashes and the crash log

An exception nobody caught does not close the app without a word. Every exception that reaches the top is written to
`%AppData%\GeometryHelper\Drawing\crash.log` (appended, UTF-8): the time, the app's name and version, the runtime and the
system, which handler caught it, the command run last (`PLOT`, say), and the exception in full, its type, message, stack and
each inner exception. The file is moved to `crash.old.log` once it passes 256 KB, so the two hold 512 KB at most. The
same text goes to `GeometryHelperLog` as a Warn. Writing the log never throws; where the file cannot be written (a read-only
folder) the message box says so.

| Where it was thrown | What happens |
|---|---|
| A Python error on the window's thread outside a script's run (a script's handler of a .NET event that raises after the run ended) | No box and no crash entry: the command line of the drawing shown gets `Python error: <Type>: <message>`, the log a Warn, and the app goes on |
| The window's thread (a click, a key, a command) | The entry is written and a message box says `An error occurred: <message>. Details were written to <path>. The drawing is kept; save your work.`; the app goes on and the drawings stay open |
| Another thread | The entry is written; the process ends, as .NET ends it |
| A task nobody awaited | The entry is written and the exception is marked observed, so the app goes on |
| Out of memory, an access violation or another fault of native code, on the window's thread | The entry is written, the box says the app cannot go on, and it closes: after these the process can no longer be trusted to hold the drawings |

A stack overflow cannot be caught by any handler: .NET ends the process at once and nothing is written, so a crash with no
entry in the log and no message is one of those. `GeometryHelper.Cad.App` and any other host built on `CadAppHost` install the
handlers, as part of `CadAppHost.Run`.

### Screenshots

For a picture of the window in a test or a document:

```
GeometryHelper.Cad.App.exe --screenshot out.png plan.dxf --size 1600 960
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
| `--tab Insert` | shows a ribbon tab: Home (the default), Insert, Annotate, View, Plugins, Settings, or a host's; another name exits with 2, `--tab Nowhere: no such tab.` |
| `--plugin path.dll` | loads a command plugin, every file trusted, into a folder of the run under the temporary folder |
| `--layers` | draws the Layer manager instead of the window, the lines of `--run` typed first: `--space Layout1 --run MS --layers` shows its VP columns |
| `--dialog name` | draws a dialog or a menu instead of the window, the lines of `--run` typed first where it needs them |
| `--recent "a.dxf\|*b.dxf"` | fills Recent Documents, the first the newest, a file written with `*` pinned |

The dialogs are `save-changes`; `mtext-unsaved` (Multiline Text - Unsaved Changes); `options` or `settings:Canvas and cursor` (Global settings on a category); `plugins` (Load/Unload Applications)
and `plugin-untrusted`; `secureload` (the load warning), `writable-folder` and `settings-security` (the
Security page); `color`, `truecolor` and `truecolor-rgb` (Select Color on index 30);
`dsettings[:Tab]`; the menus `osnap-menu`, `snap-menu` (after `--run "LINE"`), `canvas-menu` (the canvas's right-click menu, after `--run "SELECTALL"` the Edit menu, after `--run "LINE;0,0;100,0"` the Command menu), `drawings-menu` and `scale-menu` (the status bar's scale list); `missing-shx` and
`missing-shx-replace`; `ptype`; `linetype`, `linetype-load`, `select-linetype`, `hatch`, `hatch-gradient`, `hatch-user`,
`hatch-solid` and `hatch-file`; `block`, `insert`, `wblock`, `attdef` and `eattedit[:Tab]`; `battman`,
`battman-edit[:Tab]`, `battman-settings` and `attdia` (on the first block with attributes); `purge` and
`purge-nonpurgeable`; `classicgroup` and `group-order` (on the first group); `style`, `style-new`,
`dimstyle`, `dimstyle-new`, `dimstyle-modify[:Tab]`, `dimstyle-override[:Tab]` and `dimstyle-compare[:Other]`;
`pastespec`; `app-menu` and `app-menu:export` (the application menu on Recent Documents, or on the list of New, Open,
Save As, Export, Print or Close); the command line's menus `cmdline-menu`, `input-settings-menu` and `recent-commands-menu`;
and `autocomplete:LA` (the AutoComplete list for LA). A popup of the running window cannot be drawn; these draw its
content alone. A name neither app knows exits with 2: `--dialog update: no such dialog.` from GeometryHelper CAD.

The dialogs of rounds 5 to 9 are drawn over the drawing open, each in a sample state of its own, the lines of `--run` typed first:
`mlstyle` (Multiline Style, WALL-240 added with elements 120, 0 and -120 where the drawing has only STANDARD, and chosen),
`mlstyle-new` (Create New Multiline Style, asking PARTITION), `mlstyle-new-page` (New Multiline Style), `mlstyle-modify` and
`mlstyle-load`; `mledit`; `plotstamp`, `plotstamp-advanced` (Advanced Options) and `plotstamp-fields` (User Defined Fields);
`imageadjust` (on the pictures of the space shown); `attext`; `xbind` (on the external references, one attached by
`--run "-XREF;A;grid.dxf;0,0;;;"`); `insertobject`; `recovery` (three copies written in the temporary folder); `spell` (on the
entire drawing, started, so that the first word not known is shown) and `spell-settings`; `objectscale` and `objectscale-add` (on
the annotative objects of the space shown); `insertlayout` and `templateoptions` (over a template written in the temporary
folder, the second on the first layout); `saveaspdf`, `pdf-override` and `pdf-options`; `rename` (on Layers, one name changed and
one typed); `lweight`; `boundary`; `viewports` (Three: Right, the second tile given the first named view) and `viewports-named`
(two configurations saved, the first one's name in Rename); `filter` (a layer and a kind of object listed); `find` (Find and
Replace on "plate", its result listed); `attach` (Attach External Reference on the `grid.dxf` beside the drawing, or on the drawing
itself where there is none); `layerstate-new` (New Layer State to Save with a description); and `layerfilter` (Layer Filter Properties of the Layer manager's New Property Filter, two rows of cells and the preview of the layers that pass). The picture is taken from the dialog's content with the window's
resources, so a style the window sets for a kind of control (the dark scroll bar, a list's rows) is drawn as it is in the window.
They need a drawing: with none, `--dialog rename: needs a drawing.` exits with 2. Where the drawing lacks what a dialog is made of (`imageadjust`
with no picture, `objectscale` with no annotative object, `xbind` with no reference), the dialog says so, as `--dialog imageadjust:
the space shown has no image (--run "-IMAGE;...").`, and exits with 2.

A host that makes model drawings adds its own switches by the same hooks.

Fonts are looked for in Global settings > Folder preference > Font search folders in order, then in the `Fonts` folder next to `GeometryHelper.Cad.dll` and its subfolders (see [the categories](#the-categories)), a bare name taking `.shx` and a full path used as it is; the list is empty by default, the folder of the drawing is not looked in, and AutoCAD's own Fonts folders are never looked in unless you add them there. A TrueType file is also looked for in the Windows Fonts folder, and a TrueType family Windows does not have is drawn from a file of that family in those folders. A style naming `romans.shx` is drawn as the single strokes of romans, read from the file: shapes, unifont and big fonts, every pen code; a TEXT of romans.shx `ABC123` 10 high right-aligned on x 100 is inked from 44.762 to 100, as AutoCAD puts it. A style naming Arial is drawn by the canvas. A letter a TrueType font has no glyph for is drawn from a font that has it, as AutoCAD draws it through the font linking of Windows, on the screen, on a plot, in a PDF and in the outlines of TXTEXP: the Vietnamese letters of U+1EA0 to U+1EF9 in Gulim or Malgun Gothic (an MTEXT written on a Korean machine names Gulim for them, `\fGulim|b0|i0|c129|p50;ỗ`) were the font's empty box and are Arial's own letters; the fonts tried are Arial, Segoe UI, Tahoma and Times New Roman in that order, in the face's bold and italic, then the font Windows gives the letter, and a letter none has stays the font's box. A font named in the substitution table, or in the font mapping file (an `.fmp` file of `oldfont;newfont` lines, as FONTMAP), is drawn in the font given for it. A TrueType family or file the machine has not (a Korean drawing's 돋움체, DotumChe or Gulim on a machine without them) is drawn in a TrueType font, the drawing font (Arial by default), as AutoCAD draws it through the font mapping of Windows; its Korean, Chinese or Japanese letters come out of the font Windows links for them (Malgun Gothic, SimSun, MS Gothic) as the letters of any Arial text do, where the single-stroke font would have drawn each as a question mark. The style keeps the drawing's own font name, and the command line says `Font DotumChe not found: using the drawing font.` once. A family is matched by any of its names, English or local (`Malgun Gothic` and `맑은 고딕`, `MS Gothic` and `ＭＳ ゴシック`, `SimSun` and `宋体` are one font). The Text Style dialog's Font name box always shows the font the style stores, whether or not the machine has it: a family it lacks is listed as stored and marked `(not installed)` (`DotumChe`), an SHX file or big font no folder has `(not found)`, and a family it has under another of its names (`맑은 고딕`) selects the installed entry (`Malgun Gothic`); showing a style writes nothing, so Apply after a look at every style leaves each style's font, big font, charset and file as they were, and only a change made in the dialog marks a style as changed. A TrueType family that has names in other languages in its font file shows them after its English name, in muted words: `TT  Malgun Gothic  맑은 고딕`, `TT  MS Gothic  ＭＳ ゴシック`, `TT  DotumChe  돋움체` where DotumChe is installed, and `TT  Arial` alone; a name too long for the box is trimmed with an ellipsis and the tooltip holds them all. The list stays in the order of the English names, and typing in the box finds a font by the start of any of its names (`dot` or `돋` finds DotumChe, `맑` finds Malgun Gothic). Picking a family stores its English name, as before, and a style that stores a local name keeps it stored. The same names follow the font in the style list of the dialog and in the MTEXT editor's font box; the Missing Fonts dialog lists a family the machine lacks as the drawing stores it, with nothing after it. An SHX font not found is drawn in the replacement font (as FONTALT): by default GeometryHelper Simplex (`ghsimplex`), the presenter's own single-stroke font after the Hershey Roman Simplex letters, its capitals as high as the text. Write `simplex.shx,gbcbig.shx` to give a big font for big fonts not found too. On opening, the command line lists the missing SHX files once, each with what it is drawn in: `Missing SHX files: romans.shx drawn in GeometryHelper Simplex.` A drawing opened with SHX files not found shows the Missing SHX files dialog: the files, the styles that use them and what they are drawn in, and a replacement to pick for each or the replacement font kept. Where TrueType fonts the machine has not are named too, by a text style or by an MTEXT's font code (`{\fDotumChe|b0|i0|c129|p50;...}`; MTEXTs of model space, layouts and blocks are read, those inside dimensions, leaders and attributes are not), the dialog is titled Missing Fonts and a second, read-only list follows with each family or `.ttf`/`.ttc`/`.otf` name once, whatever its case, in the order of the names, and what it is drawn in (`DotumChe`: `Arial, letters it lacks from another font`; Arial is the drawing font of Global settings). A TrueType font has no replacement to pick. A drawing whose only missing fonts are TrueType opens the dialog with that list alone, titled Missing Fonts; with SHX files alone the title stays Missing SHX files. A family the machine has under another of its names is not listed. Do not show again turns the dialog off; Global settings › Text and fonts › Font not found has the switch Show the missing fonts dialog on opening a drawing to turn it on again (saved as `showMissingShxDialog`). The Chinese, Japanese and Korean code pages of the big fonts are registered by the app at start. The font lists are read ahead once the window has drawn: the TrueType families of the machine (the name table of each file of the Fonts folders of Windows and of the user, 2 447 files on a machine with 1 324 families) and the SHX files of the font folders (1 389 in AutoCAD 2021's Fonts folder), each on a thread of its own at below normal priority, so that the first STYLE, MTEXT or dimension text opens on lists already read; one opened before they are done waits for the same reading and does not repeat it. The head of an SHX file is read once for the process and again only where the file's size or time written changes, a font folder changed in Global settings is read as it is applied, and the lists are the same, in the same order, as when each was read at the first use. The Font name box of the Text Style dialog, the font box of the MTEXT editor and the other drop-downs of the app's own style make the rows they show and not one for every item: a drop-down of the 1 325 fonts opens in 0.1 second where it took 2.

## The ribbon

The window's ribbon has the tabs Home (Draw, Modify, Annotation, Layers, Block, Properties, Groups, Utilities, Clipboard, View, in AutoCAD 2021's order), Insert (after a host's tabs, as below), Annotate (Text, Dimensions, Centerlines, Leaders, Tables, Markup and Annotation Scaling, below), View (Viewport Tools, Named Views, Model Viewports, Palettes and Interface in AutoCAD 2021's order, then Navigate, Views and Drafting, below), Manage (Customization, Applications, CAD Standards and Cleanup, below), Output (Plot and Export to DWF/PDF, below), Plugins and Settings. Home is laid out as AutoCAD 2021's, and the sizes of its controls are those of every tab: a panel is 98 pixels high, its body 80 over a title of 18; a large button is 72 high, its icon in a slot of 32 and its label under it in at most two lines, broken between words only; a small button is 22 high, the three of a column 24 apart; one shown by its icon alone, a tile, is 24 by 22, and 35 wide with the arrow of its split; the boxes are 22 high. Draw holds Line and Polyline, the Circle split (Center Radius and Center Diameter; 2-Point and 3-Point; Tan Tan Radius and Tan Tan Tan) and the Arc split (3-Point; Start Center End, Angle and Length; Start End Angle, Direction and Radius; Center Start End, Angle and Length; Continue), each button running the item picked last under its label, which stays Circle or Arc, and in a column the tile splits Rectangle and Polygon, Ellipse (Center and Axis End; Elliptical Arc) and Hatch, Gradient and Boundary, the tile showing the one picked last. Its title, Draw ▾, drops Spline Fit and Spline CV, Construction Line, Ray, Multiple Points, Divide and Measure, then Region, Wipeout, Donut and the Revision Cloud split (Rectangular, Polygonal, Freehand), all tiles. 3D Polyline and Helix are not there: the presenter has no 3DPOLY nor HELIX. Modify is three columns of Move, Copy and Stretch; Rotate, Mirror and Scale; Trim, Fillet and Array, each with an arrow that drops Trim and Extend, Fillet, Chamfer and Blend Curves, Rectangular, Path and Polar Array (the label staying Array), then the tiles Erase, Explode and Offset: no fourth row, no menu on the title. Modify ▾ drops, as tiles, Set to ByLayer (SETBYLAYER), Change Space (CHSPACE), Lengthen, Edit Polyline (PEDIT), Edit Spline (SPLINEDIT), Edit Hatch (HATCHEDIT) and Edit Array (ARRAYEDIT), then Align, Break, Break at Point, Join, Reverse (REVERSE), Delete Duplicate Objects (OVERKILL) and the Draw Order split: Bring to Front, Send to Back, Bring Above Objects and Send Under Objects run DRAWORDER and give it Front, Back, Above objects or Under objects once the objects are picked, Bring Text, Dimensions, Leaders and All Annotations to Front run TEXTTOFRONT with Text, Dimensions, Leaders or All, and Send Hatches to Back runs HATCHTOBACK. A split button shows the command last picked from its arrow, as AutoCAD's do: Extend picked once from Trim's arrow leaves the button reading Extend and running EXTEND until Trim is picked again; Circle, Arc, Text, Array, Edit Attributes and Measure keep their label and run the item picked, and Paste always pastes. The choice lasts for the window's session. Annotation holds the Text split (Multiline Text, MTEXT, or Single Line, TEXT), Dimension (DIM), the Linear split (Linear, Aligned, Angular; Arc Length, Radius, Diameter; Ordinate, Jogged; the button reads the one picked last), the Leader split (Leader; Add Leader and Remove Leader, MLEADEREDIT; Align and Collect) and Table (TABLE); Annotation ▾ drops four rows, each a tile and a box 160 wide: Text Style (STYLE), Dimension Style (DIMSTYLE), Multileader Style (MLEADERSTYLE) and Table Style (TABLESTYLE), the box setting the current style as one undo step. Layers is laid out as AutoCAD 2021's: Layer Properties large (LAYER, the Layer manager), the layer box 208 pixels wide over two rows of tiles 24 by 22, icon only, with the tooltip naming the command: Off (LAYOFF), Isolate (LAYISO), Freeze (LAYFRZ), Lock (LAYLCK) and Make Current (LAYMCUR, with its text), then Turn All Layers On (LAYON), Unisolate (LAYUNISO), Thaw All Layers (LAYTHW), Unlock (LAYULK) and Match Layer (LAYMCH, with its text). The layer box draws each layer as three state glyphs, then its colour and name: a lit or dark bulb for on or off, a sun or a snowflake for thawed or frozen, an open or a closed padlock for unlocked or locked; the closed box shows the current layer so, and the open list draws the current layer's row selected. In the open list a click on a glyph toggles that state of that layer at once, leaves the list open and the current layer as it is, and is one undo step that LAYERP takes back, as the Layer manager's cells are; freezing the current layer is refused on the command line (`Cannot freeze layer "0".  It is the CURRENT layer.`), turning it off is not. A click on a name makes the layer current and closes the list. The title Layers ▾ drops the slide-out: the Layer State box, 209 wide, with the layer state icon before its name, which lists the drawing's layer states by name (Unsaved Layer State where none matches the layers) and restores the one picked as one undo step, with New Layer State... and Manage Layer States... at its end opening LAYERSTATE, over the tiles Previous (LAYERP), Change to Current Layer (LAYCUR), Copy Objects to New Layer (COPYTOLAYER), Layer Walk (LAYWALK), VP Freeze in All Viewports except Current (LAYVPI), Merge (LAYMRG) and Delete (LAYDEL). Block, new on Home, holds Insert large (INSERT, with the gallery of the drawing's blocks under its arrow, as Insert's), Create (BLOCK), Edit (BEDIT) and the Edit Attributes split (Single, EATTEDIT; Multiple, -ATTEDIT); Block ▾ drops Define Attributes (ATTDEF), Manage Attributes (BATTMAN), Synchronize Attributes (ATTSYNC), the Attribute Display split (ATTDISP: Normal, On, Off) and Set Base Point (BASE). Properties holds Match Properties (MATCHPROP) and, each after its 20-wide icon and 180 wide (160 in a medium panel), the boxes Object Color (the colour of new objects, CECOLOR; its icon is the button of Select Color), Lineweight and Linetype (CELTYPE); the Lineweight box sets the lineweight of new objects (CELWEIGHT: ByLayer, ByBlock, Default, then 0.00 mm to 2.11 mm, each drawn as a line of its width, one undo step) and shows it as typed; the slide-out Properties ▾ holds Plot Style, greyed as the presenter keeps no plot style for new objects, Transparency (CETRANSPARENCY: ByLayer, ByBlock and 0, 10 to 90 per cent; a line drawn after a pick of 30 carries 30 per cent) and List (LIST). The launcher at the right of the title, the arrow, shows the Properties palette (PROPERTIES), or hides it where it is shown (PROPERTIESCLOSE). Groups holds Group (GROUP) and the tiles Ungroup, Group Edit (GROUPEDIT) and Group Selection On/Off, drawn pressed while PICKSTYLE is 1 and switching it as Ctrl+Shift+A does; Groups ▾ drops Group Manager (CLASSICGROUP). Utilities holds the Measure split (MEASUREGEOM: Quick, Distance, Radius, Angle, Area; Volume is left out) and the tiles Quick Select (QSELECT), Select All (SELECTALL) and Quick Calculator (QUICKCALC); Utilities ▾ drops ID Point (ID) and Point Style (PTYPE). Clipboard holds the Paste split, whose top always runs PASTECLIP and whose arrow drops Paste, Paste as Block, Paste to Original Coordinates and Paste Special, and the tiles Cut, Copy Clip (COPYCLIP) and Copy with Base Point (COPYBASE). View, which AutoCAD 2021's Home has for its model documentation and so is ours, holds Zoom Extents and Pan. A panel with a slide-out shows its name and ▾ and opens it from its title; Clipboard and View have none. Copy Nested Objects, Group Bounding Box, Paste as Hyperlink and Locked Layer Fading are not there, the presenter having no NCOPY, GROUPDISPLAYMODE, PASTEASHYPERLINK nor LAYLOCKFADECTL. Every button runs a command through the same path as typing it. A thing that is not built yet has no button, or one that says so (greyed, with a tooltip); the comparison with AutoCAD 2021, tab by tab, is in the drawing plan, 72-autocad-gap.md.

Insert holds six panels, as AutoCAD 2021 parts them (Location, which needs Autodesk's services, is left out). Block: Insert, whose top opens the Blocks palette (INSERT) on Current drawing and whose arrow drops the gallery of the drawing's blocks, a tile for each in the palette's order (by name, letters of either case alike), "No blocks in this drawing" where it has none; a click on a tile starts INSERT placing that block at once, with the palette's options (the point asked, scale 1, no rotation), and lists it first under the palette's Recent tab. Under the tiles, Recent Blocks… and Blocks from Other Drawings… open the palette on its Recent and Other drawing tabs; Favorite Blocks… is greyed. Beside it the Edit Attribute split of Single (EATTEDIT) and Multiple (-ATTEDIT). Its title, Block ▾, drops Attribute Display, a split of Normal, On and Off (ATTDISP). Block Definition: the Create Block split of Create Block (BLOCK) and Write Block (WBLOCK), Define Attributes (ATTDEF), Manage Attributes (BATTMAN) and Block Editor (BEDIT); Block Definition ▾ drops Set Base Point (BASE) and Synchronize (ATTSYNC). Reference: the Attach split of Attach DXF… (XATTACH), Attach Image… (IMAGEATTACH) and Attach PDF… (PDFATTACH); Clip (IMAGECLIP) and Adjust (PDFADJUST); Underlay Layers (PDFLAYERS); and two splits that show the drawing's setting: the frames, "Display and Plot Frames" where IMAGEFRAME is 1, with Display but Not Plot Frames (2) and Hide Frames (0) in its menu, and "Snap to Underlays ON" or OFF as PDFOSNAP is, with the other in its menu; the button runs the setting it shows again, and a drawing with no image yet is given its variables of images by IMAGEFRAME, as AutoCAD 2021 gives them. Reference ▾ drops Edit Reference (REFEDIT), and the launcher at the right of its title opens External References (the palette, ER). Import: Import (IMPORT) and Combine Text (TXT2MTXT). Data: Field (FIELD), Update Fields (UPDATEFIELD), OLE Object (INSERTOBJ) and Hyperlink (HYPERLINK). Linking & Extraction: Data Link (the Data Link Manager, DATALINK), Download from Source and Upload to Source (DATALINKUPDATE answered Update and Write) and Extract Data (DATAEXTRACTION). Every button is off with no drawing open.

Annotate holds seven panels, as AutoCAD 2021 parts them. Text: the Multiline Text split of Multiline Text (MTEXT) and Single Line (TEXT); Check Spelling (SPELL), Text Align (TEXTALIGN) and Justify (JUSTIFYTEXT) by their icons; the text style box, the Find Text box and the text height box, 200 pixels wide each. Words typed in the Find Text box and Enter start FIND with them in Find what, as AutoCAD's does, and stay in the box; the height box is the one that sets the height TEXT and MTEXT offer, a value typed and Enter taking it at once. Text ▾ drops Scale (SCALETEXT) and the Text Layer box, "Use Current" then the layers of the drawing: a layer picked is where the next TEXT and MTEXT go (TEXTLAYER, kept by the session; the current layer stays the current layer), and Use Current puts them back on it; the launcher opens the Text Style dialog (STYLE). Dimensions: Dimension (DIM); the dimension style box with Break (DIMBREAK), Adjust Space (DIMSPACE) and Jog Line (DIMJOGLINE); the Dim Layer box (DIMLAYER, "Use Current" or a layer) with Inspect (DIMINSPECT), Update (-DIMSTYLE Apply) and Reassociate (DIMREASSOCIATE); and the Linear split (Linear, Aligned, Angular; Arc Length, Radius, Diameter; Ordinate, Jogged), Quick (QDIM) and the Continue split (Continue, Baseline). Dimensions ▾ drops Tolerance (TOLERANCE), Oblique and Text Angle (DIMEDIT), Left, Center and Right Justify (DIMTEDIT: the dimension is picked, then the text goes to the left, the middle or the right) and Override (DIMOVERRIDE), all by their icons; the launcher opens the Dimension Style Manager (DIMSTYLE). Centerlines: Center Mark (CENTERMARK) and Centerline (CENTERLINE). Leaders: Multileader (MLEADER), the multileader style box, which lists Standard in a drawing with no style yet as AutoCAD's blank drawing does, Align (MLEADERALIGN) with Add Leader (MLEADEREDIT), Collect (MLEADERCOLLECT) with Remove Leader (MLEADEREDIT, Remove leaders); the launcher opens the Multileader Style Manager. Tables: Table (TABLE), the table style box, Download from Source and Upload to Source (DATALINKUPDATE) with Extract Data (DATAEXTRACTION) and Link Data (DATALINK); the launcher opens the Table Style dialog. Markup: Wipeout (WIPEOUT) and the Revision Cloud split (Rectangular, Polygonal, Freehand). Annotation Scaling: the Add Current Scale split (AIOBJECTSCALEADD; Remove Current Scale, AIOBJECTSCALEREMOVE), Scale List (SCALELISTEDIT) and Add/Delete Scales (OBJECTSCALE). Every button is off with no drawing open.


A box of the ribbon that holds a value takes it as AutoCAD 2021's do: type 10 in the Scale box of the Hatch Editor tab and press Enter, and the hatch changes at once (one undo step, the canvas redrawn), the text of the box selected and the keyboard left in it; Tab or a click elsewhere takes the value as well, and Esc puts the old value back. This holds for every box of the Hatch Creation and Hatch Editor tabs (Angle, Scale, Spacing, Gap tolerance), the Array tab (Columns, Rows, Levels, Between, Total, Elevation), the Image and PDF tabs (Brightness, Contrast, Fade), the Text Editor tab (height, oblique, tracking, width; Enter and Esc give the keyboard back to the words) and the Text height box of Annotate; the rows of Properties did it already. A half-typed number is never taken: nothing is written until Enter or the box is left, so typing 10 does not first make a scale of 1. A host's own ribbon box gets the same behaviour from the attached property `CommitOnEnter.Enabled` (a box bound with `UpdateSourceTrigger=LostFocus`; `CommitOnEnter.Finished` bubbles from it after Enter or Esc).

As the window narrows, the panels shrink as AutoCAD's do, in steps taken in turn until they fit and given back in reverse as the window widens. Home, 1 695 pixels wide (Draw 224, Modify 250, Annotation 183, Layers 274, Block 161, Properties 266, Groups 70, Utilities 97, Clipboard 80, View 90), has every panel full in a window 1 719 wide or wider; the width of a panel is that of its content, but for Draw, Modify, Utilities and Clipboard, which are no narrower than AutoCAD's own. Narrower, Block, Properties (its boxes 180 pixels wide go to 160), Modify, Annotation and Layers go medium (Layers drops only the text of Make Current and Match Layer, its box holding the width as AutoCAD's does), then View, Clipboard, Utilities and Groups collapse in that order, then Properties, Block, Layers and Annotation collapse; Draw never shrinks and Modify never collapses. At a ribbon 1 904 pixels wide (a window 1 920 wide, less its border) no step is taken, at 1 584 three, at 1 424 four, at 1 264 ten, at 1 084 twelve and at 884 all thirteen, which leaves Draw full, Modify medium and the other eight panels 61 pixels wide each; AutoCAD takes the same number of steps at those widths, but two at 1 584, where ours takes Modify medium a step early. At 1 600 pixels the first three steps are taken. A medium panel keeps every button and drops the labels of its small ones, the tooltip still naming the command. A collapsed panel is one big button, its icon, name and ▾: a click drops the full panel below it, with its title bar and a pin; Esc or a click elsewhere closes it unless it is pinned, a command picked in it closes it, and near the window's right edge it shifts left to stay inside. The contextual Text Editor tab fits 1 600 pixels the same way: Spell Check and Tools collapse and Insert becomes a column of small buttons; narrower, Options goes small and Paragraph collapses. Style, Formatting and Close never shrink, and Close stays the last panel. A panel with a dialog has its launcher, the small arrow at the right of its title (`RibbonPanel.Launcher`), on the title bar of its drop-down too when it is collapsed: Paragraph opens the Paragraph dialog from either. Insert, 1 046 pixels wide (Block 110, Block Definition 207, Reference 289, Import 102, Data 146, Linking & Extraction 192), is full in a window 1 070 wide or wider; narrower, Linking & Extraction goes medium, then Data below 986 and Reference below 910, which drop the labels of their small buttons, then Import collapses below 780, Data below 739 and Linking & Extraction below 730; Block and Block Definition never shrink. At a ribbon 1 904 pixels wide no step is taken, and at 1 584, 1 424, 1 264 and 1 084 none, at 884 three, where AutoCAD takes six: our Import is 102 pixels wide where AutoCAD's is 270. Annotate, 1 550 pixels wide (Text 282, Dimensions 340, Centerlines 105, Leaders 272, Tables 246, Markup 108, Annotation Scaling 197), is full in a window 1 574 wide or wider; narrower, Annotation Scaling goes medium (its labels gone), then Tables collapses below 1 479, Markup below 1 294, Annotation Scaling below 1 247, Leaders below 1 206 and Dimensions below 995; Text and Centerlines never shrink. At a ribbon 1 904 and 1 584 pixels wide no step is taken, at 1 424 two, at 1 264 three, at 1 084 five and at 884 all six, which leaves Text and Centerlines full and the other five panels 61 pixels wide each, as AutoCAD's ribbon does; AutoCAD takes one step at 1 584 and four at 1 424 and 1 264, its panels being a little wider than ours. Each tab's steps are the `Steps` of its `RibbonStrip`, as `View:Collapsed; Clipboard:Collapsed`.

Parametric, View, Manage and Output are laid out as AutoCAD 2021's, with the sizes above, and the Import panel of Output is gone (Import is on Insert). **Parametric**, 616 pixels wide (Geometric 250, Dimensional 232, Manage 134), never shrinks: it is full in a window 640 wide. Geometric holds Auto Constrain large, the twelve constraints as three rows of four tiles, a separator, then Show/Hide, Show All and Hide All. Dimensional holds the Linear split large, Aligned large, Radius, Diameter, Angular and Convert as tiles, a separator, then Show/Hide, Show All and Hide All of the dynamic constraints. Linear's arrow drops Linear, Horizontal and Vertical (DCLINEAR, DCHORIZONTAL, DCVERTICAL), and its label follows the pick. Show All and Hide All run DCDISPLAY over every object of the drawing and answer Display or Hide, the objects selected before selected again after it. The title Dimensional ▾ drops the two constraint modes, Dynamic Constraint Mode and Annotational Constraint Mode, which are CCONSTRAINTFORM 0 and 1: the one in force is pressed, a click puts the other in force, and typing CCONSTRAINTFORM moves the pressed one (`ParametricRibbonViewModel.IsDynamicMode` and `IsAnnotationalMode`). The launcher of Geometric opens Constraint Settings on its Geometric tab and that of Dimensional on its Dimensional tab (CONSTRAINTSETTINGS typed opens on Geometric, as before). **View**, 1 123 pixels wide, is full in a window 1 147 wide or wider; it has Viewport Tools (the UCS Icon large toggle, pressed while UCSICON is on, a press running UCSICON ON or OFF), Named Views (a box 200 wide listing Unsaved View, then the views of the space shown by name, letters of either case alike; a pick restores the view as -VIEW Restore does, and the box goes back to Unsaved View where the drawing loses the view or another drawing is shown; New View opens the View Manager on its New View dialog, as AutoCAD's NEWVIEW does; View Manager runs VIEW), Model Viewports (the Viewport Configuration split large, whose top opens Viewports (VPORTS) and whose list holds the ten layouts -VPORTS makes, Single, Two: Vertical, Two: Horizontal, Three: Right, Left, Above, Below, Vertical and Horizontal, and Four: Equal, each running -VPORTS with its answers; Named opens Viewports on its Named Viewports tab; Join and Restore start -VPORTS Join and Restore, which wait for the viewport or the name), Palettes (Tool Palettes large; Properties, a large toggle pressed while the Properties panel is shown, which has left Interface; the tiles Command Line, pressed while the command line is shown, Layer Properties, Quick Calculator, Design Center and External References) and Interface (the Drawings large toggle, and a launcher, "Global settings: Appearance", that opens Global settings on Appearance; AutoCAD's opens Options on Display). Ours follow AutoCAD's: Navigate, Views and the drafting toggles of the status bar. View's steps are Drafting and Navigate medium, Views collapsed, Drafting and Navigate collapsed, Named Views and Model Viewports medium, then Interface collapsed; at a ribbon 1 904, 1 584, 1 424 and 1 264 pixels wide none is taken, at 1 084 one and at 884 six, and Viewport Tools and Palettes never shrink. **Manage**, 686 pixels wide (Customization 217, Applications 129, CAD Standards 116, Cleanup 224), and **Output**, 392 (Plot 284, Export to DWF/PDF 108), have no steps: every panel is full down to 884 pixels. Output's Plot holds Plot, Batch Plot (PUBLISH) and Preview large, then Page Setup Manager and Plot Stamp; it has no launcher, as the presenter has no Plot and Publish page to open. Export to DWF/PDF is one large Export split: its top runs EXPORTPDF, its list holds Export PDF, Export DWF (EXPORTDWF, which says the presenter has no DWF writer) and Export (EXPORT), and the top runs the item picked last under its label Export.

The contextual Hatch Creation tab, shown while HATCH or GRADIENT runs (HPDLGMODE 2), and the Hatch Editor tab, shown while hatches are selected, are green as the Array tab is: Boundaries, Pattern, Properties, Origin, Options and Close, described with the hatch commands in [the commands](editing.md#the-commands). The Hatch dialog stays for HPDLGMODE 1 and for the Hatch Settings button of Options.

The contextual Image tab, shown while images alone are selected, is green as the others are: Adjust (Brightness, Contrast and Fade, sliders and boxes from 0 to 100, greyed for a bitonal picture), Clipping (Create Clipping Boundary, Remove Clipping), Options (Show Image, Transparency, and the Frame box of IMAGEFRAME) and Close Image, described with the raster types in [the commands](editing.md#the-commands).

The contextual PDF Underlay tab, shown while PDF underlays alone are selected, is green as the others are: Adjust (Fade and Contrast, sliders and boxes from 0 to 100, Monochrome and Adjust colors for background), Clipping (Create Clipping Boundary, Remove Clipping), Options (Show Underlay, the Frame box of PDFFRAME, Underlay Layers) and Close PDF Underlay, described with PDFATTACH in [the commands](editing.md#the-commands).

With DYN on, dynamic input works at the cursor as AutoCAD 2021's: what is typed and what is asked show 18 pixels right of the crosshair and 14 below it, and follow it, while the command line keeps its own copy of every prompt and every key. At the Command prompt the box opens with the first key, "REC", and the AutoComplete list opens under it after the delay, ranked as the command line's, whose own list stays closed; Enter runs the row highlighted, a click on a row runs it and a click on the canvas closes it. In a command the box shows the prompt as AutoCAD words it, "Specify next point or", and a ↓ where the prompt has options: ↓ opens their menu at the cursor, one keyword a row, where the arrows move, Enter or a click picks one, Esc closes the menu alone and a key typed closes it and goes into the field. At a point prompt the box has two fields, X and Y; with a base point they are relative to it, as DYNPIFORMAT 0, so RECTANG's other corner shows 1 200.00 and 800.00 for a corner 1 200 by 800 from the first. # typed first makes a value absolute and @ relative. At a second point the fields are dimensional input instead, the two fields alone with no dimension line, extension lines or angle arc drawn: the length beside the middle of the rubber band, 8 pixels off it on its left as it runs from the base point, and the angle 50 pixels along the band from the base point, 8 pixels off it on the other side, so that neither covers the band. The box of the prompt never hides a field: where it would, as when it stands left of a cursor by the canvas's right edge, the length moves to the band's other side or the box moves up or down clear of the fields, whichever moves less. That is the next point of LINE and PLINE, the second point of ARC, of CIRCLE's 2P, of ELLIPSE's axis, of POLYGON's edge, of XLINE and RAY, of MOVE, COPY and STRETCH, of OFFSET's distance shown by two points, and of a grip stretched or moved; and, as AutoCAD 2021's pointer input takes a second or next point in polar form by default, the second and third points of CIRCLE's 3P, each from the point before, its first point X and Y as any first point, the second point of MIRROR's line, of ROTATE's and SCALE's Reference by two points, of ALIGN's destinations, of BREAK's First point, of DIST, of TEXT's Align and Fit, of DIMLINEAR's and DIMALIGNED's second extension line, of DIMCONTINUE and DIMBASELINE, the endpoints of an angle in MEASUREGEOM and DIMANGULAR, and the next points of AREA and of REVCLOUD's Polygonal. The corner of a box, as RECTANG's other corner, ZOOM's window, MTEXT's or REVCLOUD's Rectangular, and the location of a dimension line or a leader keep the X and the Y from the base point. Tab locks the field typed into, drawn with a lock, and moves to the other; a comma moves from X to Y and a < from the length to the angle. Enter takes the point from the locked fields and the cursor's value in the other, and a coordinate typed whole, "500,300" or "400<90", is relative to the base point unless # comes first. A prompt for a distance from a known point shows the length field alone, as the radius of CIRCLE or POLYGON and ELLIPSE's distance to the other axis, and a prompt for an angle from one the angle field alone, as ROTATE's and that of a grip's ROTATE mode, by the grip; a number typed shows in the field and Enter gives it to the command. SCALE's factor keeps the one box, pointer input only as AutoCAD's, showing the factor alone (0.50 with the cursor 0.5 from the base point), and so does a grip's SCALE mode. The angle field counts from the X axis of the UCS turned by ANGBASE wherever it shows: ROTATE's new angle after Reference too, which AutoCAD's help calls "a new, absolute angle", and the next point of PLINE after an arc segment; Autodesk's help names no field counted from a reference line. An angle override typed, `<30` at LINE's next point, shows in the angle field locked, 30 wherever the cursor is, until the point is given; a length typed then runs along it. The fields measure to the point the rubber band ends at, not to the cursor: with an endpoint snapped, or ortho or polar holding the band, the length and the angle are those of that point and the length stands by the middle of the band to it; a distance typed alone and Enter go along that band too, as on the command line. With ortho on, LINE's next point from (1 000, 1 000) with the cursor at (1 300, 1 400) shows 400 and 90, and 300 typed ends the line at (1 000, 1 300). A field that holds a number holds the rubber band, the preview and a click with it: with 300 locked as the length of a line from (1 000, 1 000) and the cursor at (1 300, 1 400), 500 away, the preview and a click end at (1 180, 1 240), and 250 typed at CIRCLE's radius draws the preview 250 in radius wherever the cursor is. Esc at the Command prompt clears what is typed and the list at once and writes *Cancel*; in a command it cancels it. With DYN off the cursor carries nothing and the command line does all of it. With Select by dragging off, click at one corner and click at the other for a box, or press on nothing and drag round the objects for a lasso: drawn clockwise it is a window, counter-clockwise a crossing.

F12 (DYN) shows the prompt, the cursor's coordinates or its distance and angle from the base point, and what you type beside the cursor; Global settings > Canvas and cursor > Input sets it on or off at start. Ctrl+9 hides or shows the command line; typing still reaches it while hidden. Ctrl+1 shows Properties when it is hidden and hides it when it is shown, as AutoCAD's, and Ctrl+2 does the same for the Drawings list and Ctrl+3 for the whole ribbon (its tabs and panels; the canvas takes the room); all three are rows of Global settings > Shortcuts (PROPERTIES, DRAWINGS and RIBBON) and can be given other keys. A settings file saved before Ctrl+2 or Ctrl+3 existed gets it once when it is read, if the key and its command are both unbound; the file lists the default keys it has seen, so a key cleared afterwards stays cleared. Typed, PROPERTIES still shows Properties and PROPERTIESCLOSE hides it, and RIBBON shows the ribbon and RIBBONCLOSE hides it, as AutoCAD's, with or without a drawing open. COMMANDLINE (`CLI`) shows the command line and COMMANDLINEHIDE hides it, as Ctrl+9 does. HIDEPALETTES (`POFF`) hides Properties, the Drawings list and the command line at once, remembering which were shown, and SHOWPALETTES (`PON`) shows those again; Ctrl+Shift+H does the one or the other, and the ribbon stays as it is. QUIT (`EXIT`) closes the window as its x does, asking once about the drawings with changes not saved. -OSNAP (`-OS`) writes `Current osnap modes: End,Mid` (the rarer ones as AutoCAD 2021 names them: `Near`, `Gcen`, `App Int`) and asks `Enter list of object snap modes:`; END,MID or NONe replaces the object snaps that are on (each by its first three letters or a longer start of its word), a name that is none of them says `Invalid object snap modes.` and is asked again, and whether OSNAP is on is left as it is; OSNAP itself still asks ON or OFF, and OS opens Drafting Settings. All of these work with no drawing open. `--dialog color` and `--dialog truecolor` draw Select Color in screenshot mode, `--toggle DYN` switches dynamic input, and `--type REC` types text left unsent with its AutoComplete list, `--type {Down}` the options menu of the prompt.

The status bar starts as AutoCAD 2021's does: SNAP and ORTHO off (SNAPMODE 0, ORTHOMODE 0), POLAR, OTRACK and OSNAP on (AUTOSNAP 63, OSMODE without 16384), and DYN as Global settings say, on by default (DYNMODE 3). SNAP, GRID, ORTHO and the lineweights are each drawing's own, as AutoCAD keeps SNAPMODE, GRIDMODE, ORTHOMODE and LWDISPLAY in the drawing: a drawing opened shows SNAP and GRID, and their spacing, from its `*Active` viewport, ORTHO from its `$ORTHOMODE` and its lineweights from its `$LWDISPLAY`, and its tab shows them again whenever it is shown, while POLAR, OTRACK, OSNAP and DYN stay the window's, as AutoCAD keeps them in the registry. A drawing with a snap of 2.5 by 5 and a grid of 10 by 20 steps the cursor by 2.5 across and 5 up, and draws its grid lines 10 apart across and 20 up, every fifth the stronger (GRIDMAJOR 5); a grid spacing of nought takes the snap's, as GRIDUNIT 0 does. A drawing that has no such value takes the window's: SNAP and ORTHO off, GRID and the lineweights as Global settings say, a spacing of 10; a new drawing is given the GRID of Global settings. Switched, by the status bar, F7 to F9, the commands, the variables or Drafting Settings, each is written into the drawing shown, and the snap spacing too, as AutoCAD saves them, so that a save keeps them. OSNAP's arrow on the status bar lists the fourteen snaps; Object Snap Settings... opens Drafting Settings (OSNAP, OS and DDOSNAP always on the Object Snap tab; DSETTINGS, DS and SE on the tab used last) with the Snap and Grid, Polar Tracking and Object Snap tabs, Object Snap Tracking On (F11) among them. OTRACK on the status bar, before OSNAP and on as in AutoCAD, switches object snap tracking: at a point prompt, a rest of half a second on a snap point acquires it, marked by a small cross, and the cursor is then held on the level and upright lines through it (every polar angle while POLAR is on) and where two of them cross, the lines drawn dotted. EXT, PAR, APP (or APPINT) and INS typed at a point prompt take Extension, Parallel, Apparent Intersection and Insertion for that point alone. A POINT is a node. `ObjectSnap.Override` takes snaps for the next point alone, whether OSNAP is on or not, as END typed at a prompt does; `CadSnapMarker` gives each kind's marker as strokes in pixels, 9 across.

The snap and the grid are laid from SNAPBASE and turned by SNAPANG, each drawing's own from its `*Active` viewport, as AutoCAD 2021 keeps them, and set by SETVAR or typed alone: SNAPBASE a point, SNAPANG an angle in the drawing's angle units, kept from nought up to a whole turn (400 is kept as 40, -30 as 330), GRIDMAJOR 1 to 100 ("Requires an integer between 1 and 100."), SNAPSTYL 0 or 1 ("Requires 0 or 1 only.") and SNAPISOPAIR 0 to 2. A snap of 10 turned by 30 degrees puts the cursor at (9, 4) on (8.6603, 5); ortho holds the cursor along the turned axes, the polar angles are counted from SNAPANG, the crosshair is drawn along it, and the grid is drawn through SNAPBASE along it, a major line every GRIDMAJOR; zoomed out until its lines come within 6 pixels, the grid shows its major lines alone, then 2, 5, 2 and on times them. SNAPSTYL 1 makes the snap isometric: its points lie where the 30, 90 and 150 degree lines cross, as far apart along each as the vertical spacing, which the drawing keeps halved, as AutoCAD does (SNAP Style Isometric 5 in AutoCAD keeps 8.6603 by 2.5); SNAP typed then asks the vertical spacing, and 3 keeps 5.1962 by 1.5. Ortho, the crosshair and the grid follow the plane SNAPISOPAIR names: Left on the 90 and 150 degree axes, Top on 30 and 150, Right on 30 and 90. ISOPLANE says "Current isoplane: Left", asks "Enter isometric plane setting [Left/Top/Right] <Top>:", Enter taking the next, and F5 or Ctrl+E turns to the next at any time, a command running going on, writing "<Isoplane Top>". A point typed is taken as typed, neither snapped nor held by ortho, as in AutoCAD.

The Snap and Grid tab of Drafting Settings is AutoCAD 2021's, the dialog 560 by 540 on every tab. On the left: Snap On (F9); Snap X and Y spacing (SNAPUNIT) with Equal X and Y spacing, ticked as the dialog opens where the snap and the grid are each as wide as high, which copies a spacing typed in an X box to its Y box and back; Polar distance (POLARDIST); Grid snap or PolarSnap (SNAPTYPE), and Rectangular or Isometric snap (SNAPSTYL). On the right: Grid On (F7); the dotted grid in 2D model space, the Block editor and Sheet/Layout (GRIDSTYLE 1, 2 and 4); Grid X and Y spacing (GRIDUNIT, nought taking the snap's) and Major line every (GRIDMAJOR); Adaptive grid, Allow subdivision below grid spacing, Display grid beyond Limits and Follow Dynamic UCS (GRIDDISPLAY 2, 4, 1 and 8). PolarSnap greys the snap spacing, Equal X and Y spacing and the snap style and opens Polar distance; the isometric snap greys the X boxes and Equal X and Y spacing, and the Y boxes show the full vertical spacing, twice what the drawing keeps: a snap of 8.6603 by 2.5 shows 5, and 10 typed there writes 17.3205 by 5, as SNAP Style Isometric 10 does, the grid the same way, nought kept nought. Allow subdivision is greyed, and kept, while Adaptive grid is off. Apply and OK write the values changed, the drawing's into its `*Active` viewport and SNAPTYPE, POLARDIST and GRIDSTYLE into the settings file, as AutoCAD keeps them in the registry. A box refused is edged in red, the caret put in it and the reason said at the foot, nothing written: "The snap spacing must be a number above nought.", "The polar distance must be a number.", "The grid spacing must be a number, nought or above." and AutoCAD's "Requires an integer between 1 and 100." for Major line every; a grid spacing typed below nought is taken as its size, as AutoCAD's GRIDUNIT takes -1,1 as 1,1, and a polar distance below nought is kept, as SETVAR POLARDIST keeps -1, so that the dialog opened over it OKs as it is. With SNAP on and PolarSnap chosen, a point held on a polar angle from the base point is taken a whole number of POLARDIST from it, or of the snap X spacing where POLARDIST is nought: with 250, a line from (1 000.3, 1 000.6) to a cursor at (1 620, 1 000.9) ends at (1 500.3, 1 000.6); off a polar angle the cursor goes free and the grid is not snapped to. PolarSnap steps along the lines of object snap tracking too, from the point acquired: with 25, a cursor 37 along the level line from a point acquired is taken 25 along it.

The grid is drawn by GRIDSTYLE and GRIDDISPLAY, as AutoCAD 2021's help describes them. GRIDSTYLE 1 draws it in model space as dots where its lines would cross, 2 in the Block Editor, 4 in the layouts; the grid is drawn in model space, the Block Editor and a layout's paper space. A layout's paper space has a snap and a grid of its own, as AutoCAD 2021's: GRID, SNAP, F7, F9 and every snap and grid variable typed there (GRIDMODE, GRIDUNIT, GRIDDISPLAY, GRIDMAJOR, SNAPMODE, SNAPUNIT, SNAPBASE, SNAPANG, SNAPSTYL, SNAPISOPAIR) set the sheet's, kept in the viewport 1 of the layout, and model space's is left as it was, each shown again with its space: GRIDUNIT 3,3 typed in Layout1 writes 3 by 3 into its viewport 1, model space's GRIDUNIT still reading 4,6 there. The sheet's grid is drawn on the paper 8 and 18 per cent darker than it, by the spacing, major lines, GRIDDISPLAY bits, base point, angle and isometric plane of that viewport (10 by 10, every 5 and 3 from the paper's origin where the layout has none yet), within the layout's limits without 1, and the snap on the paper steps by its spacing. Through a viewport (MSPACE) the snap and grid are that viewport's own, set and switched there as AutoCAD 2021 keeps them, the sheet's and model space's left as they were: GRID 9 through Layout1's viewport writes 9 by 9 into it. Its grid is drawn inside its frame alone, laid in model space as the viewport shows it, turned with a twisted view, within LIMMIN and LIMMAX without 1, and no paper grid is drawn then; a viewport of the room at 1:500 with a grid of 1 000 draws a line every 2 units of paper. A viewport whose grid is on draws it in its frame whether it is worked through or not, on the paper and through another viewport, as AutoCAD 2021 keeps a grid by viewport, F7 and GRID switching that of the space shown alone. Through a viewport the grid is model space's, in dots by GRIDSTYLE 1 (2D model space) and not by 4 (layouts). A twisted viewport keeps its grid within LIMMIN and LIMMAX as they are turned on the sheet: through a viewport at 1:500 twisted by 30 degrees, with limits 2 000 by 2 000 about the middle of its frame, the line 900 above the middle is drawn, 0.9 of paper across from it, and the line 1 400 above it is not. U and REDO show the snap and grid the drawing holds again, of model space and of each viewport, as AutoCAD's UNDO takes back a snap or grid set, GRID and SNAP being a step each and F7 and F9 joining the step of the command before them (see [the command line](editing.md)): after a line drawn and GRID 50 typed through a viewport, U takes GRID 50 back and shows that viewport's grid off and of the spacing it had, the line staying until the next U. GRIDDISPLAY without 1 keeps the grid of model space within LIMMIN and LIMMAX; without 2 it is not thinned out as the view zooms out, and not drawn at all where its lines would come within 6 pixels, as AutoCAD shows no grid too dense, and the command line says "Grid too dense to display" once as the grid comes on, or the view or the spacing goes to where it is too dense, not again at each redraw; 4 with 2 adds lines between as the view zooms in, a fifth of the spacing apart with GRIDMAJOR 5, so that a grid of lines 60 pixels apart is drawn every 12; 8, following the dynamic UCS, is kept and written into the drawing but not drawn by, as AutoCAD 2021 writes GRIDDISPLAY 8 into its `*Active` viewport (group 60) and has no dynamic UCS in a plan. Object snap tracking follows the snap: its level and upright lines from a point acquired run along the axes turned by SNAPANG (with 30 degrees at 30, 120, 210 and 300), or along those of the isoplane (on the top plane at 30, 150, 210 and 330), and the polar lines from the base point are turned by SNAPANG too, as the polar angles are.

SNAP and GRID typed ask as AutoCAD 2021 does, checked in its console. SNAP: "Specify snap spacing or [ON/OFF/Aspect/Legacy/Style/Type] <10.0000>:", Aspect left out while the snap is isometric; the spacing offered is A where the X and Y spacings differ, and Enter then takes Aspect. A spacing, Enter, Aspect ("Specify horizontal spacing <10.0000>:", "Specify vertical spacing <10.0000>:"), Legacy ("Maintain legacy behavior of always snapping to the grid? [Yes/No] <No>:", SNAPGRIDLEGACY, kept in the settings file as AutoCAD keeps it in the registry: Yes holds the cursor on the snap grid with no point asked too, so that with SNAP on the crosshair, the coordinates of the status bar, the object under the cursor and a click or a box corner go from one grid point to the next, a click at (1 130, 70) over a snap of 500 picking the line through (1 000, 0); No, as by default, holds it only while a point is asked), Style and Type all turn SNAP on, OFF alone off; nought or less is refused with "Value must be positive and nonzero." and text with "Requires numeric distance, two points, or option keyword.", both asked again. Style asks "Enter snap grid style [Standard/Isometric] <S>:": Isometric then "Specify vertical spacing <10.0000>:", a snap of 10 by 10 with a grid of 4 by 6 turning into a snap of 8.6603 by 2.5 for 5 and a grid of 10.3923 by 3, as AutoCAD keeps them; Standard then "Specify snap spacing or [Aspect] <5.0000>:", the grid taking twice its kept Y spacing, 6 by 6. Type asks "Enter snap type [Polar/Grid] <Grid>:", and Polar "Enter polar snap tracking distance <0.0000>:" (SNAPTYPE, POLARDIST). GRID: "Specify grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect] <10.0000>:", Aspect left out while the snap is isometric, where the spacing typed is the vertical one (1 keeps 1.7321 by 0.5); 2x is twice the snap spacing, 0 or Snap takes the snap's; below nought is refused with "Value must be positive.", other text with "Requires a distance, numberX, or option keyword.". Major asks "Enter the number of grid divisions per major grid line <5>:", aDaptive "Turn adaptive behavior on [Yes/No] <Yes>:" and, where Yes, "Allow subdivision below grid spacing [Yes/No] <No>:", Limits "Display grid beyond Limits [Yes/No] <Yes>:", Follow "Follow dynamic UCS [Yes/No] <No>:", Aspect "Specify the horizontal spacing(X) <10.0000>:" and "Specify the vertical spacing(Y) <10.0000>:"; every answer but OFF turns GRID on.

The variables of the snap and the grid are set by SETVAR or typed alone, refused in AutoCAD's words. SNAPUNIT takes a point of two spacings above nought, 0,5 and -1,1 getting "Cannot set SNAPUNIT to that value." and ending; with an isometric snap it reads the Y doubled and keeps what is typed, so that 4,6 reads 4,12, as AutoCAD does. GRIDUNIT takes nought and drops a sign, -2,3 kept as 2,3. Text where a point is asked gets "Invalid 2D point.", asked again; a number alone is a distance from the last point towards the cursor, as AutoCAD 2021 takes GRIDUNIT 5, SNAPUNIT 5 and SNAPBASE 5: with the last point at (10, 20) and the cursor at (13, 24), GRIDUNIT 5 is 13,24. A point typed at SETVAR is the variable's value, not a point given, and leaves the last point where it was, as AutoCAD 2021 leaves LASTPOINT: after ID 10,20, SNAPBASE 3,4 and GRIDUNIT 6,7, SNAPUNIT @2,3 is 12,23. POLARDIST takes a distance of either sign, -1 kept as -1 and stepping by the snap X spacing, or two points, "Enter new value for POLARDIST <0.0000>: 0,0" then "Specify second point: 3,4" keeping 5; text gets "Requires numeric distance or two points.". SNAPGRIDLEGACY takes 0 or 1 ("Requires 0 or 1 only."), as SNAP Legacy sets it. SNAPTYPE takes 0 or 1 ("Requires 0 or 1 only."), GRIDSTYLE 0 to 7, GRIDDISPLAY 0 to 15 and SNAPMODE 0 to 2 ("Requires an integer between 0 and 7.", text and 2.5 refused the same way): SNAPMODE 2, which AutoCAD takes and reads back though its help lists only 0 and 1, turns SNAP on and reads 2 until SNAP is switched, each space keeping its own as AutoCAD 2021 keeps it by viewport (model space, a layout's paper and each viewport worked through): SNAPMODE 2 set in model space reads 2 there again after SNAPMODE 1 set on the paper of Layout1; AutoCAD 2021 saves it as SNAP on (group 75 of the `*Active` viewport written 1), and so does the window, no other setting changing with it. LWDISPLAY is shown as OFF or ON, "Enter new value for LWDISPLAY <OFF>:", and takes 0, 1, ON or OFF, anything else getting "Requires 0 or 1 only, or option keyword.".

## See also

- [Editing as AutoCAD does](editing.md): the shortcuts these settings replace.
- [Command plugins](plugins.md): the plugin folder and the trusted folders.
- [Opening, drawing and picking](display.md): the settings' colours and pickbox are what the canvas draws and picks with.
