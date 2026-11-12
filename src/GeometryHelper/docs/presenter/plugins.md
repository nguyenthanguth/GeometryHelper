# Command plugins

A command written in a .dll of its own, loaded into the presenter while it runs, as AutoCAD's NETLOAD loads one, and
unloaded or loaded again without a restart where the runtime allows. A plugin is a class whose methods are marked as
commands; it asks its prompts as the presenter's own commands do, edits through the document, and is held to the same gate
and the same undo steps.

## A command

The sample below numbers the circles selected: it takes them (those selected before are taken, else it asks "Select
objects:"), asks for the number to start at, and writes a TEXT of each number at the circle's centre.

```csharp
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;

[assembly: CadPluginApi("1.0")]

public sealed class CircleTools
{
    [CadSetting("Text height", Minimum = 0.1, Maximum = 1000)]
    public double TextHeight { get; set; } = 2.5;

    [CadCommand("NUMCIRCLES", Alias = "NC", Description = "Number the selected circles", UsesSelection = true)]
    public IEnumerable<CadPrompt> NumberCircles(CadCommandContext c)
    {
        foreach (CadPrompt prompt in c.SelectObjects())
        {
            yield return prompt;
        }

        var circles = new List<DxfCircle>();

        foreach (string handle in c.Selected)
        {
            if (c.Document.FindEntity(handle) is DxfCircle circle)
            {
                circles.Add(circle);
            }
        }

        if (circles.Count == 0)
        {
            c.Write("No circle was selected.");
            yield break;
        }

        yield return CadPrompt.ForInteger("Start at", null, "1");
        int number = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;
        double height = TextHeight;

        // One call is one undo step; a refusal is written on the command line by Apply itself.
        c.Apply(null, scope =>
        {
            foreach (DxfCircle circle in circles)
            {
                var text = new DxfDBText();
                text.SetDatabaseDefaults(c.Database);
                text.Height = height;
                text.TextString = (number++).ToString(CultureInfo.InvariantCulture);
                text.Justify = DxfAttachmentPoint.MiddleCenter;
                text.AlignmentPoint = circle.Center;
                scope.Append(c.Space, text);
            }
        });

        c.Write("Numbered " + circles.Count + " circles.");
    }
}
```

The method is an iterator, like the presenter's own commands: it yields each [`CadPrompt`](xref:GeometryHelper.Dxf.Viewer.CadPrompt)
and reads the answer from `c.Input` when it is resumed, so it runs inside the command line with no thread of its own. That
is the one difference from AutoCAD's `[CommandMethod]`, which blocks on its prompts: a method that returns `void` and blocks
is refused, because it would have to run on a thread while the window waits and then reach the canvas from that thread,
which WPF does not allow.

| The attribute | Says |
|---|---|
| [`[CadCommand("NAME")]`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadCommandAttribute) | the method is a command; `Alias`, `Description`, and `UsesSelection` for a command that takes the objects selected before it, as MOVE does |
| [`[CadSetting("Label")]`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadSettingAttribute) | a public property is a setting the presenter shows; `Description`, `Minimum`, `Maximum` |
| [`[assembly: CadPluginApi("1.0")]`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPluginApiAttribute) | the least version of the plugin API the assembly needs; none is taken as 1.0 |

A command method may be static, or an instance method of a public class that is not generic and has a public constructor
that takes nothing, of which the plugin makes one when it is loaded. A setting is a property with a public get and set of
type `double`, `int`, `bool`, `string` or an enumeration, which the presenter draws a box for itself, so a plugin never
touches the window. A method or a class that cannot be one is left out, and each is a line in [`CadPlugin`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPlugin)`.Problems`.

What the plugin can use is the public API of [`CadCommandContext`](xref:GeometryHelper.Dxf.Viewer.CadCommandContext) and
[`CadDocument`](xref:GeometryHelper.Dxf.Viewer.CadDocument): `SelectObjects()`, `Selected`, `Input`, `Write`, `Apply`,
`OpenUndoStep()`, `Remembered`, the document's entities, layers and selection, the `CadPrompt.For...` methods for a point, a
distance, an angle, an object, a keyword, a string or an integer, and the [`CadLayerEditor`](xref:GeometryHelper.Dxf.Viewer.CadLayerEditor), [`CadGrips`](xref:GeometryHelper.Dxf.Viewer.CadGrips) and [`CadPropertyEditor`](xref:GeometryHelper.Dxf.Viewer.CadPropertyEditor)
calls. It reaches no internals.

## Running it

```csharp
CadPlugin plugin = CadPlugin.FromAssemblies("Circle tools", new[] { typeof(CircleTools).Assembly }, "1.0.0");
var registry = CadCommandRegistry.CreateDefault();
plugin.AddTo(registry);                          // NUMCIRCLES, and NC as its alias
var session = new CadCommandSession(document, registry);    // a document of three circles

session.Feed("NC");      // Select objects:
session.Feed("ALL");
session.Enter();         // Start at <1>:
session.Feed("7");       // three circles: TEXTs 7, 8 and 9, "Numbered 3 circles."
```

The three circles are numbered 7, 8 and 9 in one undo step, `NUMCIRCLES (3)`. This is also how a plugin's own tests run it:
`FromAssemblies` makes the plugin from assemblies already loaded, and a [`CadCommandSession`](xref:GeometryHelper.Dxf.Viewer.CadCommandSession) runs its commands with no window.

Every run is one undo step whatever it edits through the document. A run that throws has every change of it taken back, the
error is written on the command line with the plugin's name and logged as a Warn, and the document is as it was.
Entities that have an [owner](owners.md) are refused by the owner's rule, not by the plugin: the gate is the document's.

A setting is read from and written to the plugin's property through [`CadPluginSetting`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPluginSetting)`.Value`, and a value out of its range
is refused, the plugin keeping the one it had: setting the text height to 0.05 throws `The setting Text height lies from 0.1
to 1000.`

## The manifest and the package

A plugin says what it is in a `plugin.json`, read without loading any code:

```json
{ "id": "circle-tools", "name": "Circle tools", "version": "1.0.0", "author": "Nguyen Thang",
  "kind": "command", "minimumApiVersion": "1.0", "commands": [ "NUMCIRCLES" ], "icon": "icon.png" }
```

Only the name is needed: the id is the name where none is given, the kind "command", the API version 1.0.
[`CadPluginManifest.Parse(json)`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPluginManifest) reads it, and `Refusal()` says
why the presenter will not load it: a kind other than "command", or a newer plugin API than this build's, which is 1.0
(`CadPlugin.ApiVersion`): `The plugin Future needs version 2.0 of the plugin API, and this presenter has 1.0: it needs a
newer presenter.`

A plugin ships as one `.ghplugin` file, a zip of its .dll files and its `plugin.json` at the top. A bare .dll is accepted as
well, with the `plugin.json` beside it where there is one; the plugin's name is then its assembly's. A plugin targets
netstandard2.0 and references `GeometryHelper.Dxf.Viewer` only, without copying it: the presenter gives a plugin its own,
so that the `CadCommandContext` it is handed is of the type it was built against.

## Loading and unloading

[`CadPluginManager`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPluginManager) keeps the plugins of a presenter. Its commands
are the ones AutoCAD users know:

| Command | Alias | Does |
|---|---|---|
| `PLUGINLOAD` | `NETLOAD` | loads a .dll, a .ghplugin or the folder of an installed plugin, for this session |
| `PLUGINUNLOAD` | | unloads a plugin by name: its commands go from every drawing |
| `PLUGINRELOAD` | | loads it again from its file: change the code, build, reload, without restarting |

The window's PLUGINLOAD shows a file dialog; the manager's own, which `Attach(registry)` adds to a command line, asks for a
path on the line. `Install(package)` unpacks a .ghplugin into a folder of the plugin folder named by its id, replacing the
plugin of that name, and loads it; `Uninstall(name)` takes it out; `SetLoadAtStartup(name, true)` puts a plugin on the
startup list; `LoadAtStart()` loads each plugin installed, in the order of their folders' names, then the startup list.

**Trusted folders.** Loading a plugin runs its code with the user's rights, so, as AutoCAD's TRUSTEDPATHS does, a file
outside `TrustedFolders` is loaded only when `ConfirmUntrusted` says yes, and never where it is not set. The plugin folder
is always trusted (`AddTrustedFolder`, `RemoveTrustedFolder`, `IsTrusted`). Without a confirmation, `Load` gives
`...CircleTools.dll is outside the trusted folders, and loading it was not confirmed.`; with one, `Loaded Circle tools 1.0.0:
NUMCIRCLES.` Every load is a line of `Log`, with the full path of the file and whether it was trusted. A yes to a folder outside the trusted ones holds for that folder for the life of the manager, so a reload does not ask again; it is never written to disk, and a no is asked again.

**A plugin loaded twice.** `Load` of a plugin already loaded is refused: `The plugin Circle tools is loaded already, from
...; PLUGINRELOAD loads it again.`

**The shadow copy.** The manager copies a plugin's files to a folder of its own, `ShadowFolder` (under the temporary
folder, one for each process, those of processes that have ended deleted at the next start), and loads the copies, so the
.dll its author builds is never locked: build again, and PLUGINRELOAD picks it up.

**What unloading can do** depends on the runtime, and the loader given to the manager is what differs. The window gives
[`CadPluginLoader`](xref:GeometryHelper.Dxf.Viewer.Plugins.CadPluginLoader)s of its own (`CadPluginAssemblyLoader`):

| Runtime | Loading | `PLUGINUNLOAD` |
|---|---|---|
| .NET 8, the standalone app | each plugin into a collectible load context of its own, so two plugins' dependencies do not clash | frees the code; a new build is taken without a restart |
| .NET Framework, hosted in Tekla | every plugin into the one AppDomain | only switches it off: `Switched Circle tools off: its commands are gone, and its code stays loaded until the presenter restarts.` A reload says the build loaded first runs |

The presenter's own assemblies (`GeometryHelper`, `GeometryHelper.Dxf.Viewer` and the controls) are never loaded from a
plugin's folder, on either runtime. The manager's files are `plugins.json`, the startup list and the trusted folders; the
apps keep them with the plugin folder, GeometryHelper DXF in `%AppData%\GeometryHelper\Dxf` and GeometryHelper Drawing in
`%AppData%\GeometryHelper\Drawing`, and the plugin folder, `Plugins`, is loaded at every start.

A plugin.json with `"kind": "drawing"` holds drawing plugins (classes derived from `DrawingPlugin`, see [Plugin runs](../drawing/update.md#plugin-runs)). The plugin manager loads them once a `DrawingPluginCatalog` is made over it, through the same shadow copy, trusted folders, startup list and log, and lists them under the Drawing chip of the Plugin manager; on .NET 8, unloading frees the plugin's code.

Run plugin on the Drawing tab, or `PLUGIN` on the command line, runs a drawing plugin loaded on the drawing shown: the plugin, the view, the objects (all ticked by default, so that the run takes those an update adds too) and its settings, each checked against its range. The run is kept in the drawing and runs again on every update; Undo takes it back, and a plugin that fails leaves the drawing as it was.

## What the window adds

The window shows a Plugins ribbon tab with a button for each command of each plugin loaded, a Plugin manager (a list with
its load log, the settings of each plugin, load at startup, install and uninstall, trusted folders) and a dialog before a
load outside the trusted folders. Its settings are drawn by the window from the `CadSetting` attributes; the plugin knows
none of it.

A command that works long shows it on the status bar's progress meter through `c.Progress`:
`using (var meter = c.Progress.Start("Checking blocks")) { meter.SetLimit(n); ... meter.MeterProgress(); }`, as
[the app](app.md#the-progress-meter) describes.

## See also

- [Editing as AutoCAD does](editing.md): the command line, `CadCommand` and the undo steps a plugin runs in.
- [Owners](owners.md): why a plugin cannot change what a model drawing owns.
- [Settings, autosave and the window](app.md): the folders the plugins and the settings live in.
