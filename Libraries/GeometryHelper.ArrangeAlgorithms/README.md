# GeometryHelper.ArrangeAlgorithms

[![NuGet Version](https://img.shields.io/nuget/v/GeometryHelper.ArrangeAlgorithms.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.ArrangeAlgorithms/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GeometryHelper.ArrangeAlgorithms.svg?style=flat-square)](https://www.nuget.org/packages/GeometryHelper.ArrangeAlgorithms/)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=flat-square)](https://github.com/nguyenthanguth/ArrangeAlgorithms/blob/main/LICENSE)

2D label placement for engineering drawings: given a set of labels, each associated with a guide
segment and surrounding blocked regions, the library calculates translation vectors that keep labels
from overlapping each other and from encroaching on the blocked regions.

It depends on AutoCAD and Tekla for nothing. The geometry it works in comes from
[GeometryHelper.PlaneGeometry](https://www.nuget.org/packages/GeometryHelper.PlaneGeometry/), which used to ship inside this package
and became one of its own in 3.0.0.

> **Moving from 2.x?** The geometric types moved to the `GeometryHelper.PlaneGeometry` package and gained a `2`
> suffix, and `Tolerance` moved to `GeometryHelper.CommonGeometry`. See
> [What changed in 3.0.0](https://github.com/nguyenthanguth/ArrangeAlgorithms#what-changed-in-300).

## Installation

```bash
dotnet add package GeometryHelper.ArrangeAlgorithms
```

`GeometryHelper.PlaneGeometry` and `GeometryHelper.CommonGeometry` come with it as dependencies.

## Visual Examples

### AutoCAD Integration
Here are some examples of labels arranged inside AutoCAD to avoid overlaps and blocked regions:

| Greedy | Force Directed |
|:---:|:---:|
| ![Greedy](https://raw.githubusercontent.com/nguyenthanguth/ArrangeAlgorithms/main/ArrangeAlgorithms.CadTest/img/ex-result-cad1.png) | ![Force Directed](https://raw.githubusercontent.com/nguyenthanguth/ArrangeAlgorithms/main/ArrangeAlgorithms.CadTest/img/ex-result-cad2.png) |

### Tekla Structures Integration
Here is an example of reinforcement marks before and after arrangement:

| Before Arrangement | After Arrangement |
|:---:|:---:|
| ![Before Arrangement](https://raw.githubusercontent.com/nguyenthanguth/ArrangeAlgorithms/main/ArrangeAlgorithms.TeklaTest/img/ex-from.png) | ![After Arrangement](https://raw.githubusercontent.com/nguyenthanguth/ArrangeAlgorithms/main/ArrangeAlgorithms.TeklaTest/img/ex-result.png) |

| Arranged Marks Avoiding Dimension Obstacles |
|:---:|
| ![Tekla Result Detail](https://raw.githubusercontent.com/nguyenthanguth/ArrangeAlgorithms/main/ArrangeAlgorithms.TeklaTest/img/ex-result-2.png) |

## Quick Start

```csharp
var leader = new GeoLine2(0.0, 0.0, 2000.0, 0.0);

var arranges = new List<Arrange>
{
    new Arrange
    {
        // Label bounding box: center, width, height, rotation angle (radians, counter-clockwise)
        GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
        // Guide segment: its midpoint is the origin for candidate positions expansion
        GeoLine2      = leader,
        // Minimum perpendicular offset between label edge and guide segment, specific to this label (default 50)
        MarkOffsetFromLine = 50.0,
        // Blocked regions the label must not overlap
        BlockPolygons = new List<GeoPolygon2>(),
        BlockLines    = new List<GeoLine2>()
    }
};

// Returns translation vector for each label, in the exact input order.
// Each Arrange object is also automatically updated: arranges[i].TranslationVector contains the same vector.
List<GeoVector2> moves = Arrange.Run(arranges);

for (int i = 0; i < arranges.Count; i++)
{
    // You can use the returned 'moves[i]' or read the property directly:
    GeoVector2 move = arranges[i].TranslationVector; 
    
    GeoPoint2 newPosition = arranges[i].GeoRectangle2.Center + move;
    bool isPlaced = arranges[i].Placed; // false = forced to fallback, still has overlap
}
```

To change the algorithm or fine-tune parameters, pass `ArrangeOptions`:

```csharp
var options = new ArrangeOptions
{
    Algorithm           = ArrangeAlgorithmType.BoundedBacktracking,
    RowGap              = 20.0,
    PerpendicularLevels = 3
};

List<GeoVector2> moves = Arrange.Run(arranges, options);
```

`ArrangeOptions` is the shared configuration for the entire list. `MarkOffsetFromLine` is set per `Arrange` because each label may require a different offset:

```csharp
var smallTextLabel = new Arrange
{
    GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 2000.0, 1000.0),
    GeoLine2      = leader,
    MarkOffsetFromLine = 50.0   // small text, closely sticks to guide segment
};

var largeTextLabel = new Arrange
{
    GeoRectangle2 = new GeoRectangle2(new GeoPoint2(1000.0, 0.0), 4000.0, 2000.0),
    GeoLine2      = leader,
    MarkOffsetFromLine = 200.0  // large text, must move further away
};

List<GeoVector2> moves = Arrange.Run(new List<Arrange> { smallTextLabel, largeTextLabel }, options);
```

## Candidate Positions Generation

All 5 algorithms share the same set of discrete candidate positions, expanding from the midpoint of the guide segment:

- **Perpendicular Translation** — each level in `PerpendicularLevels` creates a row of labels, symmetric on both sides of the guide segment. The first level is placed at half the label height plus the label's own `MarkOffsetFromLine`. Each subsequent level adds the label height plus `RowGap`.
- **Longitudinal Sliding** — in each row, the label slides parallel to the guide segment in both directions, up to a maximum of half the guide segment length plus `LongitudinalOvershootRatio` times the label width.

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

## Parameters for each `Arrange`

| Parameter | Default | Meaning |
|---|---|---|
| `GeoRectangle2` | — | Label bounding box, the geometry that will be translated |
| `GeoLine2` | — | Guide segment; its midpoint is the origin for candidate positions expansion |
| `MarkOffsetFromLine` | 50.0 | Minimum perpendicular offset between label edge and guide segment |
| `BlockPolygons` | — | Blocked polygons that the label must not overlap |
| `BlockLines` | — | Blocked line segments that the label must not overlap |

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
| `Tolerance` | `Tolerance.Global` | Tolerance for geometric comparisons |

Default values are in millimeters, matching conventional structural drawings.

## Running inside AutoCAD

`GeometryHelper.ArrangeAlgorithms.CadTest` builds a DLL file to be loaded into AutoCAD:

```bash
dotnet build Samples/GeometryHelper.ArrangeAlgorithms.CadTest/GeometryHelper.ArrangeAlgorithms.CadTest.csproj
```

The output is located at `Samples/GeometryHelper.ArrangeAlgorithms.CadTest/bin/Debug/net48/GeometryHelper.ArrangeAlgorithms.CadTest.dll`. Load this file into AutoCAD using the `NETLOAD` command, then run one of the following commands: `T1_Greedy`, `T1_BoundedBacktracking`, `T1_SimulatedAnnealing`, `T1_ForceDirected`, `T1_ConstraintSatisfaction`. Select LINE or LWPOLYLINE objects, and the plugin will draw the label box before and after arrangement, along with statistics.

The project references three DLLs: `accoremgd`, `acdbmgd`, `acmgd` via the `AutoCadPath` declared in the `.csproj` file. If those DLLs are located elsewhere on your machine, edit the `AutoCadPath` line.

## Running inside Tekla Structures

`GeometryHelper.ArrangeAlgorithms.TeklaTest` is a console application that connects to the active Tekla Structures model and drawing to arrange reinforcement marks.

To build and run:
1. Open Tekla Structures and open a drawing with some reinforcement marks and dimensions selected.
2. Build the project:
   ```bash
   dotnet build Samples/GeometryHelper.ArrangeAlgorithms.TeklaTest/GeometryHelper.ArrangeAlgorithms.TeklaTest.csproj
   ```
3. Run the compiled executable:
   ```bash
   Samples/GeometryHelper.ArrangeAlgorithms.TeklaTest/bin/Debug/net48/GeometryHelper.ArrangeAlgorithms.TeklaTest.exe
   ```

## Licence

MIT.
