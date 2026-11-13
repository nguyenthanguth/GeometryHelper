# Entities and curves

What every entity has (its look, its box, its shapes), the curves and how they are walked, cut, converted and joined,
and each entity's shape given typed.

## What every entity has

Every entity has AutoCAD's `Layer`, `Color`, `ColorIndex`, `TrueColor`, `Linetype`, `LinetypeScale`, `LineWeight`,
`Visible` and `Transparency`, its `Handle`, `OwnerHandle`, `XData`, `Hyperlinks` and `ExtensionDictionary`, its `Owner` and
`BlockName`, and `GetGeometry()`, the library's shapes for it as [`DxfReader`](xref:GeometryHelper.Dxf.DxfReader) reads it, a LEADER its curve and a
MULTILEADER the lines of its leaders. A true colour's `Color` carries the
index nearest it, orange (0xFF8000) the index 30, and is written with it. `Transparency`, group 440, is a [`DxfTransparency`](xref:GeometryHelper.Dxf.DxfTransparency):
BYLAYER where the record gives none, BYBLOCK, or an alpha from 0, clear, to 255, solid, `new DxfTransparency(127)` the 50 per
cent of AutoCAD's dialog, written 33 554 559 (0x0200007F); a file older than R2004 has none and refuses one. Its
`Hyperlinks` are those of its extended data of PE_URL, written as AutoCAD writes them:
`line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" })`.
`SetPropertiesFrom` takes the look of another entity, checked whole before anything is written, and `SetDatabaseDefaults`
that of a drawing's current variables. `line.HandOverTo(circle, true, true)` puts an entity made in code in the line's place,
as AutoCAD's `HandOverTo` hands the id over: the circle is written where the line stood in its block, under its handle, owner
and reactors, so that a group that held the line holds the circle, with the line's extended data and extension dictionary
where the flags keep them, else its own and none; the line's extended data kept where it has none leaves the circle its own,
as AutoCAD 2021 keeps it. The line is in no file from then on, a copy of what it was. A copy of an annotative TEXT handed
the place writes its contexts where the line's extension dictionary is not kept, as `AppendEntity` writes them; kept, the
line's dictionary stands for the copy's, as AutoCAD writes it. Kept, a dictionary of contexts of another kind of entity is
refused before anything is written, since AutoCAD 2021 keeps such contexts unconverted and crashed reading its own drawings
of them: a TEXT of contexts handed over to an MTEXT keeping its dictionary throws `ArgumentException`, as do an MTEXT handed
to a TEXT, an INSERT to a TEXT and a TEXT to an INSERT. A TEXT, an ATTDEF and an ATTRIB take one another's contexts, which
AutoCAD read; a LINE, never annotative, takes any. Not keeping the dictionary, the new entity writes contexts of its own.

### Its box and its shapes

`GeometricExtents` is the box of its shapes, a [`GeoAabb3`](xref:GeometryHelper.Geometry.GeoAabb3); an entity with none, a REGION or a LINE of no length, throws, as
AutoCAD throws for an entity with no extents. A text has no fonts here, so its box is the point it stands at. `GetPlane()`
gives the plane it lies in, that of its extrusion through its first point where all of it lies there, else the one its shapes
span, to `Tolerance.Default`, and `IsPlanar` whether there is one: a 3D polyline with a corner 0.002 off the plane of the
others has none. `IntersectWith(other, DxfIntersect.ExtendThis)` gives the points where two entities meet, by the library's
shapes for them: the line from (0, 0, 0) to (10, 0, 0) meets the circle of radius 3 about (10, 0, 0) at (7, 0, 0), and, run
on as its infinite line, at (13, 0, 0) too; an arc runs on as its circle. A closed polyline, a SOLID and a 3DFACE meet along
their edges, not across their inside: the square 4 by 4 about the origin meets the line along the x axis at (-2, 0, 0) and
(2, 0, 0), and a line standing through its middle not at all. A polyline meets segment by segment, a point meets what it lies
on, and a pair the library cannot cross, a text among them, throws `NotSupportedException`. `BoundingBoxIntersectWith`
gives where the boxes of two entities meet.

## Curves

### Lines, circles, arcs and the rest

Lines, circles, arcs, ellipses, points, rays and xlines are [`DxfLine`](xref:GeometryHelper.Dxf.DxfLine), [`DxfCircle`](xref:GeometryHelper.Dxf.DxfCircle), [`DxfArc`](xref:GeometryHelper.Dxf.DxfArc), [`DxfEllipse`](xref:GeometryHelper.Dxf.DxfEllipse), [`DxfDBPoint`](xref:GeometryHelper.Dxf.DxfDBPoint),
[`DxfRay`](xref:GeometryHelper.Dxf.DxfRay) and [`DxfXline`](xref:GeometryHelper.Dxf.DxfXline), with AutoCAD's members. A CIRCLE whose groups 10, 20 and 30 are (10, 20, 5) on the extrusion
(0, 0, -1) has its `Center` at (-10, 20, -5), as AutoCAD gives it; setting `Normal` keeps that centre where it is in the
world. An arc's `StartAngle` and `EndAngle` are radians from nought up to 2 pi, written as degrees. On the ellipse with major
axis (10, 0, 0) and ratio 0.5, `StartParam` 0.7854 (pi / 4) is the point (7.071, 3.536, 0), whose `StartAngle` is 0.4636.
`new DxfArc(center, normal, 10, 0, Math.PI / 2)` then `db.ModelSpace.AppendEntity(arc)` adds a quarter circle.

### Lightweight polylines

A LWPOLYLINE is a [`DxfPolyline`](xref:GeometryHelper.Dxf.DxfPolyline): `pl.GetPoint2dAt(1)` is its second vertex in the plane of its normal, `pl.GetPoint3dAt(1)`
the same in the world (every vertex of a polyline of 10 000 in 15 ms), `pl.AddVertexAt(2, point, bulge, 0, 0)` puts one in, and `pl.SetBulgeAt(1, 1.0)` makes the segment
leaving it a half circle. A POLYLINE is a [`DxfPolyline2d`](xref:GeometryHelper.Dxf.DxfPolyline2d) or a [`DxfPolyline3d`](xref:GeometryHelper.Dxf.DxfPolyline3d), walked as AutoCAD walks one,
`foreach (DxfVertex2d v in polyline2d) v.Bulge = 0;`, and grows by `AppendVertex(new DxfVertex2d(point, 0, 0, 0, 0))`; a
vertex is written, put in or taken out as its own record, the others keeping their handles, and its `Owner` is the polyline. A SPLINE
is a [`DxfSpline`](xref:GeometryHelper.Dxf.DxfSpline) of its `Degree`, `ControlPoints`, `Knots`, `Weights` and `FitPoints`, a 3DFACE a [`DxfFace`](xref:GeometryHelper.Dxf.DxfFace)
(`GetVertexAt`, `IsEdgeVisibleAt`), a SOLID a [`DxfSolid`](xref:GeometryHelper.Dxf.DxfSolid) (`GetPointAt`), and the meshes a [`DxfPolyFaceMesh`](xref:GeometryHelper.Dxf.DxfPolyFaceMesh) and a
[`DxfPolygonMesh`](xref:GeometryHelper.Dxf.DxfPolygonMesh), whose vertices and face records are walked as their polyline's are. A point or face of a polyface mesh
erased keeps the mesh's counts and the faces' numbers of its points true.

### Walking a curve

A curve is a [`DxfCurve`](xref:GeometryHelper.Dxf.DxfCurve), walked as AutoCAD walks one: the line from (0, 0, 0) to (30, 40, 0) is 50 long, its parameter the
distance from its start; a circle or an arc is walked by its angle, an ellipse by its eccentric angle, a ray or an xline by
the distance from its base point, a polyline by the index of a vertex plus how far along its segment, and a spline by its knot
value. The polyline (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex is 25.71 long, and its parameter 1.5 is
(15, 5). The curves edit as AutoCAD's do, each result a new entity of the same look in no file, to be appended:

```csharp
DxfPolyline pl = db.ModelSpace.OfType<DxfPolyline>().First();
IReadOnlyList<DxfCurve> pieces = pl.GetSplitCurves(new[] { 1.5 });   // cut half way along the second segment
IReadOnlyList<DxfCurve> outer = pl.GetOffsetCurves(5.0);              // to the right of its way: a counter-clockwise loop grows
pl.Extend(pl.EndParam + 0.5);                                         // the last segment run on by half its length or angle
DxfSpline spline = pl.Spline;                                         // the spline that is exactly the polyline
DxfCurve flat = spline.GetOrthoProjectedCurve(new GeoPlane3(new GeoPoint3(0, 0, 0), new GeoVector3(0, 0, 1)));
```

### Cutting a curve

A segment cut keeps the part of its arc and its widths read off along it: the polyline above cut at 1.5 is (0, 0), (10, 0),
(15, 5) and (15, 5), (10, 10), each arc of bulge 0.414. An offset keeps arcs as arcs and runs the corners on until they meet,
as AutoCAD's OFFSETGAPTYPE 0; the square of side 10 running counter-clockwise offset by 1 is 12 by 12, running clockwise 8 by
8. A closed polyline that crosses itself has no offset, either way: `GetOffsetCurves` and `GetOffsetCurvesGivenPlaneNormal`
of a `DxfPolyline`, a `DxfPolyline2d` or a `DxfPolyline3d` give none and say why in one Debug line, as AutoCAD 2021 gives none
of a bow tie. It crosses itself where a side passes through another, coming from one side of it and leaving to the other: the
bow tie (-10, 1), (10, -1), (10, 1), (-10, -1) at the origin, and the square (0, 0), (6, 0.06), (4, 0.06), (10, 0), (10, 10),
(0, 10), whose first and third sides cross at (5, 0.05) and curl into a loop of 0.01 square units, which AutoCAD gave no
offset inwards and outwards one running out to x = 206 on a square 10 wide. A corner that only touches another side, both
its own sides on one side of it, as (5, 0) of (0, 0), (10, 0), (10, 10), (5, 0), (0, 10), is no crossing, nor is a spike
narrower than the point tolerance (1E-3) at its foot, nor a corner given twice: such a polyline, and an open one that crosses
itself, is offset as before. `Offset2.Offset` of a [`GeoPolygonArc2`](xref:GeometryHelper.Geometry.GeoPolygonArc2) that crosses itself is not changed by this: it reads the
loop even-odd, as it did. A polyline and a spline offset to that side, the right of their way seen from the tip of their normal, where an arc's
radius grows, as AutoCAD 2021 offsets them; a line, a ray and an xline to the left, as AutoCAD offsets a line: the line from
(0, 0, 0) to (30, 40, 0) offset by 5 runs from (-4, 3, 0). The exceptions
are an arc, a circle and an ellipse given a normal by `GetOffsetCurvesGivenPlaneNormal`: their radius grows with a distance
above nought whichever way the normal points, as AutoCAD offsets them by their radius, so that under the normal turned over an
arc and a polyline of that one arc offset to opposite sides. An ellipse is offset as the spline of the curve at the distance,
as AutoCAD gives one. A spline is offset piece by piece between its corners (knots standing as many times as its degree where
its direction turns), each piece a cubic fitted through points of the true offset, more of them only in the spans that need
them, until it keeps within 1E-3 of the true offset both ways; the pieces are joined as a polyline's offsets are, so that the L
of degree 2 from (0, 0) to (10, 0) to (10, 10) offset by 1 has its corner at (11, -1). A span that does not keep within 1E-3
at 512 points is given as fitted. A cubic of 300 spans with one corner gives an offset of some 9 600 control points in 0.6 s.
A spline is run on along the polynomial of its end span. Every curve gives the `Spline` that is exactly it (a circle of
radius 10 is the rational quadratic of 9 control points), but for a polyline of the plane fitted with a spline, which gives
the B-spline it is fitted with, on its frame, as AutoCAD 2021's `Polyline2d.Spline` does: the frame (0, 0), (10, 10),
(20, 0), (30, 10), (40, 0) fitted by a cubic gives the cubic of those 5 control points, not a spline through its 17 vertices
drawn. A curve projected keeps its kind where it can: a circle of
radius 10 on the floor projected along (0, 0, 1) onto the plane y + z = 0 is an ellipse of radii 14.14 and 10.

### Turning one polyline into another

`DxfPolyline.ConvertFrom(polyline2d, true)` and `ConvertTo(true)` turn a LWPOLYLINE into a POLYLINE of the plane and back
in its place, its handle, owner, look, extended data and extension dictionary kept, the old object erased, as AutoCAD hands
the object id over; with `false` the new polyline is made in no file and the old one kept. A polyline of the plane is
fitted with `CurveFit` (a pair of arcs on each segment through every vertex) or `SplineFit` (a uniform B-spline on its
vertices as a frame, of the header's SPLINETYPE, 5 quadratic or 6 cubic, and SPLINESEGS segments to each span), a 3D
polyline with `SplineFit`, the vertices of the curve kept after the first of the frame, as AutoCAD keeps them. `CurveFit`
is AutoCAD 2021's PEDIT Fit to 1E-11 in every vertex and bulge: the tangent at an end of an open polyline is that of the
circle through its first or last three vertices, and between, a blend of the two segments at the vertex weighted by the
turns at the vertices either side, so that on (0, 0), (100, 0), (100, 50), (0, 80) the tangent at (100, 0) is 64.41
degrees, not the 63.43 of the circle through it and its neighbours; a segment whose tangent at either end is within a
millionth of a radian of its line stays straight, with no vertex added, and two vertices on one point split the curve in
two there. A polygon mesh is smoothed with `SurfaceFit` (of SURFTYPE, SURFU and SURFV, spans each way, so that an open
mesh smoothed at 6 and 6 is given 7 rows of 7 points, as AutoCAD 2021's PEDIT makes them), each straightened by
`Straighten` or `ConvertToPolyType` / `ConvertToPolyMeshType`; the vertices of the frame keep their handles. The reader reads
a fitted polyline along the vertices the fitting made, and a smoothed mesh as its grid. Every vertex of a POLYLINE carries
the polyline's layer, linetype, colour, lineweight, linetype scale, visibility, true colour and transparency, written on it
when it is made and when the polyline's are set, as AutoCAD keeps them (its AUDIT reports a vertex of another colour); a
face of a polyface mesh keeps a colour of its own.

### Splines

A spline is periodic (`IsPeriodic`) by the flag 2048 of its group 70 on a curve that runs on round its seam, as AutoCAD
2021 reads it: AutoCAD writes the flag 2 on its closed splines of a smooth seam too and reads no period from it, its
periodic cubic of 70 3115 being periodic and its closed cubic of 70 11 not, either turning into the other with the flag
2048 alone taken out or put in; its 2059 with a control point beside the seam moved by 5, a corner there, is not periodic.
The seam is read as AutoCAD's periodic form is made: the curve unclamped at its start and at its end, the first degree of
its points the same as the degree a period on, within 1E-10, AutoCAD's own point tolerance, and 1E-14 of the spline's
size, one and its largest coordinate, for rounding. AutoCAD's own periodic splines are so to 1.3E-15 of their size, and
stay so scaled to millions or moved far from the origin; of its 2059 of size 61.5 with the control point beside the seam
moved, it read the seam off by 4.1E-11 as periodic and off by 4.1E-10 as not. NURBS data whose curve ends where it starts make a closed spline, the flag 1, and one that leaves its start in
the direction it reaches its end, within 1E-12 of a radian, the flag 2 too, whatever `closed` and `periodic` say, as
AutoCAD writes them: its closed cubic of 70 11 given its own data not periodic, or not closed, was saved 70 11 and read
closed, and with its seam turned by a hundred-millionth of a degree saved 70 9. `periodic` puts the flag 2
on any curve, as before, which `NurbsData.Periodic` and `FitData.Periodic` give back, and not the flag 2048, so that the
library's own periodic spline of 70 3 reads as not periodic, as AutoCAD read it; NURBS data set not periodic take the flag
2048 off. Where the record holds no fit points and the flag 1024 of its 70 is set, as AutoCAD saves a periodic spline it
fitted, `NumFitPoints`, `FitPoints`, `GetFitPointAt` and `FitData` give the points of the curve at its knots from its start
to its end, as AutoCAD gives them: (0, 0), (30, 0), (30, 15), (20, 15) and (0, 0) for its 3115 of the knots 0, 30, 45, 55
and 80; its fit data taken out (`SetNurbsData`, `PurgeFitData`) take the flag 1024 off, and `SetNurbsData` the flag 32
of fit data set as well, as AutoCAD saved its 3115 given its own data back 2059.

### Fit data

Fit data set (`FitData`) keep a closed spline closed and give it the flags 32 and 1024 of fit data, as AutoCAD 2021 saved
its closed cubic of 70 1035 given fit data 70 1067. Set periodic, they give AutoCAD's periodic cubic through the points
taken round, whatever degree they ask: each point on the curve at its knot, the knots the lengths of the chords, clamped
into as many control points as the points and 3, written 70 3115 with no fit points, tangents or fit tolerance, its points
at its knots being its fit points. Through (0, 0), (40, 1), (50, 30) and (10, 25) it runs over the knots 0, 40.01, 70.69,
111.00 and 137.93, its control points AutoCAD's to 1E-14; asked a degree 2 through (0, 0), (40, 0), (50, 30) and (10, 25),
AutoCAD wrote the same cubic as asked a degree 3, as the library does. Three points give the cubic too, of 3 control points
to a period, as AutoCAD fitted one through (0, 0), (30, 0) and (20, 15), and two a line there and back. Where two of the
points are one, within 1E-3, which make no periodic spline, it is the closed spline the library fits through them.

Fit data of the parameters `SqrtChord` or `Uniform` give the same cubics, open, closed or periodic, their knots going up by
the square root of each chord or by 1 from point to point: through (0, 0), (40, 1), (50, 30), (10, 25) and (-20, 40) one
step each, the knots 0 to 4 and AutoCAD 2021's control points to 3E-14. They are written as AutoCAD writes them, with no fit
points, tangents or fit tolerance and the flag 64 (`SqrtChord`) or 128 (`Uniform`) in place of the 32 of the chord, saved
70 1096 and 1160 (3147 and 3211 periodic), the points at their knots their fit points by the flag 1024. A spline reads its
parameters from its group 70 as AutoCAD read them: 64 the square root, 128 one step each, 256 its own
(`CustomParameterization`, a knot put in), else the chord; and where its fit points are the points at its knots and it is
not periodic, its derivatives at its ends are its tangents (`StartFitTangent` (7.01, -1.77, 0) for the square roots
through those five points), as AutoCAD read its own files and the library's. Parameters of the program's own are refused.

Fit data not periodic give AutoCAD 2021's cubic through the points, whatever degree they ask (it fitted degrees 1 to 5 all
as the cubic): each point on the curve at its knot, the knots the lengths of the chords from the first point, at an end
given a tangent the derivative there that tangent at the length it is given, and at an end given none the curve bent no
more, its second derivative nought. Through (0, 0), (30, 40) and (60, 0) it is the cubic of (0, 0), (10, 20), (30, 60),
(50, 20) and (60, 0) over the knots 0 to 100; through (0, 0) and (100, 0) the straight cubic of 4 points a third apart.
Through five points, alone or with tangents of length 1 or 5 (written as given, which AutoCAD keeps and fits by), the
control points are AutoCAD's to 1E-13. Points one after another within 1E-10 are one, as AutoCAD took two a trillionth
apart; a closed spline given no tangents has a corner at its seam (AutoCAD's 70 1065). A fit tolerance above 1E-10 is
written but not fitted to: AutoCAD fits a curve that passes near the points there, within 1.5 times the tolerance. AutoCAD
reads a SPLINE of fit points by fitting it so anew, whatever its control points, flags and degree (but one of the flag
2048, or of a fit tolerance above 1E-10): the reader and `DxfSpline` read it so where its control points are not that fit,
as AutoCAD read the closed cubics of 70 11 of the library's check 17-g (44 vertices at 10, where their control points gave
54), and as they are, to the bit, where they are, as AutoCAD's own are. Read so, its `Degree`, `ControlPoints`, `Knots`,
`Weights` (none), `IsRational`, `NumControlPoints` and `GetControlPointAt` are those of the cubic, as AutoCAD gave them:
a record of five fit points alone of degree 2 is of degree 3 and 7 control points, and one whose control point 1 was moved
gives the cubic's; an edit of its control points, knots, weights or degree, a knot put in or its fit data purged works on
the cubic, written to the record first. A move, turn or scale keeps the length of a tangent
(5 scaled by 2 stays 5); a control point or the knots set, or `UpdateFitData` (which AutoCAD 2021 leaves no fit data
after), take the fit data out, the flags 32 and 1024 with them (its 1064 is saved 8, its 3115 2059); a knot put in keeps
its fit points at its knots (70 1288), where fit points left in AutoCAD would fit anew of one control point fewer, and so
do `ModifyPositionAndTangent` (70 1288, a closed spline 1289) and the pieces of `GetSplitCurves`, as AutoCAD 2021 kept them.

### A spline's data whole

A spline gives and takes its `NurbsData` and `FitData` whole. `ElevateDegree` raises its degree with the curve kept to the
rounding, `InsertControlPointAt` and `RemoveControlPointAt` put a control point in and take one out with a knot,
`InsertFitPointAt`, `SetFitPointAt` and `RemoveFitPointAt` fit it anew through its points (its knots the lengths of the
chords, so that a spline through (0, 0, 0), (30, 40, 0) and (60, 0, 0) runs from 0 to 100), `ModifyPositionAndTangent`
makes it pass a point in a direction, `Rebuild` fits it again of another degree and count of points, and `ToPolyline` /
`ToPolylineWithPrecision` give the polyline along it. Made lines, it is AutoCAD 2021's: the spline's whole parameter range
halved until the spline's points halfway, a quarter and three quarters along each piece lie within max(1 / M, L / 10 000) /
(p + 1)^2 of its chord's, M the largest length of its second derivative, L its length and p the precision. The cubic on
(0, 0), (10, 10), (20, 10) and (30, 0), whose second derivative is 60 everywhere, gives 33, 65, 257, 1 025 and 4 097
vertices at 0, 1, 10, 30 and 99, and the cubics of two and three spans of the checks 50 and 78 at 0, 4 750 and 7 562 at
99, as AutoCAD gives them; so do a quadratic of three spans and a cubic of a double knot (29 and 76 vertices at 0), a
quintic Bezier (86 at 0 and 7 929 at 99, the same ten times its size), a hairpin cubic 100 by 1 (129 at 0 over the knots
0 to 1, where its length decides, and 5 over 0 to 100), and an S whose middle lies on its chord (173 at 0). Of AutoCAD's
156 counts at hand 148 are the rule's, the 8 others within 3, of rational cubics and the S at 20 (1 378 at 10 where AutoCAD
gives 1 377): AutoCAD measures their length a little longer than it is, by 2E-4 for the rational ones, and with its own
lengths the rule gives all eight. A spline not periodic of n control points is cut instead into (n - 3) + (n - 2) k pieces
even in its parameter, k half the precision rounded down, where that gives more pieces than the halving, as AutoCAD 2021
cut its closed cubics, of fit points or not, and its open curves of few pieces: the straight cubic of 4 points is one
segment at 0 and 1 and three at 2 and 3, the quintic Bezier over 0 to 50 three at 0, and the closed cubic through
(0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25) five at 0, (0, 0), (40.5, 0.5), (49.32, 14.74), (50, 30) and
(10.71, 25.63), with no last vertex on its first; of a closed spline of fit points, its knots stand in place there too,
as many as its vertices less two. Otherwise a closed spline has its last vertex on its first, the polyline closed; one closed or of the flag 2 of its 70 that
runs through fit points has each of its knots, one after another, in the place of the vertex nearest it, the later of two as near, a
knot taking the place another took before it: the closed cubic through (0, 0), (40, 0), (50, 30) and (10, 25), its knots
the lengths of the chords, is those four points and the first again at 0, its 48 vertices at 10 the range's 64ths with the
knot 40 in the place of the 64th at 39.05, as AutoCAD 2021 gave them. So does one AutoCAD keeps by its fit points, the flag
1024 of its 70 set (3115 for a periodic cubic), though its DXF holds no fit points, as AutoCAD read its own again. The same
cubic of no fit points and no flag 1024, its 70 of 3 or 11, is the halving alone, (0, 0), (34.42, -3.51), (50.86, 28.40), (18.15, 30.49) and the first again at 0, as AutoCAD
2021 gave it too; the arcs are bent as the pieces the halving made. Made
arcs, for a flat spline, it has the same vertices, each segment the arc that turns as the spline's
tangent turns along it, its bulge the tangent of a quarter of that turn, as AutoCAD 2021 makes them: the cubic of one span
at 0 gives 33 vertices, its first arc, from (0, 0) to (0.9375, 0.908), of bulge -0.00806, the tangent turning from 45
degrees to 43.15.

`ToPolyline(n)` is the library's own reading of a count: n vertices set evenly along the spline's length, each segment a
line or the arc through the spline's point halfway along it. AutoCAD 2021's `ToPolyline(n)` keeps a different promise: it
was seen to give the cubic of one span 33 vertices for every n from 2 to 50 and 65 for 99, the cubic of two spans 50 for
every n from 3 to 99, and the cubic of three spans 78 for every n from 4 to 99, about what it gives at precision 0, and to
refuse the smaller n (2 for two spans, 2 and 3 for three) with `eInvalidInput`.

### Joining curves

Curves are joined as AutoCAD's JOIN joins them, by `JoinEntity(other)` and `JoinEntities(others)` of the entity they join
into: its record is written anew to run over them, and the others are left as they are, for the caller to erase. A
`DxfLine` takes lines along its line, gaps and overlaps and all, and runs from the lowest of their ends to the highest: the
line from (0, 0, 0) to (10, 0, 0) joined to the one from (25, 0, 0) to (15, 0, 0) runs to (25, 0, 0). A `DxfArc` takes arcs
of its circle, and a `DxfEllipse` elliptical arcs of its ellipse, counter-clockwise from its start, the gaps filled: the
quarter from 0 to pi / 2 joined to the arc from pi to 3 pi / 2 runs to 3 pi / 2; one that would make it whole is refused,
as AutoCAD makes a CIRCLE of it. Several arcs are taken in the order of their starts counter-clockwise from the source's
end, so that the order they are given in changes nothing. The gaps follow the JOIN command: that AutoCAD's `JoinEntity`
fills them too is not confirmed. A `DxfPolyline` or `DxfPolyline2d` takes lines, arcs and polylines of its plane, the whole
of each arc within the tolerance of it, end to end at either end, each turned to run on along it, an arc as a bulge, a
polyline with its bulges and widths: the polyline (0, 0), (10, 0) joined to the half circle of radius 5 about (10, 5) from
(10, 0) is (0, 0), (10, 0), (10, 10) with a bulge of 1 on its second vertex. A `DxfPolyline3d` takes lines and polylines
with no arc, in any plane, as AutoCAD's JOIN makes a spline of one joined to a curve; a `DxfSpline` takes lines, arcs,
elliptical arcs, polylines and splines, as their `Spline`, all raised to the highest degree among them and given by their
control points. Ends meet within 0.001 in space, the point tolerance of `Tolerance.Default`, and where they come back
together the polyline or spline is closed. `JoinEntities` chains its pieces on from the entity's end, then back from its
start, the one of lowest index each time, and gives the indices of those joined: the four sides of a square given in any
order close a polyline of one of them. A piece that would run back over the one at the end it meets is left out, as a line
drawn twice is: the line from (10, 0) back to (0, 0) does not close the polyline (0, 0), (10, 0) into a loop of no area. A
closed curve, a fitted polyline and a polyline of no vertices are not joined, and a POLYLINE's vertices are written anew,
each with a handle of its own.

### Regions

A REGION is flat ACIS data, and the library writes and reads it for the faces AutoCAD's REGION command makes: loops of lines,
arcs, circles, ellipses and polylines with bulges, an outer loop and its holes.
[`DxfRegionShape.Create(outer, holes, tolerance)`](xref:GeometryHelper.Dxf.DxfRegionShape) takes the curves of each loop in any
order and any way round, joins them end to end within the point tolerance, runs the outer loop counter-clockwise about the
normal of its plane (+Z for a loop lying on the XY plane, which it turns over if it was drawn clockwise) and each hole
clockwise, and refuses a loop that does not close or does not lie in the plane of the others with `ArgumentException`. A box
40 by 30 with a hole of radius 5 has area 1 121.46 and perimeter 171.42, the two numbers AutoCAD's `Region.Area` and
`Region.Perimeter` give for it; `shape.Loops` lists the hole first, then the box, each with its `Curves` as new entities.

`DxfUnknownEntity.CreateRegion(shape, database)` makes the REGION for a drawing, to be added with `AppendEntity`, its data
written as AutoCAD 2021 writes it for the drawing's version: SAT text in groups 1 for R2000 (ACIS 4.00), R2004, R2007 and R2010
(ASM 208, 212 and 215), each character but a space written 0x9F less and a caret followed by a space, and for R2013 and R2018
binary SAB in a record of the section ACDSDATA under the entity's handle, the entity naming it by a GUID (group 2). AutoCAD 2021
opens the regions written for each of the six versions, and the same drawing written as a DWG of R2018, with `AUDIT` finding
0 errors, and `LIST` gives the area and perimeter the shape has. A region made for one version and added to a drawing of
another throws `ArgumentException`, and a drawing before R2000 `NotSupportedException`.

`GetRegionShape()` reads the shape of a REGION back, from the SAT of its groups or the SAB of the data store, whoever wrote
it: the regions of AutoCAD 2021's own DXF of R2000 to R2018 read to AutoCAD's area and perimeter. It is null for a region
whose body is not flat faces of lines and arcs of ellipses (a spline edge, a face on a cone). `Explode()` gives a region's curves as AutoCAD's does: a
box with a hole a circle and four lines, the hole first, a stadium two lines and two arcs, a whole ellipse an ellipse
whose major axis runs from its centre away from where the loop starts, and the parameter from -pi to pi, each with the
region's look; AutoCAD 2021's `Explode` and the library's were compared for twelve regions, tilted and elliptical ones among
them, and agree in every curve, point and axis. A region of more than one face is read but not written, and no region is
moved, scaled or copied with its data: a copy of one in a drawing of R2013 or later has none of its own.

### What it costs

What it costs: a vertex of a POLYLINE appended or set one at a time costs about the same however many the polyline has, 20 000
appended one by one in a fifth of a second and 10 000 points of a polyface mesh in a twentieth; a LWPOLYLINE's vertex put in,
set or taken out rewrites only that vertex's groups and the count, 10 000 put in one by one in 0.8 s and set one by one in
1.2 s; moving a polyline of 2 000 vertices writes them in one pass, 50 milliseconds; straightening a fitted polyline or
desmoothing a mesh takes the made vertices out in one writing, 90 000 in 0.6 s; a spline of 200 spans made a polyline of 100
vertices takes 0.04 s, and one of 1 000 spans is rebuilt of 5 000 control points in 0.05 s; 10 000 lines in any order are
joined into a LWPOLYLINE in 0.23 s and into a SPLINE in 0.19 s, all on .NET 10.

## Typed shapes

### The kinds given

Each class gives its shape as the library's, typed, by the names CadConvert gives AutoCAD's: `line.ToGeoLine3()` where
`GetGeCurve()` gives an `object` to be cast. A conversion of space (`...3`) is the shape `GetGeometry()` gives, to the bit;
one of the plane (`...2`) is what `DxfReader` reads with `PlanAs2D`, to the bit, within the default tolerance or the one
given (`ToGeoLine2(tolerance)`). An entity that does not lie on the floor, its heights not all nought or, in the system of
its extrusion, its extrusion not (0, 0, 1) or (0, 0, -1), is refused with an `InvalidOperationException` rather than
flattened: the line from (0, 0, 0) to (100, 50, 0) is the [`GeoLine2`](xref:GeometryHelper.Geometry.GeoLine2) from (0, 0) to (100, 50), and with a z of 5 at one end
`ToGeoLine2` refuses it, where CadConvert's drops z. On (0, 0, -1), as AutoCAD writes a mirrored entity, x is turned over,
an arc runs clockwise and a bulge is negated, as the reader reads them. So is an entity with no shape, with the reader's
reason (a LINE of no length, an ELLIPSE of no axis, a number that is not finite), and one that is erased.

| Class | Of space | Of the plane |
|---|---|---|
| `DxfLine`, `DxfCircle`, `DxfArc` | `ToGeoLine3`, `ToGeoCircle3`, `ToGeoArc3` | `ToGeoLine2`, `ToGeoCircle2`, `ToGeoArc2` |
| `DxfEllipse` | `ToGeoEllipse3` of a whole one, `ToGeoSpline3` of a part | `ToGeoEllipse2`, `ToGeoSpline2` |
| `DxfRay`, `DxfXline`, `DxfSpline`, `DxfDBPoint` | `ToGeoRay3`, `ToGeoXLine3`, `ToGeoSpline3`, `ToGeoPoint3` | `ToGeoRay2`, `ToGeoXLine2`, `ToGeoSpline2`, `ToGeoPoint2` |
| `DxfPolyline`, `DxfPolyline2d` | `ToGeoPolyline3`, `ToGeoPolylineArc3`, `ToGeoPolygon3`, `ToGeoPolygonArc3` | `ToGeoPolyline2`, `ToGeoPolylineArc2`, `ToGeoPolygon2`, `ToGeoPolygonArc2` |
| `DxfPolyline3d` | `ToGeoPolyline3`, `ToGeoPolygon3` | `ToGeoPolyline2`, `ToGeoPolygon2` |
| [`DxfLeader`](xref:GeometryHelper.Dxf.DxfLeader) | `ToGeoPolyline3` of a straight one, `ToGeoSpline3` of a splined one | `ToGeoPolyline2`, `ToGeoSpline2` |
| `DxfFace`, `DxfSolid` | `ToGeoPolygon3` | `ToGeoPolygon2` |

### Polylines

A polyline is given as the kind asked for, as CadConvert gives one, built by the reader's own chain and loop. A loop
(`...Polygon...`) joins its last vertex to its first whether the polyline is closed or not, the bulge of the last vertex that
of the closing edge, and is refused where it encloses no area (three vertices on a line) or, in space, does not lie in one
plane. A chain (`...Polyline...`) of a closed polyline runs on back to its first vertex. The straight kinds refuse a polyline
with an arc, its arcs never cut into chords here, and the kinds with arcs give one with none, every bulge nought. A vertex on
the one before it is left out and a closed polyline of two vertices with an arc has its first arc halved, as the reader
reads them; a polyline fitted with a spline is read through the vertices of the spline drawn, its frame left out, and its
widths are no part of the shape. A 3DFACE is the polygon of its corners where they are in one plane, and refused where the
reader cuts it into two triangles; a SOLID is its corners in DXF's order 1, 2, 4, 3. An ELLIPSE whole is a [`GeoEllipse3`](xref:GeometryHelper.Geometry.GeoEllipse3)
and a part of one the spline that is exactly that part, each refusing the other (`Spline` gives a whole one as a spline).
`GetLineSegment2dAt` and `GetArcSegment2dAt` of a `DxfPolyline` give a segment in the system of its normal, of the vertices
of `GetPoint2dAt` and the arc as its bulge turns there, as AutoCAD's do: on a tilted or mirrored normal, the segment of
`GetLineSegmentAt` or `GetArcSegmentAt` taken into that system.

### The way back

The way back, `SetFromGeCurve`, takes the shape of the plane too, written at z = 0 on the normal (0, 0, 1) as [`DxfWriter`](xref:GeometryHelper.Dxf.DxfWriter)
writes it, and `ToGeoX2()` gives it back to the bit, but the angles of an arc, which go through degrees; a clockwise
[`GeoArc2`](xref:GeometryHelper.Geometry.GeoArc2) comes back counter-clockwise over the same points. A `DxfPolyline` takes a [`GeoPolyline2`](xref:GeometryHelper.Geometry.GeoPolyline2), [`GeoPolylineArc2`](xref:GeometryHelper.Geometry.GeoPolylineArc2),
[`GeoPolygon2`](xref:GeometryHelper.Geometry.GeoPolygon2) or `GeoPolygonArc2` (closed for a loop), or the same of space; a `DxfPolyline2d` the same, written as a
POLYLINE of the plane; a `DxfPolyline3d` a [`GeoPolyline3`](xref:GeometryHelper.Geometry.GeoPolyline3) or [`GeoPolygon3`](xref:GeometryHelper.Geometry.GeoPolygon3), or a `GeoPolyline2` or `GeoPolygon2` at
z = 0. A chain or loop of space whose every vertex is within the point tolerance of the plane through its first vertex
square to the polyline's own normal, and no point of its arcs further off it than the planar tolerance, is written in the
system of that normal (a half circle of radius 100 turned 0.001 out of that plane stands 0.1 off it, and is written in its
own plane instead), so a polyline
mirrored on (0, 0, -1) given its loop edited in its plane stays mirrored, and a chain on one line has a plane to be in: the
LWPOLYLINE (0, 0), (10, 0) at the elevation 5 takes the chain from (0, 0, 5) to (20, 0, 5) and gives it back from
`ToGeoPolyline3()` to the bit. Any other is written in the system of the plane it lies in, its normal turned up; the chain
from (0, 0, 0) to (10 000, 0, 0.1) on a polyline of the normal (0, 0, 1), on one line and 0.1 off the polyline's plane, lies
in no plane and is refused rather than flattened. The handle, layer, look, thickness and extended data are kept. Where the
shape is written anew a polyline's widths, linetype generation and fitting are not kept; a shape written as the shape it has
now is written, as the one `GetGeometry()` gives, leaves it as it is, widths and fitting and all. What the entity's type
cannot hold is refused with an `ArgumentException`: a face with holes, a chain of space bent out of one plane for a
polyline of the plane. `CreateFromGeCurve` makes the curve of a shape of the plane as of one of space.

```csharp
DxfDatabase db = DxfDatabase.Open("plan.dxf");
DxfPolyline plate = db.ModelSpace.OfType<DxfPolyline>().First(p => p.Closed);
GeoPolygonArc2 outline = plate.ToGeoPolygonArc2();                 // refused where the plate does not lie on the floor
GeoPolygonArc2 grown = outline.Offset(5.0, Tolerance.Default)[0];  // 5 outwards, its arcs kept
plate.SetFromGeCurve(grown);                                       // the same handle, layer and look
db.Save("plan-grown.dxf");
```

A conversion of space at the default tolerance gives the shape `GetGeometry()` keeps; one of the plane, or one of a
polyline of a kind other than the file's, reads the record anew.

## See also

- [Editing rules and what the model does not do](rules.md): how each class moves and explodes.
- [AutoCAD and GeometryHelper](autocad.md).
- [How do I](../howto.md).
