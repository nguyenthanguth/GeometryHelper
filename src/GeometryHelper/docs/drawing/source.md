# Sources

What a drawing is made from: a [`DrawingSource`](xref:GeometryHelper.Drawing.DrawingSource) of
[`DrawingObject`](xref:GeometryHelper.Drawing.DrawingObject)s, each a body in model millimetres with a kind, a frame and the
texts its marks and the part list show. A source knows nothing of where it came from: a bridge fills it from Tekla Structures,
and a test builds it in code.

## An object

```csharp
var plate = new DrawingObject("P-1", DrawingObjectKind.Part, body, frame,
    name: "PLATE", mark: "P1", profile: "PL20*200", material: "S355", parentId: "A-1", isMain: true);
```

| Member | Holds |
|---|---|
| `Id` | the key of the object, unique in its source and the same from one run to the next: Tekla's GUID, or one given in code. Compared character by character, case and all. An update recognises an object by it |
| `Kind` | a [`DrawingObjectKind`](xref:GeometryHelper.Drawing.DrawingObjectKind), which decides how the drawing shows it |
| `Solid` | the body, a [`GeoSolid3`](xref:GeometryHelper.Geometry.GeoSolid3), closed and wound outwards as the library makes them |
| `Frame` | the object's own [`GeoCoordinateSystem3`](xref:GeometryHelper.Geometry.GeoCoordinateSystem3): for a member, X along its length and Y up in its cross-section, as Tekla gives a part's |
| `Name`, `Mark`, `Profile`, `Material` | texts, empty where not given; `Mark` is the position mark, `P1` |
| `ParentId` | the id of the assembly it belongs to, or null |
| `IsMain` | whether it is the main part of its assembly, whose frame a drawing takes |
| `Attributes` | further named values, kept in the order of their names |

An object does not change once made. The constructor refuses a null id or solid, an empty id, a kind that is not one of the
five, a frame whose axes are not unit and square, and an attribute with a null name or value or a name given twice.

| Kind | Is | How it is drawn |
|---|---|---|
| `Part` | a plate, a profile, a tube, a concrete member | its outline, marked and dimensioned |
| `Bolt` | a bolt with its nut and washers | with its symbol, dimensioned by its centre, marked with its group's count and size |
| `Hole` | a hole through parts, drawn alone where no bolt fills it | dimensioned by its centre |
| `Weld` | a weld joining parts | its symbol |
| `Rebar` | a bar or a stirrup | drawn inside the concrete it lies in |

### Attributes the drawing reads

An attribute is a name and a text. These are the names the drawing reads:

| Name | Read by | Holds |
|---|---|---|
| `Length` | the Length field of a mark | the length in millimetres, as written; else how far the body reaches along the frame's X (the part list always takes the reach, rounded to a millimetre) |
| `Diameter` | the Diameter field of a bolt mark | `"20"`, shown as `M20`; else twice the furthest a corner of the body stands off its axis |
| `Elevation` | the level marks of a general arrangement | the level of the object's top in model millimetres, `"3600"`, with a point for decimals |
| `WeldType`, `WeldSide`, `WeldSize`, `WeldLength`, `WeldSecondaryPart` | the weld symbol | the kind by the names of [`WeldType`](xref:GeometryHelper.Drawing.Annotation.WeldType), `Fillet`, `SquareButt`, `VButt` or `BevelButt`; the side by those of [`WeldSide`](xref:GeometryHelper.Drawing.Annotation.WeldSide), `Arrow`, `Other` or `Both`; `"a5"`; `"200"`; the id of the part welded on |
| any name | a mark whose format has an `Attribute` field | the value, empty where the object has none |

## A source

```csharp
var source = new DrawingSource(objects);                     // the frame of the main object
var source = new DrawingSource(objects, frame);              // a frame given
source.Objects;                                              // in the order of the ids, character by character
source.TryGetObject("B1", out DrawingObject beam);
```

The objects are kept in the order of their ids, so a drawing never follows the order the model gave them in: a beam "B-2" and
its end plate "B-1" come out plate first however they were passed. Two objects of one id, or a null object, are refused.

**The frame** is the one the views look along: X along the length of what is drawn, Y up in its cross-section. Given none, it
is the frame of the main object, the one marked `IsMain` with the lowest id; with none marked, the frame of the object with
the lowest id; with no objects at all, the world's. A beam 3 000 long lying along the model's Y is then drawn as one lying
along X: its front view shows its length across the sheet whichever way it lies in the model. A frame given that is not a
frame of three unit axes square to each other is refused.

## A source in code

The examples of these pages are drawn from two objects built in code: a beam, an I-section 290 deep and 300 wide with flanges
of 14 and a web of 8.5, 3 000 long along the model's X, and an end plate 300 by 340 by 20 at its far end with four holes of
22 (24-sided) on a grid of 200 by 240.

```csharp
static GeoPoint2 P(double x, double y) => new GeoPoint2(x, y);
static GeoPolygon2 Rect(double x0, double y0, double x1, double y1) => new GeoPolygon2(P(x0, y0), P(x1, y0), P(x1, y1), P(x0, y1));

static GeoPolygon2 Circle(double cx, double cy, double r, int n = 24)
{
    var corners = new GeoPoint2[n];
    for (int i = 0; i < n; i++)
    {
        double a = 2 * Math.PI * i / n;
        corners[i] = P(cx + r * Math.Cos(a), cy + r * Math.Sin(a));
    }
    return new GeoPolygon2(corners);
}

static GeoPolygon2 ISection(double depth, double width, double flange, double web)
{
    double w = width / 2, d = depth / 2, t = web / 2, f = d - flange;
    return new GeoPolygon2(P(-w, -d), P(w, -d), P(w, -f), P(t, -f), P(t, f), P(w, f), P(w, d), P(-w, d), P(-w, f), P(-t, f), P(-t, -f), P(-w, -f));
}

public static DrawingSource BeamWithEndPlate(double length = 3000)
{
    Tolerance tolerance = Tolerance.Default;
    var beamFrame = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.XAxis, GeoVector3.ZAxis);     // X along it, Y up
    var section = new GeoCoordinateSystem3(GeoPoint3.Origin, GeoVector3.YAxis, GeoVector3.ZAxis);       // where a section lies
    GeoSolid3 beam = GeoSolid3.Extrude(ISection(290, 300, 14, 8.5), section, length, tolerance);

    var plateAt = new GeoCoordinateSystem3(new GeoPoint3(length, 0, 0), GeoVector3.YAxis, GeoVector3.ZAxis);
    var face = new GeoFace2(Rect(-150, -170, 150, 170),
        new[] { Circle(-100, -120, 11), Circle(100, -120, 11), Circle(-100, 120, 11), Circle(100, 120, 11) });
    GeoSolid3 plate = GeoSolid3.Extrude(face, plateAt, 20, tolerance);

    return new DrawingSource(new[]
    {
        new DrawingObject("B1", DrawingObjectKind.Part, beam, beamFrame, "BEAM", "B1", "HEA300", "S355", "B1", true),
        new DrawingObject("P1", DrawingObjectKind.Part, plate, plateAt, "END PLATE", "P1", "PL20*300", "S355", "B1"),
    });
}
```

`GeoSolid3.Extrude(profile, coordinateSystem, height, tolerance)` takes the profile in the plane of the system and raises it
along its Z (see [Closing, making, weighing and merging](../solid/making.md#making-bodies)); a face with holes gives a body
with the holes through it. Here the beam's frame has X along the length, so the source's frame is the beam's, and the front
view looks at the beam from the side with its web upright. A source of 1 000 columns of 200 by 200 on a grid of 3 000 is
made the same way, one object each, with the attribute `Elevation`.

## Reading Tekla Structures

`GeometryHelper.TeklaConvert` reads Tekla model objects into a source, in the current work plane and in millimetres, and
never changes the model. It is one of the three bridges, built once for each Tekla version (see the
[Tekla Structures bridge](../../../GeometryHelper.TeklaConvert/README.md)).

| Call | Reads | Frame of the source |
|---|---|---|
| [`DrawingSourceConvert.FromAssembly(assembly)`](xref:GeometryHelper.TeklaConvert.DrawingSourceConvert) | the main part, the secondaries and those of the sub-assemblies, with the bolts and welds of them all | the main part's |
| `FromPart(part)` | one part with its bolts and welds | the part's own |
| `FromObjects(objects)` | a selection: assemblies as `FromAssembly` reads them, parts, bolt groups and welds; any other object is left out | the rule of the source, over the main part with the lowest id |

```csharp
var objects = new List<ModelObject>();
ModelObjectEnumerator selected = new Tekla.Structures.Model.UI.ModelObjectSelector().GetSelectedObjects();
while (selected.MoveNext()) objects.Add(selected.Current);

DrawingSource source = DrawingSourceConvert.FromObjects(objects);
Drawing drawing = DrawingBuilder.Build(source, DrawingSettings.Assembly, Tolerance.Default);
```

That code is compiled against the bridge for Tekla 2025; it has not been run against a running Tekla Structures, which needs
the maintainer's go-ahead.

**A part** becomes a `Part`: its body read as `SolidConvert.TryToGeoSolid3` reads a solid, its cuts and holes in it, its
coordinate system as its frame (X along its length), its name, its position (PART_POS) as its mark, its profile and
material, its assembly as its parent, and the report properties ASSEMBLY_POS, LENGTH and WEIGHT as attributes under those
names. Its id is its Tekla GUID.

**A bolt group** gives one object for each position: a `Bolt`, or a `Hole` where the group has holes only. Each is made from
the group's data, not from Tekla's faceted body: a cylinder of 24 sides, of the bolt's diameter or the hole's, along the
group's Z axis, with its frame's X along that axis. A bolt runs as far as the body Tekla draws for it, head, washers and nut;
a hole runs through the parts bolted that are read too. An M20 group of four, 60 long, through a plate 20 thick gives four
bolts, each with the attributes `BoltGroup` (the group's GUID), `Count` "4", `Diameter` "20", `HoleDiameter` "22" with Tekla's 2 of
tolerance, `Length` "60", `Standard` and `BoltedParts` (the GUIDs of the parts bolted, in order, a comma between); their parent
is the part bolted to, and their ids are the group's GUID and a number, "-1", "-2" and on.

**A weld** becomes a `Weld`: its body, the part welded to as its parent, and what its symbol reads, by the names above: a
fillet weld of 5 above the line, 200 long, is `WeldType` "Fillet", `WeldSide` "Arrow", `WeldSize` "a5", `WeldLength` "200", and
`WeldSecondaryPart` the GUID of the part welded on.

**What is left out.** An object of a kind not drawn, one whose solid cannot be read, and a bolt with no length along its axis
are left out and counted in one Debug line of [`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog); an object Tekla
cannot describe is a Warn and is left out; each call ends with one Info line of what it read. An object reached twice, a part
through its assembly and through its own selection, or a bolt group through each part it bolts, is read once.

## See also

- [Views, projection, sections and details](views.md): what is made of the objects.
- [Settings, scale and sheet](settings.md): the presets that decide which views and rules a source gets.
- [Tekla Structures](../../../GeometryHelper.TeklaConvert/README.md): the bridge, its packages and its versions.
- [Between the plane and space, and local frames](../solid/frames.md): the frames the objects are set in.
