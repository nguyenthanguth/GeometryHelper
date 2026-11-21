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
using GeometryHelper.Cad;
using GeometryHelper.Cad.Plugins;

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

The method is an iterator, like the presenter's own commands: it yields each [`CadPrompt`](xref:GeometryHelper.Cad.CadPrompt)
and reads the answer from `c.Input` when it is resumed, so it runs inside the command line with no thread of its own. That
is the one difference from AutoCAD's `[CommandMethod]`, which blocks on its prompts: a method that returns `void` and blocks
is refused, because it would have to run on a thread while the window waits and then reach the canvas from that thread,
which WPF does not allow.

| The attribute | Says |
|---|---|
| [`[CadCommand("NAME")]`](xref:GeometryHelper.Cad.Plugins.CadCommandAttribute) | the method is a command; `Alias`, `Description`, and `UsesSelection` for a command that takes the objects selected before it, as MOVE does |
| [`[CadSetting("Label")]`](xref:GeometryHelper.Cad.Plugins.CadSettingAttribute) | a public property is a setting the presenter shows; `Description`, `Minimum`, `Maximum` |
| [`[assembly: CadPluginApi("1.0")]`](xref:GeometryHelper.Cad.Plugins.CadPluginApiAttribute) | the least version of the plugin API the assembly needs; none is taken as 1.0 |

A command method may be static, or an instance method of a public class that is not generic and has a public constructor
that takes nothing, of which the plugin makes one when it is loaded. A setting is a property with a public get and set of
type `double`, `int`, `bool`, `string` or an enumeration, which the presenter draws a box for itself, so a plugin never
touches the window. A method or a class that cannot be one is left out, and each is a line in [`CadPlugin`](xref:GeometryHelper.Cad.Plugins.CadPlugin)`.Problems`.

What the plugin can use is the public API of [`CadCommandContext`](xref:GeometryHelper.Cad.CadCommandContext) and
[`CadDocument`](xref:GeometryHelper.Cad.CadDocument): `SelectObjects()`, `Selected`, `Input`, `Write`, `Apply`,
`OpenUndoStep()`, `Remembered`, the document's entities, layers and selection, the `CadPrompt.For...` methods for a point, a
distance, an angle, an object, a keyword, a string or an integer, and the [`CadLayerEditor`](xref:GeometryHelper.Cad.CadLayerEditor), [`CadGrips`](xref:GeometryHelper.Cad.CadGrips) and [`CadPropertyEditor`](xref:GeometryHelper.Cad.CadPropertyEditor)
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
`FromAssemblies` makes the plugin from assemblies already loaded, and a [`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) runs its commands with no window.

Every run is one undo step whatever it edits through the document. A run that throws has every change of it taken back, the
error is written on the command line with the plugin's name and logged as a Warn, and the document is as it was.
Entities that have an [owner](owners.md) are refused by the owner's rule, not by the plugin: the gate is the document's.

A setting is read from and written to the plugin's property through [`CadPluginSetting`](xref:GeometryHelper.Cad.Plugins.CadPluginSetting)`.Value`, and a value out of its range
is refused, the plugin keeping the one it had: setting the text height to 0.05 throws `The setting Text height lies from 0.1
to 1000.`

## The manifest and the package

A plugin says what it is in a `plugin.json`, read without loading any code:

```json
{ "id": "circle-tools", "name": "Circle tools", "version": "1.0.0", "author": "Nguyen Thang",
  "kind": "command", "minimumApiVersion": "1.0", "commands": [ "NUMCIRCLES" ], "icon": "icon.png" }
```

Only the name is needed: the id is the name where none is given, the kind "command", the API version 1.0.
[`CadPluginManifest.Parse(json)`](xref:GeometryHelper.Cad.Plugins.CadPluginManifest) reads it, and `Refusal()` says
why the presenter will not load it: a kind other than "command", or a newer plugin API than this build's, which is 1.1
(`CadPlugin.ApiVersion`): `The plugin Future needs version 2.0 of the plugin API, and this presenter has 1.1: it needs a
newer presenter.` A plugin that adds to the window, as [below](#what-a-plugin-adds-to-the-window), states 1.1.

A plugin ships as one `.ghplugin` file, a zip of its .dll files and its `plugin.json` at the top. A bare .dll is accepted as
well, with the `plugin.json` beside it where there is one; the plugin's name is then its assembly's. A plugin targets
netstandard2.0 and references `GeometryHelper.Cad` only, without copying it: the presenter gives a plugin its own,
so that the `CadCommandContext` it is handed is of the type it was built against.

## Loading and unloading

[`CadPluginManager`](xref:GeometryHelper.Cad.Plugins.CadPluginManager) keeps the plugins of a presenter. Its commands
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
[`CadPluginLoader`](xref:GeometryHelper.Cad.Plugins.CadPluginLoader)s of its own (`CadPluginAssemblyLoader`):

| Runtime | Loading | `PLUGINUNLOAD` |
|---|---|---|
| .NET 8, the standalone app | each plugin into a collectible load context of its own, so two plugins' dependencies do not clash | frees the code; a new build is taken without a restart |
| .NET Framework, hosted in Tekla | every plugin into the one AppDomain | only switches it off: `Switched Circle tools off: its commands are gone, and its code stays loaded until the presenter restarts.` A reload says the build loaded first runs |

The presenter's own assemblies (`GeometryHelper`, `GeometryHelper.Cad` and the controls) are never loaded from a
plugin's folder, on either runtime. The manager's files are `plugins.json`, the startup list and the trusted folders; the
apps keep them with the plugin folder, GeometryHelper CAD in `%AppData%\GeometryHelper\Dxf`, and the plugin folder, `Plugins`, is loaded at every start.

A plugin.json with `"kind": "drawing"` holds drawing plugins, which this presenter does not run: a library that makes model drawings gives the plugin manager a `DrawingPluginFinder`, and the manager then loads such a plugin through the same shadow copy, trusted folders, startup list and log, and lists it under the Drawing chip of the Plugin manager. Without a finder the presenter refuses the plugin with the manager's reason.

## What the window adds

The window shows a Plugins ribbon tab with a button for each command of each plugin loaded, a Plugin manager (a list with
its load log, the settings of each plugin, load at startup, install and uninstall, trusted folders) and a dialog before a
load outside the trusted folders. Its settings are drawn by the window from the `CadSetting` attributes; the plugin knows
none of it.

A command that works long shows it on the status bar's progress meter through `c.Progress`:
`using (var meter = c.Progress.Start("Checking blocks")) { meter.SetLimit(n); ... meter.MeterProgress(); }`, as
[the app](app.md#the-progress-meter) describes.

## What a plugin adds to the window

A plugin may add to the window what AutoCAD's plugins add to theirs: ribbon tabs and panels, rows of PROPERTIES, buttons
and items of the DRAWINGS list, items of the canvas's right-click menu, and object snaps of its own. It describes them
with the types of `GeometryHelper.Cad.Plugins`, none of them WPF's, so the same .dll runs on .NET 8 and .NET Framework;
the window draws them in its own look and takes them away when the plugin is unloaded.

A public class that implements [`ICadPluginApplication`](xref:GeometryHelper.Cad.Plugins.ICadPluginApplication), as
AutoCAD's `IExtensionApplication`, is made once when the plugin is loaded and given a
[`CadPluginHost`](xref:GeometryHelper.Cad.Plugins.CadPluginHost) in `Initialize`; `Terminate` is called when the plugin
is unloaded or reloaded, and when the manager is disposed. What `Initialize` adds is all the plugin adds: an `Add...`
call after it has returned throws. A class that also holds commands uses the same object for both, and a plugin with no
command but such a class is loaded. The sample below, cut short from the test plugin `GeometryHelper.Cad.SampleUiPlugin`,
adds a tab, a group of Properties, a DRAWINGS button and a snap:

```csharp
[assembly: CadPluginApi("1.1")]

public sealed class CircleUi : ICadPluginApplication
{
    public void Initialize(CadPluginHost host)
    {
        var number = new CadRibbonPanel("Number");
        number.Items.Add(new CadRibbonButton("Number circles", "NUMCIRCLES2")
        {
            Size = CadRibbonItemSize.Large,
            Icon = CadPluginIcon.FromPathData("M4 6h16M4 12h16M4 18h10"),
            Description = "Writes a number at the centre of each circle selected.",
        });
        number.Items.Add(new CadRibbonButton("Count", c => c.Write(Count(c.Document) + " circles."))
        {
            CanRun = c => c.Document != null,
        });
        var tab = new CadRibbonTab("Circles", "Circles");
        tab.Panels.Add(number);
        host.AddRibbonTab(tab);

        host.AddProperties(Describe);
        host.AddDrawingsButton(new CadPluginButton(CadPluginIcon.FromPathData("M4 12H20M12 4V20"), "Count the circles", Count));

        var top = new CadCustomSnap("Circle top", "CTOP", r =>
            r.Entity is DxfCircle circle
                ? new[] { new GeoPoint2(circle.Center.X, circle.Center.Y + circle.Radius) }
                : null);
        top.EntityTypes.Add(typeof(DxfCircle));
        host.AddObjectSnap(top);
    }

    public void Terminate() { }
}
```

An `Initialize` that throws leaves the plugin's commands loaded and everything it added out, with a line in
`CadPlugin.Problems` and a Warn in the log.

**Ribbon.** [`CadRibbonTab`](xref:GeometryHelper.Cad.Plugins.CadRibbonTab)s come after the Plugins tab, in the order the
plugins were loaded; `AddRibbonPanel` puts a panel on the Plugins tab instead, between "Command plugins" and "Manage", and
`ShowsCommandButtons = false` keeps the plugin's commands off "Command plugins". A panel's items are buttons and split
buttons. A `Large` item has its icon in a slot of 32 pixels over its text, alone in its column; `Standard` items have a
slot of 16 beside their text, or the icon alone where `ShowText` is false, and stand three to a column, as AutoCAD's row
panels do. An icon is a path on a square of 24 drawn 2 wide like the presenter's own (`FromPathData`), or a PNG, BMP,
JPEG, GIF or ICO from bytes, an embedded resource or a file by its full path (`FromImage`, `FromResource`, `FromFile`,
`Path.Combine(host.Folder, "icons", "count.png")`): its bytes are copied when it is made, so no file stays locked. An icon
the window cannot draw shows the plugin icon. A button runs a command, started as the presenter's ribbon buttons start
theirs, or a callback given a [`CadPluginContext`](xref:GeometryHelper.Cad.Plugins.CadPluginContext) (the document, the
handles selected, `Write`, `RunCommand`); `CanRun` greys it, asked as the selection, the drawing and the command change.
A callback that throws writes its message on the command line after the plugin's name. A split button shows and runs the
one used last, as AutoCAD's Circle button does. A tab with `ShowWhen` is contextual: shown in green after the presenter's
contextual tabs while it says true, as "Circle" while only circles are selected, and taken up when it shows.

**Properties.** A [`CadPropertyProvider`](xref:GeometryHelper.Cad.Plugins.CadPropertyProvider) is asked, after the
extensions, with the document, the handles shown and what Properties shows, and returns what it shows, usually the same
groups and one of its own. Its rows are the extension's: a setter returns null where it made the change and the words of
the refusal otherwise. `CadPropertyRow` makes rows of a kind: `Number`, `Length` and `Angle` (written and read in the
drawing's units), `Point` (three rows, "Center X", "Center Y" and "Center Z"), `Color`, `Layer`, `Linetype` and
`Lineweight`, the last four drop-downs of the window's own lists. Text that is not one is refused in the presenter's words
before the setter is called: "Diameter is a number." A setter edits the drawing through `CadDocument.ApplyEdit`, one undo
step a change.

**DRAWINGS and the canvas.** `AddDrawingsButton` puts an icon button first in the header of the list;
`AddDrawingsMenu` adds items after a separator at the end of a file row's right-click menu, given the row's path, its
document where it is open and the rows selected; `AddCanvasMenu` adds items to the canvas's menu for the objects
selected, after the extensions' and before the presenter's own. [`CadPluginMenuItem`](xref:GeometryHelper.Cad.Plugins.CadPluginMenuItem)
makes an item, a grey or ticked one, a submenu and a separator.

**Object snaps.** A [`CadCustomSnap`](xref:GeometryHelper.Cad.Plugins.CadCustomSnap), as AutoCAD's
`CustomObjectSnapMode`, has a name, a keyword, the entity types it applies to (all where none is given), a glyph drawn
as a snap marker (a square with a dot where none is given) and a finder that gives its points for an entity under the
aperture. It is listed in the Object Snap tab of Drafting Settings under "Plugin snaps", in the OSNAP menu and in the
Shift+right-click menu, and is on while OSNAP is, as the built-in snaps are; the choice is kept in the settings by its
keyword (`GlobalSettings.PluginSnaps`), and a snap never chosen starts as its `IsOnByDefault`, off unless set. Its keyword
typed at a point prompt, `CTOP` or `_ctop`, takes it for the next point alone: "Specify next point: _ctop of". A keyword is
2 to 16 letters and digits, and one that is or starts with one of AutoCAD's snap keywords (END, MID, CEN and the rest), or
is FROM, TT, M2P or MTP, is refused. A point of a plugin's snap competes with the built-in snaps by distance, the built-in
one taken at a tie: "Circle top" is the circle's 90 degree quadrant, so it shows with Quadrant off, or typed. A finder that
throws switches its snap off with one line, `The snap CTOP of Circle tools failed and is off: ...`, until it is chosen on
again. A plugin's snap is not tried through a viewport of a layout, inside a block, or for a point typed after it.

Two plugins that add a tab of the same id, or snaps of the same keyword, keep the first loaded; the second's is left out
with a line in its `Problems`. After PLUGINUNLOAD the window shows Home where the plugin's tab was shown, and its panels,
rows, buttons, items and snaps are gone; on .NET 8 the plugin's code is freed with them.

## See also

- [Editing as AutoCAD does](editing.md): the command line, `CadCommand` and the undo steps a plugin runs in.
- [Owners](owners.md): why a plugin cannot change what a model drawing owns.
- [Settings, autosave and the window](app.md): the folders the plugins and the settings live in.
