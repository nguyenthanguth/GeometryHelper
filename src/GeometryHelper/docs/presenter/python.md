# Python commands and tools

GeometryHelper CAD runs Python as it runs AutoLISP. A `.py` file loads from a Pythons folder when the app starts, from APPLOAD,
from PYLOAD, from the Python Startup Suite and by dropping it on the window; PYRUN runs a file once, as `(load "x")` loads a
routine. A function marked `@command("C1")` becomes a command of the command line, talks to the user through the `get_*`
prompts and edits the drawing as one undo step. A file adds to the window as a [command plugin](plugins.md) does: ribbon
tabs, panels and buttons, menu items, Properties rows. A program outside the app drives the running app and any drawing in
its list of open drawings with the same names ([Driving the app from another program](remote.md)). The old PYTHON palette is
gone: Python is a way to write commands, not a console.

The Python is yours: the app does not bring one. It needs a CPython 3.11 to 3.15, 64-bit, on the machine, and finds it as
[Finding Python](#finding-python) says. With none the app works as it did, and says what to install where Python is asked for.
This page is about Python inside the app; a script that runs outside it, on the library alone or against a running app, is on
[Python](../python.md) and [Driving the app from another program](remote.md).

```python
from geometryhelper.cad import active_drawing, command, get_distance, get_point

@command("RING", alias="RG", description="Draws a circle of the radius asked")
def ring() -> None:
    centre = get_point("Specify centre")          # picked with the object snaps, or typed
    radius = get_distance("Specify radius", base=centre)
    drawing = active_drawing()
    drawing.add_circle(centre, radius)
    drawing.commit()
```

Units are the drawing's, millimetres in a drawing made in them; nothing is converted. No tolerance is chosen for you: a script
passes a [`Tolerance`](xref:GeometryHelper.Tolerance) where the C# would. The presenter is 2D, so a point asked is a
[`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2). Every public name of `geometryhelper.cad` has a docstring with an
`Example:` block and is typed inline (the package carries `py.typed`, so an editor and pyright read the types from the code).

## Where files load from

| How | What happens |
|---|---|
| The **Pythons** folder, beside `GeometryHelper.Cad.dll` as Lisps is | the `.py` files directly in it load at start, by name ignoring case then ordinal; the folders under it are packages they import and do not run as startup files. A `.py` in Lisps is not loaded, nor a `.lsp` in Pythons |
| The **Python Startup Suite** of Load/Unload Applications (APPLOAD), saved in the settings as `support.pythonStartupSuite` | its files load after the Pythons folder, in the order of the list |
| **APPLOAD**, Load… | a `.py` row of kind Python, with the commands it defined; Reload loads it again, Unload takes its commands out of every drawing |
| **PYLOAD** | the multi-select picker for `.py` files, loading them as APPLOAD does so that they show in its list |
| **Drag and drop** of a `.py` onto the window | loads it as APPLOAD's Load does; passed over while a command or a routine waits, and with no drawing open |
| **PYRUN** | runs a file once, as `python file.py` does; its commands are not kept |

The start loads once per process, at the first drawing opened or made and after that drawing's AutoLISP start, because Python
commands belong to the whole process. The engine starts at the start of the app only where there is a `.py` to load; with none
it starts at the first run, as before, and an app with an empty Pythons folder starts as it always did. A file that fails writes
one line and the next file loads; with no Python found the start writes, once, `Python files were not loaded: <the reason>` and
the app carries on. The Pythons folder lies under the program folder, which [the security gate](#trusted-folders) trusts with all
its subfolders, so its files load with no question; a Startup Suite file elsewhere goes through the gate as PYLOAD does.

One sample ships in the Pythons folder, `GH-DrawingInfo.py`: loading it defines `GHPYINFO` and writes nothing, and `GHPYINFO` writes the
drawing's name and folder, the current layer, the objects of model space counted by type, the number of layers and blocks, and the
extents, and changes nothing. It is a newcomer's first example, 20 lines of code in `@command`, `active_drawing()` and `write`. Because it is
a `.py` in the folder, the engine starts with the app (about 0.45 s), and with no Python found the start writes the one line above;
delete the file to start as an app with an empty folder does.

Load/Unload Applications lists a Python file with a Python chip beside Lisp and Plugins, an amber kind colour, and its commands.
A file that loads says `tools.py loaded: C1, DOT.` on the command line and `tools.py successfully loaded.` in the footer; one
that does not says `tools.py failed to load: <the error>` and defines no command.

## Finding Python

The app looks for Python once, before it starts it, because a start that fails cannot be tried again in the same process. It
looks in this order and takes the first that is set:

1. The Python set in Global settings, Folder preference, **Python** (`GlobalSettings.PythonPath`): a `python.exe`, of a
   virtual environment or of an install, or the folder that holds it. Empty is automatic. A setting that does not work is
   a problem to show, never replaced silently by another Python.
2. `PYTHONNET_PYDLL`, when the environment variable is set: the DLL it names, with the `python.exe` beside it as the program
   where there is one.
3. The Python launcher's registry entries (PEP 514), `HKCU` then `HKLM`, under `Software\Python\PythonCore\<tag>`: the newest
   64-bit 3.11 to 3.15 whose files are there, `HKCU` before `HKLM` at one version.

The registry holds more than what is installed. On the test machine it lists 3.10 with no install path, 3.13 whose
`python.exe` is gone from the disk, and 3.14 (3.14.4, 64-bit). The finder checks the files, so it takes 3.14 and keeps the
others, each with its reason: `3.10: no InstallPath`, `3.13: ...\Python313\python.exe is missing`. The reasons are what
Global settings lists as "Passed over".

A virtual environment is read through its `pyvenv.cfg`, beside or above its `python.exe`: it names the base install that
holds the DLL, and the version. Its packages come with it, so `import numpy` works where the environment has numpy. Whatever
the environment holds, `import geometryhelper` gives the app's own copy of the package, which is first on `sys.path`.

Before Python starts the app checks `python3XY.dll` in the base folder (XY from 11 to 15, never the stable-ABI
`python3.dll`, which does not work) and that the DLL is a 64-bit one. What is wrong is written where Python was asked for,
on the command line, in one of these sentences:

| When | The sentence |
|---|---|
| nothing found automatically | `No Python 3.11 to 3.15 (64-bit) was found. Install one from python.org, or set its python.exe in Global settings > Python.` |
| the setting names nothing | `The Python set in Global settings (<path>) was not found.` |
| `PYTHONNET_PYDLL` names nothing | `The Python set in PYTHONNET_PYDLL (<path>) was not found.` |
| another version | `<path> is Python 3.<n>: GeometryHelper CAD needs 3.11 to 3.15.` |
| a 32-bit Python | `<path> is a 32-bit Python: GeometryHelper CAD needs a 64-bit one.` |
| a DLL of another machine | `<path> is not an x64 Python: GeometryHelper CAD needs a 64-bit x64 one.` |
| no versioned DLL | `<path> has no python3XY.dll in <folder>: GeometryHelper CAD needs a Python installed with its DLL.` |
| `python3.dll` named | `<path> is not a python3XY.dll: GeometryHelper CAD needs the versioned DLL, never python3.dll.` |
| the start failed | `Python could not start: <first line of the error>. Fix Global settings > Python and restart GeometryHelper CAD.` |

`<path>` is the `python.exe` the finder tried, or the DLL where `PYTHONNET_PYDLL` named one with no `python.exe` beside it.
PYRUN and PYLOAD write the sentence and end; every other command works. Once the engine has started, a changed setting takes
effect at the next start of the app, and Global settings says so.

## PYRUN

PYRUN runs a file once. It asks for the file as SCRIPT does: the open dialog, filtered to `*.py`, where the window shows
dialogs, else `Enter Python file name:` on the command line (FILEDIA 0). A name with no extension gets `.py`, and a name that
is not a full path is taken from the folder of the drawing where the drawing has a file.

The file runs as `python file.py` runs it: fresh globals each time with `__name__` equal to `"__main__"` and `__file__` the
full path, `sys.argv` equal to `[path]`, and the file's folder first on `sys.path` for the run only, so a file beside it can
be imported. PYRUN ends the command first, and the script starts when the command line is at the Command prompt again, so
`session().IsRunning` is false in it. The run is one undo step named `PYRUN`.

## `@command`

A file loaded as above is imported as a fresh module named `geometryhelper_user_<stem>`, with its folder on `sys.path` for the
import, and each function decorated with `command` becomes a command of every open drawing and of those opened after:

```python
@command(name, *, alias=None, description=None, uses_selection=False)
```

The command is typed by its name or alias, Enter repeats it, AutoComplete offers it as it offers a plugin's commands, and
the function runs with no arguments as a run of its own whose undo step carries the command's name; what it returns is
ignored. `alias` is taken when no command or alias has it. `uses_selection=True` makes `get_selection()` return the objects
selected before the command started, without asking, as MOVE does, and ask only when none were. `description` is kept for later:
nothing shows it yet. The module-level code of the file runs at the load, as the twin of a plugin's `Initialize`, in a run
named PYLOAD (one undo step if it draws); a function marked `@on_unload` is the twin of `Terminate` and runs when the file is
unloaded, loaded again or the app closes (an error in it is one line and the unload goes on).

Loading a file again replaces its commands, the old ones taken out first. A name that is a command already, and not this file's,
is refused and the rest of the file's commands are kept: `PYLOAD: LINE is a command already; tools.py did not define it.` A file
that fails part way keeps no command of its own.

`command` is the Python twin of the `[CadCommand]` attribute of a [command plugin](plugins.md).

### A command with a selection

`selection_lengths.py` asks `Select objects:` as a command does, adds up the length and the closed area, and writes the total.
On a line from 0,0 to 300,400, a circle of radius 120, a closed polyline rectangle 100 by 50 and a text, all selected, it writes
`4 objects: length 1553.982, closed area 50238.934, 1 skipped` (the test `TheFourObjectsOfDrawingH_GiveALengthAnAreaAndOneSkipped`
of `ExampleTests` runs this file).

[!code-python[](../../../python/examples/in_app/selection_lengths.py)]

### A command that draws

`drawing_tools.py` defines RECTC (alias RC), a rectangle about a centre, and LAYERCOUNT. RECTC with the centre 500,300, a width
of 200 and a height of 100 draws one closed polyline of 20 000 square units, one undo step named RECTC; the width and the height
default to 1 000 and 500 on Enter.

[!code-python[](../../../python/examples/in_app/drawing_tools.py)]

### An edit that numbers

`number_points.py` is run with PYRUN: it asks for the first number and then for points until Enter, and adds a text of height
2.5 with the next number at each point. With a first number of 5 and the points 100,200, 300,200 and 500,200 it adds the texts
5, 6 and 7 and writes `3 points numbered, 5 to 7.`; one U takes all three away.

[!code-python[](../../../python/examples/in_app/number_points.py)]

## The prompts

| Function | What it does |
|---|---|
| `get_point(message, base, keywords, default)` | a [`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2), picked with the object snaps or typed |
| `get_distance(message, base, keywords, default)` | a distance, typed or as two points |
| `get_angle(message, base, keywords, default)` | an angle in radians, typed in degrees or as a direction |
| `get_integer(message, keywords, default)` | a whole number |
| `get_real(message, keywords, default)` | a number |
| `get_string(message, default, spaces)` | a text; with `spaces` false it ends at the first space |
| `get_keyword(message, keywords, default)` | one of the keywords |
| `get_entity(message, keywords)` | one object, as an `Entity` of the run's drawing, or `None` on Enter |
| `get_selection()` | the objects selected at "Select objects:", until Enter, as a list of `Entity` |
| `write(text)` | writes one line or more on the command line |
| `session()` | the raw .NET [`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) of the run's drawing |
| `Cancelled` | the exception of Esc at a prompt |

A prompt behaves as a command's own prompt does: the object snaps, typed coordinates, the rubber band from `base`, and Esc.
The message is written with a colon after it, `Specify centre:`, and a default is shown in angle brackets. The answer is a
point, a number or an `Entity` as it was given, or a keyword as the prompt writes it (a `str`). Enter with no default gives
`None`, as `getpoint` of AutoLISP gives `nil`, and Enter with a default gives the default. The default of an angle is in
degrees, as the prompt shows it, while the answer is in radians. `get_integer` and `get_real` read text in the invariant
culture, so `2.5` is 2.5 whatever the Windows settings are, and ask again after text that is none, writing `Requires an
integer value.` or `Requires numeric value.`, the words of AutoLISP's `getint` and `getreal`.

## `cmd` and `PAUSE`

`cmd(*answers)` runs a presenter command to its end, inside the run's undo step, at the Command prompt. The first answer is the
name of the command (`"CIRCLE"`, `"_LINE"`, an alias), and each answer after it is fed in order:

| Answer | Fed as |
|---|---|
| `""` | Enter |
| a `str` | typed |
| an `int` or a `float` | typed, in the invariant culture |
| a `GeoPoint2` or `(x, y)` | a point picked |
| a `GeoPoint3` or `(x, y, z)` | typed as `x,y` |
| a `list` of handles or of entities | the answer to "Select objects:", then Enter |
| `PAUSE` | hands that one prompt to the user (the string `"\"`, as AutoLISP's `pause`); Esc there raises `Cancelled` |

`cmd("_LINE", (0, 100), (100, 100), "")` draws a line of 100 and ends; `cmd("CIRCLE", PAUSE, 50)` lets the user pick the centre
and gives the radius 50. After the last answer the command must have ended; one that still asks is cancelled and raises
`RuntimeError`: `CIRCLE still asked 'Specify radius of circle or [Diameter]:' after the last answer.` A name that is not a
command, an answer the prompt refuses and an answer given after the command ended raise `ValueError`, the command cancelled
first. The command's prompts and answers are written on the command line as if typed. A command that opens a window of its own
shows it, modal, as typed by hand; use the `-` form of such a command (`-LAYER`) in a script.

## The drawing

`active_drawing()` is the drawing the run started in (not the one shown later) and gives a `Drawing`; `documents()` lists the
drawings of the window in its order, `open_drawing(path)` opens one as OPEN does and `new_drawing()` makes one. The same
`Drawing` works on a running app from outside (`connect()`) and on a file with no app (`open_file`, `new_file`), so a script
written for one runs on the others with only that line changed ([Python](../python.md#the-drawing-api)).

A drawing is read as copies and changed by queueing (on a drawing made by `new_file()` the commit gives `['101', '102']`):

```python
drawing = active_drawing()
drawing.layers.add("Steel", color=1)
drawing.add_line((0, 0), (100, 0), layer="Steel")
drawing.add_circle((50, 50), 25)
handles = drawing.commit()          # the handles of the circle and the line, in the order added
```

| Member | What it does |
|---|---|
| `name`, `path`, `is_active`, `is_modified` | what the tab shows; `path` is `None` for a drawing never saved |
| `entities(type, layer, layout)`, `entity(handle)`, `selection()`, `select(items)` | read objects as `Entity` copies; `type` is the DXF name, `"LINE"`, ignoring case |
| `add(dxf_entity, layout)` | queue a copy of any `GeometryHelper.Dxf` entity |
| `add_line`, `add_circle`, `add_arc`, `add_polyline`, `add_text`, `add_mtext` | queue the common objects; angles are **radians**; `**props` takes `layer`, `color`, `linetype` |
| `modify(entity)`, `erase(entity_or_handle)` | queue a change made to `entity.net`, or an erase |
| `commit()` | apply everything queued as one undo step; the handles of the objects added |
| `layers`, `current_layer`, `text_styles`, `dimension_styles`, `blocks`, `layouts`, `current_layout` | the tables; styles and blocks are read-only copies |
| `pick_point`, `pick_object`, `pick_objects` | ask the user (the `get_*` prompts in the app; the Remote picks from outside; `RuntimeError` on a file) |
| `save()`, `save_as(path, version)`, `close(discard)` | as from outside; a modified drawing is refused by `close` unless `discard` is true |
| `net` | the `CadDocument` (the `CadRemoteDocument` outside the app) |

A point is a `GeoPoint2`, a `GeoPoint3` or a tuple of two or three numbers. An `Entity` has `handle`, `dxf_type` and a `net`, a
**copy** of the `DxfEntity` that is never the live object, so nothing bypasses undo; `layer`, `color` (256 ByLayer, 0 ByBlock)
and `linetype` are set on the `Entity` and committed with the drawing; after changing `net`, call `drawing.modify(entity)`. A
`Layer` has `name` (read only) and settable `color`, `linetype`, `description`, `is_on`, `is_frozen`, `is_locked` and
`is_plottable`. A dimension added or modified from Python is drawn by the app, so its picture holds the lines, arrows and words
of its measurement ([`CadDimensionPicture.Redraw`](xref:GeometryHelper.Cad.CadDimensionPicture)): a rotated dimension from
0,0 to 100,0 comes with the words `100`, and one moved to 150 with `150`.

An object added from the app has the handle `~1`, `~2`... until `commit()` returns the real ones. In the app a command or run
commits what is still queued when it ends, inside its own step; `save()` commits first in every backend; anything left queued
when a script ends outside the app is lost, without a warning. A commit to a drawing other than the run's makes one step in
that drawing, named after the run.

### A drawing made and saved in code

`make_drawing.py OUT` makes a blank drawing with `new_file()`, adds a layer `Outline`, makes it current, adds a rectangle 4 000
by 3 000 and a circle of radius 500 on it, and saves the drawing as `OUT`, DXF or DWG by its extension. It prints `objects 2`,
`layers 0, Outline` and `saved plan.dwg`, and the file holds a polyline of area 12 000 000 and the circle on `Outline`.

[!code-python[](../../../python/examples/library/make_drawing.py)]

## One undo step

A run is one undo step, whatever it draws, through the Drawing API, through `cmd`, through the object model or all three. The
step is named `PYRUN` for a file, the command's name for a command, and `PYLOAD` for the module-level code of a load. A run
that ends well ends every group it left open. A run that fails or is cancelled takes every open group back, and drops what is
still queued, so the drawing is as it was before the run and nothing is left open that would make the next `SaveAs` throw.

A file that draws a circle with `cmd("CIRCLE", (0, 0), 25)` and then divides by zero fails the run: the drawing has 0 objects,
not 1, and no undo step. A run that only asks and writes makes no step and does not mark the drawing modified. An
`undo_group` of the script, from `geometryhelper`, nests in the run's step. Autosave skips a drawing while a run waits at a
prompt with its step open.

## Adding to the window

A loaded file adds to the window as a plugin's `Initialize` does, through functions that take the same objects:

```python
from geometryhelper.cad import add_ribbon_tab, ribbon_button, ribbon_panel, ribbon_tab

circle = ribbon_button("Circle", command="CIRCLE")
hello = ribbon_button("Hello", run=lambda context: context.write("Hello"))
add_ribbon_tab(ribbon_tab("tools", "Tools", [ribbon_panel("Draw", [circle, hello])]))
```

This adds a tab `Tools` with one panel `Draw` holding the buttons Circle, which starts CIRCLE, and Hello, which writes a line
(the test `AfterTheLoad_TheFilesHostHoldsItsTab_ItsPanel_AndItsTwoButtons` of `PluginParityTests` reads these from the host the file filled). A
callback is given a `Context` with `drawing` (a `Drawing`, or `None`), `selected` (a list of `Entity`), `is_command_running`,
`write(text)` and `run_command(text)`.

Every public member of `GeometryHelper.Cad.Plugins` has a Python name, or is left out with a reason:

| C# | Python |
|---|---|
| `[CadCommand(Name, Alias, Description, UsesSelection)]` | `@command(name, alias=, description=, uses_selection=)` |
| `ICadPluginApplication.Initialize(host)` | the file's module-level code at load |
| `ICadPluginApplication.Terminate()` | `@on_unload` |
| `AddRibbonTab`, `CadRibbonTab` | `add_ribbon_tab(ribbon_tab(id, title, panels, show_when=None))` |
| `AddRibbonPanel`, `CadRibbonPanel` | `add_ribbon_panel(ribbon_panel(title, items))`, on the Plugins tab |
| `CadRibbonButton`, `CadRibbonItem` | `ribbon_button(text, command=None, run=None, can_run=None, icon=None, size="standard", show_text=True, tooltip=None, description=None)` |
| `CadRibbonSplitButton` | `split_button(text, buttons, remembers_last=True)` |
| `CadPluginIcon.FromPathData / FromImage / FromFile` | `icon_from_path_data(data)`, `icon_from_image(data)`, `icon_from_file(path)` (`FromResource` is left out: a `.py` has no assembly) |
| `AddDrawingsButton`, `CadPluginButton` | `add_drawings_button(icon, tooltip, run, can_run=None)` |
| `AddDrawingsMenu`, `CadDrawingsRequest` | `add_drawings_menu(provider)`; the request has `path`, `drawing`, `selected_paths` |
| `AddCanvasMenu`, `CadPluginMenuItem`, `Separator()` | `add_canvas_menu(provider)`, `menu_item(...)`, `separator()` |
| `AddProperties`, `CadPropertyProvider` | `add_properties(provider)`: the .NET `CadPropertyRequest` in, the .NET `CadSelectionInfo` out |
| `ShowsCommandButtons` | `show_command_buttons(False)` |
| `CadPluginContext` | the `Context` above |
| `AddObjectSnap`, `CadCustomSnap` | **left out**: the finder runs at every cursor move for every object in the aperture, and a Python call there (GIL, run guard) would lag the cursor |
| `[CadSetting]`, `CadPluginSetting` | **left out**: no window shows plugin settings today; a module variable does the same |
| `CadPluginHost` read-backs (`RibbonTabs`, `DrawingsMenus`...) | **left out**: they are for the window, and Python has no use for them |
| `CadPlugin`, `CadPluginManager`, `CadPluginLoader`, `CadPluginManifest` and the like | **left out**: management of `.dll` plugins; Python files are managed by APPLOAD |
| the side panel (`CadWorkspaceExtension.SidePanelContent`) | **left out**: it takes WPF content and Python builds none; a tool that needs its own window is an outside app (`connect()`) |
| document events (`CadDocument.Changed` and the others) | through `drawing.net` with pythonnet's `+=`; no wrapper |
| `CadCommandContext.Remembered`, `Progress`, `ClearsSelection` | **left out**: module variables remember; `session().Progress` is there raw |

Additions are taken only while the file loads: a call later raises `RuntimeError`. Each loaded file has its own plugin host,
named after the file and closed when the load ends; reloading replaces it and unloading drops it, and the window reads these
hosts after the plugins', in load order. Every callback runs on the window thread, under the run guard: an error is one line on
the command line, `Python error: ...` with the file name, never a box or a crash, and a `can_run`, `show_when` or provider that
raises counts as false or as no items.

## Esc

Esc ends a script in either of two places.

At a prompt Esc is the command line's own: the question ends, the prompt function raises `geometryhelper.cad.Cancelled`, and
every prompt the same run asks after it raises at once without asking, so a script that catches the first cannot go on
asking. `Cancelled` is a `KeyboardInterrupt`, so `except Exception` does not swallow it, and Esc at a prompt and Esc during
work end a run alike: the groups are cancelled, and the command line writes `*Cancel*`. A script that catches `BaseException`
or a bare `except` swallows each Esc once.

While Python computes the window takes no clicks and no typing, so the app watches for Esc itself, and the script gets
`KeyboardInterrupt`. On the test machine a loop of Python, `while True: pass`, ended 19 to 20 ms after the stop on .NET 8 and
25 to 28 ms on .NET Framework 4.8; numpy matrix products of 2 500 by 2 500 ended 62 ms after.

It cannot stop what is not running Python. The interrupt lands when the call that holds the thread returns: a script in
`time.sleep(5)` ended 4 725 ms after the stop, when the sleep returned, and one inside a .NET call that blocked for two
seconds ended 1 693 ms after, when the call returned. Keep a blocking call short, or wait in a loop of short sleeps that
Esc can interrupt.

## Errors and refusals

An error ends the run and never closes the app. The command line gets one line,
`Python error: <Type>: <message> (<file name>, line <n>)`, where the file and line are those of the innermost frame in a file
the script runs. A script with `print(ratio(1, 0))` on line 7 and the division on line 4 of `ratio` writes
`Python error: ZeroDivisionError: division by zero (boom.py, line 4)`. A syntax error gives its file, line and caret the same
way. `sys.exit()` and `sys.exit(0)` end the run well; `sys.exit(3)` is a failed run.

These are written, and the run does not start:

| When | The line |
|---|---|
| a run is going on, in any drawing | `A Python script is running in <drawing name>: wait for it or press Esc.` |
| a command runs in that drawing | `A command is running: finish it or press Esc first.` |
| a file outside the trusted folders, refused ([Trusted folders](#trusted-folders)) | `File load canceled: <full path>` |
| PYRUN or PYLOAD of a file that is not there | `Cannot find Python file "<name>".` |
| a file that cannot be read | `<name> could not be read: <the system's reason>` |
| no usable Python | one of the sentences of [Finding Python](#finding-python) |

A fault in the app's own code during a run is not a crash either: it is written as `Python host error: <message>`, logged,
and the run's groups are cancelled.

The ways a script can end badly were each run, and each leaves the app running, no undo group open, the drawing as it was where
the run failed, and the next run working:

| The script | What it leaves |
|---|---|
| `1/0`, `import nonexistent_module`, a file with a syntax error | a failed run and its `Python error:` line, as above |
| `sys.exit(3)`, `sys.exit("bye")` | a failed run: `Python error: SystemExit: 3` (or `bye`) and the file and line |
| `exit()`, `quit()`, `sys.exit(0)` | a run ended well, with no line |
| `raise KeyboardInterrupt` | a cancelled run, `*Cancel*`, as Esc gives |
| a function that calls itself for ever | a failed run, `RecursionError: maximum recursion depth exceeded` |
| an error in a command, or in the file PYLOAD loads | a failed run; the command runs again next time |
| a .NET exception from the object model (`ApplyEdit(None, None, None)`) | a failed run and one line naming the .NET type and message, `Python error: System.ArgumentNullException: Value cannot be null. ...`, the .NET stack left out |
| `sys.stdout = None`, or `sys.stdout` left on a file it closed or kept open | that run alone: the app puts its own `sys.stdout`, `sys.stderr` and `sys.stdin` back before every run, so the next run writes to the command line again |
| a handler attached to a .NET event (`CadDocument.Changed`) that raises when the event fires after the run ended | the error goes to the code that fired the event, as any handler's would; where nothing there catches it and it reaches the window, the command line of the drawing shown gets `Python error: <Type>: <message>`, the log a Warn, and no box comes up ([the crash log](app.md#crashes-and-the-crash-log)) |

Two ends are beyond the app. `os._exit()` ends the process at once, drawings and all, as it ends any Python. And a recursion
that passes through .NET on every level, a Python function called back by a .NET delegate that calls it again, uses the
window thread's stack faster than Python counts its own frames: 200 and 600 levels ran, but past about 650 levels the
stack overflows and .NET ends the process with nothing written, below Python's recursion limit of 1 000. A script that
recurses through callbacks keeps well under that or lowers the limit with `sys.setrecursionlimit`.

## Trusted folders

PYRUN, PYLOAD, APPLOAD and the Startup Suite go through the gate AutoLISP's `load` goes through, with the same trusted folders
and the same answers: SECURELOAD, TRUSTEDPATHS and the files answered Always Load, set with SETVAR or on
[the Security page](app.md#the-security-page) of Global settings, as
[Trusted folders and SECURELOAD](lisp.md#trusted-folders-and-secureload) describes. At SECURELOAD 1, the app's default, a
`.py` outside the trusted folders brings AutoCAD's warning, **Security - Unsigned Executable File**: Load Once runs it and
does not ask about that file again until the app restarts, Always Load also keeps the answer for that file and its
content, and Do Not Load writes `File load canceled: <full path>` on the command line and ends the run as `Refused`. At
SECURELOAD 2 such a file is refused with no question. The folder of the app, with the Pythons folder in it, is always trusted.

The gate is in front of the files the app is asked to run. The modules a script imports are Python's own business, found on
`sys.path`, and are not checked. A script outside the app, on the `geometryhelper` package alone, meets no gate: a session made
by the library starts at SECURELOAD 0.

## `print`, `input` and the threads

What a script prints goes to the command line in whole lines: Python's `print` of two lines makes 11 separate writes to
`sys.stdout`, so the app gathers them. A partial line is written before every prompt, so that `input("Name? ")` reads well,
and at the end of the run. `input(prompt)` asks the text at the command line, and Esc raises `Cancelled`. Nothing is read from
a console.

A script runs on the thread that owns the drawings, as a command's body does, so the object model is used with nothing
marshalled between threads: a .NET call from Python took 3.1 to 3.5 microseconds on .NET 8 and 2.1 to 2.2 on .NET Framework 4.8
(100 000 calls, 309 to 353 ms and 207 to 218 ms). The price is that the window does not repaint while a script computes,
as AutoCAD's does not during a loop of AutoLISP; what the script writes appears at the next prompt or at its end. A script
may start threads of its own, but one that leaves a thread running at exit makes the app skip Python's shutdown, which would
wait for it for ever, and log it. A script that runs inside the app cannot open a
[`CadApplication`](xref:GeometryHelper.Cad.Remote.CadApplication) to that same app: the app answers it on the thread the
script holds, so it would wait for itself. Such a connection is refused before anything is sent, with a
[`CadRemoteException`](xref:GeometryHelper.Cad.Remote.CadRemoteException): `A script inside GeometryHelper CAD cannot drive
the same app through CadApplication: use geometryhelper.cad, which reaches the drawing directly.`

## What is not there yet

- `save()` inside a run makes two undo steps: the run's group ends at the save and opens again, so what was done before it is
  one step and what follows another.
- `Entity.dxf_type` is read from the text of the object (`str(net)`): a public DXF name on `DxfEntity` would touch the core
  library.
- Custom object snaps and plugin settings (the two **left out** rows of the table above).
- A script editor, completion (Ctrl+Space) and a debugger; a console for typing Python an entry at a time, which the PYTHON
  palette was and no longer is.
- A Python of the app's own: the app does not bundle one.
- The window repainting while a script computes.
- Systems other than Windows: the app is Windows only.

## See also

- [Python](../python.md): the library and the presenter from outside the app, the stubs, numpy in blocks and undo groups.
- [Python examples](../python-examples.md): three scripts for the app, with what each prints, and ten more for outside it.
- [AutoLISP](lisp.md): the other language of the command line; its prompts and Esc as Python does them.
- [Command plugins](plugins.md): a command written in a .NET assembly.
- [Driving the app from another program](remote.md): the same `Drawing` from outside the app.
- [Settings, autosave and the window](app.md#python-in-the-window): the Python setting.
