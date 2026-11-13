# Dynamic blocks: parameters and actions

Every parameter and action of AutoCAD's Block Editor the library writes, each with an example as AutoCAD 2021 took it,
and how each is worked out when a value is set. The examples are added to the drawing of the first example of
[Dynamic blocks](dynamic-blocks.md#making-one-a-linear-parameter-and-its-stretch).

## Move, scale, point, rotation, flip and XY

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

## Arcs, multipliers, arrays, lookups and grips

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
bolts every 25 by 20 from 12 at 100 by 70 to six at 60 by 70.

### A base point a grip moved

The INSERT of the XY sample of 100.59 by 104.30 keeps its base point where a grip moved it, from (0, 5) to (0, 104.30), its
end point at (100.59, 0). A linear or an XY parameter's base point is worked out from where the INSERT keeps it, as
AutoCAD 2021 worked it out (plugin GhDy2a): the end point is the base point and the distance, and a stretch, a move or an
array that follows the end point moves its points by the end point's change from the block's own, the base point's move
included. A plate 100 by 50 whose XY parameter runs from (0, 0) to (100, 50), its right and top edges stretched by the end
point, whose corner of base x and end y was dragged by (20, 10) keeps the base point at (20, 0) and the end point at
(100, 60): it reads Width 80 and Height 60 and the right edge stays at x 100, and a Width then set to 120 takes the end
point to (140, 60). The same holds of a linear parameter, Width set to 200 from a base point at (20, 0) putting its end
point at x 220.

`MoveBasePoint` of one of the parameter's properties (`DxfDynamicBlockReferenceProperty`) moves the base point as a grip does, the end point staying: a distance shrinks as the
base point goes towards the end point, an XY parameter's along x and along y, a linear parameter's along its line alone,
and two moves add up. A stretch or a move that follows the base point (BaseXDelta and BaseYDelta) and the first copy of an
array (which AutoCAD ties to the base point) follow the base point where it stands: the left edge and the lower attribute definition
of a plate whose base point is dragged by (20, 0) start 20 along x, and the array of a grid whose base grip is dragged by
(10, 20) starts at (10, 20), and they stay there at the next value set, as AutoCAD 2021 kept them (probe GhDb4a): a panel whose base grip went 10 along x and whose Width is then
set to 150 has its right edge at x 160 and its left, top and bottom edges still from x 10, in memory, after the drawing was written to DXF or
DWG and read back, and from the library's own file; a second drag adds to the first (the first circle of an array at x 60, and at 70 after
10 more). The distances a move leaves are taken to the parameter's value set, and the base point stands where they put it, the end point staying, as AutoCAD 2021 did (probe GhDy3a): a panel whose Width is 50 to 300 by 10 and whose base grip is dragged by 33 reads Width 70 with the base point at 30, by 60 Width 50 (the minimum) with it at 50; a plate of Width 50 to 300 by 10 and Height 20 to 100 by 5 dragged by (33, 13) reads 70 and 35, base point (30, 15), and by (60, 50) 50 and 20, (50, 30); an axis the move leaves alone is not taken.

The base point of a polar parameter is moved the same way, anywhere in the plane: a pointer from (0, 0) to (100, 0) of Length 50 to 300 by 10 and angles every 15 degrees whose base grip is dragged by (20, 15) reads Length 80 and Angle 345 degrees, the end point staying and the base point at (22.73, 20.71); with no value set a drag by (30, 10) reads Length 81.39 and Angle 354.3 degrees. The polar stretch that holds the pointer's end then measures from the new base point, as AutoCAD 2021 drew it: the base grip by (20, 0) and the end grip to 50 at 45 degrees from the new base point turn the line from (0, 0) to (100, 0) into one from (5.86, -14.14) to (55.36, 35.36). `MoveEndPoint` drags the end grip, the distance and the angle taken to their value sets (of a linear parameter the distance along its line, of an XY parameter each distance along its axis).

AutoCAD 2021 works the grip edits of a polar parameter out one after the other, on the lines as they stand (probe GhDb4a): each edit turns them about the base point as it stands then, by the angle the end point goes through from there, and moves the points the stretch holds along the new direction by the distance it gains; an edit that leaves the end point where it was (a base grip dragged) changes nothing. The end grip to block point (0, 50), the base grip by (30, 0) and the end grip to (35, 0) leave the first line, which the stretch does not hold at its start, from (45.43, 25.72) to (35, 0) and the tip from (35, 0) to (28.998, 9.432), where the lines worked out from the final values alone would stand at (0, 0); the base grip first and the end grip to (0, 50) after it leave (45.43, -25.72) to (0, 50). The library works them out the same way from the INSERT's history of grip edits (below), the Length and the Angle set between them too, about 1 000 random sequences of the edits (and of Origin sets) on five kinds of pointer (free, with value sets, with a list, turned and scaled, with points on the base side) giving AutoCAD's lines to 1E-3; where the INSERT keeps no history that leads to where it stands (a polar distance or angle set away from the block's own, the history taken out) it does not know the edits and refuses a base grip with NotSupportedException. The base point of a rotation or a flip parameter has no grip: AutoCAD 2021 gave none, and the grip at the insertion point moved the INSERT.

### The Origin set

`Origin` of a linear, an XY or a polar parameter is set to a point in the coordinates of the file (`GeoPoint3`), as AutoCAD 2021 sets it (probe GhDb4a): the point is taken into the block's coordinates, and the parameter's base point and its end point move together to it, the distances and the angle kept. A pointer at (1 000, 500) whose `Origin` is set to (1 030, 520, 0) reads (30, 20, 0), keeps Length 100 and Angle 0 and leaves its lines where they were, the polar stretch measuring the end point from the base point; a panel 100 wide from (0, 100) whose `Origin` is set to (30, 120) runs from (30, 120) to (130, 120) and the edges its stretches hold move by (30, 20), off the line too, so that a linear parameter's base point may stand beside its line. On an INSERT turned a quarter turn at the scale 2 the same point reads (10, -15, 0). The `Origin` of a rotation or a constraint parameter is read only (`InvalidOperationException`). Edits after it work from where the base point went: the end grip to block point (80, 60) after `Origin` (30, 20) leaves Length 64.03 and the first line from (19.07, -14.36) to (69.07, 25.64).

### The history of grip edits

A block with a polar parameter, an array or a scale action keeps a history of grip edits in the INSERT (ACAD_ENHANCEDBLOCKHISTORY, started with a `1070` of 3 at the first edit), as AutoCAD 2021 started one (probe GhDb4a): a pointer, a panel with an array, a grid and a bar a scale action scales gave one, a block of stretches, moves, rotates and flips alone none. A grip dragged leaves `GRIPLOC` under its node, how far it went in the block's coordinates (the end grip, the base grip, the end grip of a linear or an XY parameter, the grip of a point parameter, the end grip of a rotation parameter and the two other corners of an XY parameter: a plate dragged from its corner of base x and end y by (10, 5) keeps one `GRIPLOC` of (10, 5, 0) and no record of Height or of the base grip, a point grip dragged by (20, 10) keeps (20, 10, 0), a rotation grip taken a quarter turn about its base point from (60, 0) to (0, 60) keeps (-60, 60, 0)), `Origin` the new point, a value set the property's name and the value in the block's own units (Width 150 of Width 300 at the scale 2); a record of the node and the name of the last replaces it, two drags of one grip in a row add up, and a drag of 1E-12 or less, or one a second drag brings back, leaves none. The history is what the library replays to work a polar parameter's edits out.

The library still sets no property of an INSERT that keeps the base point of a rotation or a flip parameter away
from the block's own, or the base point of a linear, an XY or a polar parameter that a move carries (below) or a scale or a rotate
follows, beyond the point tolerance of `Tolerance.Default`: it refuses with
NotSupportedException, writing nothing.

## What a block from AutoCAD's Block Editor holds

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
it (check D26); AutoCAD's sets continued on the library's INSERTs drew the same. The history of a block with a polar parameter is started by the first value set or grip dragged, as AutoCAD 2021 started it (probe GhDy3a): the node's id, the property's name and the value, and for a grip dragged the name GRIPLOC and how far the grip went in that drag, in the block's coordinates (`1071` the grip's node, `300` GRIPLOC, `11` the vector): the pointer's end grip dragged to (0, 50) writes (-100, 50, 0), and dragged on to (10, 0) one record of (-90, 0, 0). A record of the node and the name of the last one is merged into it (the value of a property replaced, the vectors added), as AutoCAD 2021 kept Width 120 alone of 140 and then 120.

## Alignment and base point parameters

An alignment parameter and a base point parameter are written as AutoCAD saved ACadSharp's samples of them, and give no
property; a block takes one of each, and AutoCAD 2021 read them so (check v21). AutoCAD drew an INSERT at (300, 0) of a
panel 100 by 50 of a base point at (20, 10) over x 300 to 400, the block's entities where they are, as the library keeps
them.

## Polar parameters

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

## Value sets

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

## See also

- [Dynamic blocks](dynamic-blocks.md): reading, making and setting, visibility states.
- [Lookups, tables and constraints](dynamic-tables.md).
- [What the model does not do](rules.md#what-the-model-does-not-do).
