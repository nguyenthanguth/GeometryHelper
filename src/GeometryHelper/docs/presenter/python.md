# Python in the app

GeometryHelper CAD runs Python. A PYTHON palette takes a line of Python at a time, as `python` does at its prompt, PYRUN
runs a `.py` file, and PYLOAD loads a file whose decorated functions become commands of the command line. A script works on
the drawing it was started in through the .NET object model, the same one the C# pages describe, and through a small module,
`geometryhelper.cad`, that gives it the command line, the prompts a command has and one undo step for everything it does.

The Python is yours: the app does not bring one. It needs a CPython 3.11 to 3.15, 64-bit, on the machine, and finds it as
[the next section](#finding-python) says. With none the app works as it did, and says where Python is asked for what to
install. This page is about Python inside the app; a script that runs outside it, on the library alone or against a running
app, is on [Python](../python.md) and [Driving the app from another program](remote.md).

```python
from geometryhelper.cad import command, get_point

centre = get_point("Specify centre")      # picked with the object snaps, or typed
command("CIRCLE", centre, 25)             # as if CIRCLE, the centre and 25 were typed
```

Units are the drawing's, millimetres in a drawing made in them; the module converts nothing. No tolerance is chosen for you:
a script passes a [`Tolerance`](xref:GeometryHelper.Tolerance) where the C# would. The presenter is 2D, so a point asked is a
[`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2).

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
on the command line and in the palette, in one of these sentences:

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

`<path>` is the `python.exe` the finder tried, or the DLL where `PYTHONNET_PYDLL` named one with no `python.exe` beside it. PYRUN and PYLOAD write the
sentence and end; the palette shows it in its banner with a button to Global settings; every other command works.

Python starts at the first run or palette entry of the session, not when the app starts: through a virtual environment's
`python.exe` the engine took 80 ms to start (a probe, warm). Once it has started, a changed setting takes effect at the next
start of the app, and Global settings says so. The engine is started once per process, and a failed start is why the last
sentence says to restart.

## The PYTHON palette

PYTHON shows the palette, docked under the canvas as wide as the canvas, and PYTHONCLOSE or the × in its header hides it.
It has, from the top:

- a header with the name of the drawing the console belongs to;
- a toolbar: Run file (PYRUN), Load (PYLOAD), Stop, then Clear and Reset console;
- the transcript, in a monospace font and three colours: what you typed after `>>>` or `...`, what the scripts printed, and
  errors with their tracebacks, with the last 5 000 lines kept. A fourth, muted colour is the app's own lines: the start of a
  run that is not an entry (`PYRUN circles.py`), the result of PYLOAD, and refusals;
- the input box, where Enter runs a complete entry, Shift+Enter adds a line, and Up and Down walk the entries typed, 200
  kept for the session;
- a status line, `Python 3.14.4 (venv314)` and a dot, green when Python is ready, amber while a script runs or waits at a
  prompt, red with `No Python found` when none was found, or `Python is not available` when the one found cannot be used.

The input is disabled while a run goes on, with the reason in it, and so are Run file, Load and Reset console. A script that
asks a question asks it on the command line, so there is one place to answer; Stop, or Esc, ends the question.

Each drawing has a console of its own: its own transcript and its own names, made at its first entry, with nothing imported.
The palette follows the drawing shown, so switching drawings shows that drawing's transcript and names. Reset console drops
the names of the drawing shown and writes `Console reset.` in the app's muted colour.

```text
>>> x = 2 + 3
>>> x
5
>>> def area(r):
...     return 3.14159 * r * r
...
>>> area(10)
314.159
```

An entry that is not yet complete, such as `def area(r):`, keeps its text and opens a `...` line, as `python -i` does; a
syntax error is written and the entry is dropped; the value of an expression is written with `repr`. An entry that draws is
one undo step named `Python` ([below](#one-undo-step)). An entry typed while a command runs in that drawing is refused:
`A command is running: finish it or press Esc first.` One typed while a script runs is refused with the first sentence of
[Errors and refusals](#errors-and-refusals).

With no usable Python the palette shows a banner at the top of the transcript with the sentence above and a button that opens
Global settings at Python, and says that everything else works as before. Clear still works.

## PYRUN

PYRUN runs a file once. It asks for the file as SCRIPT does: the open dialog, filtered to `*.py`, where the window shows
dialogs, else `Enter Python file name:` on the command line (FILEDIA 0). A name with no extension gets `.py`, and a name that
is not a full path is taken from the folder of the drawing where the drawing has a file.

The file runs as `python file.py` runs it: fresh globals each time with `__name__` equal to `"__main__"` and `__file__` the
full path, `sys.argv` equal to `[path]`, and the file's folder first on `sys.path` for the run only, so a file beside it can
be imported. PYRUN ends the command first, and the script starts when the command line is at the Command prompt again, so
`session().IsRunning` is false in it.

## PYLOAD and `@cad_command`

PYLOAD loads a file that defines commands. The file is imported as a fresh module named `geometryhelper_user_<stem>`, with
its folder on `sys.path` for the import, and each function decorated with `cad_command` becomes a command:

```python
from geometryhelper.cad import cad_command, command, get_distance, get_point, write

@cad_command("HELLO", alias="HI")
def hello():
    write("Hello from Python")

@cad_command("DOT")
def dot():
    centre = get_point("Specify centre")
    radius = get_distance("Specify radius", base=centre)
    if centre is not None and radius is not None:
        command("CIRCLE", centre, radius)
```

PYLOAD of this file, `tools.py`, writes `tools.py loaded: DOT, HELLO.` (the names sorted). HELLO, HI and DOT are then commands
of every open drawing and of those opened after, Enter repeats them, and AutoComplete offers them as it offers a plugin's
commands. A command runs its function with no arguments, as a run of its own whose undo step carries the command's name; what
the function returns is ignored. `alias` is taken when no command or alias has it. `description` is accepted and kept for
later: nothing shows it yet.

Loading the same file again replaces its commands, the old ones taken out first. A name that is a command already, and not
this file's, is refused and the rest of the file's commands are kept: `PYLOAD: LINE is a command already; tools.py did not
define it.` A file that fails part way keeps no command of its own; the error is written as [below](#errors-and-refusals). The
module-level code of the file runs at the load, as a run named PYLOAD, so a file that draws at its top level makes one undo
step. The commands last for the session; there is no startup list yet.

`cad_command` is the Python twin of the `[CadCommand]` attribute of a [command plugin](plugins.md). It is not called
`command`, which is the function that feeds the command line.

## The module `geometryhelper.cad`

Import it in a script, in the palette, or in a loaded file. Outside the app the import is an `ImportError`:
`geometryhelper.cad works inside GeometryHelper CAD only: in the PYTHON palette, PYRUN or PYLOAD.` A function that needs a
running script, called when none runs, is a `RuntimeError`.

| Function | What it does |
|---|---|
| `document()` | the drawing the run started in, a [`CadDocument`](xref:GeometryHelper.Cad.CadDocument); not the one shown later |
| `session()` | that drawing's [`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) |
| `selection()` | the handles of the objects selected now |
| `write(text)` | writes one line or more on the command line and in the palette |
| `command(*answers)` | runs a command to its end, as [below](#command) |
| `get_point(message, base, keywords, default)` | a [`GeoPoint2`](xref:GeometryHelper.Geometry.GeoPoint2), picked with the object snaps or typed |
| `get_distance(message, base, keywords, default)` | a distance, typed or as two points |
| `get_angle(message, base, keywords, default)` | an angle in radians, typed in degrees or as a direction |
| `get_integer(message, keywords, default)` | a whole number |
| `get_real(message, keywords, default)` | a number |
| `get_string(message, default, spaces)` | a text; with `spaces` false it ends at the first space |
| `get_keyword(message, keywords, default)` | one of the keywords |
| `get_entity(message, keywords)` | the handle of one object |
| `get_selection()` | the handles of the objects selected at "Select objects:", until Enter |
| `Cancelled` | the exception of Esc at a prompt |
| `cad_command(name, alias, description)` | the decorator of [PYLOAD](#pyload-and-cad_command) |

A prompt behaves as a command's own prompt does: the object snaps, typed coordinates, the rubber band from `base`, and Esc.
The message is written with a colon after it, `Specify centre:`, and a default is shown in angle brackets. The answer is a
point, a number or a handle as it was given, or a keyword as the prompt writes it (a `str`). Enter with no default gives
`None`, as `getpoint` of AutoLISP gives `nil`, and Enter with a default gives the default. `get_distance` takes a distance
typed or picked as two points, `get_angle` a direction as well as degrees, and the default of an angle is in degrees, as the
prompt shows it, while the answer is in radians. `get_integer` and `get_real` ask for text and read it in the invariant culture,
so `2.5` is 2.5 whatever the Windows settings are, and ask again after text that is none, writing `Requires an integer value.`
or `Requires numeric value.`, the words of AutoLISP's `getint` and `getreal`.

### A script that asks and draws

The file `rings.py` asks for a point and a distance, draws ten circles with `command`, and a line through the object model.
Each of the ten circles is a command typed for the user; the line is an edit made as C# would make it.

```python
from System import Action
from GeometryHelper.Cad import CadEditScope
from GeometryHelper.Dxf import DxfLine
from GeometryHelper.Geometry import GeoPoint3
from geometryhelper.cad import command, document, get_distance, get_point, write

centre = get_point("Specify centre")
step = get_distance("Specify ring step", base=centre)
if centre is None or step is None:
    write("Nothing drawn.")
else:
    for i in range(1, 11):
        command("CIRCLE", centre, step * i)
    doc = document()
    axis = DxfLine()
    axis.StartPoint = GeoPoint3(centre.X - 10 * step, centre.Y, 0)
    axis.EndPoint = GeoPoint3(centre.X + 10 * step, centre.Y, 0)
    doc.ApplyEdit("Axis", None, Action[CadEditScope](lambda scope: scope.Append[DxfLine](doc.ModelSpace, axis)))
    write(f"{doc.EntityCount} objects drawn, the axis {axis.StartPoint.DistanceTo(axis.EndPoint)} long")
```

Run with PYRUN in an empty drawing, with the point `100,50` picked and `5` typed, it draws circles of radius 5, 10 up to 50
about (100, 50) and a line 100 long through the centre, and the command line ends:

```text
Specify centre:
Specify ring step: 5
Command: CIRCLE
Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:
Specify radius of circle or [Diameter]: 5.0
Command: CIRCLE
Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:
Specify radius of circle or [Diameter] <5.0000>: 10.0
...
11 objects drawn, the axis 100.0 long
```

One U then takes all 11 objects away, the circles and the line, and Redo puts them back ([the undo step](#one-undo-step)).
The `pythonnet` details of the line, the delegate and the generic argument, are those of [Python](../python.md#what-pythonnet-does-with-net).

### `command`

`command(*answers)` runs a presenter command to its end, inside the run's undo step, at the Command prompt. The first answer
is the name of the command (`"CIRCLE"`, `"_LINE"`, an alias), and each answer after it is fed in order:

| Answer | Fed as |
|---|---|
| `""` | Enter |
| a `str` | typed |
| an `int` or a `float` | typed, in the invariant culture |
| a `GeoPoint2` or `(x, y)` | a point picked |
| a `GeoPoint3` or `(x, y, z)` | typed as `x,y` |
| a `list` of handles | the answer to "Select objects:", then Enter |

`command("_LINE", (0, 100), (100, 100), "")` draws a line of 100 and ends. After the last answer the command must have ended;
one that still asks is cancelled and raises `RuntimeError`: `CIRCLE still asked 'Specify radius of circle or [Diameter]:'
after the last answer.` A name that is not a command, an answer the prompt refuses and an answer given after the command
ended raise `ValueError` (`CIRCLE did not take 'abc' at 'Specify radius of circle or [Diameter]:'.`), the command cancelled
first. The command's prompts and answers are written on the command line as if typed, and Enter afterwards still repeats the
command the user ran, not the one the script fed. A command that opens a window of its own, a file dialog, shows it, modal,
as typed by hand; use the `-` form of such a command (`-LAYER`) in a script.

## One undo step

A run is one undo step, whatever it draws, through the object model, through `command` or both. The step is named `PYRUN`
for a file, the command's name for a command PYLOAD defined, `PYLOAD` for the module-level code of a load, and `Python` for a
palette entry. A run that ends well ends every group it left open. A run that fails or is cancelled takes every open group
back, so the drawing is as it was before the run, and nothing is left open that would make the next `SaveAs` throw.

The file `boom.py` draws a circle by `command("CIRCLE", (0, 0), 25)` and then divides by zero: the run fails, and the drawing
has 0 objects, not 1, and no undo step. A run that only asks and writes makes no step and does not mark the drawing
modified. An `undo_group` of the script, from `geometryhelper`, nests in the run's step. Autosave skips a drawing while a run
waits at a prompt with its step open.

## Esc

Esc ends a script in either of two places.

At a prompt Esc is the command line's own: the question ends, the prompt function raises `geometryhelper.cad.Cancelled`, and
every prompt the same run asks after it raises at once without asking, so a script that catches the first cannot go on
asking. `Cancelled` is a `KeyboardInterrupt`, so `except Exception` does not swallow it, and Esc at a prompt and Esc during
work end a run alike: the groups are cancelled, the command line writes `*Cancel*` and the palette `KeyboardInterrupt`. A
script that catches `BaseException` or a bare `except` swallows each Esc once.

While Python computes the window takes no clicks and no typing, so the app watches for Esc itself, and the script gets
`KeyboardInterrupt`. At a prompt the Stop of the palette presses Esc for you. On the test machine a loop of Python, `while True: pass`, ended 19 to 20 ms
after the stop on .NET 8 and 25 to 28 ms on .NET Framework 4.8; numpy matrix products of 2 500 by 2 500 ended 62 ms after.

It cannot stop what is not running Python. The interrupt lands when the call that holds the thread returns: a script in
`time.sleep(5)` ended 4 725 ms after the stop, when the sleep returned, and one inside a .NET call that blocked for two
seconds ended 1 693 ms after, when the call returned. Keep a blocking call short, or wait in a loop of short sleeps that
Esc can interrupt.

## Errors and refusals

An error ends the run and never closes the app. The command line gets one line,
`Python error: <Type>: <message> (<file name>, line <n>)`, where the file and line are those of the innermost frame in a file
the script runs (a palette entry has no file part), and the palette gets the whole traceback as error lines, without the
app's own frames. `boom.py`, with `print(ratio(1, 0))` on line 7 and the division on line 4 of `ratio`, writes
`Python error: ZeroDivisionError: division by zero (boom.py, line 4)`; the palette shows the call on line 7 as well. In
the palette, `1/0` writes `Python error: ZeroDivisionError: division by zero` and the traceback of one frame, `<console>`.
A syntax error gives its file, line and caret the same way. `sys.exit()` and `sys.exit(0)` end the run well; `sys.exit(3)`
is a failed run.

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

The ways a script can end badly were each run, by PYRUN and as a palette entry, and each leaves the app running, no undo
group open, the drawing as it was where the run failed, and the next run working:

| The script | What it leaves |
|---|---|
| `1/0`, `import nonexistent_module`, a file with a syntax error | a failed run and its `Python error:` line, as above |
| `sys.exit(3)`, `sys.exit("bye")` | a failed run: `Python error: SystemExit: 3` (or `bye`) and the file and line |
| `exit()`, `quit()`, `sys.exit(0)` | a run ended well, with no line |
| `raise KeyboardInterrupt` | a cancelled run, `*Cancel*`, as Esc gives |
| a function that calls itself for ever | a failed run, `RecursionError: maximum recursion depth exceeded` |
| an error in a PYLOAD command, or in the file PYLOAD loads | a failed run; the command runs again next time |
| a .NET exception from the object model (`ApplyEdit(None, None, None)`) | a failed run and one line naming the .NET type and message, `Python error: System.ArgumentNullException: Value cannot be null. ...`, the .NET stack left out |
| `sys.stdout = None`, or `sys.stdout` left on a file it closed or kept open | that run alone: the app puts its own `sys.stdout`, `sys.stderr` and `sys.stdin` back before every run, so the next run writes to the command line again |
| a handler attached to a .NET event (`CadDocument.Changed`) that raises when the event fires after the run ended | the error goes to the code that fired the event, as any handler's would; where nothing there catches it and it reaches the window, the command line of the drawing shown gets `Python error: <Type>: <message>`, the log a Warn, and no box comes up ([the crash log](app.md#crashes-and-the-crash-log)) |

Two ends are beyond the app. `os._exit()` ends the process at once, drawings and all, as it ends any Python. And a recursion
that passes through .NET on every level, a Python function called back by a .NET delegate that calls it again, uses the
window thread's stack faster than Python counts its own frames: 200 and 600 levels ran, but past about 650 levels the
stack overflows and .NET ends the process with nothing written, below Python's recursion limit of 1 000. A script that
recurses through callbacks keeps well under that or lowers the limit with `sys.setrecursionlimit`.

## Trusted folders

PYRUN, PYLOAD and the palette's Run file and Load go through the gate AutoLISP's `load` goes through, with the same
trusted folders and the same answers: SECURELOAD, TRUSTEDPATHS and the files answered Always Load, set with SETVAR or on
[the Security page](app.md#the-security-page) of Global settings, as
[Trusted folders and SECURELOAD](lisp.md#trusted-folders-and-secureload) describes. At SECURELOAD 1, the app's default, a
`.py` outside the trusted folders brings AutoCAD's warning, **Security - Unsigned Executable File**: Load Once runs it and
does not ask about that file again until the app restarts, Always Load also keeps the answer for that file and its
content, and Do Not Load writes `File load canceled: <full path>` on the command line and in the palette and ends the run
as `Refused`. At SECURELOAD 2 such a file is refused with no question. The folder of the app is always trusted.

The gate is in front of the files the app is asked to run. The modules a script imports are Python's own business, found on
`sys.path`, and are not checked, nor is code typed in the palette. A script outside the app, on the `geometryhelper`
package alone, meets no gate: a session made by the library starts at SECURELOAD 0.

## `print`, `input` and the threads

What a script prints goes to the command line and the palette in whole lines: Python's `print` of two lines makes 11 separate
writes to `sys.stdout`, so the app gathers them. A partial line is written before every prompt, so that
`input("Name? ")` reads well, and at the end of the run. `input(prompt)` asks the text at the command line, and Esc raises
`Cancelled`. Nothing is read from a console.

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

- `PAUSE` in `command`: handing one prompt of a fed command to the user, as AutoLISP's `PAUSE` does.
- A startup list for PYLOAD, as APPLOAD's Startup Suite keeps for AutoLISP; commands live for the session.
- A script editor in the palette, completion (Ctrl+Space) and a debugger.
- A Python of the app's own: the app does not bundle one.
- The window repainting while a script computes.
- Systems other than Windows: the app is Windows only.

## See also

- [Python](../python.md): the library and the presenter from outside the app, the stubs, numpy in blocks and undo groups.
- [AutoLISP](lisp.md): the other language of the command line; `command`, the prompts and Esc as it does them.
- [Command plugins](plugins.md): a command written in a .NET assembly.
- [Settings, autosave and the window](app.md#the-python-palette): the palette and the Python setting.
