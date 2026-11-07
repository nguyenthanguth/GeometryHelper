# L3: annotation

## Points of interest

Before anything is dimensioned, each view lists what can be measured to, each point with the object and the feature it
comes from:

- the ends and the outline corners of each part, as seen in the view;
- the centres of holes and bolts, with their group;
- work points and the reference line of the main part;
- the edges of cuts, chamfers and fittings.

## Dimensions

Rules, chosen by settings, decide which points are dimensioned and how:

| Rule | What it gives |
|---|---|
| Overall | the overall length, width and height of the main part |
| Chain | dimensions end to end between consecutive points |
| Cumulative | all from one origin, as one ordinate-like line |
| Holes by group | a bolt group's pitch as "n x p = total" and its distance to the part's end |
| Fittings | the position of each fitting from the main part's end or work point |

Placement:

- dimension lines in tiers outside the view, the overall dimension outermost, the spacing a setting in paper millimetres;
- no dimension line crossing another, no text over a line or another text; a test counts overlaps on every test body
  and wants none;
- equal neighbouring dimensions may be merged into "n x p";
- a dimension is never doubled by another in the same view;
- the value is the model length (D1: `DIMLFAC`), rounded as the settings say.

The dimensions are written as real DIMENSION entities (L5), so that a user can move them in CAD and the value stays.

## Marks

- part marks, assembly marks and bolt marks, with what each shows set by the settings (Q5);
- placed through `Arranging` (`Arranger.Run`), with the view's lines and dimensions as obstacles, so that no mark covers
  another, a dimension or a line where it can be avoided;
- a leader from each mark to a point inside its object, as an MLEADER.

## Symbols

- centre lines of holes, bolt groups and tubes;
- section marks (the cut line with its letters and arrows) and detail marks (a circle and its letter);
- weld symbols (Q3) on a leader to the weld;
- level marks for general arrangement drawings.
