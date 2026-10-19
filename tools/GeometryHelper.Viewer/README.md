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
| `GeometryHelper.Viewer.Core` | netstandard2.0: the scene model, `SceneWriter` and `SceneReader`, the drawing primitives, the camera maths, the keymap. A net48 harness can reference it and dump a scene. |
| `GeometryHelper.Viewer` | the window, the 3D view and the script host |
| `GeometryHelper.Viewer.Tests` | xUnit, net8.0: tests of Core, and of the script host, whose source it compiles in because the app is a Windows desktop project |

### Command line

```
GeometryHelper.Viewer.exe [file] [--check] [--screenshot out.png] [--view iso|top|front|right] [--mode wireframe|shaded|edges|transparent]
```

| Switch | Does |
|---|---|
| `file` | opens a `.csx` script or a `.ghscene.json` scene; a file dropped on the window is opened the same way |
| `--check` | loads the file with no window, prints what it holds, and exits 0, or 1 with the errors, a script's with the file and line number |
| `--screenshot out.png` | opens the file, fits the view, writes the picture of the 3D view, and exits 0; 1 when the file did not open |
| `--view`, `--mode` | the standard view and the render mode a picture is taken in |

`--check` and `--screenshot` do not read or write the settings of the user.

## What the window holds

- Left, the **scene tree**: layers, and the items on them, each with a check box that shows or hides it and a swatch of its colour.
  Clicking an item selects it, clicking a layer selects all its items.
- Middle, the **3D view**, with the axes at the bottom left, and below it the **output**: what a script says with `Note`, its errors,
  and what the viewer could not draw.
- Right, the **properties** of the selection: kind, name, layer, the properties the script gave it, and the numbers the library
  gives for the shape rebuilt from the scene (vertices, lengths, areas, the volume of a body and whether it is closed) and its bounds.
- The **status bar**: where the cursor is on the plane z = 0, the snap in force (none in this version), how many items are
  selected, the point tolerance of the scene (`EqualPoint`), and the render mode.

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
of radius 500 takes 4 968 pieces and one of radius 60 000 takes 54 415. A curve that would take more than the cap is cut in the cap
and the output says so. The bounds of a curve are those of the exact curve, not of its chords.

The scene keeps the tolerance it was written under: all four thresholds, the point, vector, angle and planar ones. A `SceneWriter`
takes the tolerance in force on its thread when it is made, and `Tol` in a script sets all four. A shape is rebuilt, drawn and measured
at that tolerance, so a face built under a planar tolerance of 0.01 is not refused at 0.001. Where the library still refuses a shape,
it is rebuilt at 1E-12 and the properties say "rebuilt and measured at 1E-12"; where it refuses that too, the properties still show the raw vertex
count and the bounds of what is drawn. Where the library keeps fewer vertices than the scene holds, the properties say how many.

The surfaces are pushed back from their edges by a few steps of the depth buffer so that the edges show. The orthographic eye stands
just outside the depth of the scene (a twentieth of it beyond the nearest point), so a plan of a site 60 000 across is a view a few
millimetres deep; a perspective view clips at a hundredth of its distance to the target, so the target is never cut however far the
rest of the scene reaches. The push is the fewest steps that come to 2E-4 mm, at least 2 and at most 400: 6E-6 mm a step in an
orthographic view 100 mm deep, 2E-3 mm in a perspective view of a 100 mm part, and no depth buffer can go below about 4E-3 mm in a view
60 000 deep, whatever the push.

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
| Cancel | Esc: clears the selection and stops a script that is running |
| Hide selected / show all | Ctrl+H / Ctrl+Shift+H |
| Properties | Double-click, or Alt+Enter |
| Run the script / run it again | F5 / Ctrl+R (File > Reload does the same) |
| Open / save the scene as | Ctrl+O / Ctrl+S |
| Rotation centre, box select, measure, snaps, screenshot | V, Shift+V, left drag, F2 to F4, F8 to F12, Ctrl+Shift+S: bound, and not in this version |

The mouse follows the scene: dragging the middle button right moves the scene right, and Ctrl and the middle button turns the near
side of the scene right. Ctrl+Left turns the scene by 15 degrees counter-clockwise seen from above, Ctrl+Up tilts the camera down
towards the scene. The view looks along -Z with +Y up in the plan, and +Z is up in every other view.

## Scripts

A `.csx` file is C# with these globals; the library's namespaces, `System`, `System.Linq` and `System.Collections.Generic` are
imported, so a script names no namespace.

| Global | Is |
|---|---|
| `Scene` | the `SceneWriter` being built: `Scene.Layer("name", "#RRGGBB")`, `Scene.Add(geo, ...)`, `Scene.Text(point, "text")` |
| `Show(geo, layer, name, color, props)` | `Scene.Add`; `geo` is any shape the viewer draws, or an array or list of them |
| `Note(text)` | a line in the output pane |
| `Tol` | the `Tolerance` the script works at, the one in force when it starts. Setting it sets the tolerance of the library on the script's thread and all four thresholds the scene is read at |
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

`samples/` holds three small scripts, each built in code:

| Sample | Shows |
|---|---|
| `box-with-opening.csx` | a box 100 by 60 by 40 with a round hole and a pocket as openings, and its volume |
| `curved-offset-joins.csx` | a loop with two arcs offset outward and inward with the round, miter and chamfer joins, each on its own layer |
| `mesh.csx` | an L-shaped plate as triangles, convex pieces and a grid, and tilted in space |

## Scene files

A scene is `*.ghscene.json`: a header, one line to a layer and one line to an item, every number as the 17 digits that read back
as the same double, so a scene holds a shape exactly. `SceneItem.ToGeometry()` gives the library object back. `Scene` is in
`GeometryHelper.Viewer.Core`, which a net48 harness can reference to dump the shapes of a failing case:

```csharp
new SceneWriter("repro").Add(shape, layer: "input").Save(path);
```

A kind a newer viewer wrote and this one does not know is kept in the scene, listed in the output, and written again unchanged.

**Never save a scene of a real model in the repository.** Geometry from real projects does not go into this repository, and a
scene holds it whole. Saving a scene to a folder inside a Git repository asks first. Build a body in code that shows the same thing.

## Settings

`%APPDATA%\GeometryHelper.Viewer\settings.json` keeps the window layout, the width of the panes, the last folder, the render mode,
the grid, run-on-save, and the last ten files. An error the window could not carry on from is appended to `error.log` beside it.
