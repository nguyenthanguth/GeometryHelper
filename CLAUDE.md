# CLAUDE.md

Guidance for Claude Code, and any other coding agent, working in this repository. Part one is a set of working habits
adapted from [andrej-karpathy-skills](https://github.com/multica-ai/andrej-karpathy-skills); part two is what is
particular to GeometryHelper. Where the two differ, part two wins.

The habits lean towards care over speed. For a trivial change, use judgement.

## Part one: how to work

### 1. Think before coding

Don't assume, don't hide confusion, and say what the trade-offs are.

- State what a change rests on: the units, the tolerance, whether a face is flat, whether a body is closed. Where one is
  uncertain, check it in the code or ask.
- When a request can be read two ways, set out both rather than picking one quietly. "Make this body closed" can mean
  welding a crack or filling a hole, and the two change the volume very differently.
- If there is a simpler way, say so, and push back where it is warranted.
- When something is unclear, stop, name what is unclear, and ask.

### 2. Simplicity first

The least code that solves the problem, and nothing speculative.

- No features, options, overloads or abstractions beyond what was asked for, and no configurability nobody requested.
- No handling for states that cannot happen. Near-degenerate geometry is not one of them: slivers, near-coincident
  faces and faces a hair off flat come out of real models every day, and coping with them is this library's job.
- If 200 lines could be 50, rewrite them. If a senior engineer would call it overcomplicated, simplify it.

### 3. Surgical changes

Touch only what you must, and clean up only your own mess.

- Don't "improve" the code, comments or formatting next to your change, and don't refactor what isn't broken.
- Match the style around you, even where you would write it differently.
- Mention dead code you notice; don't delete it unasked. Remove only what your own change left unused.
- Every changed line should trace back to the request.

### 4. Goal-driven execution

Say what success is, then loop until it is verified.

- "Fix the bug" means a test that fails without the fix, then the fix that makes it pass.
- "Make it faster" means timings before and after on the same inputs, and the results shown unchanged.
- "Make it robust" means a test or a fuzz run over the inputs that broke it.
- For work in several steps, write the plan with a check for each: `1. [step] -> verify: [check]`.

These habits are working when diffs carry fewer needless changes, fewer rewrites come from over-building, and
clarifying questions come before the code rather than after the mistake.

## Part two: this repository

### Layout

| Path | What it holds |
|---|---|
| `src/GeometryHelper` | the core library: 2D and 3D geometry, booleans, meshing, clash, arranging, packing |
| `src/GeometryHelper.TeklaConvert`, `.CadConvert`, `.IfcConvert` | the bridges to Tekla Structures, AutoCAD and IFC (xBIM) |
| `tests/` | the xUnit suites, net48 |
| `examples/` | console and plugin samples for Tekla and AutoCAD |
| `src/GeometryHelper/docs/` | the hand-written guides; `docfx.json` at the root builds them, with the API reference, into the docs site |
| `CHANGELOG.md` | the full release notes, newest first |
| `Directory.Build.props` | the one version all four packages share (`GeometryHelperVersion`), and `TeklaVersion` |

The libraries target netstandard2.0 and ship together under one version. TeklaConvert is built once per Tekla version
(`-p:TeklaVersion=2020`, `2025` or `2026`; 2020 when not given) and packed as `GeometryHelper.TeklaConvert.{year}`.

### Build and verify

After every change, build from scratch and run every suite:

```bash
dotnet build GeometryHelper.slnx -c Release -warnaserror --no-incremental
for v in 2025 2026; do
  dotnet build tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -c Release -warnaserror --no-incremental -p:TeklaVersion=$v
done
dotnet test tests/GeometryHelper.UnitTest/GeometryHelper.UnitTest.csproj -c Release --no-build
dotnet test tests/GeometryHelper.IfcConvert.UnitTest/GeometryHelper.IfcConvert.UnitTest.csproj -c Release --no-build
for v in 2020 2025 2026; do
  dotnet test tests/GeometryHelper.TeklaConvert.UnitTest/GeometryHelper.TeklaConvert.UnitTest.csproj -c Release --no-build -p:TeklaVersion=$v
done
```

- `--no-incremental` is not optional. Analyzers run only when the compiler runs, so an incremental build of an
  unchanged tree prints 0 errors over an analyzer error that CI then catches. Read the counts.
- `dotnet test --no-build` and `dotnet pack --no-build` need the same `-p:TeklaVersion` as the build, or they run the
  wrong output folder.
- Report what happened. A suite skipped or a test failing is said plainly, with the output.

### Tests

- A fix comes with a test shown to fail without it.
- Pass a `Tolerance` explicitly. New tests never set `Tolerance.Global`: it is process-wide, and the assemblies run
  their tests one at a time only because a few old tests still change it.
- The xUnit analyzers run under `-warnaserror`: `Assert.Contains(items, p)`, not `Assert.True(items.Any(p))`;
  `Assert.Equal(a, b)`, not `Assert.True(a == b)`; `Assert.Empty(list)`, not `Assert.Equal(0, list.Count)`.
- Name a test the way its file does. Newer files name each one as a sentence:
  `ACornerOfTheTopMovedThreeThousandths_IsWeldedBack`.
- Geometry from the maintainer's real projects (models, dumps, report cases) never goes into the repository. Build a
  body in code that shows the same thing.

### Geometry

- Units are millimetres: the maintainer builds Tekla Structures tools. Don't propose precision fixes that matter only
  in metres.
- Coordinates are never compared with `==`; every comparison goes through a `Tolerance`. `Tolerance.Default` is 1E-3
  for points and planes and 1E-5 for vectors.
- Names carry their dimension as a digit: `GeoPoint3`, `GeoFace2`, `ProjectToFace2`. Search with `[A-Za-z0-9]+`, since
  a letters-only pattern finds none of them, and read `docs/*.md` before saying a member is missing.
- New code gives every sort a full tie-break, and lets no result follow the enumeration order of a `Dictionary` or
  `HashSet`.
- Two speed traps that have cost real time. A sort-and-sweep along a fixed axis goes quadratic on bodies thin or long
  in that axis, so sweep the axis with the fewest pairs. A `long` key built as `(a << 32) | b` collides badly in a
  `Dictionary` without a comparer of its own.

### Code and docs

- LF line endings everywhere (`.gitattributes`). Write new files with LF.
- Every public member carries XML documentation. The API reference is generated from it, and the `-warnaserror` build
  fails without it (CS1591).
- Prose in XML docs, guides and the changelog is plain English with measured examples: "a box 100 by 200 by 300 with
  its top left out is closed by a face of 20 000 square units". Match the voice and comment density of the file.
- A change to public behaviour updates its guide in `src/GeometryHelper/docs/` and the changelog.
- Argument checks go through `Guard` (`src/GeometryHelper/Internal/Guard.cs`).
- Logging goes through `GeometryHelperLog`, which never throws. The bridges log Warn for a caught exception, one
  aggregated Debug line for expected skips, and one Info summary per public call.
- No `[Obsolete]` forwarders. A renamed or removed public member is a breaking change, written up as one, with a major
  version bump.
- `GeometryHelper.IfcConvert` stays netstandard2.0, though the xBIM geometry engine is .NET Framework only. That is a
  deliberate choice; don't propose net48.
- The vendored Clipper2 in `src/GeometryHelper/Internal/Clipper` keeps its notice in `THIRD-PARTY-NOTICES.md`.

### Git and releases

- Commit once the full verify is green, then stop. Never push unless asked to in that same request.
- Tags, GitHub releases and the `release.yml` workflow are the maintainer's to run.
- Commit messages: a summary line in plain words, then a body saying what changed and why, with the tests and numbers
  behind it, ending with the `Co-Authored-By:` trailer.
- Entries go under `## Unreleased` in `CHANGELOG.md`, marked **NEW.**, **FIXED.**, **CHANGED.** or **BREAKING.**
- `PackageReleaseNotes` carries the latest release only, with the link to the changelog. Replace it on a version bump,
  don't stack it. The build fails past NuGet's 35 000-character limit.
- Don't stage anything under `examples/` without asking.

### Working with the maintainer

- Reply in Vietnamese. Code, comments, docs and commit messages stay in English.
- Anything that talks to a running Tekla Structures needs the maintainer's go-ahead first.
- When several agents share a task, only one builds in the repository at a time, since they share `bin` and `obj`. The
  others draft outside it, and the lead merges, verifies and commits.
