# Views, projection, sections and details

A view is a window into the model, as a Tekla view or AutoCAD's MVIEW is: a direction to look in, a depth it sees, a crop
and a scale. This page says where each view looks from, how the lines of a view are found with the hidden ones left out,
and how sections and details are cut. The examples are the beam B1 and its end plate P1 of
[Sources](source.md#a-source-in-code), and boxes of 300 by 200 by 20 where one is enough.

## Where a view looks from

A [`ViewFrame3`](xref:GeometryHelper.Drawing.ViewFrame3) is a coordinate system in the model whose X runs to the right of
the view, Y up it and Z towards the viewer, its XY plane the cut plane, and how far in front of that plane and behind it the
view sees. It is right-handed, so nothing in a view comes out mirrored.

```csharp
var frame = new ViewFrame3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis,
    double.PositiveInfinity, double.PositiveInfinity, Tolerance.Default);   // origin, direction, up, depths, tolerance
GeoPoint2 at = frame.ToView(new GeoPoint3(30, 20, 10), out double depth);   // (30, 10), depth -20
```

Looking along the model's Y with Z up, a box 30 by 20 by 10 has its corner (30, 20, 10) at (30, 10) in the view, 20 behind
the cut plane through the origin. `depth` is positive towards the viewer. The depths are what the view takes in:
`DepthInFront` towards the viewer, `DepthBehind` away from it, both infinite for a view along an axis. A section sees nothing
in front of its cut and a depth behind it: a frame of depths 0 and 100 has `IsWithinDepth(-50, tolerance)` true and
`IsWithinDepth(1, tolerance)` false. The constructor refuses an origin that is not a point of numbers, a direction or an up with
no length, an up along the direction, and a negative depth.

### The standard views

[`StandardViews.GetFrame(drawingFrame, kind)`](xref:GeometryHelper.Drawing.StandardViews) gives the frame of a view along the
axes of a drawing's frame, or its isometric view, its origin at the frame's, seeing everything. Each view is the object as seen
from its side, never a mirror image.

| [`ViewKind`](xref:GeometryHelper.Drawing.ViewKind) | Looks from | Across the view, up it | Where the corner (30, 20, 10) of a box at the origin falls |
|---|---|---|---|
| `Front` | +Z | X, Y | (30, 20), depth 10 |
| `Top` | +Y | X, -Z | (30, -10), depth 20 |
| `Bottom` | -Y | X, Z | |
| `Left` | -X | Z, Y | |
| `Right` | +X | -Z, Y | (-10, 20), depth 30 |
| `Back` | -Z | -X, Y | |
| `Isometric` | +X, +Y and +Z alike | Y up | (14.142, 0), depth 34.64 |
| `Section`, `Detail`, `Free` | set by whoever makes them | | |

In the drawing's frame the box's front is the bottom edge of the top view and the left edge of the right view, and the face
turned to the viewer runs round the way it does seen from outside. The frame of a drawing is the main object's, X along its
length, so a beam lying along any axis of the model gives the same views.

### Where they stand on the sheet

[`ProjectionMethod`](xref:GeometryHelper.Drawing.ProjectionMethod) says how the views along the axes go round the front view.
Each is the same picture either way; only its place changes. `ThirdAngle`, the default, puts each view on the side it looks
from, the top above the front and the right end on its right, as American drawings have it; `FirstAngle` puts it on the side
away, the top below and the right end on the left, as ISO drawings in Europe have it. On an A3, the beam of the first look
has the origin of each view at, in paper millimetres:

| | `V-FRONT` | `V-TOP` | `V-RIGHT` |
|---|---|---|---|
| third angle | (21, 189.3) | (21, 250.9), above | (360, 189.3), to the right |
| first angle | (63, 248.9) | (63, 185), below | (36, 248.9), to the left |

`StandardViews.GetSlot(kind, method, out column, out row)` gives the slot of a view round the front view: the top view is
(0, 1) in third angle and (0, -1) in first.

## A view

A [`DrawingView`](xref:GeometryHelper.Drawing.DrawingView) holds a view's frame, kind, crop, scale and lines, and once the
drawing is made its dimensions, marks and symbols and where it stands.

| Member | Holds |
|---|---|
| `Id`, `Name`, `Title` | `V-FRONT`; `FRONT`; `FRONT 1:10`. An id is made from the kind and, for a section or detail, its name: `V-SECTION-B`, `V-DETAIL-C`, so that an update recognises it |
| `Kind`, `Frame`, `SectionPlane`, `ParentViewId` | what it is, where it looks from, the plane a section is cut by, the view a detail is taken from |
| `Crop` | the part drawn, in view millimetres at 1:1; by default the box of its lines with a margin of 10 |
| `Scale`, `HiddenLines` | 10 for 1:10; whether hidden lines are drawn |
| `Lines` | the [`ProjectedView2`](xref:GeometryHelper.Drawing.Projection.ProjectedView2) |
| `ObjectIds` | the objects it was projected from, in the order of their ids |
| `Dimensions`, `DimensionIds`, `Marks`, `CentreLines`, `WeldSymbols`, `SectionMarks`, `DetailMarks`, `LevelMarks` | what [annotation](annotation.md) made of it |
| `Box`, `SheetOrigin`, `ModelOrigin`, `SheetBox` | everything it draws, and where it stands on the sheet and in model space |

The lines are in view millimetres at 1:1 and the scale maps them to the sheet: the front view of the beam, 3 000 long at 1:10,
has its lines from 0 to 3 000 across and stands 300 wide on the sheet. A view does not change once its drawing is made.
A view with no line in it is not made, and a view sees only the objects whose box in it meets its crop.

[`ViewBuilder`](xref:GeometryHelper.Drawing.ViewBuilder) makes the views the settings ask for, and makes one again:

```csharp
var builder = new ViewBuilder(source, DrawingSettings.Assembly, Tolerance.Default);
IReadOnlyList<DrawingView> views = builder.Build();                  // V-FRONT, V-TOP, V-RIGHT, each at 1:1
DrawingView end = builder.WithCrop(views[0], new GeoRectangle2(new GeoPoint2(2950, 0), 300, 400));
```

| Call | Does |
|---|---|
| `Build()` | the views along the axes and the isometric one in the order of [`ViewSettings`](xref:GeometryHelper.Drawing.ViewSettings)`.Kinds`, then the sections, then the details, each at the smallest scale allowed until the drawing sets its scale |
| `WithCrop(view, crop)` | the lines made again from the objects the new crop meets: the crop of the beam's middle, 300 by 400 at 1 000, meets the beam alone and gives 6 visible lines, the plate being out |
| `WithDirection(view, frame)` | looking another way: a `Free` view from then on, its id kept |
| `WithDepth(view, inFront, behind)` | another depth |
| `WithScale(view, scale)` | another scale, the lines not made again: the front at 1:20 stands 152 by 18 on the sheet |
| `ChooseScale(box, width, height)` | the largest of the scales allowed at which a box fits a space: a box 3 200 by 400 in a space 380 by 250 fits at 1:10 and not at 1:5 |

A projector other than [`ViewProjector`](xref:GeometryHelper.Drawing.Projection.ViewProjector) can be given to the constructor,
as a test that counts the calls does: the three views of a part are three calls, and a new crop is one more. The lines are
cut at the crop when they are written, so that what is in the DXF is what the view shows.

## Projection

[`ViewProjector.Project(inputs, frame, tolerance)`](xref:GeometryHelper.Drawing.Projection.ViewProjector) projects bodies into
the lines of one view: which edges are seen, which are hidden, and the faces a section cuts, exactly on the planar faces of
the bodies, to the point tolerance, with no depth buffer. The view is orthographic along the Z of its frame, and a point of
the model is drawn at its X and Y in the frame, in millimetres at 1:1.

```csharp
var inputs = new[] { new ProjectionInput("B", box) };            // a box 300 by 200 by 20
ProjectedView2 front = ViewProjector.Project(inputs, GeoCoordinateSystem3.Global, tolerance);
// View2(Visible: 4, Hidden: 0, Sections: 0); front.Bounds is 300 by 200
```

A box lying in the frame's XY plane is seen from the front as a rectangle of four lines: its four back edges fall on the
four front ones and are not drawn again, and the four edges along Z are points. Turned half a radian about X, so that three
faces are seen, it shows 5 visible lines and 1 hidden one.

**Which edges are drawn.** An edge where two faces of a body meet at more than the smooth angle is sharp and drawn. An edge
between two faces meeting within it, as two facets of a tube, is drawn only as a silhouette, where one face is turned to the
viewer and the other away or seen edge-on. An edge seen end-on, shorter than the point tolerance, is a point and not drawn.
The smooth angle is 30 degrees ([`ProjectionOptions`](xref:GeometryHelper.Drawing.Projection.ProjectionOptions)`.DefaultSmoothAngleRad`): a tube of 24 sides meets at 15 and is seen across
as two long lines, not 13; a chamfer at 45 stays two lines. The angle reads the faces, not what they were made from: a bar of
8 sides at 45 degrees shows every facet, and a real corner meeting within the angle is read as smooth.

**What hides what.** An edge is hidden where it lies within a face turned to the viewer and stands behind the face's plane by
more than the point tolerance; a face never hides the edges it is bounded by, so an edge piercing a face is seen on one side
and hidden on the other. An edge lying in a face's plane, as where two plates lie face to face with their faces flush, is not
hidden by it, and a face seen edge-on hides nothing. A face a hair off flat is taken in the plane through the middle of its
corners.

**Each line once.** Where edges fall on one another in the view, of one object or of two touching, the line is drawn once:
seen where any of them is seen, hidden where all are, and listing the objects whose edges give it, lowest id first. A chain
of short lines that a faceted circle leaves is fitted back into an arc or a circle: a hole of 22 through a plate seen along
its axis is one circle of 22, a hexagon, turning by 60 degrees, stays six lines.

The bodies are taken in the order of their ids, so the same bodies in any order give the same view, to the bit. A body's
openings are cut in before it is drawn; one wound inwards is turned first. Every body should be closed: an open one can show
edges its missing faces would have hidden.

### The lines

A [`ProjectedView2`](xref:GeometryHelper.Drawing.Projection.ProjectedView2) has `VisibleEdges`, `HiddenEdges`, `SectionFaces`
and `Bounds`, each list in a fixed order, by object id, kind and coordinates. A
[`ProjectedEdge2`](xref:GeometryHelper.Drawing.Projection.ProjectedEdge2) is a straight `Line`, or an `Arc` or a whole circle
(`IsArc`, `IsCircle`), seen or hidden along its whole length, with its [`EdgeKind`](xref:GeometryHelper.Drawing.Projection.EdgeKind)
(`Sharp`, `Silhouette`, `SectionOutline`), its `Visibility` and the ids of the objects whose edges it is. `WriteTo(database,
hatchScale)` writes a view into model space at 1:1: seen lines on the layer Visible, hidden ones on Hidden with the linetype
HIDDEN, a straight line as a LINE, an arc as an ARC, a whole circle as a CIRCLE, and each section face as a HATCH of ANSI31 on
the layer Hatch.

`ProjectionOptions(smoothAngleRad, nearDepth, farDepth, sectionPlane)` sets the smooth angle, the depth range (an object lying
wholly outside it is left out, one reaching into it drawn whole, as Tekla draws the parts its view depth takes in) and a
section plane.

## Sections

A section cuts the model by a plane and shows what lies beyond it, the side towards the viewer taken away. Each body is split
by the plane and the part beyond it drawn, its cut face hiding what lies behind it; the faces the plane cuts are those of
[`GeoSolid3`](xref:GeometryHelper.Geometry.GeoSolid3)`.Section`, seen in the view, drawn as `SectionOutline` and hatched. A box 300 by 200 by 20 cut by a plane through its
middle, square to the view, gives one face of 60 000 square units.

**By a share of the depth.** A [`SectionViewRequest(name, lookingAs, share)`](xref:GeometryHelper.Drawing.SectionViewRequest)
looks as one of the views along the axes does, at a share from nought to one of the depth of the objects, counted from the
side away from the viewer. The `Part` preset asks for "A" looking as the right end view at 0.5; on the end plate P1 that is a
cut across the middle of its thickness, one face of 6 800 square units, the 340 by 20 of the plate, at 1:5 on an A3.

**Along a line.** A `SectionViewRequest(name, inView, cutLine, depth)` cuts square to the view the line is drawn in, looking
to the right of the line from its first point to its second, as far as `depth` behind the cut, in model millimetres, infinity to
see all. The line from (1500, -200) to (1500, 200) across the front view of a beam 3 000 long cuts it at 1 500 and looks
towards its far end; from (1500, 200) to (1500, -200) it looks back. On the beam of the first look, `AddSection("B", "V-FRONT",
line, 100)` gives:

| | |
|---|---|
| the view | `V-SECTION-B`, titled `SECTION B-B 1:10`, a frame of depths 0 in front and 100 behind |
| its lines | 12 visible and none hidden, and one section face of 10 627 square units: the I-section, 2 x 300 x 14 + 262 x 8.5 |
| its dimensions | the overall 300 across and 290 up |
| in the front view | a [`SectionMark`](xref:GeometryHelper.Drawing.Annotation.SectionMark) `SM-B`, the cut line with an arrow at each end and the letter B |

The name is the letter of the section, "B" for SECTION B-B, and `V-SECTION-B` is the view's id. A name a drawing has already is
refused. A view with the plane seen edge-on is refused by the projector.

## Details

A [`DetailViewRequest(name, of, area, enlargement)`](xref:GeometryHelper.Drawing.DetailViewRequest) is a rectangle of a view
along the axes or the isometric view, drawn on its own at a larger scale. The rectangle is in the view's millimetres at 1:1,
and `enlargement` is how many times larger the detail is drawn; the scale is the nearest of the drawing's scales no smaller.
The end of the beam, from (2800, -200) to (3100, 200) in the front view at 1:10, drawn at 1:5, is `V-DETAIL-C`, titled `DETAIL C
1:5`, its crop 300 by 400, its `ParentViewId` `V-FRONT`, twice the size of the view it is taken from; the front view carries a
[`DetailMark`](xref:GeometryHelper.Drawing.Annotation.DetailMark) `DM-C`, a circle or a rectangle round the area with the
letter beside it. A detail is not dimensioned unless asked for, and a detail of a section, a detail or a free view is refused.

## See also

- [Dimensions, marks and symbols](annotation.md): what each view is annotated with.
- [Settings, scale and sheet](settings.md): `ViewSettings`, the kinds a preset makes, and how the scale is chosen.
- [Making it again](update.md#adding-and-changing-one-view): `AddView`, `AddSection` and `AddDetail` on a drawing made.
- [Geometry in space](../solid.md): the bodies a view is projected from.
