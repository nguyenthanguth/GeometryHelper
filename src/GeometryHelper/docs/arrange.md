# Label placement

2D label placement for engineering drawings: given a set of labels, each belonging to a leader and
keeping clear of blocked regions, `Arranger.Run` works out how far to move each label so that none
overlaps another or encroaches on the blocked regions.

It depends on AutoCAD and Tekla for nothing. The geometry it works in is [geometry in the
plane](plane.md), and its own types live in the `GeometryHelper.Arranging` namespace.

## Visual Examples

### AutoCAD Integration
Here are some examples of labels arranged inside AutoCAD to avoid overlaps and blocked regions:

| Greedy | Force Directed |
|:---:|:---:|
| ![Greedy](https://raw.githubusercontent.com/nguyenthanguth/GeometryHelper/main/examples/GeometryHelper.ArrangeAlgorithms.CadTest/img/ex-result-cad1.png) | ![Force Directed](https://raw.githubusercontent.com/nguyenthanguth/GeometryHelper/main/examples/GeometryHelper.ArrangeAlgorithms.CadTest/img/ex-result-cad2.png) |

### Tekla Structures Integration
Here is an example of reinforcement marks before and after arrangement:

| Before Arrangement | After Arrangement |
|:---:|:---:|
| ![Before Arrangement](https://raw.githubusercontent.com/nguyenthanguth/GeometryHelper/main/examples/GeometryHelper.ArrangeAlgorithms.TeklaTest/img/ex-from.png) | ![After Arrangement](https://raw.githubusercontent.com/nguyenthanguth/GeometryHelper/main/examples/GeometryHelper.ArrangeAlgorithms.TeklaTest/img/ex-result.png) |

| Arranged Marks Avoiding Dimension Obstacles |
|:---:|
| ![Tekla Result Detail](https://raw.githubusercontent.com/nguyenthanguth/GeometryHelper/main/examples/GeometryHelper.ArrangeAlgorithms.TeklaTest/img/ex-result-2.png) |

## Quick Start

```csharp
var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

var items = new List<ArrangeItem>
{
    new ArrangeItem
    {
        // The label's box: centre, width, height, rotation angle (radians, counter-clockwise)
        Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
        // The leader: the candidate positions spread out from its midpoint
        Leader = leader,
        // The least gap between the label's edge and the leader, for this label alone (default 50)
        Offset = 50.0,
        // What the label must not overlap
        BlockPolygons = new List<GeoPolygon2>(),
        BlockLines    = new List<GeoLine2>()
    }
};

// One result for each item, in the same order. The items themselves are left as they were given.
ArrangeResult[] results = Arranger.Run(items);

for (int i = 0; i < items.Count; i++)
{
    GeoPoint2 newCentre = items[i].Box.Center + results[i].Translation;
    bool placed = results[i].Placed; // false: no clear place was found, and the label overlaps something
}
```

`Arranger.Run` only reads the items. What becomes of each comes back as an `ArrangeResult`: `Translation`,
how far to move the label, and `Placed`, whether it ends up clear of every other label and of every block.
The same list can therefore be run again, with other options or on another thread, to compare. A null entry
is passed over and answered with `default`: not moved, not placed.

A run goes over the labels twice. The first pass places every label under every constraint. The labels it
leaves overlapping something are tried once more with the block lines lifted, keeping clear of the labels
already placed. `Placed` is judged afterwards, on the final layout as a whole, so a label that another one fell
back onto is not reported clear.

To change the algorithm or fine-tune parameters, pass `ArrangeOptions`:

```csharp
var options = new ArrangeOptions
{
    Algorithm           = ArrangeAlgorithmType.BoundedBacktracking,
    RowGap              = 20.0,
    PerpendicularLevels = 3
};

ArrangeResult[] results = Arranger.Run(items, options);
```

`ArrangeOptions` is the shared configuration for the entire list. `Offset` is set per `ArrangeItem` because each label may require a different offset:

```csharp
var smallTextLabel = new ArrangeItem
{
    Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
    Leader = leader,
    Offset = 50.0   // small text, closely sticks to the leader
};

var largeTextLabel = new ArrangeItem
{
    Box    = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 4000.0, 2000.0),
    Leader = leader,
    Offset = 200.0  // large text, must move further away
};

ArrangeResult[] results = Arranger.Run(new List<ArrangeItem> { smallTextLabel, largeTextLabel }, options);
```

## Candidate Positions Generation

All 5 algorithms share the same set of discrete candidate positions, expanding from the midpoint of the leader;
`ArrangeItem.GetPlacePoints(options)` lists them:

- **Perpendicular Translation** — each level in `PerpendicularLevels` creates a row of labels, symmetric on both sides of the leader. The first level is placed at half the label height plus the label's own `Offset`. Each subsequent level adds the label height plus `RowGap`.
- **Longitudinal Sliding** — in each row, the label slides parallel to the leader in both directions, up to a maximum of half the leader's length plus `LongitudinalOvershootRatio` times the label width.

The algorithms only differ in how they **select** from this candidate set.

## Five Algorithms

| `ArrangeAlgorithmType` | Selection Strategy | Trade-off |
|---|---|---|
| `Greedy` (default) | Sequentially places labels, prioritizing the most constrained ones; selects the most open spot in the first group of free candidates | Fastest, reproducible results, but prone to local optima |
| `BoundedBacktracking` | Same as Greedy, but backtracks when subsequent labels are stuck, bounded by `MaxBacktrackSteps` | Higher clean placement rate, slower on crowded drawings |
| `SimulatedAnnealing` | Global optimization based on a collision-penalty energy function, gradually cooling down | Best for extremely crowded drawings, CPU-heavy |
| `ForceDirected` | Simulates spring and repulsive forces, then maps to the nearest discrete candidate | Distributes labels evenly and naturally |
| `ConstraintSatisfaction` | CSP with MRV heuristic and forward checking | Most rigorous, potential combinatorial explosion with large number of labels |

`BoundedBacktracking` and `ConstraintSatisfaction` automatically fallback to `Greedy` if no collision-free solution is found, ensuring every label always has a display position.

`SimulatedAnnealing` uses a fixed seed, so its results are reproducible between runs.

## Parameters of each `ArrangeItem`

| Parameter | Default | Meaning |
|---|---|---|
| `Box` | — | The label's box, the rectangle that is moved |
| `Leader` | — | The segment the label belongs to; its midpoint is the origin of the candidate positions |
| `Offset` | 50.0 | The least gap between the label's edge and the leader |
| `BlockPolygons` | empty | Regions the label must not overlap |
| `BlockLines` | empty | Segments the label must not overlap; lifted in the second pass, and a label left across one is not `Placed` |

The blocks of all the items are gathered into one set before any label is placed, so every label keeps clear of
the blocks of every item, and a block given to many items is tested once.

## Main Parameters of `ArrangeOptions`

| Parameter | Default | Meaning |
|---|---|---|
| `Algorithm` | `Greedy` | Algorithm to use |
| `RowGap` | 20.0 | Clearance between two consecutive rows of labels |
| `PerpendicularLevels` | 3 | Number of perpendicular fallback levels to test on each side |
| `LongitudinalOvershootRatio` | 0.75 | Ratio of label width allowed to overshoot beyond the two endpoints of the guide segment |
| `MinimumBoxSize` | 10.0 | Labels smaller than this size are ignored |
| `MinimumMoveDistance` | 0.1 | Translations smaller than this threshold are rounded to zero |
| `NeighbourMargin` | 50.0 | Expanded margin when filtering nearby obstacles |
| `PlaceMostConstrainedFirst` | true | Place labels with fewer options first |
| `PlaceFromInsideOut` | true | Prioritize labels close to the area centroid |
| `LookAheadCandidates` | 3 | Number of free positions considered before selection |
| `MaxBacktrackSteps` | 1000 | Cap on the number of backtracking steps |
| `AnnealingInitialTemperature` | 100.0 | Initial temperature for the Simulated Annealing algorithm |
| `AnnealingCoolingRate` | 0.95 | Cooling rate for the Simulated Annealing algorithm |
| `ForceIterations` | 100 | Number of force simulation iterations for the Force-Directed algorithm |
| `Tolerance` | `Tolerance.Global` | Tolerance for geometric comparisons; `Tolerance.Global` as it stands when the options are made |

Default values are in millimeters, matching conventional structural drawings.

## Running inside AutoCAD

`GeometryHelper.ArrangeAlgorithms.CadTest` builds a DLL file to be loaded into AutoCAD:

```bash
dotnet build examples/GeometryHelper.ArrangeAlgorithms.CadTest/GeometryHelper.ArrangeAlgorithms.CadTest.csproj
```

The output is located at `examples/GeometryHelper.ArrangeAlgorithms.CadTest/bin/Debug/net48/GeometryHelper.ArrangeAlgorithms.CadTest.dll`. Load this file into AutoCAD using the `NETLOAD` command, then run one of the following commands: `T1_Greedy`, `T1_BoundedBacktracking`, `T1_SimulatedAnnealing`, `T1_ForceDirected`, `T1_ConstraintSatisfaction`. Select LINE or LWPOLYLINE objects, and the plugin will draw the label box before and after arrangement, along with statistics.

The project compiles against three AutoCAD assemblies — `accoremgd`, `acdbmgd`, `acmgd` — committed under `src/GeometryHelper.CadConvert/Lib` and referenced from there by relative path, so no AutoCAD installation is needed to build. Loading the result still needs AutoCAD, which supplies those assemblies at run time.

## Running inside Tekla Structures

`GeometryHelper.ArrangeAlgorithms.TeklaTest` is a console application that connects to the active Tekla Structures model and drawing to arrange reinforcement marks.

To build and run:
1. Open Tekla Structures and open a drawing with some reinforcement marks and dimensions selected.
2. Build the project:
   ```bash
   dotnet build examples/GeometryHelper.ArrangeAlgorithms.TeklaTest/GeometryHelper.ArrangeAlgorithms.TeklaTest.csproj
   ```
3. Run the compiled executable:
   ```bash
   examples/GeometryHelper.ArrangeAlgorithms.TeklaTest/bin/Debug/net48/GeometryHelper.ArrangeAlgorithms.TeklaTest.exe
   ```

## Licence

MIT.
