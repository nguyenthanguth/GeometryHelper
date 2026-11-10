# The drawing, its tables and layouts

The drawing as a whole: opening and saving it, its header, its tables (blocks, layers, linetypes, dimension styles),
its dictionaries and groups, other drawings and external references, and its layouts.

## Opening and saving

`DxfDatabase.Open(path)` and `Open(stream)` read the bytes of a file, `Parse(text)` its text; `Save(path)` and `Save(stream)`
write them, and `ToText()` gives the text as it would be saved. What is kept is everything: the version, R12 (AC1009) to
R2018 (AC1032), as `Version` names it; the line ends, a carriage return and a line feed, a line feed alone, or both in one
file; the group codes padded to three places or not; the mark of UTF-8 or none; the text in the code page of a file before
R2007 and in UTF-8 after, byte for byte, `CodePage` naming it (`ANSI_1252`, `UTF-8`); comments (999); the sections, tables,
objects and entities the library does not read (CLASSES, objects of a type with no class and the rest); extended data,
reactors and extension dictionaries; and whatever follows the EOF. A file is read as [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader) reads it (see [Reading
DXF](reader.md#reading-dxf)), and one it refuses is refused the same way, with the same message: a binary DXF, one cut short, one whose
header names a code page that does not read each byte of ASCII as itself. `Warnings` says what was said of reading its text,
a file before R2007 whose text is UTF-8 or one in a code page the runtime has not; it is empty for most files. A string
given to `Parse` is text already read, so one of R2007 or later, or one all ASCII, is kept as UTF-8, and one of an earlier
version with letters beyond ASCII in the code page its header names (ANSI_1252 where it names none), each letter the page
lacks as its escape, as AutoCAD would have saved it, and what is changed after is written the same way: an MTEXT café Đ
in a string of R2004 is saved as caf, the byte E9 and `\U+0110`, which AutoCAD 2021 reads as café Đ. Saved as the UTF-8
of the string, as before, AutoCAD read é as Ã© and refused the drawing outright where a layer was named so.

What is written is in the file's own style: its line end, its padding, the numbers as [`DxfWriter`](xref:GeometryHelper.Dxf.DxfWriter) prints them, and the text
in its encoding, a letter the page cannot hold, or whose bytes would read back as UTF-8, written as its escape (`\U+00E9`).
A file that names no version is written as one of R12, or of R2000 where its records carry subclass markers. Held against
ezdxf 1.4.5 on its drawings of R12, R2000, R2007 and R2018, recover and audit find nothing to fix in what the model writes,
ezdxf reads each value as set, and every untouched record keeps its bytes (186, 306, 359 and 361 of them; of the two
more the header has, `$PEXTMIN` and `$PEXTMAX`, which ezdxf leaves empty, grow to hold what the edits draw in paper space).

## Damaged files

`DxfDatabase.Recover(path, out report)`, or `Recover(stream, out report)`, opens a damaged file as AutoCAD's RECOVER does: what can be read, repaired, with a [`DxfRepairReport`](xref:GeometryHelper.Dxf.DxfRepairReport) of what was found, each a [`DxfRepair`](xref:GeometryHelper.Dxf.DxfRepair) of a `DxfRepairKind`, a sentence with its line or handle, fixed or not. A sound file is opened as `Open` opens it, every byte kept, and the report is empty: an architectural drawing of AutoCAD 2010 of 21.8 MB and 36 112 entities gives "No errors found." in 0.25 s on .NET 10 and 0.36 s on .NET Framework. The file is read as pairs of lines whatever is wrong with them, a line that is not a group code left out and the next read as the code; a file cut short is closed with its ENDSEC and EOF; sections are closed and put in order; a number that is no number is read as the number it begins with (`500mm` as 500), or nought; an entity with a number that is not finite is left out; the counts of a LWPOLYLINE and a HATCH are set to their parts; a handle two records have, or one missing, is given a new one, and an owner that names nothing is pointed at the block, table or polyline the record stands in. Layers, linetypes, text styles and dimension styles the entities name and the tables lack are added. An INSERT of a block not defined, a type unknown, and a missing TABLES, BLOCKS or OBJECTS section are reported and left. `Audit(fix)` checks an open drawing the same way and, with `true`, fixes in place what does not need the file written again. In the presenter, RECOVER and AUDIT are commands, and a file that cannot be read as it is opens as `<name> (recovered)`, unsaved.

## The drawing

### The header

The header is read and set raw by `GetHeader("$INSUNITS")` and `SetHeader("$LTSCALE", new DxfTypedValue(40, "2"))`, and
through AutoCAD's properties: `Clayer`, `Insunits`, `Extmin`, `Extmax`, `Handseed`, `Celtype`, `Cecolor`, `Celweight`,
`Ltscale`, `Limmin`, `Limmax`, `Insbase`, `Pdmode`, `Pdsize`, `Textsize`, `Textstyle`, `Dimstyle`, `Lunits`, `Luprec`,
`Aunits`, `Auprec`, `Angbase` (in radians; the file writes degrees), `Angdir`, `Orthomode`, `Fillmode`, `Mirrtext`,
`LineWeightDisplay`, `Saveproxygraphics`, `Splinesegs`, `Splinetype`, `Surftype`, `Surfu` and `Surfv`. A variable R2000
brought, `$INSUNITS`, `$CELWEIGHT`, `$LWDISPLAY` and `$PROXYGRAPHICS` among them, is refused in a file of R12, as AutoCAD
writes none there. Setting `Clayer` to a layer the table does not list adds it, as an entity's `Layer` does.

Every other variable of the header AutoCAD's `Database` has as a member is read and set by its AutoCAD name in the same
way: `db.Chamfera = 10`, `db.Filletrad`, `db.TileMode`, `db.Plimmax`, `db.Useri1`, `db.Latitude`, `db.LoftAng1` and the rest,
as the API reference of [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase) lists them, each giving its variable (`$CHAMFERA`) and its default. Where the header
gives none, a variable reads as a new drawing of ezdxf 1.4.5 has it, but for `MaintenanceReleaseVersion` (nought),
`Pextmin` and `Pextmax` (the origin, as `Extmin`), the variables that name an object (null), and `PsolHeight` and
`PsolWidth` (80 and 5, as AutoCAD's metric drawing has them, where ezdxf writes 0.08 and 0.005 in metres). R2004 and R2007
brought some, which are refused in a file of an older version as those of R2000 are. A text (`HyperlinkBase`,
`ProjectName`) is read as written, its spaces kept, and a GUID set is written in braces and capitals, as AutoCAD writes it.
A few read differently from the file:

- the UCS: `Ucsorg`, `Ucsxdir` and `Ucsydir` read the header, and `db.Ucs = new GeoCoordinateSystem3(origin, xAxis, yAxis)`
  writes all three, as AutoCAD's `Ucs` does, its axes made of unit length and square; `Ucsname`, `UcsBase` and those of
  paper space are the name of a record of the UCS table, null for none; `WorldUcsBaseOrigin(DxfOrthographicView.TopView)`
  reads `$UCSORGTOP`.
- the times: `Tdcreate` and the other dates are a `DateTime` read from the Julian day the file writes (2451545.75 is
  1 January 2000 at 18:00), `Tdindwg` and `Tdusrtimer` a `TimeSpan`; `ResetTimes()` makes the drawing new now; `TimeZone` is
  one of AutoCAD's zones in [`DxfTimeZone`](xref:GeometryHelper.Dxf.DxfTimeZone), and `SetTimeZoneAsUtcOffset(-7)` picks `Mountain`, the first of three.
- the objects: `Cmaterial`, `dragvs`, `Interfereobjvs`, `Interferevpvs` and `CmlstyleID` give the MATERIAL, VISUALSTYLE or
  MLINESTYLE of the drawing's dictionary that the header names, null where it has none of that name, and take one of that
  dictionary; `ApproxNumObjects` counts the objects `GetObject` finds.
- the file: `Filename` is the full path the drawing was opened from or last saved to, `OriginalFileName` the one it was
  opened from; for a drawing parsed from text or opened from a stream the second is null, and the first until it is saved
  to a path.

### Dimension variables

The dimension variables of the header, those new dimensions take, are properties too, by AutoCAD's names: `db.Dimasz`
is `$DIMASZ`, `db.Dimfxlen` is `$DIMFXL`, 77 of them. Where the header gives none, a variable reads as AutoCAD's metric
drawing (acadiso.dwt) has it, as a new drawing of ezdxf 1.4.5 does: arrowheads and texts 2.5, extension lines 1.25 past the
dimension line and 0.625 off the points, two decimal places, a comma, trailing zeros left out, the text above the line;
`Dimaltf` 1 / 25.4 and `Dimjogang` pi / 4, where ezdxf writes them to 11 and 12 places. The arrowheads (`Dimblk`,
`Dimldrblk`), the linetypes (`Dimltype`) and the text style (`Dimtxsty`) are the records the header names, by name, null
where the drawing has none of the name; `db.Dimblk = db.BlockTable["_ARCHTICK"]` writes `_ARCHTICK`. `Dimjogang` is in
radians, as the header holds it. A value AutoCAD refuses is refused, a `Dimdec` of 9 or a `Dimlunit` of 7, and a variable a
later version brought is refused in a file of an older one: `Dimdec` (R2000) in R12, `DimAssoc` (R2004) and `Dimjogang`
(R2007) in R2000, `Dimtxtdirection` (R2010) in R2007. Such a variable reads as a dimension style of that file reads it,
AutoCAD's own (acad.dwt's: `Dimdec` 4, `Dimdsep` '.', `Dimtxsty` null in R12), so that the header's data and a style's agree
there, and set to that value it writes nothing. `db.GetDimstyleData()` gives them as a [`DxfDimStyleTableRecord`](xref:GeometryHelper.Dxf.DxfDimStyleTableRecord) in no table,
named as `Dimstyle`.

Three `SetDimstyleData` give a style's variables, each writing only those that differ from what it reads and checking every
value before it writes any; a record that names no text style leaves the text style as it is in all three:

- `db.SetDimstyleData(style)` makes them those new dimensions take, `Dimstyle` left as it is. A variable the header of the
  file's version has not is passed over whatever the record holds, as AutoCAD's SAVEAS to that version leaves it out:
  DIMARCSYM, which a style holds from R2000 and the header from R2007, in a drawing of R2000. A whole number out of AutoCAD's
  range is refused, as the properties refuse it.
- `dim.SetDimstyleData(style)` and `leader.SetDimstyleData(style)` write them as overrides. A variable the file's version has
  not is passed over where the record holds AutoCAD's own value and refused where it holds another, as their properties
  refuse it; a whole number out of AutoCAD's range, a `Dimdec` of 9, and a size of the arrows, the text or the scale below
  nought are refused, as the header refuses them.

### Extents

`$EXTMIN` and `$EXTMAX` hold model space and paper space. They grow to hold what is appended or moved, each axis on its
own, and an erase does not shrink them: an entity whose own record is written whole, as a LINE moved or a POLYLINE given a
new flag, grows them at once; a run of groups of its record written in place (a LWPOLYLINE's vertex, a SPLINE's knots) or
a record that follows it (a VERTEX put in or set in a POLYLINE, a point of a mesh) grows them by the entity as it stands
when the header is next read or the file saved, once however many vertices were written, so that a vertex moved out and
back between two readings leaves no trace. An INSERT appended, moved or copied by `WblockCloneObjects` grows them by what it
places, its `GeometricExtents`, each cell of a MINSERT's array among it (whose own `GeometricExtents` is its first cell's, as AutoCAD 2021's), read in the same way when the header is next read
or the file saved, so that its block is placed once and as the drawing then has it: block W of the line from (0, 0, 0) to
(10, 0, 0) inserted at (5100, 0, 0) grows them to x 5110. An attribute of an INSERT written places its block anew in the
same way. An INSERT that places nothing, its block not defined, an external reference or the block of a layout, or one
that would place more than `MaxPlacedEntities`, leaves them as they are. The block is placed again at each reading of the
header after the INSERT changed, and only where the header holds the extents: an INSERT placing 1 000 000 lines, moved and
the header read, takes about 2 s each time, and none where the header has no `$EXTMIN` and `$EXTMAX`. An entity of a
block appended, moved or changed leaves them as they are, as AutoCAD leaves them until the INSERTs of the block are regenerated. Extents the header leaves empty, the least
corner above the most, as the 1E+20 and -1E+20 ezdxf writes for a drawing it has not measured, become the box of what is
appended or moved, as AutoCAD grows them from empty: a line from (5000, -4000, 0) to (5100, -3900, 10) appended to such a
drawing makes `Extmin` (5000, -4000, 0) and `Extmax` (5100, -3900, 10), before saving and after. What the drawing held
already is not measured into them. An axis the wrong way round alone is made the box's along it and the others grown as
ever: `$EXTMIN` (0, 0, 0) and `$EXTMAX` (100, -50, 0) with a circle of radius 5 about (200, 10) appended are (0, 0, 0) and
(205, 15, 0).

`db.UpdateExt(true)` works the extents out anew, as AutoCAD's `UpdateExt` does: `$EXTMIN` and `$EXTMAX` the box of the
`GeometricExtents` of what model space holds, `$PEXTMIN` and `$PEXTMAX` that of paper space, so that they shrink as well as
grow. An entity with no extents is passed over, a text counts as the point it stands at, and a space with nothing of extents
in it is given AutoCAD's empty extents, 1E+20 for the least corner and -1E+20 for the most; a value the header has already,
to the bit, is not written. A header that still reaches (5110, 300, 0) for a line drawn and erased since, with a circle of
radius 5 about (100, 50) left in model space, is given (95, 45, 0) to (105, 55, 0). The library takes an INSERT's placed
shapes either way, so `doBestFit` changes nothing.

### Variables kept out of the header, and the drawing's properties

The drawing reads and sets the variables AutoCAD keeps out of the header, as DICTIONARYVAR objects of the dictionary
AcDbVariableDictionary: `Cannoscale` (a scale of the drawing's list of scales), `Cetransparency` (the number an entity's
440 holds, as AutoCAD 2021 reads it: the alpha 127 is 33554559, and a per cent written before reads BYLAYER), `Tablestyle`, `MLeaderstyle`, `DetailViewStyle` and `SectionViewStyle` (by name, one of the drawing's),
`MLeaderscale`, `AnnoAllVisible`, `MsLtScale`, `MsOleScale`, `LayerEval`, `LightingUnits`, `LightsInBlocks`,
`TStackAlign`, `TstackSize`, `HpInherit`, `Isolines`, `Facetres`, `PdfFrame` and `PlineEllipse`; AutoCAD's default where the
drawing gives none, as a drawing of ezdxf, to which a variable set adds the dictionary of variables. A drawing whose
CLASSES section lists no DICTIONARYVAR, as one the library makes new with `new DxfDatabase("AC1027")`, is given the class
as AutoCAD 2021 lists it when its first variable is set (`AcDbDictionaryVar` of ObjectDBX Classes, proxy flags 0, counted
once from R2004 on), so that `db.MLeaderstyle = "GH_NOTES"` in a new drawing is read by AutoCAD as CMLEADERSTYLE GH_NOTES;
a drawing with no CLASSES section, as one of R12, has none to set. `ThumbnailBitmap` reads
the bytes of the THUMBNAILIMAGE section.

The drawing's properties are `db.SummaryInfo`, set as AutoCAD sets them:
`db.SummaryInfo = new DxfDatabaseSummaryInfoBuilder(db.SummaryInfo) { Title = "Plate B-12" }.ToDatabaseSummaryInfo()`. A
drawing AutoCAD saved as R2004 or later keeps them in its header and has no DWGPROPS: its texts in `$TITLE`, `$AUTHOR` and
the like, and each custom property a `$CUSTOMPROPERTYTAG` followed by a `$CUSTOMPROPERTY`, as AutoCAD 2021 saved a drawing of
R2018 titled Plate B-12 with Project and Checked by after its `$DWGCODEPAGE`. They are read from there, and set there in a
drawing of R2004 or later, as many custom properties as there are, an empty text leaving its variable out and a DWGPROPS the
drawing had taken out, as AutoCAD saved such a drawing, in AutoCAD 2021's order after `$DWGCODEPAGE`: `$TITLE`, `$SUBJECT`,
`$AUTHOR`, `$KEYWORDS`, `$COMMENTS`, the header's `$LASTSAVEDBY` as it was, `$REVISIONNUMBER`, then the pairs. A drawing of R2000 keeps them in its DWGPROPS, as AutoCAD 2021 saves
one: its custom properties are read from the groups 300 to 309 (`name=value`) and from a list after a group 90 (a count,
then each name and value in two groups 1), and written in the ten groups, their count after the base of the hyperlinks,
then each after the tenth in a group 300 after the count, as AutoCAD 2021 writes and reads twelve: it reads none of the
list. The base of the hyperlinks is written to the
header's `$HYPERLINKBASE` too, where AutoCAD reads it, and read from there where it is not empty.

## The tables

### Blocks

`ModelSpace`, `PaperSpace` and each block of the `BlockTable` are a [`DxfBlockTableRecord`](xref:GeometryHelper.Dxf.DxfBlockTableRecord): its `Name`, `Origin`,
`IsLayout`, `IsAnonymous` and `IsFromExternalReference`, and its entities, each the [`DxfEntity`](xref:GeometryHelper.Dxf.DxfEntity) of its type's class or a
[`DxfUnknownEntity`](xref:GeometryHelper.Dxf.DxfUnknownEntity). Model space and paper space are named `*Model_Space` and `*Paper_Space`, as AutoCAD names them,
whatever the file calls them (ezdxf's files of R12 call them `$Model_Space` and `$Paper_Space`), and stand for their records
from the moment the drawing is opened, with their handles and extended data.
`BlockTable.Add(new DxfBlockTableRecord { Name = "BOLT", Origin = new GeoPoint3(0, 0, 0) })` adds a block, which is filled
by its own `AppendEntity`. A block named `*D` or `*U` is added as AutoCAD's `Add` adds one so named, an anonymous block
the table names itself: `*D1`, or `*U1`, the first number no block, INSERT, dimension or table of the drawing names (see
[the block that draws a dimension](annotation.md#the-block-that-draws-a-dimension)). A block given an attribute definition is marked as one (`HasAttributeDefinitions`, flag 2 of its
BLOCK), model space too, and keeps the mark when the definition is erased, as AutoCAD does; AutoCAD takes no value a
multileader gives the attributes of a block without it. A block has its
`Comments` (the BLOCK's 4), `Units`, `Explodable` and `BlockScaling` (its BLOCK_RECORD's 70, 280 and 281, from R2007 on),
`PreviewIcon` (the bytes of its 310), `IsDynamicBlock` and `Hyperlinks`. `GetBlockReferences(true)` gives its INSERTs, as
AutoCAD's `GetBlockReferenceIds` gives their ids, and `GetBlockReferences(false)` those of the blocks that hold one of it too
(all of 2 000 nested blocks in 0.02 seconds); `GetAnonymousBlocks()` gives the blocks AutoCAD made of a dynamic block.
Setting its `Name` renames it with every INSERT of it, and the arrows the overrides of a dimension of R12 name by name
(`1070 5`, `6` or `7`, then `1000` the name), as a drawing of R2000 or later names them by handle, which needs no renaming;
`Erase()` takes it out, BLOCK, entities, ENDBLK and BLOCK_RECORD, where nothing inserts it, and an INSERT made of it before
is refused from then on, as one of a block the drawing has not; and `AssumeOwnershipOf(entities)` moves entities of the
drawing into it, each keeping its handle, out of the draw order (SORTENTSTABLE) of the block it leaves and onto the end of
that of the block it enters. 3 000 blocks nested one inside the next are built from the bottom up, each added and given an
INSERT of the one before, in 0.11 seconds, and erased one by one, each looked up by name, in 0.2 seconds.

A block's draw order is its [`DxfDrawOrderTable`](xref:GeometryHelper.Dxf.DxfDrawOrderTable), read by `DrawOrderTable`
(null where the block has none) or made by `CreateDrawOrderTable()` under ACAD_SORTENTS in the block's extension dictionary,
as AutoCAD's `DrawOrderTable` is: `GetFullDrawOrder()` gives the entities back to front, and `MoveToTop`, `MoveToBottom`,
`MoveAbove` and `MoveBelow` reorder them, giving the sort handles out again in the new order and writing only the entries
that differ from an entity's own handle.

### Layers

The `LayerTable` holds a [`DxfLayerTableRecord`](xref:GeometryHelper.Dxf.DxfLayerTableRecord) for each layer, found by its name in either case: its `Color`, `ColorIndex`,
`TrueColor`, `LinetypeName`, `LineWeight`, `IsOff`, `IsFrozen`, `IsLocked` and `IsPlottable`; its `Description` and
`Transparency`, kept in the extended data of AcAecLayerStandard and AcCmTransparency as AutoCAD keeps them;
`ViewportVisibilityDefault` (frozen in new viewports, flag 2); and `IsUsed`: layer 0, Defpoints, the current layer and any
layer an entity is on, those appended in code among them. The first `IsUsed` reads the drawing once for every layer, and the
answers hold until the drawing is edited: 400 layers of a drawing of 300 000 lines take 0.08 seconds. Setting the `Name` of
a layer renames it with every reference to it: the layer steel renamed Steel_S355 has every entity of it, in model space,
paper space and the blocks, its vertices and attributes among them, on Steel_S355, a layer named in extended data (1003) too,
and `$CLAYER` where it was the current one; layer 0 and Defpoints keep their names. `Erase()` takes a layer out where nothing
is on it, nothing points at it and it is not 0, Defpoints or the current layer, the table's count lowered by one. The
LAYER_INDEX and LAYER_FILTER objects a drawing may keep are left as they are.

A layer's look in a viewport is `layer.GetViewportOverrides(viewport)`, a [`DxfLayerViewportProperties`](xref:GeometryHelper.Dxf.DxfLayerViewportProperties) of its colour,
linetype, lineweight, transparency and plot style there, each the override AutoCAD keeps in the layer's extension
dictionary (ADSK_XREC_LAYER_COLOR_OVR and the four others) else the layer's own. Set, each is written as AutoCAD 2021 writes
it: steel given the colour 1 in the viewport 226 holds the XRECORD ADSK_XREC_LAYER_COLOR_OVR of
`102 {ADSK_LYR_COLOR_OVERRIDE, 335 226, 420 -1023410175, 102 }`, a second viewport's list after the first. A plot style is
refused in a drawing plotted by colour, as AutoCAD answers ePlotStyleInColorDependentMode. `RemoveColorOverride()` and its
like take one out, `layer.RemoveViewportOverrides(viewport)` those of one viewport, `RemoveAllOverrides()` them all, and
erasing a viewport takes its overrides out of the layers. The layers frozen in a viewport are its own:
`viewport.FreezeLayersInViewport(layers)`, `ThawLayersInViewport` and `GetFrozenLayers()` (group 331), and
`layer.ViewportVisibilityDefault` freezes a layer in the viewports made from now on (flag 2 of group 70).

### Named views

`db.ViewTable` holds the named views as AutoCAD's `ViewTable`, each a
[`DxfViewTableRecord`](xref:GeometryHelper.Dxf.DxfViewTableRecord): `db.ViewTable.Add(new DxfViewTableRecord { Name =
"Stair core", CenterPoint = new GeoPoint2(18450, 6200), Height = 9000, Width = 14400 })` writes it as AutoCAD 2021's
-VIEW Window writes one, looking down Z with a lens of 50; `IsPaperspaceView` marks a layout view (flag 1 of group 70),
and `LayerState` names the layer state its layer snapshot is kept in, `ACAD_VIEWS_Stair core` as AutoCAD 2021 names it,
the group 302 of the XRECORD ADSK_XREC_VTRVIEWINFO of its extension dictionary. Setting a value it has changes no byte;
`Name` renames it, and `Erase` takes it out with its extension dictionary.

### Layer states and filters

The layer states and layer filters a drawing keeps in the layer table's extension dictionary are read and listed:
`db.LayerStateManager.GetLayerStateNames()` gives the entries of ACAD_LAYERSTATES, Plan and Sections for a drawing with the
two, `GetLayerStateNames(false, false)` leaves out a state hidden (the flag 0x8000 of its mask, group 91, as AutoCAD 2021
saves one) and one of an xref (its layers named by name, group 8, not by handle), `HasLayerState("plan")` finds one in
either case and `LayerStatesDictionary` is the dictionary, each state an XRECORD of it. `db.LayerFilters.Root` is All, and
nested in it, as AutoCAD 2021 lists them, the property filters and groups of AutoCAD 2005 and later (the XRECORDs of
ACLYDICTIONARY, each named by its group 300), the filters of AutoCAD 2004 and earlier (the entries of ACAD_LAYERFILTERS,
AutoCAD keeping a property filter in both, given once), then the three AutoCAD makes, All Used Layers, Unreconciled New
Layers and Viewport Overrides: a drawing with the property filter GH_STEEL and the group GH_NOTES gives those two, then the
three. A state is not saved, restored, renamed or deleted, and a filter not changed. A drawing's place on the earth,
AutoCAD's `GeoLocationData`, is a [`DxfGeoData`](xref:GeometryHelper.Dxf.DxfGeoData), `db.GeoDataObject` the GEODATA model space's extension dictionary names
ACAD_GEOGRAPHICDATA, null where the drawing holds none:
`new DxfGeoData { BlockTableRecord = db.ModelSpace, TypeOfCoordinates = DxfTypeOfCoordinates.Local, ReferencePoint = new GeoPoint3(500000, 4100000, 0), HorizontalUnits = DxfUnits.Millimeters, HorizontalUnitsScale = 0.001 }.PostToDb()`
puts a drawing of R2010 or later in millimetres on a local grid at 500 000 east and 4 100 000 north, the GEODATA it had
erased and the class GEODATA listed where the drawing lists none; a drawing older than R2010 is refused, and so is any block
but model space, as AutoCAD 2021 refuses paper space. A `new DxfGeoData()` is AutoCAD's new one: of version 3 (90), of no
known type of coordinates, in metres, its scale judged at the reference point; its class is listed with a count of 1, as
AutoCAD 2021 lists it. `NorthDirection` is the angle of `NorthDirectionVector` from
the Y axis towards X, as AutoCAD gives it: (1, 0) is a quarter turn, (-1, 0) minus one. The class is
listed in the CLASSES section, so a file of R2010 or later written with none is given the GEODATA and no class for it. Its
design and reference points, units and their scales, up and north directions, how the scale is judged, the correction to
the sea's level, the coordinate system's XML (in pieces of 255 characters, `^J` a line feed, as ezdxf writes it, a piece
never ending between the two halves of a letter of two units of UTF-16), the tags and the points of the mesh
(`GetMeshPointMaps`, `SetMeshPointMaps`, `AddMeshPointMap`, `ResetMeshPointMaps`) are read and set. One of version 1 (R2009)
has its points of the mesh read from its groups 12 and 13, as a comment of ezdxf maps them, though ezdxf 1.4.5 itself reads
no points from it; it is not given a north direction or points of its mesh. Taking a point to latitude and longitude needs
the coordinate system's projection and is not there.

### Linetypes

The `LinetypeTable` holds a [`DxfLinetypeTableRecord`](xref:GeometryHelper.Dxf.DxfLinetypeTableRecord) for each linetype: its `AsciiDescription`, `PatternLength`,
`NumDashes` and `DashLengthAt`, a new one of simple dashes added by `Add`. A linetype's dashes carry their shapes and texts:
AutoCAD's gas line is `SetTextAt(1, "GAS")`, `SetShapeStyleAt(1, db.TextStyleTable["Standard"])`, `SetShapeScaleAt(1, 0.1)`
and `SetShapeOffsetAt(1, new GeoVector2(-0.1, -0.05))` on its gap of 0.2, written `49 -0.2, 74 2, 75 0, 340` the style's
handle `, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`; a dash of a file has its length set too, and a linetype nothing draws
with is erased. A text style is added to the `TextStyleTable`,
`new DxfTextStyleTableRecord { Name = "ROMANS", FileName = "romans.shx", TextSize = 2.5 }`, and its font, height, width,
slant and TrueType `Font` (a [`DxfFontDescriptor`](xref:GeometryHelper.Dxf.DxfFontDescriptor), `1001 ACAD, 1000 Arial, 1071 34` as AutoCAD keeps it) are set.

### Dimension styles

The dimension styles are the `DimStyleTable`, each a `DxfDimStyleTableRecord` with AutoCAD's 74 variables of a DXF file,
each read and set in its group: `Dimasz` 41, `Dimtxt` 140, `Dimdec` 271, `Dimjogang` 50 (radians; the file writes degrees),
`Dimtfillclr` the record's second 70 (the first is its flags), `Dimtxtdirection` 294 (from R2010), the arrowheads `Dimblk`, `Dimblk1`, `Dimblk2` and `Dimldrblk`
as the blocks they are drawn with (by handle, 342 to 344 and 341; by name, 5 to 7, in R12, ARCHTICK for the block
_ARCHTICK; null for the closed filled one), `Dimtxsty` a [`DxfTextStyleTableRecord`](xref:GeometryHelper.Dxf.DxfTextStyleTableRecord) and `Dimltype`, `Dimltex1` and `Dimltex2`
linetypes. A variable the record gives no group for reads as AutoCAD's own, that of acad.dwt: arrowheads 0.18 long, four
decimal places. A style made in code, `new DxfDimStyleTableRecord { Name = "GH_25", Dimasz = 2.5 }`, is added whole, every
variable its file's version has, its handle in group 105 as AutoCAD writes a DIMSTYLE's; renamed by its `Name`, the
dimensions, leaders and tolerances drawn in it, the styles AutoCAD keeps for its kinds of dimension (GH_25$0) and
`$DIMSTYLE` follow; `Erase()` takes out one nothing is drawn in that is not Standard or the current style.

## Dictionaries, records of data and groups

### Dictionaries

The drawing's own dictionaries are objects: `NamedObjectsDictionary`, and those it names, `GroupDictionary` (ACAD_GROUP),
`LayoutDictionary` (ACAD_LAYOUT), `MLeaderStyleDictionary`, `TableStyleDictionary` and the others AutoCAD's `Database`
names by id; null where a drawing has none, as one of R12. `SetAt` adds a [`DxfDBDictionary`](xref:GeometryHelper.Dxf.DxfDBDictionary), a [`DxfXrecord`](xref:GeometryHelper.Dxf.DxfXrecord) or a [`DxfGroup`](xref:GeometryHelper.Dxf.DxfGroup)
made in code; `TreatElementsAsHard` says whether a dictionary owns its entries hard (280 of 1, its entries named by 360),
and `CopyTo` copies its entries into an array. An XRECORD is a `DxfXrecord`, its `Data` the groups after its cloning flag,
the groups 102 of a list among them:

```csharp
db.NamedObjectsDictionary.SetAt("GH_PLATE", new DxfXrecord
{
    Data = new[] { new DxfTypedValue(1, "B-12"), new DxfTypedValue(40, "12.5") },   // a text and a real kept with the drawing
});
```

### Extension dictionaries and extended data

Every object can be given an extension dictionary, `CreateExtensionDictionary()`, an empty DICTIONARY owned by it and
written as AutoCAD writes one, reached as its `ExtensionDictionary`: `SetAt("GH_DATA", new DxfDBDictionary())` adds an
entry, `SetName` renames one, `Remove` takes one out with its object, and `ReleaseExtensionDictionary()` takes the
dictionary out once it is empty. `SetBinaryDataForKey("GH_BIN", bytes)` keeps bytes there, in an XRECORD of groups 310 of
127 bytes each, and `GetBinaryDataForKey` reads them back. `Remove` of an entry erases the dictionaries and records it owns at
every depth, and an object erased takes its extension dictionary with everything in it.

Extended data is `XData`, a list of [`DxfTypedValue`](xref:GeometryHelper.Dxf.DxfTypedValue)s set application by application as AutoCAD sets it, each application
named in the groups given having its data replaced and one the table of applications does not list registered:
`line.XData = new[] { new DxfTypedValue(1001, "GH"), new DxfTypedValue(1000, "B-12") }`. `GetXDataForApplication("GH")`
gives one application's. A text of extended data is read with its escapes as letters, as a layer's name is, so that a letter
the file's code page has not, written as `\U+0110`, comes back as the letter it was set as. `XDataTransformBy` moves the
extended data as AutoCAD moves it: a position of the world (1011) moved, a displacement (1012) turned and scaled, a direction
(1013) turned, a distance (1041) and a scale factor (1042) scaled, a plain point (1010) kept.

### Groups

A group is a `DxfGroup`, named by its entry of the group dictionary:
`db.GroupDictionary.SetAt("BEAMS", new DxfGroup("Beams of grid A", true))`, then `group.Append(beam)`, which writes the
beam's handle last among the group's (340) and the group into the beam's reactors, as AutoCAD writes a member. `Prepend`, `InsertAt`, `Remove`, `RemoveAt`, `Replace`,
`Reverse`, `Transfer` and `Clear` reorder the members, `GetAllEntityIds` gives them as entities, and `SetColorIndex`,
`SetLayer` and the other setters of the look set every member at once. A large group is filled by `Append` of a collection,
in one writing: one at a time is fine for groups as drawings have them (8 000 in half a second), but 32 000 take some 15
seconds. A group added under `*` is anonymous, `*A1` the first name its dictionary has not, and given a name, by its `Name`
or by `GroupDictionary.SetName`, it is anonymous no longer. An entity erased leaves its
groups at once. A group left empty is kept, named or anonymous, as AutoCAD 2021 keeps it, whatever emptied it: its last
member erased, `Clear`, `RemoveAt`, or never given one; the anonymous *A1, *A2 and *A3 so emptied are in the drawing AutoCAD
saves, each of no member.

## Other drawings, external references and tidying up

### Inserting another drawing

`db.Insert(transform, other, true)` inserts another drawing into model space, as AutoCAD's `Database.Insert` does: every
entity of its model space copied, as `WblockCloneObjects` copies entities together, and moved by the transformation, and the
records of its tables and its named blocks merged by name, this drawing's own kept where it has one of the name. A drawing
of a line from (0, 0, 0) to (10, 0, 0) on the layer steel, inserted by `GeoTransform3.Translation(new GeoVector3(100, 0, 0))`,
gives this one the line from (100, 0, 0) to (110, 0, 0) and the layer steel where it had none. `db.Insert("PLATE", other,
true)` makes the other drawing's model space the block PLATE instead, drawn about its base point (`$INSBASE`), and adds
nothing to model space; a name the other drawing has a block of is refused, as its block would come under the same name.
Every entity is checked before anything is written, so that a SPLINE into a drawing of R12, or an INSERT the transformation
would shear, refuses the whole; so does a block that inserts itself, X holding an INSERT of X or Y and Z inserting each
other, which AutoCAD's AUDIT takes out, and an entity of a type the library has no class for, a SHAPE or a REGION, in a model
space moved by anything but the identity, as its geometry is not moved (`NotSupportedException`). A block of it that would
come to insert itself only through an INSERT of this drawing of a block it has not is refused before anything is written
too, the blocks to be merged walked with those the copies bring: this drawing's P holding an INSERT of M, the other's A a
line and M an INSERT of P, neither A nor M comes. So is `db.Insert("NEWB", other, true)` where this drawing's P holds an
INSERT of NEWB and the other's model space one of P, which NEWB would hold. An INSERT of the other drawing that names a
block it has not, as a drawing read from a file may hold, is copied as it is, naming that block, wherever it stands: in its
model space, in a block merged or in one a copy brings, as AutoCAD 2021's `Insert` copies one, placing nothing (where it
used to be refused in model space and in a block merged, and copied in a block brought); one Debug line of
[`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) says how many there were and how many place a block of the name this drawing has. AutoCAD's own DXFIN
refuses a file holding such an INSERT ("Block "Z" was not defined"), and its `WblockCloneObjects` a set that reaches one, as
`WblockCloneObjects` here refuses one given among the entities. Where no INSERT of this drawing names a block it has not,
and none of the other's names the block `Insert(name, ...)` makes, no block can come to insert itself, and the blocks to
be merged are not walked for it. The extents grow by the
entities where they were moved to, and the other drawing is left as it is, whatever `preserveSourceDatabase` says. Its paper
space is not inserted, nor its external references but one an INSERT of its model space places, which comes with the INSERT
an external reference still, its BLOCK flagged 4 (12 overlaid) and naming its drawing as before. A dynamic block stays
dynamic, as AutoCAD's own insert keeps it: the extended data of its BLOCK_RECORD (AcDbDynamicBlockTrueName,
AcDbDynamicBlockGUID, AcDbBlockRepETag) and the evaluation graph with its nodes and the purge preventer of its extension
dictionary come with it, each anonymous block standing for it keeps its AcDbBlockRepBTag, and each INSERT its
representation data, the values it keeps and any history of grip edits. A block AutoCAD names itself is copied once for the
whole insert, however many INSERTs place it, dynamic or not: two INSERTs of one `*U` come as two INSERTs of one copy, as
AutoCAD's own insert keeps one `*U` for both (AutoCAD's saves of the checks 24d-d and 24d-g, inserted by AutoCAD in its
check v23), where the copy brought one for each; a block placed by one INSERT comes as before, to the bit. Every object
is copied under a handle of its own, and every handle among them names the copy of what it named, an entity of the block,
a node of the graph or the block itself, so that nothing points back into the other drawing; the classes of the objects are
listed and the applications of their extended data registered. A block GH_PANEL of
a Width of 100 with an INSERT of it set to 200 comes as GH_PANEL, dynamic, with an INSERT that reads Width 200, can be set
again, and stretches as the one it was copied from. A dynamic block of a name this drawing has is this drawing's own, as any
block of the name is: the INSERTs copied stand for it where it is dynamic, and are static copies otherwise. A drawing older
than R2010 (AC1024), into which the library writes no dynamic block, takes them all static. Copying what makes them
dynamic takes about a tenth of a millisecond for each INSERT that keeps values of its own: a drawing of 1 000 INSERTs of
GH_PANEL, 500 of them set, takes 30 milliseconds more to insert under .NET 10 and 65 more under .NET Framework 4.8. A
drawing of 10 000 blocks nested one inside the next is inserted in about a second, and 3 000 dimensions each drawn by a
block of its own are copied in under half a second; nothing of the drawing copied from is written
but what is read of it once, under a lock or kept by the first thread to read it, its entities and objects by handle among
them, so that several threads may copy from it at once, its dynamic blocks and all. The walks down nested blocks keep no limit of depth, but exploding and the reader's placing stop at 64
deep, as they did.

### External references

`db.AttachXref("column.dwg", "COLUMN")` attaches a drawing as an external reference: a block whose BLOCK is flagged 4 (12
for `OverlayXref`) and names the path in its group 1, with no entities, which AutoCAD fills when it opens the drawing; an
INSERT of the block places it. `XrefStatus`, of the new [`DxfXrefStatus`](xref:GeometryHelper.Dxf.DxfXrefStatus), says where one stands, from its flags, its INSERTs
and its path: `Unreferenced` while no INSERT places it, `FileNotFound` where the path names no file, as written or from the
folder of the drawing's `Filename`, `Resolved` where AutoCAD saved it resolved (70 of 36), else `Unresolved`; no DXF says one
is unloaded. `db.DetachXref(block)` erases its INSERTs, the records it brought, those named `COLUMN|...` in the layer,
linetype, text style, dimension style and block tables, and its block. `block.PathName = @"..\xref\column.dxf"` gives
it a new path, written in its BLOCK's group 1 as AutoCAD's -XREF Path writes it; a block that is no external reference
refuses one. The library reads no drawing referenced; the presenter does, and shows it
([Editing in the presenter](../presenter/editing.md)). AutoCAD 2021 reads only a DWG as an external reference: a path to a
DXF it keeps, saying "Unable to load".

### Linetypes from a file

`db.LoadLineTypeFile("GAS_LINE", "acad.lin")` adds a linetype of a file of linetypes as AutoCAD loads one: its description and
dashes, a dash's text in the text style the file names, and a shape found by its name in the compiled file of shapes beside
the .lin (`ltypeshp.shx`), drawn in the drawing's style of that file, one with no name (70 of 1) added where it has none, as
AutoCAD adds it. AutoCAD's gas line, `A,.5,-.2,["GAS",STANDARD,S=.1,U=0.0,X=-0.1,Y=-.05],-.25`, comes out three dashes 0.95
long, the text written `49 -0.2, 74 2, 75 0, 340` Standard's handle `, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS`. A name of
wildcards, `*` or `DASH?`, loads each linetype that matches and passes over those the drawing has or cannot take; a name of
none is loaded or refused, a linetype of its name already there as AutoCAD's eDuplicateRecordName. A drawing of R12 takes the
simple linetypes alone. `db.LoadMlineStyleFile("STANDARD", "acad.mln")` adds a multiline style of a .mln to the dictionary of
multiline styles, made where the drawing has none, each of its elements drawn with a linetype the drawing has. A style of
that dictionary is a [`DxfMlineStyle`](xref:GeometryHelper.Dxf.DxfMlineStyle), read as AutoCAD's `MlineStyle`: its `Elements` (each a [`DxfMlineStyleElement`](xref:GeometryHelper.Dxf.DxfMlineStyleElement) of
`Offset`, `Color` and `Linetype`), `Filled`, `FillColor`, the caps and the angles of its ends.

### Tidying up

`db.CountEmptyObjects((int)DxfEraseFlags.ZeroLengthCurve)` and `EraseEmptyObjects` count and erase the empty objects of every
space and block by AutoCAD's flags: the curves of no length and the texts of no words, a TEXT of spaces and an MTEXT of
formatting alone. A curve is of no length where no coordinate of its points is apart by more than 4 eps times the larger
magnitude of their coordinates (eps the machine epsilon, 2.2E-16, so 4 to 8 units of the last place), not to a tolerance: a
block may be drawn at any scale, and a line from (1000, 0, 0) to (1000.0008, 0, 0) in a block an INSERT draws at a scale of
10 000 is 8 long there, so it is kept, while one whose ends are 1.1E-13 apart at x 1000, one unit of the last place, is
empty. A LINE, a CIRCLE, an ARC or a polyline with a thickness is never empty, as it draws a segment of that height: a LINE
from (5, 5, 0) to (5, 5, 0) of thickness 100 is kept. `db.CountHardReferences(objects, counts)` adds to each count, in one
walk, the hard pointers that name the object (340 to 349, 360 to 369, 390 to 399, 480 and 481), the handles of extended data
of any application (1005, as a dimension's overrides name its arrow blocks and text style), the layers a viewport freezes
(331), and the names a DXF writes where AutoCAD's database holds a hard pointer: the layer and linetype of each entity, the
style of a text, of a cell of a table and of a table style, the dimension style of a dimension, the block of an INSERT, the
arrows of a dimension style (its 5, 6 and 7, in any version) and of a dimension's override in a drawing of R12 (`1070 5`
then `1000` the block's name), a layer's linetype, a layer a viewport of a drawing before R2000 freezes by name, and the
header's `$CLAYER`, `$CELTYPE`, `$TEXTSTYLE`, `$DIMSTYLE` and their kin; an entity counts its block besides.
`db.Purge(objects)` gives back those of them nothing refers to and AutoCAD does not keep itself: layer 0 and Defpoints, the
linetypes ByBlock, ByLayer and Continuous, the Standard styles (text, dimension, multiline, multileader and table), every text
style of a file of shapes, the materials Global, ByLayer and ByBlock, the plot style Normal, every visual style, the
DICTIONARYVAR variables and those of images and of wipeouts, the blocks of the layouts, the external references and what they
brought. A layer, a linetype or a block any pointer names, a soft one too, is kept, as its `Erase` refuses it: of the layers
0, steel with a line on it, one a viewport freezes and an empty one, the empty one. A layer, a linetype, a dimension style, a
block, an XRECORD and a GROUP given back are erased by their own `Erase`, which refuses none of them; a text style, an
MLINESTYLE, a MATERIAL, a DICTIONARYVAR and the other objects of the OBJECTS section are not erased by the library yet, and
their `Erase` throws `NotSupportedException`. A linetype only a layer of the list draws with is kept until that layer is
erased and the purge run again, as AutoCAD's is.

## Layouts and viewports

### Layouts

The layouts are `db.Layouts`, [`DxfLayout`](xref:GeometryHelper.Dxf.DxfLayout) objects in the order of their tabs: `LayoutName` (set, the layout dictionary's
entry renamed with it; Model keeps its name), `TabOrder`, `TabSelected` (the layout the drawing opens on), `Limits`,
`Extents` (empty for a layout never worked out, as ezdxf's 1E+20 above -1E+20 says), `BlockTableRecord` and
`GetViewports()`, with the plot settings read as AutoCAD's `PlotSettings` has them: a layout of A3 on its side has
`PlotPaperSize` (420, 297) in millimetres. A viewport is a [`DxfViewport`](xref:GeometryHelper.Dxf.DxfViewport): one 200 by 150 about (150, 100) on the sheet,
showing model space about (5 000, 3 000) with a `ViewHeight` of 7 500, has `CustomScale` 0.02 and `StandardScale`
`Scale1To50`; its view, snap, grid, user coordinate system, plot style, shading and lighting are read and set, and its layers
frozen and thawed by `FreezeLayersInViewport` and `ThawLayersInViewport` (331, by handle). A viewport of a file before R2000
keeps its view in its extended data MVIEW, read and set there, its twist and its snap angle in radians as AutoCAD 2021
reads and writes them there, a snap of 30 degrees as 0.5235987755982988 (degrees from R2000); what those files have not, as the user coordinate system,
reads as AutoCAD's default and is refused when set. A viewport made in code goes to a layout of paper space, given the next
`Number` of the layout; `On = true` turns it on. `Number` is the id the file writes (69) for a viewport on, and -1 for one
turned off, as AutoCAD 2021 gives it once the layout is shown (it gives -1 for every viewport of a layout not shown, which a
file does not say); the id of one turned off is kept, and not given to a new one. The viewport 1 of a layout of paper space, which shows the sheet itself, is
not erased, as AutoCAD keeps it; erasing another takes the overrides the layers keep for it out of their extension
dictionaries, and a layout whose last active viewport (331) it was names its viewport 1 instead, as AutoCAD 2021 does.

### Making a layout

A layout is made as AutoCAD makes one, `var plan = new DxfLayout { LayoutName = "Plan 1-50" }`, then
`plan.AddToLayoutDictionary(db, null)`: a LAYOUT in the layout dictionary on the tab after the last, with the plot
settings of a sheet of A3 on its side and no plotter (`none_device`, `ISO_A3_(420.00_x_297.00_MM)`, margins 7.5 at the
sides and 20 above and below, 1:1), its limits the sheet about its plotted area, (-7.5, -20) to (412.5, 277), and the block
`*Paper_Space1`, the first of `*Paper_Space0`, `*Paper_Space1` and on the drawing has not, whose BLOCK_RECORD names the
layout; a block of that name no layout has is taken where it is given. `plan.Initialize()` gives it the viewport 1 that
shows its sheet, about the middle of its limits, (202.5, 128.5), 1.1 times their size, 462 by 326.7, at 1:1, as ezdxf
makes a layout's main viewport, ahead of the viewports the layout has, those stacked on (68 of 1 or more) moved up one and
one that is off left so, AutoCAD and BricsCAD
taking the first VIEWPORT for the sheet; a layout that has its viewport 1 is given that one. `db.GetViewports(true)` gives the
viewports of the layout the drawing shows where it shows paper space (`TileMode` false), and none in model space, whose
tiled viewports are records of the VPORT table. `PlotWireframe` and `PlotAsRaster` say how a viewport is plotted, by its
`ShadePlot`, the visual style it names (333) or is shown in (348) and its render mode (281): one shaded wireframe or shown
in 2D is plotted as wires, one rendered or shown Realistic as a raster image, one shaded hidden as neither. A viewport of
R2007 or later is lit by a sun, `viewport.SetSun(new DxfSun { DateTime = new DateTime(2026, 6, 21, 15, 0, 0) })`: a
SUN owned by the viewport (361), the one it had erased where the viewport owned it, the class SUN listed where the
drawing lists none, `viewport.Sun` read back, its `IsOn`, `SunColor`, `Intensity`, `DateTime` (the Julian day 2461213
and 54 000 seconds past midnight), `IsDaylightSavingsOn`, `ShadowsOn`, `ShadowType`, `ShadowMapSize` and
`ShadowMapSoftness` read and set, a new sun's ezdxf's (AutoCAD's own not confirmed). Where the sun stands in the sky is AutoCAD's to work out from the
drawing's location, and a viewport's `ToneOperatorParameters` and `Transparent` are not there: neither the DXF reference
nor ezdxf gives where a file keeps them.

### The sheet of a layout

The sheet a layout is plotted on is set as AutoCAD's `PlotSettingsValidator` sets it, on a layout of a drawing or one made
in code once it is added. `CanonicalMediaName` takes a name that ends in the sheet's size in millimetres, width then
height, and sets `PlotPaperSize` from it: the ISO sheets on their sides, `ISO_A0_(1189.00_x_841.00_MM)`,
`ISO_A1_(841.00_x_594.00_MM)`, `ISO_A2_(594.00_x_420.00_MM)`, `ISO_A3_(420.00_x_297.00_MM)` and
`ISO_A4_(297.00_x_210.00_MM)`, the same upright with the sizes the other way round, and a size of one's own as ezdxf's
`page_setup` names one, `ezdxf_(1000.00_x_500.00_MM)`; a name without its size, as `A2`, or in inches, is refused, since
the size of a plotter's sheet is in its configuration, which a DXF does not hold. `PlotPaperMargins` (left, bottom, right,
top) and `PlotOrigin` are set in millimetres whatever `PlotPaperUnits` says, `PlotPaperUnits` and `PlotRotation` as they
read. Each makes the limits of a layout of paper space the sheet about its plotted area again, as ezdxf's `page_setup`
does: `plan.CanonicalMediaName = "ISO_A2_(594.00_x_420.00_MM)"` with margins of 10 all round gives limits (-10, -10) to
(584, 410), the paper the viewer draws 594 by 420; turned a quarter, an upright sheet lies across, its left and bottom
margins changing places; in inches the limits are the millimetres divided by 25.4. Model keeps its limits. A sheet is best
set before `Initialize`, which sizes the viewport 1 by the limits; a viewport 1 made already keeps its size, as AutoCAD
draws it anew when it next shows the layout. A layout of a file reads as it did, and a value set to what it has writes
nothing.

What is plotted and how is set as AutoCAD's validator sets it too: `PlotConfigurationName`, `CurrentStyleSheet`,
`PlotType` with `PlotWindowArea`, `CustomPrintScale`, `StdScaleType` (writing group 147 with it) and `UseStandardScale`,
and the flags `PlotCentered`, `PlotPlotStyles`, `ScaleLineweights` and `PrintLineweights`; a page setup of A3 at 1:20 with
monochrome.ctb reads back as written.

## See also

- [Entities and curves](entities.md): what every entity has.
- [Blocks and attributes](blocks.md): INSERTs of the blocks.
- [Editing rules and what the model does not do](rules.md).
