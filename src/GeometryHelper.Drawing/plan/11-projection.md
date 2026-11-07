# L1: projection and hidden lines

The core of every drawing, and the part the library does not have yet. Given solids and a view frame, it gives the 2D
lines of the view: which are seen, which are hidden, and the faces a section cuts.

## Input and output

In:

- the solids, each with its object's id;
- a view frame (`ViewFrame3`): x to the right of the sheet, y up, z towards the viewer; a near and a far depth; for a
  section, the cut plane and the side kept;
- a `Tolerance`.

Out, `ProjectedView2`:

- `ProjectedEdge2` items: a `GeoLine2` (or a `GeoArc2` after fitting, D6), `EdgeVisibility` Visible or Hidden, `EdgeKind`
  Sharp, Silhouette or SectionOutline, and the object's id;
- section faces: `GeoFace2` per object cut, for hatching;
- all in view millimetres at 1:1; the view's scale is applied later, by L2.

## The method (proposed, D2)

The solids are polyhedra: every face is a flat polygon. Hidden lines are removed exactly on them.

1. **Transform** every solid into the view frame.
2. **Candidate edges.** For each edge of a solid, with its two faces:
   - a sharp edge (the faces meet at more than the smooth angle, a setting) is a candidate;
   - a smooth edge (two facets of the same curved surface, a tube or a rounded corner) is a candidate only where it is a
     silhouette: one face turned to the viewer and the other away;
   - an edge seen end-on (its projection shorter than the point tolerance) is dropped.
3. **Occluders.** The faces turned to the viewer, projected to 2D, each with its plane for depth. Faces seen edge-on are
   not occluders.
4. **Occlusion.** For each candidate edge, the occluders whose 2D box meets the edge's, found through a spatial index:
   - clip the edge's 2D segment against the occluder's polygon (with its holes), giving parameter intervals;
   - within an interval, the depth of the edge minus the depth of the face is linear along the edge, so it changes sign
     at most once: split there, and the part behind the face is hidden;
   - the faces the edge belongs to never hide it; a face the edge lies in, within tolerance, does not hide it either.
   The union of the hidden intervals is hidden; the rest is visible.
5. **Clean-up.**
   - Pieces shorter than the point tolerance are dropped.
   - Collinear pieces that touch, of the same visibility and object, are joined.
   - Where two objects touch and their edges fall on one another, the line is drawn once: visible wins over hidden, and
     the lower id keeps it, so that the result is the same every run.
6. **Arcs (D6).** Chains of short segments that lie on one circle within tolerance become one arc, or one circle when
   closed.
7. **Order.** The edges are sorted by object id, kind, visibility and coordinates, with a full tie-break.

## Sections

- The cut faces come from `GeoSolid3.Section(plane, tolerance)`.
- What lies on the kept side is drawn by projecting the part of each solid on that side (`GeoSolid3.TrySplitBy`, or by
  clipping the candidate edges and occluders by the half-space, whichever G0 shows is the more robust).
- The cut faces hide what lies behind them.

## Speed

- The spatial index sweeps the axis with the fewest pairs, not a fixed one (CLAUDE.md's speed trap).
- Target, set in G0: the three views of an assembly of 50 parts in well under a second.
- Benchmarks sign the result bit for bit, so that a speed-up shows it changed nothing.

## Hard cases to test

- An edge that pierces a face: hidden on one side, visible on the other.
- Coplanar faces of two touching parts.
- Faces a hair off flat, as Tekla's cut faces are (the library's 1E-2 tolerance history).
- A tube seen along its axis (only circles), across it (two silhouette lines) and at an angle.
- Long thin bodies along the sweep axis (the quadratic trap).
- A slot, a hole through a flange, a bolt in its hole.
