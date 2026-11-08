# The Tekla bridge

`DrawingSourceConvert` in `GeometryHelper.TeklaConvert` turns Tekla model objects into a `DrawingSource`. It is the only
code of this work that touches Tekla, and it only reads.

## What it reads

| From Tekla | Into |
|---|---|
| an `Assembly`: its main part and secondaries, sub-assemblies | `DrawingObject`s of kind Part, `IsMain` on the main part, `ParentId` the assembly |
| a part's solid (`GetSolid`), through `SolidConvert.TryToGeoSolid3` | `Solid` |
| a part's coordinate system | `Frame` |
| position, profile, material, name, and the report properties the settings ask for | `Mark`, `Profile`, `Material`, `Name`, `Attributes` |
| bolt groups: positions, diameter, hole sizes, the parts bolted | objects of kind Bolt and Hole |
| welds: type, size, the parts joined, the path | objects of kind Weld |
| reinforcement (later), through `ReinforcementConvert` | objects of kind Rebar |

The exact calls are settled in G0 against the Open API of Tekla 2020, 2025 and 2026, since the bridge is built for each.

## Rules

- Read only: the model is never changed.
- Each Tekla version's build is tested in code, as the other TeklaConvert members are.
- A run against the maintainer's running Tekla needs their go-ahead first, every time.
- Logging as the bridges do: Warn for a caught exception, one aggregated Debug line for skipped objects, one Info summary
  per public call.
- Model data from the maintainer's projects never goes into the repository; tests build objects in code.

## What was built (G6 step 2)

`DrawingSourceConvert.FromAssembly(Assembly)`, `FromPart(Part)` and `FromObjects(IEnumerable<ModelObject>)` in
`src/GeometryHelper.TeklaConvert`. The converter is a thin layer of Tekla reads (`DrawingSourceConvert.cs`) over a small
read model (`DrawingSourceData.cs`: `TeklaPartData`, `TeklaBoltGroupData`, `TeklaWeldData`, `TeklaDrawingData`) and a
build that knows no Tekla (`DrawingSourceBuild.cs`), tested in code in `DrawingSourceConvertTests` for each Tekla
version. Everything is read in the current work plane, in millimetres; the model is never changed.

| Tekla | `DrawingObject` |
|---|---|
| a part | kind Part; id its GUID; body by `TryToGeoSolid3` (`Tolerance.Global` of the call); frame its coordinate system; `Name`; `Mark` PART_POS; `Profile`; `Material`; `ParentId` its assembly; `IsMain` the main part of its assembly; attributes ASSEMBLY_POS, LENGTH and WEIGHT, numbers to a hundredth with a point |
| a bolt group with bolts | kind Bolt, one for each of `BoltPositions`; id the group's GUID and "-1", "-2" and on; a cylinder of 24 sides of `BoltSize` along the group's Z axis, as long as the corners of the group's `GetSolid()` within a diameter of the axis reach, or where they give nothing, as the hole runs through the parts bolted that are read; frame X along the axis, origin at its start; `ParentId` `PartToBoltTo`; attributes BoltGroup, Count, Diameter, HoleDiameter (`BoltSize` + `Tolerance`), Length, Standard, BoltedParts |
| a bolt group of holes only (`Bolt` false) | kind Hole, as above but of the hole's diameter and through the parts bolted that are read: the corners of their bodies within 1.1 hole radii of the axis; no Length or Standard |
| a weld | kind Weld; id its GUID; body `GetSolid()`; frame its coordinate system, the world's where it has none; `ParentId` `MainObject`; attributes WeldType (Fillet, SquareButt, VButt, BevelButt, or Tekla's name for another kind), WeldSide (above the line Arrow, below Other, both Both), WeldSize (prefix and size of the arrow side, "a5"), WeldLength (the side's length, or LENGTH where that is nought), WeldSecondaryPart `SecondaryObject` |

- `FromAssembly` reads the main part, the secondaries and the sub-assemblies, then the bolt groups and welds of every
  part read, and sets the source in the main part's frame. `FromPart` reads one part with its bolts and welds, in its
  frame. `FromObjects` reads assemblies, parts, bolt groups and welds, the bolts and welds of every part read too, and
  leaves the frame to the rule of `DrawingSource` (the main part with the lowest id).
- An object reached twice is read once. An object of another kind, one with no GUID, a part whose solid or frame cannot
  be read, a weld whose solid cannot be read, a bolt group with no positions and a bolt of no length along its axis are
  left out and counted in one Debug line per call, by reason with their ids; an object Tekla cannot describe is one Warn
  with the exception; each call ends with one Info line of the objects by kind.
- Each `GeometryHelper.TeklaConvert.{year}` package now depends on `GeometryHelper.Drawing` (and through it on
  `GeometryHelper.Dxf.Viewer`). Neither is packable yet, so the packages cannot be installed from NuGet until they are
  (D4) or the reference is otherwise settled.

## Live check, with the maintainer's go-ahead

Only these lines need a running Tekla; none has been run against one. Each is a read.

1. `IdOf`: `Identifier.GUID` is filled on objects from `GetMainPart`, `GetSecondaries`, `GetBolts`, `GetWelds`,
   `PartToBoltTo`, `PartToBeBolted`, `GetOtherPartsToBolt`, `MainObject` and `SecondaryObject`, or `Select()` fills it;
   the ids are the same from one run to the next.
2. `AddAssembly`: `GetMainPart`, `GetSecondaries` and `GetSubAssemblies` give every part of an assembly with
   sub-assemblies once.
3. `AddPart`: `GetSolid()` comes with cuts, fittings and bolt holes in the current work plane (the holes of a group of
   holes only are found in it); `GetCoordinateSystem()` has X along the part's length; PART_POS, ASSEMBLY_POS, LENGTH
   (mm) and WEIGHT (kg) are read; `GetAssembly()` and `GetMainPart()` give the parent and the main flag of a part
   selected alone.
4. `AddConnections`: `GetBolts()` and `GetWelds()` of a part give every bolt group and weld that touches it, site bolts
   included.
5. `AddBoltGroup`: the bolts run along the Z axis of the group's `GetCoordinateSystem()`; `BoltPositions` are in the
   current work plane on the group's plane; `GetSolid()` holds the bolts, washers and nuts, so that an M20 bolt 60 long
   comes out 60 long and where Tekla draws it; `Tolerance` is the hole's clearance (22 for M20 with 2).
6. `AddWeld`: Tekla's side above the line is the arrow side (ISO 2553); `PrefixAboveLine` is "a" for a fillet's
   throat; `LengthAbove` is nought for a weld along the whole joint and LENGTH then its length; `GetSolid()` reads for
   welds and polygon welds; `MainObject` is the part welded to.
7. Speed: TC10, 200 assemblies like TC5, read with `FromObjects`; how long the remoting calls take.
