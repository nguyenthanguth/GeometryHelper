# Plotting to PDF

How a space of the presenter is plotted, as AutoCAD 2021 plots it: the page setup a layout keeps, the sheet the plot is
laid on, the plot style tables, the PDF the presenter writes, and the commands PLOT (Ctrl+P), -PLOT, PREVIEW and
PAGESETUP. The page setup and the sheet are worked out by
[`CadPlotSettings`](xref:GeometryHelper.Dxf.Viewer.CadPlotSettings) and
[`CadPlotPage`](xref:GeometryHelper.Dxf.Viewer.CadPlotPage) with no window; the PDF is written by the controls'
`CadPdfWriter`, through SkiaSharp. Every example below was run on a plan of model space: a frame from (120, 60) to
(7 620, 5 360), 7 500 by 5 300, with ROOM 1 inside it in Arial.

## The page setup

A drawing keeps how each space is plotted in the plot settings of its LAYOUT, model space's in the layout Model: the part
AcDbPlotSettings that AutoCAD writes. [`CadPlotSettings.Read`](xref:GeometryHelper.Dxf.Viewer.CadPlotSettings) takes them
from there, and [`ApplyTo`](xref:GeometryHelper.Dxf.Viewer.CadPlotSettings) writes them back as one undo step named PLOT,
as Apply to Layout does:

```csharp
CadPlotSettings settings = CadPlotSettings.Read(document, document.ModelSpace);
settings.Area = CadPlotArea.Window;
settings.Window = new DisplayBox(120, 60, 7620, 5360);
settings.FitToPaper = false;
settings.PaperUnits = 1;
settings.DrawingUnits = 20;                                     // 1:20
settings.StyleTable = CadPlotStyleTable.Monochrome;
CadEditResult result = settings.ApplyTo(document, document.ModelSpace);
// the layout Model: 74 of 4 (a window), 48 49 140 141 the window, 142 143 of 1 and 20, 75 of 23 (1:20), 7 monochrome.ctb
```

| Setting | Groups of AcDbPlotSettings |
|---|---|
| the plotter, `GeometryHelper PDF` | 2 |
| the sheet, `ISO_A3_(420.00_x_297.00_MM)`, its size and margins in millimetres | 4, 44, 45, 40 to 43, 72 of 1 |
| the turn: landscape or portrait as AutoCAD reckons it from the sheet as named, plus 180 upside down | 73 |
| the offset from the printable area's corner | 46, 47 |
| what is plotted: Display, Extents, Limits, Window or Layout | 74, the window 48, 49, 140, 141 |
| the scale: paper millimetres to drawing units; 75 with 147 and flag 16 where it is one of AutoCAD's standard scales, 75 of 0 for Fit | 142, 143, 75, 147 |
| the plot style table: `monochrome.ctb`, `grayscale.ctb`, or none | 7 |
| centred (4), plot with plot styles (32), scale lineweights (64), plot lineweights (128) | 70 |

Read back after the drawing is saved and opened again, every one of them is as it was written. A DXF of AC1027 and one of
AC1032 saved after a page setup applied to Model and to a layout pass AutoCAD 2021's AUDIT with 0 errors. Plot transparency
is not written: a DXF keeps it in no group of the plot settings, and the display lists hold no transparency, so a plot is
the same with it on or off.

A sheet the layout names is the standard one of its name and margins, else one made of the layout's size and margins,
so a layout of `LAYOUT New`, A3 with margins of 7.5 at the sides and 20 above and below, plots on the sheet its tab shows.
The page setups of the drawing's ACAD_PLOTSETTINGS are listed by
[`PageSetupNames`](xref:GeometryHelper.Dxf.Viewer.CadPlotSettings) and read by
[`ReadPageSetup`](xref:GeometryHelper.Dxf.Viewer.CadPlotSettings); a new one is not added yet.

## The sheet

[`CadPaper.Standard`](xref:GeometryHelper.Dxf.Viewer.CadPaper) lists the sheets of GeometryHelper PDF: ISO A0 to A4 and
ANSI A to E, each on its side, with margins of 5 all round. [`CadPlotPage.Compute`](xref:GeometryHelper.Dxf.Viewer.CadPlotPage)
lays a plot on its sheet:

```csharp
CadPlotPage page = CadPlotPage.Compute(document, document.ModelSpace, settings, view.VisibleBox);
// page.SheetWidth 420, SheetHeight 297; Printable (5, 5) to (415, 292)
// page.Scale 0.05; OffsetX 17.5, OffsetY 11: the plot 375 by 265, centred
// page.DrawingToSheet: (120, 60) at (22.5, 16), (7 620, 5 360) at (397.5, 281)
// page.SheetWidthPoints 1 190.55, SheetHeightPoints 841.89
```

| Case | What it gives |
|---|---|
| Fit to paper | the larger scale that keeps the area in the printable area: 1 mm to 18.4669 units for the window above, 406.13 by 287, moved (1.93, 0) |
| Portrait | the sheet upright, 297 by 420, its margins turning with it |
| Upside down | the sheet turned half round about its middle after the plot is laid: (120, 60) at (397.5, 281) |
| Not centred | the area's lower left corner at the printable corner plus the offset: (3, 4) puts (120, 60) at (8, 9) |
| Layout | a layout's sheet, its origin at the printable corner plus the offset, at 1:1 unless fitted: the paper (-7.5, -20) to (412.5, 277) of an A3 layout lies on the whole sheet |
| Limits | LIMMIN to LIMMAX in model space, a layout's own limits in a layout |
| Display | what the view shows, the extents where no view is given |

[`CadPlotScale.ListFor`](xref:GeometryHelper.Dxf.Viewer.CadPlotScale) gives the Scale list: 1:1, 1:2, 1:5, 1:10, 1:20,
1:50, 1:100, 1:200, 1:500 and 1:1000, then the scales of the drawing's own list that are none of those.

## Plot style tables

The three tables are made in code; no `.ctb` file is read. [`CadPlotStyles.Map`](xref:GeometryHelper.Dxf.Viewer.CadPlotStyles)
gives the colour an object plots in:

| Table | Red | Green | Blue | White (colour 7) |
|---|---|---|---|---|
| Monochrome | black | black | black | black |
| Grayscale, 0.299 red + 0.587 green + 0.114 blue | #4C4C4C | #969696 | #1D1D1D | black |
| None | red | green | blue | black |

With Plot with plot styles off, the colours plot as with None.

## The PDF

`CadPdfWriter.Write(path, document, space, settings, display)` writes one page through SkiaSharp's `SKDocument.CreatePdf`,
in vectors, from the display list the canvas draws, as a plotter draws rather than as the screen does: every item whatever
its size, every text however small, every line of a hatch, arcs flattened to a twentieth of a point, colours through the
table, and each line at its lineweight on the sheet, 0.5 mm as 1.417 points (scaled with the plot where Scale lineweights
is ticked), the thinnest line the reader draws where Plot object lineweights is off. The plot is clipped to the printable
area and to the area plotted. Solids and hatches are filled.

The page is the sheet's exact size: ISO A3 on its side is 1 190.5512 by 841.8898 points, where SkiaSharp alone writes 1 191
by 842. The text of a TrueType font is written as text with its font embedded and a map from its glyphs to its letters, so
a reader finds and copies ROOM 1; the text of an SHX font is its strokes, as the canvas draws it, and is not found. The
font is embedded whole, so a PDF with Arial in it is 543 KB at least.

PDFs are written one at a time: SkiaSharp's PDF writer shares the typefaces its font manager caches, and two pages written
at once lost the map of their text in 3 to 6 of 64.

| Plot of the extents on ISO A3, best of 5 | .NET Framework 4.8 | .NET 8 | File |
|---|---|---|---|
| 100 lines and 1 text | 24.0 ms | 22.3 ms | 543 KB |
| 10 000 lines and 100 texts | 42.1 ms | 41.3 ms | 744 KB |
| 100 000 lines and 1 000 texts | 200.1 ms | 180.7 ms | 2 563 KB |

## The commands

| Command | What it does |
|---|---|
| PLOT, Ctrl+P | the Plot dialog, expanded, on the space's page setup; OK asks for the file and writes the PDF, or the PNG or JPEG of a raster device; with no window, -PLOT's prompts |
| PAGESETUP | the same dialog titled Page Setup; OK writes the page setup to the layout |
| PREVIEW | the plot preview of the space's page setup over the whole window; its Plot asks for the file and writes the PDF |
| -PLOT | AutoCAD's plot on the command line |

The dialog follows AutoCAD's: the page setup's name; the plotter, GeometryHelper PDF, with the printers of Windows listed
greyed, since printing to them is not built; the paper size and its small picture; what to plot, and for a window the
Window&lt; button, which hides the dialog while "Specify first corner:" and "Specify opposite corner:" are answered on the
drawing and shows it again with the window set; the offset, or Center the plot; Fit to paper, or the scale and its two
units; the plot style table; the plot options; and the orientation. Apply to Layout writes the page setup at once, Save
changes to layout writes it when OK plots. Preview… shows the preview, whose Plot plots.

The preview shows the sheet on grey with its printable area dashed. A drag pans, zooms in real time (100 pixels up zoom
twice in) or frames a window, as the toolbar or the right button's menu chooses; the wheel zooms about the cursor; Zoom
Original shows the whole sheet; ESC or ENTER leaves it.

-PLOT asks AutoCAD 2021's prompts:

```text
Detailed plot configuration? [Yes/No] <No>: Y
Enter a layout name or [?] <Model>:
Enter an output device name or [?] <GeometryHelper PDF>:
Enter paper size or [?] <ISO A3 (420.00 x 297.00 MM)>:
Enter paper units [Inches/Millimeters] <Millimeters>:
Enter drawing orientation [Portrait/Landscape] <Landscape>:
Plot upside down? [Yes/No] <No>:
Enter plot area [Display/Extents/Limits/Window] <Display>: W
Enter lower left corner of window <0.000000,0.000000>: 120,60
Enter upper right corner of window <0.000000,0.000000>: 7620,5360
Enter plot scale (Plotted Millimeters=Drawing Units) or [Fit] <Fit>: 1=20
Enter plot offset (x,y) or [Center] <Center>:
Plot with plot styles? [Yes/No] <Yes>:
Enter plot style table name (enter . for none) or [?] <monochrome.ctb>:
Plot with lineweights? [Yes/No] <Yes>:
Enter shade plot setting [As displayed/legacy Wireframe/legacy Hidden/Visual styles/Rendered] <As displayed>:
Enter file name <C:\Plans\plan-level2-Model.pdf>:
Save changes to page setup [Yes/No] <No>: Y
Proceed with plot [Yes/No] <Yes>:
Effective plotting area:  375.00 wide by 265.00 high
```

Without the detailed configuration it asks the layout, a page setup's name, the device and the file. In a layout the
area offers Layout in place of Limits, and the last prompts are Scale lineweights, Plot paper space first and Hide
paperspace objects; the last two, like the shade plot, change nothing in a 2D plot. View is not offered: the presenter has
no named views. The file is written by the window, which holds the PDF writer.

## Plotting to an image

PLOT and -PLOT offer two raster devices beside GeometryHelper PDF: GeometryHelper PNG, which writes a PNG, lossless, and
GeometryHelper JPG, which writes a JPEG at a quality of 90. Their Paper size list holds sizes in pixels rather than
sheets: 1280 x 720 (HD), 1600 x 1200 (UXGA), 1920 x 1080 (Full HD), 2560 x 1440 (QHD) and 3840 x 2160 (4K UHD), then the
sizes of your own, then Custom…, which asks a width and a height of 16 to 8 000 pixels each in Custom Image Size and adds
the size to the list as `2480 x 1754 Pixels (Custom)`. The ten newest sizes of your own are kept in the settings file
from one session to the next.

Choosing a raster device starts on 1920 x 1080, fitted to the image and centred, with no plot style table: the image is
drawn on white, as the paper, every colour as displayed except colour 7, which plots black. The whole image is printable,
the offset and the scale read in pixels (Fit to paper off, the scale is pixels per drawing unit), and each line is as wide
as its lineweight at 96 dots an inch, 0.5 mm being 1.9 pixels. The DPI box is grey, the size being set in pixels. The
plot is always to a file: OK asks for it through Browse for Plot File, offered as `plan-level2-Model.png`, or with FILEDIA
0 on the command line, `Enter file name <C:\Plans\plan-level2-Model.png>:`. Model space fitted to 1920 x 1080 is a PNG of
exactly 1920 by 1080 pixels. Going back to GeometryHelper PDF gives back the sheet chosen before.

-PLOT takes the device by its name, and then the size as listed or as `2480 x 1754`; its units are pixels alone:

```text
Enter an output device name or [?] <GeometryHelper PDF>: GeometryHelper PNG
Enter paper size or [?] <1920 x 1080 Pixels (Full HD)>: 2480 x 1754
Enter paper units [Pixels] <Pixels>:
...
Enter plot scale (Plotted Pixels=Drawing Units) or [Fit] <Fit>:
...
Enter file name <C:\Plans\plan-level2-Model.png>:
```

A page setup saved with a raster device keeps the device's name and the size, its units written as pixels (group 72 = 2),
and is read back as that image. `CadRasterWriter.WritePlot(path, document, space, settings, display)` writes the image
where there is no window.

## Images of the drawing area

PNGOUT, JPGOUT and BMPOUT write the drawing area as it is shown, as AutoCAD 2021's do. The window asks for the file in
its Create Raster File dialog; with no window the command asks on the command line, offering the drawing's name beside
it. A name typed without the extension has it added, and so does one with another: `plan.jpeg` given to JPGOUT is
written as `plan.jpeg.jpg`, as AutoCAD writes it. A name with no folder goes beside the drawing.

```text
Command: PNGOUT
Enter file name <C:\Plans\plan-level2.png>: C:\Plans\level2
Select objects or <all objects and viewports>:
```

Enter writes the current view of model space or the layout at the drawing area's size in pixels (a canvas 1 600 by 900
at 150 per cent is an image of 2 400 by 1 350), on its background colour, with the grid where it is shown, and without
the cursor, the grips, the UCS icon or the highlight of a selection. Objects selected are written alone in the same view,
without the grid. A PNG is 8-bit red, green and blue; a JPEG is written at a quality of 90; a bitmap is 24 bits a pixel,
as BMPOUT's. The same view gives the same bytes. Nothing is printed when the file is written, and the drawing is not
changed, so there is no undo step. With no window, `CadRasterWriter` of the window's library
writes the [`CadImageJob`](xref:GeometryHelper.Dxf.Viewer.CadImageJob) the command hands over, at the session's view, or
at 1 920 by 1 080 showing the extents where there is none. A view of 1 920 by 1 080 of a drawing of 100 000 entities is
written as a PNG of 1.2 MB in 0.25 s (.NET Framework 4.8; 0.20 s on .NET 10).

## EXPORT and -EXPORT

EXPORT asks for the file in the window's Export Data dialog, which lists the types AutoCAD 2021's help gives for it: 3D
DWF, 3D DWFx, ACIS, Bitmap, Block, DXX Extract, Encapsulated PS, IGES, Lithography, Metafile, V7 DGN and V8 DGN
([`CadExportJob.ExportFileTypes`](xref:GeometryHelper.Dxf.Viewer.CadExportJob)). Bitmap goes on as BMPOUT does, with
`Select objects or <all objects and viewports>:`. The presenter has no writer for the other types, so EXPORT refuses them
and writes nothing: `Metafile (*.wmf) has no writer in the presenter: nothing was exported.`, with a pointer to PNGOUT,
JPGOUT and BMPOUT, PLOT and -EXPORT, and SAVEAS. AutoCAD's EXPORT lists no DXF, PNG, JPG or PDF, and neither does this
one. Bitmap is offered first; after that, the type last chosen is offered for the rest of the session. With FILEDIA 0, or
with no window, EXPORT asks `Enter file name <C:\Plans\plan-level2.bmp>:`. A name ending in the extension of a listed type
takes that type, as `plan.dgn` takes V7 DGN; any other name has the extension of the type last chosen added.

-EXPORT asks AutoCAD 2021's prompts, as its console writes them:

```text
Command: -EXPORT
Enter file format [Dwf/dwfX/Pdf] <dwfX>P
Enter plot area [Display/Extents/Window] <Display>: E
Detailed plot configuration? [Yes/No] <No>:
```

On a layout the area is asked as `Enter plot area [Current layout/All layouts] <Current Layout>:`. Window asks
`Specify first corner:` and `Specify opposite corner:`. Yes at the detailed configuration asks the paper size, its units,
the orientation, the scale, the plot styles, the style table and the lineweights, in -PLOT's words. The file is then asked
in the window's Save As PDF dialog, or with FILEDIA 0 as `Enter file name <C:\Plans\plan-level2-Model.pdf>:`. -PLOT's
PDF writer writes it from the page setup of the space, with the area and the answers given. The format answered becomes
the default of the next -EXPORT, as AutoCAD keeps EXPORTEPLOTFORMAT. Dwf and dwfX have no writer, and neither does All
layouts, since the writer makes one sheet; -EXPORT refuses all three and writes nothing.

## See also

- [Editing as AutoCAD does](editing.md): the command line and the undo steps.
- [The sheet of a layout](../dxf/drawing.md#the-sheet-of-a-layout): the LAYOUT the page setup is kept in, and its sheet.
