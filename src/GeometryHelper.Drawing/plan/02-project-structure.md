# Project structure

## The projects

Each part has a core with no UI and a project of WPF controls; one app holds both (A16, D23). Part A never references
Part B.

| Project | Part | Holds | Target | References |
|---|---|---|---|---|
| `src/GeometryHelper.Dxf.Viewer` | A | the document (K1), display lists (K2), commands, snaps and grips (K3), the command plugin API (K4), the owner contract; no WPF | netstandard2.0 | `GeometryHelper` |
| `src/GeometryHelper.Dxf.Viewer.Wpf` | A | the canvas, Properties, the drawing-files panel, ribbon, command line, status bar, Global settings, Layer manager, Plugin manager; their view models | net48 and net8.0-windows (D12) | `GeometryHelper.Dxf.Viewer` |
| `src/GeometryHelper.Drawing` | B | L0 to L7 and the drawing plugin API; no WPF | netstandard2.0 | `GeometryHelper`, `GeometryHelper.Dxf.Viewer` |
| `src/GeometryHelper.Drawing.Wpf` | B | the drawing list, model properties, the Drawing ribbon tab, the New drawing, Drawing settings, Update and Export dialogs | net48 and net8.0-windows (D12) | `GeometryHelper.Drawing`, `GeometryHelper.Dxf.Viewer.Wpf` |
| `src/GeometryHelper.Drawing.App` | both | the standalone app: one window built from the controls of both parts, plugin loading, opening files and sources | net8.0-windows | the four above, the bridges |
| `src/GeometryHelper.TeklaConvert` (exists) | B | gains `DrawingSourceConvert`: Tekla model to `DrawingSource` | as now | `GeometryHelper.Drawing` |

The cores and the controls are packed as NuGet packages under the same version as the others (`GeometryHelperVersion`
in `Directory.Build.props`). The standalone app is not a NuGet package; how it is shipped is decided later (Q8).

Every new project:

- carries XML documentation on every public member (CS1591 fails the `-warnaserror` build without it);
- is added to `GeometryHelper.slnx`;
- is made when its first code is written: `Dxf.Viewer` and `Dxf.Viewer.Wpf` in C1, `Drawing` in G1, `Drawing.Wpf` in U1.

## Folders

```
src/GeometryHelper.Drawing/
  README.md                       what the module is, and the plan's index (the plan covers both parts)
  plan/                           this plan (*.md)

src/GeometryHelper.Dxf.Viewer/                         Part A core
  CadDocument.cs                  one DxfDatabase open for editing: file, undo, selection, owners
  Owners/
    ICadEntityOwner.cs            the owner contract (01-architecture.md)
    CadEditRule.cs, CadEdit.cs
  Display/
    DisplayList2.cs               the 2D primitives of one space or viewport, cached
    EntityRenderer.cs             entity -> primitives (lines, arcs, fills, glyphs), one per entity kind
    PickIndex.cs                  spatial index for picking and snapping
  Editing/
    CadCommand.cs                 a command: name, aliases, its steps as prompts
    CadPrompt.cs                  point, distance, selection, keyword, string prompts, as AutoCAD's
    ObjectSnap.cs                 endpoint, midpoint, centre, intersection, perpendicular, nearest
    Grips.cs                      grip points per entity kind, and what dragging one does
    Commands/                     the built-in commands (ZOOM, MOVE, LINE, ...), one file each
  Plugins/
    CadCommandAttribute.cs        marks a plugin method as a command (60-plugins.md)
    CadCommandContext.cs
  Settings/
    GlobalSettings.cs             saved as JSON, with defaults, applied with Apply (D13)

src/GeometryHelper.Dxf.Viewer.Wpf/                     Part A controls
  Controls/                       Canvas, PropertiesPanel, FilesPanel, LayoutsPanel, LayersPanel, Ribbon, CommandLine,
                                  StatusBar, SettingsDialog, LayerManager, PluginManager
  ViewModels/                     the logic of each control, testable without a window
  Themes/                         colour, type and spacing resources from the design tokens (Dark, Light)
  Fonts/                          Inter (OFL licence)

src/GeometryHelper.Drawing/                       Part B core (the same folder as the plan)
  GeometryHelper.Drawing.csproj
  Drawing.cs                      the drawing document (L7)
  DrawingBuilder.cs               source + settings -> Drawing; Update(source) rebuilds and keeps hand edits
  Source/                         DrawingObject, DrawingObjectKind, DrawingSource
  Projection/                     ViewProjector, ProjectedView2, ProjectedEdge2, EdgeVisibility, EdgeKind, Internal/
  Views/                          DrawingView, ViewKind, ViewFrame3, ViewBuilder
  Annotation/                     Dimensions/, Marks/, Symbols/
  Sheets/                         DrawingSheet, TitleBlock, PartList, SheetLayout
  Document/
    DrawingEdit.cs                one hand-edit record (object, property, old value, new value)
    DrawingLinks.cs               links from annotation and views to the model
    DrawingOwner.cs               implements ICadEntityOwner: edits on its entities become hand edits
    DrawingFile.cs                saving and opening the document in the DXF (A19)
  Output/
    DrawingDxfWriter.cs           Drawing -> DxfDatabase, every entity tagged with its owner and id
  Settings/
    DrawingSettings.cs            drawing settings and the presets
  Plugins/
    DrawingPlugin.cs              the drawing plugin base class, its input and run context (60-plugins.md)

src/GeometryHelper.Drawing.Wpf/                   Part B controls
  Controls/                       DrawingListPanel, ModelPropertiesSection, DrawingRibbonTab, NewDrawingDialog,
                                  DrawingSettingsDialog, UpdateDialog, ExportDialog
  ViewModels/

src/GeometryHelper.Drawing.App/
  MainWindow.xaml                 the controls laid out as in the UI design
  App.xaml                        loads Global settings and plugins

tests/GeometryHelper.Dxf.Viewer.UnitTest/         net48 xUnit: document, display, commands, snaps, owners
tests/GeometryHelper.Drawing.UnitTest/       net48 xUnit, as the other suites
  TestBodies.cs                   the bodies of 20-test-cases.md, built in code
  Projection/ Views/ Annotation/ Sheets/ Document/ Output/ Settings/ Plugins/
tests/GeometryHelper.Drawing.Wpf.UnitTest/   the view models of both parts' controls (from C1)

benchmarks/GeometryHelper.Benchmarks/Scenarios/
  DxfViewer*.cs                       opening and drawing large DXF files, timed
  Drawing*.cs                     projection and whole drawings, timed and signed

src/GeometryHelper/docs/drawing/  (or a docs folder of this project, decided in G7)
  index.md and one page per part and layer, a group of the site's menu with its own index page
```

The two sample plugins go in `examples/`, and as CLAUDE.md says, nothing under `examples/` is staged without asking the
maintainer.

## Verify

Once the projects exist, CLAUDE.md's verify gains:

```bash
dotnet test tests/GeometryHelper.Dxf.Viewer.UnitTest/GeometryHelper.Dxf.Viewer.UnitTest.csproj -c Release --no-build
dotnet test tests/GeometryHelper.Drawing.UnitTest/GeometryHelper.Drawing.UnitTest.csproj -c Release --no-build
dotnet test tests/GeometryHelper.Drawing.Wpf.UnitTest/GeometryHelper.Drawing.Wpf.UnitTest.csproj -c Release --no-build
```

and the build of `GeometryHelper.slnx` takes the new projects in. A change to a path the benchmarks time is checked by
them too.

## Looking at the output

- The standalone app (from C1), opening any DXF and, from G1, the drawings of the test bodies.
- AutoCAD 2021, through accoreconsole with the leader's own plugins (open, AUDIT, count the entities), as for the DXF
  work before; and AutoCAD's screen beside Part A's for the same file.
- An SVG of a view through `SvgWriter`, for a quick look in a browser while working on L1.
- The UI set beside the design, screenshot by screenshot.
