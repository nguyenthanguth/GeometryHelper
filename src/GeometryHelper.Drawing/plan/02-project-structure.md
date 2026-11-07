# Project structure

## The project

`src/GeometryHelper.Drawing/GeometryHelper.Drawing.csproj`, made in G1 when the first code is written:

- targets netstandard2.0, as the other libraries do;
- references `GeometryHelper` only, never Tekla;
- version from `GeometryHelperVersion` in `Directory.Build.props`, packed as `GeometryHelper.Drawing` and released with
  the other packages as one set;
- XML documentation on every public member (CS1591 fails the `-warnaserror` build without it);
- added to `GeometryHelper.slnx`.

## Folders

```
src/GeometryHelper.Drawing/
  README.md                       what the module is, and the plan's index
  plan/                           this plan (*.md)
  GeometryHelper.Drawing.csproj
  Drawing.cs                      the result: views and sheet in sheet millimetres
  DrawingBuilder.cs               source + settings -> Drawing
  Source/
    DrawingObject.cs              one object: solid, kind, name, mark, profile, material, parent, attributes
    DrawingObjectKind.cs          Part, Bolt, Hole, Weld, Rebar, ...
    DrawingSource.cs              the objects of one drawing, and its frame
  Projection/
    ViewProjector.cs              L1 entry point
    ProjectedView2.cs             the lines and section faces of one view
    ProjectedEdge2.cs             a 2D segment or arc, its visibility, kind and object
    EdgeVisibility.cs             Visible, Hidden
    EdgeKind.cs                   Sharp, Silhouette, SectionOutline, ...
    Internal/                     edge extraction, occlusion, spatial index
  Views/
    DrawingView.cs                one view: frame, scale, crop, depth, projected lines, annotation
    ViewKind.cs                   Front, Top, Bottom, Left, Right, Back, Section, Detail, Isometric
    ViewFrame3.cs                 the view's coordinate system and its depth range
    ViewBuilder.cs                the views a drawing's settings ask for
  Annotation/
    Dimensions/                   points of interest, dimension rules, chains, tiers
    Marks/                        part, assembly and bolt marks, leaders
    Symbols/                      centre lines, weld symbols, levels, section and detail marks
  Sheets/
    DrawingSheet.cs               the sheet: size, frame, title block, tables
    TitleBlock.cs
    PartList.cs
    SheetLayout.cs                view placement through SheetPacker
  Output/
    DrawingDxfWriter.cs           Drawing -> DxfDatabase -> file
  Settings/
    DrawingSettings.cs            and its parts per layer, and the presets

src/GeometryHelper.TeklaConvert/
  DrawingSourceConvert.cs         Tekla assemblies and parts -> DrawingSource

tests/GeometryHelper.Drawing.UnitTest/       net48 xUnit, as the other suites
  TestBodies.cs                   the bodies of 20-test-cases.md, built in code
  Projection/ Views/ Annotation/ Sheets/ Output/ Builder/

benchmarks/GeometryHelper.Benchmarks/Scenarios/
  Drawing*.cs                     projection and whole drawings, timed and signed

src/GeometryHelper/docs/drawing/  (or a docs folder of this project, decided in G7)
  index.md and one page per layer, a group of the site's menu with its own index page
```

A separate test project keeps the drawing suite from slowing the core suite, and matches one test project per library.
Whether it stays separate or joins `GeometryHelper.UnitTest` is decision D7 in [03-decisions.md](03-decisions.md).

## Verify

Once the project exists, CLAUDE.md's verify gains one line:

```bash
dotnet test tests/GeometryHelper.Drawing.UnitTest/GeometryHelper.Drawing.UnitTest.csproj -c Release --no-build
```

and the build of `GeometryHelper.slnx` takes the new projects in. A change to a path the benchmarks time is checked by
them too.

## Looking at the output

- AutoCAD 2021, through accoreconsole with the leader's own plugins (open, AUDIT, count the entities), as for the DXF
  work before.
- The repository's viewer (`tools/GeometryHelper.Viewer`), which opens DXF.
- An SVG of a view through `SvgWriter`, for a quick look in a browser while working on L1.
