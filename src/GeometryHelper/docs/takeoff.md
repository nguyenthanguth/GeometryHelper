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
    new VolumeItem(slab,   "SLAB",   3),
    new VolumeItem(beam,   "BEAM",   2),
    new VolumeItem(column, "COLUMN", 0),
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

The lower `Priority` keeps what two parts share; of two the same, the item earlier in the list. Rank the parts by what
is poured or set first: columns 0, walls 1, beams 2, slabs 3. A column 400 by 400 and 3 200 high, a beam 300 wide, 600
deep and 6 000 long running through it, and a slab 6 000 by 6 000 by 200 on both, their tops flush:

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

## What is promised, and what is not

- The net volumes add up to the volume of the parts together, within the tolerance and the contact: an overlap thinner
  than either comes back as touching and is not taken off.
- A common part that is not valid, or holds more than the smaller part, is read by a cut instead, as a number with no
  body; what it shares with the overlaps before it may be taken off twice, and its issue says so. A common part that
  comes back not valid with more than four times the faces of the two parts is not read by a cut, which would take far
  longer for no better answer: on a Tekla model of 30 921 parts, one girder against one wall gave 4 256 faces, and its
  cut 80 805 after 37 minutes, neither valid. Such a pair is not taken off, and its issue says so.
- A pair that cannot be worked out either way is not taken off: the net volume is then an upper bound. A cut that cannot
  be taken once leaves an overlap counted twice: the net volume is then a lower bound. The issue says which.
- A common part is checked to be valid and to hold no more than the smaller part. Nothing bounds it from below, so a
  valid body can still have lost part of what the two share; for a take-off that matters, compare against the net bodies
  `TrySubtractAll` cuts.
- Only volumes are taken off. A formwork area, or the net bodies themselves, come from cutting each part by the parts
  ranked before it: `GeoSolid3.TrySubtractAll`, with closing where a cut would be skipped.
