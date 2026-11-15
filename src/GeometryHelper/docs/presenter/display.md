# Opening, drawing and picking

A drawing opened as a [`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument), turned into what the canvas draws, and
asked what lies under a point or inside a box. Nothing on this page needs a window: the document, its display lists and
its index are plain objects, which is how the presenter's own tests drive them.

The examples use a file of six entities, a rectangle of four lines 100 by 60 on a layer Steel, a circle of 20 across at
its middle on the same layer, and a TEXT "PLATE" 5 high above it at (10, 70).

## The document

```csharp
CadDocument document = CadDocument.Open("plate.dxf");             // or Open(stream, name), or From(database, name)
document.Title;                                                   // "plate.dxf"
document.EntityCount;                                             // 6, the entities of model space
document.Spaces;                                                  // Model, Layout1
document.Layers;                                                  // 0 (white) and Steel (red, #FF0000)
```

| Member | What it gives |
|---|---|
| `Open(path)`, `Open(stream, name)` | the drawing read by [`DxfDatabase.Open`](xref:GeometryHelper.Dxf.DxfDatabase); a stream has no file of its own, so `HasFile` is false and `Save()` refuses where `SaveAs(path)` does not |
| `From(database, name)` | a drawing already in memory, as a model drawing's is |
| a drawing with no handles | one of R12 written by hand, with neither header nor tables, is read anew with a handle for each entity, as AutoCAD gives them on opening it: the next above every handle of the file, in its order, with `$HANDSEED` above them and `$HANDLING` 1, a header made where it has none. The document picks, snaps and edits by handle and an undo reads the drawing back from its saved bytes, so the handles are written, and saved with it, as AutoCAD saves it. Three walls and a column read as LINEs 1 to 3 and the CIRCLE 4, and `Database` is the copy read anew |
| `Database` | the [`DxfDatabase`](xref:GeometryHelper.Dxf.DxfDatabase); a new object after an undo or a redo, so hold the handle of an entity, not the entity |
| `FindEntity(handle)`, `FindSpace(handle)` | the entity of a handle, or null where there is none or it is erased; the space that holds it |
| `Layers` | a [`CadLayerInfo`](xref:GeometryHelper.Dxf.Viewer.CadLayerInfo) for each layer: name, colour as 0xRRGGBB, on, frozen, locked, linetype, line weight in millimetres |
| `Selection` | the [`CadSelection`](xref:GeometryHelper.Dxf.Viewer.CadSelection) of the document (see [Selecting](#selecting)) |

## Spaces

A [`CadSpace`](xref:GeometryHelper.Dxf.Viewer.CadSpace) is model space or one layout. `Spaces` lists model space first, then
the layouts in the order of their tabs, a tie going by name; the layout "Model" of the file is not listed twice.
`CurrentSpace` is the one the canvas shows.

A layout is a sheet of paper, and `PaperBounds` says where the paper lies in the layout's units, as AutoCAD shows it behind
the layout: the paper size of its plot settings about its plotted area, the lower left corner of the printable area at the
origin. A layout of A3 on its side with margins of 7.5 at the sides and 20 above and below lies from (-7.5, -20) to
(412.5, 277). Where a layout has no paper size its limits are taken, and where it has neither, what it draws with a margin
of 2 per cent of its larger side. Model space has no paper and its `PaperBounds` is empty.

## What is drawn

`GetDisplay(space)` gives the [`DisplayList2`](xref:GeometryHelper.Dxf.Viewer.DisplayList2) of a space, made the first time
and kept. It holds [`DisplayItem2`](xref:GeometryHelper.Dxf.Viewer.DisplayItem2) objects in drawing order, the first at the
back, and the box that holds them all. Each item carries the handle of the entity it came from, its layer, and a
[`DisplayStyle`](xref:GeometryHelper.Dxf.Viewer.DisplayStyle): colour, line weight and dashes, already worked out. The
items of a drawing share the strings of its layer names and give back the room the list made by doubling left empty: a
drawing of 100 000 entities (178 000 items, one in five a text) keeps 35.5 MB of display list where it kept 41.1 MB.

The items themselves are small. An item holds its handle and one object, shared by every item of its style and layer, that
also holds the clip of a block and the few flags an item may have, so that an item without them pays for neither; a chain of
two points (a LINE) is its box and two flags saying which corner each end is at, with no array of vertices, and `Xy` makes the
list of four when asked, so ask it once; a text keeps its SHX strokes, its parts, its measure, its slant and a width factor other
than 1 in one object made when it has any of them; the insertion point of an entity is kept in its entry, and what an entity
read, for the edits that draw it again, apart from the runs that read nothing. A LINE takes 80 bytes where it took 96 and an array
of 56, an arc or a circle 104 where it took 128, a text 128 where it took 192, an insertion point 40 where it took 100 and a run
24 where it took 32. A drawing of 1 000 000 entities (1 780 000 items: lines, arcs, circles, polylines with a bulge, texts and one
INSERT in fifty) keeps 237 MB of display list where it kept 335, and of 100 000 entities 23.6 MB where it kept 33.4. An arc keeps
its box: worked out at each ask, from four sines and cosines, it made the frame of 1 000 000 entities shown whole 10 per cent
slower, for the box of each arc drawn as a dot is asked for at every frame.

The entities the list is made from keep what they have read, and that is small too: a DXF of 1 000 000 entities (77 MB), once
displayed, holds 364 MB in the objects of its database, 73 MB of them its bytes, where it held 737 MB. The entities of a drawing
share the object of each look, a polyline asked for its width keeps none of its vertices, and an entity as it was read from the file
takes 104 bytes. Opened, displayed and its pick index made, the drawing grows the heap by 650 MB (the display list 237 MB of it).

The styles a hatch or an MTEXT makes of its own, the lines of a pattern, the middle colour of a gradient and the runs of an MTEXT in a
colour, are one object for each colour and weight, as the style of a LINE is. A drawing of 100 000 entities, half of them hatches (solid, or
ANSI31 and AR-BRSTD) and half MTEXTs of three to four lines, 250 000 texts among the items, held 75 000 styles with their looks, one for
each hatch and MTEXT, and holds three: 343.6 MB of heap where it held 351.0.

What else a displayed drawing keeps is kept for the next time it is drawn, and was weighed before it was let go. The shapes an entity has
read (128 bytes of each of 1 000 000 entities, 128 MB) take 2 to 5 µs and 3 KB of garbage to read again for a LINE, an ARC, a CIRCLE or a
TEXT, and 10 µs and 6 KB for a polyline. A layer switched on at 1 000 000 entities, which draws its entities again, took 1.1 s with the
shapes kept, 1.5 s with those of the polylines read afresh (578 MB of heap in place of 651) and 2.4 s with all of them (523 MB). The
strings of a hatch's loops, 45 MB of the 351 MB of the hatch and MTEXT drawing above (900 bytes of a hatch of four vertices), cost 10 µs a
hatch to read again: a layer switched on in that drawing took 590 ms with them and 710 ms without (300 MB of heap in place of 351). They
stay. The handle of an entity is one string, shared by its database item, its display items and its run: 32 bytes of each entity, 31 MB at
1 000 000. An arc's box is 32 bytes of the 104 of its item, 25 MB at 1 000 000 entities; the frame of a drawing shown whole asks for it at
each arc it draws as a dot, so it is kept.

The shapes of a LINE, an ARC, a CIRCLE and a LWPOLYLINE are read from the bytes of their records (see "Numbers" in the reader guide), with
no string made of a group and no reader made for the entity: 0.2 to 0.5 µs and 64 to 88 bytes of garbage for the first three, where they
took 1.1 to 1.6 µs and 3.2 KB, and 2.0 µs and 3.0 KB for a polyline of four vertices with a bulge, where it took 4.0 µs and 6.6 KB (1.4 µs
and 2.3 KB of it the closed polyline with an arc that it is). The shapes of the 78 000 of them in a drawing of 100 000 entities take 86 ms (123 under .NET Framework 4.8)
where they took 170 (270), and its cold display (open not counted) 350 ms where it took 400 under .NET 8 and 550 where it took 650
under .NET Framework 4.8; a drawing of 1 000 000 entities displays in 3.1 to 3.9 s where it took 3.5 to 4.0 under .NET 8 and in 5.0 to 5.5 s
where it took 6.1 to 7.0 under .NET Framework 4.8. The 4 377 of them in an AutoCAD sample whose coordinates have 17 digits (070-PBBASE) take
14 ms where they took 37 under .NET 8, and 4 ms where they took 21 under .NET Framework. The display list, the heap (650.9 MB at 1 000 000
entities) and a layer switched on (1.0 s) are as they were, since the shapes are kept. They are kept still, the faster read
notwithstanding: a layer switched on at 1 000 000 entities with the polylines' shapes read afresh took 1.3 to 1.5 s where it takes 1.0 s, for
578 MB of heap in place of 651, as it did before the faster read, since a polyline's own making is most of its cost.

The shapes of a TEXT and an MTEXT are read from the bytes too, and the number reader every group goes through reads a number of up to 19
digits from its characters (see "Numbers" in the reader guide), but a cold display is hardly faster for them. A LINE, an ARC, a CIRCLE and
a polyline are drawn from their shapes, which are 20 % of the 300 ms of the drawing of 100 000 entities (.NET 8) already; a TEXT is drawn
from the groups of the entity itself, whose numbers are what the faster reader helps: the 20 000 TEXTs of the drawing alone display in
41 ms where they took 49 (.NET 8); and an INSERT is drawn from the entities of its block, not from the shapes it places, which only what
picks, snaps and measures from the shapes of the file asks for, with the layers of the entities round it. The cold display of the whole
drawing is the same within the noise of a machine other processes take cores of (medians of 9 runs, three alternated rounds: 311, 318 and
318 ms where it took 296, 321 and 311 under .NET 8; 510, 496 and 506 where 506, 509 and 495 under .NET Framework 4.8), and so is that of
the same drawing with 17 digits in every coordinate (about 350 ms under .NET 8, 560 under .NET Framework 4.8, both ways).

What the display itself reads of an entity was found in a sampled profile of that drawing before anything in it was changed, and it was
not the shapes. A LWPOLYLINE asked whether it has a width (`HasWidth`, asked of every polyline before it is drawn) made the typed value
of every group of its record, a string for each coordinate, to look at the widths among them: 31 % of the cold display of the drawing
and 58 % of one of polylines alone, 0.74 µs and 2.4 KB of garbage a polyline. Whether an entity is visible made the list of every group
of its record to read the one (60), 0.34 to 0.39 µs and 0.5 to 1.1 KB, and the look of an entity (layer, colour, line weight, linetype,
thickness and handle) a dictionary for each entity to hold the first group of eight codes, 0.44 µs and 344 bytes. Each now walks the
bytes of the record where its groups stand, with no list, no dictionary and no group made a string but the widths and the words: a
polyline's width 0.21 µs and 24 bytes, visibility 0.07 to 0.09 µs and 24 bytes, a look 0.28 µs and 128 bytes. A TEXT is drawn from one
pass over its groups (`DxfDBText.ReadForDisplay`) where each of the twelve properties the display reads, the words, height, place, turn,
slant, width factor, justification, mirrors and style, found its own among them again (about 2 µs and 2 KB for them); an attribute,
a text with an extension dictionary, whose contexts may put it elsewhere, and a copy that carries contexts are read property by
property as before. The pass keeps nothing, for the groups of a record never change once written and a text read afresh each time is
one an edit cannot leave stale; the 48-byte tuple the properties keep for the contexts of a text that has none is not made for a text the
display draws, 4.7 MB less heap at 100 000 TEXTs. A block with no handle, as in a file of R12 or one written by hand, was listed anew
for each INSERT of it (100 000 times in a drawing of INSERTs of one block); it is listed once a build, by its name.

The cold display of the drawing of 100 000 entities (open not counted) takes 255 ms under .NET 8 where it took 330, and 431 ms under
.NET Framework 4.8 where it took 519, with 154 MB of garbage in place of 277; a layer switched on 75 ms where it took 123 (.NET 8) and
124 where 175 (.NET Framework), and a REGEN 219 ms where it took 267. The 100 000 texts of the file of texts alone (18 groups each, a
second point, a turn and a style) take 245 ms where they took 360, the 100 000 polylines 439 where they took 492 (.NET 8: most of what is
left of a polyline is the loop it is made, 1.7 µs and 2.9 KB, which the shapes of an entity keep, a test holding that they are the same
objects each time they are asked for), and 100 000 INSERTs of a block of 20 entities 1 175 ms where they took 1 591, with 634 MB of
garbage in place of 1 619. The drawing of 1 000 000 entities displays in 2.8 s where it took 3.4 (.NET 8) and in 4.3 s where it took 5.0
(.NET Framework), 1 583 MB of garbage in place of 2 715 and the same 552 MB of heap. The display lists and the bytes a drawing
saves are those they were: 167 samples of AutoCAD and five generated drawings give the same list, item for item and field for field, and the same
pixels in three views, plain, selected, highlighted and with a hover, as they did (under .NET 8 the pixels of eight of the 172 drawings differ
between two runs of the unchanged code, and are judged by the lists alone).

What is left in a sampled profile of an INSERT's own part, after the above, was the INSERT itself and the loop of a closed polyline. An INSERT
was asked for its block (`BlockTableRecord`), its point (`Position`) and its placing (`BlockTransform`) one after another, each finding the
normal, the scale factors and the angle again among the groups of its record, and the placing making two matrices of sixteen numbers: 1.7 µs
and 2.5 KB of garbage an INSERT, 16 % of a drawing of INSERTs alone. They are now read in one pass over the bytes
(`DxfBlockReference.TryReadForDisplay`), 0.6 µs, for an INSERT whose record holds no extension dictionary: such an INSERT has no annotative
context and no clip, so neither is looked for (the dictionary of a record, its handle, and a clip's boundary in it, were each a walk over its
groups). An INSERT with a dictionary, a copy that carries contexts or one in no drawing, and one given its block by code, are read property by
property as before. The arithmetic is that of the properties, step for step, and a test holds the point, the entries of the matrix and the block
the same to the bit over generated records. Whether an INSERT is an array (a MINSERT, told when its object is made, for every INSERT of a
drawing) is found by a walk of the bytes, as is a polyline's width, where it was told from the list of all its groups. An INSERT that
no ATTRIB follows no longer makes the frame its attributes are drawn in. The block a run of INSERTs places and the layer a run of entities
are on are found again by the very object asked for last, where a dictionary was asked each time. The loop of a closed polyline with a bulge
(`GeoPolygonArc3`, and its area, which the display asks for to tell a loop from a line) was made of three lists, copies of the lists
given, a chain of its vertices to find the plane and an edge for each side, then laid out through four more lists: it is built in the
arrays it keeps, with the same plane and the same edges, and laid out into the arrays of the loop it makes, 0.61 µs and 2.2 KB for the loop
and its area where it is 0.37 µs and 0.6 KB (.NET 8; every vertex, bulge, normal, plane and area is the same to the bit, as are the words of
every exception, over 300 000 generated loops). The end of a line of a record is looked for in its first 24 bytes by a plain loop, before
the vectorised search of the rest, which a line of three bytes pays more to start than to finish.

The cold display of the drawing of 100 000 entities (open not counted) takes 217 ms under .NET 8 where it took 247, and 345 ms under .NET
Framework 4.8 where it took 449 (medians of three alternated rounds of seven), with 121 MB of garbage where there were 155; a layer
switched on takes 45 ms where it took 52 and 78 where 89, and a REGEN 109 ms where 124 and 173 where 221. 100 000 INSERTs of a block of 20
entities take 858 ms where they took 1 115 (.NET 8), and 1 494 where 2 057 (.NET Framework), with 424 MB of garbage in place of 640, and a
layer switched on in them 316 ms where 441 and 550 where 758. The drawing of 1 000 000 entities displays in 2.1 s where it took 2.3
(.NET 8) and in 3.8 s where it took 4.4 (.NET Framework), with 1 155 MB of garbage in place of 1 492 and a REGEN 1.2 s where 1.3 and 1.8 where 2.2; a MOVE
and its display at 1 000 000 entities, 250 to 300 ms, is as it was within the noise of the machine. The display lists, the bytes a drawing
saves and the pixels are those they were: 167 samples of AutoCAD and nine generated drawings (INSERTs plain, scaled alike and unalike, mirrored,
with a normal, in arrays and with attributes) give the same list, item for item and field for field, and the same bytes saved, under
.NET 8 and .NET Framework 4.8, and 171 of them the same pixels in three views, plain, selected, highlighted and with a hover.

The note of that round said a MOVE and its display at 1 000 000 entities was no slower and not told from the noise of the machine. It
was slower: with the machine quiet and the read-aheads of the undo steps given time to finish between the moves, the display after a
MOVE of one line (the move itself is 10 ms) took 232 and 239 ms before the INSERTs were read in one pass and 296 and 292 after (.NET 8,
medians of eight moves, alternated). The display after an edit makes the list again from the older one: it walks every entity and
copies the items of those the edit left alone, and for each it asked the older list's table of insertion points for the entity's
handle (a hash of a string that ignores case and a probe among 220 000 entries, a million times), 180 ms of the 300. The run of an
entity now says whether the entity kept a point when it was drawn (a bit in the count of its items, so that a run is still 24 bytes),
and only those 220 000 are looked for. The patched list then shares the table of the older list and copies it only when it has a point
to take out or put in (those of an entity drawn again, erased or hidden since, taken out as the patch comes to it, a run of the older
list that no entity met being found by one pass over a bit for each run where the count of the entities met is short), so that moving
a line copies nothing and moving a text copies it once. The display after a MOVE of one line
at 1 000 000 entities takes 165 ms where it took 295 (.NET 8) and 375 where it took 540 (.NET Framework), the MOVE with its display 125
ms where 336 and 307 where 399; the rest is the walk over the entities and the copy of their 1 780 000 items.

What a display asks of the record of an entity was still two walks over its bytes, one for its look (layer, linetype, thickness,
colour, lineweight, true colour and handle) and one for whether it is hidden. It is one: the walk over the groups of the head record
finds the first of each code of the look, as before, the places where they stand are hashed and compared with those of the looks the
file has shared (64 are remembered by their bytes), so that an entity of a look met already makes no string of them and no `Look`,
and whether the entity is hidden is settled in the same walk by the rules of the group 60 and kept with the look, to be read again when
the record is written anew. A group 102 that opens a group of an application is told from its bytes, a brace or not as `Trim` reads
the value (the first byte that is not a space or a control white space, read as text only where it is beyond ASCII), and no longer made
a string. The origin of a block (`DxfBlockTableRecord.Origin`, 0.25 µs and three reads for each INSERT) is read once until the records
of the file next change, as the contexts of an INSERT are, and the style of the last colour and weight is answered as it was, as the
layer already was. A layer switched on at 100 000 entities takes 48 ms where it took 60, at 1 000 000 entities 492 where 696, and a
REGEN of 1 000 000 entities 1.2 s where 2.0.

The greatest handle of a drawing, which opening it asks for (a pass over every group of every record, a quarter of the time it takes to
open 1 000 000 entities), is found as the walk that lists the records goes by their groups: the first 5 of each record outside the
header (105 for a DIMSTYLE, whose 5 is the name of its arrow block in a file of R12) and the greatest value of a pointer (320 to 369,
390 to 399, 480, 481 and 1005). 1 000 000 entities open in 0.50 s where they took 0.90 (.NET 8), 100 000 in 45 ms where 86, and under
.NET Framework 1.8 s where 3.1 on a loaded machine (0.87 s where 1.30 on a quiet one). A new handle is the one it was: tests over
generated files hold it, with the seed, the pointers, the header's own 390 and a DIMSTYLE.

An INSERT whose scale factors are not alike (2 and 0.5, for one) or whose block is turned out of the drawing's plane draws the arcs and
circles of its block as the chains of points they become, 180 for a circle and up to 91 for an arc, each found by the shape's own
arithmetic: 0.16 ms an INSERT of a block of 20 entities with five circles and five arcs, 3.5 s for 20 000 of them. The points of a shape
of a block, before the matrix of the INSERT, are found once a build for all the INSERTs that place it (the shape is a value that never
changes, so a sample is never out of date), and each INSERT puts them through its own matrix into its array at once. 20 000 INSERTs
display in 0.41 s where they took 3.5 (.NET 8; 0.72 s where 4.5 under .NET Framework), a layer switched on in them in 0.19 s where 1.2, with
451 MB of garbage where there were 1 479; every point and chain is the same to the bit. The cold display of 100 000 mixed entities is as it
was within the noise (222 ms where 233; the first touch of every record and its shapes is what it does), 98 MB of garbage where 107.
The display lists and the bytes a drawing saves are those they were: 167 samples of AutoCAD and twelve generated drawings (INSERTs plain,
scaled alike and unalike, mirrored, with a normal, tilted, in arrays; 100 000 mixed entities and 100 000 INSERTs) give the same list,
item for item and field for field, the same bytes saved and after a MOVE and an added entity, and the same new handle, under .NET 8 and
.NET Framework 4.8, and 179 of them the same pixels in three views, plain, selected, highlighted and with a hover.

What was left of the display after a MOVE of one line at 1 000 000 entities, the walk over every entity and the copy of its 1 780 000
items one by one, is gone. A display list keeps the list of entities it was drawn from, and its version, which the space of a drawing
counts up for each entity added to it or erased from it (an entity added stands last), and its runs, its items and its box are arrays
that a patch copies in stretches: where the entities are those the older list was made from, but for those erased and those added, the
patch does not walk them. The entities an edit names are found by their places among the entities of the space (a few, up to twelve,
by a walk of their records, and by the handle index the editing already keeps), the runs of those erased by one walk over the handles
of the runs, the runs that read a layer the edit changed or something a named entity drew from their masks, and each is drawn again,
or dropped; the runs between are copied by one `Array.Copy` of their items and one of their records, and the entities added are drawn
last. Where that cannot be known (an undo or a redo, which read the drawing back as other records; more entities named than a walk
finds by place; an entity added or erased that no handle names; an entity with no handle; a draw order, or the Block Editor hiding
entities; an MTEXT that lists linked columns, which the walk of its block leaves out) the patch walks the entities as before, but by
their records and not by an object made for each, finds the older run of each by the run that follows the last (and by one of the
sixteen after it where an entity was erased), copies the runs it takes in stretches, and looks the runs up by handle only where an
entity is many runs from its place. The box of the patched list is the older list's grown by the items drawn again where no item
dropped or drawn has a side level with a side of the older box (the items kept then still reach every side, so it is the box a fold of
every item gives, to the bit), and is found again from every item where one has, or more than 64 runs were dropped, as a layer switched
drops them. At 1 000 000 entities (best of seven, alternated with the older build, .NET 8 and .NET Framework 4.8) the display after a
MOVE of one line takes 18 ms where it took 93 and 22 where 198, after an ERASE of one 26 ms where 196 and 25 where 278, after a COPY of
one 25 ms where 192 and 30 where 283, with 76 MB of garbage where 115 under .NET 8; at 100 000 entities 1.4 ms where 7.8 and 1.5 where
17.5 for the MOVE, 1.9 where 13.8 and 1.5 where 24.8 for the ERASE, 2.3 where 13.7 and 2.2 where 23.7 for the COPY, 3.9 MB of garbage
where 6.5 (.NET 8). The cold display, a layer switched on or off and a REGEN are as they were within the noise of the machine (the
1 000 000 entities displayed in 2.4 s and regenerated in 0.76 s under .NET 8 as before), because they draw every entity or a third of
them again, and the display list keeps its 627 MB. The shapes an entity keeps stay: not keeping those of a LINE, an ARC and a CIRCLE
would take 55 MB off the heap (572 MB where 627) and put 27 per cent on a REGEN and on a layer switched on (1.6 s where 1.26, 0.73 s
where 0.57), the shapes being read afresh for each. The display lists, the bytes a drawing saves and the pixels are those they were: 167
samples of AutoCAD (cold, after a MOVE, an ERASE, a layer off and on, a REGEN and the layouts; the bytes saved but for the four with an
ACIS body, whose bytes are not the same from one run to the next in any build) and a generated drawing of 20 000 entities after eight
MOVEs and ERASEs, a COPY, a layer off and on and a REGEN, item for item and field for field, under .NET 8 and .NET Framework 4.8, and
the pixels of the 167 samples and of a generated drawing of 100 000 entities in three views, plain, selected, highlighted and with a hover.

`PrepareDisplay(space, progress, cancellationToken)` makes the list and the pick index of a space ahead, item for item the
list `GetDisplay` makes, telling `progress` the share of the space's entities drawn every 4 096 of them and stopping, with
an `OperationCanceledException`, when the token is cancelled; a list stopped is not kept, and is made whole when next
asked for. The window calls it on a worker thread for a drawing it opens, after `CadDocument.Open(path, progress,
cancellationToken)`, which is stopped the same way (see "Opening a drawing" in the app guide). A document is not safe to use
from two threads at once, so nothing else may use it until it returns.

| Item | What it is |
|---|---|
| [`DisplayPolyline2`](xref:GeometryHelper.Dxf.Viewer.DisplayPolyline2) | a chain of points, open or closed: a line is one of two points; an ellipse and a spline are sampled into one |
| [`DisplaySegments2`](xref:GeometryHelper.Dxf.Viewer.DisplaySegments2) | many separate segments in one item: the lines of a hatch's pattern |
| [`DisplayRay2`](xref:GeometryHelper.Dxf.Viewer.DisplayRay2) | a line without end from its base point: both ways for an XLINE, on from it for a RAY; the canvas draws the part the view shows (`TryClip`) |
| [`DisplayArc2`](xref:GeometryHelper.Dxf.Viewer.DisplayArc2) | an arc or a whole circle, kept as an arc and flattened by the canvas to the tolerance of the screen |
| [`DisplayFill2`](xref:GeometryHelper.Dxf.Viewer.DisplayFill2) | a filled outline with its holes: a solid hatch, a SOLID, and a WIPEOUT, which masks what is under it |
| [`DisplayText2`](xref:GeometryHelper.Dxf.Viewer.DisplayText2) | a text with its place, height, turn, width factor, justification and font name; the canvas sets the letters, a TEXT's spaces before and after its words kept, as AutoCAD justifies by them: "ABC " on the right of its baseline a space further left than "ABC" |
| [`DisplayViewport2`](xref:GeometryHelper.Dxf.Viewer.DisplayViewport2) | a viewport of a layout: its frame, and the model-space items it shows |

The six entities of the file give six items: four `DisplayPolyline2`, one `DisplayArc2` and one `DisplayText2`, in a box
from (0, 0) to (100, 75). A polyline of a LWPOLYLINE with an arc segment gives a `DisplayPolyline2` and a `DisplayArc2`.

How each entity is drawn:

| Entity | Drawn as |
|---|---|
| LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE | lines and arcs; a bulge stays an arc |
| ELLIPSE, SPLINE | sampled: a spline into 128 segments |
| POINT | a polyline of one point |
| 3DFACE | its edges |
| SOLID, WIPEOUT | a fill; a wipeout is a mask |
| HATCH | a solid fill, a gradient filled with the colour half way along it, or the lines of its pattern cut by its loops; a hatch that holds no lines of its pattern, or would draw more than 20 000, is drawn by its boundary |
| TEXT | a text item; the library has no fonts, so the box of a text is estimated from its height and its characters. In an SHX font, `%%U`, `%%K` and `%%O` draw a line under, through or over the letters after them, as AutoCAD 2021 draws it: 0.2 of the height below the baseline, half the height or 1.2 heights above it, along the letters' pens but 0.15 of the height left of them, slanted as the letters, and a justified text is boxed with its lines ("%%uAB" of simplex 10 high is lined from -1.5 to 18.98); in a TrueType font the codes are left out and no line is drawn yet |
| MTEXT | a text item whose `Parts` are its picture, laid out as AutoCAD 2021 lays it out: the mask and the frame, then a text for each run of each line and a polyline for each line under, over or through a run and for the bar of a stack; see below |
| INSERT, MINSERT | the block's entities under the INSERT's matrix, to a depth of 12 blocks; every item is picked as the INSERT; the values of its ATTRIBs are drawn where the file puts them, an invisible one left out |
| DIMENSION, TABLE | the block AutoCAD drew it with |
| LEADER, MLEADER | exploded, as AutoCAD breaks them: the arrowhead, then the line or spline |
| VIEWPORT | a `DisplayViewport2` |
| IMAGE | the outline of its frame; no pixel is read |
| RAY, XLINE | a `DisplayRay2`, across every view it crosses; its box reaches without end the ways it runs, so a pick anywhere along it finds it, while the box of the list holds its base point alone, as AutoCAD's ZOOM Extents does: a line 100 by 50 from the origin and an XLINE through (200, 300) zoom to the box from (0, 0) to (200, 300). A crossing box or lasso it runs through takes it; a window never does |

An entity that is not visible, or lies on a layer that is off or frozen, is left out. ByLayer and ByBlock colour, line weight
and linetype are resolved here, a line weight left at the default being 0.25 mm, and dashes are the linetype's pattern
times LTSCALE and the entity's own scale. One entity that cannot be drawn never stops the others: it is passed over.

### MTEXT

The words of an MTEXT are set in the fonts of the document (`document.Fonts`), measured letter by letter, by the rules
AutoCAD 2021 was found to follow on 155 MTEXTs of 536 lines:

- **Wrapping.** The words wrap at the defined width (group 41) at their spaces alone: never after a hyphen, a word too
  long stands whole past the width, and `\~` holds two words together. A width of 0 wraps nothing. The attachment (71)
  hangs the lines by a corner, a side or the middle of the box the width and the lines make; a defined height (46)
  taller than the lines moves no line.
- **Ink.** A line starts at the ink of its first letter, but ink left of the pen is not counted: italic.shx's f, whose
  tail runs left of where the pen stands, is 0.809524 of the height wide and stands out of the box, as in AutoCAD. An
  SHX letter's ink is measured as AutoCAD measures it: an arc of less than a half circle reaches the rectangle of its
  chord and of the line along it touching the arc, a longer one the square about its circle (isocp.shx's 3 is 0.441421
  of the height wide, its strokes 0.4), and where the pen is put down counts though it moves no further (monotxt.shx's
  1 is half the height wide).
- **Line spacing.** From one baseline to the next is 5/3 of the text height times the factor (44). Under `Exactly`
  (73 = 2) it is that whatever the runs. Under `AtLeast` (73 = 1) each line has a top, the height of its tallest run or
  of its stack, its first baseline that far below the top of the MTEXT; the next line's top lies the factor times
  (the top of this line + a third of its tallest run's height + a third of the text height) below this line's top. For
  lines of the text height alone that is the same 5/3; in an MTEXT 2.5 high, the line after one with a run 5 high has
  its top 5 + 5/3 + 2.5/3 = 7.5 below that line's top. An empty paragraph is a line of 5/3 of the height.
- **Stacks.** `\S1/2;` sets its parts one over the other with a bar, `\S1^2;` with none, and `\S1#2;` along a diagonal,
  each part as high as its ink; `\A0;`, `\A1;` and `\A2;` set the words beside a stack at its bottom, middle or top.
- **Paragraphs.** In a `\p` code, `i`, `l` and `r` indent the first line, the left and the right; `ql`, `qc`, `qr`,
  `qj` and `qd` align left, centre, right, justified and distributed; `t` sets tab stops (left, centre, right, decimal;
  with none a stop every 4 heights); `b` and `a` give space before and after the paragraph, and `sm`, `se` and `sa` a
  line spacing of the paragraph's own (a multiple, exactly, at least).
- **Columns.** Static and dynamic columns take the lines that fit their height, each column its width and the gutter
  on from the last; `\N` starts the next column.
- **Mask and frame.** The background mask reaches the scale factor (45) less 1 of a text height beyond the lines, and
  the frame (R2018) stands where a mask of its scale would, 1.5 for a frame with no mask.

"II II II" 20 wide in letters 10 high, whose I is inked 1 in and moves the pen 4, is set as "II II" and, 16.67 below it,
"II": an item whose box is 20 wide and 26.67 high. The box of an MTEXT's item is that of its parts, so picking, window
selection and the highlight follow the lines as they are drawn. Not as AutoCAD: a flow direction (72) of top to bottom
is set left to right, `\pxsm` with a factor is 0.05 of a height off on one line in 536, and `\M+` codes are shown as
written.

The MTEXT editor (TEXTEDIT, MTEXT) sets the words being typed by this same layout, as AutoCAD's in-place editor shows
them as they will look:
[`CadMTextEditorLayout`](xref:GeometryHelper.Dxf.Viewer.CadMTextEditorLayout) lays out the editor's words with the
MTEXT's width, attachment, spacing, columns, mask and frame, and gives the parts the canvas would draw and where each
place of the editor stands. The editor's frame draws those parts at the canvas's zoom, so a line breaks, a baseline lies
and a stack stands in the editor where it will be drawn. In "II II II" 20 wide the caret after the last letter is on the
second line, 7 along it, and the space the first line breaks at stays at that line's end, at 20. A word typed past the
width takes the caret down with it; Home, End and the arrows up and down follow the lines as they wrap; an Enter at the
end makes an empty line for the caret, which the drawn MTEXT does not have. Typing in an MTEXT of 2 000 letters lays it
out again in about 0.5 ms a key and draws it in about 1 ms. A width code in times is times the style's width, as AutoCAD
2021 draws it: `{\W1.2x;ABC}` in a style 0.8 wide is 0.96 wide in the editor as in the drawing, and written back as
`\W0.9600;`. Where a line breaks between two letters with no space, as between two ideographs, End puts the caret at
the end of that line, though its place is the first of the next line, and Home, Up and Down go on from the line it is
drawn on; a click past the end of such a line does the same.

The MTEXT command's Columns option opens the editor with those columns: Static with its defaults, in an MTEXT 2.5 high,
gives two columns 93.75 wide with a gutter of 12.5 and 25 high, which the ruler and the Columns menu then change, and
the MTEXT is written with the columns the editor ends with, No Columns too.

`CadMTextEditor.SetNumbering` sets the selected paragraphs as a list, as AutoCAD 2021's Bullets and Numbering writes
one (checked against its TextEditor): each paragraph hangs 3 heights in with a tab stop there and opens with its label
and a tab, "Plate" and "Beam" numbered `\pxi-3,l3,t3;1.\tPlate\P2.\tBeam`, lettered `a.\t` or `A.\t`, bulleted
`{\fSymbol|b0|i0|c2|p18;·\t}`; a label already there is replaced, and Off takes the labels, the indent and the stop
away. The Text Editor tab's Bullets and Numbering menu offers it as AutoCAD 2021's does: Off, Numbered, Lettered
(Lowercase, Uppercase) and Bulleted, the one every selected paragraph has ticked (`CadMTextEditor.NumberingOf` reads a
paragraph's), none where they differ, then Start and Continue, Allow Auto Bullets and Numbering and Allow Bullets and
Lists, grey as the editor does not build them, the last ticked as lists are always allowed. The editor's right-click
menu has the same items under Bullets and Lists, with no list ticked, as AutoCAD's.

The layout an MTEXT is drawn with is kept by the document from one drawing to the next, REGEN too, and taken again only
where the words, the sizes, the spacing, the columns, the style's width and slant and the faces of the fonts are the same
to the bit, so a drawing drawn again shows the same picture, faster: 10 000 MTEXTs drawn again after a REGEN in 128 ms
under .NET Framework 4.8, where a new document lays them all out in 233 ms (196 ms before the layouts were kept).

The ruler on the editor's frame works as AutoCAD 2021's. The box at its left end gives the kind of the next tab stop,
Left, Centre, Right or Decimal, each click the next; a click on the ruler adds a stop of that kind at the nearest minor
tick to the selected paragraphs, or the caret's. A stop dragged along the ruler moves, snapping to the minor ticks; one
dragged more than 12 pixels off it is removed. The upper triangle drags the first line's indent, the lower one the
later lines', and the arrows at the right end the width. With columns the ruler spans every column, the gutters shaded:
the arrows at the end of the first column drag the width of every column, the edge of the first gutter the gutter, the
arrows at the end of static columns the total, and the grips under the frame the height (one grip, or one under each
dynamic column of its own height). Each release is one step of the editor's undo, and Esc during a drag puts it back.
A right-click on the ruler gives Set Mtext Width and Set Mtext Height. Ticks stand every 1, 2 or 5 × 10ⁿ units, the
smallest at least 30 pixels apart, with four minor ticks between: at 6 pixels to a unit, a major tick every 5.

The Paragraph dialog (the launcher of the Paragraph panel, Line Spacing › More…, the editor's right-click) sets the tab
stops, the first-line, hanging and right indents, and, each ticked to be the paragraph's own, the alignment, the
paragraph spacing and the line spacing. Everything is written as AutoCAD 2021's own text editor writes it, checked
against it: in an MTEXT 2.5 high a left stop at 10, a centre one at 20, a right one at 30 and a decimal one at 40 write
`\pxt4,c8,r12,d16;`, a decimal one on a comma `D,16`; a first line at 2 and the later lines at 10 write `\pxi-3.2,l4;`;
2 before and 3 after `\pxb0.8,a1.2;`; 1.5x `\pxsm1.5;`. Only what differs from the paragraph before is written, and the
paragraph after those changed is put back as AutoCAD puts it back: a stop set on the first of "A" and "B" writes
`\pxt4;A\P\ptz;B`. Exactly and at least are distances as AutoCAD's paragraph spacing reads them, 2.5 spacing an MTEXT
2.5 high as 1x does; as AutoCAD's own editor, it takes a multiple from 0.25x to 4x, those distances from 0.25 to 4, and a
space before or after of nought or from 0.25 to 4. Line Spacing on the ribbon sets the paragraphs' own multiple, as
AutoCAD's does, and Clear Paragraph Spacing takes it off. The Columns menu (No Columns, Dynamic Columns › Auto or Manual
Height, Static Columns › 2 to 6, Insert Column Break, Alt+Enter, and Column Settings) and the Column Settings dialog set
the columns; the box and the columns are written with the words when the editor closes, one undo step of the drawing.
A column break typed is written `\N`, and one already there is kept as one.

Esc ends an edit in place as AutoCAD 2021 ends it. In the edit box of a single-line text it drops what was typed: a
TEXT that read "ROOM 1" and was typed over as "ROOM 2" still reads "ROOM 1", and nothing is added to the undo list. In
the MTEXT editor with nothing changed it closes the editor; with changes it asks Multiline Text - Unsaved Changes, "Do
you want to save your text changes?": Yes (Enter) writes them and closes, No drops them and closes, and Cancel (Esc)
goes back to the editor. A Yes or No given with Always perform my current choice ticked is kept in Global settings
(`MTextUnsavedChanges`, `"hiddenMessages"` in the JSON), as AutoCAD keeps it in its Hidden Message Settings, and Esc then
does it with no question; Cancel is never kept. Text and fonts › Hidden messages, set back to Ask, or Reset to defaults,
shows the question again. A host that sets no `AskKeepMTextChanges` keeps the changes, as Close Text Editor does.

### Viewports

A layout shows model space through its viewports. A `DisplayViewport2` has the frame in paper units (`CenterX`, `CenterY`,
`Width`, `Height`), the point of model space at the middle of the frame (`ViewCenterX`, `ViewCenterY`), `Scale`, the paper
length of one unit of model space (a viewport at 1:20 has 0.05), the `Twist` in radians, counter-clockwise as AutoCAD
turns the view, the layers frozen in that viewport only, and the model-space display list it draws, clipped to the frame.
`ModelToPaper` is the matrix from one to the other. The viewport's own view centre, which AutoCAD keeps about the view's
target and turned by the twist, is taken back to the point of model space it stands for.
Because a viewport draws model space, an edit in model space drops the display lists of every layout too.

A viewport clipped by an entity (VPCLIP) has its `Clip`, a [`DisplayClip2`](xref:GeometryHelper.Dxf.Viewer.DisplayClip2): the
boundary as a closed ring of paper units, a circle as two vertices with the bulge 1 and the arcs of a polyline kept as
arcs, an ellipse or a spline sampled finely, `Flatten(tolerance)` cutting the arcs to a chord that lies no farther than the
tolerance and `Contains` and `DistanceToEdge` answering for a point. A REGION boundary with a hole, or of several faces, adds its other
rings (`Others`, `FlattenAll(tolerance)` giving every ring): a point is inside where it is inside an odd number of them, so that a
ring inside the first is a hole, as AutoCAD 2021 plotted one with nothing of model space in it. The canvas draws model space through that outline in
place of the frame (arcs kept to a tenth of a pixel), draws no frame of its own, since the boundary entity is the border and
is drawn as itself, and a pick finds the viewport by the distance to the outline. [`CadViewportClips.OutlineOf(viewport)`](xref:GeometryHelper.Dxf.Viewer.CadViewportClips)
makes the outline from the viewport. A viewport that is not clipped is drawn as it was, its frame a rectangle.

A block reference or external reference clipped by XCLIP is drawn inside its boundary alone. Each item it draws has a
`BlockClip`, a [`DisplayClipRegion`](xref:GeometryHelper.Dxf.Viewer.DisplayClipRegion): the boundary as a ring in drawing
units (`Ring`, `Outline`), within the region of the clipped block the reference stands in (`Parent`), with the box of
what is shown (`Bounds`) and `Contains`, `Reaches` (inside, or within a distance, as a pick asks) and `Meets` (a box, as a
crossing asks) answering for a point or a box. The box of the display list is the box of what is shown, and the canvas, a
plot and the images written clip every item to its region. The frame of the boundary is a closed chain of its own
(`IsClipFrame`, left out of a plot where XCLIPFRAME is 2, `IsNotPlotted`) that no clip touches. A [`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex)
finds a clipped item only where its region does, a window takes the reference by the box of what is shown, and a crossing and
the aperture of a snap meet only the part inside. Each cell of a MINSERT is clipped by its own place, and a clip that is
switched off draws the block whole. A clip made in a system tilted out of the plan has the region where its prism cuts the plane
of the block, and the frame where its boundary stands in the plan. The front and back planes of Clipdepth leave out the items
of the block that lie wholly outside them, in height, and cut a line that crosses one; the display keeps no height, so any other
entity that crosses a plane is drawn whole.

### Keeping the lists

A display list and its index are kept until an edit touches the space, when its own are dropped and made again the next time
they are asked for; `Changed` says which spaces and which handles. `Regen()` drops every list, as REGEN does. A change
to the layer table, a layer switched off or given a colour, makes the builder read the table again.

## The view

[`CadView`](xref:GeometryHelper.Dxf.Viewer.CadView) is what part of a space the canvas shows and at what zoom: the point of
the drawing at the middle of the canvas, and the pixels one drawing unit takes. Panning and zooming are arithmetic on those
three numbers, so they are tested here. Y runs up in the drawing and down on the screen.

```csharp
var view = new CadView(800, 600);
view.ZoomTo(display.Bounds);                       // 7.2 pixels to the unit: the plate's 100 by 75 plus 5 per cent each side
view.DrawingToPixel(50, 30, out double px, out double py);   // (400, 354)
view.ZoomAt(100, 100, 1.2);                        // one wheel notch in, about the pixel (100, 100): 8.64 pixels to the unit
view.Pan(40, 0);                                   // the drawing moves 40 pixels to the right
```

| Member | What it does |
|---|---|
| `ZoomTo(box, margin)` | shows a box wholly with a margin of 5 per cent of the canvas on each side, as ZOOM Extents; an empty box shows the origin at 1 pixel to the unit |
| `ZoomAt(px, py, factor)` | zooms about a pixel, the point of the drawing under it staying under it |
| `Pan(dx, dy)` | moves the drawing under the canvas by pixels |
| `ScreenToDrawing`, `DrawingToPixel` | the two ways between a pixel and a point |
| `VisibleBox`, `DrawingToScreen` | the box of the drawing on the canvas, and the matrix from the drawing to the pixels |
| `Resize(w, h)` | a new canvas size, the centre kept |

The zoom is held between 1E-9 and 1E9 pixels to the unit.

## Picking

[`PickIndex`](xref:GeometryHelper.Dxf.Viewer.PickIndex) is a grid over the items of one display list that answers which of
them lie near a point or in a box without looking at every item. It works in the drawing's coordinates, never in pixels, so
a caller working in pixels divides by the zoom first. `GetPickIndex(space)` gives the index of a space, made once and kept
with its display list.

| Call | Gives |
|---|---|
| `Pick(x, y, tolerance)` | the handles of the entities drawn within `tolerance` of the point, in the order AutoCAD picks them: the one drawn on top first while the drawing's SORTENTS holds its bit 1 (object selection, in AutoCAD's default 127), else the one drawn first, however near the others lie. Two lines crossing at the point come back the one drawn later first; with a tolerance of 3, a line drawn on top 2.7 away comes before one under the point |
| `IndicesIn(box)` | the places of the items whose box meets the box, in drawing order: what the canvas draws of a view |
| `Window(box)` | the entities drawn wholly inside the box, as a window selection takes them |
| `Crossing(box)` | the entities that cross or lie in the box, as a crossing selection takes them |
| `ItemsNear(box)` | the items whose box meets the box, in drawing order, as object snaps ask for what lies under the aperture |

An entity is picked as a whole: all the items it was drawn as answer with its one handle, and the entities that make up an
INSERT answer as the INSERT. On the plate:

```csharp
index.Pick(50, 40.5, 2);                           // the circle: 0.5 off its rim
index.Window(new DisplayBox(-1, -1, 101, 61));     // the four lines and the circle; the text, above y = 70, is out
index.Window(new DisplayBox(-1, -1, 60, 61));      // the circle and the left edge, the only two wholly inside
index.Crossing(new DisplayBox(-1, -1, 60, 61));    // those two and the bottom and top edges, which it cuts
```

The grid has up to 512 cells a side, the square root of the number of items. An item whose box covers more than a quarter
of the grid is kept in a list of its own, so one large hatch does not slow every cell. On 99 856 lines a pick takes 0.002 ms
and a window of 100 by 100 that takes 100 of them 0.56 ms.

## Selecting

[`CadSelection`](xref:GeometryHelper.Dxf.Viewer.CadSelection) holds the handles of the selected entities, ignoring case.

```csharp
document.Selection.Set(index.Pick(50, 40.5, 2));   // the circle
document.Selection.Add(index.Window(box));         // more
document.Selection.Toggle(handle);                 // as Shift+click does
document.Selection.Changed += (s, e) => { /* Properties and grips follow */ };
```

`Add`, `Remove`, `Toggle`, `Set` and `Clear` raise `Changed` only when the set changes. On the canvas a click picks within the
pickbox, Shift+click toggles, and a click on nothing starts a box that the next click ends: left to right a window, right to left
a crossing, Esc dropping it (Select by dragging in Global settings lets it be dragged too) (see [Settings, autosave and the window](app.md#the-keys-and-the-mouse)).

## See also

- [Editing as AutoCAD does](editing.md): the selection is what MOVE, ERASE and Properties work on.
- [Owners](owners.md): an entity whose owner keeps it read-only is still drawn, picked and selected.
- [Reading DXF](../dxf/reader.md#reading-dxf): the shapes the display is made from.
