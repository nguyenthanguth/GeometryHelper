# Between the plane and space, and local frames

## Between the plane and space

A plate, a face, a chain marked on a face: flat geometry lying in a plane in space. `PlanarMap` lays it
out in two dimensions, so that everything [the plane half](../plane.md) can do — offsetting, the booleans,
splitting, containment with holes — applies to it, and puts the answer back where it came from.

```csharp
GeoCoordinateSystem3 frame = plate.GetFrame();   // origin at the first vertex, Z along the normal

GeoFace2 flat = plate.ProjectToFace2(frame);     // the plan view of the plate
GeoFace2[] cut = flat.Subtract(openings);        // work in the plane
GeoFace3 back = cut[0].ToFace3(frame);           // and put it back in space
```

Everything goes through a frame, and the same frame has to be used both ways or the answer lands
somewhere else, so hold on to it. Where the frame comes from decides what the local coordinates mean:

| Frame from | Origin and first axis | Use it when |
|---|---|---|
| `polygon.GetFrame()`, `face.GetFrame()` | the first vertex, along the first edge | the plan view should be the same drawing wherever the shape sits in the model |
| `PlanarMap.FrameOf(plane)` | the plane's own origin and axes | several shapes have to share one set of local coordinates |
| `new GeoCoordinateSystem3(origin, xAxis, yAxis)` | whatever you name | the local coordinates mean something to you |

Every flat type makes the trip: `GeoPoint3`, `GeoVector3`, `GeoLine3`, `GeoPolyline3`, `GeoPolygon3` and
`GeoFace3` go out with `ProjectTo…`, and `GeoPoint2` … `GeoFace2` come back with `To…3`. An ellipse does too:
`GeoEllipse3.ProjectToEllipse2(frame)` goes out, and `GeoEllipse2.ToEllipse3(frame)` comes back; a circle in
space goes out as an ellipse, `GeoCircle3.ProjectToEllipse2(frame)`, which is a circle again when it lies
flat in the frame.

**Off the plane.** Flattening drops the local Z, so a point that does not lie on the plane has no honest
answer. `ProjectToPoint2` projects it and says so in its name; `TryToPoint2` refuses it when it is farther
away than `Tolerance.EqualPlanar`, which is how to check that geometry really is where you think it is.

```csharp
point.TryToPoint2(frame, out GeoPoint2 flat);   // false when the point is off the plane
point.ProjectToPoint2(frame);                   // always answers, by projecting
```

**Short sides.** A chain, a polygon or a face goes out and comes back side for side, however short a side: the
square 10 by 10 with a corner cut by 0.0005, laid out in a frame tilted 60 degrees to it, keeps its five corners,
where the global tolerance of a thousandth put the cut corner back, and a chain with a step of 0.0004 keeps the step
both ways. Only a side seen end-on, along the Z axis of the frame, goes. A polygon or a face seen edge-on is refused,
as one that encloses no area is when it is built, where it came out as a line of corners; one a ten-thousandth of a
radian off edge-on comes out as a sliver. A chain seen end-on, a point in the frame, is refused as it was.

**Winding.** A frame whose Z axis agrees with the shape's normal keeps the shape running the way it did;
one that opposes it mirrors the plan view, so a counter-clockwise polygon comes out clockwise. A frame
taken from the shape itself always agrees.

## Working in a local frame

`GeoCoordinateSystem3` moves geometry between world coordinates and a local frame, and its axes are always
orthonormal whatever is passed in. `ToLocal` and `ToGlobal` are exact inverses of each other.

Two directions span a frame by the angle between them, whatever their lengths: they are parallel where the sine of it
is within the global vector tolerance of 1E-5. Two directions 0.002 long and square to each other span a frame, where
the length of their cross product, 4E-6, was read against that tolerance and they were refused; two 10 long and 5E-6 of
a radian apart are parallel, where their cross product of 5E-4 let them through. An X direction of any length but none
is taken: (0, 6E-7, 8E-7) points the X axis along (0, 0.6, 0.8), where one shorter than 1E-5 was refused as having no
length, and so `TransformBy` follows a scaling however small, where one by less than 1E-5 was refused.

```csharp
var frame = new GeoCoordinateSystem3(
    new GeoPoint3(10, -20, 30),
    new GeoVector3(1, 1, 0),
    new GeoVector3(-1, 1, 1));

var point = new GeoPoint3(3, -7, 11);
frame.ToGlobal(frame.ToLocal(point)).IsEqualTo(point);  // true
```

`ToLocal` and `ToGlobal` take points and vectors. A whole shape goes through `TransformBy`:
`GeoTransform3.ToCoordinateSystem(frame)` takes it into the frame, to be worked on in local coordinates,
and `GeoTransform3.FromCoordinateSystem(frame)` puts the result back; neither inverts a matrix. A frame
given in the coordinates of another nests by multiplying, the outer one read into first:
`ToCoordinateSystem(inner) * ToCoordinateSystem(outer)`.

```csharp
GeoPolygon3 inFrame = outline.TransformBy(GeoTransform3.ToCoordinateSystem(frame));
GeoPolygon3 back = inFrame.TransformBy(GeoTransform3.FromCoordinateSystem(frame));
```

`GeoTransform3` is a 4x4 matrix applied on the left, so `a.Multiply(b)` means "apply b, then a". A plane
normal is carried by the inverse transpose rather than by the matrix itself, so it stays perpendicular to
the surface even under a non-uniform scaling.

```csharp
GeoTransform3 motion = GeoTransform3.Translation(new GeoVector3(10, 0, 0))
    .Multiply(GeoTransform3.RotationZ(Math.PI / 2));

motion.Transform(GeoPoint3.Origin);  // (10, 0, 0)
motion.Inverse().Transform(motion.Transform(GeoPoint3.Origin)); // back to the origin
```

## Tekla Structures

`GeometryHelper.TeklaConvert` converts geometry between Tekla Structures and this library: points, vectors,
segments, planes, coordinate systems, bounding boxes, transformation matrices, and the faces and loops of
a Tekla solid. It is a separate project, so the core library carries no dependency on Tekla.

```csharp
using GeometryHelper.TeklaConvert;

teklaSolid.TryToGeoSolid3(out GeoSolid3 body, tolerance);

body.GetVolume();
body.TrySubtract(otherBody, out GeoSolid3 left);
```

Three things are checked rather than trusted on the way in. A Tekla coordinate system whose Y axis is not
quite square to its X axis is squared up. Each face is turned to agree with the normal Tekla gives it, and
the finished body is turned the right way out, as `TurnOutwards()` turns one, if the whole surface arrived reversed —
without that, volume still measures the same but every containment query answers backwards. And a face
that cannot be made sense of is skipped rather than thrown on, which leaves the body no longer closed, so
take a volume from `TryGetVolume`, and trust it where that returns true.

Tekla models in millimetres with coordinates that can run to hundreds of thousands, and a face of a twelve
metre member is rarely flat to the last decimal. The default `EqualPlanar` is often too tight for that, so
pass a `Tolerance` suited to the model rather than relying on the default.
