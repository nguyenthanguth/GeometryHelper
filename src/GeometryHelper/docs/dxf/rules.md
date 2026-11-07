# Editing rules and what the model does not do

## Editing rules

**A property given the value it has writes nothing.** Every setter of an entity, a table record, the header and the
dictionaries compares first: given the value it has, it changes nothing, to the byte, and never throws, whatever the file
holds. A change writes the record it is about anew in the file's own style, or, for a vertex of a long polyline, the knots of a
spline or a loop of a hatch, the run of groups it is about, and every other record keeps its bytes.

**Appending.** `AppendEntity` writes an entity made in code at the end of the ENTITIES section, or before the ENDBLK of a
block, with a handle above every handle of the file and every handle something points at, `$HANDSEED` set above it, the
block's BLOCK_RECORD as its owner from R13 on and group 67 in paper space, the blocks of its other layouts
(`*Paper_Space0` and on) among it. Its layer is added to the layer table where it is
not listed, in the colour 7 with CONTINUOUS, and the applications of its extended data to the table of applications. A file
of R12 takes no subclass markers and has no SPLINE, ELLIPSE, RAY, XLINE, LWPOLYLINE, HATCH, LEADER or MULTILEADER, nor
lineweights, true colours, or a linetype scale or visibility of an entity's own: those are refused, by `AppendEntity` and by
the setters alike, as an ELLIPSE is: "A ELLIPSE cannot be added to a DXF of version AC1009, which has no such type: R13
(AC1012) brought it." Nor has R12 an MTEXT, which is written there as one TEXT for each paragraph (`\P`, a column break `\N`
taken as one, blank paragraphs at its end left out): its words without their formatting, as `DxfMText.Text` reads them, its
height, turn and plane, the lines spaced as `LineSpaceDistance` says and held at the left, the middle or the right as the
MTEXT is held, in its text style where the drawing has it or is given it by name with the copy, else Standard. The TEXTs are
appended in its place and the MTEXT stays in no file: an MTEXT of two lines 2.5 high held at its top left at (10, 20) comes
out as two TEXTs on the baselines 17.5 and 13.33 high. A paragraph is not wrapped at the MTEXT's width, as AutoCAD's SAVEAS to
R12 wraps it by the letters of its font, which the library does not read.

**Walking while editing.** A block is walked over the entities it holds when the walk begins, so that
`foreach (DxfEntity e in db.ModelSpace) e.Erase();` erases them all, as it does in AutoCAD: an entity erased on the way is
passed over, one appended on the way is not reached, and nothing throws.

**Erasing.** `Erase()` takes an entity out with every record of it, a POLYLINE its vertices and end and an INSERT its
attributes, and its handle is not given again; when the file is saved, a GROUP no longer lists it, and one it leaves empty
goes, a list of reactors, a SORTENTSTABLE and a dictionary no longer hold it, the objects it owned go with it, and any other
pointer at it, as a table cell's at an erased attribute definition or an XRECORD's at an erased entity, stays where it stands,
the saving naming up to ten of them in the log (Warn) once, not again at each saving after. A table record and a block are
erased only where nothing uses them, as each says above.

**Moving.** `TransformBy` moves, turns, scales or mirrors an entity as AutoCAD's does: its record is written anew with the
new geometry, its look kept, and its extended data moved with it in the same writing, a POLYLINE that of its vertices and an
INSERT that of its attributes; one with none of the groups `XDataTransformBy` moves keeps the bytes of its extended data.
`GetTransformedCopy` moves a copy. Each class refuses what it cannot hold, with an `ArgumentException` and nothing written,
and an entity held in the system of its normal (a circle, an arc, a polyline of the plane, a text, an INSERT, a SOLID, a
hatch, a dimension) refuses a transformation that flattens that plane:

| Class | Takes | Refuses |
|---|---|---|
| `DxfPolyline3d`, `DxfSpline`, `DxfFace`, the meshes | any | nothing |
| `DxfLine`, `DxfDBPoint`, `DxfRay`, `DxfXline` | any | one that takes the normal of a line or a point, or the direction of a ray or an xline, to no length |
| `DxfSolid` | any, its corners held in the system of its new normal | a flattening |
| `DxfCircle`, `DxfArc` | a scale alike in their plane, a turn, a mirror | a scale more one way than the other, or a shear: a circle of radius 5 scaled by 2 along x would be an ellipse |
| `DxfEllipse` | any that leaves it an ellipse | a flattening |
| `DxfPolyline`, `DxfPolyline2d` | any, where they have no arcs; with arcs, a scale alike in their plane | with arcs, an uneven scale or a shear, which would make the arcs ellipses |
| `DxfDBText`, `DxfAttributeReference`, `DxfAttributeDefinition` | any, a shear given to the text as its lean; a mirror turns its normal over | a flattening; an attribute of several lines, a shear |
| `DxfMText` | a turn, a mirror, a scale even or not, the columns with it | a shear, which it cannot lean by, and a flattening |
| `DxfBlockReference`, `DxfMInsertBlock` | a turn, a mirror and an uneven scale, taken into `ScaleFactors`; the attributes with it | a shear, which its axes cannot hold, and a flattening |
| `DxfHatch` | any: under an uneven scale or a shear its arcs become ellipse edges, as ezdxf makes them; seed points and gradient moved | a flattening |
| `DxfLeader` | any, its offsets with it | a flattening |
| `DxfMLeader` | a turn, a mirror, a scale alike | an uneven scale or a shear, as AutoCAD refuses it |
| the dimensions | any, the points moved as ezdxf's `transform()` moves them, the block with them where it is the dimension's alone and placed at no point of its own | a flattening |
| `DxfTable` | a move, a turn, a tilt, a mirror and a scale alike in every direction, its sizes scaled with it, the block with it where no other entity names it | a shear or a scale unlike along its axes, which would make its cells other than the rectangles AutoCAD lays it out in |
| `DxfViewport` | a move and a scale alike, on its sheet, its view kept | a turn, a mirror or a stretch |
| `DxfRasterImage`, `DxfWipeout` | any, as AutoCAD 2021's `TransformBy` takes it: the corner moved, the pixel across and up turned and scaled, a shear leaning the sides and a mirror showing the image from behind; `GetTransformedCopy` of a wipeout refuses an uneven scale and a shear, as AutoCAD answers eCannotScaleNonUniformly | one that takes a side to no length, or the two onto one line |
| `DxfOle2Frame` | a move, a scale by one factor and a mirror about a line along x or y, the corners written into the head of its binary that AutoCAD reads; mirrored, the box of its corners, the picture not turned over, as AutoCAD 2021 mirrors one | a turn, a shear, a stretch or a move out of the plane xy, with `ArgumentException`: AutoCAD 2021 keeps a frame upright |
| `DxfUnknownEntity` | nothing | everything, with `NotSupportedException`: its geometry is kept as the file has it |

**Exploding.** `Explode()` is every entity's, as AutoCAD's is, and gives new entities in no file, to be appended. Each class
breaks as AutoCAD breaks it:

| Class | Breaks into |
|---|---|
| `DxfBlockReference` | the entities of its block placed where it puts them, one level down: the block's ATTDEFs among them, each a `DxfAttributeDefinition` placed as the rest are, and none of the INSERT's ATTRIBs, whose values stay in its `AttributeCollection` |
| `DxfMInsertBlock` | refused with `NotSupportedException`, of one cell or more, as AutoCAD 2021's `Explode` refused one of a circle in 2 rows by 3 columns and one of one cell (eNotApplicable); `ExplodeToOwnerSpace` gives its first cell |
| `DxfPolyline`, `DxfPolyline2d` | a LINE for each straight segment and an ARC for each bulged one, in its order, each with its look and thickness, its widths left out as AutoCAD leaves them out, and a segment of no length too: a closed square of side 10 with one side bulged by 1 gives three lines and a half circle. Each LINE and ARC is on the polyline's normal, as AutoCAD gives it, so that their walls stand where its wall stood: a polyline on (0, 0, -1) at elevation 5 with a thickness of 2 gives walls from z = -5 down to -7. A POLYLINE fitted to a curve or a spline breaks along the vertices it is drawn through |
| `DxfPolyline3d` | a LINE for each segment between two vertices it is drawn through, the closing one among them: the polyline through (0, 0, 0), (1, 2, 3) and (4, 5, 6) gives two lines |
| `DxfMText` | a TEXT for each paragraph, as it is written into a drawing of R12 (see Appending): B-12 and M16 on two lines, 2.5 high, held at the top left at (10, 20), give two TEXTs on the baselines 17.5 and 13.33 high; a paragraph is not wrapped at its width, and a part of it in another font or height is not given a TEXT of its own, as AutoCAD gives it |
| the dimensions | the entities of the block they are drawn with (`*D7`), placed where the dimension draws them, as ezdxf's `virtual_entities()` places them, but the POINTs on Defpoints that mark where it measures, which AutoCAD 2021's `Explode()` leaves out: the picture AutoCAD drew last, so a point set since is not in it |
| `DxfTable` | the entities of its block (`*T12`), the lines of its cells and their words, placed where the table draws them: the picture AutoCAD drew last, so a cell's text set since is not in it |
| `DxfPolyFaceMesh` | a 3DFACE for each face, in the order of the file, its corners the points its numbers name and the edge from a corner whose number is below nought hidden; a face of three corners ends on its third, and the edge from its third corner back to its first, the 3DFACE's fourth, is hidden with the third. A face record's own layer and colour are not the face's, as AutoCAD 2021's `Explode` gives a face of a record on a layer of its own, and of colour 1, the mesh's layer and BYLAYER. Its corners are its numbers up to the first of nought, as the reader takes them: a face of fewer than three corners so, (1, 2, 0, 4) among them, or one naming a point the mesh has not, is left out. A pyramid of a square base gives five faces |
| `DxfPolygonMesh` | a 3DFACE for each cell of its grid, row after row, from its corner (m, n) to (m + 1, n), (m + 1, n + 1) and (m, n + 1), closed where the mesh is closed: a grid of 3 by 4 gives 6 faces, closed both ways 12. A smoothed mesh breaks as AutoCAD 2021's `Explode` breaks one: its points of flag 8, one more than `MSurfaceDensity` and `NSurfaceDensity` along an open direction, read as a grid of the densities themselves, row after row from the first, each cell from (m, n) to (m, n + 1), (m + 1, n + 1) and (m + 1, n): the 7 by 7 points of a mesh smoothed at 6 and 6 give 25 faces, the first of the points 0, 1, 7 and 6, as AutoCAD's do; where those points fill neither grid, it breaks into the cells of its frame |
| `DxfLeader` | as AutoCAD 2021's `Explode()` gives it: where it has an arrow, its arrowhead first, at the first vertex pointing away from the second, `Dimasz` times `Dimscale` long: an INSERT of its `Dimldrblk` block, or AutoCAD's closed filled arrow as a SOLID a third as wide as long, its corners the one on the right of the leader, the tip and the one on its left; then, straight, one LWPOLYLINE through its vertices (a LINE between each two where they stand off the plane of its normal, which a polyline cannot hold), or splined, one SPLINE through them, of degree 3, leaving along its first segment and reaching along its last, its knots the lengths of the chords, and its fit data the vertices it runs through and those two directions, as AutoCAD's has three fit points; the line starts at the arrow's back. Each in the leader's own look, its colour, true colour, linetype and lineweight, whatever DIMCLRD, DIMLTYPE and DIMLWD say. The leader from (0, 0, 0) through (10, 10, 0) to (20, 10, 0) with an arrow 2.5 long gives a SOLID and a polyline from (1.77, 1.77, 0); the vertex AutoCAD adds where the words of its annotation end is AutoCAD's to work out from its font and is not among them |
| `DxfMLeader` | as AutoCAD 2021's `Explode()` gives it: its words as an MTEXT (as wide as its record says) or its block as an INSERT with its attributes, both in the multileader's own linetype and lineweight, then, leader by leader, its dogleg as a LINE from the last point of the leader, of no length where the dogleg has none, and line by line an LWPOLYLINE to the last point of the leader, or one SPLINE for a splined line, its knots the lengths of the chords, its fit data the points it runs through and its directions at the ends, then the arrowhead at the first vertex, where the first segment is more than twice as long as the arrow; each line starts at the back of its arrow, and the closed filled arrow is a SOLID a third as wide as long, its corners the one on the right, the tip and the one on the left. A multileader whose leader lines are splined has no dogleg drawn, and its splines run to the end the dogleg would have. A colour BYBLOCK is the multileader's own; the arrowheads have its own linetype, a SOLID its own lineweight and an INSERT of an arrow block its line's, turned by an angle from nought to a whole turn. The words on the right of two leader lines with a dogleg of 8 give an MTEXT, the dogleg, and a polyline and a SOLID for each line. The lines AutoCAD draws under or over the words, as long as the words are wide, are AutoCAD's to work out and are not among them. A copy in no file (`Clone()`) finds its blocks, linetype and text style in the drawing it was copied from and gives what its original gives, the values of its attributes among it; one whose block that drawing has erased since is refused with `InvalidOperationException`, the handle of the block said |
| an MLINE (`DxfUnknownEntity`) | from its record and its MLINESTYLE, for an open one its start caps, a LINE for each element along each segment, the miter lines at each joint where the style draws them, then its end caps: a line across the ends as two LINEs from the outer elements to the point halfway between them, an outer arc as one half circle, or two quarter ARCs where the outer elements' colours differ, and inner arcs joining each pair of inner elements. Each LINE has its element's colour and linetype. An element broken by MLEDIT gives a LINE for each piece left, caps the MLINE leaves off are not drawn, and the fill is left out. Standard through four vertices gives six lines |
| a MESH (`DxfUnknownEntity`) not smoothed | a 3DFACE for each face, in the order of its list of faces, with the MESH's look and every edge drawn: a face of four corners a 3DFACE of them, a triangle one ending on its third corner, as ezdxf's `MeshBuilder` renders them; a cube 10 by 10 by 10 of eight vertices and six faces gives six 3DFACEs. A face of more than four corners breaks into a strip of triangles, as AutoCAD 2021 breaks one: from two corners on, the rest taken by turns from the far end and the near one, each triangle after the first the other way round, from its first two corners, or from its last two back the other way where it faces into a closed MESH, whose faces AutoCAD turns to face out: the corners 0 to 4 alone, or in an open MESH, give (0, 1, 4), (2, 4, 1) and (4, 2, 3), as the base of a pyramid facing up into it (4, 3, 0), (2, 0, 3) and (0, 2, 1); 0 to 5 alone (0, 1, 5), (2, 5, 1), (5, 2, 4) and (3, 4, 2), as such a base (5, 4, 0), (3, 0, 4), (0, 3, 1) and (2, 1, 3). A face of four corners or three that faces into a closed MESH is turned too, its corners read from its last back, as AutoCAD 2021 explodes one: the square base (0, 1, 2, 3) of a pyramid facing up into it gives the 3DFACE of (3, 2, 1, 0), a side (0, 3, 4) facing in (4, 3, 0); every other face is given as the file holds it. A face given a colour of its own keeps it on its 3DFACEs. A MESH smoothed (level 1 or more), and one that gives a face a material or a transparency of its own, or an edge or a vertex anything of its own, are refused with `NotSupportedException`, saying which |
| `DxfHatch` of a pattern | the dashes of each line of its pattern, as the HATCH holds them, cut by the loops its style fills by the even-odd rule, each a LINE, a dot a LINE of no length, laid from the base point of its line, as ezdxf's `hatch_entity` draws them; an arc, an ellipse and a polyline's bulge cut exactly, a spline within each sixteenth of a span of its knots. A dot that falls on a loop where its line crosses it is kept, and a line that runs along a straight edge gives nothing there, as AutoCAD 2021 gives nothing: DOTS 1.5875 apart in a square 30 by 20 from its corner give 228 dots, none on its bottom edge. In the outer style the loops flagged external or outermost are those it is cut by, as AutoCAD and ezdxf take them, ignoring islands those flagged external; where none is flagged, by how deep each lies. A square 30 by 20 of ANSI31 gives 11 lines; round a circle of radius 5 about its middle in the normal style, 14, the three lines that cross the circle broken in two. A pattern that would give more than 2 000 000 parts, or cross its loops more than 32 000 000 times (half a gigabyte of crossings), is refused before anything is made, and so is one whose loops lie more than 2^52 of its spacings or dash lengths from its base point |
| `DxfRasterImage`, `DxfWipeout` | a LINE along each side of `GetVertices()`, as AutoCAD 2021's `Explode` gives them, each with the image's look (AutoCAD's have none of their own): the frame of an image not clipped, four LINEs from the corner it is placed at up its left side and round, and the boundary of one clipped, three for a triangle; a side of no length left out |
| `DxfOle2Frame` | four LINEs along the edges of its frame, as AutoCAD 2021's `Explode` gives four, from the upper left corner round by the upper right, each with the frame's look; a side of no length left out, so that a frame of no corners gives none, where AutoCAD 2021 gives four of no length; the object itself is not kept |

The library explodes exactly these classes. Every other entity refuses with `NotSupportedException`: those AutoCAD does not
break, a LINE or a CIRCLE, as AutoCAD answers eNotApplicable for them, and some AutoCAD does break, listed under [What the
model does not do](#what-the-model-does-not-do), among them a HATCH filled solid or with a gradient, a MESH smoothed, and a
REGION, a 3DSOLID and a BODY, which AutoCAD breaks with its solid modeler, each refused with its reason. A dimension or a table whose
drawing has no block of the name it is drawn by, and an MLINE whose drawing has no MLINESTYLE of its handle or name, throw
`InvalidOperationException`.

An INSERT inside the block that cannot hold the placing is exploded in its turn; one of a block already being exploded further
out is refused with an `ArgumentException`; and what exploding an INSERT or a MINSERT makes is counted first and held to the
reader's 2 000 000 (`DxfReadOptions.MaxPlacedEntities`), past which an `InvalidOperationException` is thrown and nothing is
given: a block of 100 INSERTs turned by 20 degrees, of a block of 100 INSERTs turned by 10 degrees, of a block of 100 lines,
inserted at twice its width, explodes into 1 000 000 lines; with 200 lines it is refused before anything is made, in 0.8 s
the first time in a process and 0.26 s after. An INSERT
that comes out of it places the block of the drawing it came from, so that it explodes in its turn, with what that block now
holds. A HATCH, a LEADER, a MULTILEADER and an ACAD_TABLE of the block are placed as the rest are; a MULTILEADER or an
ACAD_TABLE under an INSERT that scales it unlike along its axes, or shears it, is left out, as it cannot hold that placing,
while one scaled alike or mirrored is kept.

`ExplodeToOwnerSpace()`, a member of `DxfBlockReference` as of AutoCAD's `BlockReference`, appends what `Explode()` gives to
the block the INSERT is in, in the order of the block, and keeps the INSERT with its ATTRIBs, as AutoCAD does:
`insert.ExplodeToOwnerSpace(); insert.Erase();` explodes it as AutoCAD's EXPLODE does, the attributes turned back into their
definitions. Where the drawing cannot hold a part, as R12 the ELLIPSE a circle comes out as under a scale unlike along its
axes, the parts appended before it are erased again. A MINSERT gives its first cell alone, as AutoCAD 2021's
`ExplodeToOwnerSpace` gave one circle of a MINSERT of a circle in 2 rows by 3 columns; its `GeometricExtents` is that
first cell's, and its `GeometryExtentsBestFit` is refused, as AutoCAD 2021's were, while the drawing's extents take in
every cell.

**Copying.** `Clone()` copies any entity as it is, as AutoCAD's `Clone` does, a `DxfUnknownEntity` among them, an INSERT with
its attributes, and `GetTransformedCopy` copies and moves one; a copy is of the class of its original, a vertex a vertex, in
no file until it is appended. An erased entity refuses to be copied. A copy appended to the drawing it came from points at what
its original points at, and shares the block that draws it, a dimension's `*D`, a table's `*T`, an INSERT's `*U`. A copy of a
multileader keeps the attribute definitions its values are given for, and appended to another drawing gives each value to
the definition of the same tag in the block of the same name there, leaving it out where there is none; appended back to its
own drawing, it leaves out the value of a definition erased since, as ezdxf would otherwise find the definition missing. The
original keeps its handle of the erased definition, as AutoCAD leaves it.

Appended to another drawing, a copy's pointers are taken to the objects of the same name there, as AutoCAD's deep clone
between drawings maps them: a material (347), a plot style (390) or a visual style (348) by the names of its dictionary and its
entry, a record of a table by its name; a pointer to an object the drawing has no namesake of is left out, so that the
drawing's default holds, and a handle of extended data (1005) is kept in its place as null (0), as AutoCAD 2021's
`CopyObjects` keeps it, so that the groups after it keep theirs (`WblockCloneObjects` names the copy where what it names is
copied with it). The LINE of
the material Steel_mat at 2F copied into a drawing whose Steel_mat is 57 points at 57; into one with no Steel_mat it has no
material. A copy brings what it names that the drawing has not, and uses what the drawing has of the same name:

- the records of the tables, its layer, linetype, text style and dimension style, a dimension style's arrow blocks among
  them, by name, and the overrides of its dimension style (ACAD DSTYLE) naming the records of their names there;
- a block AutoCAD names itself it draws by, a dimension's `*D`, a table's `*T`, an INSERT's `*U`, under the first name of
  its letter the drawing has not (`*D2` beside a `*D1`), with every entity of it and any block of a name the drawing has not
  that it inserts;
- an INSERT's named block, under its name, with the blocks it inserts that the drawing has not and its description, units,
  explodability and scaling, as AutoCAD's `WblockCloneObjects` brings it; where the drawing has a block of the name, the
  copy places the drawing's own;
- every block it points to by a hard pointer so too, as AutoCAD's deep clone follows its pointers to them: a MULTILEADER's
  block and arrow blocks, the multileader of GKTAG copied alone into a drawing with no GKTAG bringing GKTAG and giving NUM 42
  to the copy of its attribute; and the block of an ACAD_TABLE's cell, which the table copied alone brings though its `*T`
  does not insert it.

What the entities of a block copied point to among themselves, a LEADER to its MTEXT or a HATCH to its boundary, is kept
between their copies whichever comes first in the block, each listed among the reactors of what it points to, as AutoCAD
translates its map of ids once every object is copied. A MULTILEADER of a block copied places the copy of its block, and draws
its arrows by the copies of theirs, where those blocks are copied with it, though after it: a block W holding a multileader
of the block GKTAG and an INSERT of GKTAG, copied into another drawing, brings GKTAG, and W's multileader places that GKTAG
with the value of its attribute. So does every other pointer to a block copied after the entity that holds it, whichever
of the two comes first in the block: an ACAD_TABLE points to the copies of its `*T` (343) and of the blocks of its cells
(340), and a LEADER or a dimension whose style is overridden with an arrow block (DIMLDRBLK, DIMBLK, DIMBLK1, DIMBLK2) names
the copy of that block, by its handle, or by its name in a drawing of R12 (1070 5, 1000 BOLT). A copy of an INSERT places its block as the drawing it was copied from
has it now, edits and all, keeps to it when it is renamed there, and places nothing once it is erased.

`db.WblockCloneObjects(entities, owner)` copies entities of a drawing together into a block of another drawing, or of its
own, as AutoCAD's `WblockCloneObjects` copies the objects of ids, and gives the copies in the order of the entities: each
copied as `Clone()` and `AppendEntity` copy one, and what one of them points to among the others pointing to the copy of that
other. A HATCH copied with every object its loops follow follows their copies and stays associative; a LEADER copied with its
MTEXT, TOLERANCE or INSERT points to its copy, and a handle of extended data (1005) naming one of the others names its
copy: the LINE whose data names a CIRCLE, copied with it, names the CIRCLE's copy. A LEADER copied alone into another
drawing brings its annotation with it, which it points at by a hard pointer (340), as AutoCAD 2021's `WblockCloneObjects`
brings it; copied alone into its own drawing it points to nothing (`AnnoType` `NoAnnotation`), where `Clone()` and
`AppendEntity` keep its pointer. A HATCH copied without every object it follows follows none. No GROUP is copied, as
AutoCAD 2021's `WblockCloneObjects` and COPYCLIP copy none; `db.Insert` copies a drawing's groups with it, each all of
whose entities come into model space under its own name, or that name and `_1`, `_2` and on where the dictionary has it, an
anonymous one as the next `*A1`. Every copy is checked before any is added, so that a set the drawing cannot hold, a SPLINE for a drawing of
R12, changes nothing. A set whose blocks would come to insert themselves once written is refused whole too, with an
`ArgumentException`: a drawing whose block P holds an INSERT of M, a block it has not, as a drawing read from a file may,
refuses an INSERT of another drawing's M that inserts P, which would bring an M placing P and a P placing that M without
end. So are blocks of the other drawing that insert one another, and blocks brought that would come back to the block the
copies are added to; and a copy appended alone by `AppendEntity` is refused alike. A block AutoCAD names itself that a copy
brings takes no name an INSERT, a dimension or a table of this drawing gives of a block it has not, nor one an INSERT in a
block the set brings gives of a block neither drawing has: a DIMENSION here naming a `*D1` the drawing has not leaves the
copy of another's `*D1` to come as `*D2`, rather than drawing by it. An INSERT in a block brought that names a block its
drawing has not is copied as it is, naming it, and the set says how many there were, and their names, in one Debug line of
`GeometryHelperLog`; one such given among the entities is refused, as AutoCAD 2021's `WblockCloneObjects` refuses a set
holding or reaching one (`eWasErased`). A rectangle of four LINEs with a HATCH that follows them, copied together into another drawing, comes
out as four LINEs and a HATCH that follows those four, each LINE listing the HATCH among its reactors. A block AutoCAD
names itself that several of the entities draw by is copied once for the call, as AutoCAD's map of ids keeps one: two
INSERTs of one `*U1` copied together place one copy; copied by two calls, they place a copy each, as two calls of
AutoCAD's do.

A dynamic INSERT copied into another drawing of R2010 or later stays dynamic, as `db.Insert` keeps it: the dynamic block
it places comes with it, or the one its anonymous block stands for where it places a `*U`, which AutoCAD's deep clone brings
by the pointer of the INSERT's representation; then what makes them dynamic, as `db.Insert` copies it. An INSERT of
GH_PANEL set to a Width of 200, copied alone into a new drawing, brings `*U1` and GH_PANEL, reads Width 200 and is set again
as the one it was copied from. A drawing that has a block of the name keeps its own, as for any block: the copies stand for
it where it is dynamic, and are static otherwise; and a drawing older than R2010 takes a static copy of the INSERT's block
alone, as before. Within `db.Insert` the drawing's blocks are merged first, so it brings nothing more. Copied one by one,
4 000 INSERTs of GH_PANEL of which 2 000 are set take about 0.6 seconds under .NET 10 and 1.1 under .NET Framework 4.8.

What a copy by `Clone()` and `AppendEntity` leaves behind: a copy of an associative HATCH, in its own drawing or another,
leaves its boundary objects, each path counting the pointers it keeps (97 of 0 where it keeps none), and is no longer
associative (71 of 0), as AutoCAD's COPY leaves it; a LEADER appended to another drawing points to nothing (`AnnoType`
`NoAnnotation`), its annotation not coming with it; a copy of an ACAD_TABLE for another drawing keeps the values
its block cells give the attributes of a block that comes with it, each pointer (331) naming the copy of its attribute
definition, and loses those of a block the drawing has of its own, each pointer left out with its value (300) and the cell's
count (179) made to agree; a copy of the first column of an MTEXT of linked columns holds the words of them all and links to
none.

**Versions.** An entity of a file of R12, or a copy of one (`GetTransformedCopy`, `GetOffsetCurves`, `GetSplitCurves`),
appended to a drawing of R13 or later is written in the forms AutoCAD writes there: its subclass markers in AutoCAD's order,
the marker AcDbEntity, its owner and a handle of its own. One of R13 or later appended to a drawing of R12 is written in the
forms of R12, as AutoCAD saves a drawing as R12: each record keeps the groups R12 has for its type, each read from the part of
the record it belongs to, so that the centred ATTRIB whose AcDbAttribute part writes 71 and 72 again keeps its justification,
and loses its markers, owner, lineweight, material and plot style, and an object embedded after its 101, as a multi-line
attribute's, which R12 reads as one line; an MTEXT is written as TEXTs, as Appending says. A DIMENSION is written in the forms
of R12 into a drawing of R12, its block with it, where every entity of the block has a form of R12, an MTEXT of the block
written as the TEXTs of its lines, so that a dimension of R2018 whose picture holds its text in an MTEXT goes into R12 with
its picture. A text style an entity copied between drawings of R12 and of R13 or later names is copied by name, as a layer is,
and the dimension style of a dimension, a leader or a tolerance copied into a drawing of R12 is written in the form of R12:
its name, flags and the variables R12 has, its texts (3, 4) and its arrow blocks by name (5, 6, 7) after its flags, a block
the drawing has not and a text it has none of written empty, as AutoCAD 2021 asks for all five ("Omitted group 5"), its text
style and the variables of later versions left out, said in the log. A dimension copied into a drawing before R2010 is
written without its version (280 of AcDbDimension), which R2010 brought and AutoCAD 2021 refuses in a DIMENSION of R2000
("Unexpected DXF group code: 280").
A HATCH copied into a drawing of another version is written as that version writes it, the count of fit points of a spline
edge as the version has it and a gradient dropped before R2004, as AutoCAD's SAVEAS drops it.

What a drawing of an older version has no room for is left out of what is copied into it, as AutoCAD's SAVEAS to that version
leaves it out, and said in the log at Debug: an override of a dimension style (ACAD DSTYLE) of a variable the version has not,
as DIMLTYPE and DIMFXLEN of R2007 on a dimension copied or made in code for a drawing of R2000, or DIMDEC of R2000 for one of
R12; and a group of a dimension style or a layer copied by name with an entity that the version has not, as a DIMSTYLE's
DIMFXLEN (49), DIMJOGANG (50), DIMTFILL (69), DIMTFILLCLR (its second 70), DIMFXLENON (290), linetypes (345 to 347) and
DIMTXTDIRECTION (294), and a LAYER's true colour and colour book (420, 430) of R2004 and material (347, 348) of R2007, in a
drawing of R2000; a LAYER's plotting, lineweight and plot style (290, 370, 390) of R2000 go too in a drawing of R13 or R14,
the versions taken as ezdxf gives them. In a drawing of R12 an override of an arrow block
(DIMBLK, DIMBLK1, DIMBLK2) names the block by its name, as R12 writes it, where the drawing has a block of the name, and is left
out, said in the log, where it has none. A record set on a dimension in no drawing for a variable the version has not is
refused.

**Memory.** A copy in no file, and an entity made in code, holds its own records alone: one file holds every copy of a
drawing and keeps none of them, about 460 bytes a copy, so that the explode of an INSERT of 1 000 000 lines holds 440 MB.

## What the model does not do

- It draws nothing anew. The block that draws a dimension (`*D`) or a table (`*T`) is left as it was when a point, a text, a
  cell or a variable of its style changes, for AutoCAD to draw anew; until AutoCAD does, it shows the old picture. AutoCAD 2021 draws a
  dimension's block anew only when it is edited, not on opening or REGEN; a table's it draws anew on opening. A dimension's
  `Measurement` is worked out from its points all the same.
- Without `FontSearchPath` (see Fonts) it has no fonts. A text's `GeometricExtents` is the point it stands at; an MTEXT's `ActualWidth` and `ActualHeight` are what
  the file says, nought where it says nothing; where the words of an MTEXT wrap, and the box of a fitted or aligned TEXT, are
  AutoCAD's to work out, and so is where it wraps an MTEXT exploded or written into R12 as TEXTs.
- Without `PatternSearchPath` (see Hatches) it has no pattern file, and carries none of AutoCAD's. A pattern of AutoCAD's
  acad.pat the HATCH does not hold the lines of, ANSI31 given by `SetHatchPattern` among them, holds no lines
  (`NumberOfPatternDefinitions` 0), its name written for AutoCAD to take the lines from its own file, and `Explode()` and
  `GetHatchLinesData()` refuse it. AutoCAD 2021 draws none of those lines either, even after REGEN, until the hatch is
  edited: give the drawing the folder of the pattern files, or a pattern of the user's own (`_USER`), where the lines must
  show. A leader drawn anew (`EvaluateLeader`),
  where a multileader's leaders meet its words, and the size of those words in its `GeometricExtents` are not worked out: a
  leader added on the left of words held at their top left is landed as AutoCAD lands it, but one on their right, which
  AutoCAD lands as far right as its font makes the words wide, starts its dogleg at the base point of the content (10 of
  the context data), as does one of a block, at the origin where the block is placed after its leader line is added; and
  a gap, dogleg, height or scale set afterwards leaves the words where they are, where AutoCAD moves them to keep the
  landing.
- It evaluates only the dynamic blocks it can write. `DynamicBlockReferencePropertyCollection` reads the values AutoCAD
  saved of every parameter but the angular and diametric constraint parameters and user parameters other than of a text
  or a real, which give no properties; the horizontal, vertical, aligned and radial constraint parameters give their
  distances, set but for an aligned one's by solving the block's constraints (check D27).
  - Written and set: linear parameters with stretch, move, scale and array actions; XY parameters with stretch, move and
    array actions; polar parameters with polar stretch, stretch and move actions; point parameters with move actions;
    rotation parameters with rotate actions; flip parameters with flip actions; one visibility parameter; lookup parameters
    with their tables, of the distances of linear, XY and polar parameters, the x and y of point parameters and the angles
    of polar and rotation parameters; one block properties table of those columns and of attribute definitions, set by
    its row, its user parameters set where AutoCAD wrote them;
    and one alignment and one base point parameter, which give no property. Linear, XY, polar and rotation parameters take
    value sets. A stretch holds lines and polylines by their points, arcs by both ends or, lying flat, by one, and circles,
    texts, multiline texts and INSERTs by their one point, in a frame of a rectangle; a stretch and a move take a distance
    multiplier and an angle offset. A polar stretch holds them so but an arc by one end, and turns other entities only. A
    stretch, a move, a scale and a rotate take attribute definitions locked in their block, the INSERTs' ATTRIBs following
    (check D30), and so does a polar stretch, to stretch or to turn only: AutoCAD 2021 turned and moved such an ATTDEF
    and its ATTRIB, and turned one it turned only, as the library does within 3E-13, on INSERTs plain, turned a quarter at
    the scale 2 and mirrored, and left one not locked where it was (check D34).
  - Refused when written, with `NotSupportedException`, as AutoCAD 2021 worked them out from where an INSERT's last set left
    it rather than from its values: a flip parameter beside any other parameter (checks v20b and v21; AutoCAD 2021 read
    Hand back as 0 again on the library's door of a flip beside a Width, stretching or not, check D30), and a rotate or a
    scale of an entity a stretch or a move holds (check v21). An entity both copied by an array, or turned by a polar
    stretch, and worked on by another action is refused too, an order no sample of AutoCAD shows.
  - Not written: a frame of a polygon; a text aligned or fitted between two points and an INSERT of rows and columns in a
    stretch; an attribute definition not locked in its block in any action, or in a flip or an array (AutoCAD 2021 refused
    to set an INSERT of an array holding one, eInvalidInput, and placed one a flip held by its text's extents in its font,
    which the library does not measure, check D34); an arc held by one end in a polar stretch; a polar stretch of a distance multiplier or
    an angle offset, or tied to the base grip; a stretch or a move tied to a base grip and a move carrying other
    parameters, which are set where AutoCAD's Block Editor wrote them; a user parameter, a
    constraint, and a block properties table whose properties must match a row, or of columns of a point, a lookup or a
    user parameter (AutoCAD's `AddItem` refused the first two), or of empty cells of a parameter or values of no match.
  - Set where another writer wrote them: stretches and moves tied to a parameter's base grip and moves carrying other
    linear parameters; attribute definitions not locked in their block, which a move, a scale and a rotate leave where they
    are and a stretch moves without their ATTRIBs, as AutoCAD 2021 did (check D30); a polar stretch naming an entity to
    turn only in its selection too, or its parameter's grip; horizontal, vertical and radial constraint parameters, by
    solving the block's constraints, and a flip carrying them (check D27). The network is copied into the anonymous block
    at the values set, from which AutoCAD 2021 set such an INSERT again as from its own (check D27b).
  - Not set, with `NotSupportedException`, nothing written: an INSERT of a block of another writer holding what is not
    written above or refused (an aligned, angular or diametric constraint parameter; a constraint other than those of
    AutoCAD's samples, geometry other than lines, arcs, circles, polyline edges and construction lines flat in the block,
    an expression of another function, or a value the constraints cannot meet; a row of a table that would change a
    constraint parameter; a polar stretch tied to the base grip or of a multiplier or an offset, a move
    carrying a parameter other than a linear one or one that chains its actions, any other action holding a parameter,
    an attribute definition in a flip or an array, a lookup column of another kind than those written above, as
    a visibility state's, a block properties table column of another kind, or a lookup or a user parameter of a block
    whose table its properties must match, or a parameter of such a table of a column of a user parameter or of the
    visibility state), and an INSERT that keeps the base point of a linear, an XY, a polar, a
    rotation or a flip parameter away from the block's own, as the moves carrying it leave it, as a grip moved that of
    AutoCAD's XY sample, which AutoCAD draws from there.

  A block whose entities another program changed keeps the values AutoCAD saved. `ConvertToStaticBlock` copies what the
  INSERT draws as it stands; `ResetBlock` is not there. `db.Insert` of another drawing keeps a dynamic block dynamic, as
  AutoCAD's own insert does, but `WblockCloneObjects` of an INSERT alone brings its block as a static copy.
- It resolves no external reference: `AttachXref` writes the block that names the drawing, and reading that drawing into it
  is AutoCAD's work when it opens this one, so an INSERT of it places nothing here and `XrefStatus` is never `Unloaded`.
- It does not explode every entity AutoCAD explodes. `Explode()` refuses with `NotSupportedException` a HATCH filled solid
  or with a gradient, whose parts AutoCAD's EXPLODE makes and the library does not work out; a MESH smoothed, which AutoCAD
  breaks into the 3DFACEs of the faces its smoothing makes, and one whose faces have a material or a transparency of their
  own; a REGION, a 3DSOLID, a BODY and a surface, which AutoCAD breaks with its solid modeler (ACIS);
  and every other type the library keeps as a `DxfUnknownEntity` but the MLINE and the MESH. It gives a dimension's and a
  table's parts as their last picture, not as their data now, and AutoCAD 2021 draws a table anew before it explodes it;
  and it leaves out the vertex AutoCAD adds to an exploded leader where the words of its annotation end, and the lines
  under or over a multileader's words, whose length the width of the words decides.
- It makes no drawing from nothing but `DxfWriter`'s, of R2000 or of a later version named (`new DxfDatabase("AC1024")`),
  reads no DWG and no binary DXF, and saves a drawing in its own version only.
- It has none of AutoCAD's members that need AutoCAD running: `ObjectId`, transactions, documents, drawing to the screen
  (`Draw`, `Highlight`), grips and object snaps, overrules, events and undo.

What is still to be checked in AutoCAD itself is listed in [Checking against AutoCAD](../dxf-autocad-checks.md), and a whole
session of editing, run against the library, is in [Editing a drawing, step by step](../dxf-editing-example.md).
