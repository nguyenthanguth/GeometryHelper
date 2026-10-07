# Volume take-off

`GeometryHelper.Takeoff` takes off the volumes of the parts of a model with each bit of material counted once. Where
a slab, a beam and a column meet, the block the three share belongs to the one ranked first, and the others lose it
once: the net volumes add up to the volume of all the parts together. The bodies given are never cut; only what they
share is.

## Quick start

```csharp
using GeometryHelper;
using GeometryHelper.Takeoff;

var items = new List<VolumeItem>
{
    new VolumeItem(slab,   "SLAB",   1),
    new VolumeItem(beam,   "BEAM",   2),
    new VolumeItem(column, "COLUMN", 3),
};

VolumeTakeoffResult[] results = VolumeTakeoff.Run(items);            // in the order of the items

foreach (VolumeTakeoffResult result in results)
{
    double m3 = result.NetVolume / 1E9;                               // mm3 to m3
    if (!result.IsExact) { /* result.Issues says what and which way */ }
}
```

`VolumeTakeoff.Run(items, options)` takes a `VolumeTakeoffOptions`: the `SolidBooleanOptions` every common part is
worked out with, and `MaxDegreeOfParallelism`, -1 for every processor (the default) or a count.

## Who keeps the overlap

The higher `Priority` keeps what two parts share and cuts it out of the lower. Of two the same, an item given an `Id`,
`new VolumeItem(solid, name, priority, id)`, keeps it from one without, the larger id from the smaller, and otherwise
the item earlier in the list. A Tekla part's `Identifier.ID` given as the id makes ties go as HDC WBS's WBSCalculator
takes them, the larger id cutting. Here the column keeps most: columns 3, beams 2, slabs 1. A column 400 by 400 and 3 200 high, a beam 300 wide, 600 deep and 6 000 long running through it,
and a slab 6 000 by 6 000 by 200 on both, their tops flush:

| Part | Gross | Taken off | Net |
|---|---|---|---|
| column | 0.512 m3 | | 0.512 m3 |
| beam | 1.080 m3 | 0.072 m3 to the column | 1.008 m3 |
| slab | 7.200 m3 | 0.032 m3 to the column, 0.336 m3 to the beam | 6.832 m3 |

The beam shares 0.360 m3 with the slab, but 0.024 m3 of it, the block 300 by 400 by 200 inside the column, is the
column's already: taken off by each part it meets, the slab would lose it twice. The three nets add up to 8.352 m3,
the volume of the three together. Two exact copies count once, the later coming out nought, and so does the same body
given twice; a part wholly inside one ranked before it comes out nought.

## What comes back

| Property | Meaning |
|---|---|
| `GrossVolume` | the part's own material, its openings cut out |
| `Deductions` | what each part ranked before it kept, `ByIndex` and `Volume`, in the order of their ranks |
| `DeductedVolume` | the deductions added up |
| `NetVolume` | gross less deducted, held between nought and the gross |
| `Issues` | what could not be worked out exactly, naming the other item by its index |
| `IsExact` | no issues |

A null entry in the items gets a result with no item, every volume nought and the issue "no item".

## How it is worked out

Each pair whose boxes overlap by more than the point tolerance has its common part worked out by `TryIntersect` of the
part ranked later with the part ranked first, which is put onto it as the contact says. Then each part's pieces, in
the order of their keepers, have the pieces before them taken out by `TrySubtractAll`, so that each deduction is what
that keeper took and no keeper before it. A common part thinner on average than half the point tolerance is taken as
touching: a beam flush under a slab loses nothing. Both steps run in parallel, the heaviest work first, and the
numbers are the same, bit for bit, on one thread or on every processor.

A common part the booleans cannot make, one that is not valid, holds more than the smaller part, or throws, is worked
out by slicing instead: the part is cut by parallel planes across the axis that needs the fewest, and the area of its
material within the keeper and outside every keeper ranked before is added up across them. Between two planes with no
corner, no edge through a face and no three faces meeting, the area is a polynomial of the second degree, so three
sections a layer give it exactly, and nothing is snapped to a tolerance. A piece whose cut of the pieces before it skips
one, or throws, or that has a keeper before it whose overlap was sliced, is sliced the same way. On a Tekla model a
curved wall of 354 faces and a curved girder of 322, their sides 0.16 to 0.48 apart and 0.05° off, share a common part
the booleans give as 4 256 faces, not valid, after a minute or two; slicing gives 2 226 236 967 mm3 in 0.04 seconds, the
same across any of the three axes, where Tekla's own boolean gives 2 226 236 773 cutting the wall and 2 226 236 807
cutting the girder.

## On a real model

A Tekla Structures model of 30 921 parts, read closed, ranked columns 8, walls 7, girders and beams 6, slabs 5, stairs
4, grout and base plates 3, steel fittings 2 and formwork 1, holds 69 040.90 m3 gross. Taken off with the contact and the
fallback that suit a Tekla model in millimetres,

```csharp
var options = new VolumeTakeoffOptions(new SolidBooleanOptions(
    Tolerance.Default,                                                   // 0.001
    0.01,                                                                // contact
    new Tolerance(0.01, 0.01, Tolerance.DefaultEqualAngleRad, 0.01)));   // fallback
```

it loses 1 671.73 m3 to the parts ranked before and keeps 67 369.17 m3 net, and not one of the 30 921 results carries
an issue: the common parts the booleans cannot make, the curved wall and girder above among them, are sliced. Cutting each part
with every part ranked before it that it meets, by `TrySubtractAll` with the same options, skips 23 cuts on 6 parts and
leaves that overlap in. The run takes 293 seconds on 24 processors and 438 on one, with the same numbers to the last
bit; the booleans on the curved pair alone take about two minutes of it before slicing takes over. Without the contact it takes 108 seconds
and keeps 67 369.16 m3, also with no issue, 8.5 litres less: the micron-thin sheets Tekla leaves between parts, which the
contact takes as touching, are taken off.

## What is promised, and what is not

- The net volumes add up to the volume of the parts together, within the tolerance and the contact: an overlap thinner
  than either comes back as touching and is not taken off.
- A common part the booleans cannot make is worked out by slicing, exactly over flat faces, and carries no issue.
  Slicing snaps nothing, so a sheet thinner than the contact, which the booleans put away as touching, is taken off as
  the faces give it: 194 mm3 more than Tekla on the curved pair.
- A pair that cannot be sliced either is not taken off: the net volume is then an upper bound, and the issue says so.
  Slicing needs every section crossed an even number of times, so it refuses a body with a corner more than about 1E-9
  off its neighbour's edge, a T-junction, even where `Validate` accepts the body.
- A common part is checked to be valid and to hold no more than the smaller part. Nothing bounds it from below, so a
  valid body can still have lost part of what the two share; for a take-off that matters, compare against the net bodies
  `TrySubtractAll` cuts.
- Only volumes are taken off. A formwork area, or the net bodies themselves, come from cutting each part by the parts
  ranked before it: `GeoSolid3.TrySubtractAll`, with closing where a cut would be skipped.
