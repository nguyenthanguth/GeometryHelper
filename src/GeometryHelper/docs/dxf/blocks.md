# Blocks and attributes

An INSERT is a [`DxfBlockReference`](xref:GeometryHelper.Dxf.DxfBlockReference). The INSERT of BOLT at (50, 60, 0) turned by 45 degrees and scaled by (1.5, -1.5, 2) has
`Position` (50, 60, 0), `Rotation` 0.785 and a `BlockTransform` that takes BOLT's origin (1, 2, 0) to the insertion point;
the block's point (0, 0, 0) lands at (46.82, 61.06, 0). Setting `Position` to (60, 55, 1) moves its ATTRIB MARK from
(50, 59, 0) to (60, 54, 1): an INSERT moved, turned, scaled or given a new normal carries its attributes with it. `Name` is
the block it places, `BlockTableRecord` that block; `BlockName` is the block it is in.
`new DxfBlockReference(position, block)` places a block of the drawing.

Its attributes are [`DxfAttributeReference`](xref:GeometryHelper.Dxf.DxfAttributeReference) objects (`Tag`, `TextString`, owned by the INSERT) in its
`AttributeCollection`: `AppendAttribute` adds one, `SetAttributeFromBlock` fills one from its ATTDEF, and erasing one keeps
the others' handles. An ATTDEF is a [`DxfAttributeDefinition`](xref:GeometryHelper.Dxf.DxfAttributeDefinition) with its `Prompt`. An attribute of several lines embeds an
MTEXT from R2018: `IsMTextAttribute` makes one of its value where its text stands, or takes it out, `MTextAttribute` gives
and takes the MTEXT, its text made to agree as ezdxf's `set_mtext` makes it but for its value, the words of every line joined
by spaces as AutoCAD 2021 reads it (`LINE ONE LINE TWO LINE THREE` for three lines, where ezdxf writes the first). AutoCAD
writes an empty group 1 in an ATTRIB of several lines it makes, but reads the library's as its own, and a reader that does
not read the MTEXT needs the words there, so the ATTRIB keeps them. An ATTDEF of several lines writes the MTEXT's contents
in its group 1, its lines apart by `\P` (`LINE ONE\PLINE TWO\PLINE THREE`), as AutoCAD 2021 writes its own; made one of one
line it keeps its first line. The `TextString` of an ATTRIB or ATTDEF of several lines is the words of the lines of its
MTEXT joined by spaces, whatever its group 1 holds, as AutoCAD 2021 reads both: `LINE ONE LINE TWO LINE THREE` of the
definition above, and of an ATTRIB AutoCAD wrote with an empty group 1. Setting it sets the words of the MTEXT, which keeps
its place and look, its backslashes and braces written as codes, and its group 1 made to agree: the ATTRIB of three lines set
to `M24 GALV` is an MTEXT of one line `M24 GALV`, still an attribute of several lines. `UpdateMTextAttribute()` places the MTEXT
again where the text stands, after the attribute is moved or set from its definition. A MINSERT is a [`DxfMInsertBlock`](xref:GeometryHelper.Dxf.DxfMInsertBlock) with
its `Columns`, `Rows`, `ColumnSpacing` and `RowSpacing`.

`UnitFactor` takes the block's units to the drawing's (`$INSUNITS`): 25.4 for a block in inches inserted in millimetres, 1
where either gives none. `GeometryExtentsBestFit()` is the box of the shapes placed, and
`GeometryExtentsBestFit(parentTransform)` the same after an outer INSERT's `BlockTransform`; a MINSERT has none
(`InvalidOperationException`, as AutoCAD 2021 threw eInvalidExtents), its `GeometricExtents` its first cell's, and its
`TreatAsBlockRefForExplode` false. A MINSERT is one by its subclass AcDbMInsertBlock, of one cell too, or by its counts in
R12.

**Dynamic blocks for an AI.** A dynamic block is made in a drawing of R2010 or later (`new DxfDatabase("AC1024")`) by one
call on its [`DxfBlockTableRecord`](xref:GeometryHelper.Dxf.DxfBlockTableRecord) for each parameter and one for each action, each written as AutoCAD 2021 saved its own
sample of it, and an INSERT of it is set by
`insert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == name).Value`. The kinds, each written and set
as AutoCAD 2021 read and set it:

- a distance: `AddLinearParameter`, with `AddStretchAction`, `AddMoveAction`, `AddScaleAction` or
  `AddArrayAction(linear, entities, columnSpacing)`;
- a distance along x and one along y: `AddXYParameter`, with `AddStretchAction`, `AddMoveAction` or
  `AddArrayAction(xy, entities, columnSpacing, rowSpacing)`;
- a distance and an angle: `AddPolarParameter`, with `AddPolarStretchAction`, `AddStretchAction` or `AddMoveAction`;
- a position: `AddPointParameter`, with `AddMoveAction`;
- an angle: `AddRotationParameter`, with `AddRotateAction`;
- the values a distance or an angle may take: a [`DxfBlockParameterValueSet`](xref:GeometryHelper.Dxf.DxfBlockParameterValueSet) given to `AddLinearParameter`,
  `AddPolarParameter`, and the overloads of `AddXYParameter` and `AddRotationParameter` that take one;
- a mirror: `AddFlipParameter`, with `AddFlipAction`;
- the entities shown: `AddVisibilityParameter`, each state a [`DxfBlockVisibilityState`](xref:GeometryHelper.Dxf.DxfBlockVisibilityState);
- named sizes: `AddLookupParameter`, its inputs [`DxfBlockLookupInput`](xref:GeometryHelper.Dxf.DxfBlockLookupInput) (a linear or a polar distance, the x or the y of a
  point, the distances of an XY parameter, the angle of a polar or a rotation parameter) and its rows [`DxfBlockLookupRow`](xref:GeometryHelper.Dxf.DxfBlockLookupRow),
  the parameter and its action in one call;
- several properties and attributes set at once by rows: `AddBlockPropertiesTable`, its columns
  [`DxfBlockPropertiesTableColumn`](xref:GeometryHelper.Dxf.DxfBlockPropertiesTableColumn) (the properties a lookup input takes, and `Attribute(definition)`) and its rows
  [`DxfBlockPropertiesTableRow`](xref:GeometryHelper.Dxf.DxfBlockPropertiesTableRow), read back by the block's `BlockPropertiesTable`;
- grips for placing the block, of no property: `AddAlignmentParameter` and `AddBasePointParameter`.

Two things are refused with `NotSupportedException` before anything is written, either given second: a flip parameter
beside any other parameter, as AutoCAD 2021 set Hand to 1 on such INSERTs and read 0 back each time (checks v20b and v21);
and a rotate or a scale of an entity a stretch or a move also holds, as AutoCAD worked such an entity out from where its last
set left it, stretching a shaft to (0, 155) where the values give (50, 105) (check v21). In each check AutoCAD 2021 read the
library's files, set the values itself, inserted the block anew and kept all through a DXF and a DWG saved and opened
again: a linear parameter and its stretch written by a script in v17 (11 files of 11) and by the library in v18 (6 of 6),
new drawings of R2000 to R2018 and visibility in v19 (13 of 13), a move, a scale, stretches of arcs, circles, texts and
INSERTs, a point, a flip alone and an XY parameter in v20, a rotation in v20b, and distance multipliers and angle offsets,
arrays along a distance and in rows and columns, lookups, a turn and a scale beside a stretch, a drawing of R2018 and
AutoCAD's own XY and lookup samples set by the library in v21b (8 of 8), a polar parameter with its polar stretch and
the value sets of a rotation, an XY and a polar parameter in D25, and the ties of value sets, a stretch and a move of a
polar parameter and lookups of an XY, a polar and a rotation parameter in D25b, and block properties tables in D26. The examples below give one block of each kind as
AutoCAD took it, to be copied as they stand: the first is a whole program, and each after it a block of statements in braces
whose names are its own and whose block names no other example uses, so that any of them, or all of them in any order, may
be pasted after the first; what is not written or not set is listed under
[What the model does not do](rules.md#what-the-model-does-not-do).

A block drawn in AutoCAD's Block Editor often holds more than these calls write, and an INSERT of it is set too: a stretch
or a move tied to a parameter's base grip, a move whose selection holds other linear parameters and carries them, and
attribute definitions in a stretch, a move, a scale or a rotate, whose ATTRIBs on the INSERT follow. How each is worked out,
and what is still refused, is set out after the examples.

Constraint parameters of such a block (AutoCAD's horizontal, vertical and radial ones) are set too, by solving the block's
geometric and dimensional constraints, which the library reads from the block's associative network and copies into the
INSERT's anonymous block as AutoCAD does: `Property(insert, "Width").Value = 1200.0` on AutoCAD's Fluorescent sample, a
rectangle 1 000 by 500 fixed at its corner, moves its right side to x = 1 200 as AutoCAD 2021 did, to 3E-13 (check D27).
Geometry the constraints leave free moves as AutoCAD moved it: Width 1 000 of its Door Elevation, fixed nowhere, keeps the
door's left side, where Width's base point is (check D27b). A block of a constraint or an expression the library does not
solve is refused with `NotSupportedException`, nothing written, as the paragraph on constraint parameters after the block
properties tables below sets out.

A dynamic block is found as
ezdxf finds it, by the extended data `AcDbDynamicBlockGUID` of its BLOCK_RECORD, or `AcDbBlockRepBTag` of the anonymous
block that stands for it (`DynamicBlockTableRecord`, `AnonymousBlockTableRecord`); it is dynamic (`IsDynamicBlock`) where its
extension dictionary holds its evaluation graph (ACAD_ENHANCEDBLOCK), as AutoCAD 2021 read it: the Bore, PVI, Station and
Valve blocks of its samples keep the GUID over an empty extension dictionary and are not, nor are their INSERTs, and a
block whose GUID was taken out and its graph kept is (check D26b). Its dynamic properties
are read, not worked out: `DynamicBlockReferencePropertyCollection` gives, as AutoCAD's does, each property's
`PropertyName`, `Value`, `PropertyTypeCode`, `UnitsType`, `ReadOnly`, `Description` and `GetAllowedValues()`, its value the one
AutoCAD saved for the INSERT (the XRECORD of the parameter's node under AcDbBlockRepresentation, AppDataCache and
ACAD_ENHANCEDBLOCKDATA of its extension dictionary), or the block's own where it saved none, as for an INSERT of the
dynamic block itself. Each parameter of the block's evaluation graph gives its own, in the order of the graph: a linear
parameter its distance, a polar one its distance and its angle, an XY one its distances along x and along y, a rotation one
its angle from its base angle, a point one its position's x and y (`Anchor X`, `Anchor Y`), a flip one 0 or 1 (a `short`), a
visibility one its state, the first of its states where none is saved, every state allowed, and a lookup one the value its
table gives, read only where the table allows no reverse lookup, and then of its label of no match allowed last, as
AutoCAD 2021 gave Custom last to Ball Count and the other read-only lookups of its Roller Bearing; a lookup of a visibility
state reads the row of the state shown, as AutoCAD read Part Number SSI-4 of its Shoulder Screw (check D26b). Lengths are scaled by the largest of the INSERT's scale
factors and angles are in radians: the door whose linear parameter Width AutoCAD stretched from 900 to 1 000, inserted at a
scale of 2, gives `Width = 2000.0`, `UnitsType` `Distance`; AutoCAD 2021 read a distance of 7.5 along x as 22.5 at the
scale (2, 3, 2), and as 15 at (2, 2, 1), where `IsDynamicBlock` is false, as it read such INSERTs. A value set's list is
allowed where its flags (96, or 97 for the second set) hold 8; AutoCAD gave none of a list under the flags 1, 2 or 4. The
block's own value is one of the list where the list was written without it, after the last value below it: Door Size of
AutoCAD's Door sample, own 750, allows 600, 700, 750, 800, 900 and 1 000, a list written 300, 200, 50 of an own 100 allows
300, 200, 50, 100, and a value set is taken to it, 760 and the tie 725 to 750, as AutoCAD 2021 took them (check D26b); a
value an INSERT shows off the list is not added.
AutoCAD's hidden `Origin` points (not shown in its properties) are left out. A block properties table gives the index of
the row the INSERT matches (`Block Table1`, a `short`, -1 for none), a user parameter its text or real, and a horizontal, a
vertical, an aligned or a radial constraint parameter its distance, set by solving the block's constraints but for an
aligned one, as set out below. AutoCAD 2021 read the same names, values (to 1E-9, most to the bit),
types, units, read-only flags, descriptions and allowed values on ACadSharp's AutoCAD-saved samples.

```csharp
// Every dynamic property of every INSERT in model space of a drawing on disk.
foreach (DxfBlockReference reference in DxfDatabase.Open("panel.dxf").ModelSpace.OfType<DxfBlockReference>())
{
    foreach (DxfDynamicBlockReferenceProperty p in reference.DynamicBlockReferencePropertyCollection)
    {
        Console.WriteLine(reference.Handle + " " + p.PropertyName + " = " + p.Value + " (" + p.UnitsType + ")");
    }
}
```

A dynamic block is made in code from a block of a drawing of R2010 or later, which `new DxfDatabase("AC1024")` makes from
nothing: `AddLinearParameter` gives it a linear parameter, a distance from a base point to an end point that every INSERT of it shows as a property of the parameter's
name, with the values it may take (`DxfBlockParameterValueSet.Increments(minimum, maximum, increment)`, `List(values)`, or
null for any), and `AddStretchAction` ties the entities to it: as the distance grows by an amount, the points of the lines
and polylines inside the stretch frame (a rectangle of two corners, in the block's coordinates) move by that amount from
the base point towards the end point, so an entity across the frame stretches and one inside it moves whole. A rectangle
100 by 50 given Width from (0, 0, 0) to (100, 0, 0), from 50 to 300 by 10, the stretch of its right edge by the frame
(90, -10)-(110, 60), and an INSERT of it whose Width is set to 200:

```csharp
using System;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

var db = new DxfDatabase("AC1024");                              // a new drawing of R2010, in millimetres
var panel = new DxfBlockTableRecord { Name = "PANEL", Origin = new GeoPoint3(0, 0, 0) };
db.BlockTable.Add(panel);
var right = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
var bottom = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
var top = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
var left = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(0, 0, 0));
foreach (DxfLine line in new[] { right, bottom, top, left })
{
    panel.AppendEntity(line);
}

DxfBlockLinearParameter width = panel.AddLinearParameter("Width", "Overall width", new GeoPoint3(0, 0, 0),
    new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10));
panel.AddStretchAction(width, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { right, bottom, top });
var insert = new DxfBlockReference(new GeoPoint3(0, 0, 0), panel);
db.ModelSpace.AppendEntity(insert);
insert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;
db.Save("panel.dxf");
```

The frame holds the right edge whole and the right ends of the bottom and the top; the left edge has no point in it and is
not given (an entity given with none is refused, as are the kinds listed below that a stretch does not take). An INSERT shows
`Width = 100.0` until its property is set; AutoCAD 2021, given the same block (plugin v17), set it to 200 and moved the right edge to x = 200, took
75 to 70, 77 to 80 and 400 to 300 by the value set, and kept the values through a DXF and a DWG saved and opened again. The block is written as AutoCAD's Block
Editor saves one: the extended data AcDbDynamicBlockTrueName, AcDbDynamicBlockGUID (a new GUID) and AcDbBlockRepETag on
its BLOCK_RECORD, a tag on each of its entities, the evaluation graph and the purge preventer in its extension dictionary,
each parameter with its two grips and their location components, and the classes listed; the graph of the block above is
AutoCAD 2021's own save of it, number for number. A block may be given several parameters, each with its actions, named
Linear, Linear1 and Stretch, Stretch1 and on as AutoCAD names them; a second parameter Height from (0, 0, 0) to (0, 50, 0)
with the frame (-10, 40)-(110, 60) over the top gives each INSERT Width and then Height. Writing the parameter and the
stretch of the rectangle takes about half a millisecond, and tagging and stretching a block of 1 000 lines 15 milliseconds
under .NET Framework 4.8 and 30 under .NET 10.

Setting `Value` works the block out as AutoCAD's `DynamicBlockReferenceProperty.Value` does. The value is taken to the
value set as AutoCAD 2021 took it (plugins v17 to v20): to the nearest step of an increment, a tie to the step below, then
into the minimum and the maximum, so that 75 goes to 70, 85 to 80, 77 to 80 and 400 to 300 of the set above, and 95 to 90
though the block's own is 100; to the nearest of a list, a tie to the block's own value where it is one of the two, else to
the one written first, the lesser of a list written from the least up, whatever value the property had: of 50, 100 ...
300, 75 and 125 go to 100 and 175 to 150 for a block's own 100, and 125 to 100 and 225 to 250 for an own 250; and as given
where there is no set. AutoCAD 2021 took all 358 ties of lists of 3 to 6 values so (check D25b, 278 of linear parameters,
47 of an XY and 33 of polar ones, each approached from every value of its list, alike in two sessions), and the 20 of
checks v20 and D25 too, which the library took before by the value the property had. The INSERT is then made to place an anonymous
block, `*U1` the first the drawing has not, holding a copy of each entity of the block with the points the stretches hold
moved by the change: here the right edge at x = 200 and the right ends of the bottom and the top with it, the left edge at
x = 0. The anonymous block names the dynamic one in its extended data AcDbBlockRepBTag, each copy keeps its source's tag
with its own handle, and the INSERT's extension dictionary is given AcDbBlockRepresentation, with the representation data
naming the dynamic block and the record of each parameter and action AutoCAD reads the values from
(ACAD_ENHANCEDBLOCKDATA under AppDataCache), the end point of Width at (200, 0, 0). A history of grip edits the INSERT
keeps (ACAD_ENHANCEDBLOCKHISTORY) is kept, the value set added after it (its node's id, the property's name and the value
read back), as AutoCAD 2021 kept the history of its Parking sample's INSERT and added each value it set; an INSERT of none
is given none, as AutoCAD gave none to the library's INSERTs it set (check D26). AutoCAD 2021 read
such an INSERT, written by a script, as Width 150 with its stretched lines, and set it again (plugin v17).
`DynamicBlockReferencePropertyCollection` reads 200 back; the dynamic block itself keeps 100. Set again, the INSERT's
anonymous block is filled anew where no other INSERT places it, else the INSERT is given one of its own. A length of an
INSERT scaled by 2 is twice the block's: 300 set on it moves the right edge to x = 150 in the block. A block of the
parameters and actions this section writes is worked out: a property of a block holding another parameter or action (a
constraint, a block properties table), or an action that moves other parameters in a way
refused below, or scales or turns about another point than its parameter's base point, is refused with
`NotSupportedException`, and an INSERT scaled unlike along its axes with `InvalidOperationException`, as
AutoCAD takes it as no dynamic block's; nothing is written then. A value is a number, a whole number taken as the distance
it is, and a distance of nought or less after the value set is refused. Setting Width takes under a millisecond, and on
each of 1 000 INSERTs of a drawing half a second to a second; setting again an INSERT that places an anonymous block
already looks through the drawing for another INSERT of that block, and takes about a millisecond with 1 000 INSERTs and
five with 4 000.

`AddVisibilityParameter` gives a block a visibility parameter: named states, each a `DxfBlockVisibilityState` of a name and
the entities of the block shown in it. The entities the states name are governed by it, each shown in the states that name
it and hidden in the others; an entity named in no state is shown in every state. Every INSERT shows the first state until
its property is set, and the block is kept in that state, as AutoCAD keeps it: a governed entity not named in the first
state is made invisible. A sign of a frame, a cross and a tick, added to the drawing above, with an INSERT showing the tick
(each example after the first is a block of statements of its own, its names its own, to be pasted after the first alone
or with any of the others):

```csharp
{
    var sign = new DxfBlockTableRecord { Name = "SIGN", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(sign);
    var frame = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 0, 0));
    var cross = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(40, 40, 0));
    var tick = new DxfLine(new GeoPoint3(10, 20, 0), new GeoPoint3(20, 10, 0));
    foreach (DxfLine signLine in new[] { frame, cross, tick })
    {
        sign.AppendEntity(signLine);
    }

    sign.AddVisibilityParameter("Mark", "What the sign shows", new GeoPoint3(20, -10, 0), new[]
    {
        new DxfBlockVisibilityState("Cross", new DxfEntity[] { cross }),
        new DxfBlockVisibilityState("Tick", new DxfEntity[] { tick }),
        new DxfBlockVisibilityState("Blank", new DxfEntity[0]),
    });
    var mark = new DxfBlockReference(new GeoPoint3(200, 0, 0), sign);
    db.ModelSpace.AppendEntity(mark);
    mark.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Mark").Value = "Tick";
    db.Save("panel.dxf");
}
```

Each INSERT of SIGN gives `Mark = Cross` until set, its allowed values Cross, Tick and Blank; the one set shows the frame and
the tick, the cross hidden in its anonymous block, and reads `Mark = Tick` back. A state is set by its name, letters of
either case the same, and one the parameter has not is refused (`ArgumentException`). The parameter is written as AutoCAD
2021 saved the visibility parameter of ACadSharp's sample: the parameter at the location with the governed entities and
each state with the entities shown in it, its grip and the grip's two location components, four nodes and three edges of
the evaluation graph, number for number. A block takes one visibility parameter, as AutoCAD's Block Editor gives it one, and
may take linear parameters besides: its INSERTs keep the state and the distances together. Writing the parameter takes a
fifth of a millisecond.

The other parameters and actions of AutoCAD's Block Editor are made the same way, each written number for number as
AutoCAD 2021 saved ACadSharp's sample of it, and each set on an INSERT as AutoCAD sets it. A stretch takes, besides lines
and polylines, an arc with both its ends in the frame, a circle by its centre, a text by the point it is held by (its
position, or its alignment point where it is not left on its baseline), a multiline text by its location and an INSERT by
its insertion point; each moves whole. An arc with one end only in the frame is stretched as AutoCAD stretches it, below. A
text aligned or fitted between two points and an INSERT of rows and columns are refused. A stretch, a move, a scale and a
rotate take an attribute definition locked in its block (`LockPositionInBlock`), a stretch by its point as a text, and the
ATTRIB of its tag on each INSERT follows it, below; one not locked is refused. Each kind in a few lines, added to the
drawing above:

```csharp
{
    // Each example adds a block to the drawing above; Block makes it, Place puts an INSERT of it in model space.
    DxfBlockTableRecord Block(string name)
    {
        var block = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
        db.BlockTable.Add(block);
        return block;
    }

    DxfBlockReference Place(DxfBlockTableRecord block, double x, double y)
    {
        var placed = new DxfBlockReference(new GeoPoint3(x, y, 0), block);
        db.ModelSpace.AppendEntity(placed);
        return placed;
    }

    // Move: a tag circle follows the end point of Length; Length 200 puts its centre at (190, 25).
    DxfBlockTableRecord slide = Block("SLIDE");
    var tagCircle = new DxfCircle(new GeoPoint3(90, 25, 0), GeoVector3.ZAxis, 5);
    slide.AppendEntity(tagCircle);
    DxfBlockLinearParameter length = slide.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    slide.AddMoveAction(length, new[] { tagCircle });

    // Scale: a disc of the radius 25 at (50, 25) scaled about the origin by Size over 100; Size 200 doubles it, about (100, 50).
    DxfBlockTableRecord disc = Block("DISC");
    var round = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 25);
    disc.AppendEntity(round);
    DxfBlockLinearParameter size = disc.AddLinearParameter("Size", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    disc.AddScaleAction(size, new[] { round });

    // Point and move: Tag X and Tag Y move the tag anywhere; Tag X 120 puts its centre at (120, 20).
    DxfBlockTableRecord tagged = Block("TAGGED");
    var tag = new DxfCircle(new GeoPoint3(80, 20, 0), GeoVector3.ZAxis, 5);
    tagged.AppendEntity(tag);
    DxfBlockPointParameter where = tagged.AddPointParameter("Tag", "Where the tag stands", new GeoPoint3(80, 20, 0));
    tagged.AddMoveAction(where, new[] { tag });

    // Rotation and rotate: Angle, in radians, turns the arrow about its tail; Math.PI / 2 makes it point along y, to (50, 55).
    DxfBlockTableRecord arrowBlock = Block("ARROW");
    var arrow = new DxfLine(new GeoPoint3(50, 25, 0), new GeoPoint3(80, 25, 0));
    arrowBlock.AppendEntity(arrow);
    DxfBlockRotationParameter angle = arrowBlock.AddRotationParameter("Angle", "Turn of the arrow", new GeoPoint3(50, 25, 0), 30, 0);
    arrowBlock.AddRotateAction(angle, new[] { arrow });

    // Flip and flip: Hand 1 mirrors the leaf about the y axis, from (0, 0)-(90, 0) to (0, 0)-(-90, 0).
    DxfBlockTableRecord door = Block("DOOR");
    var leaf = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(90, 0, 0));
    door.AppendEntity(leaf);
    DxfBlockFlipParameter hand = door.AddFlipParameter("Hand", "Which way the door opens", new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0));
    door.AddFlipAction(hand, new[] { leaf });

    // XY: Width and Depth move the top right corner of the plate and the hole on it both ways; 160 by 80 takes them to (160, 80).
    DxfBlockTableRecord plate = Block("PLATE");
    var outline = new DxfPolyline();
    outline.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
    outline.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
    outline.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
    outline.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
    outline.Closed = true;
    var hole = new DxfCircle(new GeoPoint3(100, 50, 0), GeoVector3.ZAxis, 4);
    plate.AppendEntity(outline);
    plate.AppendEntity(hole);
    DxfBlockXYParameter xy = plate.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
    plate.AddStretchAction(xy, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { outline });
    plate.AddMoveAction(xy, new[] { hole });

    Place(slide, 0, 150).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 200.0;
    Place(disc, 0, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = 200.0;
    Place(tagged, 300, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Tag X").Value = 120.0;
    Place(arrowBlock, 600, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Angle").Value = Math.PI / 2;
    Place(door, 900, 300).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Hand").Value = (short)1;
    DxfBlockReference sheet = Place(plate, 1200, 300);
    sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 160.0;
    sheet.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 80.0;
    db.Save("panel.dxf");
}
```

A point parameter gives two properties, its name with " X" and " Y" after it, the position in the block's coordinates (scaled
as a length is); an XY parameter gives its distance along x and then along y, each measured the way its end point lies from
its base point; a rotation parameter its angle in radians from the block's x axis, a value set taken into a turn, from nought
up to 2 pi (-pi / 2 reads back as 3 pi / 2); a flip parameter 0 or 1, a `short`, a whole number set taken as the state. A move
moves its entities whole by the change of the point it follows: the end point of a linear or an XY parameter, the position
of a point parameter. A scale scales them alike about the base point of its linear parameter by the distance over the
block's own; a rotate turns them about the base point of its rotation parameter by the angle less the block's own; a flip
mirrors them about its reflection line while the state is 1. The actions are worked out in the order they were added, the
stretches in a row summed point by point first. A flip parameter is written alone in its block: a flip parameter in a
block of any other parameter, and any other parameter in a block of a flip, are refused with NotSupportedException.
AutoCAD 2021 flipped a door of the flip alone both ways (check v20), but set Hand to 1 on INSERTs of a door that also had a
linear parameter of no action, and of a panel whose stretch held other entities, it read 0 back each time and left the
leaf as it was (checks v20b and v21), and did again once the library kept an INSERT's history (check D30). A rotate or a
scale and a stretch or a move of one entity are refused the same way,
either given second, and an INSERT of a block of such an entity from another writer is not set: AutoCAD 2021 worked such an
entity out from where its last set left it, stretching a shaft turned a quarter turn along x to (0, 155) for a Reach of
150 set after 200, where the block's values give (50, 105) (check v21). A turn and a stretch of different entities of one
block are written and worked out. A scale or a rotate turns about its parameter's base
point as AutoCAD writes it, 1012 nought: AutoCAD 2021 adds 1012 to the base point, and turned an arrow about (100, 50) for
a base point of (50, 25) when the library wrote that point there (check v20). The flip has no value set; a distance of an XY parameter is above the point
tolerance. The block keeps its own values: an INSERT shows them until its property is set. Writing a point, a rotation, a flip
and an XY parameter with their actions takes about a millisecond, and setting any of them on an INSERT of a drawing of 1 000
INSERTs about half a millisecond under .NET 10 and one under .NET Framework 4.8. A polar parameter and its polar stretch,
and the value sets of a rotation and an XY parameter, are set out after the last kinds below.

The last kinds, each in a few lines, added to the drawing above; each is shown as AutoCAD 2021 read and set it (checks v21
and v21b):

```csharp
{
    // This example's own helpers: AddBlock makes a block of the drawing above, PlaceAt puts an INSERT of it in model space.
    DxfBlockTableRecord AddBlock(string name)
    {
        var made = new DxfBlockTableRecord { Name = name, Origin = new GeoPoint3(0, 0, 0) };
        db.BlockTable.Add(made);
        return made;
    }

    DxfBlockReference PlaceAt(DxfBlockTableRecord block, double x, double y)
    {
        var placedAt = new DxfBlockReference(new GeoPoint3(x, y, 0), block);
        db.ModelSpace.AppendEntity(placedAt);
        return placedAt;
    }

    // Stretch of an arc by one end: its start (100, 25) is in the frame, its end (90, 35) is not; Width 200 moves the start to
    // (200, 25) and the arc keeps the height of its middle off its chord, 2.929, its radius becoming 522.13.
    DxfBlockTableRecord bend = AddBlock("BEND");
    var edge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
    var swing = new DxfArc(new GeoPoint3(90, 25, 0), 10, 0, Math.PI / 2);
    bend.AppendEntity(edge);
    bend.AppendEntity(swing);
    DxfBlockLinearParameter bendWidth = bend.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    bend.AddStretchAction(bendWidth, new GeoPoint2(95, -10), new GeoPoint2(130, 60), new DxfEntity[] { edge, swing });

    // Distance multiplier: the mark moves half as far as the end point, staying in the middle; Span 200 puts it at (100, 25).
    DxfBlockTableRecord centred = AddBlock("CENTRED");
    var centredEdge = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
    var centredMark = new DxfCircle(new GeoPoint3(50, 25, 0), GeoVector3.ZAxis, 5);
    centred.AppendEntity(centredEdge);
    centred.AppendEntity(centredMark);
    DxfBlockLinearParameter span = centred.AddLinearParameter("Span", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    centred.AddStretchAction(span, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { centredEdge });
    centred.AddMoveAction(span, new[] { centredMark }, 0.5, 0);

    // Array of a linear parameter: a post every 60 along Length; AutoCAD set Length 250 and drew four, at x = 0, 60, 120 and 180.
    DxfBlockTableRecord fence = AddBlock("FENCE");
    var post = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
    fence.AppendEntity(post);
    DxfBlockLinearParameter fenceLength = fence.AddLinearParameter("Length", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    fence.AddArrayAction(fenceLength, new[] { post }, 60);

    // Array of an XY parameter: columns every 25 along x, rows every 20 along y; Width 100 and Depth 70 show 4 by 3 bolts.
    DxfBlockTableRecord bolted = AddBlock("BOLTS");
    var bolt = new DxfCircle(new GeoPoint3(0, 0, 0), GeoVector3.ZAxis, 5);
    bolted.AppendEntity(bolt);
    DxfBlockXYParameter grid = bolted.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(40, 30, 0));
    bolted.AddArrayAction(grid, new[] { bolt }, 25, 20);

    // Lookup: Size sets Width and Height by the row of its label; Large gives 200 by 80, and Width 150 then reads Custom.
    DxfBlockTableRecord sized = AddBlock("SIZED");
    var sizedTop = new DxfLine(new GeoPoint3(0, 50, 0), new GeoPoint3(100, 50, 0));
    var sizedSide = new DxfLine(new GeoPoint3(100, 0, 0), new GeoPoint3(100, 50, 0));
    sized.AppendEntity(sizedTop);
    sized.AppendEntity(sizedSide);
    DxfBlockLinearParameter w = sized.AddLinearParameter("Width", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0), -20, null);
    sized.AddStretchAction(w, new GeoPoint2(90, -10), new GeoPoint2(110, 60), new[] { sizedTop, sizedSide });
    DxfBlockLinearParameter h = sized.AddLinearParameter("Height", null, new GeoPoint3(0, 0, 0), new GeoPoint3(0, 50, 0), 20, null);
    sized.AddStretchAction(h, new GeoPoint2(-10, 40), new GeoPoint2(110, 60), new[] { sizedTop, sizedSide });
    sized.AddLookupParameter("Size", "Panel size", new GeoPoint3(50, -20, 0),
        new[] { DxfBlockLookupInput.Distance(w), DxfBlockLookupInput.Distance(h) },
        new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });

    // Alignment and base point: grips AutoCAD uses when the block is placed; they give no property.
    DxfBlockTableRecord pipe = AddBlock("PIPE");
    pipe.AppendEntity(new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0)));
    pipe.AddAlignmentParameter(new GeoPoint3(0, 0, 0), new GeoPoint3(50, 0, 0));
    pipe.AddBasePointParameter(new GeoPoint3(0, 0, 0));

    PlaceAt(bend, 0, 0).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 200.0;
    PlaceAt(centred, 0, 200).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Span").Value = 200.0;
    PlaceAt(fence, 0, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 250.0;
    DxfBlockReference boltedInsert = PlaceAt(bolted, 300, 400);
    boltedInsert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 100.0;
    boltedInsert.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 70.0;
    PlaceAt(sized, 600, 400).DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = "Large";
    PlaceAt(pipe, 900, 400);
    db.Save("panel.dxf");
}
```

An arc held by one end has that end moved and the other kept, and is drawn anew through them keeping the height of its
middle off its chord, as AutoCAD 2021 stretched one (check v20): the arc of the radius 10 from (100, 25) to (90, 35), its
start moved to (150, 25) and then to (200, 25), kept its height of 2.929 as its radius grew to 159.37 and then 522.13. AutoCAD
numbers an arc's start 0 and its end 1; an arc held by one end lies flat, its normal along z, and a tilted one is refused.
A stretch and a move take a distance multiplier and an angle offset, AutoCAD's Distance Multiplier and Angle Offset: the
change of the point they follow is multiplied by the one and turned counter-clockwise by the other, in radians, as AutoCAD
2021 read them (check v21); the overloads without them write 1 and nought. An INSERT whose value the library sets keeps the
offset in its cache as AutoCAD keeps it: AutoCAD moved along x, unturned, the circle of an offset of a quarter turn on an
INSERT whose cache held nought. A move that lists its own parameter's grip among what it moves, as AutoCAD's point sample
does and as drawings from AutoCAD often do, is worked out too, the grip following its parameter anyway.

An array copies its entities as its parameter's distance grows: along a linear parameter a copy at each column spacing
from the base point towards the end point, of an XY parameter in columns along x and rows along y the way the end point
lies; each count is the distance over its spacing, rounded down, as AutoCAD's save of ACadSharp's XY sample shows (four
columns of 25 in 100.59, five rows of 20 in 104.30) and as AutoCAD 2021 drew the blocks above (four posts at 250, one at
119; one column of three rows at 40 by 70, two bolts at 60 by 30). An XY array of a count of nought copies nothing, as
AutoCAD drew the one circle of its sample at 80 by 5. The entity itself is the first copy; the others follow the block's
entities cell by cell, row by row, each cell's in the order the array was given them, and one array's after another's, as
AutoCAD 2021 drew them (check D30): the seat and the back of a chair arrayed every 600 come as seat 0, back 0, seat 600,
back 600 and on, where the library drew every seat and then every back. The copies keep the entity's tag and are
tagged as AutoCAD tags the copies of its arrays (AcDbBlockArrayActionTag). An entity an array copies is worked on by no
other action, and a block's own distance that holds the spacing twice draws one copy still, as AutoCAD's block itself does.
The linear parameter's array is written as the XY sample's with no rows (140 nought). AutoCAD 2021 refused to set again an
INSERT whose array the library had set while its copies were not tagged (eInvalidInput, check v21); with the tags it set
each again (check v21b), a fence of a post every 60 from four posts at 250 to one at 119 and three at 222, and a plate of
bolts every 25 by 20 from 12 at 100 by 70 to six at 60 by 70. The INSERT of the XY sample of 100.59 by 104.30 keeps its
base point where a grip moved it, from (0, 5) to (0, 104.30), its end point at (100.59, 0), and AutoCAD draws its rows from
there, at y 99.30 down to 19.30. The library works a block out from the block's own base points, and would draw them at 0
down to -80: it sets no property of an INSERT that keeps the base point of a linear, an XY, a rotation or a flip parameter
away from the block's own, moved by the moves that carry the parameter (below), beyond the point tolerance of
`Tolerance.Default`, and refuses with NotSupportedException, writing nothing.

Blocks drawn in AutoCAD's Block Editor hold three things more that are set as well. A stretch or a move tied to a
parameter's base grip (BaseXDelta and BaseYDelta) moves nothing when a value is set, as a value set moves the end point
only: Depth from (40, 60) down to (40, 0) set to 100 takes the bottom corners of a frame 100 by 60 down to y -40 by the
stretch of its end point, leaves the top corners its base point's stretch holds at 60, and moves a circle at (40, 30) to
(40, 10) by a move of the multiplier 0.5 tied to the end point, the one tied to the base point leaving it. A move whose
selection holds another linear parameter carries it (AutoCAD writes the parameters' node ids, 91, after the count 70 of the
action's head, and the parameter and its grips among what it moves): the move by Depth's end point carries Bottom from
(0, 0)-(100, 0) to (0, -40)-(100, -40), still 100 long, its grips with it, and Bottom's own stretches of the bottom corners
do not follow its being carried, as Bottom chains no actions (281 of 0); Bottom then set to 150 takes the bottom right
corner to (150, -40), and Depth set back to 80 carries it up to y -20, still 150. The base point the INSERT keeps of a
parameter carried is where the moves took it, which is no grip edit. An attribute definition locked in its block that a
stretch, a move, a scale or a rotate holds moves, scales or turns with the rest in the anonymous block, and each ATTRIB of
the INSERT of its tag changes as its definition's place changes, carried by the INSERT's placing: Mark set from x 20 to 30
on an INSERT turned a quarter turn at the scale 2 moves the ATTRIB 20 along y in the world, keeping a place the user gave
it, and setting Mark back takes it back; a scale of Width from 200 to 150 on that INSERT takes an ATTRIB at (480, 200), 10
high, to (485, 150), 7.5 high, as AutoCAD 2021 scaled the library's (check D30). AutoCAD's Block Editor takes into an
action only an attribute locked so, and every one an action holds in AutoCAD's Dynamic Blocks samples is; AutoCAD 2021
left an unlocked one of the library's move, scale and rotate where it was, and moved one a stretch held in the anonymous
block but not its ATTRIB, and the library works such a block of another writer out the same way (check D30). Still
refused, with NotSupportedException and nothing written: a move carrying a parameter other than a linear one, or its own,
or a parameter that chains its actions or that a scale or an array follows; a move holding a grip of a parameter it does
not carry; a stretch, a scale, a rotate, a flip or an array holding other parameters; an attribute definition in a polar
stretch, a flip or an array; and attribute definitions of one tag that the actions change apart, where the INSERT has an
ATTRIB of that tag. A block of three linear
parameters, a point parameter, six stretches and five moves of these kinds, drawn in AutoCAD, is set this way, its
anonymous block at the INSERT's own values the same, entity for entity to 1E-9, as the one AutoCAD saved; AutoCAD 2021's
own sets of such a block, each of its five properties alone, all five, two set back, an ATTRIB dragged first, and three
sets continued (check v22), are the library's: its values, its 44 entities and its 15 ATTRIBs within 2E-12 (check D25b).
AutoCAD kept the INSERT's history of grip edits when it set a value, and so does the library, adding the value set after
it (check D26); AutoCAD's sets continued on the library's INSERTs drew the same.

A lookup parameter's table has input columns, each the distance of a linear parameter or the x or the y of a point
parameter (`DxfBlockLookupInput.Distance`, `X`, `Y`), and rows of a label and a value for each column
(`DxfBlockLookupRow`), as the panel SIZED above has. AutoCAD 2021 read every INSERT of such a block as dynamic and set
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

A block properties table (`AddBlockPropertiesTable`, AutoCAD's Block Properties Table) sets several properties and
attributes at once: its columns are properties of the block's parameters, as a lookup's inputs are
(`DxfBlockPropertiesTableColumn.Distance`, `X`, `Y`, [`Angle`](xref:GeometryHelper.Angle)), or attribute definitions of the block
(`DxfBlockPropertiesTableColumn.Attribute`), and each row (`DxfBlockPropertiesTableRow`) gives a number to each parameter's
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

An alignment parameter and a base point parameter are written as AutoCAD saved ACadSharp's samples of them, and give no
property; a block takes one of each, and AutoCAD 2021 read them so (check v21). AutoCAD drew an INSERT at (300, 0) of a
panel 100 by 50 of a base point at (20, 10) over x 300 to 400, the block's entities where they are, as the library keeps
them.

A polar parameter (`AddPolarParameter`) gives every INSERT two properties, its distance and then its angle, from its base
point to its end point, and a polar stretch (`AddPolarStretchAction`) turns and stretches with it: as the angle turns and
the distance grows, every entity it holds turns about the base point by the change of the angle, the points inside its
frame move besides along the new line by the change of the distance, and the entities given to turn only turn whole. A
rotation, an XY and a polar parameter take value sets as a linear one does, an angle's in radians within a turn. Each in a
few lines, added to the drawing above, as AutoCAD 2021 read and set the library's blocks (check D25):

```csharp
{
    // Polar and polar stretch: a pointer turned and stretched by its tip; Length 150 and Angle a quarter turn point it up the
    // y axis, its tip at (0, 150), the tick by its foot turned alone to (0, 20)-(-10, 20).
    var pointer = new DxfBlockTableRecord { Name = "POINTER", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(pointer);
    var shaft = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
    var head = new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5);
    var foot = new DxfLine(new GeoPoint3(20, 0, 0), new GeoPoint3(20, 10, 0));
    foreach (DxfEntity pointerPart in new DxfEntity[] { shaft, head, foot })
    {
        pointer.AppendEntity(pointerPart);
    }

    DxfBlockPolarParameter reach = pointer.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0),
        new GeoPoint3(100, 0, 0), -20, DxfBlockParameterValueSet.Increments(50, 300, 10), null);
    pointer.AddPolarStretchAction(reach, new GeoPoint2(90, -10), new GeoPoint2(110, 10), new DxfEntity[] { shaft, head }, new[] { foot });

    // Rotation of a value set: a needle turns in steps of 15 degrees; 40 degrees turns it 45.
    var dial = new DxfBlockTableRecord { Name = "DIAL", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(dial);
    var needle = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0));
    dial.AppendEntity(needle);
    DxfBlockRotationParameter dialAngle = dial.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 30, 0,
        DxfBlockParameterValueSet.Increments(0, 2 * Math.PI, Math.PI / 12));
    dial.AddRotateAction(dialAngle, new[] { needle });

    // XY of value sets: Width by 10 from 50 to 300, Depth one of 50, 80 and 120; 77 by 100 gives a sheet 80 by 80.
    var sheet = new DxfBlockTableRecord { Name = "SHEET", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(sheet);
    var sheetEdge = new DxfPolyline();
    sheetEdge.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
    sheetEdge.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
    sheetEdge.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
    sheetEdge.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
    sheetEdge.Closed = true;
    sheet.AppendEntity(sheetEdge);
    DxfBlockXYParameter sheetSize = sheet.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0),
        DxfBlockParameterValueSet.Increments(50, 300, 10), DxfBlockParameterValueSet.List(new[] { 50.0, 80, 120 }));
    sheet.AddStretchAction(sheetSize, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { sheetEdge });

    var pointed = new DxfBlockReference(new GeoPoint3(0, 600, 0), pointer);
    var dialled = new DxfBlockReference(new GeoPoint3(300, 600, 0), dial);
    var sheeted = new DxfBlockReference(new GeoPoint3(600, 600, 0), sheet);
    db.ModelSpace.AppendEntity(pointed);
    db.ModelSpace.AppendEntity(dialled);
    db.ModelSpace.AppendEntity(sheeted);
    pointed.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 150.0;
    pointed.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Angle").Value = Math.PI / 2;
    dialled.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Angle").Value = 40 * Math.PI / 180;
    sheeted.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Width").Value = 77.0;
    sheeted.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Depth").Value = 100.0;
    db.Save("panel.dxf");
}
```

A polar stretch holds each entity by its points in the frame, as a stretch does: a point held turns about the base point by
the angle less the block's own and then moves along the parameter's line by the distance less the block's own, as the end
point does, and every other point of the entity turns alone. AutoCAD 2021 drew the pointer so, its line's start kept at the
base point, a polyline from (50, -5) to (100, -5) whose end alone was in the frame going to (5, 50)-(5, 150), a text and an
arc held whole turning and moving with the tip, and worked each set out from the block's own values, not from the last
set. An entity of one point is held whole by it, and an arc by both its ends; an arc with one end only in the frame is
refused, as AutoCAD's way with it is not known. The entities to turn only are written apart from the action's selection
(the 331s, not among the 330s): named in the selection too, AutoCAD 2021 turned the tick and moved it as well, to (70, 0)
for a Length of 150, and the library works such a block of another writer out the same way, as it leaves out a grip of
the parameter named in the selection, as AutoCAD's Annotation sample names one. An entity a polar stretch turns is worked on
by no other action, either way round. The parameter and the action are written as AutoCAD saved ACadSharp's polar sample,
code for code, and the INSERT's cache keeps the parameter's base point, end point, the normal down and the base point 10
along x, as AutoCAD 2021 wrote it.

A polar parameter also takes a stretch (`AddStretchAction`) and a move (`AddMoveAction`), each by its end point and with
the overloads of a distance multiplier and an angle offset a linear one has: they follow the end point's move along x and
along y and turn nothing, as AutoCAD 2021 drew them (check D25b, 0 lines apart from the library). A shaft from (0, 0) to
(100, 0) set to a Length of 150 and a quarter turn moves a circle at its tip to (0, 150) and a text at (95, 3) to (-5, 153),
upright; a circle at (50, 0) moved of the multiplier 0.5 goes to (0, 75), and one at (100, 20) moved turned a quarter turn
to (-50, -80); a right edge (100, 30)-(100, 50) stretched by its end goes to (0, 180)-(0, 200). They are written as a
linear parameter's are, of the connections EndXDelta and EndYDelta of the polar parameter, and may stand beside a polar
stretch of other entities:

```csharp
{
    // Stretch and move of a polar parameter: Length 150 and Angle a quarter turn take the tip circle to (0, 150), its label
    // to (-5, 153) unturned, and the right edge of the frame to (0, 180)-(0, 200).
    var reacher = new DxfBlockTableRecord { Name = "REACHER", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(reacher);
    var arm = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(100, 0, 0));
    var hand = new DxfCircle(new GeoPoint3(100, 0, 0), GeoVector3.ZAxis, 5);
    var handLabel = new DxfDBText { Position = new GeoPoint3(95, 3, 0), Height = 2.5, TextString = "A" };
    var post = new DxfLine(new GeoPoint3(100, 30, 0), new GeoPoint3(100, 50, 0));
    foreach (DxfEntity reacherPart in new DxfEntity[] { arm, hand, handLabel, post })
    {
        reacher.AppendEntity(reacherPart);
    }

    DxfBlockPolarParameter armReach = reacher.AddPolarParameter("Length", null, "Angle", null, new GeoPoint3(0, 0, 0),
        new GeoPoint3(100, 0, 0), -20, null, null);
    reacher.AddPolarStretchAction(armReach, new GeoPoint2(90, -5), new GeoPoint2(110, 5), new[] { arm }, new DxfEntity[0]);
    reacher.AddMoveAction(armReach, new DxfEntity[] { hand, handLabel });
    reacher.AddStretchAction(armReach, new GeoPoint2(90, 25), new GeoPoint2(110, 60), new[] { post });

    // Lookup of an XY, a polar and a rotation parameter: Size Large makes a plate 200 by 80; Way North turns a needle up.
    var plateSizes = new DxfBlockTableRecord { Name = "PLATE_SIZES", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(plateSizes);
    var plateEdge = new DxfPolyline();
    plateEdge.AddVertexAt(0, new GeoPoint2(0, 0), 0, 0, 0);
    plateEdge.AddVertexAt(1, new GeoPoint2(100, 0), 0, 0, 0);
    plateEdge.AddVertexAt(2, new GeoPoint2(100, 50), 0, 0, 0);
    plateEdge.AddVertexAt(3, new GeoPoint2(0, 50), 0, 0, 0);
    plateEdge.Closed = true;
    plateSizes.AppendEntity(plateEdge);
    DxfBlockXYParameter plateXY = plateSizes.AddXYParameter("Width", null, "Depth", null, new GeoPoint3(0, 0, 0), new GeoPoint3(100, 50, 0));
    plateSizes.AddStretchAction(plateXY, new GeoPoint2(90, 40), new GeoPoint2(110, 60), new[] { plateEdge });
    plateSizes.AddLookupParameter("Size", null, new GeoPoint3(50, -20, 0),
        new[] { DxfBlockLookupInput.X(plateXY), DxfBlockLookupInput.Y(plateXY) },
        new[] { new DxfBlockLookupRow("Small", 100, 50), new DxfBlockLookupRow("Large", 200, 80) });

    var compass = new DxfBlockTableRecord { Name = "COMPASS", Origin = new GeoPoint3(0, 0, 0) };
    db.BlockTable.Add(compass);
    var compassNeedle = new DxfLine(new GeoPoint3(0, 0, 0), new GeoPoint3(30, 0, 0));
    compass.AppendEntity(compassNeedle);
    DxfBlockRotationParameter heading = compass.AddRotationParameter("Angle", null, new GeoPoint3(0, 0, 0), 30, 0);
    compass.AddRotateAction(heading, new[] { compassNeedle });
    compass.AddLookupParameter("Way", null, new GeoPoint3(0, -20, 0), new[] { DxfBlockLookupInput.Angle(heading) },
        new[] { new DxfBlockLookupRow("East", 0), new DxfBlockLookupRow("North", Math.PI / 2), new DxfBlockLookupRow("West", Math.PI) });

    var reached = new DxfBlockReference(new GeoPoint3(0, 900, 0), reacher);
    var sized = new DxfBlockReference(new GeoPoint3(300, 900, 0), plateSizes);
    var headed = new DxfBlockReference(new GeoPoint3(600, 900, 0), compass);
    db.ModelSpace.AppendEntity(reached);
    db.ModelSpace.AppendEntity(sized);
    db.ModelSpace.AppendEntity(headed);
    reached.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Length").Value = 150.0;
    reached.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Angle").Value = Math.PI / 2;
    sized.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Size").Value = "Large";
    headed.DynamicBlockReferencePropertyCollection.First(p => p.PropertyName == "Way").Value = "North";
    db.Save("panel.dxf");
}
```

A value set is taken as AutoCAD 2021 took it. A distance of an XY or a polar parameter goes to the nearest step, a tie to
the step below (75 to 70 of 50 to 300 by 10, as AutoCAD took all 18 such ties of check D25), and to the nearest of a list,
a tie to the block's own value where it is one of the two, else to the lesser, as a linear parameter's: 65 to 50 and 100 to
80 of 50, 80, 120 and 150 for a Depth of its own 150, and 135 to 150, from every value before, as AutoCAD took all 80 such
ties of an XY and a polar parameter (check D25b). An angle goes round the circle: of the steps of 15 degrees, 40 to 45, 370
to 15 and -10 to 345; of steps from 0 to 90 degrees, 120 to 90, 300 to 0 and 200 to 90, the bound nearer round the circle;
of the list 0, 90, 180 and 270, 338 to 0. A tie of angles goes to the value behind the one asked, clockwise, 22.5 degrees
to 15 and 315 to 270; AutoCAD's own choice in such a tie followed its rounding rather than a rule, the step behind in 26 of
58 ties of steps of 15 degrees and the value behind in 23 of 32 of the list above, 135 going to 180 each time. A list of
angles may hold nought; a set of angles reaching beyond a turn, or a set that does not hold the block's own value, is
refused.

`insert.ConvertToStaticBlock("BOLT_500")` makes the INSERT place a
new block BOLT_500 holding copies of what it draws, the anonymous block's entities (or the dynamic block's, where it places
that), its tie to the dynamic block (the entry AcDbBlockRepresentation of its extension dictionary) taken out;
`ConvertToStaticBlock()` names the copy as AutoCAD names an anonymous block, `*U1` the first the drawing has not, and no
INSERT of it names: a drawing whose PPP holds an INSERT of `*U1`, a block it has not, gets `*U2`. A name given that an
INSERT of the drawing names, which would then place the new block, is refused where that block would come to insert itself
through it, PPP holding an INSERT of MM and the block made static an INSERT of PPP; nothing is written then. Exploding an
INSERT is set out under [Editing rules](rules.md#editing-rules).
