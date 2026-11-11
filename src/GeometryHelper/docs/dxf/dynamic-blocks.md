# Dynamic blocks

A dynamic block is a block whose INSERTs carry properties, a width or a visibility state, that change what they draw.
The library reads those properties, sets them as AutoCAD 2021 sets them, and makes such blocks in code. The
parameters and actions besides the linear one and visibility are on [Parameters and actions](dynamic-actions.md);
lookups, block properties tables and constraints on [Lookups, tables and constraints](dynamic-tables.md).

## What can be made

A dynamic block is made in a drawing of R2010 or later (`new DxfDatabase("AC1024")`) by one
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

## What is refused, and how it was checked

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

## Blocks drawn in AutoCAD's Block Editor

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

## Reading the properties of an INSERT

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

## Making one: a linear parameter and its stretch

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

## Setting a value

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

## Visibility states

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
may take linear parameters besides: its INSERTs keep the state and the distances together. Every state lists the block's
other parameters, their grips and its actions, those added before the visibility parameter and those added after it, as
AutoCAD's Block Editor lists them in each state of its Door sample: a Width of SIGN is shown, with its grip, in Cross, Tick
and Blank alike. AutoCAD 2021 read a state that lists none as hiding them (Width not visible in any state, its grip not
shown), as the library wrote them up to 13.11.0 (probe GhDb1). Writing the parameter takes a fifth of a millisecond.

## Grips, reset and the states of a block

`GetDynamicGrips` gives the custom grips AutoCAD shows on a dynamic INSERT selected, where the INSERT's values put them,
in the coordinates of the file: of each linear, polar, XY, rotation and horizontal, vertical or radial constraint
parameter the grip at its end, of an XY parameter the grips at its two other corners after it (base x with end y, end x
with base y), of each flip, point, visibility and lookup parameter and of a block properties table its grip, in the order
of the grips in the block's graph. Each `DxfDynamicBlockGrip` gives its `GripType`, its `Position`, the `BasePoint` its parameter measures
from, the `Direction` an arrow points (along a linear parameter, across a flip's line, to the other side while flipped)
and the `PropertyNames` it sets. A grip stands where the block puts it, moved as the point it follows moved: Width set to
200 on an INSERT of the panel above at (1 000, 500) turned a quarter turn has its grip at (1 000, 700), its base point
(1 000, 500) and its direction (0, 1). On AutoCAD 2021's Architectural and Annotation samples every custom grip
`GetGripPoints` gave came out at its point and in its order (probe GhDyn1), Door Size of the Door at (750, 0), Hinge at
(375, -312.5) and Opening Angle at (-125, 375). A rectangle 100 by 50 of an XY parameter from (0, 0) to (100, 50) inserted
at (1 000, 500) gives its grips at (1 100, 550), (1 000, 550) and (1 100, 500), and a block properties table at (0, -20)
its grip at (1 000, 480), of the type `BlockTable`, as AutoCAD 2021 gave them (probe GhDb1). A linear or an XY
parameter's base grip is not given, as a value set moves the end alone; AutoCAD 2021 showed none of an XY parameter.

A block of a visibility parameter shows a parameter, its grip and its property in the states that list it, as AutoCAD's
Block Editor lists them: `VisibleInCurrentVisibilityState` is false for the others, and their grips are not given, as
AutoCAD 2021 showed no grip of the Angle of its Toilet sample in the state Elongated (Front). `Show` is the parameter's Show
properties, false for Stud Height of AutoCAD's Stud sample; AutoCAD's Properties palette lists a property where both are
true. `StateNames` gives the names of a flip's two states, Not flipped and Flipped, or Left and Right for Hinge of the
Door sample.

`ResetBlock` puts every value of the INSERT back to the block's own, as AutoCAD's `ResetBlock` and RESETBLOCK: the INSERT
places the dynamic block again and its AcDbBlockRepresentation goes, the extension dictionary kept, as AutoCAD 2021 left
it. The place, the scale and the ATTRIBs stay where they are, and the anonymous block stays in the drawing until purged:

```csharp
{
    DxfBlockReference reset = db.ModelSpace.OfType<DxfBlockReference>().First(i => i.DynamicBlockTableRecord.Name == "PANEL");
    reset.ResetBlock();
    // reset.Name is PANEL again; Width reads 100.
}
```

`GetVisibilityStates` lists a block's states, as the Block Editor's Visibility State box does, each a
`DxfBlockVisibilityState` of its name and the entities it shows: SIGN reads Cross (the cross), Tick (the tick) and Blank
(none), the frame in no state, shown in all three.

## See also

- [Parameters and actions](dynamic-actions.md): move, scale, point, rotation, flip, XY, arrays, polar parameters and value sets.
- [Lookups, tables and constraints](dynamic-tables.md).
- [Blocks and attributes](blocks.md): INSERTs and attributes.
- [What the model does not do](rules.md#what-the-model-does-not-do).
