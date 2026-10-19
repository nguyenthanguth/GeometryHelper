# GeometryHelper.Viewer

A viewer for GeometryHelper geometry, to look at a shape in three dimensions with the navigation of a modelling program:
write a C# script that builds shapes, or open a scene file a test or an audit harness wrote, and see them, with the numbers the
library gives for each. It is a tool of the repository, not a package: it is not in `GeometryHelper.slnx`, it is not packed, and
the library's own verify does not build it.

## Build and run

```bash
dotnet build tools/GeometryHelper.Viewer.slnx -c Release -warnaserror --no-incremental
dotnet test tools/GeometryHelper.Viewer/GeometryHelper.Viewer.Tests/GeometryHelper.Viewer.Tests.csproj -c Release --no-build
tools/GeometryHelper.Viewer/GeometryHelper.Viewer/bin/Release/net8.0-windows/GeometryHelper.Viewer.exe tools/GeometryHelper.Viewer/samples/box-with-opening.csx
```

The viewer is a WPF application on `net8.0-windows`, so it needs Windows and a graphics card that runs DirectX 11. The 3D view is
HelixToolkit.SharpDX (`HelixToolkit.SharpDX.Core.Wpf` 2.27.3). Scripts run with Roslyn
(`Microsoft.CodeAnalysis.CSharp.Scripting` 4.14.0).

| Project | What it is |
|---|---|
| `GeometryHelper.Viewer.Core` | netstandard2.0: the scene model, `SceneWriter` and `SceneReader`, the drawing primitives, the camera maths, the keymap, the work plane, the clip planes and their sections, the layout of the views, the saved views. A net48 harness can reference it and dump a scene. |
| `GeometryHelper.Viewer.Scripting` | net8.0, no window in it: the script host, which the app and the runner both use, and what runs a script against another build of the library in a child process (the cache of runners, the build of a commit, the watching of a DLL) |
| `GeometryHelper.Viewer.Runner` | net8.0 console: runs one script with the GeometryHelper.dll beside it and writes the scene. The app copies its folder once for each build of the library it compares |
| `GeometryHelper.Viewer` | the window and the 3D view; the runner's folder is copied into `runner` beside it |
| `GeometryHelper.Viewer.Tests` | xUnit, net8.0: tests of Core, of the script host, and of the comparison, which starts the runner and builds commits of throwaway repositories made under the temporary folder |

### Command line

```
GeometryHelper.Viewer.exe [file] [--check] [--screenshot out.png] [--view iso|top|front|right] [--mode wireframe|shaded|edges|transparent]
                          [--views 1|2|4] [--scale 1|2|4] [--background window|white|transparent] [--do commands]
```

| Switch | Does |
|---|---|
| `file` | opens a `.csx` script or a `.ghscene.json` scene; a file dropped on the window is opened the same way |
| `--check` | loads the file with no window, prints what it holds, and exits 0, or 1 with the errors, a script's with the file and line number |
| `--screenshot out.png` | opens the file, fits the views, writes the picture of the 3D views, and exits 0; 1 when the file did not open |
| `--view`, `--mode` | the standard view of the active view and the render mode of every view in the picture |
| `--views 1\|2\|4` | how many views the picture is of: 1 is the one view the window starts with, 4 the plan, the front, the right and the iso as they stand in the window |
| `--scale 1\|2\|4` | how many times the size of the window the picture is: each view is drawn again at that size by the graphics card, not stretched, so the picture is the size of the area the views fill, rounded up, times 1, 2 or 4 |
| `--background window\|white\|transparent` | what is behind the scene: the colour of the views in the window (the default, as pictures were always taken), white, or nothing, a PNG with an alpha channel |
| `--do commands` | with `--screenshot`: does the commands, separated by semicolons, in the process through the code a click and a drag go through, after the view is fitted and before the picture, and prints the output pane and the status bar |

The commands are `select 0 2`, `hover P`, `box P P [add|toggle]`, `band P P` (the rubber band as if mid-drag), `mode distance|angle|radius|centre`
followed by `click P`, `keys ZoomIn F8` (the action names of the keymap), and `wait 500` (milliseconds, for the library to answer). A place P is two numbers, pixels of the 3D view, or one word `@x,y,z`, a point
of the scene taken to where it shows: `--do "mode distance; click @60,60,0; click @150,60,0"` measures between two points of a scene with no hand on the mouse.

The diagnostics have three more: `diag edges crossings` runs the named checks (`all` for every one, none for the ones ticked in the pane) on the selected items, or on every item when
none is selected, waits for the run, and writes each finding to the output pane; a last word `own` leaves the library out and makes the viewer's own checks alone. `diagshow 3` does what a click on the third finding does (selects its item, zooms to the spot, marks it) and
`diagclear` takes the findings and their drawing off. `--do "select 10 11; diag gap" --view top` gives the gap of two items drawn larger in the picture.

The comparison has its own (see Comparing two builds): `compare own commit:9ee3f57 [repo=path] [timeout=seconds] [watch]` runs the open script against the two builds and shows
the comparison, `comparerows` writes the table to the output pane, `compareview left|right|both` is the keys 1, 2 and 3, `compareclick 3` clicks the third row, `comparedialog out.png` draws the window that asks for a
comparison, and `exporttest file.cs` writes the test skeleton of the selected items to a file (and to no clipboard).

The views, the work plane, the clip planes and the pictures have commands too (see Views, the work plane and the clip planes): `views 4`, `activeview plan|front|right|iso`, `viewmode front wireframe`, `viewset iso top`, `link front`
(toggles), `grid on|off`, `viewsizes` and `cursor P` (the status bar readout for a place) write what the window shows to the output pane; `keys WorkPlaneThreePoints; click @0,0,0; click @100,0,0; click @100,60,40` sets a work plane the way three
clicks do, `keys ClipAddView; click @50,0,20` and `keys ClipBox` add clip planes, `clipselect 1`, `clipoffset 1 12.5`, `clipoff 1` and `clipon 1` choose and move them (numbered from 1), `saveview name` and `restoreview name` keep and bring back a view, and `shot out.png [all|active] [1|2|4]
[window|white|transparent]` writes a picture then and there, with what the commands before it have done.

`--check` and `--screenshot` do not read or write the settings of the user.

## What the window holds

- Left, the **scene tree**: layers, and the items on them, each with a check box that shows or hides it and a swatch of its colour.
  Clicking an item selects it, clicking a layer selects all its items.
- Middle, the **3D views** (one, two or four, each with the axes at the bottom left), and below them the **output**, with the tab of the clip planes beside it: what a script says with `Note`, its errors,
  and what the viewer could not draw.
- Right, the **properties** of the selection: kind, name, layer, the properties the script gave it, and the numbers the library
  gives for the shape rebuilt from the scene (vertices, lengths, areas, the volume of a body and whether it is closed) and its bounds.
- The **status bar**: where the cursor is on the work plane (the plane z = 0 where none is set), the snap under the cursor with its coordinates, which snaps are on
  (`snaps EMIC-`: end, middle, intersection, centre, nearest), the work plane, how many items are selected, the point tolerance of the scene
  (`EqualPoint`), and the render mode of the active view.

Everything is kept in double precision. The graphics card works in floats, so what it is given is each point less a render origin,
and the origin follows the camera: when the target of the camera has strayed from it by more than a float can hold to a thousandth
of a pixel (a thousandth of a pixel over two half units in the last place of a float, 8 400 pixels at 1 000 mm of distance and a view
800 pixels high), the geometry is made again relative to the target, on another thread, and swapped in with the new origin once it
is ready; the old geometry is drawn until then. A needle 0.0005 wide 30 000 from the middle of a scene 60 000 across is drawn 0.0005
wide, to 1E-7, once the view is on it, where from the middle of the scene a float could only give it 0 or 0.00049. The view is
rebased a fifth of a second after the camera comes to rest.

Arcs, circles and ellipses are cut into chords by the viewer's own rule, never by the library's `Flatten`, so that a fault in the
library's flattening shows as the fault it is. A chord lies no further than a tenth of the point tolerance of the scene from its
curve (1E-4 at the default 1E-3), a piece turns at most 2 degrees, a full turn has at least 16 pieces and at most 65 536; a circle
of radius 500 takes 4 968 pieces and one of radius 60 000 takes 54 414. A curve that would take more than the cap is cut in the cap
and the output says so. The bounds of a curve are those of the exact curve, not of its chords. A scene whose lines would take more than
4 million points (96 MB of coordinates in double precision, 48 MB of floats for the graphics card) is warned of once, and all its arcs, circles and ellipses are
cut at one coarser chord tolerance, the finest at which the lines fit the cap, found by counting the pieces before any curve is cut; the warning names it. Where
even 2 degrees a piece, 180 to a turn, does not fit, they are cut at that and the warning says it is still over. The polygons, faces and bodies are drawn as
they are. A circle of radius 60 000 costs 54 414 points at the chord tolerance of 1E-4 and 180 at 2 degrees.

The scene keeps the tolerance it was written under: all four thresholds, the point, vector, angle and planar ones. A `SceneWriter`
takes the tolerance in force on its thread when it is made, and `Tol` in a script sets all four. A shape is rebuilt, drawn and measured
at that tolerance, so a face built under a planar tolerance of 0.01 is not refused at 0.001. Where the library still refuses a shape,
it is rebuilt at 1E-12 and the properties say "rebuilt and measured at 1E-12"; where it refuses that too, the properties still show the raw vertex
count and the bounds of what is drawn. Where the library keeps fewer vertices than the scene holds, the properties say how many.

The surfaces are pushed back from their edges by a few steps of the depth buffer so that the edges show. The orthographic eye stands
just outside the depth of the scene (a twentieth of it beyond the nearest point), so a plan of a site 60 000 across is a view a few
millimetres deep; a perspective view clips at a hundredth of its distance to the target, so the target is never cut however far the
rest of the scene reaches, and at the farthest corner of the scene's box in front of the eye, though never further than a million times
the near plane: a camera 2 mm from its target sees 20 m. A step of the depth buffer at a distance z from the eye is about z squared over the near plane
over 2^24 in perspective, so at the far plane it is about 600 times the distance to the target, and the bound is what keeps it from growing without limit as the
scene is made longer. What an item is pushed back by is the fewest steps that come to 2E-4 mm at the target, at least 2 and at most 400: 6E-6 mm a step in an
orthographic view 100 mm deep, which is the depth divided by 2^24, and in a perspective view the step at the target, d squared over the near plane
over 2^24 for a distance d, 6E-6 times d: 2E-3 mm for a part 300 mm off. No depth buffer can go below about 4E-3 mm in an orthographic view 60 000 deep,
whatever the push.

A face or a body is drawn by the triangles the library cuts it into, with the openings of a body cut in; the lines are the edges of its
faces and its openings. A shape the library refuses, a polygon of two points or a face off its plane, is still drawn by its outline,
and the output says what the library did not take. A body with openings that gives no triangle is drawn by its outline and warned of,
"nothing left: the openings take all of it, or the library could not cut them", since the library says nothing when a cut is
unworkable but logs it; what it logged at Warn or Err while each item was built is shown beside the item in the output. A scene at
a tolerance of nought (`Tol = new Tolerance(0, 0)`) saves and opens again.

## Keys

The defaults are in `keymap.json`, shipped inside the assembly and beside the executable. To change one, copy the file to
`%APPDATA%\GeometryHelper.Viewer\keymap.json` and edit it: an action the copy lists takes its gestures from the copy, and the others
keep theirs. A gesture is written `Ctrl+Shift+H`, `Wheel`, `Ctrl+MiddleDrag`, `DoubleClick`; Help > Keys lists what is in force.

| Action | Default |
|---|---|
| Zoom about the cursor | Wheel; zoom about the middle: Page Up, Page Down |
| Pan | Middle drag; Arrow keys |
| Rotate | Ctrl + Middle drag; Ctrl + Arrow keys, 15 degrees at a time |
| 3D / plan toggle | Ctrl+P |
| Fit all / fit selection | F / Shift+F |
| Standard views | Ctrl+0 iso, Ctrl+5 top (plan), Ctrl+6 front, Ctrl+7 right |
| Render modes | Ctrl+1 wireframe, Ctrl+2 shaded, Ctrl+3 shaded and edges, Ctrl+4 transparent |
| Select / add / toggle | Click / Shift+Click / Ctrl+Click |
| Box select | Left drag: from left to right a window, from right to left a crossing; Shift adds, Ctrl toggles |
| Rotation centre | V, then a click: turn about that point (snapped); Shift+V: turn about the middle of the view again |
| Measure distance / angle / radius | F2 / F3 / F4 |
| Snaps: end / middle / intersection / centre / nearest | F8 / F9 / F10 / F11 / F12 |
| Cancel | Esc: stops a script that is running; else ends the measuring and clears its dimensions; else clears the selection |
| Hide selected / show all | Ctrl+H / Ctrl+Shift+H |
| Properties | Double-click, or Alt+Enter |
| Diagnostics pane | Ctrl+D |
| Run the script / run it again | F5 / Ctrl+R (File > Reload does the same; with a comparison open, Ctrl+R runs it again) |
| Compare two builds | Ctrl+Shift+C |
| The side of a comparison shown: left / right / both | 1 / 2 / 3 (only while a comparison is open) |
| Export a test | Ctrl+T |
| Open / save the scene as | Ctrl+O / Ctrl+S |
| Screenshot | Ctrl+Shift+S |
| One / two / four views | Ctrl+Shift+1 / Ctrl+Shift+2 / Ctrl+Shift+4 |
| Link the active view to the other linked views | L |
| Work plane: by three points / on a face / on the view / global | W / Ctrl+W / Ctrl+Shift+W / Shift+W |
| Clip plane: through a point square to the view / on a face / box round the selection | K / Ctrl+K / Ctrl+B |
| Clip plane: flip the selected one / remove them all / the pane | Shift+K / Ctrl+Shift+B / Ctrl+Shift+K |
| Save the active view under a name | Ctrl+Shift+V |

The mouse follows the scene: dragging the middle button right moves the scene right, and Ctrl and the middle button turns the near
side of the scene right. Ctrl+Left turns the scene by 15 degrees counter-clockwise seen from above, Ctrl+Up tilts the camera down
towards the scene. The view looks along -Z with +Y up in the plan, and +Z is up in every other view.

## Selecting, snapping and measuring

All of it is done in double precision from the exact numbers of the scene, never from the floats the graphics card was given, so a
needle 0.0005 wide is picked and measured as 0.0005 wide, and an arc is the arc and not the chords it is drawn with.

**Selecting.** A click selects the item under the cursor; the line within 5 pixels of the cursor is on it, a point within 7, and an edge on
a face is taken before the face. What the cursor is over is lit as it moves, to show what a click would take. Dragging with the left button
draws a box. From left to right it is a *window*, in solid blue, and takes what lies wholly inside it; from right to left it is a *crossing*,
in dashed green, and takes what lies inside it or touches it, a face included where the box is inside the face and touches none of its edges.
Shift adds the result to the selection, Ctrl toggles it, neither replaces it. The box is judged on what is drawn, the chords of a curve and
the triangles of a surface, through everything in the way; where a perspective view has part of an item behind the camera, a window leaves
that item out and a crossing judges the part in front.

**Snaps.** F8 to F12 switch the five kinds on and off; the first four start on, nearest off. A snap is the point nearest the cursor on the screen,
within 12 pixels, of these: the *ends* of segments and arcs, the vertices of chains, loops and faces, and points; the *middles* of segments and arcs
(for the arc of a bulge, the sagitta, half the bulge times the chord, out from the middle of the chord); the *centres* of arcs, circles and
ellipses, wherever the arc itself is; the *intersections* of two drawn segments, arcs or circles, in the plane or in space (two curves meet where
they come within the point tolerance of the scene), and of a segment with an ellipse; an ellipse against a circle, an arc or another ellipse
is not looked for; and the *nearest* point of a curve, taken only where none of the others is in reach. Of the twenty-four curves nearest the cursor
the crossings are found; a crossing at an end is offered as that end while ends are snapped to, and as a crossing when they are not. Nothing beyond the far plane of a
perspective view is snapped to, picked or selected. Equal snaps are told apart by kind, then item, then place, so the choice never
rests on the order the curves are held in. A snap shows as a marker at the point, a square for an end or a point, a cross for a middle or a
crossing and a circle for a centre or the nearest point, and its name and coordinates are in the status bar. Ends, middles, centres and crossings
are computed from the numbers of the curves to a few units in the last place of the coordinates, 1E-12 on a scene a hundred across; an arc is measured from
its start and not its centre, so an arc of a bulge of 1E-8 on a chord of 60 000, whose centre is 1.5E12 away, crosses a line where it should to 1E-9. Two arcs or circles are crossed from their centres, though, so where both radii are beyond
about 1E10 the crossing loses digits (3E-4 off for bulges of 1E-7 and 1E-8 on that chord). Two shapes are in one plane
where the tolerance of the scene says so: a segment lies in the plane of a circle when both its ends are within the tolerance of it, and two circles share a plane
when the angle between their planes, over the larger radius, moves a point by no more than the tolerance. A segment that comes within the tolerance of a circle touches it,
as two circles do. A tolerance of nought is taken as 16 units in the last place of the coordinates.

**Rotation centre.** V, then a click: the point is the snap under the cursor, else the point of the surface or line under it, else the point of the
plane through the target of the view square to it. The view then turns about it, with the mouse and with Ctrl and the arrow keys, and it stays where it is on the screen. A blue
cross shows it. Shift+V goes back to turning about the middle of the view, as it did before.

**Measuring.** F2, F3 and F4 start a measurement and it stays on until Esc, which also takes the dimensions off the view. Each click gives a point
the way the rotation centre does. The dimension is drawn over the scene, in front of any body it runs through, with its number beside it at a size
that can be read at any zoom, and the numbers go to the output pane to ten significant digits.

| Key | Picks | Gives |
|---|---|---|
| F2, distance | two points | the straight distance, the offsets along X, Y and Z, and the length in plan |
| F3, angle | a point on the first leg, the vertex, a point on the second | the angle at the vertex from 0 to 180 degrees, from the arc tangent of the cross and dot products, so a hair off straight is not lost |
| F4, radius | an arc or a circle | the radius and diameter, the centre and normal, the sweep and length of an arc; an ellipse gives both semi-axes, and a segment has no radius |

Where the two points of a distance are on two different items, the library is asked as well, at the tolerance of the scene: `DistanceTo` between
the two shapes rebuilt from the scene, and `GetShortestLineTo`, whose segment is drawn in teal apart from the dimension. Both numbers are shown, since
the distance between two points on two shapes is not the distance between the shapes. With exactly two items selected, F2 asks the library alone about
them. Where the library has no call between the two kinds, the output says so; where a call exists on only one of the two, the other is turned round.

## Views, the work plane and the clip planes

**Views.** Window > One view, Two views, Four views (Ctrl+Shift+1, 2, 4). The window starts with one, the iso it always had. Four are the plan, the front, the right and the iso, two by two: the plan and the front above, the right and the iso below.
Two are the plan beside the active view, or the plan beside the front where the plan is the active one. Each view has a camera and a render mode of its own (Ctrl+1 to 4 and the standard views act on the active one) and its own render origin, so that each is rebased to its
own target and a needle 0.0005 wide is as sharp in the view looked into as in the one looking out. The views share the scene, the selection, the hover, the snaps, the dimensions, the findings of the diagnostics, the work plane and the clip planes: a snap marker shows in all four at once.
The view under the mouse is the active one, with a blue outline; the keys and the mouse act on it. Each view is fitted to a new scene, those not shown when they are shown, and the layout, the render modes and the links are kept in the settings.

**Linked views.** L links the active view: linked views zoom and pan together, whichever of them is moved, each looking from its own direction, at the same target and the same distance, so that one pixel is the same length in all of them. A view that is not linked
moves alone and takes none with it. It is for comparing one spot from two sides: link the plan and the front, and the wheel over one brings the other to the same place.

**Work plane.** A coordinate system that the grid, the readout of the cursor, the point a click on nothing lands on, and the offsets of a measured distance use; the plane z = 0 until one is set. W then three clicks sets it: the origin, a point along the x axis, a point in the plane on the side
the y axis points to; the clicks snap as any do, and three points on a line, or two the same, are refused and asked for again. Ctrl+W then a click on a face lays it on the face, its z the normal of the face turned to the eye, its x as near the right of the view as the face allows. Ctrl+Shift+W
lays it on the active view, the target its origin, its x the right of the screen and its y up it. Shift+W goes back to the global plane. A work plane that is not global is drawn as a triad at its origin (x red, y green, z blue), and setting one turns the grid on (View > Grid on the work plane): the grid lies on it, through the scene, 20 lines across a round step apart; the status bar reads
`x 20.462   y 33.871   z 0 (work plane)   world 20.462, 0.000, 33.871` and the measurement of a distance gives dx, dy and dz along its axes, "along the axes of the work plane" in the output. The axes are made perpendicular to a unit in the last place of the coordinates, so a point taken to the plane
and back comes back to 1E-12 on coordinates of a thousand. A click that hits something is on it as always; the work plane is where a click on nothing goes.

**Clip planes.** Up to six. A plane keeps the side its normal points to and cuts the rest away: it is not drawn, not picked, not snapped to and not selected by a box. K then a click puts a plane through the point square to the view and cuts away what is nearer the eye; Ctrl+K then a click on a face lays the plane on
the face and cuts away what is in front of it; Ctrl+B puts a box of six planes round the selection, or round everything shown where nothing is selected, a hundredth of its diagonal out on every side (a box 100 by 60 by 40, 123.3 across its corners, is boxed 1.23 out), and takes the planes there were. The
**Clip planes** tab beside the output lists them: choose one and its outline, an arrow along its normal and a handle show in every view; type an offset along the normal from where the plane was made and press Enter, or drag the handle in a view (a plane moves by how far along its normal the
mouse has gone, and nothing is done where the view looks along the normal). Flip (Shift+K) turns the selected plane, or the last, over; On / off, Remove and Remove all do what they say. The outline of a plane is drawn where the other planes leave it, so the six planes of a box are the box.

The cutting is done in double precision on the shapes themselves: a line is cut to the parts inside, a triangle to a convex piece that is cut again into triangles which keep the normal of the whole, a point or a label outside is dropped; what the graphics card is given is the cut scene, relative to the origin of
each view, made on another thread while the old one is drawn. One slack decides what is on a plane: a billionth of the size of the scene and of a unit, and a trillionth of its largest coordinate (3.6E-7 mm on a scene 360 across at the origin, 8.6E-7 mm on the same scene 500 000 from it). A point no further outside a plane than that is ON it and is kept, so a face that lies in the plane, to the rounding of its corners, is kept whole whichever side the plane keeps, and is drawn, picked, snapped to, selected by a box and cut for its section by the same rule; a point beyond the slack is cut at the plane itself, so a line cut by a plane is as long as the plane leaves it. The picking, the snaps and the box select see the same cut: a click goes through what is cut away to what is behind it, a snap at the end of a line that is cut away is not offered, and a window takes an item by the part that is left. **The section lines** are
where a plane meets the triangles of a body or a mesh, joined into lines in double precision: a box 30 by 20 by 10 cut at half its height has one closed line of four corners, 100 long; a prism of 64 sides cut across its axis, one of 64 corners as long as its perimeter; a box with a through hole of 10 by 10, two lines of 100 and 40. They are
drawn in a darker shade of the item's colour, cut by the other planes so that only what lies inside the clipped region is left, and are not filled: a section is its outline. Where a plane only touches a body along a line, as a plane touches the top of a bore, the section is the outline with a line across it where the plane touches, and it is reported as the pieces between the points where three lines meet, none of them called closed that does not return to its start; a closed outline cut by another plane where it begins and ends is one piece across its start. Lines, curves and points are cut but have no section. A plane dragged over a large scene is not cut a hundred times: one cut runs and one waits, and the one that is out of date is stopped between thousands of triangles. A clip put in force while a scene loads is applied to the scene when it is shown. A view that is not shown lets go of its geometry and its overlay, and makes them again when it is shown.

**Saved views.** Window > Save this view as... (Ctrl+Shift+V) names the active view and keeps it in the scene; Window > Saved views brings one back into the active view. The scene file holds them under `"views"` (eye, target, up, whether there is perspective, how wide a view with none is), so a repro opens at the spot it is about: a scene that
has saved views opens the active view at the first, and a script puts one there with `Scene.View("the spike", new GeoPoint3(40, -60, 30), new GeoPoint3(40, 0, 0), new GeoVector3(0, 0, 1))`, or with `ortho: true, width: 20` for a view with no perspective 20 wide (the width is that of the view the file is opened in, whatever its shape). A `--view` on the command line is the view that was asked for, and wins over the first saved view; a comparison opens at the saved view of the build under test as a scene does. A view in the file that is not one (no name, an eye on its target) is left out with a warning in the output, and the rest of the scene opens. The camera of the viewer never rolls, so
`up` is read only where the view looks straight down or up (within a twentieth of a degree of it): +Z is up everywhere else. The eye and the target are points, each carried to the last place of its largest coordinate, so a view saved from 0.1 away from a target 10 000 from the origin has its direction to a few parts in 1E-10. A scene with no views is
written without the list, and a scene written before there were any opens as it did. The views saved while looking at a scene a script made are in the scene, not in the script: save the scene, or put `Scene.View` in the script, to keep them across a run.

**Pictures.** Ctrl+Shift+S (Window > Screenshot...) asks for the active view or all the views as they stand in the window, one, two or four times the size of the window, and white, as in the window, or transparent behind the scene, then for the file, and writes a PNG. Each view is drawn again by the graphics card at the size
it has in the picture, so 4 is four times as sharp and not four times as large a blur. A transparent picture is made by drawing each view twice, over white and over black: a pixel the scene covers is the same in both, one it does not is white and black, and the alpha of every pixel is 1 minus the difference of the two over 255, its colour the black one over the alpha;
the lines are as smooth at their edges as the antialiasing makes them. `--screenshot out.png --views 4 --scale 2 --background white` does the same without a person.

## Diagnostics

Ctrl+D (Diagnostics > Pane) opens the pane on the right. Each check in it is a tick box; **Run** makes the ticked ones on the selected items, or on every item
when none is selected, on a thread of its own, with the library at the tolerance of the scene (`Tolerance.Use` on that thread). **Cancel**, or Esc, stops it,
and a new run stops and replaces the one before. Nothing runs again when the selection changes: the pane shows what the last run found. The findings are listed
with a number, the item, the kind and a line of text in which every number is written to ten significant digits and the member that gave it is named in
brackets: `GeoSolid3.Validate() OpenEdge` where the library read it, `viewer: ...` where the viewer's own check did. A fault is red and a fact is grey.
Clicking a finding selects its item, zooms to the spot (the spot is at least 20 times `EqualPoint` across, so a needle 0.0005 wide is in view) and marks
it with a green cross; clicking the finding already selected zooms to it again. What a check draws is drawn in front of the scene, in its own colour, until
**Clear** or until another scene is opened.

| Check | Finds | Read by | Drawn |
|---|---|---|---|
| Edges | an edge of a body or mesh used by one face (open), by three or more (non-manifold), or run the same way by the two faces on it (inconsistent winding); open edges of a body that lie along one line are counted by stretch, as the library counts: the line is cut at every end of every edge on it and on each piece the edges one way and the other are counted; a piece with as many each way is met (a T-junction where the line was cut, a fact), one with an odd number of faces is open or non-manifold, one with an even and unequal number is run the same way (so a side cut at thirds under a whole top and a second top cut at halves, 1 one way against 2 the other, is 4 stretches of 3 faces, faults, as `Validate()` says) | a body: `GeoSolid3.Validate()` (its stretches of edge, by overlap); a mesh and a body the library refuses: the viewer's count of the faces on each edge, with the T-junction rule for a body. Edges four or more faces meet on as many one way as the other are a fact (the body is valid), the rest of these are faults, and so is an open edge of a body but not the outline of a mesh | red, magenta, orange, olive lines |
| Body report | `Validate()` valid or not, `IsClosed()`, whether the faces are wound alike, the signed volume (negative: wound inwards), `GetVolume()`, and how many of each kind of issue; the openings of a body are asked as bodies of their own | `GeoSolid3.Validate()`, `IsClosed()`, `TryGetVolume()` | text |
| Face normals | the normal of each face of a body, a mesh or a flat shape in space, from the right-hand rule round its outline | the viewer | arrows |
| Faces not flat | a face with a corner further off its plane than `EqualPlanar`, measured as the library measures it: the distance from the plane through the first corner of the outline, with the right-hand normal of the outline, of every corner of the outline and the holes; a triangle with no hole is not measured | a body: `Validate()` (`NotFlatFace`); otherwise the viewer, by the same rule, and so for each face of a body the library refuses at construction (the finding says so and names the face that made it refuse) | the outline of the face |
| Loops crossing themselves | a loop of a polygon, chain, face or body that crosses or touches itself, with the points, and a hole of a face that crosses or touches its outline or another hole; two edges meet where they come within `EqualPoint`; a meeting is a crossing where one passes from one side of the other, a touch where an end of one lies on the other, and a tangent where they come within `EqualPoint` without changing side (all three are faults: `IsSimple()` says no to each); meetings within `EqualPoint` of one another, joined in any chain, are one place; edges joined by edges no longer than `EqualPoint` do not count as meeting where they join | the viewer's exact curves (`CurveIntersections`, arcs exact), with `IsSimple()` of the polygon asked beside it: the finding says whether it agrees. A loop on its own is asked of the library always; a face of a body, of which there can be thousands, only when the viewer finds it crossing itself | a dot |
| Winding | which way each loop runs: signed area, clockwise or not; a hole that runs the same way as its outline is a fact, not a fault, since the library stores the holes of a `GeoFace3` that way (its constructor turns a hole that runs against the outline) and reads a hole of a `GeoFace2` either way | `GeoPolygon2` and `GeoPolygonArc2`: `SignedArea`, `IsClockwise`; the rest the viewer | an arrow on each edge, up to 64 to a loop |
| Duplicate vertices | two vertices of a loop, or two corners of a body's faces, or two vertices of a mesh, closer than `EqualPoint`; in a body corners that are the same point to the bit, or within 64 units in the last place of the largest coordinate (rounding), are one corner and are told of nothing, and before the sweep they are made one, so that a cone's apex 16 000 faces share costs 16 000 points and not 128 million pairs. In a body the library calls valid (`Validate()`, asked whenever Duplicates is ticked for a body) the corners it welds are a fact; otherwise a fault. At a scene tolerance finer than the rounding of the numbers, crossings use the tolerance `CurveIntersections` raises it to, 16 units in the last place | the viewer | a segment and a dot |
| Needle spikes | a vertex where the loop turns back by more than 179.9 degrees, from the tangents of the exact curves; a tip given twice is one tip. It is a fault where the spike is no wider than `EqualPoint` where its shorter edge ends (that edge times the sine of the angle between the two), and a fact where it is wider though it turns back as far: the 4 000 thin faces of a cone the library calls valid turn back by 179.92 degrees at the apex and are 1.57 wide. The facts of an item are one line with the count and the narrowest; the faults stay listed. A loop that is itself a sliver is told as the sliver and not as a needle at each corner when the Slivers check is on | the viewer | a dot, the two edges |
| Slivers | a loop or triangle whose area is under `EqualPoint` times its longest edge (not asked of a loop that crosses itself, whose signed area is a difference); a face of a body: the library's `SliverFace`, whose rule is different: the area at most half of `EqualPoint` times the perimeter, that is an average width, twice the area over the perimeter, of no more than `EqualPoint` | the viewer; for a body `Validate()` | the outline |
| Gap | the library's `DistanceTo` and `GetShortestLineTo` between exactly two selected items, and the magnifier below | `DistanceTo`, `GetShortestLineTo` | the shortest segment, the lens |
| Tolerance spheres | a sphere of radius `EqualPoint` round each vertex (three great circles), the first 200 in order of place; another vertex inside a sphere is the same point to the library | the viewer | circles |
| Pieces | the items of each layer, in the order of the scene, coloured one by one by their number in the layer, with the number and the area (the volume of a body, the length of a chain) | `Area`, `TryGetVolume`, `Length` of the shape | the outline in a colour, a label |

The viewer's own count of the faces on an edge welds corners within `EqualPoint` into one and matches two edges by their welded ends, as a mesh does by its
indexes, and then matches by overlap what is left open in a body, as the library does: the T-junction rule in the table. The edges of a **mesh** are the viewer's alone, since the library has no reading of a
mesh, and the outline of a mesh is open edges by the count, not faults: only the non-manifold and the inconsistently wound edges of a mesh are.
Switching the library off (`--do "diag edges own"`) makes the viewer's count of a body and lets the two be compared; they agree on the box with its top left out
(4 open edges), the box with a face turned over (4 same-way edges) and the box with a fin (1 non-manifold edge and 3 open), and on 400 grid unions and cuts of boxes, where the viewer's count finds no fault in a body the library calls valid.

Every list is in a fixed order and none follows a hash table: the findings of an item by check, kind, place (x, then y, then z), size and text; the items in the order of
the scene. At most 200 findings of one kind are kept for an item (`DiagnosticOptions.MaxFindingsPerKind`), with a note giving the number left out, and what
they drew is left out with them. Loops and edges are swept along the axis with the fewest candidate pairs, counted first, so that a loop 100 000 long and 1 wide is not
searched quadratically.

**The gap magnifier.** A gap of 0.0034 between two squares in a view where a pixel is 0.3 is a hundredth of a pixel. The lens draws the lines of the two items
within 100 pixels of the middle of the gap, and the gap between them, scaled about that middle by the power of ten, from 10 to 1 000 000, that brings the gap
nearest to 40 pixels; a gap of 12.7 pixels or more is already seen and is not scaled. The label gives the true number and the scale (`gap 0.0034  drawn x10000`). The
scale is chosen again a quarter of a second after the view stops moving, when the zoom has changed it. A segment is cut to the window and scaled; an arc or a circle is cut again from its own numbers into chords so fine that none is more than a tenth of a pixel from the curve once scaled (at most 4 096 chords), not scaled from the chords the view draws it with, which are a tenth of EqualPoint from the curve and would show as facets at 10 000 times.

The ticked checks, the width of the pane and whether it is open are kept in the settings.

## Scripts

A `.csx` file is C# with these globals; the library's namespaces, `System`, `System.Linq` and `System.Collections.Generic` are
imported, so a script names no namespace.

| Global | Is |
|---|---|
| `Scene` | the `SceneWriter` being built: `Scene.Layer("name", "#RRGGBB")`, `Scene.Add(geo, ...)`, `Scene.Text(point, "text")` |
| `Show(geo, layer, name, color, props)` | `Scene.Add`; `geo` is any shape the viewer draws, or an array or list of them |
| `Note(text)` | a line in the output pane |
| `Tol` | the `Tolerance` the script works at, which starts as `Tolerance.Default` whatever the process has set `Tolerance.Global` to. Setting it sets the tolerance of the library on the script's thread and all four thresholds the scene is read at |
| `Cancel` | the `CancellationToken` that Esc cancels; a long loop that shows and notes nothing should test it |

`#r` and `#load` work, with paths from the folder of the script. A compile error is reported as `file.csx(3,14): error CS0029: ...`,
an exception as `file.csx(2): ArgumentException: ...`, and the scene shown before stays on the screen. The script runs on a thread
of its own, and what follows an `await` comes back to that thread, at the tolerance of the script. Esc cancels the token: `Show`, `Note`, `Scene.Add` and setting `Tol` then throw, which stops a loop that calls any of them, and a script that tests `Cancel` stops where it asks. A loop that does none of these cannot be stopped: it runs to its end, and while a script is alive F5 and Ctrl+R answer "a script is still running (Esc to stop)" and start no second one. A script that awaits something that never completes cannot be stopped either. Esc a second time, or F5 or Ctrl+R after an Esc, abandons the run: the window is free, the script's result is ignored, the output says so, and its thread keeps running until it ends, since a thread cannot be taken from a script that never looks at its token. Two traps of the script's own: a `catch (Exception)` around `Show` swallows the stop, and `.Result` or `.Wait()` on a task whose continuation has to come back to the script's thread deadlocks it, since that thread is the one waiting; `await` it instead. Script > Run again when the file is saved
runs it again whenever its file changes.

```csharp
var s = new SceneWriter("item 6 star 2:116");
s.Layer("source", "#202020").Layer("round", "#1f77b4");
s.Add(loop, layer: "source", name: "star 2:116", props: new { distance = -0.3, join = "Round" });
s.Add(offsets, layer: "round");
s.Save(@"C:\scenes\star.ghscene.json");
```

`Add` takes every kind the viewer draws: points, segments, rays, chains, polygons, chains and loops with arcs (in the plane and in
space), arcs, circles, ellipses, rectangles, triangles, faces, bodies with their openings, meshes, boxes, oriented boxes and planes.
`Add(object)` takes any of them, or a sequence of them, and throws an `ArgumentException` naming the type it cannot draw.

### Samples

`samples/` holds six small scripts, each built in code:

| Sample | Shows |
|---|---|
| `box-with-opening.csx` | a box 100 by 60 by 40 with a round hole and a pocket as openings, and its volume |
| `curved-offset-joins.csx` | a loop with two arcs offset outward and inward with the round, miter and chamfer joins, each on its own layer |
| `mesh.csx` | an L-shaped plate as triangles, convex pieces and a grid, and tilted in space |
| `snaps-and-measuring.csx` | a plate 200 by 120 with a rounded corner, a hole, two crossing lines and an arc, with things to try the snaps, the measuring and the box select on |
| `diagnostics.csx` | one of each fault the diagnostics look for: an open box, a box with a face turned over, a fin on a box, a mesh with a triangle turned over, a bow tie, a needle, a doubled corner, a sliver, arcs that cross, a face off flat, two squares 0.0034 apart, and three pieces on a layer |
| `sections-and-views.csx` | a block 100 by 60 by 40 with a bore along Y, a pipe through the bore and a marker box standing out at a corner, with two saved views, to try the clip planes (select the marker and press Ctrl+B), the work plane and the four views on; the scene opens at the front view of the bore |

## Comparing two builds

File > Compare... (Ctrl+Shift+C) asks for the script and two builds of the library, runs the script against each in a child process, and shows
what each built, laid over each other. It is for the question "what did this change do to this shape": a script that shows an offset of a loop, run against
the library of last week and the library of now.

A **build** is one of three: the viewer's *own library*; a *DLL* at a path; or a *commit* of a Git repository, built into the cache. Each side runs in a process of its own, the
runner, which is a copy of the runner folder with that build's `GeometryHelper.dll` put in it, in `%LOCALAPPDATA%\GeometryHelper.Viewer\runners\<SHA-256 of the DLL>-<start of the SHA-256 of the runner program>`. The DLL is copied once, first, and the key is the hash of that
copy, so it names the bytes that run. A DLL seen before costs no copying, and two paths holding the same bytes share a folder; a runner program that has changed gets a folder of its own and the old one is left where it is, since a process
may have it open. Two runs that want the same copy take turns (a lock named by the folder), and the second finds it made. The runner runs the script with the same globals as the window
does (`Show`, `Note`, `Tol`), at the tolerance the script sets, and writes the scene; with `--measure`, which the viewer always gives it, it also puts into each item the numbers
its own build of the library gives for it (`measure.vertices`, `measure.length`, `measure.area`, `measure.volume`, 17 digits each, and `measure.note` where the library refused the shape at the tolerance of the scene and the numbers are those at 1E-12), since a build cannot be asked
in the viewer's process what another build says. A script that does not compile against a build shows the compiler errors of that side, with the file and line number, and the scene before stays.

**Older builds.** A library of a lower version than the viewer's loads too: the runner puts its own resolution of GeometryHelper in front of the runtime's, which refuses a lower version. A type the older build lacks does not
stop it either: the runner writes, reads and measures each kind of shape in a method of its own and picks it by the name of the type, so a type is looked for only when an item of it is shown (a build of 12.1.0, which has no
`GeoRay2`, runs the scripts that do not use one). A script that *uses* a type the older build lacks cannot compile for that side, and that side shows the compiler error; the other side is not affected.

A **commit** is read out of the repository with `git archive` (no work tree is added: the work tree, the branches, the index and the list of work trees of the repository are not touched), unpacked beside the cache, built with
`dotnet build src/GeometryHelper/GeometryHelper.csproj -c Release`, and only the DLL is kept, in `%LOCALAPPDATA%\GeometryHelper.Viewer\builds\<the full hash>`; a hash, a short hash, a tag or a branch name are the
same commit once the hash is found. The commit must hold that project. The repository box starts from the one used last, else the one the script is in, else the one the viewer's own folder is in; `--do compare` takes the script's, else the viewer's, unless `repo=` says.

**Timeout and killing.** A script may run 120 seconds against a build (`CompareTimeoutSeconds` in the settings, the box in the dialog), a build of a commit 15 minutes. Esc, the timeout and closing the window kill the
process the viewer started and everything that process started, and no other: each run has a Windows job object of its own, which holds the process and its descendants, so they end together even when the first has already ended. The
wait covers the output too: a script that ends and leaves a child holding its output open is given two seconds, and the child is killed. What the runner and the build print goes to the output pane, one line at a time, behind `[left]` or `[right]`.

**The cache** keeps the newest 20 copies of the runner and the newest 20 built commits by when each was last used (`CompareCacheKeep` in the settings; never fewer than 2, since the two sides each need a copy at once). An entry used
within the longest a run and a build may take, plus an hour, is never trimmed, so a copy another viewer sharing the cache is running from is safe. An entry is renamed before it is deleted, and one that cannot be renamed (it is open) stays; the temporary
folders an interrupted copy or build left are deleted when a day old. It deletes only folders directly inside the runners and builds folders of the cache root.

**What is shown.** The two scenes are paired item by item. First by layer and name, the first of several of one name on the left with the first on the right, but only for a name the script gave (an item shown with no name is named
the kind and a number, `GeoPolygon2 3`, which moves when an earlier layer gains or loses a piece, so it is marked in the scene file as made up and never paired by); then what is left, on the same layer and kind, by data, an item whose
numbers are the same to the bit with its twin ("by data" in the table); then the rest by order ("by order"). A paired item whose numbers are the same to the bit (a number that differs in the last place, or `-0` against `0`, makes it
changed) and whose measures, as each build gave them, are too, is drawn once, in grey, on the layer `identical`. If its data is the same and the numbers the builds gave differ (a fix to the measuring, a
note that one measured at 1E-12, an error one had), it is "numbers differ", drawn once in yellow on `numbers differ`, and the difference lists them: `Area -0.5, Volume: left '1', right none, Measure error: left none, right 'MissingMethodException: x'`.
A changed item is drawn twice: the left's red and dashed on `changed - left`, the right's green on `changed - right`. An item on one side only is orange (left) or blue (right). The dashes are a hundred and fiftieth of the scene
across and are made from the lines the item is drawn with, so a curve's chords are dashed too. Keys 1, 2 and 3 show the left side, the right side or both, by hiding what belongs to the other side; what is the same on both is always shown, and an item you hid yourself stays hidden whichever side is shown. A paired item
that stands in another place in the order than on the other side, the fewest that makes the rest climb, is marked "same, moved": order is not a change.

The **Compare** tab beside the output lists every item: status, layer, name, kind, the vertex count of each side, the length, area and volume each build gave (ten digits), whether the data is bit-identical, and the difference in
words: `Vertices +4, Length -238.6172558, Area -5890.820813, the numbers have a different layout (vertices added or taken away)`, or, where the layout is the same, how many numbers differ and by how much at most. Clicking a row selects its items and zooms to them; clicking the
selected row zooms again. Ctrl+R (or Run again) runs both sides again; File > Open of another file leaves the comparison and keeps the scene on the screen. The Properties and Output panes measure
what is selected with the viewer's own library, as for any scene; the numbers each build gave are the `measure.*` properties of the item and the columns of the table.

**Watching.** With "Run a side again when its DLL changes on disk" on (the Compare menu), a side whose build is the viewer's own library or a DLL at a path is run again, alone, a second after the file stops changing and only when its bytes are not the
bytes last run; the other side keeps its result. Saving the script, with Script > Run again when the file is saved on, runs both again, and a save that comes while a comparison runs is run once it has ended. Whenever a side is run again
and the script text is not the text the other side ran with (the viewer keeps a hash of it for each side), both sides are run. A commit never changes.

Two things the comparison cannot see: a build older than the viewer's may lack a member the viewer measures with (that item then carries `measure.error` and shows `-`, and a row whose data is the same shows "numbers differ"), and the two scenes
are drawn at the tolerance of the left one (the Compare output says when they were written at different tolerances).

## Exporting a test

Tools > Export test... (Ctrl+T) writes an xUnit test skeleton for the selected items to the clipboard and to a file picked in a dialog. It never writes into `tests/` by itself. Each item becomes one `[Fact]`, named as a sentence (`Miter1544412_HasTheExpectedNumbers`),
with the tolerance of the scene passed explicitly (`Tolerance.Use(tolerance)`, never `Tolerance.Global`), the shape built in code from the 17-digit literals of its numbers with the library's own constructors, and one assertion for each number the library gives for it
(vertices, length, area, volume of a body, ...). The value the viewer measured is in the comment above it and the expected value is `double.NaN` (`-1` for a count) to be filled in, so the test is red until then; a shape with no number to assert gets `Assert.Fail`. A label, and a
kind the viewer does not know, are left out and named in a comment, and so are the items beyond forty. In a comparison each selected copy is taken from its own side: the item as that side's script made it, at that side's tolerance, with
the numbers that side's build gave (the comment then says "the build that made the scene measured ..." and carries a `measure.error` or a 1E-12 note), whichever copy you select; select the right copy (green) to take the build under test. What both sides share is taken from the right.

## Scene files

A scene is `*.ghscene.json`: a header, one line to a layer and one line to an item, every number as the 17 digits that read back
as the same double, so a scene holds a shape exactly. `SceneItem.ToGeometry()` gives the library object back. `Scene` is in
`GeometryHelper.Viewer.Core`, which a net48 harness can reference to dump the shapes of a failing case:

```csharp
new SceneWriter("repro").Add(shape, layer: "input").Save(path);
```

An item the script showed with no name carries `"auto":true` in its line: its name is the kind and a number, which a comparison never pairs by. A scene file with no such field has only named items.

A kind a newer viewer wrote and this one does not know is kept in the scene, listed in the output, and written again unchanged.

A scene with saved views has one more block between the layers and the items, `"views":[ ... ]`, a line to a view: `{"name":"v","eye":[0,0,10],"target":[0,0,0],"up":[0,1,0],"ortho":true,"width":12.5}`, the numbers in the 17 digits as the rest. A scene with none is written without
the block, as every scene was before, and a file without it reads with no views; the version of the format stays 1. A view that is not one (no name, an eye on its target) is refused with its number.

**Never save a scene of a real model in the repository.** Geometry from real projects does not go into this repository, and a
scene holds it whole. Saving a scene to a folder inside a Git repository asks first. Build a body in code that shows the same thing.

## Settings

`%APPDATA%\GeometryHelper.Viewer\settings.json` keeps the window layout, the width of the panes, the last folder, the render mode, the layout of the views (how many, which is active, the render mode and the link of each of the four: `Layout`), how the last picture was taken (`Screenshot`),
the grid, run-on-save, the last ten files, and the last comparison (`CompareLeft`, `CompareRight`, `CompareRepository`, `CompareTimeoutSeconds`, `CompareCacheKeep`, `CompareWatch`). An error the window could not carry on from is appended to `error.log` beside it.
