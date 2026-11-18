# C7: user coordinate systems

What AutoCAD does with a UCS, what the presenter takes from it, and what it leaves. The presenter shows the drawing from
above (no 3D view, no view rotation), so a UCS stays in the plan: its origin moves and it turns about Z.

## AutoCAD

- **UCS** (2021): `Specify origin of UCS or [Face/NAmed/OBject/Previous/View/World/X/Y/Z/ZAxis] <World>:`. A point is the
  origin, then `Specify point on X-axis or <Accept>:` and `Specify point on the XY plane or <Accept>:` (the old 3point and
  Move in one). NAmed: `Enter an option [Restore/Save/Delete/?]:`. The prompts of older releases (M, 3, S, R, D, ?, G for
  orthoGraphic, A for Apply) are still taken when typed. OBject: a line from the end nearer the pick along the line; a
  circle at the centre, X through the pick; an arc at the centre, X through the end nearer the pick; a polyline from its
  start along its first segment; a text, MTEXT, INSERT at its insertion point along its rotation; a point along the OCS X.
  The first line written is `Current ucs name:  *WORLD*` (`*NO NAME*` for an unnamed system).
- **UCSICON**: `[ON/OFF/All/Noorigin/ORigin/Selectable/Properties]`; the icon at the origin when it fits in view (ORigin),
  else at the lower left corner; the 2D style has a W while the UCS is the world and a + in its corner box at the origin.
- **PLAN**: `[Current ucs/Ucs/World] <Current>`; turns the view to the plan of the system and zooms to the extents.
- **UCSMAN** (UC): Named UCSs (Set Current, Rename, Delete, Details), Orthographic UCSs (Top, Bottom, Front, Back, Left,
  Right, relative to UCSBASE), Settings (icon on, at origin, apply to all viewports; save UCS with viewport, UCSFOLLOW).
- **Variables**: UCSNAME, UCSORG, UCSXDIR, UCSYDIR (read-only, the current system), UCSBASE (what the orthographic ones
  come from), UCSFOLLOW (plan view on a UCS change), UCSICON (bits: 1 on, 2 at origin), UCSVP (UCS kept per viewport).
  Typed `*x,y` and `@*dx,dy` are world coordinates whatever the UCS.
- **DXF**: the header's `$UCSBASE`, `$UCSNAME`, `$UCSORG`, `$UCSXDIR`, `$UCSYDIR`, `$UCSORTHOREF`, `$UCSORTHOVIEW`,
  `$UCSORGTOP` to `$UCSORGBACK`, and the same with `$PUCS` for paper space; the UCS table, each record: 2 name, 70 flags,
  10/20/30 origin, 11/21/31 X axis, 12/22/32 Y axis, 79 orthographic type, 146 elevation, 346 base UCS, then 71 and 13/23/33
  for each orthographic origin. The active VPORT keeps its own (110, 111, 112, 79, 146, 345, 346); a layout keeps one in
  its LAYOUT object.
- **ezdxf** (`ezdxf/math/ucs.py`, `ezdxf/entities/ucs.py`, MIT, read for ideas only): a UCS is an origin and unit axes;
  `from_wcs` is the transposed rotation of `p - origin`, `to_wcs` its inverse; angles are turned by the UCS's own angle
  (`to_ocs_angle_deg`); `rotate_local_z`, `moveto` and `from_x_axis_and_point_in_xy` (the 3point case) make new systems.
  The record writes exactly the groups above.

## In the presenter

| UCS option | Done | How |
|---|---|---|
| Origin (point, then X point, then XY point, each Accept) | yes | the 3point and Move cases; a third point on the negative side (Z down) is refused |
| World, Enter | yes | |
| Previous | yes | ten per space, as AutoCAD; a name since deleted comes back as none |
| View | yes | X right, Y up (the view is always the plan), origin kept |
| Z | yes | angle typed or picked, from the UCS's X axis |
| ZAxis | partly | new origin; a Z point off upright, or Object, is refused with a message |
| OBject | yes | line, circle, arc, LWPOLYLINE, TEXT, MTEXT, INSERT, POINT; others: "Could not use object to define UCS." |
| NAmed Save/Restore/Delete/? | yes | Save asks before replacing; Delete takes commas and * ? wildcards; ? lists in the current UCS |
| M, 3, S, R, D, ?, G (Top) | yes | the older prompts typed |
| X, Y, Face, G other than Top | no | "not available in a 2D view" |
| Apply | no | each space has one view |
| UCSICON ON/OFF/Noorigin/ORigin/All | yes | All asks again; Selectable, Properties say not available |
| PLAN | partly | zooms to the extents; does not turn the view (CadView has no rotation) and says so |
| UCSMAN | no dialog | lists the named systems on the command line; a window may handle the request "UCSMAN" |

Rules kept: points typed are in the UCS, `*` gives world ones; the status bar, dynamic input and the tip of a point prompt
show UCS numbers; the grid, snap, ortho and polar follow the UCS axes; the crosshair and the icon are drawn along them;
entities are always written in world coordinates. UCS changes are not undo steps (U leaves them), and make the drawing
modified. The current system of model space is `$UCS*`, of paper space `$PUCS*` (one for every layout). A system read
from a file out of the plan is kept and saved untouched, listed by `?`, and not made current; the presenter works in the
world meanwhile. The named systems are written on save, as AutoCAD writes them, through `DxfDatabase.UcsTable`.

Not done: the active VPORT's own UCS (110 to 112), the UCS of each layout, orthographic origins (`$UCSORGTOP`...),
UCSFOLLOW, per-viewport UCS (UCSVP), the paper space triangle icon, the UCSMAN dialog.
