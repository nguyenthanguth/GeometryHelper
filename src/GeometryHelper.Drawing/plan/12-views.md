# L2: views

## A view is a window into the model (A11)

A view works as a Tekla view or an AutoCAD MVIEW does: a frame on the sheet that looks into the 3D model in one direction,
at one scale, within one crop and one depth range. The lines in a view are **not dead lines**: L1 makes them again from the
model whenever the model, the direction, the crop or the depth changes. Dimensions and marks in a view are tied to points
of the model (L3, L7).

Unlike AutoCAD's MVIEW, a view here has true hidden lines and sections, since L1 works them out from the solids; AutoCAD
only shows the model as it was drawn.

## The drawing frame

A drawing has one frame, by default the main object's own (a Tekla part's coordinate system: x along its length, y up
in its cross-section). Every view is set in that frame, so a beam lying along any axis of the model gives the same
drawing.

## Kinds of view

| `ViewKind` | Looks | Notes |
|---|---|---|
| Front | from the frame's +Z, across X and up Y (Y is up in a view) | the main view; the projection method (D9) changes where the other views go round it, never what a view shows |
| Top, Bottom | Top from +Y (across X, up -Z), Bottom from -Y (across X, up Z) | third angle puts Top above the front view and Bottom below; first angle the other way |
| Left, Right (end views) | Right from +X (across -Z, up Y), Left from -X (across Z, up Y) | third angle puts Right on the right of the front view; first angle on the left |
| Back | from -Z (across -X, up Y), opposite the front | rarely asked for; goes in column +2 either way |
| Section | across a cut plane, one side kept | a section mark in the view it is cut from (L3) |
| Detail | a part of another view, cropped by a circle or a rectangle, at a larger scale | a detail mark in the view it comes from |
| Isometric | along a fixed oblique direction | no dimensions by default |
| Free | a direction the user sets, as an MVIEW looking from any side | made from the UI or a plugin |

Which views a drawing has is set by the settings (L6); the user adds, deletes and edits views in the UI (L8) or through a
plugin (P). A view with no lines in it is not made.

## DrawingView

Its properties, which are also what Properties shows when a view is selected (see the "B2 · a view selected" board of the UI design):

| Group | Properties |
|---|---|
| View | name, kind, direction, scale (from the allowed list, Q4) |
| Visible area | crop (by default the box of the objects seen plus a margin; dragged by the corner grips), depth in front, depth behind |
| Display | hidden lines, centre lines, shortening of long views |
| Content | the objects in the view; lines made again when the model changes |
| On the sheet | place, title (for example "SECTION A-A 1:10") |

A view holds:

- its `ViewFrame3` and the values above;
- its projected lines from L1, scaled to sheet millimetres;
- its annotation from L3;
- a stable id (L7) for the UI, plugins and the next update to refer to.

## When lines are made again

- The model changes (updating the drawing): every view with a changed object.
- The direction, crop, depth or hidden-line display changes: that view only.
- The scale changes: no new lines, only the scale on the sheet; the annotation is placed again.

## Views of long parts

A long part at a scale that fits the sheet can come out too small to read. A setting allows a view to be shortened:
the length between the features is cut out and the view drawn with break lines, dimensions keeping the true lengths.
This comes after G2's first steps, once the plain views work.

## In the DXF

By D10, each view becomes by default one viewport in a layout of the DXF, at its scale, looking at the view's lines placed
at 1:1 in model space, so that AutoCAD opens it on layouts and viewports as MVIEW makes them. The other way is the sheet
at 1:1 in model space ([15-output-dxf.md](15-output-dxf.md)).
