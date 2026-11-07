# Phases

Every phase ends with the full verify of CLAUDE.md green, the DXF of the test bodies opened in AutoCAD with nothing for
AUDIT to fix, and the maintainer looking at the files before the next phase starts. Each step is written
`[step] -> verify: [check]`.

| Phase | What | Status |
|---|---|---|
| G0 | Research and spec | not started |
| G1 | Hidden-line projection, written to DXF | not started |
| G2 | Views | not started |
| G3 | Dimensions | not started |
| G4 | Marks and symbols | not started |
| G5 | The sheet | not started |
| G6 | Presets, the Tekla bridge, comparison with Tekla | not started |
| G7 | Docs, changelog, release | not started |

## G0: research and spec

1. Study FreeCAD TechDraw, OpenCascade's HLR and build123d's projection, and Tekla's user documentation on drawings
   -> verify: [30-research.md](30-research.md) says what each does and what is taken from it.
2. Collect the faults of Tekla's drawings from the maintainer -> verify: each in [40-tekla-bugs.md](40-tekla-bugs.md) with
   what Tekla does and what is right.
3. Build the test bodies of [20-test-cases.md](20-test-cases.md) in code -> verify: each is a closed, valid `GeoSolid3` (by
   `Validate`) and its volume is the one worked out by hand.
4. Settle the proposed decisions and the names of the public types -> verify: every D in
   [03-decisions.md](03-decisions.md) approved or replaced, and the layer files carry the final names.

## G1: hidden-line projection

1. Edge extraction: sharp edges, silhouettes, smooth edges dropped -> verify: counts per test body worked out by hand.
2. Occlusion: each edge cut into visible and hidden pieces -> verify: the three main views of every test body against
   lines worked out by hand, to the point tolerance.
3. Edges shared by touching objects drawn once -> verify: TC8 gives no doubled line.
4. Arcs fitted to faceted circles (D6) -> verify: every hole of TC1 and TC2 comes out as one circle of its diameter.
5. Sections: the cut faces and what lies behind the cut -> verify: section areas against `GeoSolid3.Section`.
6. A first DXF of the three views, lines on layers by visibility -> verify: opens in AutoCAD, AUDIT clean; the maintainer
   looks at it.
7. Speed -> verify: benchmark on TC10, timings recorded, and a fuzz run of random boxes and cuts with no exception and no
   edge both visible and hidden.

## G2: views

1. The drawing frame from the source (the main object's coordinate system) -> verify: TC2 lying along any axis gives the
   same views.
2. Views along the axes, chosen by settings; sections and details; isometric -> verify: per view, the lines of G1.
3. Scale chosen from the allowed list, crop to the objects, depth limits -> verify: each view fits its space on the sheet.

## G3: dimensions

1. Points of interest of a view: ends, outline corners, hole and bolt centres, work points -> verify: per test body.
2. Rules: overall, chain, cumulative, ordinate, holes by group -> verify: dimension values against the model.
3. Tiers outside the view, spacing, no dimension crossing another or the text of another -> verify: a test counts
   overlaps, and it is zero on every test body.

## G4: marks and symbols

1. Part, assembly and bolt marks placed through `Arranging` -> verify: no overlaps, every leader on its object.
2. Centre lines, section and detail marks, levels, weld symbols -> verify: per test body.

## G5: the sheet

1. Sheet size and frame, title block filled from the source and settings -> verify: attribute values in the DXF.
2. Part list as a DXF table -> verify: rows and quantities against the source.
3. Views placed through `SheetPacker` with their annotation -> verify: nothing overlaps, nothing off the sheet.

## G6: presets, Tekla bridge, comparison

1. Presets for a part, an assembly and a general arrangement -> verify: each test body drawn with each preset.
2. `DrawingSourceConvert` reading assemblies, parts, bolts and welds -> verify: TeklaConvert tests in code; a run against
   the maintainer's running Tekla only with their go-ahead.
3. Side-by-side with Tekla's exported drawing of the same objects -> verify: each fault of
   [40-tekla-bugs.md](40-tekla-bugs.md) shown right in ours.

## G7: docs, changelog, release

1. Guides in plain English with measured examples, a group in the docs site with its own index page -> verify: docfx
   `--warningsAsErrors` 0 warnings, no menu node without an href.
2. Changelog **NEW.** entries, package release notes -> verify: the release check.
3. Release as CLAUDE.md says -> verify: all three workflows green.
