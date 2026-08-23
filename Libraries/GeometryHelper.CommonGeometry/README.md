# GeometryHelper.CommonGeometry

The types that [PlaneGeometry](https://github.com/nguyenthanguth/ArrangeAlgorithms) and
[SolidGeometry](https://github.com/nguyenthanguth/ArrangeAlgorithms) both need. It exists so that a
program using the two libraries together sees one `Tolerance` and one `Angle` rather than two of
each, which is what happens when a shared type is copied into both.

It depends on nothing and knows nothing about either library.

## Tolerance

Every comparison in either library that can be affected by floating point error takes a `Tolerance`,
and every such method has an overload without one that reads `Tolerance.Global`. Neither library
compares coordinates with `==`.

| Threshold | Default | Measures |
|---|---|---|
| `EqualPoint` | `1E-4` | Distance below which two points are the same point. |
| `EqualVector` | `1E-4` | Difference below which two vectors are the same vector. |
| `EqualAngleRad` | 1° | Angular difference for parallel and perpendicular tests. |
| `EqualPlanar` | `1E-4` | Distance from a plane below which a point counts as lying on it. |

`EqualPlanar` is separate from `EqualPoint` because coplanarity is measured far from the reference
point. A face twelve metres long that is tilted by a hundredth of a degree deviates by about two
millimetres at its far end — far more than `EqualPoint` allows, yet still flat enough to work with.
Only SolidGeometry uses it.

```csharp
// One setting for both libraries.
Tolerance.Global = new Tolerance(equalPoint: 1E-3, equalVector: 1E-3);

// Or pass one explicitly, which is what to do when a single operation needs to be looser or
// tighter than the rest of the program.
bool same = first.IsEqualTo(second, new Tolerance(1E-6, 1E-6));
```

`Tolerance.Global` is a single shared setting. Changing it for a drawing in the plane changes it for
a model in space as well.

## Angle

An angle stored in radians and readable as either unit. There is deliberately no public constructor
taking a bare double: a number on its own does not say which unit it is in, so the unit is named at
the point of creation.

```csharp
Angle right = Angle.FromDegrees(90.0);
double radians = right.Radians;          // 1.5707963...

Angle turned = right + Angle.FromDegrees(300.0);
Angle wrapped = turned.Normalize();       // into [0, 2π)
Angle signed  = turned.NormalizeSigned(); // into (-π, π]
```

`Normalize` and `NormalizeSigned` differ in where they put the cut: the first is what to use for a
bearing, the second for a difference between two directions, where -170° is a nearer answer than
190°.

## PointLocation

Where a point sits relative to a shape: `Inside`, `OutSide`, or `OnSide`.

What counts as `Inside` depends on the family the shape belongs to. A volume encloses a region of
space. A region encloses an area — in space that means a point counts as inside only when it lies on
the carrier plane as well as within the boundary. A curve encloses nothing and never reports
`Inside`.

## PlaneSide

Which side of an oriented plane a point lies on: `Above`, `Below`, or `On`. The sides are named after
the plane normal rather than after world up, because a plane carries its own orientation and may
point anywhere.

## Licence

MIT.
