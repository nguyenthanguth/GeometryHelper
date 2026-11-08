# Dimensions, marks and symbols

What a view is annotated with, and the rules that decide it: dimensions chosen from the points of the model, placed in tiers
so that none overlaps another; marks placed clear of the dimensions and of one another, each with a leader to its part;
centre lines, weld symbols, section, detail and level marks. Everything is in view millimetres at 1:1, with sizes on paper
turned into view millimetres by the scale: at 1:20 a text 2.5 high on paper is 50 high in the view.

## Dimensions

### The points a view can be measured to

[`Annotator.FindPoints(view, objects, frame, tolerance)`](xref:GeometryHelper.Drawing.Annotation.Annotator) lists what a view
can be dimensioned to, each a [`DimensionPoint`](xref:GeometryHelper.Drawing.Annotation.DimensionPoint) with the object and
the feature it comes from, as a [`DimensionPointKind`](xref:GeometryHelper.Drawing.Annotation.DimensionPointKind):

| Kind | Is |
|---|---|
| `Corner` | the end of a straight line of the view where a corner of the object's body falls; the corner nearest the viewer is its model point |
| `HoleCentre` | the centre of a whole circle of a part (its arcs about one centre adding up to a full turn), or a hole object's axis; its model point on the rim nearest the viewer |
| `BoltCentre` | the middle of a bolt's axis, half way along the bolt: a point seen along the bolt, a point on its centre line seen across |
| `PartEnd` | an end of a part's reference line: the origin of its frame, and the point as far along its X as the body reaches |
| `WorkPoint` | an end of the reference line of the main part, which the ordinate rule measures from |

A plate 300 by 200 by 20 from the origin with four holes of 22 at 50 from its edges, seen from above, has the corners (0, 0) to
(300, 200), the hole centres (50, 50), (250, 50), (50, 150) and (250, 150), their model points 20 up, and the work points
(0, 0) and (300, 0). The front view of the beam of the first look has 24 points: 20 corners, 2 part ends and 2 work points.

### The rules

[`Annotator.Dimension(view, objects, frame, settings, tolerance)`](xref:GeometryHelper.Drawing.Annotation.Annotator) makes
the dimensions the [`DimensionSettings`](xref:GeometryHelper.Drawing.Annotation.DimensionSettings) switch on:

```csharp
IReadOnlyList<DrawingDimension> dims = Annotator.Dimension(
    front.Lines, source.Objects, source.Frame, new DimensionSettings { Scale = 10 }, Tolerance.Default);
// D-1 Overall '3000', D-2 Overall '290'
```

| Rule ([`DimensionKind`](xref:GeometryHelper.Drawing.Annotation.DimensionKind)) | Gives | Switch, default |
|---|---|---|
| `Overall` | from the main part's one end to its other, outermost; along X and along Y | `Overall`, on |
| `Chain` | end to end through the ends of the main part and the centres of every hole and bolt; equal neighbours merged into "n x p = total" | `Chain`, on; `MergeEqual`, on |
| `HoleGroup` | the pitches of a row of holes or bolts of a part, equal ones merged into "n x p = total": four holes 100 apart read `3 x 100 = 300` | `HolesByGroup`, on |
| `HoleEdge` | from the part's end to the first hole of a row, and from the last to the other end | `HolesByGroup`, on |
| `Cumulative` | from the main part's first end to each point, as ordinate dimensions on one tier: 0, 100, 2 900, 3 000 | `Cumulative`, off |
| `Ordinate` | from the main part's work point to each point | `Ordinate`, off |

The main part is the object marked `IsMain`, or every part where none is. The rules work along X and along Y alike. A dimension
is never given twice: one that measures between the same places along the same axis as one already made, to the point
tolerance, is left out, the overall before the chain, the chain before the holes.

The `Part` preset on the plate above, whose front view is at 1:2 on an A3, gives along X the overall 300 and the chain 50,
200, 50, and along Y the overall 200 and the chain 50, 100, 50: eight dimensions with the ids `V-FRONT/D-Overall-X-1`,
`V-FRONT/D-Chain-X-1` to `-3`, `V-FRONT/D-Overall-Y-1` and `V-FRONT/D-Chain-Y-1` to `-3`. The beam of the first look has none
in its right view; `ReDimension("V-RIGHT")` gives it ten, among them the chain through the plate's holes and `V-RIGHT/D-HoleEdge-Y-1`.

A [`DrawingDimension`](xref:GeometryHelper.Drawing.Annotation.DrawingDimension) holds its `Kind`, its `Points`, the `Links` that
tie it to the objects of the model, its `Value`, its `Text`, where it is drawn (`Direction`, `Tier`, `Offset`,
`DimensionLine`, `ExtensionLines`, `TextBox`) and the sizes it was made with. The value is worked out from the model points along the
axis of the view, so it is right to the bit of the model whatever the lines were rounded to, and every digit is kept (the chain
above holds 200.00000000000003); the text is rounded as `Rounding` says, to 1 by default, and reads 200.

### The settings

| `DimensionSettings` | Default | |
|---|---|---|
| `Scale` | 1 | the scale of the view: 20 for 1:20; a drawing sets it to each view's |
| `TextHeight` | 2.5 | on paper, millimetres |
| `TierSpacing` | 8 | on paper: the first tier stands this far off the box of the view's lines and each next one this far further out |
| `Rounding` | 1 | what the text is rounded to, in model millimetres: 1 writes 2 800.4 as 2800, 0.5 as 2800.5 |

The arrows are as long as the text is high, the text stands a quarter of its height off its line, the extension lines start a
quarter of it off their points and run on half of it past their line, and each character of a text is taken as 0.8 of its height
wide when the layout keeps texts apart.

### Placing

Placing is greedy, axis by axis, in the order chain, overall, holes, cumulative, ordinate: each dimension takes the nearest
tier where nothing it draws overlaps what is already placed, as `FindOverlaps` counts it, the overall beyond every tier on its
side and what comes after it inside it or on the other side. A dimension that finds no place on either side is left out and
named in one Debug line of [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog).

A dimension is written as a real DIMENSION: the linear ones as rotated dimensions along the axes of the view, the cumulative
and ordinate ones as ordinate dimensions, in a dimension style of the scale (`GH_MODEL_10` at 1:10). The dimension of the beam's
overall length measures 3000 and its text is `<>`, so that AutoCAD writes the measurement, and a user can move it in CAD and the
value stays.

## Marks

A [`DrawingMark`](xref:GeometryHelper.Drawing.Annotation.DrawingMark) is the text that names an object in one view, with the
box it stands in and a leader to a point on the object's visible outline. Its
[`MarkKind`](xref:GeometryHelper.Drawing.Annotation.MarkKind) is `Part` (a part that is not the main part: an end plate, a
stiffener), `Assembly` (the main part, which carries the assembly's mark) or `Bolt` (a group of bolts, marked once).

[`Marker.Mark(view, objects, frame, dimensions, settings, content, scale, tolerance)`](xref:GeometryHelper.Drawing.Annotation.Marker)
places them:

```csharp
var marks = Marker.Mark(front.Lines, source.Objects, source.Frame, dims,
    new MarkSettings { MainParts = true, Fittings = true }, new MarkContent(), 10, Tolerance.Default);
// M-B1 'B1' Assembly, M-P1 'P1' Part; Marker.FindOverlaps(marks, dims, tolerance) is empty
```

| [`MarkSettings`](xref:GeometryHelper.Drawing.MarkSettings) | Default | |
|---|---|---|
| `MainParts` | true | whether the main part of each assembly is marked |
| `Fittings` | true | whether the parts that are not main parts are marked |
| `TextHeight` | 3.5 | on paper |
| `Offset` | 4 | how far the text stands off its object, on paper: at 1:20 a text 3.5 high is 70 high and stands 80 off |
| `Content` | see below | what each kind of mark shows |

The presets differ in these: `Part` marks the main part and not the fittings, `Assembly` the fittings and not the main part
(the beam of the first look has one mark, `P1` on its end plate), `General` the main part of each assembly. The first view
along the axes is always marked, and so are the views [`ViewSettings`](xref:GeometryHelper.Drawing.ViewSettings)`.MarkedViews` names (`ReMark("V-TOP")` adds one).

### What a mark says

A [`MarkContent`](xref:GeometryHelper.Drawing.Annotation.MarkContent) has a [`MarkFormat`](xref:GeometryHelper.Drawing.Annotation.MarkFormat)
for each kind: a list of [`MarkField`](xref:GeometryHelper.Drawing.Annotation.MarkField)s and the text between two of them. By
default a part mark shows the position mark, `P1`; an assembly mark the position mark of its main part, `C1`; a bolt mark the count,
the size and the length of its group joined by " x ", `4 x M20 x 80`. A field with nothing to show is left out with its separator.

```csharp
settings.Marks.Content.Part = new MarkFormat(new[] { MarkField.Mark, MarkField.Profile }, " ");    // "P1 PL20*300"
settings.Marks.Content.Bolt = new MarkFormat(new MarkField[0], " ");                               // no bolt marks
```

| [`MarkFieldKind`](xref:GeometryHelper.Drawing.Annotation.MarkFieldKind) | Shows |
|---|---|
| `Mark`, `Name`, `Profile`, `Material` | [`DrawingObject`](xref:GeometryHelper.Drawing.DrawingObject)`.Mark`, `Name`, `Profile`, `Material`; an assembly mark shows its main part's |
| `Length` | the attribute `Length`, as written; else how far the body reaches along its frame's X, 300 for the plate and 80 for a bolt 80 long |
| `Count` | for a bolt mark the bolts of the group, 4 for a base plate's four; for a part the parts of one position mark in the view |
| `Diameter` | `M` and the attribute `Diameter`; else twice the furthest a corner stands off the axis, to a tenth: `M20` |
| `Attribute` | the value of the named attribute (`MarkField.AttributeName`) |

### Where it goes

Marks are placed through [`Arranger`](xref:GeometryHelper.Arranging.Arranger) in rounds, until every mark is clear of the view's
dimensions and of one another, and, where it can be, of the view's lines. The boxes of symbols and the lines of the centre lines
and cut lines are kept clear of as the dimensions are, so that a mark that finds no place clear of them is left out rather than laid
over them: the mark P1 of a plate seen on its face at 1:2, with the section A-A cut across its middle, stands beside the plate
clear of the cut line and its letters. A bolt group gets one mark, `4 x M20 x 80`, its leader ending at the centre of one of the bolts.

The dimensions' texts, lines, extension lines and arrows are kept clear of, each arrow taken as the box round it, as long as the dimension's text is high and a third of that wide, inside its line or outside a short one. A mark the rounds leave out is tried once more with its text alone, not its margin, kept clear of the centre lines and cut lines: in the general arrangement of 1 000 members at 1:200, 191 of 200 marks find a place, and the 9 bolt marks left out are named in the Debug line.

Each mark is an element of the drawing document, known by its view's id, a slash and its own: `V-FRONT/M-P1`, and a mark dragged
by hand keeps the move as a hand edit (see [Making it again](update.md#hand-edits)).

## Symbols

Centre lines go on the layer Centre, in the linetype CENTER; weld symbols and the section, detail and level marks on the layer
Symbol. Their sizes follow [`SymbolSettings`](xref:GeometryHelper.Drawing.Annotation.SymbolSettings)`.TextHeight`, 2.5 on paper, and the view's scale.

**Centre lines.** A [`DrawingCentreLine`](xref:GeometryHelper.Drawing.Annotation.DrawingCentreLine) crosses a hole, a bolt or a tube
seen as a circle, or runs through the centres of a row of them, past the rims by `CentreLineExtension` (3 on paper). A hole on its own
gets a cross; holes of one part whose centres line up share one line from the first rim to the last. The plate, 300 by 200 with
holes of 22 at 50 from its edges, seen from above at 1:5, gets two level lines from x = 24 to 276 and two upright ones from
y = 24 to 176, the holes 11 in radius and the lines running 15 past them. The circles are those of the view's lines, seen or
hidden, whose arcs about one centre add up to a full turn. Parts are taken first, then hole objects, then bolts, and a circle on
the centre of one taken already, as the bolt in a plate's hole, gets none of its own. The beam of the first look has four in its
right view. Ids are `CL-1`, `CL-2` and on.

**Weld symbols.** A [`WeldSymbol`](xref:GeometryHelper.Drawing.Annotation.WeldSymbol) is drawn to ISO 2553 for a weld object (see
[the attributes](source.md#attributes-the-drawing-reads)): an arrow to the weld, the reference line, the basic symbol of the weld's
[`WeldType`](xref:GeometryHelper.Drawing.Annotation.WeldType) on the side it is on, its size to the left of the symbol and its length to
the right. A weld on the arrow side stands on the continuous line, on the other side it hangs from a dashed one, on both sides a symbol
each side of the continuous line. A fillet weld `a5`, 200 long, on the arrow side, with text 2.5 high at 1:10, has its knee 50 up and
50 to the right of the weld's middle and the triangle 25 high. A kind or side not known gets no symbol and is named in one Debug line.

That is where the rule puts a symbol, and where it is tried first. A drawing places the weld symbols once the other symbols are
known, and `WeldSymbol.Find`, given the view's dimensions and the boxes and lines to keep clear of, moves a symbol off that place
where something is in the way. Its knee is tried up and right of the weld, up and left, down and right, down and left, then two
heights off and further, the nearest place first, until the box of the symbol, with 0.4 of the text's height round it, is clear of the
dimensions (their texts, lines, arrows and extension lines), the centre lines, the section, detail and level marks, and the symbols placed before it, and its arrow crosses no
text. An arrow may cross a dimension line, an extension line or a centre line, as a draughtsman lets one cross. A symbol left of
its weld has its reference line run to the left, its texts still reading from left to right; the arrow always ends at the weld's
middle, and the marks of the view are placed after the symbols and keep clear of them. A symbol that finds no place clear within
thirty heights of the text is left where the rule puts it and named in one Debug line. The fillet `a6` where the flange of a
column meets its base plate, seen from the front at 1:10, would stand over a dimension line and a dimension's text at the rule's
place; it is put down and to the left of its weld instead, under the base plate. `WeldSymbol.FindOverlaps(symbols, dimensions,
clearOfBoxes, clearOfLines, tolerance)` lists in words what overlaps: a symbol's box over a dimension's text, line or extension
line, over a box or a line, or over another symbol's box, and an arrow across a text or a box. Asking for [`WeldStandard.AwsA24`](xref:GeometryHelper.Drawing.Annotation.WeldStandard) throws `NotSupportedException`.

**Section and detail marks.** A [`SectionMark`](xref:GeometryHelper.Drawing.Annotation.SectionMark) is the cut line with an arrow at
each end pointing the way the section looks and its letter by each arrow: the section across a beam 3 000 long at x = 1 500, drawn
at 1:20, has an arrow 100 long, and the box of each letter starts 20 past its tip. It stands in the first view along the axes that
sees the section's plane edge-on. A [`DetailMark`](xref:GeometryHelper.Drawing.Annotation.DetailMark) is a circle or a rectangle
([`DetailMarkShape`](xref:GeometryHelper.Drawing.Annotation.DetailMarkShape)) round the area the detail shows, in the view it is taken from, with the letter at its upper right.

**Level marks.** With `SymbolSettings.LevelMarks` on, as in the general arrangement preset, each object that gives the attribute
`Elevation` is marked with a [`LevelMark`](xref:GeometryHelper.Drawing.Annotation.LevelMark): a triangle standing on its point at the
level, a line along its top running on to the right, and the elevation in whole millimetres with its sign, `+25`, `±0` and `-150`. It
stands just right of the upper right corner of the object's box, in every view along the axes whose up is the model's Z, as a front or
an end elevation; a plan has none. A general arrangement of 1 000 columns, each with its `Elevation`, has 1 000.

## See also

- [Views, projection, sections and details](views.md): the lines these are made from.
- [Settings, scale and sheet](settings.md): where each setting above sits in `DrawingSettings`.
- [Label placement](../arrange.md): the `Arranger` the marks are placed through.
- [Hatches, leaders and dimensions](../dxf/annotation.md): the DIMENSION entities a dimension is written as.
