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
