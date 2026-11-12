# Changelog

The release notes of the GeometryHelper package in full, newest first. The package carries the notes of its own
release only, and a link here for the rest. GeometryHelper.IfcConvert,
GeometryHelper.TeklaConvert and GeometryHelper.CadConvert carry their own notes in their packages.

## Unreleased

**NEW.** The presenter's DIM asks as AutoCAD 2021's own DIM does, read from its resource strings: the options are
Angular, Baseline, Continue, Ordinate, aliGn, Distribute, Layer and Undo (G for aliGn, where L was taken for it and LA for
Layer); a circle clicked offers Radius or Diameter and no Angular ("Specify diameter dimension location or
[Radius/Mtext/Text/text aNgle/Undo]"), an arc also Angular. Continue asks "Select extension line origin to continue or
[Baseline]", Baseline "Select extension line origin as baseline or [Continue]" (each turns into the other), then "Specify
second extension line origin or [Select/Undo] <Select>" with Offset ("Specify offset distance") in a baseline row. The row
goes on from the extension line picked, where DIMCONTINUE and DIMBASELINE still go on from the last dimension. AutoCAD's
console runs the old Dim: command for DIM, so these prompts could not be probed there.

**FIXED.** A radius or a diameter whose words are put within the room of an arrowhead of the rim is laid out as AutoCAD
2021 lays it out, probed with 2 742 of them made by DIMRADIUS and DIMDIAMETER in its console (SHX words, DIMTMOVE 0 to 2,
DIMTAD, DIMTOH, DIMTIH, DIMATFIT 0 to 3, DIMTOFL, DIMSCALE, circles of 8 to 40): of those 941 differed in lines, arrowheads
or words, 31 do now (a point exactly on the rim, which AutoCAD decides by the rounding of its own coordinates; a point at the
centre; level words inside next to the rim on an upright or nearly level line). Words put past the rim stand no nearer than two
arrowheads and the gap and half their width, or two arrowheads where DIMTOH keeps them level, however near they were put:
"R20.0000" put 20.2, 20.5 or 20.9 out in acad.dwt's Standard stands 20.979 out, put 21 out at the point; a point on the rim
is outside it, 1E-7 in is inside. A diameter follows DIMATFIT 0 to 2 as a radius does where the words are put outside it,
and words put free under DIMTMOVE 2 over a radius's centre push the arrowheads out when their near end is within an
arrowhead of it (it was at the centre itself). On a line exactly level, the words DIMTOH or DIMTIH keep level stand along
it with no landing, on an exactly upright one the landing runs left where the line runs down and right where it runs up:
"R20" put 25 out straight down has its landing from (-0.18, -25) to (0, -25) and its words at (-0.7994, -25), where they
went right. Aligned and linear dimensions whose points run straight down land level words outside their line the same way,
448 aligned dimensions of 1.6 and 3.2 at 14 angles and either side: 8 differed, none do now.

**NEW.** In the presenter the snap and grid are undo steps, as AutoCAD 2021's U takes them, probed in its console: GRID,
SNAP, SETVAR and a snap or grid variable typed alone (GRIDMODE, SNAPMODE, ORTHOMODE, SNAPUNIT, GRIDUNIT, GRIDDISPLAY,
SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL, SNAPISOPAIR) are a step each, named by the command, so that after a line, GRID 50
and SNAP 25 the first U takes SNAP 25 back, the second GRID 50 and the third the line (the three used to go with the
line in one U, and an edit undone used to take a snap or grid set before it along). F7, F9, F8 and F5 pressed at the prompt,
and the status bar's SNAP, GRID and ORTHO buttons, join the step of the command before them, one U for both under that
command's name, and are a step of their own where no command came before. The Drafting Settings dialog's OK is one step,
DSETTINGS. A drawing counts as changed after one, as AutoCAD's does (DBMOD 4). Such a step keeps no copy of the drawing:
it holds the viewport's snap and grid as they were and as they became, and an undo or a redo of it writes those back,
reading nothing again; in a drawing of 100 000 lines F7 takes 0.3 ms and its U 0.2 ms with a few kilobytes kept, where a
step that keeps the drawing keeps 20 MB and takes 205 ms to undo. An undo of another step reads the drawing and puts the
settings changed since on it. New `CadDocument.ApplySetting(name, property, read, change, replay, joinsPrevious)` makes such
a step, and `CadCommandSession.OpenUndoStep()` opens the undo step of the command running, for a window that changes a
setting while a command asks.

**NEW.** GeometryHelper DXF has an icon of its own, a white set square and an orange point on a blue tile, at 16, 24, 32,
48 and 256 pixels, on the exe, the taskbar and Alt+Tab, and an icon for the drawing files associated with it; both are
written from their vector drawing by `Icons/make-icons.ps1`. `--register` associates .dxf and .dwg files with the app for
the user alone (`HKEY_CURRENT_USER\Software\Classes`, no administrator): the ProgID `GeometryHelper.Drawing` with the file
icon and the command `"app.exe" "%1"`, in the Open with list of both extensions, and their default only where no program
has them. `--unregister` takes it out and leaves what other programs wrote. `CadAppHost.WindowIcon` sets the window's icon.
**NEW.** `--tab Home` in the screenshot mode of both apps shows the Home tab; a tab name neither app knows exits with 2,
`--tab Nowhere: no such tab.`, where it used to draw the Home tab without a word.

**NEW.** In the presenter the row of a table cell grows while the cell is typed in, as AutoCAD 2021's in-place editor
shows it: each line typed makes the row as high as the words need (in the Standard table style a data cell of two lines
15.07 high instead of 9, of three 22.57), the lines taken out shrink it back, never below the height it stood at, the
rows under it move down with it and the editor stands where the cell now is. The table is drawn from a preview of the
words, `CadTableLive` (`CadInPlaceEdit.TableLive`), and nothing is written to the drawing until the editor closes, which
is one undo step with the rows the preview showed; Esc leaves the table as it was. A key costs the layout of the one cell
(1.4 ms in a table of 50 rows by 20 columns, where laying the whole table out takes 89 ms), and a key that leaves the row
as it was draws nothing anew.

**NEW.** In the presenter the wheel and the middle button go on the ZOOM Previous list, on the paper of a layout, in
model space and through a viewport alike, as AutoCAD 2021's: the rolls and drags made before the view is still are one
entry of the list of the space or the viewport (20 each), and ZOOM Previous goes back through them with the typed ones. A
double click of the middle button, ZOOM Extents, is an entry as typed. `CadDocument.SetViewportView` puts the view a
viewport showed on its list too (the status bar's scale list does, as AutoCAD's ZOOM nXP does).
**NEW.** In the presenter ZOOM, PAN and PLAN typed in model space and on the paper of a layout, ZOOM Previous, and the
wheel and the middle button there are undo steps, as AutoCAD 2021's console, probed: ZOOM 2X, ZOOM 2X and -PAN took one U
each, ZOOM Extents and ZOOM Previous a step each, in model space and on the paper; U and Ctrl+Z move the view back and
REDO and Ctrl+Y forward, in order with the edits between them. The wheel's and the middle button's gestures made one after
another are one step, as through a viewport. A step of the view alone keeps no copy of the drawing, so U takes it back
without reading the drawing again, in a drawing of a million entities too; the drawing counts as changed after a zoom, as
AutoCAD's (DBMOD 16). New `CadDocument.RecordSpaceView(space, view, before, name, realTime)` makes such a step.
**NEW.** ZOOMWHEEL in the presenter, 0 or 1 as AutoCAD 2021's: 1 turns the wheel's direction round, forward zooming out.
Global settings > Canvas and cursor has it as "Reverse the wheel", the settings file as `"wheelZoomReversed"`
(`GlobalSettings.WheelZoomReversed`), and `CadCanvas.WheelReversed` carries it.
**NEW.** The presenter's PLOT prints on the printers of Windows. They are listed in the Printer/plotter list after
GeometryHelper PDF, PNG and JPG (a network printer marked Windows · network) and can be chosen: the Paper size list is then
the printer's own, each sheet as its driver names it at its size, with the margins of its imageable area, and the plot's
sheet stays where the printer has one of its size (ISO A3 on its side becomes the printer's A3, still on its side).
Number of copies, 1 to 999, is for a printer. OK draws the plot as the PDF writer does, with the same page, scale, offset,
turn, plot style table and lineweights, at up to 300 dots to the inch (a sheet that would take more than 40 million pixels,
as A0, at the resolution that keeps it under: 160), and prints it through WPF; "Plot sent to Office printer." or "The plot
was not printed:" and the reason is written on the command line. A layout's plotter, written by Save changes to layout, is
chosen again when the computer has the printer. A printer is a `IPlotPrinter`, which a host or a test gives a queue of its
own for; `WindowsPlotPrinter.Installed()` lists the real ones, and `MainViewModel.WindowsPrinters` takes them.
**CHANGED.** `PlotDialogViewModel` takes its printers as `IPlotPrinter` objects where it took names with a flag for the
network, and `MainViewModel.WindowsPrinters` gives them so; the printers of Windows are no longer greyed in the list.
`CadPlotSettings` has `IsPrinter`, `UsePrinter` and `SavedPlotter`.

**NEW.** Named page setups in the presenter. PAGESETUP opens the Page Setup Manager, as AutoCAD's: the drawing's page setups
with how each plots, the one the layout is set to, and Set Current, New… (a name and what to start with, then the Page
Setup dialog), Modify… and Import… (the page setups of another drawing, a name already taken becoming `Name (2)`). Add… of
the Plot dialog keeps its settings under a name. Each is one undo step named PAGESETUP. In code,
`CadPlotSettings.SaveAsPageSetup`, `SetCurrentPageSetup`, `CurrentPageSetupName`, `ImportPageSetups`, `PageSetupNames` of
another drawing and `CheckPageSetupName` do the same with no window.

**CHANGED.** The Page Setup Manager and its dialogs follow the Design artifact's board (v130), toward AutoCAD 2021's.
The layout's own setup is the first row of the list, `*Layout1*` (`*Layout1 (A2 plan)*` when a named page setup is current,
`*Model*` from model space), chosen when the manager opens, and Modify… on it edits the layout; the blue "Current" tag is
gone. "Current layout" has a layout icon, "Current page setup" stands in the Page setups group, and "Selected page setup
details" is a group of its own with Device name, Plotter, Plot size with its orientation (`594.00 x 420.00 mm (Landscape)`),
Where and Description (`PageSetupInfo`). A box "Display when creating a new layout", unticked, keeps the new system variable
SHOWPAGESETUPFORNEWLAYOUTS (0 or 1, kept by the session; the presenter makes no layouts of its own). Close is orange-red, as in
every manager, with a Help button, and OK comes before Cancel, with a Help button, in New Page Setup, Add Page Setup, Import
Page Setups and the Plot dialog; Help opens the guide's page on plotting on the documentation site. Right-click on a row of the
list gives Set Current, Rename (the name becomes a box to type in) and Delete, one undo step named PAGESETUP each; the layout's
row has Set Current alone. In code `CadPlotSettings.RenamePageSetup` renames a page setup (a layout set to it follows it) and
`DeletePageSetup` takes one out (a layout set to it keeps its settings and is set to none). New Page Setup starts with a list
box of `<None>`, `<Default output device>`, `<Previous plot>` (the last plot or print of the session), the layout's own setup and
the named page setups, in place of a combo that offered every space as `*Model*`. Import Page Setups says "Source drawing name:"
with the full path on its own line, lists the page setups in columns (a tick, Name, Location: Model or Layout, from the flag 1024
a page setup carries, `CadPlotSettings.PageSetupIsModel`) and shows the details of the one chosen; `ReadPageSetup(database,
name, isModel)` reads a page setup of a drawing opened with no document. Add Page Setup has Import… at the left of its footer,
copying page setups of another drawing in and refusing a name they now take. A red frame marks a refused name.
**CHANGED.** The Plot dialog's printer box names where a printer prints: the port of a local printer (`USB001`) or the share of a
network one (`\office-srv\Office printer`) in place of "Printer", and the list says Windows · USB for a printer on a USB port, as
Windows · network for one on the network (`IPlotPrinterPort`, which `WindowsPlotPrinter` implements). Plot to file, plain, is
offered for a printer, unticked, as AutoCAD leaves it; ticked, OK says in the footer "Plot to file is not supported for this
printer" and prints nothing, writing a plot to a file through a printer's driver not being built.

**NEW.** `DxfDBDictionary.SetAt` adds a named page setup, `db.PlotSettingsDictionary.SetAt("A3 plan", new
DxfPlotSettings())`, written as AutoCAD 2021 writes a PLOTSETTINGS with its class listed in CLASSES; `DxfPlotSettings`
has a public constructor, a settable `PlotSettingsName` and `CopyFrom`, which copies every setting of other page settings,
the name with them, as AutoCAD's does (a page setup copied into the layout Model leaves it model space's). A DWG now reads
and writes the PLOTSETTINGS objects, so that named page setups survive a save as DWG: a drawing with "A3 plan" saved as DWG
reads back with its sheet, scale and plotter, and AutoCAD 2021 reads both the DXF and the DWG, its AUDIT finding 0 errors.

**NEW.** The presenter's status bar has a progress meter, as AutoCAD 2021's: opening a drawing shows "Opening plan.dwg"
with a bar from 0 to 100 % and Stop, and SAVE, SAVEAS and QSAVE show "Saving plan.dwg", the status bar drawn while the
save runs. Commands, plugins and hosts start meters of their own with `CadProgress.Start(label)` (on
`CadCommandContext.Progress`, `CadCommandSession.Progress` or `MainViewModel.Progress`), then `SetLimit` and
`MeterProgress`, or `Report` with a share; a meter with neither sweeps, a meter started inside another is shown in its
place until it stops, and a `using` block stops it when the work throws. `CadDocument.Open(path, progress)`,
`CadDocument.SaveAs(path, progress)` and `CadDrawingFile.Open(path, progress)` tell any `IProgress<double>` how far they
have got, from 0 to 1 and never back: a DWG of 100 000 lines about a hundred times in whole percent, with the same bytes
written and read and the same times as without.
**NEW.** In the presenter Ctrl+2 shows the Drawings list when it is hidden and hides it when it is shown. It is a
default key of the Shortcuts page of Global settings, under DRAWINGS, and can be given another key like any other. A
settings file saved before keeps its own table of keys, and now lists the default keys it has seen (`knownDefaultKeys`);
a default key not on the list is added once when its key is free and no key is bound to its command, so an old file
with Ctrl+1 and F3 alone has Ctrl+2 after it is read, while a Ctrl+2 cleared or given to another command stays so.

**FIXED.** In the presenter Ctrl+1 only showed Properties; as AutoCAD's does, it now hides it when it is shown and shows
it when it is hidden, so that one key runs both ways. PROPERTIES typed still shows Properties and PROPERTIESCLOSE still
hides it.
**NEW.** A polyline's grips in the presenter are AutoCAD 2021's multi-functional ones. The grip in the middle of a
segment is a bar along it, 22 by 6 pixels at the default size (along the chord of an arc segment, turning with the
view), the vertices squares 15 across; a grip under the cursor is pink, as AutoCAD's GRIPHOVER, and a clicked one red.
The cursor resting 400 milliseconds on a vertex opens a menu of Stretch Vertex, Add Vertex and Remove Vertex; on the
middle of a segment Stretch, Add Vertex and Convert to Arc (Convert to Line on an arc segment). Add Vertex puts the
vertex after the grip's, the polyline drawn with it at the cursor, and splits an arc segment into two arcs of the arc
through the new point; Convert to Arc asks for the arc's midpoint (a chord of 100 with the point 20 off its middle gets a
bulge of 0.4); Remove Vertex is grey on a polyline of two vertices. Each is one undo step, with AutoCAD's prompts, and Esc
changes nothing. New in the library: `CadGripMenus.Of`, `CadGripAction`, `CadCommandSession.StartGripAction` and
`CadGrips.AddVertex`, `RemoveVertex`, `ConvertToArc` and `ConvertToLine`; a segment middle's `CadGrip.Direction` is now
the way the segment runs. Ctrl cycling through a grip's options is not built, and the other entities' grips have no menu yet.

**NEW.** Global settings › Appearance › Cursor has a Grip size row, right below Pickbox, and the system variables
GRIPSIZE (1 to 255, 5 by default; a plain grip is a square 2 × size + 5 pixels across, 15 at 5 and 25 at 10) and
GRIPMULTIFUNCTIONAL (0 to 3, 3 by default; bit 2 turns the hover menu on) are kept in the settings file
(`"gripSize"`, `"gripMultifunctional"`). The grips of tables, MTEXT and dynamic blocks scale with the size, so at the
default they are about twice as large as the fixed 8 pixels they had.

**CHANGED.** The triangle grips of an MTEXT (width, height, corner, column grips) and of a table (width, height, size) now
scale from GRIPSIZE like the squares: 15 pixels across at the default size of 5, 25 at 10, where they were a fixed 10 and
12; the table's breaking triangle and the autofill diamond of a selected cell grow with the size too, and are as large as
before at the default. Under the cursor these triangles, a dynamic block's custom grips and a table cell's grips are
AutoCAD's pink (`#FF7F7F`, GRIPHOVER) as the squares are, a clicked grip the hot red; they were hot red as soon as the
cursor touched them.
**CHANGED.** Stretch Vertex from a polyline vertex's hover menu says `** STRETCH **`, as a click on the grip does, where
it said `** STRETCH VERTEX **`: AutoCAD 2021's table of the polyline's grip modes gives Stretch Vertex that line.
**NEW.** Add Vertex of a polyline's hover menu offers Undo: `Specify new vertex point or [Base point/Copy/Undo/eXit]:`.
With Copy on, Undo takes the last vertex made back while the command runs, the arc segment it split given back with its
bulge and widths, and the next vertex is asked after the one before; the whole run stays one undo step. Remove Vertex
beside an arc was checked against AutoCAD's own wording ("replace neighboring segments with line") and already left one
straight segment, with a test now for an arc either side and for the closing arc of a closed polyline.

**NEW.** In a layout of the presenter, every viewport whose grid is on draws it in its frame, whether it is worked
through or not, on the paper and through another viewport, as AutoCAD 2021 keeps a grid by viewport. Only the grid of
the viewport worked through was drawn.

**FIXED.** Through a viewport the presenter's grid is model space's, drawn in dots by GRIDSTYLE 1 (2D model space)
rather than by 4 (layouts), and a twisted viewport keeps it within LIMMIN and LIMMAX as they are turned on the sheet:
through a viewport at 1:500 twisted by 30 degrees, with limits 2 000 by 2 000 about the middle of its frame, the line
900 above the middle is drawn, where it was cut off by the level box of two corners of the limits.

**FIXED.** SNAPMODE 2 in the presenter is kept by each space, as AutoCAD 2021 keeps it by viewport: set in model space,
it reads 2 there again after SNAPMODE 1 is set on the paper of Layout1. It was one value for the window, and 1 set
anywhere cleared it everywhere.

**FIXED.** U and REDO in the presenter show the snap and grid the drawing holds again, of model space and of each
viewport: after a line drawn and GRID 50 typed through a viewport, U shows that viewport's grid off and of the spacing it
had, as the drawing read back holds it. SNAP, GRID and their spacings stayed as they were before the undo.
**NEW.** The canvas's Edit menu takes in AutoCAD 2021's object menus for the objects selected, as AutoCAD merges them:
those of its menu file by kind, the one for a single object or the one for several of a kind, none for objects of two
kinds. One text gives Edit..., one polyline Polyline (Close, Open, Join, Reverse, Width, Curve Fit, Spline Fit and
Decurve, each PEDIT run on it), dimensions Dimension Style and Precision (0 to 0.000000, set as Properties sets it), one
table AutoCAD's table menu (Size Columns Equally: a table of columns 100, 40 and 40 wide gets three of 60, one undo
step; Size Rows Equally, Remove All Property Overrides, Table Indicator Color...), viewports Display Viewport Objects,
Display Locked and Remove Viewport Overrides for All Layers, and hatches, block references, xrefs and images theirs; what
the presenter does not build (Annotative Object Scale, Table Style, Export..., the spline menu) is grey, "Not built
yet.". The Command menu offers Recent Input, the points or numbers typed at prompts of the kind asked, newest first.

**NEW.** PAN and ZOOM run inside a command, typed as 'PAN and 'ZOOM or chosen from the Command menu, whose Pan and Zoom
are no longer grey: their prompts after ">>", then "Resuming LINE command." and the command's prompt again, the objects
"Select objects:" gathered kept, as AutoCAD 2021 runs them (probed in its console); one nests in the other. Another
command typed with an apostrophe gets "** That command may not be invoked transparently **".
`CadCommandSession.IsTransparent` and `CadCommandSession.RecentInput` are new.

**NEW.** SHORTCUTMENU, AutoCAD's bits 0 to 31, 11 by default, kept in the settings file as
`GlobalSettings.ShortcutMenu`: 1 the Default menu, 2 the Edit menu, 4 the Command menu at every prompt, 8 where the
prompt offers options, 16 a right click held a quarter of a second for a menu and a shorter one Enter. The canvas and
`MainViewModel.CanvasMenu` honour it; `CadCanvas.ShortcutMenu` and `CadPicking.WantsCommandMenu(session, shortcutMenu)`
are new.

**FIXED.** A point typed at "Select objects:" is written after the prompt, "Select objects: 50,0" then "1 found"; one on
nothing gives "0 found" and asks again, as AutoCAD 2021 does, where a script stopped at it. Point Filters of the
Shift+right-click menu draws AutoCAD's line after .Z.
**NEW.** `DwgReader` reads REGION, 3DSOLID and BODY of AutoCAD 2004 to 2010, whose ACIS data the file keeps as SAB: the
SAB is turned into the SAT AutoCAD writes of it in the DXF of those versions, line for line, its head (`20800 93 2 0`
and `16 Autodesk AutoCAD 20 ASM 226.0.0.65535 NT 0`), each flag as AutoCAD's word for it and each real to 19
significant digits as `%.19g` writes it (25.4 is `25.39999999999999858`). Twenty solids and regions of AutoCAD 2021, a
box, a loft, a sweep, booleans and regions of splines among them, come out as AutoCAD's DXF of them in each of the
three versions. A record or a subtype of spline the converter does not know leaves its entity out, as before, counted
in the Debug line of the types not read.

**NEW.** `DwgReader` reads SHAPE. `DxfReadOptions.SupportFolders` lists the folders a DWG's files of shapes are looked
for in, and `DwgReader.Open(string, DxfReadOptions)` takes them as `Read` does: with the file of its style found, a
SHAPE gives its name (group 2), else its number (group 70), as AutoCAD writes a shape whose file it does not find.
`DwgWriter` writes a SHAPE that gives its number, under 70 or as a number in group 2, of the first text style of a file
of shapes; one given by its name alone is still left out.

**FIXED.** A line of SAT longer than 255 characters, read from a DWG of AutoCAD 2000, comes out as AutoCAD writes it: in
pieces of 255 under group 3, the last piece under group 1, where it came as group 1 then 3. A line that goes on a
record starts with a space where the file has a tab, as AutoCAD writes it.

**NEW.** `DwgReader` reads the detail and section view styles every drawing of AutoCAD 2013 and later holds, in every
version AutoCAD saves them, and the manager of the persistent names of subentities AutoCAD keeps beside solids, each as
AutoCAD's DXF of it gives: a section view style of its own, its identifier 0.33 high, bend lines overshooting by 0.11 and
a hatch of ANSI37 60 % seen through at three angles, comes out as AutoCAD wrote it in AutoCAD 2000 to 2018. The Debug
line of the types not read no longer names them.

**NEW.** `DwgWriter` writes the detail and section view styles and the manager of subentities, so a new drawing of
AutoCAD 2018 is written whole, with nothing left out; and REGION, 3DSOLID and BODY with their ACIS data, where the
section ACDSDATA of the DXF holds it as SAB (a DXF of AutoCAD 2013 and later): the data store of the file is laid out as
AutoCAD lays it out, a record past 256 KB in pages of its own. A box, a sphere, a cylinder and a region, and a plate of 400 holes of
515 873 bytes of SAB, are written in AutoCAD's own bits but 12 bytes AutoCAD leaves unset; AutoCAD opens them with
`AUDIT` 0 and its DXF of each is the DXF they were written from. A solid of SAT text, as a DXF of an older version gives
it, is still left out, and so is SHAPE.

**NEW.** The Text Editor tab's Bullets and Numbering menu and the editor's right-click Bullets and Lists work as AutoCAD
2021's: Off, Numbered, Lettered (Lowercase, Uppercase) and Bulleted label the selected paragraphs, or the caret's, one
undo step each, "Plate" and "Beam" numbered written `\pxi-3,l3,t3;1.\tPlate\P2.\tBeam`; the ribbon's menu ticks the
label every selected paragraph has, none where they differ. Start, Continue and Allow Auto Bullets and Numbering are
grey, not built, and Allow Bullets and Lists grey and ticked. `CadMTextEditor.NumberingOf` gives a paragraph's label.

**NEW.** An MTEXT in columns has AutoCAD 2021's grips, where it had its location alone: the column width at the first
column's top right, the corner and the height, or for dynamic columns of their own heights a height under each column,
and the gutter at the last column's top right, each doing what AutoCAD's MoveGripPointsAt does (probed on static and
dynamic columns held at the top left, the middle and the bottom right). Two static columns 120 wide with a gutter of 20
keep their total of 260 when the column width grip is dragged, the gutter taking the rest; the gutter grip dragged 50
out makes the gutter 70. New grip kinds `MTextColumnWidth`, `MTextHeight`, `MTextSize`, `MTextGutter` and
`MTextColumnHeight`; one undo step each, through the grip edit too.

**CHANGED.** An MTEXT's grips are drawn as AutoCAD 2021 shows them: the location a square, the width a triangle pointing
along the MTEXT, the height one pointing down and the corner one pointing into it, turned with the MTEXT, where all were
squares; the corners of an MTEXT of no columns carry the way they point in `CadGrip.Direction` and drag as before.

**NEW.** The presenter's canvas shows AutoCAD 2021's right-click menus, as its default SHORTCUTMENU 11 has them, their
items, order and access keys read from AutoCAD's own menu file: with nothing selected the Default menu (Repeat LINE,
Clipboard, Undo, Redo, Pan, Zoom, Quick Select..., Find..., Options...), with objects selected the Edit menu (the items of
the objects, such as Reset Block, then Clipboard, Erase, Move, Copy Selection, Scale, Rotate, Draw Order, Group, Deselect
All, Quick Select..., Find..., Properties), while a command offers options the Command menu (Enter, Cancel, the options,
"Close" and "Undo" at LINE's third point, and Osnap Overrides), and while a grip edit runs the Grips menu (Enter,
Stretch, Move, Rotate, Scale, Mirror, Base Point, Copy, Reference, Undo, Exit). What AutoCAD has and the presenter does
not, as Isolate, SteeringWheels and QuickCalc, is grey. `MainViewModel.CanvasMenu` gives the items; `--dialog
canvas-menu` draws the menu in screenshot mode.

**CHANGED.** A right-click while a command runs is Enter only where the prompt offers no options, as at "Specify first
point:" and "Select objects:"; where it offers some, as LINE's "Specify next point or [Undo]:", it shows the Command menu,
whose first item is Enter, as AutoCAD 2021's does by default.

**NEW.** The implied window works inside a command's "Select objects:" as AutoCAD 2021's does: a click on nothing asks
"Specify opposite corner or [Fence/WPolygon/CPolygon]:" after "Select objects:" on the same line, the next click or a
corner typed ends the box, F, WP or CP turns it into a fence or a polygon from the corner, and what it takes is gathered
for the command ("2 found"), or taken out at "Remove objects:"; Esc cancels the command. `CadCommandSession.StartWindow`
starts it there too, where it returned false.

**CHANGED.** The Shift+right-click menu names Apparent Intersect and Insert as AutoCAD 2021's menu does, where it said
Apparent Intersection and Insertion, and parts its snaps into AutoCAD's three groups by two lines, after Extension and
after Tangent. The OSNAP menu of the status bar and Drafting Settings keep the longer names, as AutoCAD's do.

**FIXED.** A member given a shape or a number that is not finite answers as the shapes of NaN do, or refuses the
argument by its own name through an `ArgumentException`; it no longer throws from inside, naming a radius, a box size, a
control point or a normal of its own working, nor runs on for ever. `Chamfer` of a polygon or a chain with a vertex of
NaN ran on for ever (24 members of the fuzz), and names `polygon` or `polyline` now. `Translate` and `TransformBy` of a
polygon, a face or a body in space, a spline, an edge or a loop of arcs refuse a vector or a transformation that is not
finite, as their constructors refuse such a point, where the square 100 across moved by (NaN, 0, 0) came back with
vertices of NaN that every query then threw for. A box with a corner of NaN is the box about a centre of NaN to `ToObb`,
and meets nothing: the box from (NaN, 30, -10) to (80, 70, 20) gives no points with an arc and is NaN from a segment,
where 273 members threw for a box size of NaN. `TrySplitAt` of an arc, `ConvexHull2.TryOf`, `TrySplitAtParameter` of an
edge, `TryExpand` of a box, `TryGetIntersectionLine` of planes, `TryToPoint2` and `TrySplitBy` by a segment of NaN answer
false, where they answered true with values of NaN or threw; a spline meets no arc, circle or ellipse about a centre of
NaN; a triangle with a corner of NaN meets no body and is NaN from a polygon or a face. Factories and constructors name
the argument: `GeoRay2(origin, through)` names `through`, `GeoPlane3.FromThreePoints` the point, `GeoArc3` its
`endAngle`, `GeoSolid3.Extrude`, `Revolve`, `Sweep` and `Pipe` the profile, the placement or the path, and the forms of
`Chamfer`, `Extend` and `Offset` taking one distance name `distance`. Of 9 950 public members fuzzed with one argument
NaN or infinite, 1 700 threw from inside, hung or answered a `Try` with NaN before, and 33 do now, most of them a
member of a shape of NaN with no argument of its own to name, all throwing and none hanging; every finite call of the
fuzz gives the same bits as before.

**CHANGED.** The last 33 of those members throw what they mean. A member of a shape with a coordinate that is not a
finite number that has to make something of the shape itself throws an `InvalidOperationException`, "The shape has a
coordinate that is not a finite number.", where it threw an `ArgumentException` naming vertices said collinear, a normal
of no length or a distance of its own working: `ToPolygon` of a circle about a centre of NaN, of a chain or a triangle in
space with a corner of NaN, `ToFace3` of such a triangle, `ToSolid` and `TriangulateSurface` of a ball, `Extend` and
`ExtendToLength` of an edge that is an arc, `GetPlaneWith` of a segment, `OffsetInPlane` of a curved chain, `ClipTo` of a
line without ends, and `GetShortestLineTo` a loop of arcs from an arc, a circle, a chain or a curved chain. A writer
handed such a ball still names `sphere`. `ClipTo` names `rect` for a rectangle of NaN, where it named a distance `from`;
`GeoPolygonArc3(frame, loop)` names `frame` or `loop`, where it said its vertices were out of one plane; and
`GeoPolylineArc3` names `vertices` for an edge that bulges from a vertex of NaN, where it named `startPoint`. The
documentation of `GetPlaneWith` gives the `ArgumentException` it has always thrown for a segment of no length or a
direction along it, not an `InvalidOperationException`. The guide now says the part of a shape that is not finite meets
nothing, not the whole shape: the square 100 across with one corner moved to (NaN, 0) still crosses the segment from
(50, 50) to (150, 50) at (100, 50). The fuzz finds none of the 33 left; its 9 950 finite calls give the same bits as
before, under .NET Framework 4.8 and .NET 10.

**NEW.** The presenter's implied window asks on the command line as AutoCAD 2021's does: a click on nothing at the
Command prompt shows "Specify opposite corner or [Fence/WPolygon/CPolygon]:", and DYN shows it with two fields, the X and
the Y of the cursor itself, where it showed nothing. The next click, or a corner typed (110,60), ends the box; F, WP or
CP turns it into a fence or a polygon from the first corner until Enter; Esc writes "Command: Specify opposite corner or
[Fence/WPolygon/CPolygon]: *Cancel*". Its corners take no object snap. `CadCommandSession.StartWindow`,
`EndWindow`, `IsWindowing` and `IsWindowOpen` drive it.

**NEW.** The presenter's cursor carries AutoCAD's badges: while a box is dragged a square 9 pixels across, 12 pixels
right of the cursor and 21 above, filled blue for a window and dashed round a green middle for a crossing; while SCALE
runs, a square with a smaller one filled in its lower left corner.

**CHANGED.** No point prompt of the presenter marks its base point with a green square any more, as none of AutoCAD
2021's does: SCALE's factor shows the amber rubber band alone from it.

**CHANGED.** The Shift+right-click menu takes the access keys of AutoCAD 2021's own menu: t for Mid Between 2 Points and
for Point Filters, g for Tangent, and none for Geometric Center, where it had 2, T and G. Reset Block of the canvas's
right-click menu takes R.

**CHANGED.** The AutoComplete list shows five rows at a time, at the command line and under the box of dynamic input,
the rest scrolled to; the row the arrows choose is scrolled into sight in both, where the one at the cursor did not
follow it.

**FIXED.** The presenter measures the ink of SHX letters as AutoCAD 2021 does, for an MTEXT's lines and a dimension's
words: an arc of less than a half circle reaches the rectangle of its chord and of the line along it touching the arc, a
longer one the square about its circle, so that isocp.shx's 3 is 0.441421 of the height wide where its strokes are 0.4
(31 of the 94 letters of isocp, isocp2, isocp3, isoct, isoct2 and isoct3 were measured narrower); where the pen is put
down counts though it moves no further, monotxt.shx's 1 half the height wide, not a third; and ink left of the pen is not
counted, as in a TrueType font, italic.shx's f 0.809524 wide, not 0.857143, at any oblique angle. Words whose ink has
no width, as "I" of isocp.shx, are measured nought wide, where the picture took 0.7 of their height a letter. Of 1 422
words in 13 SHX fonts measured in AutoCAD, 17 differ now, all words of spaces alone, a hard space or letters the font
lacks, where 308 did.

**NEW.** DIMCONTINUEMODE, kept by the session and 1 as it starts: set to 0, DIMCONTINUE and DIMBASELINE draw their rows
in the current dimension style, overrides and DIMSCALE on the current layer, whatever DIMLAYER or DIM's LAyer names, as
AutoCAD 2021 does; DIMCONTINUE still writes `279 1`.

**FIXED.** A radius and a diameter the presenter makes record their words (group 11) as AutoCAD 2021 does: under DIMTMOVE
1 by the point they were put at, "R20" of Arial put 40 out at 30 degrees in acad.dwt's Standard writing (34.641, 20)
where its middle is (35.440, 20); under DIMTMOVE 0 by the point of the line or landing they stand by, without DIMTAD's
lift. Words put off the line under DIMTMOVE 2 stand level where DIMTIH says so, whatever DIMTOH says, the line broken
about their box; words over the rim, from inside or out, put the arrowheads outside with the centre marked where DIMTOFL
is off, as over a radius's centre. Words kept level outside on a line within 15 degrees of level stand with no landing, a
gap past its end, on a radius and on a linear dimension alike, as AutoCAD 2021 draws them from 5 to 15 degrees: of 1 152
aligned dimensions under DIMTMOVE 0 and DIMTOH, 24 are placed otherwise now, at exactly 15 or 90 degrees off level, where
AutoCAD's own rounding decides, where 104 were; 1 584 radius and diameter dimensions under DIMTMOVE, DIMTOH, DIMTIH and
DIMTAD match AutoCAD's group 11, words and lines.

**CHANGED.** The presenter keeps the layout of each MTEXT from one drawing to the next, REGEN too, and lays the words out
again only where the words, sizes, spacing, columns, the style's width and slant or the faces of the fonts have changed;
the picture is the same to the bit. 10 000 MTEXTs are drawn again after a REGEN in 128 ms under .NET Framework 4.8,
where they took 196 ms; a document drawing them for the first time takes 233 ms, 30 ms more than before, for the
layouts it keeps.

**FIXED.** The presenter drew a TEXT's `%%U`, `%%O` and `%%K` as written. In an SHX font they now draw the lines AutoCAD
2021 draws, under, over and through the letters after them, 0.2 of the height below the baseline, 1.2 heights and half
a height above it, along the letters' pens but 0.15 of the height left of them, and a justified text is boxed with its
lines: "%%uAB" of simplex 10 high centred on nought starts at -8.738, not -8.571. In a TrueType font the codes are left
out; its lines are not drawn yet.

**FIXED.** EXPLODE of an MTEXT gave every TEXT the MTEXT's colour and style. A run of its own colour keeps it (`\C1;`
red, `\C256;` ByLayer, `\C0;` ByBlock, `\c16711680;` the true colour 0,0,255), and a run of an SHX font of its own
(`\Ftxt.shx;`) is a TEXT of a style `MtXpl_txt_shx` of that font, added to the drawing, as AutoCAD 2021's Explode makes
them; a run of a TrueType font of its own stays in the MTEXT's style, as there.

**FIXED.** The MTEXT editor read a width in times (`{\W1.2x;ABC}`) as times 1 in a style of another width, and an edit
wrote it back so: in a style 0.8 wide the letters drawn 0.96 wide became 1.2 wide. They are 0.96 in the editor and are
written `\W0.9600;`. New: a `CadMTextEditor` constructor taking the style's width, and `CadMTextEditor.StyleWidth`.

**FIXED.** MTEXT's Columns option opened the editor with no columns and wrote its own columns whatever the editor did. The
editor opens with them (`CadInPlaceEdit.Frame`), and the MTEXT is written with the columns the editor ends with, No
Columns too.

**FIXED.** In the MTEXT editor, End on a line that breaks between two ideographs put the caret at the start of the next
line, its place being the first of that line. It stands at the end of the line, where Home, Up and Down go on from, and
a click past the end of such a line puts it there too (`CadMTextEditBox.CaretLine`).

**NEW.** `RibbonPanel.Launcher` and `RibbonPanel.LauncherToolTip`: the small arrow at the right of a panel's title that
opens its dialog, drawn on the title bar of the drop-down of the panel collapsed too, as AutoCAD 2021's. The Text
Editor tab's Paragraph panel opens the Paragraph dialog from either.

**NEW.** `CadMTextEditor.SetNumbering` and `CadMTextNumbering`: paragraphs numbered, lettered or bulleted as AutoCAD
2021's Bullets and Numbering writes them, "Plate", "Beam" numbered `\pxi-3,l3,t3;1.\tPlate\P2.\tBeam` and bulleted with
`{\fSymbol|b0|i0|c2|p18;·\t}`; Off takes the list away. The ribbon's menu does not offer it yet.

**NEW.** `DwgReader` reads the MATERIAL and VISUALSTYLE objects of every version, AutoCAD 2000 to 2018, into the
groups AutoCAD's own DXF of them gives: of a material what differs from a new material's values (a red of its own,
RGB 200, 30, 30 at 0.8, glossy at 0.3, gives 91, 44 and nothing of the maps it has not), of a visual style the form of
its version, from AutoCAD 2013 its 58 properties each with how it is applied. Of AutoCAD 2013 and later it reads the
data store too, which holds the ACIS data of the solids, into the section ACDSDATA AutoCAD writes: a record of SAB
bytes for each REGION, 3DSOLID and BODY, a large one (515 873 bytes for a plate of 400 holes) from the pages it is kept
in. Before, the materials, visual styles and ACIS data of the drawing were left out. `DwgWriter` writes MATERIAL and
VISUALSTYLE (of the form of AutoCAD 2013 and later): AutoCAD opens such a drawing with `AUDIT` 0 and its own DXF of it is
the one it was written from, and the 24 visual styles of a new drawing are AutoCAD's own bits.

**CHANGED.** The presenter drags objects as AutoCAD 2021 does, after the maintainer's pictures of its SCALE: while
MOVE, COPY, ROTATE, SCALE, MIRROR, ALIGN, an array or a grip edit waits for its point, the copy at the cursor is drawn in
the objects' own colours, not dashed blue, and the rubber band from the base point is dashed amber, 16 pixels on and 9
off. At MOVE's second point and at ROTATE's angle and SCALE's factor without Copy, the objects are drawn faded where they
stand, a fifth of their colour (a white line on black a grey of 51), rather than highlighted. SCALE's box at the cursor
shows the factor alone, 0.50 with the cursor 0.5 from the base point, where it showed "0.50 < 306.9°". A selection
window is blue and a crossing green, a quarter strong over the drawing, where they were a tenth; both are edged in the
crosshair's colour, the crossing dashed 4 on and 2 off. While a window, a crossing or a lasso is dragged the cursor is
the cross alone, without the pickbox, which comes back once the box ends. The window, crossing, fence or polygon an
option of "Select objects:" draws is drawn alone, without the rubber band and the base point square it had from its
first corner.

**FIXED.** The presenter's screenshot mode showed the real dialog of a command its `--run` typed, as Write Block for
`--run W`, and waited on it: the window hooked its dialogs again once it was given the view model. Such a command asks on
the command line now, as the rest of the screenshot mode does.

**NEW.** `CadMenuItemViewModel.AccessKey`: the Shift+right-click menu takes AutoCAD's access keys while it is open, F for
From, E for Endpoint, r for Nearest and the rest, the letter underlined. `CadPreview.FadesOriginals` says whether a preview
draws the objects it moves faded where they stand.

**NEW.** The MTEXT editor's ruler works as AutoCAD 2021's: the tab type box (Left, Centre, Right, Decimal), a click on
the ruler adding a tab stop at the nearest minor tick, stops dragged along it or off it, the first-line and hanging
indents, the width arrows, and with columns a column's width, the gutter, the total and the height grips; each release
one step of the editor's undo, Esc during a drag putting it back; a right-click gives Set Mtext Width and Set Mtext
Height. The Paragraph dialog, the Column Settings dialog, the Paragraph panel's launcher and the Columns menu are built,
and the paragraph codes are written as AutoCAD's own editor writes them: in an MTEXT 2.5 high a centre stop at 20 is
`\pxtc8;`, a decimal one on a comma at 40 `\pxtD,16;`, a first line at 2 with the rest at 10 `\pxi-3.2,l4;`. New in
`GeometryHelper.Dxf.Viewer`: `CadMTextParagraphFormat`, `CadMTextTabStop`, `CadMTextFrame`, and on `CadMTextEditor`
`FormatOf`, `SetFormat`, `Frame`, `SetFrame`, `BeginChange`, `EndChange` and `InsertColumnBreak`.

**CHANGED.** Line Spacing on the Text Editor tab sets the selected paragraphs' own multiple (`\pxsm1.5;`), as AutoCAD
2021's does, where it set the MTEXT's line spacing factor; More… and Clear Paragraph Spacing are added.

**FIXED.** A decimal tab stop on a comma (`\pxtD,16;`) was read as a left stop at 16 heights; the word after the tab is
set on its comma now, as AutoCAD 2021 sets it, and one with no comma by its end. An MTEXT edited in the MTEXT editor
wrote its column breaks (`\N`) back as paragraph breaks; they are kept.

**FIXED.** `DistanceTo` of a `GeoTriangle3` and a point measured a triangle nearly on one line by a foot along a normal
that is the rounding of its cross product: the point 5E-14 from two corners 1E-13 apart, of the triangle (35, 54, -67),
(88, 96, -11), (88, 96.0000000000001, -11), was measured 2.12 away. Such a triangle, no higher than about 3E-8 of its
length, is measured to its sides now, within its height. `GetClosestPointOnBoundary` and `GetShortestLineTo` of a point
check the point the Voronoi regions find on a long thin triangle against that gap and replace it where it is further
off than the triangle is high: from a point 0.00004 from a triangle 9 000 long and 0.0024 high the point given was 2.08
away. Of 400 000 triangles and points of a fuzz, every distance of a triangle with an area at a thousandth is the same
bit for bit, and 3 174 closest points changed, each one 1E-8 to 26 too far before and none further off now.

**FIXED.** `Fillet` and `TryFilletAt` of `Corner3` took a radius of infinity, alone or in a list; they refuse it with
an `ArgumentOutOfRangeException`, as the corners in the plane do.

**FIXED.** A shape with a coordinate that is not a finite number meets nothing, in the plane and in space:
`GetIntersections` gives no points and the `Try` members answer false, where 352 members gave points of NaN to a fuzz
that gave one shape a NaN or an infinite coordinate. The segment from (NaN, 20) to (150, 30) crosses no side of a
triangle, a polygon, a face or a rectangle; a segment, a ray, a line, a chain or an edge of NaN in space meets no plane,
triangle, polygon, face or segment, nor does a plane of NaN; an arc, a circle or an ellipse about a centre of NaN meets
no flat shape or plane; and `TryOffset` of an arc, a circle or a rectangle about such a centre, or of an arc by a
distance of NaN, `TrySplitAtDistance`, `TryChamferAt`, `TrySplitBy` of a curved chain, `TryExtendTo` and
`TryTrimExtendToCorner` in space and `TryGetBarycentric` of a point of NaN answer false. A spline in the plane and a chain
or a triangle with a corner of NaN or at infinity meet nowhere, where under .NET Framework 4.8 the search never ended
(24 members of the fuzz hung). Every finite answer is the same bit for bit.

**FIXED.** A dynamic block saved from the presenter's Block Editor (BSAVE, or BCLOSE with Save) has the anonymous *U
blocks its INSERTs place worked out anew from the new definition, at each INSERT's values, as AutoCAD 2021's
`UpdateAnonymousBlocks` works them: GH_PANEL, whose INSERTs of Width 150 and 200 placed *U1 and *U2, given a circle of
radius 10 at (50, 25) and saved, has them place new blocks *U3 and *U4, stretched to 150 and 200 with the circle, the
INSERT that shared *U1 sharing *U3, each INSERT keeping its values, and *U1 and *U2 left placed by none, as AutoCAD leaves
them. One undo takes it back with the save. The INSERTs kept showing the block as it was before the save.

**NEW.** LIST writes the overrides of a dimension's style as AutoCAD 2021 does, for every type of dimension: "dimension
style overrides:" after the style, then each variable by name in alphabetical order (" DIMASZ      5.0000",
" DIMCLRD     1 (red)", " DIMBLK      Dot", " DIMJOGANG   30", " DIMSE1      On"), distances in the drawing's units with
nought feet and nought inches after whole feet left out (5", 1', 1'-3 1/2"), DIMSCALE and the other factors to LUPREC
places, DIMJOGANG in the drawing's angle units, arrowheads by AutoCAD's names and DIMFXLEN as DIMFXL.

**FIXED.** A dimension's override of DIMJOGANG is read and written in radians, as AutoCAD 2021 keeps it in the list
DSTYLE: 30 degrees is `1040 0.5235987755982988`. The library read and wrote degrees there, so a dimension AutoCAD gave a
jog angle of 30 degrees read as 0.0091 radians, and one the library or DIMJOGGED wrote at 45 degrees reached AutoCAD as 45
radians. A DIMSTYLE record keeps degrees, as before.

**FIXED.** ATTSYNC of a dynamic block, by Name or by Select, brings in line the INSERTs of its anonymous *U blocks too,
against the dynamic block's own definitions, each keeping its dynamic properties: an attribute an action moves stands
where the INSERT's values put it. GH_PANEL with TAG locked in a stretch of its Width, an INSERT at (1 000, 500) set to a
Width of 150, and the block then given TAG 5 high and a new definition NEW at (60, 25): the INSERT keeps its *U block and
its Width of 150, TAG at (1 145, 520) 5 high, NEW at (1 060, 525). These INSERTs were left as they were. BATTMAN's Sync
and its count of INSERTs found take them in alike.

**FIXED.** LIST writes as AutoCAD 2021 does, checked in accoreconsole: a jogged radius its type, "jogged", and its centre,
chord point, override centre and jog point, where it wrote the rows of the Properties palette; a rotated dimension in a
turned UCS its rotation and obliquing angle counted from the UCS's X axis, so that one turned 30 in a UCS turned 30 is
"horizontal" and one turned 90 "rotation: 60"; and each object's kind as AutoCAD names it, two places before "Layer:",
where a long one ran into it ("MULTILEADERLayer:"): MULTILEADER, LWPOLYLINE for a lightweight polyline, POLYLINE for a 2D
one, BLOCK REFERENCE for a MINSERT, ACAD_TABLE, 3D FACE, the record's type for the others (IMAGE, MLINE, 3DSOLID). Its Y
ordinates, its rotations of 180 and 270 degrees and "associative: yes" were already AutoCAD's, and are now pinned by tests.

**FIXED.** In a layout's paper space of the presenter, the snap and grid variables typed (GRIDUNIT, GRIDDISPLAY,
GRIDMAJOR, SNAPUNIT, SNAPMODE, SNAPBASE, SNAPANG, SNAPSTYL, SNAPISOPAIR) and SNAP and GRID set the layout's own, kept in
its viewport 1 as AutoCAD 2021 keeps them, and the snap on the paper steps by that viewport's spacing: GRIDUNIT 3,3 typed
in Layout1 writes 3 by 3 into its viewport 1 and leaves model space's 4 by 6 as it was. They changed model space's.

**NEW.** Through a viewport (MSPACE) the snap and grid of the presenter are that viewport's own, as AutoCAD 2021 keeps
them: GRID, SNAP and their variables typed there write into it, and its grid is drawn inside its frame, laid in model
space as the viewport shows it. A viewport of the room at 1:500 with a grid of 1 000 draws a line every 2 units of paper
in its frame alone. Model space's snap and grid were used, and no grid was drawn through a viewport.

**FIXED.** A point typed at a SETVAR of the presenter, as SNAPBASE 3,4 or GRIDUNIT 6,7, leaves the last point where it
was, as AutoCAD 2021 leaves LASTPOINT: after ID 10,20 and SNAPBASE 3,4, SNAPUNIT @2,3 is 12,23. It was 5,7.

**FIXED.** A linear dimension the presenter makes with its words past a leader under DIMTMOVE 1 writes their start as
group 11, level with their middle, as AutoCAD 2021 writes it, where the presenter wrote their middle: the "0.5000" of the
dimension of 0.5 DIMCONTINUE adds after one of 100 in acad.dwt's Standard, its middle at (100.8933, 20.4088), is written
as (100.52, 20.4088). Words of DIMTMOVE 0 and 2, words too near the line for a leader, and those of the other kinds keep
their middle, as in AutoCAD.

**FIXED.** DIMCONTINUE and DIMBASELINE draw at the DIMSCALE of the dimension they go on from and put the new dimensions on
its layer, as AutoCAD 2021 does under DIMCONTINUEMODE 1, where the presenter took the current DIMSCALE and layer (and DIM's
LAyer): from a dimension made on L1 at DIMSCALE 2 they go on L1 and write `1070 40, 1040 2` with L2 current at 1, a
baseline row twice DIMDLI further out each time; linear, angular and ordinate rows alike.

**CHANGED.** `GetIntersections` and `TryIntersectWith` of a triangle, a polygon, a face or a plane with a `GeoObb3` or a
`GeoAabb3` flat on some axis, or no thicker than the point tolerance on two, read the box as its rectangle, its segment
or its point, as `CollidesWith` and `DistanceTo` read it, where they gave no points: the plane x = 30 crosses the box
100 by 100 by 0 about (50, 50, 0) at (30, 100, 0) and (30, 0, 0), on its edges, and the box 100 by 0.000002 by 0.000002
about (50, 0, 0) at (30, 0, 0). A box with its body is crossed as before, bit for bit (2.38 million calls of a fuzz
under .NET Framework 4.8 and .NET 10, old against new); an empty box is still crossed nowhere.

**FIXED.** `TryUnion`, `TryIntersect` and `TrySubtract` of a `GeoObb3`, and of a `GeoSolid3` with a box, answer false
with no result for a box no thicker than the point tolerance on two axes, as for one flat on some axis, where they ran on
its sliver body: the box 100 by 0.000002 by 0.000002 joined to a cube 40 on a side it runs through was given the cube
alone, its two ends lost.

**FIXED.** `GetIntersections` of a ray and a box flat on some axis no longer throws "A box with a zero size on some axis
has no faces to build" for a ray starting within the tolerance of a corner and running away from it: it touches the box
at its origin, as one beside a box with its body does (456 throws in 200 000 rays of a fuzz, none now).

**FIXED.** `Locate` and `Contains` of a `GeoObb3` or a `GeoAabb3` put a point that is not a number outside the box, as
a body's `Locate` does; it was inside.

**CHANGED.** A hook of the presenter's LEADER and QLEADER has a vertex of its own wherever AutoCAD 2021 writes one: on a
leader to a frame, new or copied, and on any leader made in a UCS other than the world's, DIMASZ short of the end along
the UCS's X, where these leaders kept the two points picked. `{\Fgdt;j}%%v0.1` copied to a leader from (200, 40) to
(210, 45) at DIMASZ 0.18 gives (200, 40), (209.82, 45), (210, 45), as AutoCAD writes it. A frame on a leader to the left
in a turned UCS is set back its width along the world's X and the leader's end moved to its right side, as AutoCAD does.

**FIXED.** QLEADER writes how far it raised its words from the leader's end as group 213, as AutoCAD 2021 writes it in
any UCS: (0, -0.09, 0) for Middle of top line at DIMTXT 0.18, (0, 0.09, 0) for Middle of bottom line, and their width
along X as well where Always left justify sets them back on the left. It wrote none.

**FIXED.** The snaps Nearest and Perpendicular take the sides of a frame's rows alone, not its walls or symbols, as
AutoCAD 2021 gives them. TEXTEDIT reads a frame's row written by hand that leaves a cell out by what its cells hold:
`{\Fgdt;j}%%v0.1%%vA%%vB` opens as the position, 0.1, to A and B, where A was read as a second tolerance, and `0.1%%vA`
keeps its 0.1 as the first tolerance, where it was lost.

**FIXED.** ATTSYNC keeps the extended data and the extension dictionary of an attribute it makes anew because its tag
stands out of order, as it keeps those of one synced in place: AutoCAD 2021's `SetAttributeFromBlock` kept both, the
dictionary's handle too. An INSERT of GB with GRID before LEVEL, GRID carrying data of an application and an XRECORD,
synced to a block that defines LEVEL first, keeps both on the new GRID; they were lost. ATTSYNC by Select on an INSERT of
a dynamic block asks "ATTSYNC block GH_PANEL?", the dynamic block AutoCAD's `DynamicBlockTableRecord` names, where it
named the anonymous block the INSERT places, "*U1".

**CHANGED.** -ARRAY's corner prompt of a unit cell draws the cell alone, a rectangle from the first corner to the cursor,
as AutoCAD 2021's does; it drew every copy the cell would make.

**NEW.** With DYN on, an angle override typed at a point prompt shows in the angle field locked, as AutoCAD 2021's: `<30`
at LINE's next point shows 30 wherever the cursor is until the point is given.

**FIXED.** LIST writes an ellipse and a dimension as AutoCAD 2021 does, line for line, checked in accoreconsole under four
pairs of ANGBASE and ANGDIR. An elliptical arc from 30 to 120 degrees reads "Start Angle: 60" and "End Angle: 330" with
ANGBASE 90 and ANGDIR 1, after its length, centre, axes and ends; a whole ellipse its area and circumference. A dimension
is a DIMENSION, then whether it is associative, its type (horizontal, vertical, rotated, aligned, angular, arc, radial,
diameter, X or Y ordinate), its defining points, its obliquing and text angles counted from ANGBASE (30 reads 60), the
rotation of a rotated one as an amount turned by ANGDIR alone (30 reads 330 clockwise), its text position and text, its
style and whether it is annotative. Both wrote the rows of the Properties palette, and a dimension's kind ran into its
layer, "ROTATED DIMENSIONLayer".

**FIXED.** `CollidesWith`, `DistanceTo` and `GetIntersections` of a triangle, a polygon, a face, a plane, a solid or
another box against a box no thicker than the point tolerance on two axes, `GeoObb3` or `GeoAabb3`, read its body of
four slivers and two ends below the tolerance and answered wrongly: the box 100 by 0.000002 by 0.000002 was +Infinity
from a cube 40 on a side standing 20 over it, and missed a solid it ran through. Such a box is now read as a box flat on
those axes is, the segment along the third, and on all three its centre: it is 20 from the cube and touches the solid,
and `GetIntersections` of a triangle, a polygon, a face or a plane with it gives no points, as with the flat box, where
the body gave one to four. A side no longer than the point tolerance is thin, as `IsDegenerate()` reads it: a box 100
by 0.001 by 0.001 is a segment at the default tolerance and one 100 by 0.0015 by 0.0015 a body. A box thinner than the
tolerance on one axis alone, the plate 100 by 100 by 0.000002, keeps its body, and a box with at least two sides longer
than the tolerance answers bit for bit as before. Over 79 methods taking a box, 259 of 4 740 answers for such boxes
disagreed with the box flat on the same axes, and 3 do now, boxes 100 000 from the origin whose sides round a hair over
0.001.

**FIXED.** The presenter's linear dimensions lay out words the fit moves out under DIMTMOVE 1 and 2 as AutoCAD 2021 does
where DIMTOH keeps them level, as acad.dwt's Standard does: DIMEXE, half DIMGAP and their height out, square off the
middle of the line, the leader straight to there and a level landing on away from the line, at any angle of it. The
dimension of 0.5 DIMCONTINUE adds after one of 100 in Standard has its leader straight up from (100.25, 20), its landing
on to x 100.43 and its arrowheads outside, as AutoCAD draws it, where the presenter ran the leader to (100.372, 20.405)
and kept the arrowheads inside; "4" of a dimension of 4 under ISO-25's sizes stands 6.5625 off its line, not 5.9375.
Arrowheads need two lengths and two DIMGAPs between the extension lines (6.25 with ISO-25's sizes, where 5.625 was
taken), and words above the line their width and a gap each side: a dimension of 6 under DIMATFIT 2 has its arrowheads
outside, as in AutoCAD.

**FIXED.** DIMCONTINUE and DIMBASELINE draw in the style of the dimension they go on from, with its overrides, as AutoCAD
2021 does under DIMCONTINUEMODE 1: from a dimension made with DIMASZ 4 over ISO-25 they write `41` again, and from one
of another style they are of that style, where the presenter drew them in the current style. DIMBASELINE writes no
DIMTMOVE of its own, nor do rows of ordinate dimensions and QDIM, as AutoCAD 2021 writes them; a baseline dimension
based on a continued one takes its `279 1` with the rest.

**NEW.** `DxfTolerance.GeometricExtents` is the box of the frame's rows as AutoCAD 2021 gives it, where the drawing's
`FontSearchPath` measures its words: each row twice the text height high, each cell its ink and the text height wide,
`{\Fgdt;j}%%v0.1` at (0, 0, 0) in a style of Arial 0.18 high from (0, -0.18, 0) to (0.92296, 0.18, 0), as AutoCAD
gives it to the millionth; a frame of symbols alone needs no font. It had no extents before.

**FIXED.** A cell of a feature control frame mixing gdt symbols and words was drawn up to 0.0043 too wide at DIMTXT
0.18: the diameter sign has 1.5/21 of the text height either side, not 2/21, and the circular and total runouts 2.25/21
on their right, and words inked past their pen (Arial's A and f) set what follows on from their ink, as AutoCAD 2021
lays them out. `{\Fgdt;j}%%v{\Fgdt;n}0.1{\Fgdt;m}%%vA%%vB%%vC` in Arial is 2.462701 wide, as in AutoCAD.

**NEW.** TOLERANCE, and the frames of LEADER and QLEADER, write each current dimension variable other than the style's
as an override, as dimensions and leaders write them: DIMTXT 5 typed over ISO-25 gives `1070 140, 1040 5` and rows 10
high, where the frame was drawn by its style alone. TEXTEDIT (DDEDIT) and a double click on a frame open the Geometric
Tolerance dialog filled from its words, and OK writes them, one undo step. Properties shows a frame's Dim style, Dim
line color, Text color and Text height and writes them as overrides. The snaps End and Mid take the rectangles of a
frame's rows alone, not its walls or symbols, and Insertion its point; EXPLODE says `Cannot explode TOLERANCE`, as
AutoCAD 2021 does.

**FIXED.** A frame on a leader to the left is set back the width of its widest row, not its first, and the leader writes
that width and the height of all the rows as groups 40 and 41: a frame of rows 0.553 and 1.271 wide stands 1.271 left of
the end and the leader holds 0.72 high, as AutoCAD 2021 writes it. QLEADER's angle constraints in a turned UCS read the
way the point was picked along the UCS and lay it off along the world, as AutoCAD 2021 does, and a leader to words, a
frame or a block made in a UCS writes the UCS's X as group 211, (0.866, 0.5, 0) in one turned 30 degrees, where it wrote
the world's.

**FIXED.** A new `DxfDatabase` holds the dimension style its header names: ISO-25, current and equal to the header in
every variable, beside Standard, as AutoCAD 2021's acadiso.dwt saved as each version from R2000 to R2018 has them, where
the header named ISO-25 and the drawing held Standard alone, off the header in 20 variables (arrowheads 0.18 long against
the header's 2.5). The 20 are written into ISO-25 and the header as AutoCAD writes them, so that AutoCAD reads the same
values. `new DxfDatabase("AC1015")` is the text of `new DxfWriter().ToString()` but for its code page and ISO-25.

**CHANGED.** The presenter's new dimensions are drawn by the header in every drawing, its DIMSTYLE naming a style of it or
not: where it names none, they are drawn in the style fallen back on (Standard, else the first) and write each variable
other than that style's, and leaders are compared with the same style, where dimensions were drawn by the style alone and
leaders compared with AutoCAD's own values. As AutoCAD 2021, DIMCONTINUE draws and writes DIMTMOVE 1 where it is nought
(`279` first, of linear and angular dimensions), DIMRADIUS and DIMDIAMETER write DIMUPT 1 (`288`) where the style has it
off, and DIMJOGGED writes DIMJOGANG (`50`) whatever the style has.

**CHANGED.** The presenter's new dimensions are drawn by the drawing's current dimension variables and write each that
differs from their style as an override, in the order of their groups, as AutoCAD 2021 does for every kind (linear,
continued, baseline, aligned, angular, radius, diameter, jogged, arc length, ordinate): a drawing saved with DIMASZ 5,
DIMEXO 1, DIMTAD 0 and DIMSCALE 2 over ISO-25 draws arrowheads 10 long and writes `40 41 42 77` in that order, where
the presenter drew by the style and wrote DIMTXT and DIMSCALE alone, DIMTXT first.

**FIXED.** A dimension made with DIMSCALE 0 through a viewport is drawn at one over the viewport's scale and writes it, as
AutoCAD 2021 does: 2 through a viewport of 1:2, words 2.5 high drawn 5 high and `1070 40, 1040 2` written, where the
presenter drew at 1 and wrote DIMSCALE 0. In model space DIMSCALE 0 draws at 1.

**NEW.** The arrowhead lists of the Dimension Style dialog (First, Second, Leader) and the Symbol list of the Modify
Multileader Style dialog show each arrowhead with its picture, as AutoCAD 2021's lists and Leader Settings show them, a
user arrow drawn from its block.

**FIXED.** More of the presenter's angles follow ANGBASE and ANGDIR as AutoCAD 2021's do, each checked in accoreconsole
with ANGBASE 90 and ANGDIR 1. DIMEDIT's obliquing angle is a direction: 30 slants the extension lines of a level
dimension 60 degrees from the X axis, where it slanted them 30. The Properties palette writes and takes the text rotation
of a dimension and the start and end angles of an ellipse counted so: an elliptical arc from 30 to 120 degrees reads 60
and 330, as AutoCAD's LIST writes it. MEASUREGEOM's Angle writes 30 degrees between two lines as 60, as AutoCAD writes it.
The obliquing angle of -STYLE stays as typed, as AutoCAD keeps it.

**NEW.** The angle override of AutoCAD 2021: `<30` typed at a prompt for a point that has a base point writes "Angle
Override: 30", asks the point again and holds it on the line at 30 degrees from the base point, counted as the angle of
`@100<30` is, `<<30` from east counter-clockwise. A distance typed then runs along it, and the window holds the cursor on
it over the grid, ortho and polar; a point typed and an object snap still win, as AutoCAD's help has it. From (0, 200),
`<30` and 100 end the line at (86.6, 250). New: `CadCommandSession.AngleOverride` and `ObjectSnap.AngleOverride`.

**FIXED.** ATTSYNC by Select refuses a block with no attribute definitions before asking whether to sync it, saying "This
block has no attribute definitions." and asking for another block, as Autodesk's help describes; it asked "ATTSYNC block
PLAIN? [Yes/No]" first and then ended. -ARRAY reads a distance between rows typed in the drawing's units, so `2'` in an
architectural drawing puts the rows 24 apart; it took plain numbers only, as the distance between columns did not.

**FIXED.** A disabled button of a presenter dialog is dimmed once: its words were muted and then drawn at 45 per cent as
well, about a quarter of their strength. They are now drawn at 45 per cent, as every flat button of the theme.

**CHANGED.** The paste preview draws the Clipboard's objects once and moves them with the cursor, a text drawn anew: a
Clipboard of 24 000 items, a tenth of them texts, previews in 16 to 22 ms a move where it took 34, and one of 2 400 in
under 1 ms where it took 2.7. What is drawn is the same.

**NEW.** `DxfTolerance`, AutoCAD's `FeatureControlFrame`: a TOLERANCE read from DXF and DWG as a class of its own rather
than a `DxfUnknownEntity`, made in code and written as AutoCAD 2021 writes one (`Text` with its rows as `^J`,
`Location`, `Direction` and `Normal` left out where they are the world's, `DimensionStyleName`), its style's variables
read through its overrides (`GetDimstyleData`), and moved, turned, scaled and mirrored as AutoCAD 2021's `TransformBy`
moves one: mirrored, it is turned over (normal (0, 0, -1)); scaled by 3, a frame of the style's 0.18 and 0.09 holds the
overrides `1070 140, 1040 0.54, 1070 147, 1040 0.27`, DIMTXT then DIMGAP. A drawing whose code looked for
`DxfUnknownEntity` with `DxfName == "TOLERANCE"` finds a `DxfTolerance` now.

**NEW.** TOLERANCE (TOL) in the presenter, with AutoCAD 2021's Geometric Tolerance dialog and its Symbol and Material
Condition dialogs: two rows of a symbol, two tolerances and three datums, the height of a projected zone and the datum
identifier, written as AutoCAD writes group 1, then "Enter tolerance location:" with the frame following the cursor. The
canvas draws a frame as AutoCAD 2021 lays one out, measured in its console: rows twice DIMTXT high whatever DIMGAP, each
cell its ink and DIMTXT wide, a cell of nothing taking no room; `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 in Arial is 0.92296
wide, as in AutoCAD, and a cell mixing a gdt symbol and words comes within 0.005. The symbols are the presenter's own
strokes. A frame has one grip, at its insertion point, and MOVE, COPY, ROTATE, SCALE and MIRROR (readable under MIRRTEXT
0) move it, where they refused it before.

**NEW.** LEADER's Tolerance option and QLEADER's Tolerance type show the Geometric Tolerance dialog and put the frame at
the leader's end; Copy and Reuse Current copy a TOLERANCE there, where they passed it over: on a leader running left the
frame is set back its width, `{\Fgdt;j}%%v0.1` at DIMTXT 0.18 copied to a leader ending at (90, 105) standing at
(89.07704, 105), as AutoCAD 2021 puts it. Leader Settings no longer greys Tolerance.

**CHANGED.** User Arrow... of Leader Settings opens AutoCAD's Select Custom Arrow Block as a dialog of its own over Leader
Settings, rather than a panel under the Arrowhead list: an editable list of the drawing's blocks, a name not found saying
so in the dialog with its box framed orange, and in a drawing of no block the dialog open with its list and OK greyed.
`LeaderSettingsViewModel.UserArrowMessage`, `IsUserArrowNotFound`, `HasUserArrowBlocks` and `CanKeepUserArrow` drive it;
its messages are no longer in `Notice`, and `ToleranceToolTip` is gone.

**CHANGED.** Undo and redo in the presenter hold less memory and give the processor to an edit. The drawing an undo leaves
is the one a redo goes back to; where it was itself read from the bytes of its step and nothing has written into it since,
the worker saves it, sets it against those bytes and takes it as it is where they are the same, rather than reading the
step again, so undo after undo reads one drawing ahead, not two. A drawing read back no longer copies the bytes it is read
from, and its index of handles is sized at the start. On a drawing of 1 000 000 entities a run of undos and redos
allocates about 35 GB where it allocated 47, with 28 to 33 full collections where it had 41 to 46, and a redo 3 s after an
undo takes about 0.3 s under .NET Framework 4.8 where it took 1.6 to 1.9 s. The peak working set is set by the collector
and moved within its noise (4.0 to 4.8 GB either way under .NET Framework 4.8); the drawings held while none is read are
as before. Every drawing an undo or a redo gives is the one saved at that step, byte for byte. An edit stops the readings
still running and they read again after it: an ERASE of 10 000 objects of a drawing of 100 000 straight after an undo
takes about 185 ms under .NET 10 where it took 220 (110 once the readings are done).

**FIXED.** The first undo after the Block Editor saves (BSAVE, or BCLOSE saving) or discards its changes no longer reads its
drawing then: the drawing it goes to is read ahead, as after any step. On a drawing of 100 000 entities the undo of BEDIT
and finding an entity after it take about 1 ms where they took 304, and the undo after the changes are discarded about
1 ms where it took 309.

**CHANGED.** Reading an entity's groups tells its subclass markers (AcDbEntity, AcDbLine) from their bytes where they are
plain ASCII, rather than making a string of each. In a drawing of 100 000 entities written with markers, as AutoCAD writes
them, reading whether each is visible makes 43 MB where it made 51, and drawing again the third of them a layer switched
on 48 to 50 MB where it made 52 to 53, in the same time; the display lists are item for item as before.

**NEW.** A distance or an angle the presenter asks with no base point takes two points, as AutoCAD 2021's command line
does: a point typed or picked at "Enter new value for POLARDIST <0.0000>:" asks "Specify second point:", its rubber band
from the first, and 0,0 then 3,4 sets POLARDIST to 5; SNAPANG 0,0 then 1,1 sets 45. Every such prompt takes them, SETVAR
FILLETRAD, SNAP's spacing and a command's own alike. @dx,dy at the second point is measured from the first, the last
point is left as it was, FROM and the other point modifiers work at either point, the prompt's keywords are taken at the
second (T for Through at an offset distance), Enter there is the prompt's Enter, and text gets the prompt's refusal and is
asked again. Checked in AutoCAD 2021's console.

**CHANGED.** A point SETVAR asks (GRIDUNIT, SNAPUNIT, SNAPBASE, INSBASE, LIMMIN and the others) takes a number alone as a
distance from the last point towards the cursor, as AutoCAD 2021 does: with the last point at (10, 20) and the cursor at
(13, 24), GRIDUNIT 5 is 13,24, where the presenter refused it with "Invalid 2D point.". An angle asked with no base point
refuses text with AutoCAD's "Requires valid numeric angle or two points." rather than "... or second point.", and a point
picked at a distance asked with no base point is the first of two rather than refused.

**NEW.** SNAPGRIDLEGACY, as AutoCAD 2021's: 0 or 1, set by SETVAR or by SNAP Legacy, and kept in the settings file
(`GlobalSettings.SnapGridLegacy`) as AutoCAD keeps it in the registry. With 1 and SNAP on, the canvas holds the cursor on
the snap grid with no point asked too, the crosshair, the coordinates, the object under it and a click or a box corner
going from one grid point to the next (`CadCanvas.SnapGridLegacy`, `MainViewModel.SnapGridLegacy`); a click at (1 130, 70)
over a snap of 500 picks a line through (1 000, 0). SNAP Legacy was kept for the window alone before and changed nothing.

**NEW.** A layout's paper space has a grid of its own, as AutoCAD 2021's: GRID, F7 and GRIDMODE in Layout1 switch the
sheet's grid, kept in the layout's viewport 1, and leave model space's as it was, each shown again with its space. The
canvas draws it on the paper, 8 and 18 per cent darker than the paper, by the spacing, major lines and GRIDDISPLAY bits
of that viewport, within the layout's limits without GRIDDISPLAY 1, and dotted with GRIDSTYLE 4. The paper-space snap
stays model space's.

**NEW.** "Grid too dense to display" is written on the command line once as the grid comes on, or the view or the
spacing goes to where its lines would lie closer than 6 pixels without GRIDDISPLAY 2, and not again at each redraw.

**FIXED.** The Snap and Grid tab of Drafting Settings refused a Polar distance below nought that SETVAR POLARDIST takes,
so that the dialog opened after POLARDIST -1 could not be OK'd without changing it. It takes any number now, -1 kept and
PolarSnap stepping by the snap X spacing, and refuses text with "The polar distance must be a number.".

**FIXED.** The presenter's LEADER and QLEADER write every dimension variable other than the style's as the leader's
override, in the order of their groups, as AutoCAD 2021 writes them, where they wrote five (DIMSCALE, DIMASZ, DIMTAD,
DIMTXT, DIMGAP) and DIMLDRBLK: DIMEXO 0.5, DIMCLRD 1, DIMCLRT 2, the arrow `_Dot` and DIMLWD 0.30 give 42, 176, 178, 341
and 371 in that order, after QLEADER's DIMGAP and DIMTAD. The words typed take DIMCLRT, the current colour where it is
BYBLOCK.

**FIXED.** In a UCS other than the world's, LEADER and QLEADER measure the last segment along the UCS's axes, as AutoCAD
2021 does: the words run along its X, DIMGAP along it from the end, and a segment steeper than 15 degrees to that X is
hooked with no vertex of the hook's own, where the presenter measured it in the world and added a level hook. In a UCS
turned 30 degrees a leader from the UCS's (0, 0) to (10, 10) keeps its two points, (0, 0) and (3.66, 13.66) in the world,
its words 0.625 along the UCS's X from the end; QLEADER raises its words up the world's Y, as AutoCAD 2021 does.

**NEW.** LEADER's Copy and QLEADER's Copy an Object copy an INSERT picked to the leader's end, its scale kept and turned to
the UCS's X, with no hook, and QLEADER's Reuse Current copies an INSERT kept the same way: an INSERT at (500, 500) scaled
1.5 by 2.5 and turned 0.3 is copied to (10, 320) scaled 1.5 by 2.5 and turned 0, as AutoCAD 2021 copies it. A TOLERANCE
is still passed over, as the library cannot move one.

**NEW.** LEADER and QLEADER preview the leader at the cursor while its points are picked, its arrowhead and its line or
spline as the leader made there draws them; QLEADER's preview runs to the point its angle constraint holds, the cursor at
(100, 30) previewing a first segment held level to (100, 0) (`CadPreview`).

**NEW.** The Arrowhead list of QLEADER's Leader Settings shows each arrowhead with its picture
(`CadStylePreview.Arrowhead`) and ends with `User Arrow...`, which lists the drawing's blocks as AutoCAD 2021's Select
Custom Arrow Block does (`CadQLeaderRequest.Blocks`, `LeaderSettingsViewModel.UserArrowBlocks`): the one chosen becomes
the arrow, written on OK as DIMLDRBLK and as group 3 of the settings (`CadQLeaderSettings.UserArrow`).

**FIXED.** Associative dimensions in the presenter tie and follow as AutoCAD 2021 does where they did not. An end
snapped where several lines, arcs or polylines meet is tied to the one drawn last, wherever the cursor stood: the corner
(0, 0) of a line to (100, 0) and a line drawn after it up to (0, 100) is the start of the second. DIMANGULAR of two lines
writes the line selected second as its first (groups 13 and 14). A linear dimension whose origin moves keeps its line as
far from the origin it lay nearer to, but where that would put it nearer the other origin, as far from that one: origins
(0, 0) and (60, 40) with the line between them at y = 20, the first moved to (0, 5), put the line at y = 60, where it
went to 25; and of two origins as far from a line between them, the one the line lies across from counts as the
nearer. The words of a radius or a diameter whose circle is scaled keep their end on the leader's side, "R9.0000"
becoming "R13.5000" moving group 11 0.07 away from the leader, where it stayed. Three points of an angle along one ray
measure nothing, the arc's point on the ray as far out as the point given, where they measured the whole turn; and the
second point of an angle of a circle snapped on an arc of a polyline is tied to that segment at nought, as AutoCAD ties
it. 107 dimensions made and edited in AutoCAD 2021's console were made and edited alike here, 262 states of a dimension
in all: 238 equal AutoCAD's in every point, group 11 and 42 and tie to 1E-4, 118 before. Of the others, AutoCAD
writes flag 128 on every radius and diameter it makes; puts the arc of a part of an arc's length, or of an arc under 90
degrees, up to 0.43 from where ours goes, and that of an angle too small for its words two thirds of the way round, not
a third; measures the whole turn for three points along a ray at 120 degrees one rounding apart; and dimensions an arc of
a polyline with DIMARC, which the presenter does not.

**NEW.** DIMARC in the presenter ties an arc length dimension to its arc, as AutoCAD 2021 does: the ends of the length
as the arc's start and end (a Partial length's as nearest points of the arc at their angles) and the arc's center. It
follows the arc moved, turned, scaled or lengthened, its arc as far out and a third of the way along, its words in the
middle: the arc of 50 from 0 to 120 degrees dimensioned 80 out and lengthened by 10 at its end measures 114.72, its arc
point at (57.72, 55.39) from the center. Its first and second points are the arc's start and end also where it measures
the rest of the circle, and a part's points in the order given, as AutoCAD writes them (groups 13, 14, 40 and 41); they
were the ends of the length measured, in the order the arc runs.

**FIXED.** The words of an angular dimension too wide for its arc, put level past its first ray, run out the way the arc
runs on there, left or right, as AutoCAD 2021 puts them: those of an angle of 3 degrees from -5 degrees go to the left
of the arc run down, where they went right, and those of one from 100 degrees to the right.

**CHANGED.** A number typed in a table's cell in the presenter (`CadTableEditor.SetText`, TABLEDIT and the editor of a
cell) is kept as AutoCAD 2021's `SetValue` with `ConvertTextToValue` keeps it, where decimals and fractions stayed text:
1.5 is a number of the format of the drawing's units (`%lu6`), shown 1.5000 at LUNITS 2 and LUPREC 4; 1e3 one of `%lu1`
shown 1.0000E+03; 1/2 the number 0.5 of `%lu5` shown 1/2, and 1/3 shown 5/16; 1,5 the point (1, 5, 0) shown 1.0000,
5.0000, 0.0000. A number typed in a cell of no alignment of its own sets it top right, as AutoCAD does, the whole numbers
1 and 007 too, which stood where the row's style put them. A number typed over a number keeps the cell's kind and format,
12 over 1.25 shown 12.0000. 1,500, which AutoCAD reads as 1 500 of thousands, feet and inches, and dates stay text.

**CHANGED.** `DxfTable.Fill` with `GenerateSeries` writes each number in the format of the source cell its place repeats,
and shows it in it, as AutoCAD 2021 fills a series, where it wrote six places and kept the target's format: 1.5 of
`%lu6` goes on to 2.5000 and 3.5000 at LUPREC 4, and 1/2 over 1.5 to 2 1/2, 3.5000, 4 1/2 and 5.5000. A source of no
format still gives 3.500000.

**FIXED.** `DxfTable.SetDataType` turning a text into a number in decimal units rounded as the runtime rounds: on .NET Core
and .NET 10 2.5 at no places was shown 2 and 0.015 at two 0.01, where AutoCAD 2021 and .NET Framework show 3 and 0.02.
Both now round half away from nought as the number's 15 figures write it.

**FIXED.** `AddVisibilityParameter` wrote each visibility state listing no parameter, so AutoCAD 2021 read every other
property of the block as not visible in any state and showed none of its grips: a sign of the states Cross, Tick and
Blank with a Width kept Width out of Properties and its arrow off the INSERT. Each state now lists the block's other
parameters, their grips and its actions, those added before the visibility parameter and those added after it, as AutoCAD
lists them in each state of its Door sample, and AutoCAD 2021 reads Width as visible and shows its grip in all three
states. A state of a drawing that leaves a parameter out keeps it out; a parameter added afterwards joins every state.

**CHANGED.** `DxfBlockReference.GetDynamicGrips` gives an XY parameter's grips at its two other corners after the one at
its end, base x with end y and end x with base y, and the grip of a block properties table at its location, of the new
`DxfDynamicBlockGripType.BlockTable`, as AutoCAD 2021 shows them: a rectangle 100 by 50 of an XY parameter from (0, 0)
inserted at (1 000, 500) has its grips at (1 100, 550), (1 000, 550) and (1 100, 500), and a table at (0, -20) its grip
at (1 000, 480). Code that took the grips of such a block by their place in the list finds more of them.

**NEW.** The presenter shows those grips: an XY corner dragged sets the distance of the axis it takes from the end point,
the corner of base x and end y of the rectangle above dragged by (20, 10) a Height of 60, where AutoCAD 2021 also moves
the parameter's base point and reads a Width of 80, the block drawn alike; a block table grip clicked lists the rows,
"100.0000" and "200.0000" for a table of Width, and the one chosen sets Block Table1 and its columns in one undo step.
While one custom grip is dragged, the STRETCH prompt previews the block worked out at the cursor, the drawing left as it
is until the point is picked (`CadDynamicBlocks`, `CadPreview`).

**NEW.** GRIPDYNCOLOR sets the colour of a dynamic block's custom grips, an index of AutoCAD's colours from 1 to 255, 140
(#00BFFF) by default as AutoCAD 2021 starts; it is kept in the settings file (`GlobalSettings.GripDynamicColor`) as
AutoCAD keeps it in the registry, and given to the canvas (`CanvasOptions.CustomGripColor`). The custom grips were drawn
#19B5F0 before.

**NEW.** DIMANGULAR in the presenter measures the angle of a circle, as AutoCAD 2021 does: from the point the circle was
picked at, about its center, to a second point (`Specify second angle endpoint:`), both brought onto the circle, and
ties it as AutoCAD writes the tie: the first point and the center to the circle, the second as a nearest point of what
it was snapped to, a line at the parameter of the point on the circle run on, another arc or circle or a POINT at
nought, with no snap the circle at its angle. A circle of 25 about (200, 0) picked at (225, 0), with the end (300, 40)
of a line from (260, 40) as the second point, measures 21.8 degrees to (223.212, 9.285), tied at -0.9197 along the
line. When the objects move, such a point past the end of its line, or on a POINT, is dropped and stays, and one within
its line goes to its point there, as AutoCAD moves them: the circle moved 10 down, the angle measures 39.72 degrees.

**FIXED.** The PERpendicular snap of the presenter gives the foot on a line or a straight polyline segment run on past
its end, as AutoCAD 2021's does (from (20, 120) square to (100, -20)-(100, 60), the point (100, 120)), and an
associative dimension snapped so is tied at that parameter (1.75), a polyline's foot to the segment run on, and follows
as AutoCAD follows it: the line moved by (20, 7), the origin drawn from a point tied to nothing goes to (120, 0). An arc
still gives no foot off its run, as AutoCAD gives none.

**FIXED.** The arc of an angular dimension made in the presenter goes through a point a third of the way round from the
leg it starts at, as far out as the point given, as AutoCAD 2021 writes group 10 (16 for two lines): put at (50, 550) in
the right angle at (0, 500), it passes (61.237, 535.355), where the point given was kept. An angle of three points whose
objects move keeps its arc as far out at the same share of the angle, inside the legs where it measured less than half a
turn and outside where it measured more, as AutoCAD keeps it, where it stayed where it was; and the words of a radius or
a diameter whose circle is scaled stay where they stood, as AutoCAD keeps group 11, where they moved 0.7. Each
associative dimension that follows writes groups 11 and 42 anew. 23 associative dimensions of every kind, made and then
edited by moves, stretches, rotations and scalings in AutoCAD 2021's console and here, were compared in 20 states:
1 247 of 1 335 points, groups 11 and 42 and ties equal AutoCAD's to 1E-3. The others (an end where two lines meet, the
order of an angle's two lines, the words of a radius or a diameter grown by a digit, one linear dimension line) are set
right by the entry on ends where objects meet above; the words were measured 0.025 to 0.07 apart only because that
comparison drew them in txt, where AutoCAD's template has Standard in arial.ttf.

**NEW.** The External References palette's Bind… shows Bind Xrefs/DGN Underlays (`XrefBindWindow`, `XrefBindViewModel`)
for the drawings selected, the palette now selecting several with Ctrl or Shift (`SelectedRows`): Bind or Insert, started
from BINDTYPE, and OK writes the choice back to BINDTYPE and binds them all as -XREF Bind binds them, in one undo step.
It is offered where the selection holds a drawing loaded or needing reloading, and greyed for one of only drawings
unloaded or not found, or of images. A bind refused binds none, and the lines -XREF Bind writes, "Xref "column" is nested
and cannot be bound." among them, are shown in a box (`XrefBindRefusedWindow`) as well as on the command line. A host sets
`MainViewModel.ShowXrefBind` and `ShowXrefBindRefused`; with no dialog set, Bind… binds by BINDTYPE as it stands. Global
settings › Text and fonts says on top what its Hidden messages keep.

**FIXED.** `CollidesWith` and `DistanceTo` of a triangle, a polygon, a face, a plane, a solid or another box against a
box flat on some axis, `GeoObb3` or `GeoAabb3`, threw "A box with a zero size on some axis has no faces to build"; they
read the box as the shape it has become and answer as usual: flat on one axis its rectangle, on two its segment, on all
three its point. The box 100 by 100 by 0 about (50, 50, 0) is 20 from the triangle with corners (10, 10, 20),
(90, 10, 20) and (10, 90, 20) lying over it, and touched by a square standing upright across it. A box with its faces
gives the same answer bit for bit, an empty `GeoAabb3` is still refused, and `GetFaces()` and `ToSolid()` of a flat box
still throw.

**NEW.** The presenter's Columns › Resize and Rows › Resize, on the right-click menu of a table's cells, start the drag of
the selection's right (or bottom) line as though its edge grip had been clicked: the line follows the cursor, and a
click, Enter or a size typed makes it, one undo step (`CadCanvas.DragCellGrip`, `MainViewModel.CellGripDragRequested`).
A row's line dragged with dynamic input on shows the field of its height left of the table, as a column's shows its
width over it, and takes a height typed.

**CHANGED.** The drags of a table's grips follow the object snaps, ORTHO and polar tracking from the grip, as the other
grip drags do: with Endpoint on, a column's line dragged near the end of a line at 53 makes the column 53 wide, and with
ORTHO the corner's stretch grip dragged right and a little down stretches the width alone. A perpendicular from the grip
to the table's own edge it stands on, which would hold the line where it was, is not taken.

**CHANGED.** A whole number typed in a table's cell (`CadTableEditor.SetText`, TABLEDIT and the editor of a cell) is kept
as a number, as AutoCAD 2021 reads a text it parses: 1 is the number 1 and 007 the number 7, so that the autofill grip
dragged from a typed 1 gives 2, 3 and 4 where it copied the 1. b1, 1.5, 12mm and a number in a cell made to hold text
stay text.

**FIXED.** With dynamic input, the width field of a column dragged in a table whose rows flow up ran from the new line
the wrong way, over the next column; it runs over the column dragged, below the table, where its first row is.

**FIXED.** `GetIntersections` of a triangle, a polygon, a face or a plane with a box flat on some axis, `GeoObb3` or
`GeoAabb3`, or with an empty `GeoAabb3`, threw "A box with a zero size on some axis has no faces to build" or "An empty
bounding box has no shape to convert"; it gives no points, as `TryIntersectWith` answers false: the plane x = 20 and the
box 100 by 100 by 0 cross nowhere. `GetFaces()` and `ToSolid()` of such a box still throw, as before: it has no body.

**FIXED.** The `Try` methods of a segment answered true with points of NaN or infinity where the segment, or the shape
given with it, had a coordinate that is not a finite number: the segment from (NaN, 0) to (0, 100) and the one from the
origin to (0, 100) were given a point of NaN. They answer false now, for a segment, a ray and a line, a circle and an
arc, a chain, a curved chain and a curved loop, a face, a rectangle and a triangle, and for `TrySplitAtDistance`,
`TryExtendTo` a point and `TryTrimExtendToCorner`, whichever of the two shapes the method is asked of; a curved loop with a
vertex or a bulge of NaN, and an arc with an end of NaN, threw from inside and are false too. Every finite answer is the
same bit for bit.

**FIXED.** A fillet radius that is not a positive number, given to `TryFilletAt` or `Fillet` of a chain or a loop, flat or
in space, was refused with "A chamfer distance must be a positive number."; it is refused as a radius.

**NEW.** `DxfDatabase.BindXref(block, referenced, insertBind)` binds an external reference as AutoCAD's `BindXrefs`: its
block becomes an ordinary one about the base point of its drawing, holding a copy of that drawing's model space, the
records it draws by brought under the reference's name (`GRID$0$WALL`, `GRID$1$WALL` where `GRID$0$WALL` is taken), or
merged by name when inserted; the records the reference brought (`GRID|WALL`) are renamed in their places, with what the
drawing set on them. The presenter's -XREF Bind binds by BINDTYPE (0 Bind, 1 Insert, new and kept by the session) as
AutoCAD 2021 does, the references the drawing places with it, in AutoCAD's words for one unloaded, not found or nested
("Duplicate Block "TAG" ignored." when inserting); it refused Bind.

**NEW.** `DxfRasterVariables.GetRasterVariables(db)` makes the variables of images of a drawing with none, as AutoCAD 2021
makes them when IMAGEFRAME is first changed (frames shown, pictures finely, 72 the unit of the drawing: 1 for millimetres,
5 for inches). The presenter's IMAGEFRAME, typed or from the frame box, now sets a drawing with no image yet, which it
refused; the value it has already makes nothing, as in AutoCAD.

**NEW.** `DxfMLeaderStyle.Block`, `BlockColor`, `BlockScale` and `BlockConnectionType` are set as well as read. The
presenter's Modify Multileader Style dialog offers Block content (Source block, one of the drawing's blocks, Attachment,
Color, Scale), and MLEADER in a style of a block asks its attributes ("Enter attribute values", "Enter tag number
<TAGNUMBER>:") and places it past the dogleg as AutoCAD 2021 does: a circle of radius 5 landed at (20, 10) with a dogleg of
0.36 at (25.36, 10). MLEADER in a style of no content draws the leader alone, asking no text. In a drawing of R2007 or
later with no multileader style MLEADER makes Standard current first (AutoCAD's imperial values in an imperial drawing),
where it refused. Scale multileaders to layout and Specify scale are greyed while Annotative is ticked.

**FIXED.** IMAGEFRAME 2 is written as frames shown and not plotted (RASTERVARIABLES 70 of 3), as AutoCAD 2021 writes it; the
presenter wrote 2, the setting AutoCAD's API names `ImageFrameBelow`.

**NEW.** The presenter edits a table's cells on the canvas as AutoCAD 2021 does. Cells selected are framed by the column
letters and row numbers, those of the selection on amber (TABLEINDICATOR 0 or 1, kept on the machine; Table Indicator
Color on the right-click menu of a table and in Global settings › Appearance, `GlobalSettings.TableIndicator` and
`TableIndicatorColor`). A press in a cell dragged selects a range, held at the table's edge and grown to merged blocks.
The cells' edge grips move a line of the table and their autofill diamond fills the cells it is dragged over, a 1
dragged three cells down giving 2, 3 and 4, with a right-click choice of Fill Series, Copy Cells and the rest. A whole
table has AutoCAD's grips (`CadGripKind.TableColumn`, `TableWidth`, `TableHeight`, `TableSize`, `TableBreak`): move,
column lines with the width kept or with Ctrl the table growing, and stretch; the breaking grip is drawn but not offered.
Each drag shows its new line, outline or values, takes a width typed with dynamic input, and is one undo step: Autofill,
Column Width, Row Height or Table Size (`CadTableDrag`). No column goes narrower than AutoCAD's `MinimumColumnWidth`, its
two margins and its tallest text, 9 in Standard. The cells' right-click menu is AutoCAD's, its working items wired and the
rest grey (`TableCellViewModel.Menu`). New in `CadTableEditor`: `Fill`, `SetColumnWidths`, `SetRowHeights`,
`RemoveOverrides`, `CellNear` and `MinimumColumnWidth`; Columns and Rows › Size Equally and Remove All Property
Overrides work.

**FIXED.** The presenter's selection of several rows of a table's cells was drawn as a slanted shape:
`CadTableEditor.Corners` took the top right and bottom left corners from the wrong cells. B3:C4 of the Standard schedule
now runs from (63.5, -20) to (190.5, -38).

**FIXED.** A click in an empty cell of a table, with no object under the pick box, opened a selection box instead of
selecting the cell; it selects the cell.

**FIXED.** Merge › By Row and By Column of the Table Cell tab made one undo step per row or column merged; they make one.

**NEW.** The presenter's Quick Select (QSELECT) has AutoCAD's Select objects button at the right of Apply to: it hides the
dialog while `Select objects:` asks on the command line, and the dialog comes back over the objects picked, Apply to:
Current selection, so a line of DOORS and a text picked offer Multiple, Line and Text. It is greyed while Exclude or
Append is chosen. `QuickSelectViewModel.CanSelectObjects`, `IsSelectingObjects` and `SelectObjects()` are new; the
window answers QSELECT's request with "Select objects".

**NEW.** The presenter's Purge dialog previews an unused multileader style as MLEADER draws one and an unused table
style as the Table Style dialog draws it (`PurgeViewModel.TablePreview`), as it did blocks, text and dimension styles.

**FIXED.** The presenter's Block Attribute Manager (BATTMAN) wrote the changes made to a block without asking when
another block was chosen or Select block pressed; it now asks `Save changes to the attributes of block GB?` first, as
AutoCAD's does, No dropping them (`CadAttributeManagerRequest.HasChanges` and `Revert()`,
`BlockAttributeManagerViewModel.AskSaveChanges`). Move Up and Move Down are greyed on a constant attribute, as AutoCAD's.

**FIXED.** An MTEXT whose record gives neither a direction (group 11) nor an angle (50) reads along the world's x axis put
into its plane, as AutoCAD 2021 reads it, rather than along the plane's own x axis: on the normal (0, 0, -1) `Direction` is
(1, 0, 0) and `Rotation` pi, where it was (-1, 0, 0) and nought, and the display draws it so. The presenter's MIRROR under
MIRRTEXT 1 writes an MTEXT along x mirrored across a level line with no 11, as AutoCAD does; `Normal` set on an MTEXT of
neither still reads along the new plane's x axis, now written as its direction (11 of (-1, 0, 0) for (0, 0, -1)).

**FIXED.** A header with no `$AUPREC` reads `Auprec` 0, and one with no `$MIRRTEXT` reads `Mirrtext` false, as AutoCAD 2021
reads them; they read 2 and true. A drawing without them now shows angles to whole degrees (DIST, MEASUREGEOM, the
Properties palette, prompts) and keeps texts readable under MIRROR.

**FIXED.** The presenter's MIRROR writes a MINSERT as AutoCAD 2021 writes it, as an INSERT with its columns the other way:
3 columns 15 apart and 2 rows 8 apart mirrored across y = 100 are written x factor -1 turned 180, 44 of -15 and 45 of 8,
where they were y factor -1 and 45 of -8. An annotative INSERT is written so in its record and in each of its contexts,
which kept the factors the mirror gave them. A copy of a dynamic block's INSERT made by MIRROR or COPY keeps the values its
parameters are set to: a door flipped stays flipped, where the copy lost its representation and was no longer dynamic. A
multileader turned or mirrored keeps the sizes of its context (41, 140, 145) and its doglegs to the bit: words 4 high
mirrored across a line at 45 degrees stay 4, not 4.000000000000001. Checked against AutoCAD 2021's MIRROR in its console
on 64 cases.

**NEW.** The presenter's SNAP and GRID typed ask as AutoCAD 2021's do, checked in its console. SNAP offers
[ON/OFF/Aspect/Legacy/Style/Type], Aspect left out with an isometric snap, and A where the two spacings differ; Style
turns the snap and the grid over as AutoCAD does (a snap of 10 with a grid of 4 by 6 turned isometric at 5 keeps 8.6603 by
2.5 and a grid of 10.3923 by 3, back to Standard at 4 a snap of 4 by 4 and a grid of 6 by 6), Type sets SNAPTYPE and
POLARDIST. GRID offers [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect]: a spacing, 2x for twice the snap's, Major
(GRIDMAJOR), and aDaptive, Limits and Follow setting the bits of GRIDDISPLAY. Every answer but OFF turns SNAP or GRID on;
nought or text is refused in AutoCAD's words and asked again. SETVAR takes SNAPUNIT, GRIDUNIT, POLARDIST, SNAPTYPE,
GRIDSTYLE and GRIDDISPLAY as AutoCAD does: SNAPUNIT 0,5 gets "Cannot set SNAPUNIT to that value.", GRIDUNIT -2,3 is kept as
2,3, POLARDIST -1 is kept and "abc" gets "Requires numeric distance or two points.", and with an isometric snap SNAPUNIT
4,6 reads back 4,12. SNAPMODE takes 2, as AutoCAD does. The canvas draws the grid as dots where GRIDSTYLE says (1 model
space, 2 the Block Editor, where the grid is now drawn too), within the limits without GRIDDISPLAY 1, not at all where too
dense without 2, and with lines added between as it zooms in with 4. Object snap tracking runs its level and upright
lines along the axes turned by SNAPANG, or those of the isoplane, the polar lines from the base point turned by SNAPANG
too, and PolarSnap steps along a tracking line from the point acquired. `CadSystemVariable.Point` with a take that may
refuse, `CadSystemVariable.Distance` and `CadSystemVariable.Switch`, the canvas's `GridStyle` and `GridDisplay`, and the
window's `MainViewModel.GridStyle` and `MainViewModel.GridDisplay` are new.

**CHANGED.** LWDISPLAY is shown as OFF or ON and takes ON and OFF as well as 0 and 1, "Requires 0 or 1 only, or option
keyword." refusing the rest, as AutoCAD 2021's. A whole-number variable refuses text and numbers not whole in the words
it refuses a value out of range with ("Requires an integer between 0 and 7." for GRIDSTYLE abc); a point of two
coordinates refuses text with "Invalid 2D point."; a distance asked with no base point refuses text with "Requires
numeric distance or two points.", or "Requires numeric distance, two points, or option keyword." where it has options.
`GlobalSettings.PolarDistance` takes a value below nought, as AutoCAD's POLARDIST does, and throws only for one not
finite.

**NEW.** The DWG reader gives the overrides of a table of AutoCAD 2010 and later as AutoCAD 2021 writes them in DXF:
the table's own (93 to 96 and their groups) and each cell's flags and groups (170, 7, 63, 64, 283, 140 and its edges),
its row's looks pushed into it and the line under a title of 0.35 given on each cell of the header below. It reads the
TABLECONTENT and TABLEGEOMETRY a table keeps for its round trip, from AutoCAD 2007 (the TABLEGEOMETRY of every version),
and gives a table style with cell styles of its own (101 on) the CELLSTYLEMAP AutoCAD writes in DXF. A merged range gives
its edges on its last cell alone, without those inside it, and the cells it covers no content format and an empty value:
a title over three columns reads 172 as 0, 0 and 3, its top and right on the third cell. Five table drawings
AutoCAD 2021 made, cells of one override each, a cell of all six, cells of four edges, a table of its kinds of row
overridden and a clean table style of its own, read to AutoCAD's own DXF of them, group for group.

**NEW.** The DWG writer writes the overrides of a table's cells (their alignment, fill, colour, text style, height and
borders) as AutoCAD keeps them, the cell styles of its rows, columns and whole from the TABLECONTENT it keeps for a round
trip or else from its own overrides (93 to 96), and that TABLECONTENT as its own groups give it. AutoCAD 2021 reads each
of the five drawings written so to the DXF it was written from, but `$CMATERIAL`, and `AUDIT` finds 0 errors in each. A
table style's cell styles of its own (101 on) are written from the CELLSTYLEMAP it holds in DXF, each after its own id as
AutoCAD writes it, and the rows, columns and cells that take one keep it: a table taking GhNote (101) and GhWide (102)
by a row, a column and a cell is read by AutoCAD 2021 to the DXF it was written from, its two cell styles group for
group, and `AUDIT` finds 0 errors.

**FIXED.** The DWG writer and reader gave the lines of a table style's rows the wrong places: the inside horizontal line
went to the right and the right to the inside horizontal, and a line's colour was read only where flagged as overridden,
which a table style's lines never are. A data row's red bottom line now stays its bottom line.

**FIXED.** The DWG writer read a table's row heights from the wrong groups where the table gives overrides of its own,
which AutoCAD writes before the heights: a table of 4 rows of 10 was written with rows of 0.

**FIXED.** The DWG writer wrote an MTEXT of an angle and no direction along (cos, sin, 0) whatever its plane: it reads
along its plane's x axis turned by the angle, as AutoCAD takes it, so one of the normal (1, 0, 0) turned 30 degrees
reads along (0, 0.866, 0.5). The DWG reader no longer gives a BLOCK or an ENDBLK a material (347), which AutoCAD does
not write.

**NEW.** The Snap and Grid tab of the presenter's Drafting Settings as AutoCAD 2021's, the dialog now 560 by 580 on every
tab: Snap X and Y spacing with Equal X and Y spacing, Polar distance (POLARDIST), Grid snap or PolarSnap (SNAPTYPE),
Rectangular or Isometric snap (SNAPSTYL), the dotted grid of Grid style (GRIDSTYLE), Grid X and Y spacing (GRIDUNIT),
Major line every (GRIDMAJOR) and Grid behavior (GRIDDISPLAY 1, 2, 4 and 8), each greyed where AutoCAD greys it. With the
isometric snap the Y boxes show twice what the drawing keeps, so a snap of 8.6603 by 2.5 shows 5. OK writes the values
changed, the drawing's into its `*Active` viewport, SNAPTYPE, POLARDIST and GRIDSTYLE into the settings file; a box
refused is edged in red and nothing is written. PolarSnap works: with SNAP on, a point held on a polar angle is taken a
whole number of POLARDIST from the base point (250 takes a cursor 619.7 along to 500), the snap X spacing where POLARDIST
is nought. Checked in AutoCAD 2021's console: a new drawing has SNAPUNIT and GRIDUNIT 0.5 (acad.dwt) or 10 (acadiso.dwt),
GRIDDISPLAY 3, GRIDMAJOR 5, and SNAPTYPE, POLARDIST and GRIDSTYLE 0; GRIDUNIT takes -1,1 as 1,1. `ObjectSnap.PolarSnap`,
`ObjectSnap.PolarDistance`, `GlobalSettings.PolarSnap`, `GlobalSettings.PolarDistance` and `GlobalSettings.GridStyle` are
new.

**NEW.** The presenter applies SNAPBASE, SNAPANG and GRIDMAJOR of the drawing's `*Active` viewport, which it read and
left alone: the snap grid is laid from SNAPBASE and turned by SNAPANG (a snap of 10 turned by 30 degrees puts the cursor at
(9, 4) on (8.6603, 5)), ortho holds the cursor along the turned axes, the polar angles are counted from SNAPANG, the
crosshair turns with it, and the grid is drawn from the same base at the same angle with a major line every GRIDMAJOR.
SNAPSTYL 1 makes the snap isometric, as AutoCAD 2021's: its points where the 30, 90 and 150 degree lines cross, ortho,
the crosshair and the grid on the two axes of the isoplane SNAPISOPAIR names, ISOPLANE, and F5 or Ctrl+E turning to the
next plane, "<Isoplane Top>", while a command runs. SNAPBASE, SNAPANG, GRIDMAJOR, SNAPSTYL and SNAPISOPAIR are set by
SETVAR, refused in AutoCAD's words and written into the drawing. `ObjectSnap.SnapAngle` and `ObjectSnap.Isoplane`, the
`CadIsoplane` enum, `CadSystemVariable.Angle` and `CadSystemVariable.Point`, and the canvas's `GridMajor`, `SnapBase`,
`SnapAngle` and `Isoplane` are new.

**FIXED.** A system variable of 0 and 1 given 2 says "Requires 0 or 1 only.", as AutoCAD 2021 refuses ORTHOMODE, GRIDMODE,
PICKFIRST or FILEDIA 2, where the presenter said "Requires an integer between 0 and 1.".

**NEW.** The presenter's Leader Settings dialog of QLEADER (Settings, or Enter at its first prompt), with AutoCAD 2021's
Annotation, Leader Line & Arrow and Attachment tabs, and QLEADER drawing by every setting: the annotation type (MText,
Copy an Object, Block Reference, None; Tolerance greyed), Annotation Reuse, Prompt for width, Always left justify, Frame
text, straight or spline, the arrowhead, the number of points or No Limit, the angle constraints of the first two
segments, the attachment of text on either side and Underline bottom line. The settings are kept as AutoCAD 2021 keeps
them, in the drawing's `AcadDim` XRECORD (group 90 of 990106) read by `CadQLeaderSettings`, and the arrowhead as
DIMLDRBLK; OK writes both as one undo step. Checked in AutoCAD 2021's console: group 62 is the text on the right side and
63 on the left, 61 of 2 is Reuse Current, a first segment held level takes (100, 30) to (100, 0) and a second held to 45
degrees takes (150, 100) to (175, 75), Frame text writes DIMGAP below nought and Underline bottom line DIMTAD 1. Without
a window, QLEADER says Leader Settings is shown by the window and draws by the settings the drawing has.

**NEW.** The Polar Tracking tab of the presenter's Drafting Settings as AutoCAD 2021's: Additional angles with their list,
New and Delete (POLARADDANG, POLARMODE 4, ten at most, "Not an angle." for one that does not read), Object Snap Tracking
Settings (POLARMODE 2), Polar Angle measurement (POLARMODE 1), and the increment typed or chosen. While polar tracking
holds the cursor, a dotted path runs from the base point through it and the tip says `Polar: 183.8478 < 45°` in the
drawing's units, or `Relative Polar: 100.0000 < 90°` where the angles are counted from the last segment.

**CHANGED.** A new drawing of the presenter (NEW, Ctrl+N) is named Drawing1.dwg, then Drawing2.dwg, as AutoCAD 2021's
DWGNAME of a new drawing is, where it was Drawing1.dxf. Save As offers it as an AutoCAD 2018 Drawing (*.dwg) first, and
QSAVE with FILEDIA 0 asks "Save drawing as <Drawing1.dwg>:", a name typed with no extension saved as a DWG. A DXF is still
written where the name chosen ends in .dxf.

**FIXED.** The presenter's autosave of plan.dwg and plan.dxf open from one folder: both copies were named
plan.autosave1.dxf, so the second pushed the first down the line, to plan.autosave2.dxf, and the oldest out. The copy of a
DWG now keeps its extension in its name, plan.dwg.autosave1.dxf; that of a DXF is plan.autosave1.dxf as before.

**FIXED.** The presenter's MIRROR writes an INSERT as AutoCAD 2021's MIRROR writes it: its x factor turned the other way
rather than its y, turned to its mirrored y axis less a quarter turn (one at (20, 10) mirrored across y = 100 is written
at (20, 190), x factor -1, turned 180 degrees), its ATTRIBs without a normal of (0, 0, 1) written out. With MIRRTEXT 0 an
attribute or TEXT turned over across its baseline is turned about the middle of the ink of its words ("Q1" of arial.ttf
1.5 high about 0.705, not 0.75); with MIRRTEXT 1 the attributes of an INSERT already mirrored keep their normal of
(0, 0, -1). A multileader mirrored across a line at 45 degrees keeps its words held by the same side, as AutoCAD keeps
them, where it was taken for one mirrored across an upright.

**FIXED.** `DxfDBText`, `DxfBlockReference` and `DxfMText` turned in their own plane by `TransformBy` (the presenter's
ROTATE and MIRROR) are written as AutoCAD 2021 writes them: the angle from nought to a whole turn ("ABC" turned by -30
degrees written 330, not -29.999999999999996), the width factor and height as they were where nothing is scaled (no
41 0.9999999999999999), no angle of nought, and no normal of (0, 0, 1) written out. A text turned out of its plane keeps
the angle ezdxf gives it.

**FIXED.** The presenter draws a TEXT with the spaces before and after its words, as AutoCAD justifies it by them: "ABC "
on the right of its baseline stands a space further left than "ABC".

**NEW.** `DwgWriter` writes MULTILEADER and MLEADERSTYLE, ACAD_TABLE with its TABLESTYLE (and the TABLECONTENT and
TABLEGEOMETRY a table keeps for a round trip), the ATTDEF and ATTRIB of several lines with their MTEXT, and
RASTERVARIABLES and WIPEOUTVARIABLES, where it left them out and wrote every handle to them as 0. A table goes in the
form of AutoCAD 2010, its content made of the groups DXF keeps of AutoCAD 2007: a table of 3 by 3 with its title merged
across, written and opened in AutoCAD 2021, gives AutoCAD's own DXF of it back group for group, and its table style is
AutoCAD's own bits. A drawing with an image saves as a DWG with nothing left out, where the presenter said "Left out of
the DWG, not written yet: RASTERVARIABLES 1.". The overrides of a cell and of a whole table are not written yet.

**NEW.** `DwgReader` reads ACAD_TABLE and TABLESTYLE of AutoCAD 2010 to 2018, in the groups DXF keeps of AutoCAD 2007,
and RASTERVARIABLES and WIPEOUTVARIABLES; the tables of AutoCAD 2010, 2013 and 2018 read to AutoCAD's own DXF of them.

**NEW.** The presenter's LEADER (LEAD) and QLEADER (LE) on the command line, in AutoCAD 2021's prompts: Format (Spline,
STraight, Arrow, None), Undo, words typed line by line, and the annotation options Copy, Block, None and Mtext. The
leader is drawn by the current dimension style and DIMSCALE as AutoCAD 2021 draws it: a hook DIMASZ long where the last
segment is steeper than 15 degrees, the MTEXT DIMGAP past the end, and the variables other than the style's written as
the leader's overrides (DIMSCALE 2 writes `1070 40, 1040 2` and words twice as high). QLEADER takes its dialog's default
settings; the Leader Settings dialog and the Tolerance option are not there yet.

**NEW.** MLEADERSCALE and CMLEADERSTYLE through SETVAR, as AutoCAD 2021 keeps them: a style made current gives
MLEADERSCALE its scale, again when it is current already; MLEADERSCALE set scales the multileaders drawn next and leaves
the style alone (2 in a style of scale 5 draws words 4 high at 8); nought draws at 1, or through a viewport of 1:10 at
10. A value below nought, or a name of no style, says `Cannot set ... to that value.`.

**CHANGED.** MLEADER draws at MLEADERSCALE, no longer at the current style's scale: a drawing that keeps no MLEADERSCALE
reads 1, as AutoCAD 2021 reads it, whatever its current style's scale, until a style is made current again.

**NEW.** The presenter's DIMLINEAR has AutoCAD 2021's Mtext, Angle and Rotated options, and DIMALIGNED Mtext and Angle:
0,0 to 100,0 rotated 30 measures 86.6025, and words turned by Angle stand centred on the line, which is broken about their
turned box. DIMTFILL draws its mask behind a dimension's words (the MTEXT's background, 1.25 times their height) and
DIMTXTDIRECTION turns them half round, as AutoCAD 2021 writes them into the dimension's block.

**FIXED.** Dimensions made in the presenter write the middle of their words (group 11) and their measurement (42), where
DIMLINEAR wrote 0,0,0 and none. Text position X and Y of the Properties palette move a linear dimension's line through
the words under DIMTMOVE 0, as AutoCAD 2021 moves it. A jogged ordinate under DIMFXLON starts its leader DIMFXL before
the jog, not at DIMEXO.

**NEW.** The presenter's SCALELISTEDIT shows AutoCAD 2021's Edit Drawing Scales with Add Scale and Edit Scale: Move Up
and Move Down, Delete greyed for a scale the drawing keeps, units greyed for one an object points at, Reset after a
question; OK writes the drawing's list (ACAD_SCALELIST) as one undo step, a renamed or moved scale keeping its SCALE and
handle; with no window it asks -SCALELISTEDIT's prompts. `CadScaleListRequest`, `CadScaleListRow`.

**FIXED.** -SCALELISTEDIT Reset in a drawing whose MEASUREMENT is 0 puts back AutoCAD 2021's 16 imperial scales,
`1/128" = 1'-0"` to `1'-0" = 1'-0"`, not the metric 1:1 to 100:1, as its console resets them.

**NEW.** The presenter's Layer manager renames a layer in place: F2, a slow second click, a double click or Rename
Layer; refused in AutoCAD 2021's -RENAME words (`Cannot rename layer "0".`, `A layer named "DIM" already exists.`,
`Invalid layer name.`); `CadLayerEditor.Rename`.

**FIXED.** Defpoints can be renamed (`DxfLayerTableRecord.Name`), as AutoCAD 2021's -RENAME and entmod rename it;
layer 0 is still refused.

**NEW.** The Layer States Manager's New… asks a description beside the name, and asks before it replaces a state of
the name; Edit… shows AutoCAD's Edit Layer State: the state's layers with their properties changed in the state only,
Add layers and Remove layers, the properties it restores. `CadLayerStatesRequest.Replace`, `Has`, `BeginEdit`, `Edit`,
`CadLayerStateEdit`, `CadLayerStateLayer`.

**NEW.** INPUTSEARCHOPTIONS shows AutoCAD 2021's Input Search Options (mid-string search, the order of the
suggestions, Remember corrections after 2 to 10 mistypes, Separate commands and system variables, the delay, the
Content type list and its order), from the command line's Input Settings too; -INPUTSEARCHOPTIONS asks it as AutoCAD's
console does. INPUTSEARCHOPTIONFLAGS (1 AutoComplete to 16 Mid-string search) is a system variable, and so is
INPUTSEARCHDELAY, which Delay Time… now asks as `Enter new value for INPUTSEARCHDELAY <300>:`. `GlobalSettings`
gains `SortAlphabetically`, `SeparateSystemVariables`, `AutoCorrectMistypes`, `ContentTypes` and `InputSearchFlags`.

**FIXED.** The presenter's drawing commands checked entity by entity against AutoCAD 2021 drawing the same picks.
PLINE keeps the last arc's bulge on its end vertex; a first arc and ARC Enter go on from the last polyline drawn, and
Undo moves that point back; Width after Halfwidth offers the ending width. Angle defaults are written to AUPREC (40.587
is offered as 41), and TEXT under ANGDIR 1 stores 330, not -30. MTEXT with a rotation measures its box along it: corners
30 across and 20 down at 30 degrees make a box 15.98 wide; line spacing carries to the next MTEXT until Height or Style
is chosen. MIRROR keeps a dimension in the drawing's plane and readable under either MIRRTEXT.

**FIXED.** The presenter's MIRROR leaves texts and multileaders as AutoCAD 2021's MIRROR leaves them, checked group by
group in its console. A multileader keeps the drawing's plane (297 0) under either MIRRTEXT; at 0 its words keep reading
along x from the mirrored landing, held by their other side across an upright (171 and 176 turned, bit 14 of 90), the base
point at the left of the words less the gap; at 1 they are mirrored and write 294 1. A copy of a multileader of a block is
based at the block's left and writes its matrix where it stands. At MIRRTEXT 1 a TEXT, an attribute definition and the
attributes of an INSERT keep the normal (0, 0, 1) and are written backwards (71 2), not turned over. At 0 a TEXT is
mirrored by the ink of its words where the fonts read them: "ABC" of txt.shx 2.5 high mirrored across x = 100 starts at
193.333, not 195.5, and its turn is written from 0 to 360 degrees.

**NEW.** The presenter's Properties rows of a dimension add Ext line fixed and its length, Fill color, Fractional type,
Text position X and Y and Text view direction, written as overrides; extension lines follow DIMFXLON as AutoCAD 2021
draws them. MATCHPROP copies the plot style in a drawing of named plot styles (`CadMatchProperties.PlotStyle`) and a
hatch's gradient.

**FIXED.** A hatch's origin is kept through Angle, Scale, Pattern name and MATCHPROP. DIMBREAK's gap where the style
keeps no size is AutoCAD's 0.125, or 3.75 in a metric drawing, not 1.5 arrowheads.

**FIXED.** Point modifiers of the presenter checked against AutoCAD 2021: TK's `Next point  (Press ENTER to end
tracking):` and its upright tie, (100, 0) towards (130, 30) reaching (100, 30); `40,3` at `(need YZ):` gives Y 3, not
40; Enter and text at FROM, M2P, TT, TK and filter prompts answered as AutoCAD answers them; TT and TK no longer nest in
another modifier, nor FROM in TK.

**NEW.** POLARMODE 1 (polar angles from the last segment) and 4 with POLARADDANG (up to ten additional angles, kept in
the settings file); `CadUnits.TryAngles`, `CadSystemVariable.Text(name, get, take)`,
`ObjectSnap.PolarFromLastSegment`/`LastSegment`/`AdditionalPolarAngles`, `CadCommandSession.LastSegment`,
`GlobalSettings.PolarAddedAngles`.

**NEW.** The presenter opens and saves DWG as AutoCAD 2021 does. Ctrl+O shows Select File with Drawing (*.dwg) first and
DXF (*.dxf) after it, a DWG dropped on the window opens, and the file list lists the DWGs of the folder; a file is read as
a DWG of AutoCAD 2000 to 2018 by `DwgReader` where its first bytes say so, and one of a version not read, R14 among them,
is said on the command line, not opened as a recovered DXF. Save As shows Save Drawing As with AutoCAD 2018 Drawing
(*.dwg) first and the DXF of the drawing's own version after it, the type of its file chosen first, and QSAVE of a
drawing opened from a DWG writes it back as a DWG of AutoCAD 2018 (`DwgWriter`); what that writer does not write yet is
said after the save, "Left out of the DWG, not written yet: RASTERVARIABLES 1." A DWG is attached as an external
reference (XATTACH, -XREF, the palette) and inserted (INSERT, -INSERT, the Blocks palette) as a DXF is, and WBLOCK writes
a DWG where the file ends in .dwg. **NEW.** FILEDIA 0 asks on the command line for the file QSAVE of a drawing with no
file ("Save drawing as <Drawing1.dxf>:"), XATTACH ("Enter name of file to attach:", then the Attach External Reference
dialog with it), IMAGEATTACH, and the Browse… of the Blocks palette and the Write Block dialog would ask a file dialog
for; a name typed at "Save drawing as" with no extension takes the offered one's. **NEW.** `CadDrawingFile` (`OpenFilter`,
`IsDrawingName`, `IsDwg`, `Open`), `CadDocument.LeftOutOfDwg`, `MainViewModel.CreateSaveAsJob`, `CadExportJob.Dwg`,
`CadDialogAction.Browse`, `CadInsertRequest.FileDialogs`, `CadWriteBlockRequest.FileDialogs` and `BrowsesOnCommandLine` of
the Blocks palette and Write Block view models. **CHANGED.** `CadDocument.Open` reads a DWG, and `CadDocument.SaveAs`
writes a DWG for a name ending in .dwg, where both read and wrote DXF only.

**FIXED.** The External References palette of the presenter said "Needs reloading" where AutoCAD 2021's help of the
palette's List View says "Needs Reloading". Its other words were checked against that page and stand: Loaded, Unloaded,
Not Found, Unresolved, Orphaned and Unreferenced for the status, the current drawing listed first, and the types of a
drawing attachments or overlays. The page says nothing of a type of a nested reference, so Nested is kept.

**FIXED.** The presenter drew a layer of an external reference the drawing had not changed as the file has it now under
VISRETAIN 1, and synced it on reload. AutoCAD 2021 does not: with VISRETAINMODE 0, its default, it syncs nothing, and its
console kept `x|WALL` green (62 3, 1071 0) when x.dwg made WALL blue, reloaded and opened anew alike. The drawing's
record is now drawn whole. **NEW.** VISRETAIN 0 as AutoCAD 2021 keeps it: what the drawing changed is drawn as it keeps it
until Reload, which writes the file's properties back with 1071 0 (its reload wrote `x|WALL` red back as the file's
blue, 62 5), the rest as the file has it; Unload takes the records out, as its console showed. The presenter does not
write them again when it opens a drawing of VISRETAIN 0.

**NEW.** -LAYER in the presenter takes TRansparency, Plot and Description, asked as AutoCAD 2021 asks them: "Enter
transparency value (0-90):" (95 refused with "Requires an integer between 0 and 90." and asked again), then "Enter name
list of layer(s) for transparency 50% <0>:", Enter taking the current layer; "Enter a plotting preference [Plot/No plot]
<Plot>:"; "Enter layer description:". On a layer of an external reference each is marked in the 1071 of
`AcXRefLayerPropertyOverride` as AutoCAD 2021 marked `b|WALL`: 128, 8 and 1024. 50 per cent is the alpha 127. The Layer
manager has no column for them, and none was added. **NEW.** `CadLayerEditor.SetTransparency`, `SetPlottable` and
`SetDescription`.

**CHANGED.** The presenter's XATTACH and -XREF Attach and Overlay write into the drawing the records AutoCAD 2021 writes
for a drawing it attaches, as its -XREF Attach of x.dwg, its layer WALL green on DASHED, wrote them: the linetype
`x|DASHED` with its dashes and the layer `x|WALL` on it, green, with the file's lineweight, plotting, transparency and
description and `1001 AcXRefLayerPropertyOverride, 1071 0`, each flagged 48 (dependent, resolved), and the block of the
reference 36. Nothing of the file was written before, so a drawing saved by the presenter listed no layer of a reference
in the Layer manager until AutoCAD had saved it. Unload flags them 16 and the block 4, Reload 48 and 36 again with the
records of layers the file has gained, a record the drawing has keeping what it has; each is an undo step where anything
changes, and Detach takes them out. **NEW.** `DxfLayerTableRecord.SetDependent` and `DxfLinetypeTableRecord.SetDependent`
mark a record of an external reference (flags 16 and 32), so that its table takes a name with a | in it.

**NEW.** `DwgWriter.Write` writes a DWG file of AutoCAD 2018 (AC1032), the library's own code as the reader is: a
`DxfDatabase` with every record it holds, or the shapes of a `DxfDocument`, to a path or a stream. The drawing is written
as its DXF and each record put into the object of the DWG that says the same, with the drawing's handles: the header
variables, the classes, the tables, the blocks, LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE,
POLYLINE with its vertices, the meshes, TEXT, MTEXT, ATTDEF, INSERT with its ATTRIBs of one line, SOLID, TRACE, 3DFACE,
DIMENSION, VIEWPORT, HATCH, LEADER, TOLERANCE, MLINE, IMAGE, WIPEOUT, and DICTIONARY, XRECORD, LAYOUT, GROUP, MLINESTYLE,
IMAGEDEF, DBCOLOR and the small objects of a new drawing, in the compressed container of pages AutoCAD 2004 and later write, every check sum in
place, the same bytes for the same drawing. A type not written yet (MATERIAL, VISUALSTYLE, MULTILEADER, ACAD_TABLE, REGION,
3DSOLID among them) is left out, every handle to it written as 0, and counted in one Warn line. Nineteen test drawings,
each as AutoCAD 2021 wrote its DXF, written as DWG: AutoCAD 2021 opens each and AUDIT finds 0 errors, its own DXF of
each is the source but the types left out, and `DwgReader` reads each back to the source records. A drawing of a few
hundred records takes 0.1 s and some 10 KB, where AutoCAD's own file is 13 to 17 KB, and 100 000 lines 1.1 s. See
[Reading and writing DWG files](src/GeometryHelper/docs/dxf/dwg.md).

**CHANGED.** REVCLOUD's Arc length in the presenter asks AutoCAD 2021's one approximate length, `Specify approximate
length of arc <15.0000>:`, in the place of a minimum and a maximum, and SETVAR sets REVCLOUDAPPROXARCLEN,
REVCLOUDARCVARIANCE and REVCLOUDCREATEMODE, kept by the session. At REVCLOUDARCVARIANCE 1, as AutoCAD starts, each arc is
drawn between two thirds of the length and four thirds, `Minimum arc length: 4.0000   Maximum arc length: 8.0000` for 6;
at 0 each is the length, which draws the clouds checked against AutoCAD's console before. The length is no longer scaled
by DIMSCALE, as AutoCAD 2021 does not scale it: 2021 has no REVCLOUDSCALEMODE, which later releases added to scale it
again. AutoCAD 2021 sizes the first cloud of a drawing by a share of the view's diagonal its help does not give; the
presenter still starts at 15 in millimetres and 0.5 in inches. AutoCAD's console runs only the REVCLOUD of earlier
releases, so this follows AutoCAD 2021's help and variables, read in its console.

**NEW.** PEDIT in the presenter edits a 3D polyline with AutoCAD 2021's options for one, checked in its console: Close and
Open, Join, Edit vertex (Next, Previous, Break, Insert, Move, Regen, Straighten), Spline curve, Decurve, Reverse and Undo,
in AutoCAD's words. Spline curve of the 3DPOLY (0, 0, 0), (100, 0, 10), (100, 100, 20), (0, 100, 30) stores the 13
vertices AutoCAD stores, the curve through (75, 50, 15), and closed, reversed and decurved it is AutoCAD's to 1E-8; a
vertex moved on the spline fits it again, the curve through (47.168, 18.652, 0.879) as in AutoCAD. Join adds nothing, as
AutoCAD 2021's console added nothing to a 3D polyline. PEDIT said before that 3D polylines were not edited yet. Multiple
still leaves them out.

**NEW.** PEDIT Multiple in the presenter joins by Extend, and by Both, at the end of an arc as AutoCAD 2021 does, checked
in its console: the arc is run on or cut back round its circle to where the other end segment, a line or an arc, meets
it, and the other is run on or cut back to there too. Of two crossings the one nearer the two ends together is taken, as
AutoCAD took it where the one nearer the arc's end lay elsewhere. The arc about (0, 0) of radius 100 from 0 to 80 degrees
and the line from (10, 130) to (10, 200), joined at a fuzz of 40, are one polyline through (10, 99.4987), the arc's bulge
0.38517039, to 1E-10 of AutoCAD's. Extend refused any arc before; Both added a segment there.

**FIXED.** PEDIT Multiple's Join by Extend in the presenter takes two ends whose meeting point lies within the fuzz
distance of both, as AutoCAD 2021 does, not two ends within the fuzz of each other: the ends (100, 0) and (130, 30), 42.4
apart, run on to (130, 0) at a fuzz of 35, and the ends (100, 0) and (110, 5), whose lines meet far off, are not joined
by Extend; Both adds a segment between them.

**NEW.** SETVAR in the presenter sets PEDITACCEPT and PLINETYPE, kept by the session, and PEDIT, PLINE, POLYGON and
DONUT honour them as AutoCAD 2021 does, checked in its console. PEDITACCEPT 1 turns a line or an arc picked into a
polyline without `Do you want to turn it into one? <Y>`, and Multiple without asking to convert. PLINETYPE 0 makes PLINE,
POLYGON, DONUT and PEDIT's lines and arcs old-style POLYLINEs, PLINE from (0, 100) to (100, 100) and, 2 to 4 wide, to
(100, 200) a POLYLINE whose second vertex has widths 2 and 4, and keeps a POLYLINE edited alone one; at 1, as at 2, PEDIT
makes it a LWPOLYLINE of its handle again. RECTANG and REVCLOUD make LWPOLYLINEs at 0 too, as AutoCAD's do. AutoCAD names
the first PEDITACCEPT; it has no PEACCEPT.

**FIXED.** A [`DxfViewport`](xref:GeometryHelper.Dxf.DxfViewport) made in code and appended to a layout has the layers
frozen in new viewports (`ViewportVisibilityDefault`, New VP Freeze) frozen in it at once, as AutoCAD 2021's
`AppendEntity` freezes them: its API, given layer GHB so marked, made a viewport with [GHB] frozen at birth, and left the
layout's own viewport 1 with none. Only the presenter's MVIEW froze them before. A copy keeps its original's.

**NEW.** "Isolate Selected Layers" in the shortcut menu of the presenter's Layer Properties Manager turns off every layer
but those chosen, one undo step: of the layers 0, WALLS and COLUMNS, COLUMNS chosen leaves 0 and WALLS off. The item was
grey. "Rename Layer" stays grey, since renaming in the list needs a name cell that can be typed in, which waits for a
design.

**NEW.** -SCALELISTEDIT in the presenter edits the drawing's list of scales (ACAD_SCALELIST), its prompts and answers
word for word as AutoCAD 2021's console gives them: ? lists the scales with their units, Add asks a name and a ratio and
redefines a scale of that name when told Yes, Delete takes out one scale or `*` every one in no use and says "Scale 1:1 is
referenced so it cannot be deleted." for one AutoCAD keeps, Reset puts back the standard 1:1 to 100:1, Exit ends; the
command is one undo step. The status bar's scale list in a layout offers the drawing's list, as AutoCAD's does, and its
Custom... runs -SCALELISTEDIT in place of VPSCALE. The Edit Drawing Scales dialog of SCALELISTEDIT waits for a design.
[`DxfObjectContextCollection.CanRemoveContext`](xref:GeometryHelper.Dxf.DxfObjectContextCollection.CanRemoveContext(System.String))
says whether `RemoveContext` would take a scale out.

**CHANGED.** ZOOM Previous in the presenter goes back through a list of its own for each viewport of a layout, for the
paper of each layout and for model space, 20 views each, as AutoCAD 2021 keeps them. It went back through one list of
every view the drawing's ZOOM and PAN had left, whichever viewport or space they changed, and a viewport's views were kept
as boxes. AutoCAD's console, after 12 zooms in one viewport, answers "No previous view saved." in another, and after a
zoom there gives the first its 12 back in order; zoomed 30 times, a viewport gives 20 back, then "No previous view
saved."; the paper's list is apart.

**CHANGED.** The wheel's rolls and the middle button's drags in a viewport worked through are one undo step together in
the presenter, as AutoCAD 2021 combines consecutive real-time zooms and pans by default (UNDO Control Combine: UNDOCTL is
61 in its console, its 16 set). A roll or a drag held still long enough for the view to be drawn again made a step of
its own, so one drag could leave several. ZOOM and PAN typed stay a step each, as AutoCAD's console undoes ZOOM 2X, ZOOM
2X and -PAN one U each. `CadDocument.SetViewportView` takes `realTime` to join a change to the real-time one before it.

**FIXED.** A layout viewport whose view is twisted, or looks at a target off the origin, is drawn in the presenter where
AutoCAD 2021 draws it, and a twisted one can be worked through. The presenter turned the view the wrong way and took the
view's centre as a point of model space, where AutoCAD keeps it about the target and turned by the twist: AutoCAD's
console, asked TRANS from the world to the view and on to the paper in a viewport 120 by 80 about (100, 100) at 1:5, its
view about (100, 50) twisted 30 degrees, puts (100, 0) at (97.320, 100) on the paper, and in one about (250, 100) looking
at (10, 20) puts the origin at (248, 96); the presenter now does too. MSPACE and a double click enter a twisted viewport,
which they refused: the canvas's view in model units turns with it (`CadView.Twist`), so picks, snaps, boxes, the
crosshair and the UCS icon follow the turn, PAN moves by the model distance given and ZOOM Window fits the box turned.
`CadViewports.ToView` and `FromView` give the view's coordinates of a point and back.

**NEW.** The presenter's window says on a drawing's command line when a file one of its external references was read
from changes on disk, where AutoCAD 2021 shows a balloon over its status bar (XREFNOTIFY 2): "External Reference Files
Have Changed: grid needs reloading. Reload it with -XREF Reload or in the External References palette." Each change is
said once, and again only after the reference was reloaded and its file changed anew. The files were watched only while
the External References palette was open, to refresh it. **NEW.** `MainViewModel.WatchReferenceFiles`, which the
workspace sets.

**NEW.** The External References palette of the presenter lists nested external references as AutoCAD 2021's does: a
reference the drawing places is followed by those its drawing attaches, of the type Nested in the list view, under their
parent and of their own type in the tree view, each level 16 pixels further in. A nested reference whose parent is
unloaded, not found or unresolved stands Orphaned; nested overlays are left out, as AutoCAD loads none. The record
AutoCAD writes in the host for a nested reference (`c` beside `b`, which no INSERT of the host places, as its console
showed for a host attaching `b.dwg` that attaches `c.dwg`) was listed as Unreferenced; it is listed under its parent alone.
**NEW.** `CadExternalReference.Depth`, `ParentName`, `IsNested` and `CadReferenceStatus.Orphaned`; `--dialog xref` and
`xref-tree` draw the palette in screenshot mode. **FIXED.** The palette's names were drawn in the system's black on its
dark rows; they are drawn in the text colour.

**NEW.** XOPEN in the presenter opens the drawing of an external reference picked in a tab of its own, as AutoCAD 2021
opens it in a window of its own. It asks "Select Xref:"; a line picked is answered "Object is not an Xref" and the
question asked again, and Enter ends it saying nothing, as AutoCAD 2021's console answers them. A pick on what a nested
reference draws opens the reference the drawing places, and one whose file is gone is refused as -XREF Reload refuses
it. The registry has 177 commands.

**FIXED.** A layer of an external reference the drawing keeps a record of (`grid|GRID`, as AutoCAD writes it) is drawn
as AutoCAD 2021 draws it under VISRETAIN 1: what the drawing changed of it as the drawing keeps it, the rest as the file
referenced has it now. The presenter drew the record whole, so a layer the file made blue after AutoCAD saved the drawing
stayed green. AutoCAD, probed in its console with -LAYER on `b|WALL` and the drawing written out, marks each change in
the 1071 of the record's `AcXRefLayerPropertyOverride`: 1 Off, 2 Freeze, 4 Lock, 8 Plot, 16 and 256 Color, 32 Ltype, 64
LWeight, 128 TRansparency, 1024 Description, 0 for a layer left as it was. The Layer manager, the layer box and -LAYER
now change such a layer and mark it so, and a change of it in the drawing draws its INSERT anew, which it did not.
**CHANGED.** The current layer refuses a layer of an external reference with "Cannot set CLAYER to that value.", and
-LAYER Set with "Invalid layer name.", as AutoCAD 2021 refuses them; a new layer named with a character AutoCAD refuses,
`grid|X` among them, is refused with `"grid|X" is an invalid layer name.`

**NEW.** Associative dimensions in the presenter tie the PERpendicular and TANgent snaps at a first origin, on polylines,
and in angles of three points and ordinates, as AutoCAD 2021 ties them. At a first extension line origin, an angle's
vertex and an ordinate's feature, the two snaps are drawn from the last point given (LASTPOINT), which the new
`CadPrompt.SnapFrom` hands the window's snap through `ObjectSnap.Snap`; the ends of an angle of three points are drawn
from its vertex and tied with the vertex's own tie as their last point. A polyline's segment is tied by its marker (91, or
92 for the second object of an intersection), a point on its arc by its angle counted on from the arc's start: AutoCAD
2021, probed in its console, wrote the end of a half circle from (100, 200) to (100, 300) as marker 2 and 7.85398 and its
center as 4.71239, and a line drawn across the third segment as 332 the polyline and 92 = 3. A moved object takes its
points where AutoCAD takes them: a tangent drawn again from its point; a perpendicular drawn again where its point is tied
to an object, else kept at its parameter, or, past the end of its line, at the foot from the origin of the world, so a
first origin drawn from (37, 123) square to (100, -20)-(100, 60) goes to (120, 0) when the line moves by (20, 7). The
dimension line of a linear dimension stays as far beyond the nearer origin as it was, 50 for an end of the line dragged
from (100, 0) to (120, 30) with the line 20 above. A drawing of 16 dimensions so tied, made here and opened in AutoCAD
2021, has no errors in AUDIT, and after the same moves and stretches in both every tied point is where AutoCAD puts it on
its own drawing, to 1E-3; AutoCAD's file read here follows the same way.

**FIXED.** STRETCH in the presenter moves the origins and the line point of a linear dimension that lie inside its
window, and keeps the height of a polyline's arc off its chord when one end moves, as AutoCAD 2021 does. It left the
dimension to its tie, and kept the arc's bulge: the half circle from (100, 200) to (100, 300) whose end goes to
(100, 320) now takes a bulge of 100 / 120, its center at (89, 260) where AutoCAD puts it, not (100, 260).

**CHANGED.** Esc in the presenter's MTEXT editor with changes asks as AutoCAD 2021 asks: Multiline Text - Unsaved
Changes, "Do you want to save your text changes?", with Yes, No and Cancel and Always perform my current choice, in the
place of the presenter's own "Save changes to MText?". Yes writes the changes and closes the editor, No drops them, Cancel
goes back to editing, and with nothing changed Esc closes with no question. A Yes or No given with the box ticked is kept
in Global settings as `MTextUnsavedChanges` ("Ask", "Yes" or "No"), as AutoCAD keeps it among its hidden messages, and
Text and fonts › Hidden messages sets it back to Ask; Cancel is never kept. `MainViewModel.AskKeepMTextChanges` now takes
the question, an `MTextUnsavedChangesViewModel`, and `CadQuestionWindow.AskKeepMTextChanges` is gone. The screenshot mode
draws the dialog with `--dialog mtext-unsaved`. Esc in the edit box of a single-line text already kept the old words, as
AutoCAD does, and a test now holds it.

**FIXED.** DIMBREAK in the presenter keeps its breaks as AutoCAD 2021 keeps them, so a dimension drawn anew is broken
again. It cut the lines of the dimension's picture only, and DIMEDIT, a change of style or a grip drew the dimension
whole again. AutoCAD, probed in its console, keeps a BREAKDATA under ACAD_BREAKDATA in the dimension's extension
dictionary: one AcDbBreakPointRef for each object Auto finds crossing (71 0, the object in 331, the object naming the
BREAKDATA among its reactors), one for each break by hand (71 2, the two points picked, in 91 the line: 0 and 1 the
extension lines, 3 the dimension line), and Remove takes it out with the extension dictionary. The presenter writes the
same, and breaks a dimension it draws anew from what it keeps, whether it broke the dimension or AutoCAD did; AutoCAD 2021
opening the drawing reads it, finds no errors in AUDIT, and breaks the dimensions again when it draws them anew.

**CHANGED.** A click in the presenter picks the object drawn on top among all those within the pickbox, as AutoCAD 2021
does, not only among those within a tenth of the pickbox of the nearest. AutoCAD's `(ssget pt)` on two level lines, one
through the point and one drawn later 0.05 to 0.9 of the pickbox above it, picks the later one every time, and 1.1 of
the pickbox above, outside the box, the other; sent to the back by DRAWORDER, the later one loses. Under SORTENTS without
its bit 1 AutoCAD picks the one drawn first, however much nearer the other lies, and so does the presenter, which went by
distance and handle. `PickIndex.Pick` gives its handles in that order.

**FIXED.** DIMEDIT Oblique in the presenter keeps the angle as AutoCAD 2021 keeps it, from the dimension line. It kept the
angle typed, from the x axis, in group 52, where AutoCAD's Oblique is that angle less the line's: AutoCAD 2021, slanting
three dimensions to 60, gives 60 to a level DIMLINEAR, 30 to one rotated 30 and 33.435 to a DIMALIGNED from (0, 100) to
(100, 150), and draws the extension lines of all three at 60. The presenter now writes the same, so a dimension slanted
in it reads the same in other programs, and one slanted elsewhere draws at its angle. The documentation of
[`DxfRotatedDimension.Oblique`](xref:GeometryHelper.Dxf.DxfRotatedDimension.Oblique) and
[`DxfAlignedDimension.Oblique`](xref:GeometryHelper.Dxf.DxfAlignedDimension.Oblique) said the angle ran from the square
of the dimension line; it runs from the line, nought standing for none.

**CHANGED.** The presenter's window opens a drawing in the background. The file is read and its display list and pick
index made on a worker thread, and the drawing is shown once they are made; meanwhile the status bar says "Opening
plan.dxf: drawing 45 %" with a Stop button, the view shown pans and zooms, another drawing open is shown, and Esc or Stop
stops the opening. A command typed or started meanwhile is not run, the command line saying why, so that none runs on a
drawing half drawn. A drawing of 1 000 000 entities that held the window for 15 s while it opened and drew its first
frame now holds it for 0.2 s at most while it opens, then 0.8 s for that frame; the display list is the one it was, item
for item. **NEW.** `MainViewModel.OpenAsync`, `OpensInBackground`, `IsOpening`, `OpeningText`, `CancelOpening` and
`CancelOpeningCommand`, and `CadDocument.PrepareDisplay`, which makes a space's display ahead, telling how far it has got
and stopping when asked. `Open` still opens a drawing before it returns unless `OpensInBackground` is set, as a test or a
screenshot needs.

**CHANGED.** The presenter draws the highlight of a large selection on several threads. Where several thousand items are
selected, the canvas is cut into tiles, a thread a tile, and an item reaching into several tiles is drawn once every one
of their threads has drawn what comes before it; a text, a hatch, a ray or a picture is drawn with the other threads
waiting. The picture is pixel for pixel the one a single thread draws: a test compares the two on a drawing of 100 000
entities, and the pictures of that drawing and of two sample drawings, at three zooms, plain, selected and hovered, with
lineweights on and off, hash as before. All of that drawing selected is highlighted in about 150 ms rather than 300 on
24 processors under .NET Framework 4.8, and with the whole workspace drawn, its canvas 1 000 by 696 pixels, the canvas
draws the selection in about 240 ms rather than 360. A selection crammed into a patch of a few hundred pixels gains
nothing.

**NEW.** The snap and grid a drawing keeps. `db.ViewportTable` holds the tiled viewports of model space as AutoCAD's
`ViewportTable`, each a [`DxfViewportTableRecord`](xref:GeometryHelper.Dxf.DxfViewportTableRecord), and
`db.ViewportTable["*Active"]` gives SNAPMODE, GRIDMODE, SNAPUNIT, GRIDUNIT, SNAPBASE, SNAPANG, SNAPSTYL, SNAPISOPAIR,
GRIDDISPLAY and GRIDMAJOR as `SnapEnabled`, `GridEnabled`, `SnapIncrements`, `GridIncrements`, `SnapBase`, `SnapAngle`,
`IsometricSnapEnabled`, `SnapPair`, `GridDisplay` and `GridMajor`, read from the groups 75, 76, 14 and 24, 15 and 25, 13
and 23, 50, 77, 78, 60 and 61, as AutoCAD 2021 opened a drawing whose `*Active` has a snap of 2.5 by 5 and a grid of 10
by 20 with SNAPUNIT (2.5, 5) and GRIDUNIT (10, 20). Each is set in place and kept by a save, bit for bit.

**CHANGED.** The presenter's SNAP, GRID, ORTHO and lineweights follow each drawing, as AutoCAD 2021's do. A drawing
opened shows SNAP and GRID with their spacing from its `*Active` viewport, ORTHO from `$ORTHOMODE` and its lineweights
from `$LWDISPLAY`, and its tab shows them again when it is shown; a drawing with none of them takes the window's, as
before: SNAP and ORTHO off, GRID and the lineweights of Global settings, a spacing of 10. Switching one, or setting the
snap spacing, writes it into the drawing shown, so that a save keeps it; LWDISPLAY set with a drawing open no longer
changes Global settings. The snaps step by the drawing's SNAPUNIT and the grid is drawn at its GRIDUNIT, 10 across and
20 up for a GRIDUNIT of (10, 20). POLAR, OTRACK, OSNAP and DYN stay the window's.

**NEW.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader) reads the types it left out, in all six versions (AutoCAD
2000 to 2018): VIEWPORT, so a layout comes with its viewports, numbered as AutoCAD numbers them; HATCH with every kind
of boundary, pattern lines, solid fill and gradient; LEADER, MULTILEADER with MLEADERSTYLE, and TOLERANCE; IMAGE with
IMAGEDEF and IMAGEDEF_REACTOR, and WIPEOUT; MLINE; REGION, 3DSOLID and BODY of AutoCAD 2000 (their SAT text) and from
AutoCAD 2013 (the GUID of their data); ACAD_TABLE to AutoCAD 2007 and TABLESTYLE to 2007; the colour of a book with its
name, from its DBCOLOR; an attribute of several lines of AutoCAD 2018 with its MTEXT; and the proxy graphics of an
entity where AutoCAD saved them. Each was checked against AutoCAD 2021's own DXF of the same drawing in every version:
eight drawings more, 0 differences outside what AutoCAD itself changes as it writes. See the guide page "Reading DWG
files" for what is still left out: tables and table styles from AutoCAD 2010, SHAPE, the ACIS data of AutoCAD 2004 to
2010 and of the data store, MATERIAL, VISUALSTYLE and the other style objects, proxies, OLE frames and the data of
dynamic blocks.

**FIXED.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader), from AutoCAD 2004: an entity of a colour of a book (as
PANTONE Yellow 012 C) was left out as broken, with a Warn line; it is read with that colour. An element of a multiline
style before AutoCAD 2018 in Continuous, or any linetype but ByLayer and ByBlock, came out ByLayer; it comes out with
its linetype.

**FIXED.** `DxfViewport.GetFrozenLayers` and `IsLayerFrozenInViewport` read the frozen layers of a DXF of AutoCAD 2000,
which AutoCAD writes under group 341 rather than 331; they found none.

**FIXED.** The plane the DXF editing model finds for a 3D polyline or a spline no longer throws on points it cannot
place. Points a nanometre off one line, as (0, 0, 0), (1 000, 0, 0) and (2 000, 1E-9, 0), or a normal given of length
1E-7, threw "Cannot normalize a zero-length vector" out of `DxfPolyline3d.GetOffsetCurves`, the offsets of
[`DxfSpline`](xref:GeometryHelper.Dxf.DxfSpline), JOIN and the DWG reader; they now lie in their plane, (0, 0, 1) for those
points. Points that give no plane at all (none, one, all within the point tolerance of the first, any not finite, or
so far apart that the distance between them is past the largest double) give none: a curve of them is offset as one
that lies in no plane, and the DWG reader still leaves out a spline of points 1E+233 apart as a broken object, now by
its own check rather than by the throw.

**FIXED.** `Try` methods that threw on shapes the library accepts now answer. A reflection fuzz of every public `Try`
method, 500 000 calls with ordinary and degenerate shapes, found these:

- A box with a side of nought, as the bounding box of anything flat, threw "A box with a zero size on some axis has no
  faces to build" out of `TryIntersectWith` of an arc, a circle, an ellipse, a triangle, a polygon, a face and a plane,
  out of `TrySplitBy` of a curved chain or loop, and out of the booleans of a box, `GeoObb3.TryUnion`, `TryIntersect`
  and `TrySubtract`, and of a body with a box; an empty `GeoAabb3` threw too. An arc, a circle or an ellipse now meets a
  box flat on one axis where it passes the rectangle across it, the arc upright through (50, 0, 0) and (50, 100, 0) the
  box 100 by 100 by 0 at those two points, and a box flat on two axes or three nowhere; a triangle, a polygon, a face or
  a plane against a box flat or empty answers false, and so does a boolean with a flat box, with no result.
- Two ellipses, one a circle of radius 0.0001 standing upright in the plane of the other within the planar tolerance,
  threw "The major axis needs a direction"; the small one is now laid out by its minor axis.
- `GeoFace2.TrySplitBy` of a face whose boundary or a hole encloses no area at the global tolerance threw; it is not cut,
  and comes back whole on the left.
- `TryChamferAt` of a polygon, 2D or 3D, threw where the cut left fewer than three corners, the triangle (0, 0), (100, 0),
  (0, 100) cut 100 back each way from the origin; it is false, and the polygon stays.

Everything a `Try` method answered before it answers bit for bit the same.

**FIXED.** A spline against a curve far smaller or far larger than itself is answered in milliseconds. The same fuzz found
21 calls of `TryIntersectWith` between a `GeoSpline2` or `GeoSpline3` and a circle, an arc, an ellipse, an edge or
another spline still working after ten seconds, either way round; each now takes under 5 ms, and its places are those
dense sampling of the two curves finds:

- A piece of a curve was looked at all along for a stretch lying along the other, every sixty-fourth of the size of the
  smaller curve: a piece of the circle of radius a million, 1.5 million long, every 0.31 against an ellipse of radius 10.
  Only the share of it near the other curve is looked at now. The ellipse of radii 10 and 5 about (1 000 000, 0) crosses
  that circle at (999 999.9999875, 5) and (999 999.9999875, -5).
- A curve no larger than the point tolerance, as a circle of radius 1E-4 or 1E-12 at the tolerance 0.001, was looked for
  as such a stretch, the other curve every 3E-14 of its length for the circle of 1E-12. It lies along nothing, and is
  not looked for one: the polyline (0, 0), (100, 0), (0, 100) meets the spline of either circle about (100, 0) once.
- A part of such a curve that is not straight was cut until straight against each part of the other curve near it: a
  line through a circle of radius 1E-4 measured 2 048 parts of it, a cubic through it twenty times took 7.9 s. It is
  measured as it is against a straight part, and the other curve is cut instead.
- A curve through a circle of radius 1E-12 a hundred from the origin, where the numbers are rounded to 1E-11, was read
  as running along it at every meeting and met it nowhere; it meets it once.
- An ellipse of radius 1E-12 standing upright in the plane of a `GeoSpline3`, in it only within the planar tolerance,
  threw "The major axis needs a direction", as the fuzz found once the calls before it were answered; it is laid out by
  its minor axis, and a curve through it meets it once.

The places of other splines are bit for bit the same, and found a fifth faster: 6 528 places among 40 splines and 120
circles, ellipses, lines and arcs take 1.51 s, where they took 1.89.

**FIXED.** The presenter's typed lengths and angles: a number past the largest double, as 400 nines in `999…9'`,
`1-999…9/2` or `999…9d`, threw an `OverflowException` out of the prompt in .NET Framework, and `NaNg`, `NaNr` and
`Infinityr` were read as angles of NaN and infinity. Each is now not read.

**FIXED.** A radius dimension made in the presenter with its words put inside the circle under DIMTMOVE 1 or 2 is drawn
as AutoCAD 2021 draws it. Under 1 its words stay where DIMATFIT 3 keeps them whatever DIMATFIT says: "R12" put 11 out
under DIMATFIT 0 stands 3.4 out, not in the middle of the line. Under 2, words on the line over the centre or the rim put
the arrowhead outside the rim, pointing in, and the line runs whole from the centre to the rim: "R20" put 1 out. Put 8 out
they leave the arrowhead inside and the line broken about them, as before. Of 212 pictures of radii from 3 to 26 with
their words put 0.5 to 11 out, 212 match AutoCAD's where 30 of the first 120 did; round 5's radius-3 cases now match too
(154 of 192 there, from 140). The diameters of that round are left as they were.

**FIXED.** DIMTEDIT Left and Right in the presenter put a linear dimension's words where AutoCAD 2021 puts them: an
arrowhead and a gap in from the extension line, then the words with a gap about them, where they stood two arrowheads
in. "100" of Arial on a line at 30 degrees stands 6.733 in from its end, not 7.983. The left of a vertical line is its top,
as AutoCAD takes it, and under DIMTIH the words stand level, centred on the slanted line, which is broken about them.
Of 336 pictures of aligned dimensions at six angles, Left and Right under DIMTAD 0 and 1 and DIMTIH 0 and 1, and words
moved out under DIMTOH 0 and 1, 336 match AutoCAD's where 264 did; DIMTOH already matched.

**NEW.** The presenter has DIMJOGGED (DJO, JOG), AutoCAD 2021's jogged radius, with its prompts word for word ("Select
arc or circle:", "Specify center location override:", "Specify dimension line location or [Mtext/Text/Angle]:", "Specify
jog location:", and "Object selected is not a circle or arc." for anything else). Its line runs from the centre drawn
along the radius's parallel, across at DIMJOGANG and on to the arrowhead at the rim, the jog half way between the two
lines: a circle of 50 about the origin, its centre drawn at (0, -100), its words at (30, 0) and its jog at (15, -50) has its
lines from (0, -100) to (65, -100), on to (-35, 0) and on to (47.5, 0). Words put on the other side of the rim from the
foot of the centre drawn go three quarters of the way from that centre to the rim, as AutoCAD puts them, which is the
radius at -23.6 degrees round 5 could not explain. Its picture is drawn again when its style changes, and its grips are
its chord point, the centre drawn, its jog (the new `CadGripKind.Jog`) and its words. Of 152 pictures drawn by AutoCAD,
varying the words' place, the centre drawn, the jog and DIMJOGANG, 145 match; the seven left have a jog given past the
rim or far off its middle line, DIMJOGANG 90, or words on the line pushed past the arrowhead.

**FIXED.** `DxfRadialDimensionLarge` read and wrote its points under the wrong groups. AutoCAD 2021 writes the centre
drawn as group 13, the jog as 14 and the chord point as 15, where the library took 13 for the chord point, 14 for the
centre drawn and 15 for the jog, as ezdxf 1.4.5 does; a jogged radius AutoCAD made was read with its radius measured to
its jog. `ChordPoint`, `OverrideCenter` and `JogPoint` now read and write AutoCAD's groups. AutoCAD writes nought for
`JogAngle` (group 40) and draws the jog at the dimension's DIMJOGANG.

**FIXED.** The presenter measures a dimension's words with the MTEXT layout the canvas draws them with, the letters by
their ink, and fits them as AutoCAD 2021 does. Words in the canvas's own ghsimplex were taken as 0.7 of their height a
letter; "100" of ghsimplex widened 1.5 times is now 9.107 wide, as AutoCAD lays it out. Words on a linear dimension's
line (DIMTAD 0) need two gaps more each side to stand between the arrowheads, so "9" of ghsimplex, 1.548 wide, moves out
of a dimension of 9; under DIMATFIT 3 words wider than the arrowheads' room stay inside and the arrowheads go out; and
under DIMATFIT 0 to 2 a radius's words go where the fit puts them however far out its line was put, "R20" put 35 out
standing 8.75 from the centre. Of 512 new pictures in ghsimplex, in Arial narrowed and slanted and in ghsimplex widened,
499 match AutoCAD's where 425 did; the 1 522 of earlier rounds match as before (1 443).

**CHANGED.** The presenter's status bar starts with SNAP off and POLAR on, as AutoCAD 2021 starts: SNAPMODE is 0 and
AUTOSNAP 63, whose bit 8 is polar tracking. With SNAP on from the start, a click away from any object snap went to the
nearest point of the 10 by 10 grid; it is now taken where it is, and AUTOSNAP reads 63, not 55. GRID, ORTHO, OTRACK,
OSNAP and DYN start as before, which is AutoCAD's start too, GRID aside: AutoCAD keeps GRIDMODE in each drawing, and the
presenter reads none from the file.

**FIXED.** `DxfTable.IsTitleSuppressed` and `IsHeaderSuppressed` read only the table's own 280 and 281, so a table of no
flag of its own in a table style that leaves the title or the header out was drawn and read with a title and a header it
does not have. They now read the title or header left out where the table's own flag or its style's says so, as AutoCAD
2021 reads a table, and `RowType`, `RecomputeTableBlock`, the row kinds rows put in and taken out keep and the presenter
all follow. A table of four rows in a style that leaves the title out has its first row the header and the rest data,
"R1" in its second row drawn 3 high at the bottom centre at (45, -28.5) as the data, not 5 high at the middle right as a
header; AutoCAD 2021 gave the same rows `_HEADER` and `_DATA` and drew "R1" there. A table's own `280 0` does not show a
title its style leaves out: AutoCAD read one of its own `93 1`, `280 0` in that style without its title.

**NEW.** Tab in the last cell of a table in the presenter adds a row, as AutoCAD 2021's table editor does. The new row goes
under the last, empty, with the cell styles, alignments and height of the last row, and its first cell opens for typing;
the row is an undo step of its own after the cell's words. AutoCAD's `InsertRowsAndInherit` gave a row added under a row
60.43 high a height of 60.43 and the alignments of the row above, and the presenter's row is the same.

**CHANGED.** A table in the presenter grows its rows to hold their words, as AutoCAD 2021 lays a table out. A cell's text
wraps at the cell's width less its margins, laid out as an MTEXT of that width by the presenter's own MTEXT layout, and its
row becomes as high as its tallest cell needs: the words' actual height and the margins above and below, one line of four
thirds of the text height at least. A row is never made lower than it was. This happens whenever the presenter draws a
table anew: a cell typed, Find and Replace, a table style changed or matched, and a table made by TABLE. In AutoCAD's
Standard in ghsimplex, with columns 30 wide, the title "Bill of steel members for the north frame of the hall" makes its
row 39.29 high, "Section and grade" the header's 24.21 and "HEA300 S355 with end plates both sides" its row 45.43, and
each text stands where AutoCAD 2021 put it, all within 1E-5 of AutoCAD's numbers. An MTEXT line also takes a word whose
ink ends exactly on the width, as AutoCAD does: "with end" 27 wide in ghsimplex 4.5 high is one line, where it broke in two.

**FIXED.** The presenter could not snap to, pick or select the entities of a DXF file that writes no handles, as one of
R12 written by hand with neither header nor tables: the pick index holds an entity by its handle, and they had none.
With SNAP and OSNAP on, LINE's next point near the corner (4 000, 3 000) of such a room went to the grid point
(3 990, 2 990) instead of the endpoint. `CadDocument` now gives each entity of such a file a handle on opening it, as
AutoCAD does: the next above every handle of the file, in its order, with `$HANDSEED` above them and `$HANDLING` 1, a
header made where it has none, and reads the drawing anew with them. They are written, so that an undo, which reads the
drawing back from its saved bytes, finds each entity under the same handle, and the drawing is saved with them, as
AutoCAD saves it. The corner snaps as an endpoint, 3 905.12 from (1 000, 500), the column's rim gives its quadrant and
centre, and a wall erased, undone and redone is picked as the same wall. A file with handles opens as before; the snap at
the cursor on the 100 000-entity drawing costs about 1.1 µs either way. The presenter's order was AutoCAD 2021's already
and is now pinned by a test: a point typed is taken as typed, and an object snap under the aperture beats object snap
tracking, which beats polar tracking or ortho, which beat the grid.

**FIXED.** The presenter draws a layer switched off or frozen in a third of the time it took. An entity on a layer that
is not shown is passed over before its own visibility is read from its groups, which cost near a microsecond each, and
a display list made again after an edit is sized as the one before, not grown by doubling. In a drawing of 100 000
entities the layer of a third of them switched off is drawn again in 23 ms, not 85, and everything drawn is item for item
as before. The three tests that time this drawing now run alone: run beside each other, each one's garbage collections
stopped the others' timers, and the layer switched back on, 110 to 160 ms alone, was timed at 230 to 540 ms.

**CHANGED.** Undo and redo in the presenter no longer wait for the whole drawing to be read back on a large drawing. After
every step, undo and redo, a worker thread reads ahead the drawings the next undo and the next redo go to, from the bytes
an undo has always read them from, and makes their index of handles; the undo then takes the drawing ready and draws
again only the entities its step touched. The undo states are also packed and unpacked in pieces of 4 MB side by side. On
a drawing of 1 000 000 entities, medians of five, the undo of a one-line MOVE and its display take 0.4 s where they took
2.2 s once the drawing is read ahead (about 2 s after the step), and 1.5 s where they took 2.6 s when asked at once; the
undo of an ERASE of 10 000 objects takes 0.8 s where it took 3.0 s, and finding an entity by its handle after an undo 0 ms
where it took 0.2 s. Every drawing an undo or a redo gives is the one saved at that step, byte for byte. The read-ahead
holds up to three more drawings, each about 330 MB at that size, so the peak working set rose from about 3.2 GB to 3.7 GB.

**NEW.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader) reads DWG files of AutoCAD 2007, 2008 and 2009 (AC1021),
which it refused by name until now. Their container is read: the file header and the pages coded by Reed-Solomon, the
compression of R2007 and its CRCs of 64 bits, each checked. The eight test drawings of AutoCAD 2021 in AC1021, a drawing
of anonymous blocks and one of 100 000 lines compare with AutoCAD's DXF of them record for record; a drawing of tilted
splines differs only in the two spline normals the other versions differ in. The drawing of 100 000 lines reads in
750 ms, its AC1018 copy in 747 ms in the same runs.

**FIXED.** [`DwgReader`](xref:GeometryHelper.Dwg.DwgReader), in every version: an object whose decoded values the
library's geometry refuses, such as a damaged SPLINE whose plane cannot be worked out, is left out with the Warn line of
broken objects, the others read. It refused the whole file with a `FormatException` before. Found by the fuzz of the
AC1021 reader, 2 of 8 000 changed files.

**CHANGED.** The colours of [`DxfColor`](xref:GeometryHelper.Dxf.DxfColor) by index are AutoCAD 2021's, as its
`Color.FromColorIndex(ColorMethod.ByAci, i).ColorValue` gives them, where they were ezdxf's table. 197 of the 255
indexes change: the darker shades of each hue (12 was 0xA50000 and is 0xCC0000, 14 was 0x7F0000 and is 0x990000), a few
entries of ezdxf's table that were wrong (109 was 0x135817 and is 0x264C2F), and the greys 250 to 254, which are
0x333333, 0x5B5B5B, 0x848484, 0xADADAD and 0xD6D6D6 (250 was black). The presenter draws these colours, and the nearest
index of a true colour is found among them: 0x333333 is 250, RGB 10, 200, 30 is 102. The indexes 1 to 11, the two
brightest of each hue (10 and 11, 20 and 21, and on) and 255 are unchanged.

**CHANGED.** The presenter's MTEXT editor lays out its words as the canvas draws them, as AutoCAD 2021's in-place
editor does. It used to set each paragraph on one line, 1.25 times its tallest letters below the last, in WPF's own
measure, with stacks written inline. It now wraps at the width and spaces its lines at least or exactly as the MTEXT says,
with its stacks stacked, its tabs, indents, paragraph spacing and columns set and its mask and frame drawn, all by the
display's own layout and fonts. "Pipe {\H2x;BIG} size 1\S1/2; inch" 40 wide in Arial 2.5 high breaks and stands in the
editor where the drawn MTEXT does. The caret and the selection follow the wrapped lines: a word typed past the width takes
the caret to the next line, and Home, End, the arrows up and down and a click work on the lines as drawn. The new
[`CadMTextEditorLayout`](xref:GeometryHelper.Dxf.Viewer.CadMTextEditorLayout) gives the parts and the place of each
letter, and `CadMTextEditBox` takes the `Document` whose fonts set them. Typing in an MTEXT of 2 000 letters costs about
1.5 ms a key, the layout 0.5 ms and the drawing 1 ms. The ruler still shows no paragraph indents or tab stops, and the
columns cannot be dragged.

**FIXED.** The fields of dynamic input in the presenter followed the raw cursor while OSNAP, ortho or polar held the
rubber band elsewhere. They now measure to the point the band ends at, as AutoCAD's do, and stand by that band. With the
cursor 4 pixels off the end (4 000, 0) of a wall, LINE's next point from (1 000, 1 000) showed 3 185.00; it shows
3 162.28 and 341.6, the length and the angle to the end. A distance typed alone goes along the band as well: with ortho
on and the cursor at (1 300, 1 700), 200 typed from (1 000, 1 300) ends the line at (1 000, 1 500), straight up, where
it went towards the cursor before. `CadCursorEventArgs` gives the band's end as `BandX` and `BandY`, and
`MainViewModel.SetCursor` takes it.

**CHANGED.** More second points show the length and the angle in the presenter's dynamic input, where they showed X
and Y: AutoCAD 2021's pointer input gives a second or next point in polar form by default, relative Cartesian for
RECTANG alone. They are the second point of MIRROR's line, of ROTATE's and SCALE's Reference by two points, of ALIGN's
destinations, of BREAK's First point, of DIST, of TEXT's Align and Fit, of DIMLINEAR's and DIMALIGNED's second extension
line, of DIMCONTINUE and DIMBASELINE, the endpoints of an angle in MEASUREGEOM and DIMANGULAR, and the next points of
AREA and of REVCLOUD's Polygonal. MIRROR's second point 300 right of and 400 above the first shows 500.00 and 53.1, not
300.00 and 400.00. The corner of a box (RECTANG, ZOOM's window, MTEXT, REVCLOUD's Rectangular) and the location of a
dimension line or a leader keep X and Y.

**FIXED.** CIRCLE's 3P in the presenter showed one box of text at each of its points, "500.00 < 53.1°" at the second,
because the prompts also take _tan. Its first point now shows the X and Y fields, and its second and third points the
length and the angle from the point before, as AutoCAD 2021's pointer input gives a first point and a second or next
point. A number typed goes into a field and Tab locks it: from (1 000, 0) and (1 000, 400), 600 locked with the cursor at
(1 600, 0) takes the third point 600 along the band. _tan typed still asks for the object touched.

**CHANGED.** The dynamic input of the presenter shows the length and the angle in their fields alone, with no dimension
line, extension lines, angle arc or dashed base line drawn under them. That is every prompt with these fields: the
second point of LINE, PLINE, ARC, CIRCLE's 2P, ELLIPSE's axis, POLYGON's edge, XLINE, RAY, MOVE, COPY, STRETCH, OFFSET
by two points and a grip stretched or moved, a distance asked from a base point and an angle asked from one. The length
sits beside the middle of the rubber band, 8 pixels off it on one side, and the angle 50 pixels along the band from the
base point, 8 pixels off it on the other, so that neither covers the band. The prompt's box never hides a field: where
it would, the length goes to the band's other side or the box moves up or down clear of the fields, whichever moves less.
With the cursor by the canvas's right edge the box stands left of it, and over a band rising 11 degrees it covered the
length, now set below the band. Typing, Tab to lock and a held value holding
the band work as before: 300 locked at a line from (1 000, 1 000) with the cursor at (1 300, 1 400) still ends it at
(1 180, 1 240). RECTANG's X and Y fields and the X and Y of pointer input are unchanged.

**NEW.** EXPORT and -EXPORT in the presenter, as AutoCAD 2021 has them. EXPORT's Export Data dialog lists AutoCAD's
types: 3D DWF, 3D DWFx, ACIS, Bitmap, Block, DXX Extract, Encapsulated PS, IGES, Lithography, Metafile, V7 DGN and V8 DGN.
Bitmap is written as BMPOUT writes it. The other types have no writer and are refused, with nothing written:
`Metafile (*.wmf) has no writer in the presenter: nothing was exported.` -EXPORT asks
`Enter file format [Dwf/dwfX/Pdf] <dwfX>`, then `Enter plot area [Display/Extents/Window] <Display>:` (on a layout
`[Current layout/All layouts]`), then `Detailed plot configuration? [Yes/No] <No>:`, word for word as AutoCAD 2021's
console asks them. It then asks for the file in a Save As PDF dialog, or on the command line with FILEDIA 0, and writes
the PDF with -PLOT's writer. Dwf, dwfX and All layouts are refused. The format answered becomes the next default. The
file dialogs go through `MainViewModel.AskExportFile` and a [`CadExportJob`](xref:GeometryHelper.Dxf.Viewer.CadExportJob),
and Other Formats under Export in the application menu, greyed until now, runs EXPORT. The registry holds 175 commands.

**NEW.** The presenter's command line searches the drawing's content, as AutoCAD 2021's Search Content: below the
commands and system variables, under a rule, the AutoComplete list holds the blocks, layers, hatch patterns, text styles
and dimension styles whose names begin with the letters typed, then those with the letters inside, each kind by name and
marked at the right. WAL typed in a drawing of the block WALL_TAG and the layers WALLS and WALL-FINISH lists
`WALL_TAG Block`, `WALL-FINISH Layer` and `WALLS Layer`; a kind's keyword (BLOCK or INSERT, LAYER, HATCH, TEXTSTYLE or
STYLE, DIMSTYLE) lists every name of the kind. Choosing a row acts on it: a layer is made current through CLAYER, a block
inserted through -INSERT, which then asks the insertion point, a hatch pattern given to HATCH, a text style or a
dimension style made current. Search Content of the command line's Input Settings, greyed until now, switches it and is
kept in the settings file; it is on by default, as in AutoCAD. As AutoCAD's help says, the content is searched at the
command line only, not in the box of dynamic input at the cursor. AutoCAD's choice and order of the kinds (Input Search
Options), its collapsible section and its picture of a hatch pattern under the pointer are not built: they need a dialog
and a list of their own.

**FIXED.** The presenter's dimension pictures follow DIMTMOVE as AutoCAD 2021 draws them. Words the fit moves out of a
linear dimension stand over its line under DIMTMOVE 2, on the side away from the points, DIMEXE, one and a half DIMGAP
and half their height out, their height more where they read below it ("4" of a dimension of 4, 5.9375 below the middle
of its line), and under DIMTMOVE 1 beside a leader to that place and a landing; an angle's go over its arc along the
middle of its sweep, past a leader under 1. DIMTEDIT of a linear dimension under DIMTMOVE 0 runs the line through the
point given and stands the words above it there, on it with the line broken for DIMTAD 0, where the line ran a gap and
half their height under them; under 1 a leader is drawn to words further from the line than DIMGAP and half the heights
of the words and the arrowheads, and none to words nearer; under 2 the words' middle is the point, the line broken where
it runs through them. A dimension of AutoCAD's with its words moved is drawn as AutoCAD drew it. Of 846 pictures
AutoCAD drew for this, 762 match to 0.001. Left: 56 angles whose arc passes exactly through their points, where
AutoCAD's side of one extension line follows the rounding of the coordinates, and 28 radii and diameters of a small
circle made under DIMTMOVE 1 or 2 with their point inside it.

**FIXED.** Two more rules of the presenter's radius, diameter and angular pictures follow AutoCAD 2021. A diameter's
words on its line (DIMTAD 0) need their width and a gap each side, and with the arrowheads inside the arrowheads' room
as well, as a linear dimension's do: "%%c6" 4.357 wide now stands at the centre of a diameter of 6 under DIMATFIT 1,
DIMTOFL's line through the circle broken a gap clear of it, where it stood outside. An angle's extension lines run from
DIMEXO off its points to DIMEXE past the arc wherever that is any length, as a linear dimension's: with its points 20
from the vertex and its arc 20.3 out they run from 20.625 to 21.55, where none were drawn. Checked against 70 new
pictures AutoCAD drew, all matching to 0.001; the 676 of the earlier rounds still match.

**NEW.** PURGE and -PURGE of the presenter purge groups, as AutoCAD 2021's do: the Groups option of -PURGE and of its
All, and a Groups node in the tree of the Purge dialog. A group is purged when it holds nothing, whether it was never
given a member or its members were all erased, named or unnamed: of a drawing with the groups EMPTY and FULL and an
unnamed group whose one line was erased, -PURGE Groups says `Deleting group "*A1".`, `Deleting group "EMPTY".`,
`2 groups deleted.` and keeps FULL, and a second run says `No unreferenced groups found.`. Find Non-Purgeable Items
lists FULL with `Group FULL holds objects.`.

**FIXED.** A disabled drop-down and a disabled button of the presenter look disabled. The shared styles of the theme
gave neither a disabled look, so the Live section, Visual style and Background boxes of New View, the Lighting box of
Drawing Units and the ribbon buttons that need a drawing, or are not built yet, read as ones to press, and a disabled
drop-down still lit its border under the mouse. A disabled drop-down now shows its words muted and its box at half
strength and does not answer the mouse; a disabled button shows its icon and words at 45 per cent.

**FIXED.** The presenter's Layer States Manager restores a state into the current viewport of a layout where "Apply
properties as viewport overrides" is ticked; the tick was read and then ignored, and the layers themselves were changed.
A state that has the layer A red and 0.50 wide, B off and C frozen, restored so into a viewport after A was made green,
leaves A green in the drawing and gives it a red, 0.50 override in that viewport, freezes B and C there, and leaves
every other viewport alone; a property the state and the layer agree on takes its override off, and a layer the state
has on and thawed is thawed there. With "Turn off layers not found in layer state" the layers the state does not name
are frozen in the viewport. One undo step, as the restore into the layers is. On the Model tab, and on the paper with
no viewport chosen, Restore changes the layers as before.

**FIXED.** `DxfLayerStateManager.GetLayerStateNames(includeHidden, false)` left out a layer state imported from a .las
file. AutoCAD 2021 writes such a state with its layers by name (group 8: `XR_DOORS`, `0`), which the library took for
the mark of an xref's state; `-LAYER` stAte `?` lists it with the drawing's own. A state is now an xref's only where it is
named `xref__state` and every layer of its record is named as `xref|layer`, as AutoCAD writes
`70-b-xref-source__GH_XR_STATE` of the layer `70-b-xref-source|XR_WALLS`.

**FIXED.** The presenter's typed and shown angles follow ANGBASE and ANGDIR as AutoCAD 2021's do; they were read from
the X axis counter-clockwise whatever UNITS had set (only an arc's included angle followed ANGDIR). With ANGBASE 90 and
ANGDIR 1, `@100<30` now ends at 50, 86.6 and not 86.6, 50; 30 at ROTATE turns 30 degrees clockwise, the base left out
as AutoCAD leaves it out of a turn; DIST writes a line to the east as "Angle in XY Plane = 90", and LIST, the Properties
palette, the cursor tip, the coordinate readout and the angle box of dynamic input count the same way. `@100<<30` and
`@100<<<30` end at 86.6, 50 whatever the settings. Prompts whose default is a direction offer it counted
("Specify rotation angle or [Copy/Reference] <90>:"). New: `CadUnits.ToDirection` and `FromDirection`,
`CadCommandSession.CountAngle`, and `CadPrompt.AsksTurn` and `AsksPlainAngle`, which say how the session reads an angle
typed at a prompt. A drawing with ANGBASE 0 and ANGDIR 0 behaves as before.

**NEW.** `DwgReader` (namespace `GeometryHelper.Dwg`) reads a DWG file of AutoCAD 2000 (AC1015), 2004 (AC1018), 2010
(AC1024), 2013 (AC1027) and 2018 and later (AC1032) by the library's own code, as the drawing its DXF would give:
`DwgReader.Read` gives the `DxfDocument` of `DxfReader` and `DwgReader.Open` the `DxfDatabase`, from a path or a stream.
Read are the header variables, the classes, the nine tables, the blocks and layouts, LINE, POINT, CIRCLE, ARC, ELLIPSE,
SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE with its vertices, TEXT, MTEXT, ATTDEF, INSERT with its ATTRIBs, SOLID, TRACE,
3DFACE and DIMENSION, and of the objects DICTIONARY, XRECORD, LAYOUT, GROUP and MLINESTYLE, each with its layer,
linetype, colour (from 2004 with its true colour and transparency), lineweight, owner, reactors, extension dictionary
and extended data; texts of 2010 and later, UTF-16 in the file, come out letter for letter. Eight drawings made in
AutoCAD 2021, in each of the five versions, and a drawing of 100 000 lines in each come out group for group as the DXF
AutoCAD writes of them, numbers to nine places; the 100 000 lines read in about 0.75 s, against 0.27 s for AutoCAD's
14 MB DXF of them. The types not read yet, VIEWPORT, HATCH and LEADER among them, are left out and counted by type in
one Debug line; a DWG of another version, AutoCAD 2007 (AC1021) among them, is refused with a `NotSupportedException`
that names it. Every check sum is checked, from 2004 those of every compressed page too: a wrong one in the header, the
classes, the object map or a page refuses the file with a `FormatException`, and one broken object is left out with a
Warn line while the others are read. See the guide page "Reading DWG files".

**CHANGED.** The presenter lays the words of an MTEXT out as AutoCAD 2021 does, where it drew one line a paragraph 1.25
heights apart. The words wrap at the MTEXT's width (group 41) at their spaces alone, never after a hyphen, a word too
long standing whole past the width and `\~` holding two words together; a width of 0 wraps nothing. Lines stand 5/3 of
the height apart times the factor (44): exactly so under `Exactly`, and under `AtLeast` further where a taller run or a
stacked fraction needs the room. `\S` stacks its parts with a bar (`/`), with none (`^`) or along a diagonal (`#`), each
part as high as its ink, and `\A` sets the words beside it at its bottom, middle or top. The paragraph codes `\p` set
indents and margins, the alignment (left, centre, right, justified, distributed), tab stops (left, centre, right,
decimal; every 4 heights where none is set), the space before and after and a line spacing of the paragraph's own.
Static and dynamic columns take the lines that fit their heights, `\N` starting the next. The background mask and the
frame are drawn around the lines, the scale factor less 1 of a height out (1.5 for a frame alone). "II II II" 20 wide in
letters 10 high whose I is inked 1 in and moves the pen 4 is set as "II II" and, 16.67 below it, "II". Held against
AutoCAD's own fragments of 155 MTEXTs (536 lines), in an SHX font and in Arial: 150 have the same breaks with every line
within 0.2 % of the height of where AutoCAD puts it. `DisplayText2.Parts` is now set for every MTEXT: a text for each
run of each line, the mask (`DisplayFill2`) and the frame first; the box of the MTEXT's item is that of its parts.

**CHANGED.** The presenter's grips and EXPLODE of an MTEXT follow its new layout, as AutoCAD 2021's do. An MTEXT with a
width has a grip at each corner of its box besides its location (`CadGripKind.Corner`; the location alone for no width
or for columns), and a corner dragged takes its two sides of the box to the point: "II" over "II" 240 wide at the
origin, its top right corner dragged to (260, -12), is 260 wide with a defined height of 14.67 and located at (0, -12).
`CadGrips.Of` therefore gives four or five grips where it gave one, the location no longer always the first. EXPLODE
makes a TEXT of each run of each line where the picture draws it, and a LINE of the bar of each stack, where it made the
TEXTs of the library's own simpler layout.

**FIXED.** `DxfMText.ShowBorders` set in a drawing of R2018 with no background writes the colour and the scale of a
background (63 256, 45 1.5, 441 0) after its group 90 of 16: AutoCAD 2021 discarded the whole drawing without them
("Missing DXF group code: 90"). In a drawing of R2004 to R2013 it writes none, as before: there AutoCAD discards the
drawing that has them.

**NEW.** DIMARC (DAR), the presenter's arc length dimension, as AutoCAD 2021 makes it and stored as the library's
`DxfArcDimension`: Mtext and Text for its words, Angle for theirs, Partial for the length between two points of the arc,
and Leader, offered past 90 degrees of sweep, for a leader from the words' arc to the arc measured. Below 90 degrees the
extension lines run along the middle of the arc, from 90 up from the centre; the arc symbol stands before the words
(DIMARCSYM 0), over them (1) or nowhere (2); a point in the sector the arc does not sweep dimensions the rest of the
circle. An arc of 30 from 20 to 60 degrees dimensioned 40 out reads "21" 41.88 out at 40 degrees. Its pictures match the
69 AutoCAD drew to 0.01, and AutoCAD draws the dimensions stored the same.

**CHANGED.** The words of the presenter's dimension pictures are measured in the font of their text style as AutoCAD
measures them, not taken as 0.7 of their height a letter: "R20" of arial.ttf 2.5 high is 5.965 wide and 2.553 high. A
TrueType font is read from the Fonts folders of Windows, an SHX font where the fonts set on a document find it. The
526 pictures of linear, radius, diameter and angular dimensions AutoCAD drew now all match to 0.01 (35 before); a radius
of 6 takes "R6" inside, a point on the dimension line has its extension line, and an angle's ticks turn along the arc.

**CHANGED.** DIMTEDIT of an angle under DIMTMOVE 0 runs the arc through the point given and stands the words on it
there, above it as they read; a radius's or a diameter's words slide along its line, which stays, under DIMTMOVE 0 and
1, and stand at the point under 2. A radius or a diameter made under DIMTMOVE 1 or 2 keeps its line through the point
the circle was picked at, as AutoCAD 2021 does. The grips drag the words as before.

**NEW.** PLOT and -PLOT plot to images: GeometryHelper PNG (lossless) and GeometryHelper JPG (quality 90) beside
GeometryHelper PDF. Their sizes are in pixels, 1280 x 720 to 3840 x 2160, and Custom… makes one of 16 to 8 000 pixels a
side, kept between sessions; a raster device starts fitted and centred on white, with no plot style table and colour 7
black, and always plots to a file, asked on OK or with FILEDIA 0 on the command line. Model space fitted to 1920 x 1080 is
a PNG of exactly 1920 by 1080 pixels. New API: `CadPaper.PixelSizes`, `CadPaper.Pixels`, `CadPlotSettings.PngDevice`,
`JpgDevice`, `UseDevice`, `GlobalSettings.CustomPixelSizes` and `CadRasterWriter.WritePlot`.

**CHANGED.** Dynamic input in the presenter works at the cursor as AutoCAD 2021's. At the Command prompt the text typed
shows in a box 18 pixels right of the crosshair and 14 below it, with the AutoComplete list under it; the command line's
own list stays closed while DYN is on. In a command the box shows the prompt as AutoCAD words it, "Specify next point or",
with a ↓ that opens the prompt's options one per row, Esc closing the menu alone. The X and Y fields are relative to the
base point, as DYNPIFORMAT 0, # making them absolute: RECTANG's other corner shows 1 200.00 and 800.00 for a corner
1 200 by 800 from the first, and "500,300" typed there is a corner 500 by 300 from it. LINE's and PLINE's next point
show the length on a dimension line beside the rubber band and the angle on an arc from the X axis.
`MainViewModel.DynamicInputPrompt` no longer carries the options and the colon: "Specify next point or".

**CHANGED.** The presenter's Order Group dialog follows its board: the groups in a Group Name list, and the three boxes
labelled with the ranges the group allows, a group of twelve moved from 3 to 0 reading (0-11), (0-11) and (1-9).
Highlight < now steps through the members on the canvas one at a time, `Object 0 of 11 [Next/Previous/eXit]:`, Enter
bringing Object Grouping back; Object Grouping's own Highlight still shows the whole group at once.

**NEW.** `DxfDatabase.ViewTable`, the named views of a drawing as AutoCAD's `ViewTable`: `DxfViewTableRecord` has its
`CenterPoint`, `Height`, `Width`, `IsPaperspaceView`, `Target`, `ViewDirection`, `ViewTwist` and `LayerState`, the
layer state its layer snapshot is kept in, and is added, renamed and erased as AutoCAD 2021 writes a view: a view of the
window (10, 20) to (110, 80) is written with its centre (60, 50), 100 wide and 60 high, looking down Z with a lens of 50,
pointing at the visual style 2D Wireframe. Its extension dictionary is erased with it.

**NEW.** Layer states in the presenter, as AutoCAD 2021 keeps them: LAYERSTATE (LAS) opens the Layer States Manager (New,
Update, Edit, Rename, Delete, Import, Export, the properties to restore, Turn off layers not found in layer state,
Restore), and -LAYER (-LA), now a command of its own, has stAte with ?, Save, Restore, Edit, Name, Delete, Import and
EXport in AutoCAD's words. A state is an XRECORD of ACAD_LAYERSTATES under the layer table's extension dictionary, its
groups those AutoCAD 2021 wrote of the same drawing; a .las file Export writes of a drawing of the layers 0, A red, B off
and C frozen is AutoCAD 2021's to the byte, and Import reads it. Each run is one undo step; AutoCAD 2021 audits a drawing
with two states with no error and lists them.

**NEW.** Named views in the presenter: VIEW (V) opens the View Manager (Current, Model Views, Layout Views, Preset Views
greyed; Set Current, New, Update Layers, Edit Boundaries, Delete; the name, height and width) and its New View, and -VIEW
(-V) asks ?, Delete, Orthographic, Restore, Save, sEttings (Layer snapshot) and Window as AutoCAD 2021 does, ? listing
`"Stair core"` with M or P. A layer snapshot is the hidden layer state ACAD_VIEWS_ and the view's name, restored with the
view. AutoCAD 2021 audits a drawing with three views, one of a layout, with no error and restores them.

**NEW.** UNITS opens the presenter's Drawing Units, its Sample Output following each change, and Direction Control, whose
pick asks two points; OK writes LUNITS, LUPREC, AUNITS, AUPREC, ANGDIR, INSUNITS and ANGBASE as one undo step. -UNITS
stays typed. `CadUnits` writes 15.5 as 1.55E+01, 15.50, 1'-3.50", 1'-3 1/2" or 15 1/2 and 45 degrees as 45.0000,
45d0'0", 50.0000g, 0.7854r or N 45d0'0" E, and typed lengths and angles are read in the drawing's units: `1'3-1/2"<100`
in architectural units and grads is 15.5 straight up. The status bar shows coordinates in LUNITS where they are not
decimal.

**CHANGED.** The presenter's LAYER on the command line offers stAte after Unlock, as AutoCAD's -LAYER does; the registry
holds 173 commands.

**NEW.** The presenter previews the object a prompt is about to make, as AutoCAD 2021 drags it: `CadPrompt.Preview`
(`CadPreview`) gives the display items of what a point picked at the cursor would make, made by the command's own code,
and the canvas draws them in the current layer's colour. RECTANG asking its other corner draws the rectangle, not a dashed
line to the cursor; CIRCLE (radius, diameter, 2P, 3P, Ttr, Tan Tan Tan), ARC (every way, and on from the last line),
ELLIPSE and its Arc, POLYGON, DONUT, XLINE, RAY, PLINE (lines and every arc option, widths drawn), REVCLOUD, OFFSET,
STRETCH, the frame of MTEXT and DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR, DIMORDINATE, DIMCONTINUE and
DIMBASELINE do the same. ROTATE and SCALE move their ghost by a Reference too, and ARRAYRECT and ARRAYPOLAR show every
copy while their options are offered. A preview costs 6 to 26 microseconds for a circle or a rectangle, 0.2 ms for a
linear dimension.

**NEW.** The commands that change objects preview the change too, made by the code that makes it. FILLET and CHAMFER,
hovering the second object, draw both objects trimmed and the arc or line between them; TRIM in Quick mode draws the
piece a pick would cut away faded (`CadPreview.Fades`), EXTEND the object run on to its edge, BREAK the piece its second
point takes out; LENGTHEN DYnamic draws the object with its end at the cursor. HATCH draws the hatch of the areas picked
and the one under the cursor, the area kept while the cursor stays in it; TEXT a square of the height while its height
and rotation are asked, TABLE the table's outline, MLEADER the leader and landing, QDIM the whole row. ROTATE and SCALE by
Reference Points, -ARRAY's unit cell and ALIGN move the ghost as the objects move. A preview at a prompt for one object is
of the object under the cursor, and writes nothing into the history. A FILLET of two lines costs 38 microseconds, a TRIM
26, a hatch of a square with an island 0.1 ms, a QDIM row of three dimensions 0.76 ms.

**NEW.** TRIM and EXTEND preview in Standard mode too, against the cutting or boundary edges chosen, and with Shift held
they preview what the other does, as the pick with Shift then does: `CadPreview.Shift`, which the canvas sets from the
keyboard, is carried by the answer previewed, and `Fades` follows it. A line from (0, 0) to (50, 0) hovered in TRIM with
Shift held previews itself run on to the wall at 100, not faded.

**NEW.** -ARRAY previews its copies at the prompts that had none. The distance between columns can be shown by two
points, as AutoCAD takes it, and the copies follow the second; a Polar array shows its copies while the angle to fill
and the angle between items are asked, each measured from the centre to the cursor, and while it asks whether to rotate
them. Four items round the origin with the cursor at (-10, 10) preview three copies 45 degrees apart, the 135 degrees
the pick there fills.

**NEW.** PASTECLIP and PASTEBLOCK show the Clipboard's objects at the cursor while they ask the insertion point, held
by their base point as the paste puts them, and drawn by the layers and blocks they bring before the drawing has them;
text of another app is shown as the MTEXT it becomes. PASTEORIG asks no point and previews nothing. Nothing comes into
the drawing until the point is picked.

**NEW.** TEXT's preview follows the justification, and shows the words while they are typed. The square of the height
is held at the point as the text will be: a height of 5 at (10, 10) gives the square from (10, 10) to (15, 15) for Left,
from (7.5, 10) to (12.5, 15) for Center, from (7.5, 7.5) to (12.5, 12.5) for Middle, from (10, 5) to (15, 10) for TL; a
text fitted between two points gives the box as long as they are apart. At "Enter text" the canvas draws the words
typed so far in their style, height, rotation and justification, by the code that then makes the line
(`CadPreview.Typed`, which the window sets from the command line).

**NEW.** Dimensional input at the cursor beyond LINE and PLINE. The length and the angle from the first point show at
the second point of ARC, CIRCLE's 2P, ELLIPSE's axis, POLYGON's edge, XLINE, RAY, MOVE, COPY, STRETCH and OFFSET's
distance by two points, and at a grip stretched or moved, with Tab locking a field as before. A prompt for a distance
from a known point shows the length field alone (the radius of CIRCLE and POLYGON, ELLIPSE's other axis), and one for an
angle the angle field alone (ROTATE); RECTANG's other corner keeps X and Y, its two sizes, and SCALE's factor the one
box. A field that holds a number now holds the rubber band, the preview and a click: 300 locked as a line's length from
(1 000, 1 000) with the cursor at (1 300, 1 400) ends the preview and the line at (1 180, 1 240). New API:
`MainViewModel.IsDynamicInputLength`, `IsDynamicInputAngle`, `IsDynamicInputDimensional` and `HoldDynamicInput`.

**FIXED.** ROTATE's ghost in a UCS turned from the world's now turns by the angle the objects are turned: in a UCS turned
30 degrees it turned 30 degrees more than the result.

**NEW.** BATTMAN, the presenter's Block Attribute Manager: the attribute definitions of a block in its order, with the
INSERTs of it counted in the drawing and in model space, the modes as AutoCAD's letters I C V P L M and a tag given twice
in red; Move Up, Move Down, Edit Attribute (Attribute, Text Options and Properties) and Remove, written into the block by
Apply or OK, each one undo step; Sync brings every INSERT in line as ATTSYNC does, the values typed kept, and its Settings
choose the columns of the grid and whether Apply syncs at once. A block GB of GRID, LEVEL and REV with LEVEL moved up and
synced gives each INSERT LEVEL then GRID; AutoCAD 2021 audits the drawing with no error.

**NEW.** ATTDIA is a variable of SETVAR, 0 by default; at 1 INSERT and -INSERT ask the values of a block's attributes in
the presenter's Edit Attributes dialog, eight a page in the block's order, the constant ones left out and a value to be
verified asked once. A title block of eleven attributes opens on Page 1 of 2.

**NEW.** PURGE shows the presenter's Purge dialog: the named objects nothing uses by kind, with counts, a preview of a
block, text style or dimension style, Confirm each item, Purge nested items, the geometry of no length and the empty
texts, Purge Checked Items and Purge All, each purge one undo step; and Find Non-Purgeable Items, the objects in use with
the reasons ("Layer WALLS is the current layer.", "Used by 2 objects in model space and 0 in layouts.", "Referenced by
block GB.") and Select Objects. -PURGE stays typed, and now purges nested items too: a block OUTER inserted nowhere that
holds the only INSERT of INNER takes INNER with it, and the layer only INNER used, where it took one pass before.

**NEW.** CLASSICGROUP, AutoCAD 2021's Object Grouping dialog: the groups with Include Unnamed, Find Name, Highlight,
New, Remove and Add asked on the command line with the dialog hidden, then shown again; Rename, Description, Explode,
Selectable and Re-Order (Order Group: Re-Order, Highlight, Reverse Order) at once; OK keeps every change as one undo
step, Cancel takes them back. The registry holds 167 commands.

**FIXED.** `DxfPolyline2d.CurveFit` (PEDIT Fit) now gives AutoCAD 2021's arcs, vertex by vertex to 1E-11 on 60 polylines
fitted in its console. The tangent between the ends of an open polyline, and everywhere round a closed one, is AutoCAD's
blend of the two segments at the vertex weighted by the turns either side, not the circle through the vertex and its
neighbours: on (0, 0), (100, 0), (100, 50), (0, 80) the tangent at (100, 0) is 64.41 degrees, not 63.43, and a closed
triangle takes the bisector at each corner. The arcs of a segment meet where the curve runs along the chord, or for an
S-shaped segment with radii as the sines of the tangents' angles to it, not at equal tangent lengths. A segment whose
tangent at either end is within a millionth of a radian of its line, and a polyline of two vertices, stay straight with
no vertex added; two vertices on one point split the curve in two there; the vertex added after one given a tangent
keeps its direction (group 50), as AutoCAD writes it.

**FIXED.** `DxfPolyline3d.SplineFit` keeps the vertices of the curve after the first vertex of the frame, the rest of the
frame after them, as AutoCAD 2021 stores them and as the polyline of the plane already did.

**NEW.** A drawing whose CLASSES section lists no DICTIONARYVAR, as one the library makes new and the presenter's NEW
template, is given the class as AutoCAD 2021 lists it (AcDbDictionaryVar, ObjectDBX Classes, proxy flags 0, counted once
from R2004 on) when its first variable kept out of the header is set, where before the variable was refused. Set Current
of the presenter's multileader and table styles now works in a new drawing; AutoCAD 2021 audits such a drawing with no
error and reads CMLEADERSTYLE and CTABLESTYLE as set.

**NEW.** `Annotative` of a multileader style is set, as group 296 with no extended data, as AutoCAD 2021 writes the style
it makes annotative; the Annotative box of the presenter's Modify Multileader Style is enabled, and a new style started
from an annotative one is annotative.

**NEW.** The presenter lays out radius, diameter and angular dimensions as AutoCAD 2021 does: the words by DIMTAD, DIMTVP,
DIMJUST, DIMTIH and DIMTOH, what moves out where they do not fit by DIMATFIT, DIMTIX, DIMSOXD and DIMTOFL, the centre
mark and centre lines of DIMCEN, and an angle's words moved by DIMTEDIT under DIMTMOVE 1 (a leader and a landing) and 2.
A radius of 20 put 45 from its centre has its arrowhead outside the rim and the line from its back; an angle of 8 degrees
has its words and arrowheads outside past its first ray. Checked against 526 pictures AutoCAD 2021 drew of the same
inputs: 511 agree but for the width of the words, which AutoCAD measures by the font, and AUDIT finds no error.

**NEW.** The presenter's DIMTVP lifts the words of DIMTAD 0 by that many times their height (the line broken about them
below 0.7, as AutoCAD 2021), and a DIMGAP below nought draws a box round the words, words above the line standing clear
of it by the gap again.

**FIXED.** The dimension line of the presenter's dimensions ran to the tips of the arrowheads; it now starts at the back
of each, as AutoCAD 2021 draws it: a dimension of 100 with arrowheads of 2.5 has its line from 2.5 to 97.5, closed
filled ones too. The ticks, Dot small, Dot small blank, Integral and None run to the tip and, as AutoCAD's, never move
outside a short dimension.

**NEW.** Dynamic blocks in use, in the core: `DxfBlockReference.GetDynamicGrips()` gives the custom grips AutoCAD shows on
a dynamic INSERT, where its values put them (`DxfDynamicBlockGrip`: `GripType`, `Position`, `BasePoint`, `Direction`,
`PropertyNames`); on AutoCAD 2021's Architectural and Annotation samples every custom grip came out at AutoCAD's point and
in its order, the block properties table's grip apart. `DxfBlockReference.ResetBlock()` puts every value back to the
block's own as AutoCAD's ResetBlock does: the INSERT places the dynamic block again, its AcDbBlockRepresentation goes, its
place and ATTRIBs stay. `DxfDynamicBlockReferenceProperty` gains AutoCAD's `Show` and `VisibleInCurrentVisibilityState`,
and `StateNames`, Not flipped and Flipped of a flip; `DxfBlockTableRecord.GetVisibilityStates()` lists a block's states
with the entities each shows.

**NEW.** Dynamic blocks in the presenter, to AutoCAD 2021: a selected dynamic INSERT shows its custom grips (arrows,
squares, circles, flip arrows and list triangles in the dynamic grip colour); a grip dragged sets its value, taken to the
value set, a flip grip clicked flips, a visibility or lookup grip lists its values at the grip, the current ticked.
Properties has the Custom group (lists as drop-downs, distances and angles typed) and names an anonymous copy by its
dynamic block; RESETBLOCK and the right-click Reset Block put the values back; each change is one undo step. The Block
Editor's Visibility State box is live for a block with states. AutoCAD 2021 read every value the presenter set on the
library's dynamic samples as the library reads it, and AUDIT found no error.

**FIXED.** The presenter's -GROUP on a DXF of R2000 written without its OBJECTS section said "Object reference not set to
an instance of an object."; the drawing is now given the named object dictionary AutoCAD makes on opening it, and the
group goes in, one undo taking both back. New of the Table Style and Multileader Style dialogs, and TABLE making
Standard, failed the same way on such a drawing, and are given the dictionary now too.

**FIXED.** A dimension the presenter made took its style's DIMSCALE, not the drawing's current one: DIMSCALE 10 typed
with ISO-25 current left the next dimension's words 2.5 high. It now takes the header's `$DIMSCALE`, writing it as an
override where it is not the style's, as AutoCAD 2021 does: words 25 high, arrowheads 25 long, and DIMBASELINE and QDIM
spacing their lines DIMDLI times 10 apart.

**NEW.** The presenter's point modifier TK (TRACK, TRACKING), as AutoCAD's: "First tracking point:", then "Next point
(Press ENTER to end tracking):" until Enter, each point taken square from the one before along the UCS's axes, a distance
typed going along the axis nearest the cursor. CIRCLE with TK from 100,0 and a click at (130, 50) is centred at
(100, 50).

**NEW.** The presenter's variables POLARMODE, FILEDIA and CMDDIA, and AUTOSNAP's other bits, as AutoCAD's. POLARMODE
(kept in the settings file, `"polarMode"`) 2 runs object snap tracking on every polar angle and 8 acquires points only
with Shift held (`ObjectSnap.TracksPolarAngles`, `ObjectSnap.AcquiresWithShift`); 1 and 4 are kept, not followed.
AUTOSNAP 1, 2, 4 and 32 switch the snap marker, its name by the cursor, the magnet and the tip of ortho, polar and
tracking (`ObjectSnap.ShowsMarker`, `ShowsSnapTip`, `Magnet`, `ShowsTrackingTip`). FILEDIA 0 makes PNGOUT, JPGOUT,
BMPOUT and the window's SAVEAS ask for the file on the command line (`CadCommandSession.FileDialogs`); CMDDIA 0 makes
PLOT ask -PLOT's prompts. Global settings › Canvas and cursor has a Polar angle field (POLARANG).

**CHANGED.** Object snap tracking with POLAR on runs level and upright alone unless POLARMODE 2 is set, as AutoCAD's
default POLARMODE 0 does; before, it ran on every polar angle whenever POLAR was on.

**NEW.** `DxfOsnapPointRef.LastPointReference` and `WithLastPoint`: the reference of the point a PERpendicular or TANgent
snap was drawn from, which AutoCAD writes after a reference whose group 75 is 1. **FIXED.** Such a DIMASSOC was read
wrong: the last point's reference was taken for the next point, so a DIMLINEAR whose first point was perpendicular lost
its second point's tie; and a reference was always written with 75 = 0.

**NEW.** The presenter ties a dimension's second origin snapped PERpendicular (to a line, an arc or a circle) or TANgent
(to an arc or a circle), with the point it was drawn from, and draws it again from that point when the object changes, as
AutoCAD 2021 does: DIMALIGNED from (0, 0) perpendicular to (100, -20)-(100, 60) measures 100, and 120 once that line is
moved 20 to the right; a tangent from (300, 100) to a circle of 25 about (200, 0) goes from (185.726, 20.524) to
(184.593, 9.688) when the circle moves 10 down.

**FIXED.** INSERT with Basepoint of a block in other units than the drawing landed off: a block in centimetres in a
millimetre drawing, its base point 30 above its origin, came 270 too high. The shift is now scaled by the units too, in
the drag and in the INSERT placed.

**FIXED.** -ATTEDIT took every attribute of an INSERT a crossing box touched; it now takes those the box touches, or a
window encloses, as AutoCAD's, typed (C, W, BOX) or dragged on the canvas
(`CadCommandSession.FeedSelection(handles, window, crossing, remove)`).

**NEW.** The presenter's variables PICKADD (0 makes what is picked last the selection, Shift adding) and ATTREQ (0
inserts with the attributes' defaults, unasked). DYNMODE keeps -3 to 3, negative while DYN is off; ORTHO switched is
written to the drawing's `$ORTHOMODE`, as AutoCAD saves it.

**NEW.** Recent Commands are kept in the settings file from one session to the next (`GlobalSettings.RecentCommands`);
a recent file no longer on the disk is taken off the list when chosen (`GlobalSettings.RemoveRecentFile`); the version
and preview of a recent file are read on a worker thread.

**FIXED.** A dimension the presenter made left its picture behind when moved on its own (MOVE, ROTATE, a grip): the core
wrote it without flag 32 of group 70, which says the block is the dimension's alone and which the core's `TransformBy`
needs to move the picture. `DxfDimension.DimBlock` set to an anonymous block now marks it, as AutoCAD 2021 writes a
DIMLINEAR's 70 as 32. A copy of a dimension made by COPY, ARRAY or a grip's Copy gets a picture block of its own, as
AutoCAD's COPY copies it, so that it is drawn where it stands and not on top of its original.

**NEW.** The presenter's associative dimensions are tied by more commands and snaps, as AutoCAD 2021 writes them: DIM,
DIMCONTINUE and DIMBASELINE (the first point tied to what the origin it starts from was tied to), QDIM (line ends, polyline
vertices, circle centers), DIMORDINATE, DIMANGULAR of two lines (four ends) and of three points, and INTersection snaps (both
objects). A line and its dimension copied together leave the copy tied to the copied line. An ordinate follows its point,
its leader end moved along the axis measured alone; an angle of two lines keeps its arc's share of the angle. AutoCAD
2021 audits a drawing with every case with no error and moves each such dimension with its objects.

**NEW.** The presenter has AutoCAD 2021's Block Editor. BEDIT (BE) shows the Edit Block Definition dialog, &lt;Current
Drawing&gt; first and then the drawing's named blocks with a preview and description, a new name making a new, empty
block; -BEDIT asks `Enter block name or [?]:`. The block opens alone on a background of its own (Global settings,
Block Editor background, #3B4049), the UCS icon at its base point, the Model tab only and the green Block Editor tab;
every drawing and modify command, and ATTDEF, works on the block's own entities, each an undo step that stops where the
block was opened or last saved. BSAVE (BS) redefines the block and draws every INSERT anew, the session one undo step
"BEDIT"; BSAVEAS copies the content to a new block, the one opened left unchanged; BCLOSE (BC) asks Save, Discard or
Cancel, and Discard reads the drawing back as it was. A door of one line given a second and saved shows both in each
of its INSERTs, and one undo takes them back. With nothing selected Properties shows the Block group (description,
units, Allow exploding, Scale uniformly). BEDIT, XREF, LAYOUT, PLOT, MSPACE, QSAVE and the other commands that leave the
block say "Not allowed in the Block Editor.". `CadDocument.OpenBlockEditor`, `SaveBlock`, `SaveBlockAs` and
`CloseBlockEditor`, with `CadBlockEditor`, do the same from code.

**NEW.** The presenter's Layer manager has AutoCAD 2021's VP columns while a layout is current: VP Freeze, New VP Freeze,
VP Color, VP Linetype, VP Lineweight, VP Transparency and VP Plot Style, acting on the viewport worked through or the one
selected on the paper, an override marked blue; its right-click menu freezes and thaws a layer in all viewports, adds a
layer frozen in them all and takes overrides out. VPLAYER (?, Freeze, Thaw, Reset, Newfrz, Vpvisdflt) and LAYVPI are typed
as in AutoCAD, and MVIEW freezes the layers of New VP Freeze in the viewport it makes. A viewport is drawn without the
layers frozen in it and with its overrides: TEXT, cyan, overridden red in the plan viewport is red there alone. Each change
is one undo step, through `CadViewportLayerEditor`. In the core, `DxfLayerViewportProperties`' colour, linetype,
lineweight, transparency and the new `PlotStyleName` are set as AutoCAD 2021 writes them (ADSK_XREC_LAYER_*_OVR), with
`RemoveColorOverride()` and its like and `DxfLayerTableRecord.RemoveViewportOverrides(viewport)`; AutoCAD 2021 audits such
a drawing with no error and reads every override back.

**NEW.** The status bar's right end follows AutoCAD 2021: MODEL is a label on the Model tab, PAPER a raised button on the
paper and MODEL a pressed one through a viewport; the scale opens AutoCAD's list (Scale to fit, 1:1 to 1:100, 2:1 to
100:1, Custom..., which runs the new VPSCALE and asks the scale on the command line); a padlock locks the display of the
viewport worked through or selected, its scale then grey; on the Model tab the place shows the annotation scale
(CANNOSCALE) and its list the drawing's scales.

**NEW.** Associative dimensions, as AutoCAD 2021 keeps them with DIMASSOC 2. The library reads and writes the DIMASSOC
under ACAD_DIMASSOC in a dimension's extension dictionary: `DxfDimension.DimAssoc`, `SetDimAssoc` and `ReleaseDimAssoc`,
`DxfDimAssoc`, `DxfOsnapPointRef` and `DxfOsnapType`, and `DxfDBObject.GetPersistentReactorIds`. In the presenter a
dimension whose points were picked with an object snap (or a line selected for DIMLINEAR, a circle for DIMRADIUS, an arc
for DIMANGULAR) is tied to its objects, and every edit of them carries it along in the same undo step, whatever command or
grip makes the edit: a line from (0, 0) to (100, 0) moved by (10, 5) takes its dimension, 20 above, whole to a line at 25;
its end stretched to (120, 30) measures 120 with the dimension line still 20 up; a circle of 25 scaled twice gives its
radius 50. Erasing the object, or moving the dimension on its own, unties it, as AutoCAD's "Dimension disassociated.".
DIMREASSOCIATE, DIMDISASSOCIATE and -DIMDISASSOCIATE are new commands and DIMASSOC (0 to 2, 2 by default) a system
variable; `CadCommandSession.Feed(CadSnapResult, bool)` hands the window's snap with its point. AutoCAD 2021's AUDIT finds
0 errors in a drawing of the presenter's six kinds of tied dimension, and its own MOVE and SCALE carry them.

**NEW.** The presenter gives dimensions AutoCAD 2021's grips: a linear or aligned one its two extension line origins, the
two ends of its dimension line and its words; a radius its chord point and words, a diameter its two chord points and
words; an angle its vertex, line points, arc and words; an ordinate its feature and leader end. Dragging one rewrites the
dimension's points and draws its picture anew, one undo step. A dimension of 100 from (0, 0) to (100, 0), its line 30
below, dragged by its second origin to (150, 10) measures 150 with its line still 30 below; its words dragged to (60, -50)
stand there with the line under them, as DIMTEDIT puts them at DIMTMOVE 0. `CadGripKind` has `DefinitionPoint`,
`DimensionLine` and `Text`.

**NEW.** The presenter works in model space through a viewport of a layout, as AutoCAD's MSPACE: a double click inside the
viewport, MSPACE (`MS`) or the status bar's PAPER button enters it, its frame drawn thick and the crosshair inside it alone;
a double click off it, PSPACE (`PS`) or MODEL goes back to the paper, and Ctrl+R goes to the next viewport. Through it the
commands draw in model space, picks and snaps work as the viewport shows model space, and the wheel, the middle button,
ZOOM and PAN change the viewport's view, one undo step each; `ZOOM 1/50XP` sets the scale to 1:50, and a viewport whose
display is locked keeps its view. The status bar's scale list and Properties' Standard scale, Custom scale and Display
locked rows set a viewport's scale: 1:50 in a viewport 100 high shows 5 000 of model space. `CadDocument.EnterViewport`,
`LeaveViewport`, `ActiveViewport`, `WorkingSpace`, `SetViewportView` and `CadViewports` are the API.

**NEW.** The presenter's command line has AutoCAD 2021's AutoComplete. With a drawing open and no command running, the
list opens above the command line 300 ms after the last key: LA typed lists LAYER first with "alias LA", then the commands
that begin with LA, the most used first (each command run is counted, `GlobalSettings.CommandUses`, kept in the settings
file), then the system variables marked {x}, then, below a rule, the names with LA inside (CLAYER, PLAN, POLAR). Enter or
Space runs the row chosen, the arrows move it, Esc closes the list and clears what is typed. Ten synonyms are built in (ROUND for FILLET, BEVEL for
CHAMFER, DELETE and REMOVE for ERASE, DUPLICATE for COPY, FLIP for MIRROR, TURN for ROTATE, RESIZE for SCALE, SYMBOL for
INSERT, PRINT for PLOT), shown as `ROUND (FILLET)`. AutoCorrect: LIEN followed by LINE three times has LINE offered first
for LIEN (`GlobalSettings.NoteCorrection`, `CorrectionOf`). The command line's right-click menu has Recent Commands (the
last 20), Input Settings (AutoComplete, AutoCorrect, Search System Variables, Mid-string Search, kept in the settings
file; Search Content greyed; Delay Time…, `Enter the delay time in milliseconds <300>:`, the new INPUTSEARCHDELAY
command), Copy, Copy History, Paste, Paste To CmdLine and Options…. `MainViewModel.ShowSuggestions`, `Suggestions`,
`RunSuggestion`, `RecentCommands` and `CommandLineMenu` are the API.

**NEW.** The G button of the presenter drops the application menu: a search box for the commands; New, Open, Save,
Save As, Export (PDF through PLOT, PNG, JPG, BMP, WBLOCK), Print (Plot, Plot Preview, Page Setup) and Close (the drawing
shown, or all), the entries not built greyed; Options and Exit. Recent Documents shows the pinned files first in the order
pinned, then the others newest first, 9 in all; a file opened by the Open dialog, a drop, the app's command line or the
lists goes to the top, and the list and its pins are kept in the settings file (`GlobalSettings.RecentFiles`,
`NoteFileOpened`, `SetFilePinned`). Resting the pointer on a file shows its preview, path, version, date and size, read
from the top and the end of the file only. `MainViewModel.CreateAppMenu`, `RecentDocuments` and `PinRecent` are the API.

**NEW.** The start screen lists Recent Documents (pinned and recent, with their previews, as a list or as large pictures)
and is GeometryHelper DXF's own where no host makes drawings from a model (`MainViewModel.CanMakeFromModel`): no "New
drawing from a model", `New · Ctrl+N` instead of "Blank drawing", and "Ctrl+O opens a DXF, Ctrl+N starts a new drawing."
on the command line. Ctrl+N makes a new drawing there (`MainViewModel.NewByShortcut`); GeometryHelper Drawing keeps its
items. The screenshot mode draws the new menus with `--dialog app-menu[:export]`, `cmdline-menu`, `input-settings-menu`,
`recent-commands-menu` and `autocomplete:LA`, and fills Recent Documents with `--recent`.

**NEW.** Multileader styles are written as well as read. `DxfMLeaderStyle` has setters for its leaders, landing and
words (`LeaderLineType`, `LeaderLineColor`, `LeaderLineLinetype`, `LeaderLineWeight`, `ArrowSymbol`, `ArrowSize`,
`BreakSize`, `MaxLeaderSegmentsPoints`, `EnableLanding`, `EnableDogleg`, `DoglegLength`, `Scale`, `ContentType`,
`TextStyle`, `TextAngleType`, `TextColor`, `TextHeight`, `TextAlignAlwaysLeft`, `EnableFrameText`,
`TextAttachmentDirection`, `SetTextAttachmentType`, `LandingGap`) and new `FirstSegmentAngleConstraint` and
`SecondSegmentAngleConstraint` (`DxfAngleConstraint`), `ExtendLeaderToText` and `DefaultMText`, each written as AutoCAD
2021 holds it: 45 degrees as 0.785398 radians, no limit of points as 0, scaled to the layout as a scale of 0.
`db.MLeaderStyleDictionary.SetAt("GH_NOTES", new DxfMLeaderStyle { TextHeight = 2.5 })` adds a style with every group
AutoCAD writes, its class listed and counted in CLASSES. The presenter has MLEADERSTYLE (MLS), the Multileader Style
Manager and its Modify dialog of three tabs, Leader Format, Leader Structure and Content, after AutoCAD 2021's: Set
Current, New…, Modify… and Delete (refused for Standard, the current style and a style in use), each one undo step; a
drawing of no style is given a metric Standard, arrows 2.5, break 3.75, landing 8, text 2.5 and a landing gap of 2. A
style modified gives its multileaders its values, those they have of their own kept. MLEADER draws in the current style
(CMLEADERSTYLE), its words and arrows as high as the style's rather than TEXTSIZE, and the Annotate tab's multileader
style box ends in "Multileader style…". Block content is not offered, as MLEADER places words only, and the annotative
box is greyed. MATCHPROP's Property Settings dialog is laid out as designed: the source's value beside each basic
property, its kind and layer under the boxes, and the prompt at the foot. The registry holds 149 commands and 113
aliases. AutoCAD 2021 audits a drawing of R2013 with three styles made and modified so, and multileaders drawn in them,
with no error, and reads every value as written.

**NEW.** The presenter has SETVAR and the system variables drafters set, typed as commands as in AutoCAD 2021: `Enter
variable name or [?] <FILLETRAD>:`, then `Enter new value for FILLETRAD <10.0000>:`; a variable's name typed alone asks the
same, and `?` lists those a pattern of wildcards matches. FILLETRAD 5 writes `$FILLETRAD` 5 into the header as one undo
step; SORTENTS 200 is refused with `Requires an integer between 0 and 127.` and asked again; OSMODE 4133 ticks Endpoint,
Center, Intersection and Extension, and 16391 ticks Endpoint, Midpoint and Center with OSNAP off; EXTMIN says
`EXTMIN = 0.0000,0.0000,0.0000 (read only)`. 47 variables in the window: those of the drawing's header (CLAYER, CECOLOR,
CELTYPE, CELWEIGHT, CELTSCALE, LTSCALE, PSLTSCALE, MSLTSCALE, DIMSCALE, TEXTSIZE, TEXTSTYLE, FILLETRAD, CHAMFERA, CHAMFERB,
PDMODE, PDSIZE, MIRRTEXT, SORTENTS, IMAGEFRAME, CANNOSCALE, INSBASE, LIMMIN, LIMMAX) each one undo step; those of the
session and the window (PICKSTYLE, SELECTIONCYCLING, PICKFIRST, OFFSETDIST, TILEMODE, OSMODE, ORTHOMODE, SNAPMODE, GRIDMODE,
POLARANG, AUTOSNAP, DYNMODE, PICKBOX, APERTURE, LWDISPLAY) at once; and DIMSTYLE, EXTMIN, EXTMAX, UCSNAME, UCSORG, DWGNAME,
DWGPREFIX, DATE and CDATE read only. PICKFIRST 0 (`CadDocument.PickFirst`) makes MOVE ask `Select objects:` though a line
is selected, as AutoCAD's. PICKBOX, APERTURE, LWDISPLAY and POLARANG are kept in the settings file as DYN and OSMODE are
(`MainViewModel.SettingsChanged`), POLARANG 30 as `"polarAngle": 30` of the new `GlobalSettings.PolarAngle`, which Drafting
Settings' increment sets too. PDMODE, PDSIZE, CELTSCALE, PICKSTYLE, SELECTIONCYCLING and IMAGEFRAME are variables of the
table now rather than commands of their own, and CECOLOR, CELTYPE and LTSCALE keep their own prompts when typed alone; the
registry holds 150 commands. `CadCommandRegistry.Variables`, `AddVariable` and `FindVariable`, and `CadSystemVariable`,
are the API.

**NEW.** The presenter's grips have AutoCAD 2021's grip modes. A click on a grip makes it hot and asks `** STRETCH **`,
`Specify stretch point or [Base point/Copy/Undo/eXit]:`; Enter or Space goes on to MOVE, ROTATE (with Reference), SCALE
(with Reference) and MIRROR and back, and STRETCH, MOVE, ROTATE, SCALE and MIRROR typed, or ST, MO, RO, SC and MI, go to a
mode. The modes other than STRETCH take the objects selected, drawn as a ghost: of two lines from (0, 0), MO to (10, 20)
moves both and RO 90 then turns them about (10, 20). Copy leaves the originals and asks again (`** MOVE (multiple) **`),
Undo takes the last copy back, and a first point picked with Ctrl held turns Copy on. Shift+click makes several grips hot,
which stretch together: a circle's centre and a quadrant move it by (10, 10) keeping its radius of 10. A grip pressed and
let go elsewhere is stretched there, as before; the whole grip edit is one undo step. `CadCommandSession.StartGripEdit`,
`IsGripEditing`, `Feed(point, shift, control)` and `CadInput.Control` are the API.

**NEW.** The presenter's Properties panel edits polylines, ellipses, hatches, dimensions, MTEXTs, block references,
viewports and multileaders with AutoCAD 2021's rows, each edit one undo step. Current vertex steps through a polyline (on
the rectangle 30 by 20, vertex 3 reads (30, 20), and Vertex Y 25 moves that corner alone), with its segment widths, Global
width, Elevation, Closed and Linetype generation. An ellipse of radii 20 and 10 given a Major radius of 40 becomes 40 by
20. A hatch takes its Type, Pattern name, Angle, Scale or Spacing, Double, Origin, Island detection and Background color.
A dimension's Lines & Arrows, Text, Fit and Primary Units rows are written as overrides and the dimension drawn again:
Precision 0.000, Suppress trailing zeros No and Dim suffix " TYP" show "300.000 TYP" on a dimension of 300. An MTEXT takes
Style, Justify, Direction, line spacing, Defined width and Columns, and its Contents row opens the MTEXT editor; an INSERT
shows each attribute by its tag and sets its value; a viewport takes Standard scale 1:50, Display locked, and a Height or
Width that keeps its scale; a multileader takes its style, leader type, landing and text. `CadPropertyRow.Choices` lists
the values of a row of a list and `CadSelection.CurrentVertex` the vertex shown.

**NEW.** MATCHPROP asks "Select destination object(s) or [Settings]:", and Settings shows the Property Settings dialog of
AutoCAD 2021 (`CadCommandSession.MatchProperties`): with Layer alone checked, a red line matched to one on Steel goes to
Steel and stays red. The special properties are copied too: a dimension's style and overrides, a text's style and height,
a hatch's pattern, angle and scale, a polyline's width and linetype generation, a viewport's scale, lock and frozen
layers, a table's style and a multileader's style. Plot style, Material, Shadow display and Center object are greyed.

**NEW.** The presenter has PEDIT (PE) and REVCLOUD, with AutoCAD 2021's prompts word for word, storing what AutoCAD
stores (both checked against AutoCAD's own console). PEDIT turns a line or an arc picked into a LWPOLYLINE, and takes
Close, Open, Join, Width, Edit vertex (Next, Previous, Break, Insert, Move, Regen, Straighten, Tangent, Width, with the
vertex marked by a cross), Fit, Spline, Decurve, Ltype gen, Reverse and Undo, one undo step a run; Multiple edits several
at once and joins them within a fuzz distance by Extend, Add or Both: the lines (0, 0) to (100, 0), (103, 0) to (103, 50)
and (100, 50) to (0, 52) joined at 4 are one polyline through (103, 0) and (103, 49.94), as in AutoCAD. Fit and Spline
make the POLYLINE AutoCAD makes, and Spline of (0, 0), (100, 0), (100, 50), (0, 80) gives AutoCAD's 9 vertices of the
curve, to 8 places. REVCLOUD clouds a rectangle, a polygon, the cursor's path or an object it replaces, with Arc length,
Style (Normal, Calligraphy) and Modify: a circle of radius 20 at an arc length of 5 is AutoCAD's 25 vertices of bulge
0.52056705, and the arc lengths are kept divided by DIMSCALE, as AutoCAD keeps them. A drawing made with every option
audits clean in AutoCAD 2021. `CadPrompt.Tracks` says a prompt follows the cursor, as Freehand does.

**FIXED.** `DxfPolyline2d.SplineFit` puts the vertices of the curve after the first vertex of the frame, where AutoCAD 2021
keeps them, rather than after the last; `CurveFit` reads the width at the joint of a tapered segment off the two arcs'
lengths, as AutoCAD does: a segment from width 1 to 3 whose arcs are 25.723 and 24.733 long is 2.0197 wide at its joint,
not 2.

**NEW.** The presenter takes AutoCAD's point modifiers at any prompt for a point, typed or from the top of the
Shift+right-click menu (Temporary track point, From, Mid Between 2 Points, Point Filters with .X .Y .Z .XY .XZ .YZ), with
AutoCAD's prompts. LINE with FROM, the base point 100,50 and the offset `@30,20` starts at (130, 70); M2P (or MTP) between
(0, 0) and (100, 40) gives (50, 20); `.X` of (30, 999) with (888, 40) at `(need YZ):` gives (30, 40), and `40,3` typed
there gives Y 40; TT at the midpoint (2 000, 0) of a wall holds the cursor at (2 000.3, 700) on (2 000, 700) while OTRACK
is on. Coordinates are the UCS's, Z is asked and left out, each point a modifier asks may be snapped, and modifiers nest,
as FROM with M2P for its base point. `CadCommandSession.StartPointModifier`, `TemporaryTrackPoints` and
`ObjectSnap.AcquireTrackPoint` are the API; `CadMenuItemViewModel.Items` holds a submenu.

**NEW.** The presenter's ARC, CIRCLE, PLINE, TEXT and MTEXT take every option AutoCAD 2021 offers, with its prompts word
for word. ARC draws by Start, Center, End, Angle or chord Length, the Center first, by Start, End and Angle, Direction or
Radius, and goes on tangent from the last line or arc at Enter: after a line from (0, 0) to (10, 0), Enter and (20, 10)
give the quarter arc of radius 10 about (10, 10). A point picked with Ctrl held draws the arc clockwise. CIRCLE takes Ttr,
a circle of radius 15 touching two lines that meet square at (0, 0) centred on (15, 15), and Tan, Tan, Tan as 3P with
`_tan` before each pick, the circle touching three sides of a square 40 across of radius 20. PLINE takes Width, Halfwidth
and Length, the widths kept per vertex and PLINEWID set to the ending width, and in arc mode Angle, CEnter, Direction,
Radius and Second pt. TEXT takes Justify, its fifteen justifications, and Style, and Enter at its first prompt goes on
five thirds of the height below the last text. MTEXT takes Height, Justify, Line spacing, Rotation, Style, Width and
Columns at its corner, static and dynamic columns stored as AutoCAD stores them.

**FIXED.** GeometryHelper.Dxf.Viewer: MIRROR now follows the drawing's MIRRTEXT, as AutoCAD 2021 does. At 0, AutoCAD's
default, a TEXT, an MTEXT and the attributes of an INSERT came out written backwards; they now stay readable, the box of
their words mirrored and their reading kept within the half of the circle it was in, as probed in AutoCAD 2021's console
over 96 turns and mirror lines. "ABC" 2.5 high turned 30 degrees at the origin, mirrored across the line y = 10, reads at
330 degrees from (-1.25, 17.835), where AutoCAD puts it; turned 120 degrees and mirrored across an upright it reads at 240.
A text aligned between (0, 0) and (20, 0) mirrored across x = 30 runs from (40, 0) to (60, 0), and an MTEXT attached top
left at the origin, mirrored across x = 40, is attached top right at (80, 0). At 1 they are mirrored with the rest, as before.

**NEW.** GeometryHelper.Dxf.Viewer: "Select objects:" takes the options of AutoCAD 2021: Window, Crossing and BOX by two
corners, Fence, WPolygon and CPolygon by points until Enter, Last, Previous, Group by name, Remove and Add, Undo, Multiple,
SIngle and AUto, beside ALL; Shift held takes out what a click, a box or a lasso picks, "1 found, 1 removed, 2 total";
anything else is "*Invalid selection*" with the options listed. Window, Crossing, BOX, Fence and the polygons draw their
band on the canvas (`CadPrompt.Band`), and `PickIndex.Fence`, `WindowPolygon` and `CrossingPolygon` answer them: over
100 000 lines a fence took 28 milliseconds and a polygon round them all 63 to 69. `FeedSelection` takes `remove` to take
objects out.

**NEW.** GeometryHelper DXF, `GeometryHelper.Dxf.App.exe`, the presenter's own app: the window of the 2D presenter with no
model drawing, built only on `GeometryHelper.Dxf.Viewer` and its controls, to open, edit and plot DXF drawings in place of
AutoCAD. `GeometryHelper.Dxf.App.exe plan.dxf` opens a drawing, as a file association does; its command plugins are kept in
`%AppData%\GeometryHelper\Dxf`, apart from GeometryHelper Drawing's. Both apps now start through `CadAppHost` and show
`CadAppWindow`, both in `GeometryHelper.Dxf.Viewer.Wpf`, and take the same `--screenshot` options; GeometryHelper Drawing
adds its own (`--new-drawing`, `--pick`, `--hand-edit` and the dialogs of the model drawing) through the host's protected
members. Its pictures are the same as before, pixel for pixel but for the clock of a log line, on 34 combinations of
options. `CadWorkspace.AppName` puts
the app's name in the title bar and on the start screen, "GeometryHelper Drawing" unless a host gives its own.

**NEW.** The presenter has PNGOUT, JPGOUT and BMPOUT, as AutoCAD 2021's: the file from the Create Raster File dialog (on
the command line with no window, `Enter file name <C:\Plans\plan.png>:`), then `Select objects or <all objects and
viewports>:`. Enter writes the drawing area as it is shown, at its size in pixels, on its background, with its grid and
without the cursor, grips, UCS icon or selection highlight; objects selected are written alone in the same view. A PNG is
8-bit RGB, a JPEG of quality 90, a bitmap of 24 bits; the same view gives the same bytes. Nothing is printed and nothing
is undone. A 1 920 by 1 080 PNG of a drawing of 100 000 entities is 1.2 MB, written in 0.25 s (.NET Framework 4.8; 0.20
s on .NET 10). `CadImageJob` is what the command hands the window, and `CadRasterWriter` writes one with no window.

**NEW.** The presenter's ribbon has an Insert tab, after a host's tabs and before Annotate, as AutoCAD 2021's for what is
built: Block (Insert, whose arrow drops a gallery of the drawing's blocks, a click on a tile starting INSERT with that
block; Edit Attribute), Block Definition (Create Block and Write Block, Define Attributes, Set Base Point) and Reference
(Attach DXF and Attach Image, Clip, External References, a box that shows and sets IMAGEFRAME, Transparency). What is not
built is greyed with its reason. Annotate and View now shrink as Home does: in a window 1 280 wide every panel of Insert
(852 pixels), Annotate (992) and View (670) is full; Annotate's panels go medium below 1 016, their style boxes 84 pixels
wide instead of 118, and View's below 694. View is now a `RibbonStrip` of the panels Navigate, Views, Interface and
Drafting, each button bound as before. A collapsed panel's name of two words, as Block Definition, goes on two lines.

**NEW.** The presenter's block commands come closer to AutoCAD 2021's. The Enhanced Attribute Editor shows a value on the
canvas as it is typed; Apply keeps it with the editor open, and Cancel takes back only what was typed after the last
Apply, the whole edit one undo step (`CadAttributeEditRequest.Preview` and `Apply`). INSERT drags the block with the
cursor while it asks the insertion point, the scale and the rotation (`CadPrompt.Drag`, a `CadBlockDrag`): a block drawn
about (100, 70) dragged to (500, 500) has its circle about (100, 100) drawn about (500, 530), and the INSERT is then placed
exactly as dragged. -ATTEDIT takes the one attribute clicked, not every attribute of its INSERT, and marks the attribute it
is editing with a cross (`CadPrompt.Mark`). ATTSYNC (?, Name, Select) brings every INSERT of a block, those nested in other
blocks too, in line with its attribute definitions in one undo step: an attribute for each definition in the block's
order, the values of the tags kept, those of tags dropped removed, each placed from its definition. Block Definition
draws the objects selected for a new block. Inserting a block of another drawing whose name the drawing has asks "Block
GB already exists. Redefine?" in the window, `Block "GB" already exists. Redefine it? [Yes/No] <No>:` on the command line
of -INSERT; Yes gives the drawing's block the other's entities, base point and unit. AutoCAD 2021 audited drawings left by
ATTSYNC, -ATTEDIT and a redefinition with no error.

**NEW.** The presenter makes ordinate and jogged dimensions and has AutoCAD's DIM command. DIMORDINATE (DOR) measures a
point from the origin of the UCS along X or Y as its leader runs (Xdatum, Ydatum, Mtext, Text, Angle): (300, 40) with its
leader ending at (310, 80) is an X datum of 300, its leader jogged two arrowheads before its end, as AutoCAD 2021 draws
it. DIMCONTINUE, DIMBASELINE and QDIM's Ordinate go on from an ordinate dimension. DIMJOGLINE (DJL) puts a jog in a linear
dimension's line and Remove takes it out; the jog is kept in the dimension's extended data as AutoCAD keeps it
(ACAD_DSTYLE_DIMJAG_POSITION), and AutoCAD 2021 draws it where the presenter put it. DIM dimensions what is clicked, a
line (or two, for their angle), a circle's diameter, an arc's radius, or two points, and its options run DIMANGULAR,
DIMBASELINE, DIMCONTINUE and DIMORDINATE, align and distribute dimensions, and set the layer new ones go on. AutoCAD 2021
audited drawings of R2000, R2013 and R2018 with 51 dimensions of every kind made so with no error.

**CHANGED.** A linear dimension's picture is laid out from its style as AutoCAD 2021 lays it out, where its words always
stood above the line before: DIMTAD 0 puts them on the line, broken about them by DIMGAP, 2 on the side away from the
points measured and 4 below; DIMJUST puts them next to or over an extension line; DIMTIX, DIMSOXD, DIMATFIT and DIMTOFL
move the words or the arrowheads outside where both do not fit, and DIMTIH and DIMTOH keep the words level. Against the
pictures AutoCAD 2021 draws itself for 86 linear and ordinate dimensions, they differ only by the size of the words,
which the presenter takes from their height where it finds no font (up to 0.5), and by AutoCAD starting the line at the
back of a closed arrowhead. A drawing made new in code, whose Standard style has AutoCAD's DIMTAD of 0, now has its
dimension lines broken about the words.

**NEW.** The presenter attaches and shows external references and images, as AutoCAD 2021 does: XATTACH and IMAGEATTACH
(the Attach External Reference and Attach Image dialogs), the External References palette (XREF, ER, IMAGE), -XREF,
-IMAGE, IMAGECLIP, IMAGEFRAME and TRANSPARENCY. A DXF referenced is read and drawn under its INSERT, its layers named after
it (`grid|GRID`), its attachments followed and its overlays not, and never written into the drawing; one changed on disk
stands Needs reloading until Reload. A PNG, JPEG or BMP is placed at AutoCAD's size, 40 by 30 pixels at 3 779 pixels a
metre 10.584811 by 7.938608 mm, and drawn by its pixels, decoded off the window's thread. A drawing of 100 000 entities
shown as a reference is read and drawn in 0.90 s, where opening it alone takes 0.87 s. `DxfBlockTableRecord.PathName`
is now set as well as read.

**NEW.** Tables made, styled and edited as in AutoCAD 2021. The core library reads and writes table styles
(`DxfTableStyle`, `db.TableStyleDictionary.SetAt(name, new DxfTableStyle())`, a table's `TableStyle` now set as well),
makes tables in code (`new DxfTable(position, rows, columns, rowHeight, columnWidth)`, appended in a new `*T1` block, its
title merged across) and draws a table anew in its block from its cells (`DxfTable.RecomputeTableBlock`): fills, grid
lines by their looks, the text of each cell at its alignment. In the presenter, TABLE (TB) shows the Insert Table
dialog and opens the title for typing, Tab, Shift+Tab and Enter moving on: five columns 63.5 wide and four rows of data
of AutoCAD's Standard make a table 317.5 by 56. -TABLE asks on the command line, TABLEDIT, a double click or F2 types in a
cell, and TABLESTYLE (TS) shows the Table Style dialog with New and Modify (General, Text, Borders). A click in a cell
selects it with the Table Cell tab: rows and columns put in and taken out, merge and unmerge, alignment, fill and
borders, each one undo step; a cell of a table of 100 rows of 20 columns is written and drawn in about 50 ms. AutoCAD
2021 audited drawings of R2000, R2013 and R2018 made so with no error and lists their tables as tables. A table turned
is now drawn where it stands: its block was turned twice.

**NEW.** `GeometryHelper.Dxf.Viewer`, `GeometryHelper.Dxf.Viewer.Wpf` and the `GeometryHelper.Drawing.App` window are a 2D
presenter for any DXF, built as AutoCAD behaves (not packed yet). A file opens as a `CadDocument` drawn from display
lists through SkiaSharp: 100 000 entities open in 0.9 s, a click picks in 0.007 ms and a window of 1 000 entities draws
in under 1 ms. It edits as AutoCAD does, with a command line that has AutoCAD's prompts, names and aliases, object snaps,
grips, Properties edited in place, a layer manager, and undo and redo that give the start file back byte for byte.
Command plugins load and unload at run time (collectible load contexts on .NET 8), and global settings are kept as JSON:
colours, crosshair size, text height and autosave copies that never touch the file. CLOSE and the x on a row of
the file list close a drawing, asking first about unsaved changes. An owner (`CadOwnerRegistry`, tag `GH_OWNER`) keeps
control of the entities it wrote: an edit of one goes to its owner in the same undo step, or is refused in the words the
command line shows.

**CHANGED.** The presenter draws again only what an edit changed. After an edit, an undo or a redo the display list of a
space is patched from the one before: the entities the edit names, those that read a layer switched off, frozen or
recoloured, and those that share a dimension's picture or the words of a leader are drawn anew, and every other entity
keeps its items, the list item for item the one a REGEN makes. On a drawing of a million entities (3.9 million items)
under .NET 8, a line moved is drawn again in 0.46 s instead of 9.6 s and its edit kept for undo in 23 ms instead of
567 ms (the drawing is now compressed for undo on a worker thread), an undo of it is drawn in 0.45 s, and a layer of a
third of the entities switched off in 0.53 s; under .NET Framework 4.8 the line takes 0.59 s instead of 10.6 s and the
layer 0.69 s instead of 8.3 s. The pick index is made in half the time (0.26 s instead of 0.51 s), a block's entities are
listed once a regeneration rather than once an INSERT, and a style of no dashes is shared, so the first display of the
million is 8 to 17 per cent faster and holds 230 MB less. Moving 100 000 of them at once under .NET Framework 4.8 is
drawn again in 1.4 s instead of 9.9 s, though the edit itself takes 6.4 s instead of 4.3 s, the old list being kept until
it is patched. Under .NET 8 with a heap of 4 GB the old presenter ran out at the second edit of a single line; the new one
goes on through that move (4.3 s, drawn in 1.1 s), its undo and a layer switched off and on.

**CHANGED.** The presenter's `PickIndex` hands an XLINE or a RAY to a pick, a snap or a selection only where it passes
through the box asked about or near it, where before every one was handed to every pick and snap. On a drawing of
100 000 lines, arcs and circles with 200 xlines and rays among them, 20 000 snaps take 40 ms instead of 119 to 136 ms and
20 000 picks 20 ms instead of 73 to 98 ms under .NET 8, and 60 ms instead of 220 to 243 ms and 27 ms instead of 108 to
119 ms under .NET Framework 4.8; with no xline they take 22 to 28 ms and 14 to 19 ms. What is picked, snapped and selected
is the same, bit for bit. `ItemsNear` now leaves out an xline or ray that passes the box by further than the box's own
size, unless it is drawn for a block reference, whose insertion point a snap takes from any of its items.

**NEW.** The presenter selects and tidies as AutoCAD 2021 does. QSELECT shows Quick Select over the rows of the
Properties panel (`CadQuickSelect`): Line, Layer, `= Equals`, WALLS selects the lines of WALLS and says `312 item(s)
selected.`, in 0.25 s over 100 000 objects. FIND shows Find and Replace (`CadFind`) over TEXT, MTEXT, attribute values,
dimension and multileader text and table cells, with match case, whole words, wildcards, blocks and hidden items; it
replaces in an MTEXT its words alone, `{\fArial|b1;Bolts M20}` becoming `{\fArial|b1;Bolts M24}`, and Replace All is one
undo step. DRAWORDER (DR) and TEXTTOFRONT write AutoCAD's sort table, which the display follows, through the new
`DxfDrawOrderTable` of the core library (`DxfBlockTableRecord.DrawOrderTable`, `CreateDrawOrderTable`). GROUP (G), -GROUP
and UNGROUP make and change the GROUP objects of ACAD_GROUP, and with PICKSTYLE 1 (Ctrl+Shift+A switches it) a pick of
one member takes its group. -PURGE (PU) deletes the unused blocks, dimension, text, multileader and table styles,
layers, linetypes and plot styles, the lines of no length and the empty texts, asking of each name where told to, and
never what AutoCAD keeps: layer 0, Defpoints, Standard, ByLayer, ByBlock, Continuous or the current ones. Where two or
more objects lie under a click, the list of them is shown near the cursor (SELECTIONCYCLING 2, as AutoCAD's default), and
Ctrl+W switches it off and on as AutoCAD's does; Ctrl+W no longer closes the drawing, which CLOSE and the x on its row do.

**NEW.** The presenter snaps to XLINE and RAY as AutoCAD 2021 does, which it did not at all: Nearest, Perpendicular and
Intersection along their whole length, with lines, arcs, circles and each other, and none behind the base point of a ray;
the base point of a RAY is its endpoint and the root point of an XLINE its midpoint; Extension runs back from a ray's base
point, and Parallel and Apparent Intersection take them as lines. An xline along (1, 1) through the origin and the line
from (100, 0) to (100, 500) give the intersection (100, 100). The ghost MOVE, COPY, ROTATE, SCALE and MIRROR drag draws
them too, across the view as the canvas does. Over 100 000 objects and 200 xlines and rays, a snap takes 0.012 ms.

**CHANGED.** A pick in the presenter takes the object drawn on top among those about as near the cursor, as AutoCAD's does
while SORTENTS holds its bit 1 (its default 127 does): two lines crossing under the cursor give the one drawn later, or
the one DRAWORDER put in front, where the handle decided before. Objects within a tenth of the pickbox of the nearest
count as as near: with a pickbox of 3, a line under the cursor still comes before one on top of it 0.6 away. The Selection
list of SELECTIONCYCLING and Shift+Space cycling run in the same order, the one on top first.

**NEW.** The presenter's ribbon is laid out and shrinks as AutoCAD 2021's. Home has Draw, Modify, Annotation, Layers,
Properties, Groups, Utilities, Clipboard and View; as the window narrows, the steps of each tab's `RibbonStrip` are taken
in turn until its panels fit, and given back as it widens: at 1 600 pixels View, Clipboard, Utilities and Groups are each
one button that drops the full panel below it, with a pin, and Properties, Layers and Draw drop the labels of their small
buttons; Modify never shrinks. Modify gains the Draw Order split (Bring to Front, Send to Back, Bring Above Objects, Send
Under Objects, Bring Text to Front), which gives DRAWORDER its option once the objects are picked
(`MainViewModel.StartCommandWithOptionCommand`); Groups has Group, Ungroup and Group Selection, pressed while PICKSTYLE is
1 (`MainViewModel.IsGroupSelectionOn`); Clipboard the Paste split, Cut, Copy and Copy with Base Point; Utilities Quick
Select. Annotate's Dimensions panel has the Linear and Continue splits, Quick, Adjust Space, Break and a slide-out with
Oblique and Text Angle. The Text Editor tab fits 1 600 pixels, Spell Check and Tools collapsed and Close the last panel.

**NEW.** `GeometryHelper.Drawing` makes a model drawing, in the manner of Tekla's, from a `DrawingSource` of
`DrawingObject` bodies: views along the axes and an isometric view with exact hidden-line projection (2 000 objects in
the front view take 0.3 s), sections with ANSI31 hatches, details, dimensions by rule, marks that keep clear of the
symbols and dimensions, centre lines, weld symbols to ISO 2553, section, detail and level marks. A drawing is one DXF that
also carries its own document, so it opens again with its settings. `Drawing.Update(source, settings)` makes it again with
other settings or a changed model and keeps what was moved or drawn by hand; `Prepare` makes the update on a thread of
its own and `Apply` writes it as one undo step. `ReDimension`, `ReMark`, `AddView`, `AddSection` (a cut along a line drawn
in a view) and `AddDetail` change the settings for one view and are kept through later updates; `ReCentreLines` makes the
drawing again with the centre lines of one view drawn anew. A general arrangement
of 1 000 members is drawn in 1.2 s. The dialogs, the Drawing tab and the commands VIEWSECTION and VIEWDETAIL are in
`GeometryHelper.Drawing.Wpf`.

**NEW.** Weld symbols are placed clear of the view's dimensions, centre lines, section, detail and level marks and of one
another, as marks are: `WeldSymbol.Find` given the dimensions and what else to keep clear of tries the knee up and right of
the weld, up and left, down and right and down and left, nearest first, and the arrow always ends at the weld's middle. A
symbol left of its weld runs its reference line to the left. The fillet a6 of TC5 seen from the front at 1:10, which stood over
a dimension line and a dimension's text, now stands under the base plate; the 36 symbols of TC5 in its front, top and right
views at 1:2 to 1:20 have no overlap. `WeldSymbol.FindOverlaps` lists what overlaps, and the marks of a view are placed after
the symbols and keep clear of them.

**NEW.** A model drawing made in the app is saved at once, once the global settings are applied, into their save folder
(the folder `GeometryHelper Drawing` in the user's Documents where it is empty), named by its number: `B-101.dxf`, or
`B-101 (2).dxf` where that name is taken, so that no file is written over. It then has a file, which Save writes to, and
autosave keeps copies beside it.

**NEW.** TEXT and MTEXT write the width factor of the global settings (0.8 by default), and the dimension commands DIMLINEAR,
DIMALIGNED, DIMRADIUS, DIMDIAMETER and DIMANGULAR write the text of a dimension at the text height of the global settings,
kept as a DIMTXT override of the dimension, so that a CAD that draws it again from its style draws its text as high.

**CHANGED.** The presenter's controls draw through SkiaSharp 4.155.0, where they used 3.119.1.

**FIXED.** A part or bolt mark no longer stands on the arrows of a short dimension. A dimension line shorter than two
arrows has its arrows drawn outside it, and marks now keep clear of the line run on by one arrow at each end, as weld
symbols already did: the line of a 25 dimension at 1:20 with text 2.5 high is kept clear of 50 beyond each end. Of 114
marks on five test bodies, 18 moved, all in views with short dimensions.

**FIXED.** The 2D presenter draws an MTEXT at the width factor its words open with (`\W0.8000;` for 0.8, as the T
command and AutoCAD write it) and measures it at that width, as it does a TEXT.

**FIXED.** A model drawing saved and opened again from its DXF, by Open or from the file list, is a model drawing again,
not a plain read-only DXF: its owner is registered, so a mark can be moved and a projected line cannot, it is listed under
"Opened from a file · model drawings" with the Drawing tab, and saving writes to the file it came from. Update from model
asks for the source, since the file does not keep it, and says in words when none is at hand or when the source holds none
of the drawing's objects. **NEW.** `Drawing.Open(CadDocument, DrawingSource, Tolerance)` and
`CadWorkspaceExtension.DocumentOpened`.

**FIXED.** The Text override of a dimension can be set in Properties, as in AutoCAD: `<> TYP` shows `3000 TYP` on a
dimension of 3 000, and an empty text shows the measurement. On a model drawing it is a hand edit and one undo step.

**FIXED.** PLUGINRELOAD no longer asks again whether to load a plugin from a folder outside the trusted folders: a yes is
kept for that folder for the session, never on disk; a no is asked again.

**FIXED.** Marks and weld symbols keep clear of the full width of every dimension arrow, inside a dimension line as well
as outside a short one. Each arrow is taken as the box round it, as long as the dimension's text is high and a third of
that wide. `Marker.FindOverlaps` and `WeldSymbol.FindOverlaps` name "over an arrow of dimension D-3". With the presets' text
sizes one mark and one weld symbol of the test bodies moved; with a dimension text four times the marks', 19 of 120 marks and
6 of 72 weld symbols moved off arrows.

**CHANGED.** A mark that the placing leaves out is tried once more, keeping only its text, not its margin, clear of the
centre lines and cut lines, as `FindOverlaps` counts them. In the general arrangement of 1 000 members 191 of 200 marks now
find a place instead of 185, and every other mark is the same to the bit; two of the six marks rescued have leaders of 10 and
15 m (51 and 77 mm on the paper).

**FIXED.** The presenter draws an MTEXT at its text style's width factor when its words set none of their own, as it
already took the style's slant. ROMANS 2.5 high in a style of width 0.75 is drawn 6.75 wide, and `\W1.2x;` makes it 0.9.

**FIXED.** VIEWSECTION and VIEWDETAIL on a model drawing opened again from its file said "No view of the drawing is
there."; they now make it again from the source its file names first, or say to pick the source in Update from model.

**FIXED.** A model drawing just made and saved into the save folder was listed twice: as the drawing, and as its file once
another file of the folder was opened, or once it was saved with Save and shown again. It is now one row.

**FIXED.** Properties of a model drawing opened again said "Opened from a file" for its source though its file names it; it
now says, for example, "Test bodies (from file)" until the drawing is made again. The out-of-date dot now shows on such a
drawing too, checked in the source its file names without changing it.

**NEW.** The presenter's clipboard as AutoCAD's: COPYCLIP (Ctrl+C), CUTCLIP (Ctrl+X), COPYBASE (Ctrl+Shift+C), PASTECLIP
(Ctrl+V), PASTEBLOCK (Ctrl+Shift+V), PASTEORIG and PASTESPEC, between drawings and between copies of the app, text from other
programs pasted as MTEXT; a line on layer Steel pasted into a drawing whose Steel is blue is drawn blue. TEXTEDIT (ED, or a
double click) edits a TEXT in a box over it, each text one undo step; MTEXT and MTEDIT open the MTEXT editor with its Text
Editor tab, writing AutoCAD's codes: "AB CD" with CD made bold in Arial writes `AB {\fArial|b1|i0|c0|p34;CD}`. An MTEXT of
several looks is drawn run by run, with bold, italic, colours, heights, underline, overline and strikethrough. Word wrap at
the MTEXT width, stacks, columns, fields, bullets and spell check are not built yet. AutoCAD 2021's AUDIT finds nothing in a
drawing changed by all of them.

**NEW.** The presenter makes and places blocks as AutoCAD 2021 does: BLOCK (B) with the Block Definition dialog and -BLOCK,
INSERT (I) with the Blocks palette (Current drawing, Recent, Other drawing) and -INSERT with its attribute prompts, WBLOCK (W)
writing a new metric DXF and -WBLOCK, ATTDEF (ATT) and -ATTDEF, the Enhanced Attribute Editor (EATTEDIT, ATTEDIT, or a double
click on a block with attributes) and -ATTEDIT, and BASE; each run is one undo step. A grid bubble of a circle 10 across, a
line and the attribute GRID, made a block with its base point at the foot of the line and inserted at (500, 500) at scale 2
and turned 90 degrees, asks "Grid letter <A>:"; exploding an INSERT scaled 2 by 1 turns its circle into an ellipse with a
major radius of 20. EXPLODE refuses a block that does not allow exploding. BATTMAN, ATTSYNC and the ATTDIA dialog are not
built yet. AutoCAD 2021's AUDIT finds nothing in a drawing made by these commands or in the files WBLOCK writes.

**NEW.** The presenter's last object snaps, as AutoCAD 2021's: Extension, Parallel, Apparent Intersection, Insertion and
object snap tracking (OTRACK, F11, on the status bar before OSNAP and in Drafting Settings). A rest of half a second on a
point acquires it, seven at most: the end of a line from (0, 0) to (100, 0) rested on gives (150, 0) along its run to a
cursor at (150, 0.4), and with tracking, (100, 30) above it and the crossing (100, 50) with the level line of a second end
at (200, 50), the paths drawn dotted. Parallel holds the rubber band along a line rested on; Apparent Intersection gives
(100, 0) for two lines that stop short of it, or picks one object with a click and takes where the next would meet it;
Insertion takes an INSERT's point from any of its lines, and the point of a TEXT, an MTEXT or an attribute. EXT, PAR, APP,
APPINT and INS typed take them for one point. **CHANGED.** `CadSnapModes` carries AutoCAD's OSMODE values (Node 8, Tangent
256, Nearest 512, Geometric Center 1024); the settings file keeps snaps by name and reads as before. A snap over 100 000
entities with every mode on and seven points acquired takes 6 microseconds; with the modes of before, 1 microsecond, as
before.

**NEW.** The presenter plots to PDF as AutoCAD 2021 does: PLOT (Ctrl+P) shows the Plot dialog, expanded, PAGESETUP the same
dialog as Page Setup, PREVIEW the plot preview over the whole window (pan, zoom in real time, zoom window, zoom original, a
shortcut menu, ESC or ENTER to leave), and -PLOT asks AutoCAD's prompts on the command line. Its plotter, GeometryHelper
PDF, writes one page of the sheet's exact size through SkiaSharp, in vectors: ISO A3 on its side is 1 190.55 by 841.89
points, lines keep their lineweights, solids and hatches are filled, and the text of a TrueType font stays text a reader
finds. The window from (120, 60) to (7 620, 5 360) at 1:20, centred on A3, is 375 by 265, moved 17.5 right and 11 up of the
printable area's corner. ISO A0 to A4 and ANSI A to E, Display, Extents, Limits, Window and Layout, Fit to paper or a scale
of the list (1:1 to 1:1000 and the drawing's own), the offset or centring, landscape, portrait and upside down, and the
tables Monochrome, Grayscale and None made in code. Apply to Layout and Save changes to layout write the page setup into
the LAYOUT's plot settings as AutoCAD does, one undo step, and it reads back after saving; AutoCAD 2021's AUDIT finds
nothing in a drawing saved after it. The printers of Windows are listed but greyed: printing to them is not built yet.
`DxfPlotSettings` sets, as AutoCAD's `PlotSettingsValidator` does, the plotter's name, the plot style table, the plot type
and window, the custom and standard scales, and the flags of centring, plot styles and lineweights.

**NEW.** The presenter edits dimensions as AutoCAD 2021 does: DIMCONTINUE (DCO) and DIMBASELINE (DBA) go on from the last
linear or angular dimension, or one selected, on the same line or DIMDLI further out, with Undo inside; QDIM dimensions the
ends of lines and polylines and the centres of circles in a row, staggered, from a base or as radii and diameters; DIMEDIT
(DED) gives dimensions a new text, turns their words, puts them home or slants their extension lines; DIMTEDIT moves the
words, the dimension line going with them where DIMTMOVE is 0; DIMSPACE spaces parallel dimensions a value apart or by twice
their text height; DIMBREAK gaps the lines of a dimension where lines, polylines, circles and arcs cross them. A row of 100
and 150 continued from a dimension of 100 between (0, 0) and (100, 0) stands on its line 30 below. Each run is one undo
step, and an owned dimension of a model drawing is refused in words. A dimension drawn again keeps its moved and turned
words and its oblique angle. A break of DIMBREAK is drawn into the dimension's picture only, not stored as AutoCAD stores
it, so a dimension drawn again is whole; DIMJOGLINE and ordinate dimensions are not made yet. AutoCAD 2021's AUDIT finds
nothing in a drawing edited by all of them.

**FIXED.** The presenter draws XLINE and RAY, which it drew not at all: an XLINE across the whole view both ways from its
base point and a RAY on from it, as AutoCAD draws them, picked and highlighted anywhere along them like a line (a new
`DisplayRay2`). ZOOM Extents takes their base points only, as AutoCAD's does: a line 100 by 50 from the origin and an XLINE
through (200, 300) zoom to the box from (0, 0) to (200, 300). A crossing box or lasso they run through takes them; a window
never does.

**NEW.** Home › Modify of the presenter's ribbon is laid out as AutoCAD 2021's panel, with buttons for the commands it had
only on the command line: Stretch, Trim with Extend on its arrow, Fillet with Chamfer, Array (rectangular) with Polar Array;
Erase, Explode and Offset by their icons; a row of Break, Join, Lengthen and Align under them; and its title drops Break at
Point, Break, Join, Lengthen and Align, as AutoCAD's panel slides out. A split button shows the command last picked from its
arrow: Extend picked once from Trim's leaves it reading Extend and running EXTEND until Trim is picked again.

**NEW.** The presenter's TRIM (TR), EXTEND (EX), BREAK (BR), BREAKATPOINT, JOIN (J) and LENGTHEN (LEN) as AutoCAD 2021's:
TRIM and EXTEND in the Quick and Standard modes with Fence, Crossing, Edge, eRase and Undo, Shift-pick switching between the
two; each command is one undo step (a line from (0, 0) to (100, 0) crossed by a line at x = 40 and picked at (80, 0) is
trimmed to the line from (0, 0) to (40, 0)). AutoCAD 2021's AUDIT finds nothing in a drawing edited by all of them.

**NEW.** The presenter's modify commands as AutoCAD 2021's: OFFSET (O), FILLET (F), CHAMFER (CHA), STRETCH (S), ARRAY (AR),
ARRAYRECT, ARRAYPOLAR, -ARRAY (-AR) and ALIGN (AL), each one undo step, with their options and their Undo inside (a line from
(0, 0) to (100, 0) offset by 10 to the side of (50, -3) gives the line from (0, -10) to (100, -10)); entities of a model
drawing are refused in words, and copies of them are the presenter's own. Path and associative arrays are not built.
AutoCAD 2021's AUDIT finds nothing in a drawing edited by all of them.

**NEW.** PTYPE (DDPTYPE) shows AutoCAD's Point Style dialog: twenty tiles of PDMODE, each drawn as the canvas draws a point of
that mode, the point size, and whether it is a share of the screen (PDSIZE below nought) or a size in drawing units; choosing
the plus with a circle at 2.5 units writes PDMODE 34 and PDSIZE 2.5 as one undo step.

**NEW.** The presenter's NEW starts from its own metric template, `CadTemplate.CreateMetric()`: the settings of acadiso.dwt
written into the header, so that AutoCAD opens the drawing as metric too, with ISO-25 current (arrowheads and text 2.5, a
decimal comma) and the text styles Standard and Annotative in Arial, as AutoCAD 2021's has them.

**NEW.** The presenter's 20 arrowheads, Closed and Origin indicator 2 among them, make AutoCAD 2021's blocks entity for entity
(`_Dot` a polyline 0.5 wide of two half circles) the first time a dimension style or a multileader uses them; AutoCAD's AUDIT
finds no error in a drawing using all 19. **CHANGED.** Arrowhead blocks are named as AutoCAD names them (`_ArchTick`, not
`_ARCHTICK`).

**NEW.** PTYPE (DDPTYPE), PDMODE and PDSIZE, and points drawn as PDMODE says, in drawing units or as a share of the view;
LWPOLYLINEs with a width are drawn filled, as AutoCAD fills them.

**NEW.** The Hatch dialog has a Pattern file row: Browse… adds a .pat file's patterns after the 48 built-in ones (a built-in
name wins). Load or Reload Linetypes shows the built-in GeometryHelper.lin, then the linetypes of a .lin file chosen by Browse….
A drawing opened whose text styles name SHX files not found shows Missing SHX files: the files, the styles that use them and
what they are drawn in, a replacement to pick for each or FONTALT kept (Global settings' `ShowMissingShxDialog`, on by default).
**FIXED.** `-STYLE` and the Text Style dialog take the built-in font `ghsimplex` as an SHX font, not as a TrueType family.

**CHANGED.** Close is orange-red in the WPF apps, as designed: the footer Close of the Layer manager, Plugin manager, plugin
settings, Text style, Dimension style manager and Compare dimension styles is filled #C2410C, the title-bar cross of every
dialog and of the main window is drawn #F26B3A and fills orange-red under the mouse, and the close cross of an open drawing in
the DRAWINGS list is #F26B3A, #FF9A6E on the current row so it keeps 3 to 1 contrast on #1F4E79.

**NEW.** The presenter carries one font of its own, GeometryHelper Simplex (`CadFonts.BuiltIn`, named `ghsimplex`): the Roman
Simplex letters of the Hershey fonts (U.S. National Bureau of Standards, 1967; notice in the project's THIRD-PARTY-NOTICES.md),
98 letters with the degree, plus-minus and diameter signs, drawn as an SHX font is, its capitals as high as the text. It is
the default replacement font. No folder of AutoCAD's is looked for any more (`CadFonts.DefaultFolders` is empty), and a list
saved before keeps what it holds. FONTALT takes a big-font half (`simplex.shx,gbcbig.shx`), Global settings > Fonts takes a
font mapping file (`FontMapFile`, read by `CadFonts.ReadFontMap`), and the SHX files missing are said once in one line, as
AutoCAD lists them (`CadDocument.FindMissingFonts`, `CadFonts.WithSubstitution`).

**NEW.** The Text Style dialog (STYLE, ST, -STYLE), the Dimension Style Manager (DIMSTYLE, D, DDIM) with its New, Modify,
Override and Compare dialogs, and Style and Height boxes on the Annotate tab, made from the design. Each field of a dimension
style shows the variable it sets, and a style changed redraws the dimensions drawn in it, as one undo step. AutoCAD 2021's
AUDIT finds nothing in a drawing of 4 new text styles, 4 new dimension styles and their dimensions. **FIXED.** The text style
Standard keeps its name, as AutoCAD keeps it.

**NEW.** Select Color's True Color tab picks in the HSL model, on a field of hue and saturation and a luminance bar, or in the
RGB model, on a bar for each of red, green and blue, as AutoCAD's does.

**NEW.** The presenter makes linetypes and hatches: LINETYPE with the Linetype Manager, -LINETYPE, CELTYPE, LTSCALE and
CELTSCALE; HATCH by internal point or by objects, with the Normal, Outer and Ignore island styles, solid and gradient;
HATCHEDIT; BOUNDARY. Its own linetypes (GeometryHelper.lin, 39) carry the names AutoCAD users type: BORDER, CENTER, DASHDOT,
DASHED, DIVIDE, DOT, HIDDEN and PHANTOM, each with a half size (DASHED2, a dash 3 and a gap of 0.75) and a double size, and
ACAD_ISO02W100 to ACAD_ISO15W100 drawn to ISO 128 for a pen of 1.0 mm. Its own patterns (48) are ANSI31 to ANSI38, the ISO and
basic ones, and its own drawings under AR-B816, AR-BRSTD (bricks 200 by 100, joints of 10), AR-CONC, AR-HBONE, AR-PARQ1,
AR-RROOF, AR-RSHKE and AR-SAND, at real size in millimetres. They are the default; a .lin or .pat is read only from a path or
folder the user gives, and AutoCAD's own files are never looked for. AutoCAD 2021's AUDIT finds nothing in a drawing of these
linetypes and seven kinds of hatch. **NEW.** `DxfHatch.SetPatternDefinitions` and `DxfHatch.SetAssociatedObjects`.

**NEW.** The presenter draws text in its style's SHX font, read from the drawing's folder and the folders listed in Global settings, never shipped:
shapes, unifont and big fonts, every pen code, single-stroke letters as AutoCAD draws them; a TEXT of romans.shx `ABC123` 10
high right-aligned on x 100 is inked from 44.762 to 100, as AutoCAD puts it (the ink boxes of 14 fonts of AutoCAD 2021 agree
with its own to 0.001 where the strokes are straight). A font not found is drawn in the replacement font (GeometryHelper Simplex by
default, as FONTALT) and said once on the command line; Global settings > Fonts lists the search folders, the replacement and
a substitution table. A drawing of 10 000 SHX texts builds in 171 ms where it took 47, and draws whole in 135 ms where it took
38.

**NEW.** `DxfDatabase.UcsTable`, with `DxfUcsTable` and `DxfUcsTableRecord`: the named user coordinate systems of a drawing,
added, changed, renamed and erased as AutoCAD writes them. A system turned 30 degrees about Z at (100, 50, 0) is written with
its origin (100, 50, 0) and X axis (0.866, 0.5, 0); renaming or erasing it updates `$UCSNAME` and `$UCSBASE` where they named
it.

**NEW.** The presenter's user coordinate systems as AutoCAD's, in the plane of the drawing: UCS (origin, X and Y points, Z,
View, World, Previous, OBject, NAmed Save/Restore/Delete/?), UCSICON, PLAN, UCSMAN (a list on the command line), and the named
systems kept in the file. Points typed are in the UCS (`*` gives world ones). The grid, snap, ortho, polar, crosshair, icon,
readout and dynamic input follow it. In a UCS turned a quarter turn, `@100<0` draws a line 100 long up the world's Y. The 3D
options (X, Y, Face) and a dialog for UCSMAN are not built.

**NEW.** `ArrangeItem.Rectangle`, a nullable `GeoRectangle2`, sets labels along the long side of the rectangle of a part in
place of a `Leader`. The rows stand on either side of the long side and slide along it, with the gaps `Offset`, `OffsetTop`
and `OffsetBottom` measured from the long edges: a rectangle 2 000 by 300, a label 100 by 40 and the default gap of 50 put the
first rows 70 off the edges, 220 from the middle line. It may be turned by any angle, its long side the width or the height
where that is greater, and `Side` keeps the label to one edge. Items given no rectangle are arranged as before, to the bit
(the six Arranging benchmarks match their signatures).

**NEW.** `DxfDatabase.Recover(path or stream, out DxfRepairReport)` opens a damaged DXF as AutoCAD's RECOVER does, and
`DxfDatabase.Audit(fix)` checks an open drawing as AUDIT does; each reports what it found (`DxfRepairReport`, `DxfRepair`, 26
kinds of `DxfRepairKind`) and what it fixed. Lines that are not group codes, a file cut short, sections missing or out of
order, a BLOCK without ENDBLK, a POLYLINE without SEQEND, numbers that are no numbers or not finite, wrong vertex and loop
counts, duplicate and missing handles, a `$HANDSEED` too low, owners that name nothing, text of R2007+ not in UTF-8, and layers,
linetypes and styles the tables lack are repaired; a sound file opens byte for byte as before (a drawing of 21.8 MB and 36 112
entities reports "No errors found." in 0.25 s). The presenter has RECOVER and AUDIT (Yes is one undo step), and a file that
cannot be read as it is opens as "<name> (recovered)", unsaved.

**NEW.** The presenter's DRAWINGS list works like a list of files: a click selects a row (Ctrl and Shift add to the
selection), a double click or Enter opens it, and Delete does nothing. The right-click menu has Open, Close, Close Selected
Drawings, Close All Other Drawings and Close All Drawings. Three drawings closed with two of them changed bring one question
listing both.

**NEW.** Object snaps as in AutoCAD 2021: Drafting Settings (DSETTINGS, DS, OS) with Snap and Grid, Polar Tracking and Object
Snap tabs; the OSNAP arrow and right click on the status bar; Shift+right-click at a point prompt; END, MID, CEN and the other
keywords typed for the next point only; AutoCAD's markers, 9 pixels across, yellow on a dark canvas; and the new snaps Node and
Geometric Center (an L of 30 by 10 and 10 by 20 snaps to its centroid (11, 11)). **CHANGED.** A POINT snaps as a Node, not an
Endpoint.

**CHANGED.** Reading a DXF entity finds the groups of its record once for the readings that follow one another, not once for
each property: a HATCH whose loops, pattern and extents are read one by one is parsed once, and the placing of an INSERT
(`BlockTransform`) is read in under half the time. The display of a drawing of 16 268 entities and 5 684 INSERTs is built in
0.88 s where it took 1.26 s (.NET Framework 4.8). Nothing read or saved changes, and no memory is kept beyond the last eight
records read.

**NEW.** Dynamic input asks a point in two fields, as AutoCAD 2021's: X and Y, or distance and angle after a base point. Tab
moves between them and locks a number typed. Enter takes the point from the locked fields and the cursor, so 300 locked, the
cursor straight up and Enter draw a line 300 long at 90°. A coordinate typed whole, "@300<0" or "10,20", goes to the command
line as typed. **CHANGED.** While dynamic input is shown, the canvas's own tip names an object snap only ("Endpoint").

**NEW.** CECOLOR sets the colour of new objects ($CECOLOR): `Enter new object color [Truecolor/COlorbook] <BYLAYER>:`. The
colour box of the Home tab's Properties panel shows and sets it, and its swatch and "More colors..." open Select Color. A true
colour is refused, because the header holds an index.

**NEW.** With Select by dragging off, a press on nothing dragged draws a lasso, as AutoCAD's. Drawn clockwise it takes what it
holds whole (solid blue); counter-clockwise, what it touches (dashed green). Esc drops it. **FIXED.** A box clicked at its
first corner keeps that corner on the drawing while the wheel zooms or the middle button pans.

**CHANGED.** The presenter's canvas keeps a picture of the drawing and draws the selection and the entity under the cursor
over it: moving the mouse over a sheet set of 107 565 items costs 3.3 ms, where it cost 254 ms; zooming and panning move the
picture and draw it again once the view is still. Detail no eye could see is spared: items under half a pixel are one pixel,
text under 3 pixels is left out, and a hatch whose lines come closer than 2 pixels is filled with a tint of its colour. The
sheet set shown whole draws in 26 to 84 ms instead of 261 to 281.

**CHANGED.** A box is clicked corner to corner, as AutoCAD does with PICKDRAG 0, and a click on nothing starts one rather than
clearing the selection. **NEW.** Global settings > Canvas and cursor > Select by dragging lets a box be dragged too.
Selected and hovered objects are drawn solid in a highlight colour, 2 pixels wide, no longer dashed; the grid is drawn as
lines, every fifth stronger. **NEW.** `PickIndex.IndicesOf` and `IndicesIn`, `DisplaySegments2.Rings`,
`CadCanvas.SelectByDragging`, `GlobalSettings.SelectByDragging`.

**NEW.** The presenter's Select Color dialog, as AutoCAD's: the 255 colours of the index laid out as AutoCAD lays them, and a
True Color tab with red, green and blue, hue, saturation and luminance, and hex. It opens from a layer's colour cell in the
Layer manager and from "Select Color..." of the Color row of Properties. A layer given red, green and blue 10, 20, 30 is
written 420 = 660510, with the nearest index in 62. `CadLayerEditor.SetColor(document, name, DxfColor)`. **CHANGED.** Giving a
layer a colour of the index now takes out a true colour it had.

**NEW.** Dynamic input (DYN, F12, the status bar, DYN typed, Global settings > Canvas and cursor > Input). Beside the cursor it
shows the prompt of the command running, the cursor's coordinates, or the distance and angle from the base point ("500.00 <
53.1°"), and what is typed. Ctrl+9 shows or hides the command line.

**NEW.** The presenter's command line knows 17 more of AutoCAD's commands, in AutoCAD's prompts and with its aliases, each run
one undo step: XLINE (XL), RAY, POLYGON (POL), DONUT (DO), SELECTALL (Ctrl+A); LAYOFF, LAYON, LAYFRZ, LAYTHW, LAYLCK, LAYULK,
LAYISO, LAYUNISO; MEASUREGEOM (MEA: distance, radius, angle, area), UNITS and -UNITS (UN, $LUNITS, $LUPREC, $AUNITS, $AUPREC,
$ANGBASE, $ANGDIR), and SCRIPT (SCR). A polygon of 6 sides inscribed in a circle of radius 10 is a closed polyline of area
259.8076, its bottom side level; a donut of diameters 10 and 20 is a closed polyline of width 5. ELLIPSE draws an elliptical
Arc; AREA keeps a total with Add area and Subtract area; LAYOUT renames a layout (Rename) and shows one (Set); LAYOUT Delete
waits for the DXF library to erase a layout.

**NEW.** The presenter's Annotate tab (Single Line and Multiline Text; Linear, Aligned, Angular, Radius and Diameter
dimensions; Multileader) and View tab (Zoom Extents, Window, Previous, In, Out, Pan, Regen; Model and Layout; the Drawings
list and the Properties panel shown or hidden; Grid, Snap, Ortho, Polar, Osnap). The Home tab gains Rectangle, Ellipse,
Point and a Utilities group (Distance, Area, ID Point, List).

**NEW.** Presenter commands: NEW makes Drawing1.dxf, SAVEAS, LAYOUT New and ?, also the + after the layout tabs; DIST, AREA,
ID and LIST in AutoCAD's words (DIST from (0, 0) to (100, 100) reads "Distance = 141.4214"); ELLIPSE; POINT; PROPERTIESCLOSE;
GRID, SNAP, ORTHO, POLAR and OSNAP typed. Each drawing command is one undo step.

**CHANGED.** The presenter's buttons for what is not built are hidden (Paste) or say "not built yet"; the comparison with
AutoCAD 2021's six ribbon tabs is in the drawing plan, 72-autocad-gap.md.

**FIXED.** A screenshot run of the app that left a drawing changed waited for ever on the question to save it when the
window closed; it answers "Don't save".

**NEW.** Drawing plugins, shaped as Tekla's drawing plugins are. A class derived from `DrawingPlugin` says what it needs in
`DefineInput` and adds dimensions to a view in `Run`. `Drawing.RunPlugin` runs it and keeps the run in the drawing's DXF, and
every update runs it again, so its dimensions follow the model. The sample Bolt dimensions gives the pitch 300 of TC5's
base-plate bolts, and 340 once they are moved 20 further apart and the drawing is updated, a hand-moved dimension staying
where it was put.

**NEW.** Drawing plugins in the app: Run plugin on the Drawing tab shows the drawing plugins loaded, the view (the one
selected, else the first), the objects of the view of the kinds the plugin asks for, each ticked, and the plugin's settings
with their defaults and ranges, checked as typed. `PLUGIN` asks for the plugin by name, the view and each setting in turn.
Each run is one undo step, refused in words while the drawing is being made again, and a plugin that fails leaves the drawing
as it was and is said on the command line. The dimensions a plugin adds keep clear of those the view has already: on TC5 the
sample's 50, 300, 50 stand on tier 1 on the left of the front view, outside the drawing's own, where before 7 pairs
overlapped.

**NEW.** The plugin manager loads drawing plugins from a `.dll` or a `.ghplugin` whose plugin.json says `"kind": "drawing"`,
through a `DrawingPluginCatalog`. It uses the same shadow copy, trusted folders, startup list and log as command plugins; on
.NET 8, unloading frees the plugin's code.

**CHANGED.** `UpdateReport.PluginRunsToRunAgain` now lists only the runs whose plugin is not at hand. `PluginRunsRun` and
`PluginRunsDropped` (with the reason) say what became of the others.

**FIXED.** Installing a plugin whose id is "...", ". ." or ends in a dot or a space is refused: Windows read such an id as the
plugin folder itself, and installing it deleted every plugin installed.

**FIXED.** A hand edit whose writing fails is taken back alone: an earlier hand edit, a mark moved before, stays in the
drawing and in its file.

**FIXED.** EXPLODE of a dimension, mark or view of a model drawing is refused in words ("This dimension belongs to the model
and cannot be exploded here."); before, the dimension was erased and recorded as a hand edit, and its pieces lost.

**FIXED.** An update prepared before the drawing was made again is refused by `Drawing.Apply`: adding the top view and the
isometric one at once no longer loses the first. In the presenter a second Add view, AUTODIM or AUTOMARK while B1 is being
made says "B1 is being made again; wait for it.", and a drawing closed meanwhile is not written into.

**FIXED.** Export no longer saves the drawing shown from a worker thread, nor ties its database to the exported or temporary
file; the bytes written are unchanged.

**FIXED.** Save As onto the file of another open document, B-101.dxf of a model drawing among them, is refused until that
document is closed.

**FIXED.** An error of any kind from a source during AUTODIM, AUTOMARK, centre lines, VIEWSECTION or VIEWDETAIL is said on
the command line and logged, instead of ending the app or the Tekla it runs in.

**FIXED.** Undoing the first change of a drawing takes its preset and source back in the file and in Properties; redo brings
them again.

**FIXED.** The texts of short neighbouring dimensions no longer stand side by side as one number. In a chain 50, 300, 50
at 1:20 the two 50s took the 300's place and read as "5050"; now a text that fits in the middle of its line keeps it, a text
too long for its step goes past the end of the row, and every text keeps a quarter of its height clear of other texts and of
arrowheads. `Annotator.FindOverlaps` now counts texts closer than that, a text over an arrowhead, and a text put past its
line beside another dimension line along the same axis. Of 954 dimensions on the test bodies at 1:2 to 1:20, 788 are the same
to the bit; the measured values and the ids are the same except in the crowded TC9 front view, which drops two more
hole-edge dimensions at 1:20.

**NEW.** `DrawingDocumentStore.WriteValues` and `ReadValues`: named values the app keeps with a drawing in the XRECORD
`VALUES` of GH_DRAWING. A file without them reads as none, and writing none leaves a drawing as one never given any, byte
for byte.

**FIXED.** A model drawing saved and opened again keeps the source it was made from, its preset and the assemblies it
draws whole. Update from model chooses that source when one of the name is connected, Properties shows the preset
(Assembly, not Custom), and a fitting added to a whole assembly is drawn. Drawing settings, Add view, Auto dimension and
Auto marks work on a drawing opened again from its file: they make it again from the source its file names, or the one
Update from model found its objects in, in one undo step. With none at hand they say to pick its source in Update from
model first, and a source with none of its objects is refused, leaving the drawing as it was.

**FIXED.** Opening a file that a drawing just made was saved as shows that drawing instead of opening a second document
over the same file.

**FIXED.** A text override of a single space is kept, so the dimension shows no text, as in AutoCAD. Every other value is
still trimmed.

**CHANGED.** Global settings' Layers lists Symbol and Viewport (not plotted) by default, every layer a model drawing
writes. A settings file that lists its own table keeps it.

**CHANGED.** Placing labels among many blocks is faster, with the same results to the bit: `Arranger.Run` skips the
obstacles whose box lies farther than the nearest distance already found, and asks the spatial index for the
neighbourhood of each place, not all of them. The densest arranging benchmark goes from 55.8 ms to 49.9 ms.

**CHANGED.** The guides are split into pages of a few hundred lines each, their words unchanged: the plane into
`plane/`, space into `solid/`, and the DXF half of the shared types into `dxf/`, each page in the site's menu. The old
pages stay, each with a list of its pages; a bookmark to a section that moved lands on that list, by the link to the
page the section is on now. A page "How do I" gives a task, the call that does it and the page that tells the rest; the
AutoCAD patterns of the DXF model have a page of their own; and the first mention of each public type on a page links to
its page of the API reference. Every page opens with what it is for, is cut into sections with headings (a bold lead that
opened a paragraph is now a heading), and ends with "See also"; the blocks page is four, two of them on dynamic blocks,
and combining bodies two. No guide page is over 500 lines. Each group of the menu opens on a page listing its pages,
meshing and the checks and reports among them.

**FIXED.** `DxfDBDictionary.SetAt` read and wrote every entry of the dictionary again for each entry added, so that
10 000 XRECORDs added one by one to one dictionary took 84 s and 2 000 took 1.5 s. The entries are now found by name and
by handle without reading the record again, and the entry added, replaced or removed is put into the record's bytes as
they stand, written into one array of the record's new length rather than grown and copied, and as before to the byte:
the 10 000 take 1.1 s and the 2 000 0.02 s. `GetAt`, `Contains`, `Remove` and the erasing of an XRECORD or a GROUP a
dictionary names cost as little: 2 000 entries looked up one by one take 0.001 s, where they took 1.5 s. Every other
edit that puts groups into a record's bytes gains as well: 10 000 vertices added one by one to a LWPOLYLINE in a drawing
take 0.26 s, where they took 0.83 s, and 2 000 loops added to a hatch 0.32 s, where they took 0.56 s.

**NEW.** `DrawingSourceConvert` in GeometryHelper.TeklaConvert reads Tekla model objects into the `DrawingSource` of
GeometryHelper.Drawing, in the current work plane and without changing the model: `FromAssembly` the main part, the
secondaries and the sub-assemblies, set in the main part's frame; `FromPart` one part in its own frame; `FromObjects` a
selection. A part comes with its body, its frame, its name, its PART_POS as its mark, its profile, its material, its
assembly as its parent and ASSEMBLY_POS, LENGTH and WEIGHT as attributes. A bolt is made from its group's data: an M20
group of four, 60 long, gives four cylinders of 24 sides and 20 across along the group's Z axis, as long as the body
Tekla draws for them, with Diameter "20", HoleDiameter "22", Length "60", Count "4", Standard and the parts bolted; a
group with holes only gives holes of 22 through the parts bolted. A weld carries what its symbol reads: a fillet of 5
above the line, 200 long, is WeldType "Fillet", WeldSide "Arrow", WeldSize "a5" and WeldLength "200". Each
GeometryHelper.TeklaConvert.{year} package depends on GeometryHelper.Drawing from now on.

**NEW.** The sheet of a DXF layout is set, as AutoCAD's `PlotSettingsValidator` sets it, where it was only read: a layout
made in code lay on the A3 it was given. `CanonicalMediaName` takes a name that ends in the sheet's size in millimetres
and sets `PlotPaperSize` from it: the ISO sheets A0 to A4 on their sides, `ISO_A2_(594.00_x_420.00_MM)`, or upright,
`ISO_A2_(420.00_x_594.00_MM)`, and a size of one's own as ezdxf names one, `ezdxf_(1000.00_x_500.00_MM)`.
`PlotPaperUnits`, `PlotRotation`, `PlotPaperMargins` and `PlotOrigin` are set too, and each makes the layout's limits the
sheet about its plotted area again, as ezdxf's `page_setup` does. A layout made in code and set to A2 on its side with
margins of 10 lies on a sheet 594 by 420, its limits (-10, -10) to (584, 410), and reads so after saving. A layout of a
file reads as before, and a value set to what it has writes nothing.

**NEW.** An anonymous block is added as AutoCAD adds one: `db.BlockTable.Add(new DxfBlockTableRecord { Name = "*D" })`
adds a block the table names `*D1`, the first number no block, INSERT, dimension or table of the drawing names, marked
anonymous (flag 1 of its BLOCK) and written as ezdxf writes the picture of a dimension; `*U` gives `*U1` and on. So a
dimension made in code carries its own picture: a rotated dimension of 100 given a `*D1` that holds its line and the
text 100 is saved and read back drawn by `*D1` with its two entities, where a block named so was refused before. A
drawing whose dimensions ezdxf drew as `*D1` to `*D11` names the next `*D12`.

## 13.11.0 (2026-11-07)

A minor release: public types and members are added and none is removed or renamed in any of the four libraries. OLE
objects in DXF drawings, a picture pasted, a sheet of Excel embedded or a document linked, read until now as
`DxfUnknownEntity`, are `DxfOle2Frame`: read as AutoCAD 2021 reads them, from the corners in the head of their binary,
with the picture of a static one given back as its .bmp, .wmf or .emf; moved, scaled, mirrored and sized upright as
AutoCAD 2021 does; and made in code from a .bmp or a .png, which AutoCAD's .NET API cannot. Checked in AutoCAD 2021,
whose console read back every frame the library read, moved or made, AUDIT finding no error, and whose GUI drew the
pictures the library made and moved, and against drawings its GUI saved after ROTATE, SCALE, MIRROR and a paste into
paper space. A drawing only read and saved is written as it was, to the byte.

**NEW.** OLE2FRAME entities get a class of their own, `DxfOle2Frame`, as AutoCAD's `Ole2Frame`, with `DxfRectangle3d`,
`DxfOle2FrameItemType` and `DxfOlePictureFormat`. The frame is read from the four corners in the head of its binary, as
AutoCAD 2021 reads it, not from groups 10 and 11, which it ignores: a picture of 3 by 2 pixels pasted with its lower
left corner at (10, 20, 0) and stretched to 30 by 20 gives `Location` (10, 20, 0), the lower left corner, as AutoCAD
2021 gives it; `Position3d` the four corners as held, turned, sheared or tilted; `WcsWidth` 30 and `WcsHeight` 20, the
runs along x and y as AutoCAD works them out (25.98 for the frame 30 wide turned 30 degrees); `ScaleWidth` and
`ScaleHeight` against the size AutoCAD keeps at 100 percent; `Rotation` (radians), `LockAspect` and `AutoOutputQuality`
from the extended data between OLEBEGIN and OLEEND, and `OutputQuality` from group 73. What the object is comes from its
compound file, read by the library's own reader of MS-CFB (FAT, DIFAT past 109 sectors, mini stream, directory tree):
`Type` (static, embedded or linked, as MFC finds it), `UserType` (`Picture (Device Independent Bitmap)`, `OLE Package`),
`IsLinked`, and `LinkName` and `LinkPath` of a link's moniker (`C:\Drawings\cost.xlsx!Sheet1!R1C1:R4C3`,
`C:\Drawings\cost.xlsx`). The library's own `GetCompoundDocument()` gives the compound file as written, and
`GetPictureData(out format)` the picture of a static object as a .bmp, a placeable .wmf or an .emf. `GeometricExtents`
is the box of the corners, `GetGeometry()` their closed `GeoPolyline3`, which `IntersectWith` crosses, and `Explode()`
gives four LINEs along the edges, as AutoCAD 2021 gives four. A frame is copied by `Clone()` into its own drawing,
another or a block with its binary and extended data byte for byte, and a drawing only read and saved is written as it
was. A binary damaged never makes a getter throw: the corners come from 10 and 11 where the head holds none, the kind
and user type from groups 71 and 3 where the compound file cannot be read or names none, and there is no picture, said
in the log at Debug. A frame of no corners at all, as accoreconsole writes one it could not read, stands at the origin
and explodes into nothing, and `GeometricExtents` throws `InvalidOperationException` for it, as for any entity with no
extents, so that `UpdateExt` passes it over. `Rotation` is the number stored, a NaN another writer stored given as it
is. The rules are AutoCAD 2021's own, which read the frame from the head where 10 and 11 said otherwise and gave the
lower left corner as `Location`. The OLEFRAME of R13 stays a `DxfUnknownEntity`.

**NEW.** A `DxfOle2Frame` is moved and sized as AutoCAD 2021 does, upright, checked against drawings its GUI saved:
`TransformBy` takes a move, a scale by one factor and a mirror about a line along x or y, writing the corners into the
head of the binary, which AutoCAD reads, and into groups 10 and 11, only the lines of binary the head is in written
anew; a mirrored frame is the box of its corners, the picture not turned over, and a scaled one keeps its size at 100
percent and scales its text size, as AutoCAD's SCALE does. A turn, a shear or a stretch is refused with
`ArgumentException`. The picture 30 by 20 at (10, 20) scaled by a half is 15 by 10, its `ScaleWidth` 50, which is now
the size against the one AutoCAD keeps at 100 percent (codes 41 and 42 of the extended data). `Position3d`, `WcsWidth`,
`WcsHeight`, `ScaleWidth`, `ScaleHeight`, `LockAspect` and `OutputQuality` are set as AutoCAD 2021 sets them, the upper
left corner kept. A frame copied into model or paper space is given the head's signature, u32 and group 72 AutoCAD
writes for that space; an INSERT exploded places its frames where the placing keeps them upright.

**NEW.** `new DxfOle2Frame(bytes)` makes a static picture of a .bmp or a .png, as AutoCAD 2021 makes one of a picture
pasted, which AutoCAD's .NET API cannot: the bitmap in a compound file of the class StaticDib written by the library's
own writer (MS-CFB version 3, a DIFAT past 7 MB), behind AutoCAD's head and with its extended data. A PNG of any kind is
read by the library's own decoder into a bitmap of 32 bits a pixel, its alpha laid over white. The frame stands at the
picture's natural size in millimetres, 610 by 224 pixels of 96 dots an inch being 161.38 by 59.26, and the same picture
gives the same bytes each time.

**CHANGED.** An OLE2FRAME read from a file is a `DxfOle2Frame`, not a `DxfUnknownEntity`: code that found one by
`DxfUnknownEntity.DxfName == "OLE2FRAME"` finds it by its class. Its `GetGeometry()` and `GeometricExtents` give its
frame where they gave nothing and threw, so `UpdateExt` takes it into the extents; `Explode()` gives four LINEs where it
threw; `IntersectWith` crosses its frame. `TransformBy` moves it where the transformation keeps it upright and refuses
it with `ArgumentException` where not, an INSERT exploded places it where its placing keeps it upright, and the actions
of a dynamic block still refuse it. `DxfReader` reads none, as before.


## Earlier releases

The notes of 13.10.0 and earlier are in [CHANGELOG-ARCHIVE.md](CHANGELOG-ARCHIVE.md).
