# L2: views

## The drawing frame

A drawing has one frame, by default the main object's own (a Tekla part's coordinate system: x along its length, y up
in its cross-section). Every view is set in that frame, so a beam lying along any axis of the model gives the same
drawing.

## Kinds of view

| `ViewKind` | Looks | Notes |
|---|---|---|
| Front | along the frame's -y or +y, by the projection method (D9) | the main view |
| Top, Bottom | along the frame's z | placed above or below the front view by the projection method |
| Left, Right (end views) | along the frame's x | |
| Back | opposite the front | rarely asked for |
| Section | across a cut plane, one side kept | a section mark in the view it is cut from (L3) |
| Detail | a part of another view, cropped by a circle or a rectangle, at a larger scale | a detail mark in the view it comes from |
| Isometric | along a fixed oblique direction | no dimensions by default |

Which views a drawing has is a setting (L6). A view with no lines in it is not made.

## DrawingView

- its kind and `ViewFrame3`;
- its scale, from the allowed list (Q4): the largest whose view, with room for its dimensions, fits the space the sheet
  gives it;
- its crop: by default the box of the objects seen plus a margin; a depth range in front of and behind the frame;
- its projected lines from L1, scaled to sheet millimetres;
- its annotation from L3;
- its title (for example "SECTION A-A 1:10") and its place on the sheet (L4).

## Views of long parts

A long part at a scale that fits the sheet can come out too small to read. A setting allows a view to be shortened:
the length between the features is cut out and the view drawn with break lines, dimensions keeping the true lengths.
This comes after G2's first steps, once the plain views work.
