# Dynamic blocks: lookups, tables and constraints

The parameters that set several properties at once: a lookup sets its inputs by a row's label, a block properties table
sets parameters and attributes by a row, and a constraint parameter is set by solving the block's constraints. The
examples are added to the drawing of the first example of
[Dynamic blocks](dynamic-blocks.md#making-one-a-linear-parameter-and-its-stretch).

## Lookup parameters

A lookup parameter's table has input columns, each the distance of a linear parameter or the x or the y of a point
parameter (`DxfBlockLookupInput.Distance`, `X`, `Y`), and rows of a label and a value for each column
([`DxfBlockLookupRow`](xref:GeometryHelper.Dxf.DxfBlockLookupRow)), as the panel SIZED above has. AutoCAD 2021 read every INSERT of such a block as dynamic and set
Size to Small to give 100 by 50, to Large 200 by 80, and Width to 150 to read Custom, as the library sets them (check
v21b); written with the edges between the action and its parameters flagged nought, as the library wrote them before, it
read none of the INSERTs as dynamic (check v21). Its property is set by a row's label, letters of either case the same,
and sets the inputs to the row's values; set any input, it reads the label of the first row of the values then, or Custom
where no row holds them. A label reads back as the table holds it, a space after it kept: AutoCAD's lookup sample labels a
row "Size 6 ", and AutoCAD read it so after the library set the sample's INSERT to it (check v21). An INSERT of the block
itself, of no saved label, reads the first row of the block's own values, else Custom, as AutoCAD 2021 read Small for a
panel's own Width 100 and Height 50 (check v21b). The parameter and its action are written
as AutoCAD saved those of ACadSharp's lookup sample, each pair of edges between the action and a parameter naming each
other and flagged 4 as the sample flags them. Values of the table are in the block's units. A column may also give the
distance along x or along y of an XY parameter (`DxfBlockLookupInput.X(xy)`, `Y(xy)`, AutoCAD's UpdatedDistanceX and
UpdatedDistanceY, as AutoCAD's Civil sample writes the first), the distance or the angle of a polar parameter
(`Distance(polar)`, `Angle(polar)`) or the angle of a rotation parameter (`Angle(rotation)`), an angle in radians, its column
of the units 1 (96) where a distance's is of 2: AutoCAD 2021 read and set such tables as the library does (check D25b, 0 lines
apart), a plate's Size Large giving 200 by 80, and 400 by 160 on an INSERT scaled by 2, a pointer's Pick B a Length of 150 at a
quarter turn, a needle's Way North a quarter turn, Angle pi then reading West, and an Angle of 1 Custom. An angle is taken
into a turn and matched within the vector tolerance, not round the circle: a row of 2 pi sets nought and reads Custom, as
AutoCAD read it. A lookup set reads, as every other, the label of the first row of the values then: set to Twin, a row of
the values of Small above it, it reads Small, as AutoCAD 2021 read it (check D25b).

## Block properties tables

A block properties table (`AddBlockPropertiesTable`, AutoCAD's Block Properties Table) sets several properties and
attributes at once: its columns are properties of the block's parameters, as a lookup's inputs are
(`DxfBlockPropertiesTableColumn.Distance`, `X`, `Y`, [`Angle`](xref:GeometryHelper.Angle)), or attribute definitions of the block
(`DxfBlockPropertiesTableColumn.Attribute`), and each row ([`DxfBlockPropertiesTableRow`](xref:GeometryHelper.Dxf.DxfBlockPropertiesTableRow)) gives a number to each parameter's
column, in the block's units, and a text to each attribute's. Every INSERT shows the property Block Table1, a `short`: the
index of the first row whose parameters' values it shows, -1 where none does; set to a row's index, it sets each parameter
to the row's value and the INSERT's attribute of each column's tag to its text. The attributes are not matched: an INSERT
whose MARK is another row's text still reads the row of its distances. AutoCAD 2021 read the library's tables with no
audit error through its own `BlockPropertiesTable`, and read and set them as the library does (check D26, 0 lines apart):
the plate below set to the row 1 is 200 by 80 and its MARK reads P200, and twice that on an INSERT scaled by 2; Width set
to 120 then reads -1; a row of -1, or the one after the last, it refused (eInvalidInput), as the library refuses it with
`ArgumentOutOfRangeException`. The block's `BlockPropertiesTable` reads a table back: its name, its columns' names and its
rows. AutoCAD's own tables are read too: their user parameters, a text or a real a column sets (Spaces of AutoCAD's Parking
sample, read and set as AutoCAD 2021 set it, a Width set off the rows giving Spaces its value of no match, Custom), and the
horizontal, vertical, aligned and radial constraint parameters most of them are of, whose distances are read, and set as
the next paragraph says. AutoCAD 2021 read the same row, user parameters and constraint distances as the library on every
INSERT of its thirteen sample drawings. A row that would change a constraint parameter is refused, as AutoCAD 2021 left the
constraints of its Hex Head Bolt as they were when a row set DIA from 10 to 6, though it solved them at DIA 6 set alone
(check D27).

## Constraint parameters

A horizontal, a vertical or a radial constraint parameter is set by solving the block's constraints, which AutoCAD keeps in
the associative network of its BLOCK_RECORD (`ACAD_ASSOCNETWORK`), in drawings of R2010 and of R2018 alike: the distance
is taken to its value set and to twelve significant digits, as AutoCAD 2021 solved Height 612.3456789012345 of its
Fluorescent sample at 612.345678901 (check D27b), given to the variable of the dimensional constraint the parameter drives,
the variables of expressions of it worked out (numbers, the names of variables, + - * / % ^, brackets, the constants pi
and e, and the functions of AutoCAD's help but random: sin, cos, tan, asin, acos and atan in degrees, as AutoCAD read
`width*cos(60)` as half the width; sinh, cosh, tanh, asinh, acosh, atanh, sqrt, exp, exp10, ln, log, d2r, r2d, abs, sign,
round, trunc, floor and ceil; and min, max and pow of two values apart by a semicolon or a comma, as AutoCAD 2021 worked
them out through its own variables, check D34: round takes a half up, round(-2.5) to -2 and round(2.5) to 3; a remainder
`7%3` is of whole values only, 1, and of the sign of the first, `-7%3` -1; max(2.5;3) is the whole 3 and max(2.0;2) the
real 2; a variable named pi reads as pi; and what AutoCAD would not work out, as ln(-1), 1/0, 7.5%2, round(3000000000.5)
or random, which it draws anew each time, is refused), and
the lines, arcs, circles, polyline edges and construction lines the constraints hold moved from the block's own by Newton's
method, each step the least that meets them, before any action works on what they give. The constraints read are those of
AutoCAD's samples: coincident points, a point on a curve, parallel, perpendicular, horizontal (to a datum line), colinear,
tangent, a middle, a centre, fixed, equal lengths and radii, a distance along x or y, an angle of two lines or of three
points and a radius. Geometry the constraints leave free keeps the base point of each parameter set where it is along its
distance, as AutoCAD kept the left side of its Door Elevation, fixed nowhere, at a new Width where the least change moves
both sides by half; what is still free is held where the block has it, the radii and the ends of arcs before the rest
where all at once cannot be met, as AutoCAD kept the fillets of its Hex Head Bolt whole at a new HdCr where the least
change shrinks them to nothing. AutoCAD 2021 solved its samples so once `AssocManager.EvaluateTopLevelNetwork` ran, and of
the 79 values the library set on its Fluorescent, Door Elevation, Window, Channel, Concrete Pipe, Hex Head Bolt, WF Beam
and I Beam samples, metric and imperial, 77 came out within 3E-12 of AutoCAD's, one of an I Beam whose own geometry is
7E-6 off its constraints 1.5E-6 off, and one, Fluorescent's Width 800 after Height 300, apart, as AutoCAD took its table's
row back (check D27b). A table whose properties must match a row leaves a constraint parameter at the value set and the
others at theirs, no row matched, as AutoCAD set Fluorescent's Width 1 200, and reads the parameters of its columns as read
only, as AutoCAD read Fluorescent's Width and Height, though they are set; a constraint parameter of no column leaves the
table's user parameters as they are, as AutoCAD kept the bolt's Size 1 at Length 80; a flip carrying a constraint
parameter, as the Window sample's carries DoorWidth, mirrors its points with the rest. Setting one takes from 5 to 100
milliseconds on AutoCAD's samples, 81 for DIA of the imperial bolt, whose 130 unknowns took 800 before check D34. The
INSERT's anonymous block is given a copy of the block's network at the values set, as AutoCAD writes its own: each object
copied with a handle of its own, the copies of the entities named, the variables, the geometry and the dimensions of the
constraints at the values set, and the copy listed by the drawing's network. AutoCAD 2021 read every INSERT the library
set as the library drew it, with no audit error of its own, and set it again from the copy as from its own, 15 values of 7
samples within 1E-12 of AutoCAD's own sets one after the other (check D27b); before, it set none again until the block
was reset (`RESETBLOCK`). A block of a network and no constraint parameter is copied so too: AutoCAD 2021 set the library's
Parking sample, set to a Width, a Length, a row or a number of Spaces, again to another Width or Length just as it set its
own one after the other, in R2010 and R2018 alike, 10 sets 0 apart (check D34).

## Rows, states and matching

A row is read within 1E-7: AutoCAD 2021 read the row of 200 at 200.00000001 and none at 200.0000002 (check D26b). A column
of the visibility state (`DxfBlockPropertiesTableColumn.Visibility(block)`, the connection VisibilityState as AutoCAD's own
`AddItem` wrote it) gives each row the name of a state, which the row shows and the row is matched by; an attribute
definition's cell may be empty (null), which gives the attribute an empty text, as AutoCAD 2021 gave MARK, its audit finding
no fault; an empty cell of a parameter, which AutoCAD's audit refused, and two rows the same, which it refused too and whose
property it took away once a row was set, are refused. A table whose properties must match a row (`MustMatch`, as AutoCAD's
Block Editor sets it) takes a distance or an angle of a column set to the first row that holds it beside the first row's
values in the other columns; else, where the column holds no value twice, to the row that holds it; else the value stands
beside the first row's values and no row matches. Of the rows 100 x 50, 200 x 80 and 150 x 60, Width 200 takes the row 1
and Width 120 stands beside Height 50; of the rows 100 x 50, 200 x 80, 200 x 50 and 150 x 60, Width 200 takes 200 x 50 and
Height 60 stands beside Width 100, as AutoCAD 2021 set them (check D26b); the attributes are left as they are. A dimension
that names no block, as those of the constraints of AutoCAD's Parking sample, is copied into the anonymous block hidden, as
AutoCAD keeps them, so that Parking as AutoCAD ships it is set.

```csharp
{
    // Block properties table: Width, Height and the attribute MARK in rows; the row 1 gives 200 by 80 and MARK P200.
    var plate = new DxfBlockTableRecord { Name = "PLATE", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(plate);
    var plateBottom = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
    var plateRight = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
    var plateTop = new DxfLine(new GeoPoint3(100, 50, 0), new GeoPoint3(0, 50, 0));
    var plateLeft = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0));
    var mark = new DxfAttributeDefinition(new GeoPoint3(10, 10, 0), "P100", "MARK", "Mark", null) { Height = 5 };
    foreach (DxfEntity platePart in new DxfEntity[] { plateBottom, plateRight, plateTop, plateLeft, mark })
    {
        plate.AppendEntity(platePart);
    }

    DxfBlockLinearParameter plateWidth = plate.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -10, null);
    plate.AddStretchAction(plateWidth, new GeoPoint2(90, -5), new GeoPoint2(110, 55), new DxfEntity[] { plateBottom, plateRight, plateTop });
    DxfBlockLinearParameter plateHeight = plate.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 10, null);
    plate.AddStretchAction(plateHeight, new GeoPoint2(-5, 40), new GeoPoint2(105, 55), new DxfEntity[] { plateRight, plateTop, plateLeft });
    plate.AddBlockPropertiesTable(new GeoPoint3(0, -20, 0),
        new[] { DxfBlockPropertiesTableColumn.Distance(plateWidth), DxfBlockPropertiesTableColumn.Distance(plateHeight), DxfBlockPropertiesTableColumn.Attribute(mark) },
        new[] { new DxfBlockPropertiesTableRow(100.0, 50.0, "P100"), new DxfBlockPropertiesTableRow(200.0, 80.0, "P200") });

    var plateInsert = new DxfBlockReference(new GeoPoint3(0, -300, 0), plate);
    db.ModelSpace.AppendEntity(plateInsert);
    plateInsert.AttributeCollection.AppendAttribute(new DxfAttributeReference(new GeoPoint3(10, 10, 0), "P100", "MARK", null) { Height = 5 });
    plateInsert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Block Table1").Value = (short)1;
    Console.WriteLine(plate.BlockPropertiesTable.Rows.Count + " rows; MARK reads "
        + plateInsert.AttributeCollection.Cast<DxfAttributeReference>().First().TextString);   // 2 rows; MARK reads P200
    db.Save("panel.dxf");
}
```

## See also

- [Dynamic blocks](dynamic-blocks.md): reading, making and setting, visibility states.
- [Parameters and actions](dynamic-actions.md).
- [What the model does not do](rules.md#what-the-model-does-not-do).
