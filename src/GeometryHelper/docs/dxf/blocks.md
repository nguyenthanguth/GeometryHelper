# Blocks and attributes

An INSERT places a block, and its attributes are the texts it carries. This page has INSERTs, attributes, MINSERTs
and making an INSERT static; dynamic blocks have [pages of their own](dynamic-blocks.md).

## INSERTs

An INSERT is a [`DxfBlockReference`](xref:GeometryHelper.Dxf.DxfBlockReference). The INSERT of BOLT at (50, 60, 0) turned by 45 degrees and scaled by (1.5, -1.5, 2) has
`Position` (50, 60, 0), `Rotation` 0.785 and a `BlockTransform` that takes BOLT's origin (1, 2, 0) to the insertion point;
the block's point (0, 0, 0) lands at (46.82, 61.06, 0). Setting `Position` to (60, 55, 1) moves its ATTRIB MARK from
(50, 59, 0) to (60, 54, 1): an INSERT moved, turned, scaled or given a new normal carries its attributes with it. `Name` is
the block it places, `BlockTableRecord` that block; `BlockName` is the block it is in.
`new DxfBlockReference(position, block)` places a block of the drawing.

## Attributes

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

## Clipping an INSERT

An INSERT, a MINSERT and an INSERT of an external reference are clipped as AutoCAD 2021's XCLIP clips them.
`insert.ClipTo(points, worldToClip)` writes a [`DxfSpatialFilter`](xref:GeometryHelper.Dxf.DxfSpatialFilter), a SPATIAL_FILTER
named SPATIAL in the dictionary ACAD_FILTER of the INSERT's extension dictionary, both made as AutoCAD makes them (owned hard,
the filter owned by its dictionary, its class listed and counted in CLASSES). `points` is the boundary in the clip coordinate
system, the UCS that is current: two points are a rectangle's opposite corners, three or more a polygon, and `worldToClip`
carries a point of the world into it (`GeoTransform3.Identity` for the world's). The filter keeps, as AutoCAD does, the boundary
as picked, the matrix from the world into the clip system, the inverse of the INSERT's placing as it stood (a block 100 by 100
inserted at (1000, 0) clipped to (1010, 10) and (1090, 90) keeps the shift by -1000) and the clip system's origin and normal;
a clip is kept against the block, so that the INSERT moved, turned, scaled or copied shows the same part of it.
`insert.ClipOutline` gives the boundary as a ring in the block's own coordinates, a rectangle as its four corners: a block
whose origin is (10, 20) clipped to the rectangle that covers its lower left corner 40 by 30 is (10, 20), (50, 20), (50, 50),
(10, 50), whatever the INSERT is moved to. `insert.SpatialFilter` reads the filter, null for an INSERT not clipped: its
`Points`, `Normal` and `Origin`, the matrices `InverseBlockTransform` and `WorldToClip` (twelve numbers each, row by row), and
`IsEnabled`, `FrontClip` and `BackClip`, which are set (the clip off, the planes at a distance from the boundary or none).
`insert.RemoveClip()` takes the filter off, and the dictionary ACAD_FILTER with it where that holds nothing else, leaving the
extension dictionary as AutoCAD leaves it. A clip turned inside out is kept as a polygon with a slit round the block's
extents, as AutoCAD keeps it: [`CadXClips.Inverted`](xref:GeometryHelper.Dxf.Viewer.CadXClips) of the presenter makes it. A
filter is read from and written to a DWG of R2000 and later with the rest of the objects, and AutoCAD 2021 reads both, its AUDIT
finding nothing. A boundary in a plane tilted to the drawing's is kept whole, and the presenter draws its projection on the drawing's plane.

## Units and extents

`UnitFactor` takes the block's units to the drawing's (`$INSUNITS`): 25.4 for a block in inches inserted in millimetres, 1
where either gives none. `GeometryExtentsBestFit()` is the box of the shapes placed, and
`GeometryExtentsBestFit(parentTransform)` the same after an outer INSERT's `BlockTransform`; a MINSERT has none
(`InvalidOperationException`, as AutoCAD 2021 threw eInvalidExtents), its `GeometricExtents` its first cell's, and its
`TreatAsBlockRefForExplode` false. A MINSERT is one by its subclass AcDbMInsertBlock, of one cell too, or by its counts in
R12.

## Making an INSERT static

`insert.ConvertToStaticBlock("BOLT_500")` makes the INSERT place a
new block BOLT_500 holding copies of what it draws, the anonymous block's entities (or the dynamic block's, where it places
that), its tie to the dynamic block (the entry AcDbBlockRepresentation of its extension dictionary) taken out;
`ConvertToStaticBlock()` names the copy as AutoCAD names an anonymous block, `*U1` the first the drawing has not, and no
INSERT of it names: a drawing whose PPP holds an INSERT of `*U1`, a block it has not, gets `*U2`. A name given that an
INSERT of the drawing names, which would then place the new block, is refused where that block would come to insert itself
through it, PPP holding an INSERT of MM and the block made static an INSERT of PPP; nothing is written then. Exploding an
INSERT is set out under [Editing rules](rules.md#editing-rules).

## See also

- [Dynamic blocks](dynamic-blocks.md): parameters, actions and their values on an INSERT.
- [Editing rules and what the model does not do](rules.md): how an INSERT moves and explodes.
- [The drawing, its tables and layouts](drawing.md): the block table.
