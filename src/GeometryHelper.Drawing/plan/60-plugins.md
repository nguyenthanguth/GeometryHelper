# Plugins

There are two kinds of plugin, one for each part (A16). They are loaded, unloaded and reloaded the same way (A14), and
they differ in what they work on:

| | Command plugin (Part A, K4) | Drawing plugin (Part B, P) |
|---|---|---|
| Is like | an AutoCAD .NET plugin (`[CommandMethod]`) | a Tekla Structures drawing plugin (`DrawingPluginBase`) |
| Works on | the entities of a 2D drawing: lines, text, layers, selections | a model drawing: its views, the model objects in them, dimensions and marks tied to those objects |
| Runs when | the user types its command | the user starts it, and again by itself when the drawing is updated from the model (if switched on) |
| Knows the model | no | yes: the object behind a view or a bolt group, its properties, its points |
| Sample | "Number the selected circles" | "Bolt dimensions" |
| Is in | `GeometryHelper.Dxf.Viewer` | `GeometryHelper.Drawing` |

Both are .NET assemblies (.dll) that target netstandard2.0 (D16), so one .dll runs inside Tekla (.NET Framework 4.8) and
in the standalone app (.NET 8). Neither touches WPF.

## Loading, unloading and reloading (A14, both kinds)

| Command | Alias | Does |
|---|---|---|
| `PLUGINLOAD` | `NETLOAD` | pick any .dll (or .ghplugin) in a file dialog; the app finds the command plugins and drawing plugins in it and loads them at once, for this session |
| `PLUGINUNLOAD` | | unload a loaded plugin; its commands and ribbon buttons go away |
| `PLUGINRELOAD` | | reload a plugin from its file, for plugin authors: change the code, build, reload, without restarting the app |
| `PLUGIN` | `PL` | run an installed drawing plugin by name (a command plugin is run by its own command) |

- **Startup list**, as AutoCAD's APPLOAD Startup Suite: a plugin loaded with `PLUGINLOAD` can be marked "load at every
  start"; the plugin folder is always loaded at start.
- **Isolation (D19).** In the standalone app (.NET 8) each plugin is loaded into a collectible `AssemblyLoadContext` of
  its own, so it can be unloaded, and its dependencies do not clash with another plugin's.
- **Shadow copy.** The app copies a plugin's files to a temporary folder and loads the copy, so the original .dll is not
  locked and can be rebuilt while the app runs. This is what makes `PLUGINRELOAD` work; AutoCAD cannot unload a .NET
  assembly once NETLOAD has loaded it.
- **Hosted in Tekla (.NET Framework 4.8).** .NET Framework cannot unload an assembly. Unloading and reloading would need
  one AppDomain per plugin, with every call across it going through `MarshalByRefObject`, which is heavy. Proposed: in
  Tekla, `PLUGINUNLOAD` switches a plugin off (its commands hidden, never run again in the session), and a new build is
  picked up after Tekla restarts. The standalone app does the whole thing.
- **Trusted folders (D21)**, as AutoCAD's TRUSTEDPATHS: loading a .dll is running code. The app keeps a list of trusted
  folders (the plugin folder among them); a .dll from anywhere else asks for confirmation first, and every load is logged
  with the file's full path.
- **A package for installing (D18).** A plugin ships as one `.ghplugin` file (a zip) holding its .dll files and a
  `plugin.json`: id, name, version, author, kind (command or drawing), least API version, commands, icon. It is installed
  from the Plugin manager or by dropping the file on the window. A bare .dll is accepted too, through `PLUGINLOAD`.
- **Versions (D20).** Each API has a version of its own. A plugin states the least version it needs; the app refuses a
  plugin that needs a newer one and says why.

## Command plugins (Part A)

A class with methods marked as commands, as AutoCAD's .NET API has them (D26):

```csharp
using GeometryHelper.Dxf.Viewer.Plugins;

public sealed class CircleTools
{
    [CadCommand("NUMCIRCLES", Alias = "NC", Description = "Number the selected circles")]
    public void NumberCircles(CadCommandContext ctx)
    {
        var circles = ctx.Select<DxfCircle>("Select circles to number");   // prompt on the command line and canvas
        int start = ctx.GetInteger("Start at", defaultValue: 1);
        using var step = ctx.BeginUndoStep();                              // the whole run is one step
        foreach (var c in circles)
            ctx.Document.AddText(c.Center, (start++).ToString(), height: 2.5, layer: "Mark");
    }
}
```

A command plugin can read and change the document's entities, ask for points, selections, numbers, keywords and
strings, add and remove entities, and have settings of its own (declared, drawn by the app, D17). Entities that have an
owner (Part B's) are refused by the owner's rule, not by the plugin.

## Drawing plugins (Part B)

A class derived from `DrawingPlugin`, with the shape of a Tekla drawing plugin (D27): what the user picks, then the run.

```csharp
using GeometryHelper.Drawing.Plugins;

public sealed class BoltDimensions : DrawingPlugin
{
    public override string Name => "Bolt dimensions";
    public override string Command => "BOLTDIM";

    // Like Tekla's DefineInput: what the user picks before Run.
    public override void DefineInput(PluginInput input)
    {
        input.AddView("Pick a view");
        input.AddObjects("Pick bolt groups", DrawingObjectKind.BoltGroup);
    }

    public override void Run(DrawingPluginContext ctx)
    {
        DrawingView view = ctx.View(0);
        foreach (DrawingObject bolts in ctx.Objects(1))
        {
            IReadOnlyList<GeoPoint2> points = view.Project(bolts.HolePositions);   // model to view 2D
            ctx.Drawing.AddDimension(view, points, DimensionKind.Chain, offset: 10, tiedTo: bolts);
        }
    }
}
```

What a drawing plugin can do:

- read the drawing document (L7): its views, annotation, the model objects of each view and their properties;
- add, edit and delete annotation (dimensions, marks, text, tables, symbols) and views, through the public API, tied to
  model objects by id;
- ask the user: pick views, objects, points, enter values, through `DefineInput`;
- have settings of its own, declared by the plugin and shown by the app (D17), stored in the drawing;
- run when the user starts it (the ribbon, its command, or `PLUGIN`), and again by itself when the drawing is updated, if
  the user switches that on.

**It runs again on update (D27).** The drawing keeps, for each run of a plugin, the plugin's name and version, the ids of
the objects it was given and its settings. When the model changes and the drawing is updated, those objects are looked
up again and the plugin runs again on them; its old dimensions are removed first. A plugin's results are therefore
never hand edits and never go stale. A hand edit made to one of them afterwards (a dimension moved) is kept through the
update as any hand edit is. If an object a run was given has gone from the model, the run is dropped and the update
report says so.

The core defines only the base class and the context (no WPF), so one plugin runs in the standalone app, hosted in
Tekla, and when drawings are made with no UI (where the questions to the user are skipped or take their defaults).

## Like Tekla's drawing plugins, not Tekla's plugins (A15)

The plugins do what Tekla's drawing plugins do, with our own API; Tekla's plugin .dll files are not loaded (the
maintainer, 2026-11-08: "similar functions, not using Tekla's plugins"). They could not be anyway: a Tekla drawing plugin
derives from `Tekla.Structures.Plugins.DrawingPluginBase` (strong-named by Trimble, token `2f04dbe497b71114`, checked in
`src/GeometryHelper.TeklaConvert/Lib2025`), is driven by Tekla's own core, and changes Tekla's drawing.

The ideas carry over, so someone who has written a Tekla plugin finds the same shape:

| Tekla drawing plugin | GeometryHelper drawing plugin |
|---|---|
| class derived from `DrawingPluginBase` | class derived from `DrawingPlugin` |
| `DefineInput()`: picks the user makes | `DefineInput(PluginInput)`: the same, as a list of picks |
| `Run(List<InputDefinition>)` | `Run(DrawingPluginContext)`, the picks through `ctx.View(i)`, `ctx.Objects(i)` |
| the plugin's attributes and its dialog | declared settings, drawn by the app (D17) |
| `Tekla.Structures.Drawing` objects (views, dimensions, marks) | the drawing document (L7): views, dimensions, marks |
| runs again when Tekla updates the drawing | runs again on `Update` (D27) |

A command plugin is the AutoCAD half: `[CadCommand]` methods as AutoCAD's `[CommandMethod]`.

## Safety (both kinds)

- Each run of a plugin is **one undo step**: Ctrl+Z removes everything that run changed.
- A plugin that throws has every change of that run dropped, the document stays as it was, and the error is logged as a
  Warn with the plugin's name and shown on the command line.
- A plugin goes through the public API only; it never reaches into a core's internals.
- A plugin is code running on the user's machine with their rights: the trusted folders (D21) and the load log show
  plainly which plugins are loaded and from where.

## For plugin authors (D20)

- The cores' NuGet packages are the SDK: a command plugin references `GeometryHelper.Dxf.Viewer`, a drawing plugin
  `GeometryHelper.Drawing` (which brings `GeometryHelper.Dxf.Viewer` along).
- Project templates (`dotnet new ghcad-command`, `dotnet new ghdrawing-plugin`) make a plugin with one command, one
  setting and a `plugin.json`.
- `PLUGINRELOAD` with the shadow copy gives a build-and-try loop without restarting the app.

## The sample plugins

- **Number circles** (command plugin, C3), above.
- **Bolt dimensions** (drawing plugin, P1): for every bolt group in the selected view, a chain dimension of the hole
  pitch and the distance to the part's end.

Both live in `examples/` (not staged without asking the maintainer).

## Checks

Command plugins (C3):

- the sample runs, asks for a selection and a number, and draws; undo removes all it did;
- a version that throws on purpose leaves the document unchanged;
- it is rebuilt while loaded, reloaded, and runs the new code without a restart; after unloading, its load context is
  collected;
- a .dll outside the trusted folders asks first.

Drawing plugins (P1):

- the sample runs on TC5 in the standalone app and hosted in Tekla, with no change to the modules' code;
- after TC5's bolts move and the drawing is updated, its dimensions follow;
- the same four checks as above.

## Questions

- Q6: which drawing plugins are needed first.
- Whether scripts (C# script, Python) are wanted besides assemblies: later, if wanted.
