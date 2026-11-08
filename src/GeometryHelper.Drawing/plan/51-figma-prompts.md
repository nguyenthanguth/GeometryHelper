> **Superseded 2026-11-08.** The UI is now designed in a claude.ai Design artifact (see [50-ui.md](50-ui.md)), not in Figma, and
> the screens were renumbered when the plan was split into two parts (A16). These prompts are kept only as a record of the
> first approach; do not use them.

# Prompts for Figma's AI

Prompts to paste into Figma's AI (First Draft / "Make designs", or Figma Make) so that it draws the screens of
[50-ui.md](50-ui.md) as the code needs them. Paste **Prompt 0** first in every new chat or file, then one screen prompt
at a time. Check each result against 50-ui.md, fix by hand, and name the frame by its S number.

Tips:

- Publish the components and variables of "GeometryHelper Drawing – UI" as a library and enable it, so that the AI can
  reuse them instead of drawing new ones (if the AI in use supports libraries).
- Ask for one screen per prompt; long prompts with many screens come back thinner.
- If the AI invents content, paste the "Content" part of the prompt again and ask it to use exactly that.

---

## Prompt 0 · Context (paste first)

```text
You are designing screens for "GeometryHelper Drawing", a Windows desktop app for viewing and editing engineering
drawings (steel shop drawings) made from 3D models. It looks and behaves like AutoCAD. Follow these rules in every screen:

STYLE
- Desktop app, dense professional CAD UI, not a website or a mobile app. Dark theme by default.
- Font: Inter only. Sizes: titles 13 px Semi Bold, body 12 px Regular, labels 11 px Medium, monospace (command line,
  coordinates) JetBrains Mono 12 px. Line heights 18/16/14/16.
- Colours (use the file's variables if they exist, with these names and values):
  color/bg/app #1E2228 (window chrome, ribbon, status bar)
  color/bg/panel #262B33 (left and right panels, dialogs)
  color/bg/panel-header #2E343D (panel title bars, dialog header and footer)
  color/bg/input #1A1E24 (text boxes, command line)
  color/bg/canvas #000000 (drawing area, black by default)
  color/bg/sheet #FFFFFF (the paper sheet on the canvas)
  color/bg/hover #343B45, color/bg/selected #1F4E79
  color/border/default #3A414C
  color/text/primary #E6E9ED, color/text/secondary #9AA4B1
  color/accent #3D9BFF (focus, selection, primary button)
  color/status/ok #3FB950, color/status/warning #D29922, color/status/error #F85149
- Spacing 2, 4, 6, 8, 12, 16, 24 px. Corner radius 2 px (small controls) and 4 px (boxes, tabs). 1 px borders.
- Icons: simple 16 and 24 px outline icons in the style of Lucide. Never Autodesk icons or logos.
- Use auto layout everywhere. Reuse components; make anything repeated a component with variants for its states.
- All text in English. Use realistic content from the brief, never lorem ipsum.
- 2D only: no 3D views, no orbit controls, no ViewCube.

MAIN WINDOW LAYOUT (1600 x 960 frame unless a prompt says otherwise)
- Title bar 36 px: app name "GeometryHelper Drawing", then tabs of open drawings ("B-101 · Beam B1" active,
  "C-12 · Column C3", "GA-01 · Level 2 plan", "+").
- Ribbon: tabs Home, View, Annotate, Drawing, Export, Settings, Plugins; under them groups of large buttons
  (24 px icon above an 11 px label), each group titled underneath, groups split by thin vertical lines.
- Body: left panel 288 px wide, canvas filling the middle, right panel 312 px wide.
- Left panel "DRAWINGS": header with title and two small icon buttons; a search box "Search mark, name, profile…";
  filter chips "All 24", "Out of date 2", "Edited 5"; a tree grouped by source then kind:
  "Tekla · Workshop A" > "Assembly (12)" > rows B-101 Beam B1 · rev B (A2), B-102 Beam B2 · rev A (A2),
  B-103 Beam B3 · rev A (A3), B-104 Beam B4 · rev C (A3); collapsed groups "Columns (6)", "General arrangement (2)",
  "IFC · Stair.ifc", "Built in code · Test bodies". Each drawing row: an 8 px status dot (green up to date, amber out
  of date, blue edited by hand), the mark in Semi Bold, the description and revision underneath in secondary text,
  the sheet size on the right. Below the tree, a second header "VIEWS IN B-101" with rows "Front · 1:20",
  "Top · 1:20", "Section A-A · 1:10", "Detail 1 · 1:5".
- Canvas: black background; a white A2 sheet (594 x 420 ratio) centred with a thin frame, a title block bottom right,
  a part list above it, and views of a steel I-beam: FRONT 1:20 (long rectangle with flanges and an end plate),
  TOP 1:20, SECTION A-A 1:10 (an I shape), with dimensions such as 3 000, 1 000, 2 000. Lines on the sheet are dark grey.
- Right panel "PROPERTIES": header; a selection box showing what is selected, e.g. "Dimension (1) ▾"; collapsible
  groups with a chevron and a title; rows of label (left half, secondary text) and value (right half, in an input box).
- Command line (about 56 px): two lines of history in monospace secondary text, then "Command:" with the input.
- Status bar 24 px: "X 1 234.50   Y 567.80" in monospace, toggles SNAP (on), ORTHO, GRID, then "View: Front 1:20",
  a spacer, "Source: Tekla 2025 · connected" in green and "2 drawings out of date" in amber.

Reply "Ready" and wait for the screen prompts.
```

---

## Prompt C · Components still missing

```text
Using the style of the context, create a components page with these components, each with variants for its states
(Default, Hover, Pressed, Focused, Disabled where they apply):
1. Button: Primary (accent fill), Secondary (input fill, border), Ghost (no fill), Icon-only (24 px square).
2. Text box, Number box with a unit suffix ("mm"), Dropdown (closed and open with 5 options), Checkbox, Toggle switch.
3. Colour box: a 14 px swatch plus the hex value; and a colour picker popover (hue strip, saturation square, hex field,
   10 swatches of recent colours).
4. Tabs (horizontal, with the active tab in color/bg/selected), and a vertical category list (Nav row).
5. Table / data grid: header row, rows with hover and selected states, a sortable column header, an editable cell.
6. Context menu with icons, shortcuts on the right (e.g. "Properties   Ctrl+1"), a separator, a disabled item.
7. Tooltip: dark, 11 px, with an optional shortcut line.
8. Dialog shell: header (title + close X), body, footer with buttons right-aligned and an optional left button.
9. Notice (toast): info, success, warning, error, with icon, one line of text and a close button.
10. Ribbon button large (24 px icon over label) and small (16 px icon left of label), and a ribbon group with its title.
11. Status bar toggle (on/off), splitter handle, thin scrollbar.
12. Canvas markers: square grip 8 px (default blue, hover lighter, dragging filled), snap markers 12 px in yellow:
    endpoint (square), midpoint (triangle), centre (circle), intersection (X), perpendicular (right-angle mark),
    nearest (hourglass); window selection box (solid blue outline, light blue fill) and crossing selection box
    (dashed green outline, light green fill).
```

---

## S1 · Main window, nothing open

```text
Make frame "S1 · Main window · nothing open" (1600 x 960) with the main window layout of the context, but no drawing
is open: no drawing tabs except "+", the right panel shows "PROPERTIES" with the hint "Nothing selected". The canvas area
shows a start page instead of a sheet: a large title "GeometryHelper Drawing", two big buttons "New drawing" (Primary,
Ctrl+N) and "Open…" (Secondary, Ctrl+O), a list "Recent drawings" (B-101 Beam B1 · Workshop A · 2026-11-08, C-12 Column
C3, GA-01 Level 2 plan, B-077 Beam B7) and a card "Sources" showing "Tekla 2025 · connected" (green dot) and
"IFC · none open" (grey dot) with a "Connect…" link. The left panel shows the drawing tree as in the context.
```

## S2 · Drawing open, nothing selected

```text
Make frame "S2 · Main window · drawing open, nothing selected" with the main window of the context, drawing B-101 open.
Nothing is selected on the canvas. The right panel shows the drawing's own properties, selection box "Drawing B-101 ▾":
group "Drawing": Number B-101, Name Beam B1, Revision B, Preset Assembly, Status Up to date.
group "Sheet": Size A2, Orientation Landscape, Scale 1:20, Projection Third angle.
group "Title block": Project Workshop A, Drawn by NT, Checked by (empty), Date 2026-11-08.
group "Source": Source Tekla 2025, Model Workshop A, Objects 4 parts, 4 bolts, Last update 2026-11-08 10:42.
Command line history: "Opened B-101." then "Command:".
```

## S4 · Several objects selected

```text
Make frame "S4 · Main window · several objects selected" like S2, but three dimensions are selected on the canvas
(highlighted in accent blue with grips). The selection box reads "Dimension (3) ▾" with a dropdown listing
"All (3)", "Dimension (3)". Values that differ show "*Varies*" in italic secondary text:
group "General": Layer Dimension, Colour ByLayer, Linetype ByLayer.
group "Dimension": Type Chain, Value *Varies*, Measure scale 1:20, Rounding 1 mm, Text height 2.5 mm.
group "Position": Tier spacing 8 mm, X *Varies*, Y 96.00.
Command line: "3 objects selected." then "Command:".
```

## S6 · Drawing list states

```text
Make a frame "S6 · Drawing list states" holding six copies of the left panel (288 x 730 each) side by side, titled
above each: (1) "Search with results": search box "B-10" with 4 matching rows, the matched text highlighted;
(2) "No result": search "XYZ", an empty state "No drawing matches 'XYZ'" with a "Clear search" link;
(3) "Filter Out of date": chip "Out of date 2" active, only B-102 and GA-01 shown, both with amber dots;
(4) "Groups collapsed": every group collapsed with its count;
(5) "Row hovered": row B-103 in hover colour with a small "⋯" button at its right;
(6) "Right-click menu": row B-101 selected and a context menu: Open, Update, Export…, Rename, Duplicate, separator,
Delete (red), separator, Properties (Ctrl+1).
```

## S7 · Properties states

```text
Make a frame "S7 · Properties states" holding five copies of the right panel (312 px wide) side by side, titled above:
(1) "Read-only value": row "Value 3 000" greyed with a lock icon and tooltip "Measured from the model; edit the model
or override".
(2) "Editing": row "Text height" with the box focused (accent border), caret after "2.5", unit "mm".
(3) "Invalid value": row "Rounding" with "-1" in a red-bordered box and the message under it "Must be 0 or more".
(4) "Dropdown open": row "Type" open with options Linear, Aligned, Chain, Cumulative, Ordinate, Chain checked.
(5) "Colour picker open": row "Colour" with the colour picker popover open beside the panel.
```

## S8 · Global settings, all categories

```text
Make a dialog "Global settings" (840 px wide, dialog shell: header, category list on the left 200 px, content on the
right, footer with "Reset to defaults" on the left and "Cancel", "Apply" (Primary), "OK" on the right). Make one frame
per category, named "S8 · Global settings · <category>", with the category selected in the list. Each category has a
one-line description at the top, then collapsible groups of rows (label left half, value right half):
1. Appearance: Colours: Mode Dark ▾, Canvas background #000000 Black (default) [swatch], Paper #FFFFFF White, Visible
   lines on dark #FFFFFF, Hidden lines #9AA4B1, Selected objects #3D9BFF, Grip #3D9BFF. Cursor: Crosshair size 5 % of
   screen, Pickbox 5 px. Display: Grid Off, Show lineweights On.
2. Canvas and cursor: Zoom: Wheel zoom speed 1.2, Zoom about cursor On, Middle double-click Zoom extents. Selection:
   Window left to right On, Crossing right to left On, Pick first then command On, Selection cycling On. Snap: Endpoint,
   Midpoint, Centre, Intersection, Perpendicular, Nearest as checkboxes, Snap marker size 12 px, F3 toggles snap.
3. Drawing defaults: Preset for new drawings Assembly ▾, Sheet size A2 ▾, Orientation Landscape ▾, Projection Third
   angle ▾, Allowed scales 1:1, 1:2, 1:5, 1:10, 1:15, 1:20, 1:25, 1:50, 1:100 (chips).
4. Units and rounding: Length unit mm ▾, Dimension decimals 0, Angle decimals 1, Thousands separator Space ▾,
   Decimal separator Point ▾.
5. Layers, colours and lines: a table with columns Layer, Colour, Linetype, Lineweight, Plot: Visible white Continuous
   0.35, Hidden grey HIDDEN 0.18, Centre red CENTER 0.18, Hatch grey Continuous 0.13, Dimension green Continuous 0.18,
   Mark cyan Continuous 0.18, Sheet white Continuous 0.25; buttons "Add layer", "Load linetypes…".
6. Text and fonts: UI font Inter, UI scale 100 % ▾, Drawing text font ISOCPEUR ▾, Text height 2.5 mm, Title text height
   5 mm.
7. Data sources: a list of sources with status: "Tekla 2025 · connected · Workshop A" (green), "IFC · none"; options
   Check for model changes On, Every 30 s; buttons "Connect…", "Disconnect".
8. Shortcuts: a search box "Search command, alias or key"; a table with columns Command, Alias, Keys, Description:
   ZOOM Z, PAN P, ERASE E Delete, MOVE M, COPY CO/CP Ctrl+C, PROPERTIES PR/CH Ctrl+1, OPTIONS OP, DIMLINEAR DLI,
   DIMCONTINUE DCO, MLEADER MLD, MVIEW MV, DRAWINGUPDATE DU, PLUGINLOAD NETLOAD, PLUGINRELOAD; one row in edit mode
   with the key field "Press keys…", one row with a red conflict note "Ctrl+1 is already used by PROPERTIES";
   buttons "Import acad.pgp…", "Reset to AutoCAD defaults".
9. Save and export: Save folder D:\Drawings ▾ "Browse…", DXF export layout Layout + viewports ▾ (other: Sheet in model
   space), DXF version AutoCAD 2013 (AC1027) ▾, Open exported file in AutoCAD On.
```

## S9 · Canvas interaction

```text
Make a frame "S9 · Canvas interaction" holding six crops of the canvas (each about 640 x 400, black background with the
white sheet and the beam views of the context), titled above:
(1) "Hover": the top flange line highlighted thicker in light blue, cursor crosshair with a small pickbox.
(2) "Window selection": dragging left to right, solid blue box with light blue fill over the section view.
(3) "Crossing selection": dragging right to left, dashed green box with light green fill crossing the front view.
(4) "Grip drag": a dimension selected with three grips, the middle grip being dragged, a rubber-band line, and a small
dynamic input box near the cursor showing "Offset: 12.0".
(5) "Snap": the cursor near a corner of the end plate with the yellow endpoint marker and tooltip "Endpoint".
(6) "Right-click menu": context menu over a selected mark: Repeat MOVE, separator, Move, Copy, Erase, separator,
Properties (Ctrl+1).
```

## S10 · Command in progress

```text
Make frame "S10 · Main window · command in progress" like S2 while the DIMLINEAR command runs: the canvas cursor is a
crosshair with the endpoint snap marker on the beam's left end, a rubber-band dimension follows the cursor showing
"2 150". The command line shows history "Command: DIMLINEAR" and the prompt
"Specify second extension line origin or [Undo]:" with "Undo" underlined as a clickable option. A small hint at the
cursor shows "Esc to cancel". The status bar shows SNAP on.
```

## S11 · Ribbon, every tab

```text
Make a frame "S11 · Ribbon tabs" with seven copies of the ribbon (1600 px wide), one per tab active, with real Lucide-
style icons and these groups and buttons:
Home: Clipboard (Paste, Copy, Cut) · Modify (Move, Copy, Erase, Undo, Redo) · Layers (Layer, Match properties) ·
Properties (Properties).
View: Navigate (Zoom extents, Zoom window, Pan, Regen) · Views (Add view, Section, Detail, MVIEW) · Display (Hidden
lines, Centre lines, Lineweights).
Annotate: Dimensions (Linear, Aligned, Chain, Baseline, Ordinate, Radius, Diameter, Angle) · Marks (Mark, Leader) ·
Text (Text, Multiline text) · Tables (Table) · Symbols (Centre line, Weld, Level).
Drawing: Drawing (New, Open, Save, Update, Settings) · Automatic (Auto dimensions, Auto marks).
Export: Export (DXF, PDF, Print).
Settings: Settings (Global settings, Shortcuts, Layers).
Plugins: Installed (Bolt dimensions, Weld marks, Hole numbers, Steel schedule) · My plugins (Company title block,
Drawing check) · Manage (Load .dll, Reload, Unload, Plugin manager).
```

## S12 · New drawing

```text
Make a dialog "S12 · New drawing" (720 x 560, dialog shell) as three steps shown by a stepper at the top
("1 Source", "2 Objects", "3 Settings"); make one frame per step:
Step 1 Source: radio cards "Tekla Structures (connected, Workshop A)", "IFC file…", "Objects from code (test bodies)".
Step 2 Objects: a button "Pick in Tekla" and a list of picked objects with columns Mark, Name, Profile, Kind
(B1 Beam HEA300 Main part; P1 End plate PL20*300 Part; 4 bolts M20 Bolt group); a count "4 parts, 4 bolts"; a checkbox
"One drawing per assembly".
Step 3 Settings: Preset Assembly ▾ (Part, Assembly, General), Sheet size A2 ▾, Scale Automatic ▾, Drawing number B-105,
Name Beam B5, Open after creating On.
Footer: "Back", "Next" (Primary) and on the last step "Create" (Primary).
```

## S13 · Drawing settings

```text
Make a dialog "S13 · Drawing settings" (840 x 600, dialog shell) for drawing B-101 with tabs Views, Scales, Dimensions,
Marks, Sheet; one frame per tab:
Views: checkboxes Front, Top, Bottom, Left, Right, Back, Isometric; Sections from cut lines On; Crop margin 20 mm; Depth
in front 500 mm, Depth behind 500 mm; Smooth angle 15°.
Scales: allowed scales as chips, largest first; Choose the largest that fits On.
Dimensions: rules as toggles Overall, Chain, Cumulative, Holes by group, Fittings from main part; Tier spacing 8 mm;
Text height 2.5 mm; Rounding 1 mm; Merge equal dimensions (n x p) On.
Marks: Part mark content (chips: Position, Profile, Length, Material) with a preview "P1 PL20*300 L=420"; Assembly mark
content; Text height 2.5 mm; Frame Rectangle ▾.
Sheet: Size A2 ▾, Title block Built-in ▾ / "From file…", Part list columns (chips Mark, Qty, Profile, Material, Length,
Weight), Revision table On.
Footer: "Save as preset…" on the left; "Cancel", "Apply" (Primary), "OK".
```

## S14 · Update from model

```text
Make a dialog "S14 · Update from model" (760 x 560, dialog shell) for drawing B-102: a summary line "The model changed
since 2026-11-07 16:20: 1 changed, 1 added, 0 removed." A table "Model changes" with columns Object, Change, Detail:
B2 Beam · Changed · Length 3 000 → 3 200; P5 Stiffener · Added · new part. A second table "Hand edits" with columns
Edit, Status: "Mark P1 moved" · Kept (green); "Dimension 1 000 text moved" · Kept (green); "Dimension to P4 deleted" ·
No longer applies, P4 removed (amber). A checkbox "Run plugins set to run on update". Footer: "Cancel", "Update"
(Primary).
```

## S15 · Export

```text
Make a dialog "S15 · Export" (680 x 520, dialog shell): a list of drawings with checkboxes (B-101, B-102, B-103 checked,
GA-01 unchecked) and their sheet sizes; Format DXF ▾ (PDF and Print greyed "later"); DXF layout "Layout + viewports
(like MVIEW)" ▾ with the other option "Sheet in model space"; DXF version AutoCAD 2013 (AC1027) ▾; Folder D:\Drawings
"Browse…"; File names "<Number> <Name>.dxf"; Open in AutoCAD after export On. Footer: "Cancel", "Export 3" (Primary).
```

## S16 · Plugin manager

```text
Make a dialog "S16 · Plugin manager" (900 x 600, dialog shell): toolbar buttons "Load .dll… (NETLOAD)", "Install
.ghplugin…", "Reload", "Unload", "Remove". A table with columns On, Name, Version, Commands, File, Trusted, Load at
start: Bolt dimensions 1.2.0 BOLTDIM %AppData%\…\Plugins\BoltDim\BoltDim.dll Yes Yes; Weld marks 0.9.1 WELDMARK Yes Yes;
Company title block 2.0.0 CTB D:\Dev\Ctb\bin\Ctb.dll No (amber warning icon "Outside trusted folders") No; Drawing check
1.0.0 DCHECK Yes Yes but switched off. Under the table, details of the selected plugin: description, author, least API
version 1.0, "Run on drawing update" toggle, and a log list ("10:42 Loaded BoltDim 1.2.0 from …", "10:43 BOLTDIM ran
on B-101: 6 dimensions added", "10:50 Reloaded Ctb 2.0.0"). A section "Trusted folders" with two paths and "Add…".
Footer: "Close".
```

## S17 · A plugin asking for input

```text
Make frame "S17 · Main window · plugin asking for input" like S2 while the plugin "Bolt dimensions" runs: the canvas
highlights the bolt groups in accent blue, the cursor is a pickbox, and the command line shows history
"Command: BOLTDIM" and the prompt "[Bolt dimensions] Select bolt groups or [All views/Settings]:". A small badge at
the top of the canvas reads "Plugin: Bolt dimensions · Esc to cancel".
```

## S18 · Messages

```text
Make a frame "S18 · Messages" with: (1) an error dialog "Plugin failed": "Bolt dimensions stopped with an error. The
drawing was left as it was." with "Show log" and "OK"; (2) a warning notice at the bottom right of the canvas
"Tekla disconnected. Drawings stay open; updates are paused." with "Reconnect"; (3) a confirmation dialog "Delete B-104?"
"The drawing and its hand edits are deleted. This cannot be undone." with "Cancel" and "Delete" (red); (4) a confirmation
"Save changes to B-101?" with "Don't save", "Cancel", "Save" (Primary); (5) a success notice "Exported 3 drawings to
D:\Drawings" with "Open folder".
```

## S19 · Light mode

```text
Make light versions of S2, S3 (a dimension selected) and the Global settings Appearance page, named
"S19 · Light · …". Light values: color/bg/app #EEF0F3, panel #F7F8FA, panel-header #E6E9EE, input #FFFFFF, canvas
#D9DDE3, hover #DCE1E8, selected #CCE4F7, border #CDD3DB, text primary #1E2228, text secondary #5B6573, accent #0B6BCB,
ok #1A7F37, warning #9A6700, error #CF222E. The paper sheet stays white.
```

## S20 · Hosted inside Tekla

```text
Make frame "S20 · Hosted in Tekla" (1100 x 760), a compact window of the app running inside Tekla Structures: no
title bar of its own and no ribbon; a slim toolbar 32 px with icon buttons (New, Update, Export, Settings, Plugins);
the drawing list on the left 240 px with the same rows; the canvas; Properties as a collapsible panel on the right
(260 px) that can be hidden; a one-line command line and a status bar. Same colours and components as the main window.
```
