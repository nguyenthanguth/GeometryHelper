# Editing a drawing, step by step

One drawing is opened, edited and saved here with the editing model of [Shared types](common.md#editing-a-dxf-file), the
way a plugin of AutoCAD would edit it, and two of its entities are copied into a second drawing. Every snippet was built and run
against the library on .NET 8, one after the other as a single program, and every number in the text is what it printed.

The drawing, plan.dxf, is of R2018 (AC1032) in millimetres. On layer Steel is a plate 300 by 200, a closed polyline from
(0, 0) to (300, 200), with a line across its middle; on layer Bolts two INSERTs of the block BOLT, a circle of radius 8 with
two lines across it and the attribute definition MARK at (10, 10), placed at (50, 50) and (250, 150) and marked B-1 and B-2;
on layer Dims a rotated dimension of the plate's bottom edge, 30 below it, in the dimension style GH_25, whose text is 2.5
high; and on layer Notes a note of 146 characters, an MTEXT 120 wide with letters 5 high at (0, 280). The second drawing,
sheet.dxf, is of R2018 too, with nothing in it. Both were made by ezdxf 1.4.5.

## Open the drawing

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

DxfDatabase plan = DxfDatabase.Open("plan.dxf");
Console.WriteLine(plan.Version + ", " + plan.Insunits + ", " + plan.CodePage);
```

It prints `AC1032, Millimeters, UTF-8`. Nothing is read beyond what is asked for: saved at once, the drawing is the same 73 051
bytes it was read from.

## List the layers

```csharp
foreach (DxfLayerTableRecord layer in plan.LayerTable)
{
    int count = plan.ModelSpace.Count(e => string.Equals(e.Layer, layer.Name, StringComparison.OrdinalIgnoreCase));
    Console.WriteLine($"{layer.Name,-10} colour {layer.ColorIndex,3}  {count} entities");
}
```

The layers come in the order of the layer table, each a `DxfLayerTableRecord`, and model space is walked as its entities:

```text
0          colour   7  0 entities
Defpoints  colour   7  0 entities
Steel      colour   1  2 entities
Bolts      colour   3  2 entities
Dims       colour   2  1 entities
Notes      colour   7  1 entities
```

A layer's name is compared in either case, as AutoCAD compares it; `plan.LayerTable["steel"]` is the layer Steel.

## Move every entity on a layer

```csharp
GeoTransform3 shift = GeoTransform3.Translation(new GeoVector3(10, 5, 0));

foreach (DxfEntity entity in plan.ModelSpace)
{
    if (string.Equals(entity.Layer, "Bolts", StringComparison.OrdinalIgnoreCase))
    {
        entity.TransformBy(shift);
    }
}

DxfBlockReference first = plan.ModelSpace.OfType<DxfBlockReference>().First();
DxfAttributeReference mark = first.AttributeCollection[0];
Console.WriteLine(first.Position + "  " + mark.Tag + " = " + mark.TextString + " at " + mark.Position);
```

The first bolt stands at (60, 55, 0) and its attribute MARK, B-1, at (70, 65, 0): an INSERT carries its attributes with it.
There is no transaction to open and none to commit: each `TransformBy` writes that INSERT and its ATTRIB anew, and every other
record keeps its bytes. Walking model space while changing it is safe; the walk goes over the entities it held when it began.

## Change the dimension text through its style

```csharp
DxfDimStyleTableRecord style = plan.DimStyleTable["GH_25"];
style.Dimtxt = 3.5;

DxfRotatedDimension dim = plan.ModelSpace.OfType<DxfRotatedDimension>().First();
Console.WriteLine(dim.Measurement + "  " + dim.DimensionStyleName + "  " + dim.Dimtxt + "  " + dim.DimBlock.Name);
```

It prints `300  GH_25  3.5  *D1`. The dimension has no override of its own, so it reads its text height from its style,
3.5 now, and measures the 300 of the plate's edge from its points. What draws it is the block `*D1` ezdxf drew for it, 10
entities with its MTEXT 2.5 high: the library does not draw that block anew, so AutoCAD shows the old picture until it draws
the dimension again (see [Checking against AutoCAD](dxf-autocad-checks.md)).

## Set the note in columns

```csharp
DxfMText note = plan.ModelSpace.OfType<DxfMText>().First();
note.SetStaticColumns(50, 10, 2);
Console.WriteLine(note.ColumnType + ", " + note.ColumnCount + " columns " + note.ColumnWidth + " wide, gutter " + note.ColumnGutterWidth + ", Width " + note.Width);
```

It prints `StaticColumns, 2 columns 50 wide, gutter 10, Width 50`. The columns are written in the MTEXT's embedded object,
as a drawing of R2018 holds them, the two taking 110 across, and the note's `Width` (group 41) is made the width of a
column, 50 where it was 120, as AutoCAD and ezdxf make it.

## Hatch the plate round its bolt holes

```csharp
DxfPolyline plate = plan.ModelSpace.OfType<DxfPolyline>().First(p => p.Closed);
var corners = Enumerable.Range(0, plate.NumberOfVertices).Select(plate.GetPoint3dAt);
var up = new GeoVector3(0, 0, 1);

var hatch = new DxfHatch { Layer = "Steel" };
plan.ModelSpace.AppendEntity(hatch);
hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "SOLID");
hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.External, new GeoPolygonArc3(corners)));

foreach (DxfBlockReference bolt in plan.ModelSpace.OfType<DxfBlockReference>())
{
    var hole = new GeoCircle3(bolt.Position, up, 8);
    hatch.AppendLoop(new DxfHatchLoop(DxfHatchLoopTypes.Default, new object[] { hole }));
}

Console.WriteLine(hatch.NumberOfLoops + " loops, " + hatch.Area.ToString("F2") + " filled");
```

It prints `3 loops, 59597.88 filled`: the plate's 60 000 less two holes of radius 8, 201.06 each, about the bolts where they
stand now. The loops are the library's shapes in the world, the outline a `GeoPolygonArc3` and each hole a `GeoCircle3`, and
the islands are taken off as the hatch's style, `Normal`, says. The hatch is appended before its loops are given, as
AutoCAD's own samples do it; the library takes them either way. A solid fill needs no pattern lines.

## Copy a bolt and the dimension into another drawing

```csharp
DxfDatabase sheet = DxfDatabase.Open("sheet.dxf");
IReadOnlyList<DxfEntity> copies = plan.WblockCloneObjects(new DxfEntity[] { first, dim }, sheet.ModelSpace);
var copy = (DxfBlockReference)copies[0];

Console.WriteLine(copy.Name + " " + copy.AttributeCollection[0].TextString + "; BOLT " + sheet.BlockTable["BOLT"].Count
    + " entities, GH_25 " + sheet.DimStyleTable.Has("GH_25") + " (Dimtxt " + sheet.DimStyleTable["GH_25"].Dimtxt + ")"
    + ", layers " + string.Join(", ", sheet.LayerTable.Select(l => l.Name)));
```

It prints `BOLT B-1; BOLT 4 entities, GH_25 True (Dimtxt 3.5), layers 0, Defpoints, Bolts, Dims`. `WblockCloneObjects`
copies the two together, as AutoCAD's does, and gives the copies in the order given. The copy of the INSERT keeps its
attribute, and each copy brings what it names that the sheet had not: the INSERT its block BOLT, a circle, two lines and the
definition MARK, and its layer Bolts; the dimension its layer Dims, its style GH_25 as it was edited above, and the block
that draws it, `*D1` in the sheet as well, since the sheet had no block of that name. `Clone()` and `AppendEntity` of each
would have brought the same; copied together, what one of them points to among the others points to its copy.

## Find an object by its handle, and explode one

```csharp
DxfDBObject found = sheet.GetObject(copy.Handle);
Console.WriteLine(copy.Handle + " is " + found.GetType().Name + ", the same object: " + ReferenceEquals(found, copy));

DxfRotatedDimension dimCopy = sheet.ModelSpace.OfType<DxfRotatedDimension>().First();
IReadOnlyList<DxfEntity> parts = dimCopy.Explode();
Console.WriteLine("the dimension explodes into " + string.Join(", ", parts.GroupBy(p => p.GetType().Name).Select(g => g.Count() + " " + g.Key)));
```

`dimCopy` is the copy of the dimension in the sheet. It prints:

```text
3A is DxfBlockReference, the same object: True
the dimension explodes into 4 DxfLine, 2 DxfBlockReference, 1 DxfMText, 3 DxfDBPoint
```

A handle gives back the very object, as AutoCAD's `GetObjectId` gives its id, and a dimension explodes into the 10 entities
of the picture `*D1` ezdxf drew for it: its 4 lines, its two arrows (INSERTs of their blocks), its text and its three points
of definition, in no file until they are appended. The picture is the one drawn last: the text in it is still 2.5 high, the
style's 3.5 not drawn into it.

## Save

```csharp
plan.Save("plan-edited.dxf");
sheet.Save("sheet-edited.dxf");
```

Opened again, plan-edited.dxf holds the hatch of 59 597.88, the style GH_25 with its text 3.5 high and the note in 2 columns;
`DxfReader` reads sheet-edited.dxf as the 3 shapes of the bolt placed, its circle and two lines, and counts the ATTDEF, the
ATTRIB and the DIMENSION it does not read among `Skipped`. ezdxf 1.4.5 recovers and audits both files with no error and nothing
to fix.
