# AutoLISP

The presenter reads and runs AutoLISP, the language AutoCAD's routines are written in. A line typed at the command line,
a file loaded with `(load)` or APPLOAD, and the files a drawing loads when it opens all run on an interpreter of the
presenter's own, one for each open drawing: [`LispInterpreter`](xref:GeometryHelper.Cad.Lisp.LispInterpreter), which a
[`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) makes the first time it is asked for. The values, the
printing and the error messages were measured against AutoCAD 2021, so a routine that runs there writes the same text here.

This covers five stages. The first is the core of the language: the data, the control forms, `defun` and the `c:` commands,
errors, and the functions for numbers, lists, strings and units. The second is the drawing: a routine can run the
presenter's commands with `command`, ask the user for points, distances and words, pick objects, select, read and change
entities and the tables, read and set system variables, and convert between coordinate systems. The third is Visual LISP's
library of [lists, strings and files](#strings-lists-and-files): `vl-sort`, the `vl-string-` functions, `open`, `read-line`
and `write-line`, `findfile`, the `vl-file-` functions, a store for `vl-registry-write`, the blackboard shared by the open
drawings, `getfiled`, and the pick-first selection. The fourth is Visual LISP's [ActiveX functions](#activex-objects):
`vl-load-com`, variants and safearrays, the `vlax-curve-` functions, and the objects of the drawing, which `vla-` and `vlax-`
names read and write: the document, its blocks and layers, the entities, their attributes and selection sets. The fifth is the
[dialogs](#dialogs-dcl): DCL files, `load_dialog`, `new_dialog` and `start_dialog`, and the 22 functions that fill a dialog
and read what the user did, shown in the presenter's window at the sizes AutoCAD 2021 draws. The rest of the
language's library comes in later stages and is listed [at the end](#what-is-not-there-yet).

The drawing stage was checked on seventeen routines written for AutoCAD by one user, each run in AutoCAD 2021's console and
in the presenter on the same blank drawing with the same typed answers: nine wrote the same command line and left the same
drawing, compared object by object and table by table, and the other eight differ only in the wording of a few prompts,
UNDO's settings line, AUDIT's report, the patterns of the linetypes and the boundary form of the hatches the presenter makes
with its own files, a dimension style's arrowhead stored as `_None` where AutoCAD stores `None`, and quirks of AutoCAD's console (a
prompt written over two lines or twice, a pick it misses outside its view, an arc length it kept from an earlier run).

The library stage was measured in nine probe files typed into AutoCAD 2021's console, and the 420 tests of the stage take
their expected values from them. The same routines then ran again: all 22 of the user's routines load alike in both
programs, and for the five that call the library, forms making the same calls on the same shapes of data (numbers with
repeats through `vl-sort`, a temporary dialog file written, read back and deleted, entity names filtered by type) were
typed into both. For three of the five the command line and the drawing came out the same; the other two differ only in
matters of the drawing stage (-LAYER's Lock of a backquoted name, `(setvar 'errno 0)`, the wording of -ATTDEF, -BLOCK and
-INSERT, and the groups of ATTRIB and MTEXT that `entget` lacks). A sixth feed, of the pick-first functions, differs in one
row, [described below](#pick-first-and-nentselp).

The ActiveX stage was measured in five probe files typed into AutoCAD 2021's console and in two runs of the full AutoCAD
2021, which has the object model the console lacks, and the 668 tests of the stage take their expected values from them or
from the ActiveX reference, as [described below](#activex-objects). The differences that are left are listed
[at the end of that section](#what-is-parked-in-the-activex-stage).

```csharp
var session = new CadCommandSession(document);

session.Feed("(setq a (+ 2147483647 1))");                  // Command: (setq a (+ 2147483647 1))
                                                            // -2147483648
session.Feed("(defun c:hello () (princ \"hello\") (princ))");
session.Feed("HELLO");                                      // Command: HELLO
                                                            // hello
session.Feed("(command \"CIRCLE\" '(0 0) 5)");              // Command: (command "CIRCLE" '(0 0) 5)
                                                            // CIRCLE
                                                            // Specify center point for circle or [3P/2P/Ttr (tan tan radius)]:
                                                            // Specify radius of circle or [Diameter]: 5
                                                            // Command: nil
```

## What runs

| Part | What is there |
|---|---|
| Data | lists and dotted pairs, symbols (kept in upper case), integers, reals, strings, `T` and `nil`, functions |
| Control | `quote`, `function`, `lambda`, `setq`, `if`, `cond`, `while`, `repeat`, `foreach`, `and`, `or`, `progn` |
| Definitions | `defun` and `defun-q`, with local variables after a slash: `(defun f (a / b) ...)` |
| Lists | `car`, `cdr` and the `c...r` forms, `cons`, `list`, `append`, `assoc`, `nth`, `member`, `reverse`, `length`, `subst`, `last`, `mapcar`, `apply`, `listp`, `atom`, `null`, `not` |
| Numbers | `+ - * /`, `rem`, `1+`, `1-`, `abs`, `min`, `max`, `fix`, `float`, `gcd`, `expt`, `sqrt`, `exp`, `log`, `sin`, `cos`, `atan`, `logand`, `logior`, `lsh`, `~`, `boole`, the comparisons, `minusp`, `zerop`, `numberp` |
| Strings | `strcat`, `substr`, `strlen`, `strcase`, `ascii`, `chr`, `atoi`, `atof`, `itoa`, `wcmatch`, `vl-symbol-name`, `vl-princ-to-string`, `vl-prin1-to-string` |
| Output | `princ`, `prin1`, `print`, `prompt`, `terpri` |
| Errors | `*error*`, `vl-catch-all-apply`, `vl-catch-all-error-p`, `vl-catch-all-error-message`, `exit`, `quit` |
| Units | `rtos`, `angtos`, `distof`, `angtof`, which read the drawing's LUNITS, LUPREC, AUNITS, AUPREC and DIMZIN; `cvunit`, which converts between units |
| Geometry | `angle`, `distance`, `polar`, `inters` |
| The rest | `type`, `eval`, `read`, `set`, `boundp`, `eq`, `equal`, `load`, `defun-q-list-ref`, `defun-q-list-set`, `vl-symbol-value`, `vl-symbolp`, `vl-consp`, `vl-load-com` (which defines the [ActiveX functions](#activex-objects)) |
| Visual LISP lists | `vl-sort`, `vl-sort-i`, `vl-position`, `vl-remove`, `vl-remove-if`, `vl-remove-if-not`, `vl-member-if`, `vl-member-if-not`, `vl-some`, `vl-every`, `vl-list*`, `vl-list-length` |
| Visual LISP strings | `vl-string-search`, `vl-string-subst`, `vl-string-position`, `vl-string-mismatch`, `vl-string-elt`, `vl-string-translate`, `vl-string-trim`, `vl-string-left-trim`, `vl-string-right-trim`, `vl-string->list`, `vl-list->string` |
| Files | `open`, `close`, `read-line`, `read-char`, `write-line`, `write-char`, `findfile`, `vl-filename-base`, `vl-filename-directory`, `vl-filename-extension`, `vl-filename-mktemp`, `vl-file-size`, `vl-file-systime`, `vl-file-directory-p`, `vl-file-copy`, `vl-file-rename`, `vl-file-delete`, `vl-mkdir`, `vl-directory-files` |
| Registry, blackboard, drawings | `vl-registry-read`, `vl-registry-write`, `vl-registry-delete`, `vl-registry-descendents`, `vl-bb-set`, `vl-bb-ref`, `vl-propagate`, `vl-load-all`, `vl-doc-ref`, `vl-doc-set`, and the VLX group (`vl-doc-export`, `vl-doc-import`, `vl-list-exported-functions`, `vl-vlx-loaded-p`, `vl-unload-vlx`, `vl-list-loaded-vlx`, `vl-get-resource`) |
| ActiveX variants and safearrays | after `vl-load-com`: `vlax-make-variant`, `vlax-variant-type`, `vlax-variant-value`, `vlax-variant-change-type`, `vlax-make-safearray`, `vlax-safearray-fill`, `vlax-safearray->list`, `vlax-safearray-get-element`, `vlax-safearray-put-element`, `vlax-safearray-get-dim`, `vlax-safearray-get-l-bound`, `vlax-safearray-get-u-bound`, `vlax-safearray-type`, `vlax-3d-point`, `vlax-tmatrix`, `variant-value`, `safearray-value`, and the constants `vlax-vbEmpty` to `vlax-vbArray`; `:vlax-true` and `:vlax-false` always |
| Commands | `command`, `vl-cmdf`, and the symbol `PAUSE` |
| Asking | `getpoint`, `getcorner`, `getdist`, `getangle`, `getorient`, `getint`, `getreal`, `getstring`, `getkword`, `initget`, `entsel`, `nentsel`, `nentselp`, `getfiled` |
| Selection sets | `ssget`, `sslength`, `ssname`, `ssadd`, `ssdel`, `ssmemb`, `sssetfirst`, `ssgetfirst` |
| Entities | `entget`, `entmod`, `entmake`, `entdel`, `entupd`, `entnext`, `entlast`, `handent` |
| Tables | `tblsearch`, `tblnext`, `tblobjname` |
| Variables | `getvar`, `setvar` |
| The session | `trans`, `osnap`, `alert`, `grtext`, `redraw`, `textbox` |
| ActiveX curves | after `vl-load-com`: the eighteen `vlax-curve-` functions, `getStartParam` to `isPlanar` |
| ActiveX objects | after `vl-load-com`: `vlax-get-acad-object`, `vlax-ename->vla-object`, `vlax-vla-object->ename`, `vlax-get-property`, `vlax-put-property`, `vlax-invoke-method`, `vlax-get`, `vlax-put`, `vlax-invoke`, `vlax-for` (before it too), `vlax-map-collection`, `vlax-property-available-p`, `vlax-method-applicable-p`, `vlax-object-released-p`, `vlax-release-object`, `vlax-erased-p`, `vlax-read-enabled-p`, `vlax-write-enabled-p`, `vlax-typeinfo-available-p`; refused with a message: `vlax-create-object`, `vlax-get-object`, `vlax-get-or-create-object`, `vlax-import-type-library` |
| ActiveX members | after `vl-load-com`: 143 `vla-get-`, `vla-put-` and method functions of the objects of the drawing |
| Dialogs | `load_dialog`, `unload_dialog`, `new_dialog`, `start_dialog`, `done_dialog`, `term_dialog`, `action_tile`, `get_tile`, `set_tile`, `mode_tile`, `get_attr`, `client_data_tile`, `start_list`, `add_list`, `end_list`, `start_image`, `fill_image`, `vector_image`, `slide_image`, `end_image`, `dimx_tile`, `dimy_tile`, and the variables `$key`, `$value`, `$reason`, `$data`, `$x` and `$y` inside an action |

The functions of the last eleven rows read or change the drawing, so they exist in the interpreter of a drawing and not in a bare
[`LispInterpreter`](xref:GeometryHelper.Cad.Lisp.LispInterpreter): `(getvar "OSMODE")` there is `; error: no function
definition: GETVAR`. The pure ones, `angle`, `distance`, `polar`, `inters` and `cvunit`, need no drawing and are in both, and
so are the Visual LISP rows of lists, strings, files and the registry, blackboard and drawings, and the ActiveX variants and
safearrays. What a bare interpreter lacks of those is what comes from a drawing: `getfiled`, `sssetfirst`, `ssgetfirst` and
`nentselp`, the folder of the drawing that `findfile` searches, and the line typed at the command line that `read-line` and
`read-char` read when they are given no file (they give `nil` there).

### Numbers

An integer is 32 bits and wraps, as AutoCAD's does: `(+ 2147483647 1)` is `-2147483648`, `(* 65536 65536)` is `0`, and
`(/ 7 2)` is `3` where `(/ 7 2.0)` is `3.5`. A number typed larger than 2 147 483 647 is read as a real. A real is printed
to six significant digits, and a whole value keeps a `.0`: `(/ 1 3.0)` writes `0.333333`, `(* 2 50000.0)` writes `100000.0`,
`123456789.0` writes `1.23457e+08`.

`rtos` writes a distance in the drawing's units: `(rtos 12.5 4 2)` is `1'-0 1/2"`, architectural units to a half inch, and
`(rtos 1.005 2 2)` is `"1.01"`, rounded half away from zero. DIMZIN decides which zeros are dropped, as it does in AutoCAD:
at 8 `(rtos 1.0 2 2)` is `"1"`.

### Errors

An error stops the form and writes one line, as AutoCAD does: `(car 1)` writes `; error: bad argument type: consp 1`, and
`(foo)` with no function of that name writes `; error: no function definition: FOO`. A routine that defines `*error*` has it
called with the message instead, and sees the local variables of the function that failed. `vl-catch-all-apply` catches an
error and gives it back as a value:

```lisp
(setq r (vl-catch-all-apply 'car '(1)))
(vl-catch-all-error-p r)           ; T
(vl-catch-all-error-message r)     ; "bad argument type: consp 1"
```

A routine that calls itself without end stops at 20 000 nested calls with `internal stack limit reached (simulated)`, as
AutoCAD's does at its own limit, and neither `*error*` nor `vl-catch-all-apply` sees it. The presenter keeps running. So
does a form of 20 000 open parentheses: the reader stops there with the same kind of error instead of running out of stack.

A fault inside the presenter's own code, a built-in that throws where AutoCAD would give a value, is not a crash: it is the
error `internal error: <what the code said>`, which `*error*` and `vl-catch-all-apply` see like any other and which gives
the local variables back as they were.

Esc while a routine asks the user is the error `Function cancelled`, and it is seen the same way: `*error*` is called
with it, with the locals of the function that asked.

### Speed

A loop of a million steps, `(repeat 1000000 (setq i (1+ i)))`, takes 48 ms here and 157 ms in AutoCAD 2021, both measured on
one machine, the presenter's a Release build.

## Typing at the command line

A line that starts with `(` or `!` is AutoLISP, at the Command prompt and at any prompt that does not ask for text. `!p`
writes the value of `p`. A form with an open parenthesis or string waits under the prompt `(_>`, one `(` more for each open
parenthesis and a `"` for an open string; Enter adds an empty line and Esc drops it. At a command's prompt, a value answers
it: a list of two or three numbers is a point, a number or a string is typed as it is, and nil asks again. `(defun
c:name ...)` makes `NAME` a command in either case: built-in names win, then `c:` commands, then aliases.

A form that asks for input, typed at a command's prompt, suspends the command and asks its own question, as AutoCAD does,
and the answer goes to the command as the value of the form. At the prompt of LINE after `0,0`, `(getpoint "X: ")` asks
`X: `; `5,5` answers it, and LINE takes the point `(5.0 5.0 0.0)` as its next point. A form that starts a command of its
own does not answer the prompt it was typed at.

A line is taken whole: Space types a space in it, and no word of it is read as an object snap. A bare word or number is
still a command: `1` at the Command prompt is not the number 1. A word that is no command writes `Unknown command "FOO".
Press F1 for help.`, with the name in capitals as AutoCAD writes it, whether it was typed or run by `command`.

What an evaluation writes comes first, then its value on the same line, as AutoCAD writes them: `(princ "hi")` writes
`hi"hi"`, where `(progn (princ "hi") (princ))` writes `hi` alone, the empty `(princ)` returning nothing to write. A new
line in the text starts a new line of the command line.

A `c:` command can be typed in either case and is offered by AutoComplete from its first letters. Enter repeats it, as it
does any command. A command that changes nothing leaves no undo step. It is the evaluation of its function, not a command
with a body of its own, so the function can run commands with [`command`](#commands-command-and-vl-cmdf) and ask the user
with the [get functions](#asking-the-user): while it waits, the prompt it asks is the one on the command line, and the
object snaps, transparent ZOOM and PAN, and the refusal messages work at it as at any prompt.

## Loading

`(load "name")` reads the file and evaluates its forms one at a time, and gives back the value of the last. A name with a
folder is that file; any other is looked for in the support folders, in order, those of Global settings' Folder preference
(the support file search path), and `.lsp` is added where the name has no extension. The folder of the drawing is not
searched: AutoCAD does not search it either. A file that is not found is `; error: LOAD failed: "name"`, or the value of the
second argument where there is one; an error inside the file ends the load and leaves what ran before it.

```lisp
(load "tools")                     ; the first tools.lsp of the support folders
(load "tools" "not found")         ; "not found", where there is none
```

A file is read as UTF-8, or as the machine's ANSI code page where it is not UTF-8. Compiled files (`.fas` and `.vlx`) are
refused.

### APPLOAD

APPLOAD (Manage tab, Applications, Load Application) shows Load/Unload Applications, a window of 720 by 600 that waits over
the presenter until Close, as AutoCAD 2021's dialog does but modal; what a file writes goes to the command line as it
loads. Its top half is a folder browser of its own: Look in, with Back and Up, over the folders and then the files of the
type chosen (AutoLISP Files, Plugins or All files, AutoLISP Files at the start), each group by name in ordinal order with
the case ignored. It opens in the folder it loaded from last, else the first support folder, else Documents. Choose a file,
or several with Ctrl or Shift, and press Load; or type a name, a file of the folder shown or a full path (several, each in
quotes), and press Enter. A double-click opens a folder, and loads a file.

Load takes each file in the order of the list, and the footer shows the line of the last:

| File | What Load does | Footer |
|---|---|---|
| `tools.lsp` | loads it into the drawing shown, as `CadCommandSession.LoadLisp` does: what the file wrote, then `tools.lsp successfully loaded.` | green: `tools.lsp successfully loaded.` |
| a `.lsp` with an error | runs up to the error and writes the `; error:` line; the file is not listed under Loaded Applications | red: `tools.lsp was not loaded: ; error: bad argument type: consp 1` |
| `CircleTools.dll` | goes to the [plugin manager](plugins.md), as PLUGINLOAD does | green or red, the manager's own line |
| `.arx`, `.dbx`, `.dvb`, `.fas`, `.vlx` | refused | amber: `tools.fas cannot be loaded: .arx, .dbx, .dvb, .fas and .vlx files are not supported.`, written to the command line too |
| any other file | refused | amber: `readme.txt is not an AutoLISP file or a plugin.` |

Under the browser are two tabs and the Startup Suite. **Loaded Applications** lists the AutoLISP files loaded into the
drawing shown, whether by Load, by `(load)` or as a startup file (a file read to its end, once each, in the order they
finished: `CadCommandSession.LoadedLispFiles`, or `LispInterpreter.LoadedFiles` of one interpreter), then the plugins
loaded now, with the columns File, Kind (AutoLISP in green, Plugin in blue) and Path; each drawing has its own list. The
**History list** has the files Load was given, newest first, at most 50 (a 51st pushes the oldest out), whether they
loaded or not, as long as Add to History is ticked, which it is at the start; it lasts across runs of the program. Load on
that tab loads the rows chosen.

One button beside the tabs follows the tab. **Unload** is for plugin rows, and does what the Plugin manager's Unload does:
on .NET Framework it switches the plugin off, its code stays until the presenter restarts, the footer shows the manager's
line, and the row stays, dimmed. It is greyed on an AutoLISP row, with the tooltip "AutoLISP files cannot be unloaded: what
they defined stays in the drawing until it is closed", as in AutoCAD. **Remove**, on the History list, takes the rows
chosen out of the list and never off the disk. The briefcase of the Startup Suite takes an AutoLISP file dragged from the
list or from Explorer, and Contents… shows the [Startup Suite](#the-startup-suite). Close, Esc and the cross close the
window and there is nothing to undo; Help opens this page.

The History list, the folder last used and Add to History are kept in the settings file, under `support`: `appLoadHistory`
(full paths, newest first), `appLoadFolder` and `appLoadAddToHistory`. The Plugin manager stays on the Plugins tab, and a
window that shows no such dialog (`MainViewModel.ShowAppLoad` null) shows the Plugin manager for APPLOAD, as APPLOAD did
before it loaded AutoLISP.

### When a drawing opens

Each drawing that opens runs, in this order:

1. `acad.lsp`, the first one found in the support folders, once for the run of the program, and not again for the drawings
   opened after the first;
2. `acaddoc.lsp`, the first one found in the support folders, for every drawing;
3. each file of the Startup Suite, in order;
4. the function `S::STARTUP`, where one of these files defined it. It may be made with `defun` or with `defun-q`, which
   AutoCAD's documents recommend so that several routines can add to it; its value is not written.

What the files write goes to the command line, and an error in one is written and does not stop the next. A file that is
not found is no line for `acad.lsp` and `acaddoc.lsp`, which most machines do not have, and a `; error: LOAD failed:` line
for a file of the Startup Suite. [`LispStartup`](xref:GeometryHelper.Cad.Lisp.LispStartup) does this for one run of the
program; the window gives it each drawing it opens, and each time it fills the Startup Suite from the settings.

### The Startup Suite

The Startup Suite is the list of AutoLISP files loaded into every drawing that opens, in the order of the list: Contents…
on Load/Unload Applications shows it in a window of its own, 460 by 360. Add… asks for one or more `.lsp` files and puts
them at the end; Remove takes the file chosen out of the list and never off the disk. Each change is saved to the settings
file at once and applies to the drawings opened afterwards, not to those open now. The suite holds AutoLISP files only: a
`.dll` dropped on the briefcase is refused with "Plugins load at start from the Plugin manager (At start).", and any other
file with a line that says so. The list is kept in the settings file, under `support`, next to the search folders, and the
file can still be edited there.

```json
"support": {
  "searchFolders": [ "D:\Lisp" ],
  "lispStartupSuite": [ "D:\Lisp\layers.lsp", "D:\Lisp\dims.lsp" ]
}
```

Blank entries are left out and a file named twice is loaded once.

### Trusted folders and SECURELOAD

Loading a file runs its code with the user's rights, so, as AutoCAD 2021 does, a file of code is loaded only from a trusted
folder unless the user says otherwise. The gate stands in front of `load` and everything built on it: APPLOAD, the files of
[When a drawing opens](#when-a-drawing-opens) and the Startup Suite, `autoload`, `vl-load-all` and the copy it brings into
another drawing. It does not stand in front of `load_dialog`, SCRIPT, `open` or `findfile`, nor of code typed at the
command line: AutoCAD 2021 loads an untrusted `.dcl` and runs an untrusted `.scr` with no question, also at SECURELOAD 2.
The plugin manager keeps [its own trusted folders](plugins.md). Three system variables set it, readable and settable with
`getvar`, `setvar` and SETVAR, and kept in Global settings ([the Security page](app.md#the-security-page)):

| Variable | Values | In the app | In a session of the library |
|---|---|---|---|
| SECURELOAD | 0 loads from anywhere; 1 loads from the trusted folders and asks about any other file; 2 loads from the trusted folders only | 1, AutoCAD's initial value | 0 |
| TRUSTEDPATHS | the trusted folders, parted by `;` | empty | empty |
| LEGACYCODESEARCH | 0 leaves the current folder and the drawing's out of a search for a file of code; 1 puts them in | 0 | 0 |

A session made by the library, with no window to ask, starts at SECURELOAD 0, so a program that loads routines headless
loads them as it did before; the window gives every drawing one shared
[`CadLoadSecurity`](xref:GeometryHelper.Cad.CadLoadSecurity) built from the settings, so a SETVAR of SECURELOAD in one
drawing holds in the others and after a restart. `(setvar "SECURELOAD" 3)` is
`; error: AutoCAD variable setting rejected: "SECURELOAD" 3`.

A file is decided in this order: SECURELOAD 0 loads it; a file in a trusted folder loads; SECURELOAD 2 refuses it; at 1 a
file answered Load Once in this run of the app, or Always Load with the same content, loads, and any other is asked about.
The folder of the app and all its folders below are always trusted, as AutoCAD's program folders are: the presenter's
linetypes, hatch patterns and Python package lie there. TRUSTEDPATHS is read as AutoCAD 2021 reads it, each form measured
there with a `setvar` and a `load`:

| TRUSTEDPATHS | Trusts |
|---|---|
| `D:\Lisp` | the files in `D:\Lisp`, not those of `D:\Lisp\Old` |
| `D:\Lisp\...` | `D:\Lisp` and every folder below it, at any depth |
| `D:\Lisp\` or `d:\LISP` | `D:\Lisp` (a closing backslash and the case change nothing) |
| `D:\Lisp\Old\..` | `D:\Lisp`, the path worked out |
| `D:\Lisp;D:\Tools` or `  D:\Lisp  ;;` | both; the spaces around an entry and the empty entries are passed over |
| `%USERPROFILE%\Lisp` | `C:\Users\you\Lisp`: the variable is expanded when it is set, and `getvar` gives it expanded |
| `"D:\Lisp"`, `D:\Li*` | nothing: quotes are not taken off, and there are no wildcards |
| `D:\Work` | the files in `D:\Work`, not those of `D:\Work\Lisp` |

Only a backslash before `...` takes the folders below; `D:/Lisp/...` trusts `D:\Lisp` alone, as in AutoCAD. An entry that
is not a rooted path, such as `Lisp`, is passed over (the presenter's choice; AutoCAD's was not measured).

A refused file named with a folder is the error `File load canceled:` and the name as it was given, slashes and all, at
SECURELOAD 1 and 2 alike; the second argument of `load` is not given back for it, as in AutoCAD, so `vl-catch-all-apply`
catches it as any error:

```text
Command: (load "C:/Users/you/Downloads/tools.lsp")
; error: File load canceled: C:/Users/you/Downloads/tools.lsp
Command: (load "C:/Users/you/Downloads/tools.lsp" "none")
; error: File load canceled: C:/Users/you/Downloads/tools.lsp
```

A file refused in a search for a bare name is passed over and the search goes on; where nothing else is found the load is
`; error: LOAD failed: "tools"`, and there the second argument is given back. A `.fas` or `.vlx` outside the trusted
folders is cancelled as any file; one inside them is still refused as compiled AutoLISP. APPLOAD's footer shows the line
of a refusal in red, and a startup file that is refused writes it and lets the next one run.

At SECURELOAD 1 the window asks with AutoCAD's own warning, **Security - Unsigned Executable File**: `The publisher of this
executable file could not be verified and the file is not located in a trusted folder. What do you want to do?`, the
file's name, `Unknown Publisher`, its folder, `None` for the issuer and its date, and three buttons. **Do Not Load**, the
default (Enter, Esc and the window's cross give it too), refuses the file as above. **Load Once** loads it, and the same
file is not asked about again until the app restarts, in any drawing. **Always Load** loads it and remembers the file with
its content, kept in the settings file across runs: AutoCAD 2021 asked again once one line was added to such a file, and
so does the presenter, which keeps a SHA-256 of its bytes. Always Load does not add the folder to TRUSTEDPATHS, and the
next file of that folder is asked about. At SECURELOAD 2 an Always Load answer does not count. A session with no window
refuses, as AutoCAD's console does.

`findfile` and `load_dialog` follow LEGACYCODESEARCH for the names of code. At 0, a bare name with the extension `.lsp`,
`.fas`, `.vlx`, `.mnl`, `.scr`, `.dcl`, `.dll`, `.arx`, `.dvb`, `.cuix` or `.py` is looked for in the support folders
only, not in the current folder nor beside the drawing: with ten files `ghl6f.<ext>` beside a drawing, AutoCAD 2021's
`findfile` found the `.txt`, `.js`, `.lin` and `.pat` and none of those ten. At 1 the current folder, the folder of the
drawing and then the support folders are searched for every name, by `load` too. `load_dialog` of a bare name leaves the
two folders out at 0 for any name, since it adds `.dcl` to one with no extension.

## Commands: command and vl-cmdf

`(command "LINE" '(0 0) '(10 0) "")` runs LINE and gives it one argument for each prompt, in turn, as if the user had typed
it. With CMDECHO at its default of 1 the command line shows what AutoCAD 2021's shows: the name, each prompt as it is
answered, and then the value, `nil`:

```text
Command: (command "LINE" '(0 0) '(10 0) "")
LINE
Specify first point:
Specify next point or [Undo]:
Specify next point or [Undo]:
Command: nil
```

`vl-cmdf` does the same and gives `T`. Each argument goes to whatever prompt is current:

| Argument | What it does |
|---|---|
| a string, no command running | starts the command of that name: a command, an alias, with the `_` and `.` prefixes (`"_.LINE"`), or a system variable typed alone (`"DIMSCALE"` asks `Enter new value for DIMSCALE <1.0000>:`). A command that has a command-line twin runs the twin, as AutoCAD runs it for a routine: `"layer"` asks -LAYER's prompts, and `"hatch"`, `"style"`, `"purge"` and `"insert"` those of -HATCH, -STYLE, -PURGE and -INSERT. The name is written as it was given |
| a string, at a prompt | typed text: a keyword, `"@5<0"`, `"end"`, `"0,90"`, `"All"`. It is never AutoLISP: `"(list 0 100)"` at LINE's first prompt is `Invalid point.` |
| `""` | Enter. Inside a routine, Enter at the Command prompt repeats the last command, and never a `c:` command: AutoCAD's own rule |
| an integer or a real | its text, so `5` and `2.5` are typed as they read; a real goes in at full precision, not to the six digits it prints with |
| a list of two or three numbers | a point, in the current UCS |
| an entity name | picks that entity at "Select objects:", or at a prompt that asks for one object |
| a selection set | gives its objects, still alive, at "Select objects:" (`1 found` for a set of two when one of them has been erased), and its first object at a prompt for one |
| `nil` | cancels the command running, and writes no `*Cancel*` line; with no command running it does nothing |
| `PAUSE` | the symbol, whose value is `"\\"`: waits for the user to answer the prompt the command is at, then goes on with the next argument when the command asks its next prompt or ends |
| anything else | the error `bad argument value: AutoCAD command: LINE` for `(command 'line ...)` |

A name that is no command writes `<name> Unknown command "NAME".  Press F1 for help.` and `command` goes on with the next
argument, with no error. A `c:` command is not found by `command`: typing `GHP` runs a routine, but `(command "GHP")` is an
unknown command, as in AutoCAD. Where AutoCAD writes `Select objects:   1 found` on one line, the presenter writes the
prompt and `1 found` on two.

**Refusals.** A prompt that refuses an argument writes its refusal, `Invalid point.` or `Point or option keyword required.`.
`command` then fails with `Function cancelled` and leaves the command at its prompt; `vl-cmdf` writes the same refusal,
passes nothing more and gives `T`. `(command)` with no argument cancels the command running.

```text
Command: (command "LINE" "(list 0 100)" "10,100" "")
LINE
Specify first point: (list 0 100)
Invalid point.
; error: Function cancelled
```

**A command left running.** A command still asking after the last argument stays running, and the next `command` goes on
feeding it. When the routine ends with the command still running, it waits for the user at its prompt, and the text the
routine writes after it comes after that prompt: `(defun c:ghk () (command "LINE" '(0 0)) (princ "left"))` typed as `GHK`
ends at `Specify next point or [Undo]: left"left"`, and `4,4` typed then is LINE's next point. A command that would go on
asking for more of the same, TEXT's next line or COPY's next second point, takes one go and ends, so that it does not take
the routine's next command for an answer: `(command "TEXT" '(0 0) 2.5 0 "Hello")` ends with `Enter text: Hello` and then
`Command: nil`.

**PAUSE.** `(defun c:ghp () (command "LINE" '(0 40) pause pause "") (princ))` typed as `GHP` stops at `Specify next point or
[Undo]:` for the user, who types `3,45`; LINE asks again, the user types `5,45`, and the routine goes on with `""`. Esc at a
PAUSE cancels the command and gives the routine `Function cancelled`, which a routine that defines `*error*` sees like any
other error: with `(defun *error* (m) (princ (strcat "E:" m)) (princ))`, Esc at the PAUSE of `(command "LINE" '(0 0) pause
"")` writes `Command: E:Function cancelled`. Text that starts with `(` or `!`, typed at a PAUSE, is plain input for the
prompt: AutoLISP is not read while a routine waits.

**CMDECHO.** At 0 the commands the routine runs write nothing, and a PAUSE writes only what the user typed: `5,5`, or
`*Cancel*`. The refusal lines and the `Unknown command` line are still written, so that a routine that fails says why. The
get functions write their prompts whatever CMDECHO is.

**Undo.** Each command `command` runs is an undo step of its own, as when it is typed: a routine that runs LINE and then
CIRCLE is undone by U one command at a time. The changes a routine makes between its commands, `entmake`, `entmod`,
`entdel` and `setvar` of a drawing variable, are one step of their own, named after the `c:` command or `(Lisp Expression)`
for a form typed; forms typed at the Command prompt join the steps of the forms and commands before them, as
[described below](#undo-of-typed-forms). `UNDO BEgin` and `End`
run by `command` group what lies between them as when typed. Esc at a routine's wait ends the routine's step; what `*error*`
then changes is a step of its own, and U undoes it first.

**The commands themselves.** A routine runs the presenter's commands, so it gets what they do. Its routines asked for some
of AutoCAD's command-line forms the presenter did not have, and they were added as AutoCAD 2021's console asks them:
-LAYER's Make, Color, Ltype, LWeight and Rename (`layer "m" "T1" "c" "3" "" ""` makes the layer, colours it green and makes
it current), UCS's hidden `E`, which aligns the UCS to an object, -PURGE's prompts and its line for each kind of object,
REVCLOUD's Arc length with its minimum and maximum, and DIMLINEAR's `Non-associative dimension created.` A drawing whose
DXF header leaves out the dimension variables, DIMSTYLE or CELTYPE, as AutoCAD's own blank drawing does, is read with the
values AutoCAD reads for it (DIMASZ 0.18, DIMSTYLE Standard, CELTYPE Continuous), so that a routine sees the same settings
in both programs. UNDO keeps the settings of AutoCAD's window (`Combine = Yes, Layer = Yes`), where the console reads
`Combine = No, Layer = No`: UNDO's settings line is the one place a routine's transcript differs there.

## Asking the user

The get functions ask the user and give back what was answered. The prompt is shown as the routine gave it: the text up to
its last `\n` is written as lines and the rest is the prompt, so `"\nNext: "` asks `Next: `. The echo is the prompt followed
directly by what was typed:

```text
Command: (getpoint '(1 1) "\nNext: ")
Next: @2,0
(3.0 1.0 0.0)
```

| Function | Gives | Refusal, and the prompt again |
|---|---|---|
| `(getpoint [pt] [msg])` | a list of three reals in the UCS: `3,4` is `(3.0 4.0 0.0)`, and `3,4,5` keeps its Z | `Invalid point.` |
| `(getcorner pt [msg])` | the same, the opposite corner of a rectangle from `pt` | `Invalid point.` |
| `(getdist [pt] [msg])` | a real: a number, or two points (`0,0` then ` Specify second point: 3,4` gives `5.0`); with a base point one point is enough (`6,8` from `(0 0)` gives `10.0`) | `Requires numeric distance or two points.` |
| `(getangle [pt] [msg])` | radians, counted from ANGBASE in the sense of ANGDIR: `45` is `0.785398` | `Requires valid numeric angle or two points.` |
| `(getorient [pt] [msg])` | radians from east, counter-clockwise, whatever ANGBASE and ANGDIR are | as `getangle` |
| `(getint [msg])` | an integer from -32768 to 32767 | `Requires an integer value.` and `Requires an integer between -32768 and 32767.` |
| `(getreal [msg])` | a real | `Requires numeric value.` |
| `(getstring [cr] [msg])` | the text; `""` for Enter | none |
| `(getkword [msg])` | the keyword, as written in `initget` | `Invalid option keyword.` |

Enter gives `nil` (`""` for `getstring`). An argument of the wrong kind is the error AutoCAD gives: `(getpoint 5)` is
`bad argument type: point: 5`, and `(getint 5)` is `bad argument type: stringp 5`. The base point and the message may be given either way round:
`(getpoint "Pick: " '(1 1))` is the same as `(getpoint '(1 1) "Pick: ")`. A get function leaves LASTPOINT as it was, the base
point and the answer alike; `@` measures from the base point during the prompt.

`getstring` without its first argument ends at a space, as at Enter, and the rest of the typed line is the next input:
`hello world` answers `Str: ` with `"hello"` and then runs `world` as a command, which is `Unknown command "WORLD"`. With
`cr` not nil the line is taken whole, `"hello world"`. The window's Space follows that
([`CadPrompt.TakesSpaces`](xref:GeometryHelper.Cad.CadPrompt.TakesSpaces)).

**initget** sets what the next get function, `entsel`, `nentsel` or interactive `ssget` takes, and is cleared by it, across
lines too. `(initget [bits] [string])`, the two in either order, gives `nil`. The bits:

| Bit | Meaning |
|---|---|
| 1 | Enter is refused, with the refusal of the function (`Point or option keyword required.` where there are keywords) |
| 2 | zero is refused: `Value must be nonzero.` |
| 4 | a negative is refused: `Value must be positive.`; 2 and 4 together `Value must be positive and nonzero.` |
| 64 | `getdist` in two dimensions |
| 128 | any text is returned as a string, as `"hello"` |
| 8, 16, 32, 256, 512, 1024 | accepted and ignored |

The string is the keywords, separated by spaces, with capitals marking the shortest form: `"Yes No"` takes `y`, `ye` and `yes`
for `Yes`; `"Def"` takes `d`, `de` and `def`. A keyword is typed in any case, and the function gives it as written in the
string.

```text
Command: (initget "Yes No")
nil
Command: (getkword "Choose [Yes/No]: ")
Choose [Yes/No]: y
"Yes"
```

`(initget 1.5)` is `bad argument type: fixnump: 1.5`.

**Esc** at a get function is `Function cancelled` in the routine, with the locals as they were, so `*error*` sees them. A
routine that catches it and asks again gets a new question: Esc cancels one question at a time. **A form typed at the
prompt** is not read as AutoLISP while a routine waits: `(list 1 2)` typed at a `getstring` is the answer `"(list 1 2)"`
after `Can't reenter LISP.`, and at a `getpoint` it is `Can't reenter LISP.` and `Invalid point.`. Host calls that would run
AutoLISP while a routine waits, APPLOAD or a startup file, are refused with the same line.

### entsel and nentsel

`(entsel [msg])` asks `Select object: ` and gives `(ename pt)` for the object nearest the pick, within the pick box, with the
point in the UCS: `0,0` on the end of a LINE gives `(<Entity name: ...> (0.0 0.0 0.0))`. `L` takes the last object. A pick
that finds nothing gives `nil` at once and does not ask again; Enter gives `nil`; a keyword of `initget` gives its string;
other text is `*Invalid selection*` and `Expects a point or Last`, and the prompt again. `nentsel` does the same for an object
of a space, and for one inside a block reference gives `(ename pt matrix (insert-ename))`, with the insert's name last. The matrix is the form
AutoCAD 2021 gave in its window, 4 by 4 and row by row with the translation in the fourth column, not the 4 by 3 form of
its documentation: a line in a block reference at (100, 0), picked at (125, 5), is `(<Entity name: ...> (125.0 5.0 0.0)
((1.0 0.0 0.0 100.0) (0.0 1.0 0.0 0.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0)) (<Entity name: ...>))`, and the owners come
innermost first for nested references. A routine written for the 4 by 3 form finds the origin in the last item of the first
three rows and a row `(0 0 0 1)` under them. A pick on the
text of a visible attribute gives the ATTRIB itself and the point only, `(<Entity name: ...> (103.0 2.5 0.0))`, with no matrix and
no list of references after them, as AutoCAD 2021 does; `entsel` still gives the block reference for the same pick, and an
invisible attribute cannot be picked. `(entmod ...)` of that name changes the attribute.

## Selection sets

`(ssget)` asks `Select objects:` with every way of selecting the presenter has, point, `W`, `C`, `ALL`, `L`, `P`, `F`,
`WP`, `CP`, Remove and Undo, until Enter, and gives `nil` when nothing was selected, so `(while (setq s (ssget)) ...)`
ends. The first argument gives it a mode:

| Mode | The set |
|---|---|
| `"X"` or `"_X"` | the whole drawing, every space, the newest object first |
| `"W"`, `"C"` | the window or crossing of two points: `(ssget "W" '(-1 -1) '(11 1))` |
| `"WP"`, `"CP"`, `"F"` | a polygon, a crossing polygon or a fence through a list of points |
| `"L"`, `"P"` | the last object, the previous selection |
| `"I"` | the objects selected before the command (PICKFIRST) |
| `":S"` | one pick or one window ends the selection |
| a point alone | picks at it: `(ssget '(0 0))` |

A filter list keeps only the objects that match, and with an interactive `ssget` counts only those: `(ssget '((0 . "CIRCLE")))`
at `Select objects: ALL` says `1 found` in a drawing that has one circle among five objects. The groups of a filter are:

| Group | Matches |
|---|---|
| `0` | the type, by `wcmatch` and in any case; `"DIMENSION"` takes every dimension kind |
| `8`, `2`, `1`, `7`, `410` | the layer, the block name, the text, the style, the space |
| `62` | the colour, with 256 for BYLAYER |
| `-4` | an operator for what follows: `"<AND"` ... `"AND>"`, `"<OR"` ... `"OR>"`, `"<XOR"` ... `"XOR>"`, `"<NOT"` ... `"NOT>"`, or a comparison for the next group: `"="`, `"!="`, `"/="`, `"<>"`, `"<"`, `"<="`, `">"`, `">="`, `"*"`, `"&"`, `"&="`, where a point takes `">,>,*"` |

```text
Command: (sslength (ssget "X" '((-4 . "<OR") (0 . "TEXT") (0 . "CIRCLE") (-4 . "OR>"))))
2
```

Every set `ssget` gives becomes the Previous selection, so `(ssget "P")` and `P` at the next command's prompt give it. Esc is
`Function cancelled`. `ALL` at `Select objects:` of any command lists the newest object first, as `"X"` does, with the
handle's text breaking a tie, as the selection engine of AutoCAD walks the drawing. So do `"W"`, `"C"`, `"WP"` and `"CP"`, a
window typed at the prompt and Previous after them: five lines at y 0 to 40 give 40, 30, 20, 10, 0 (AutoCAD 2021), where a fence
keeps the order of its points in AutoCAD and here the order of the drawing. With a view (the window has one, a script does not),
`"W"`, `"C"` and `"WP"` find only what the view shows, `"CP"` and `"F"` everything, as AutoCAD 2021's console did.

`(sslength ss)` is an integer, `(ssname ss i)` the i-th entity name (`nil` out of range, `-1` included), `(ssadd)` a new empty
set, `(ssadd e)` a new set with `e`, `(ssadd e ss)` adds to `ss` in place and gives it, `(ssdel e ss)` takes out in place, and
`(ssmemb e ss)` gives the name or `nil`. `(sslength nil)` is `bad argument type: lselsetp nil`. A set prints as
`<Selection set: 1d>`, a hexadecimal number that counts up for each set made.

## Entities and tables

`(entget ename [apps])` gives the entity as an association list: `(-1 . ename)` first, then its groups in the order the
presenter holds them. The pointer groups (330, 340-369, 390-399, 480-481) are entity names, the points (10-18, 110-112, 210,
1010-1013) lists of their coordinates, two for each vertex `10` of an LWPOLYLINE, the integers and reals follow the group's
type, and the `102` groups are kept. Extended data is `(-3 ("APP" ...))` for the applications named in `apps`, and
`'("*")` gives every one. An erased or unknown entity gives `nil`. A group the presenter does not hold is not invented.

`(entmod list)` gives the list, or `nil` for a list with no `-1`, an unknown or erased entity, a change of group `0`, or a
value of the wrong kind. A group code the list gives twice has the last value, as in AutoCAD: `(entmod (append (entget e) '((62 . 5) (62 . 6))))`
makes the colour 6 and leaves one group `62`, which is also how `(entmod (append (entget e) '((62 . 6))))` changes the colour of an entity that
has one; a code the entity has several of, the vertices of a polyline or the knots of a spline, is not collapsed, and a colour
256 or a linetype ByLayer takes the entity's own group out. A partial list changes only the groups it has: `(entmod (list (cons -1 e) '(10 1.0 1.0 0.0)))` moves
the start of a LINE and nothing else. A layer named in group `8` that does not exist is made, white and Continuous. A
changed DIMENSION gets its picture made again, and a TEXT whose `72`, `73` or `11` change gets its `10` computed again, in the
fonts of the drawing, by the routine `vla-put-TextString` and `vla-put-Alignment` use too, so that a text changed through the
object and one changed by `entmod` start the same distance back from their second point. **An ATTRIB** of a block reference is changed by `entmod` as AutoCAD 2021
changes it (measured in its console): `(entmod (subst (cons 1 "NEW") (assoc 1 e) e))` on the list of an attribute gives the list
and `(cdr (assoc 1 (entget att)))` is `"NEW"`, and a partial list such as `(list (cons -1 att) (cons 1 "PART"))` changes the text
alone. The text is placed as a TEXT's is; a group `10` moves the attribute and not the block reference (a reference at (10, 10)
stays there with its attribute at (20, 20)); and a layer named in `8` that is not there is made, colour 7 and Continuous. It is
one undo step, `(entupd ins)` of the reference gives its name and keeps the change, and a list of the wrong kind, or a change of
the group `0`, is `nil`. This is what a routine needs that picks an attribute with `nentsel` and edits its text.
`(entupd e)` gives the name and redraws.

`(entmake list)` gives the list, or `nil` for a type the presenter cannot make or a required group that is missing:
`(entmake '((0 . "LINE") (10 0.0 0.0)))` is `nil`, a LINE with no end. The defaults are AutoCAD's (a TEXT is made with
rotation 0, width factor 1, style Standard, justification left, and the normal `(0 0 1)`), a layer that is missing is
made, the entity goes into the current space, and `(entlast)` is it. A list with no group `6` takes the drawing's CELTYPE, so a LINE made on a blank drawing has `(6 . "Continuous")`, and
none where CELTYPE is ByLayer; a `(6 . "ByLayer")` or `(62 . 256)` the list gives is dropped, as AutoCAD drops it, and the
entity lists neither. A list straight from `entget` is taken: the leading
`-1`, and the `5` and `330` groups, are ignored, and a two-dimensional point is completed. The presenter makes LINE, POINT,
CIRCLE, ARC, ELLIPSE, TEXT, MTEXT, LWPOLYLINE, SOLID, TRACE, 3DFACE, RAY, XLINE, SPLINE and INSERT without attributes. A
POLYLINE with its VERTEX and SEQEND, an INSERT with ATTRIB, a BLOCK definition and the records of a table give `nil`.

`(entdel e)` gives the name and erases; a second `entdel` of the same entity restores it, as AutoCAD's does. The ATTRIB of a
block reference is not erased that way: `(entdel att)` gives `nil` and leaves it, as many times as it is asked, which is what
AutoCAD 2021 does. `(entnext)`
gives the first object of the drawing and `(entnext e)` the one after it, the sub-entities of a POLYLINE or an INSERT with
attributes following their owner, and `nil` after the last. `(entlast)` is the last main object made, `(handent "1A")` the
name of the object with that handle, or `nil`.

```text
Command: (entdel e1)
<Entity name: 1e4094de0e0>
Command: (entget e1)
nil
Command: (entdel e1)
<Entity name: 1e4094de0e0>
Command: (cdr (assoc 0 (entget e1)))
"LINE"
```

`(tblsearch table name [setnext])` gives the record of a layer, linetype, text style, dimension style, block, UCS, view,
viewport or application, the table and the name in any case: `(tblsearch "layer" "newlayer")` is `((0 . "LAYER") (2 .
"NEWLAYER") (70 . 0) (62 . 7) (6 . "Continuous"))`. The `62` of a layer that is off is negative, and its `70` is 1 for
frozen and 4 for locked. A record of BLOCK ends with `(-2 . ename)`, the first entity of the block. An unknown table is
`AutoCAD rejected function: invalid table function argument(s): "FOO" "x"`. `(tblnext table [rewind])` walks the records in
the order of the table, from the first when `rewind` is `T`; for LTYPE it leaves out ByBlock and ByLayer, which `tblsearch`
still finds. `(tblobjname table name)` is the record's entity name, and `entget` of it gives the whole record.

## System variables

`(getvar name)` takes a string or a symbol, in any case, and gives the value: an integer, a real (angles in radians), a
string, or a point as a list of reals (three for LASTPOINT, UCSORG and VIEWCTR). A name the presenter has no variable of gives
`nil`: the ones AutoCAD has and the presenter lacks. `(getvar 5)` is `bad argument type: (or stringp symbolp): 5`.

`(setvar name value)` takes the value by the variable's own rules, as SETVAR does, and gives the value as it was given:
`(setvar "TEXTSIZE" 3)` gives `3` and `(getvar "TEXTSIZE")` then `3.0`. A value refused is the error `AutoCAD variable
setting rejected: "NAME" <value>`, for an unknown name, a read-only variable, a wrong kind, an out-of-range number or a
layer that is not there:

```text
Command: (setvar "LUPREC" 99)
; error: AutoCAD variable setting rejected: "LUPREC" 99
Command: (setvar "ACADVER" "x")
; error: AutoCAD variable setting rejected: "ACADVER" "x"
```

Variables added for routines:

| Variable | Value |
|---|---|
| `PRODUCT` | `"GeometryHelper.Cad"`, read-only. AutoCAD gives `"AutoCAD"`, so `(= (getvar "PRODUCT") "AutoCAD")` tells a routine which program runs it |
| `ACADVER` | `"24.0"`, read-only: the AutoCAD version whose behaviour the presenter follows, so a routine that checks the version takes AutoCAD 2021's path |
| `CMDACTIVE` | read-only: 1 while a command runs, 2 transparent, 4 a script, 32 AutoLISP |
| `CMDNAMES` | read-only: the name of the command running, `""` for none |
| `UCSXDIR`, `UCSYDIR` | read-only: the axes of the UCS |
| `LISPINIT` | read-only, 1 |
| `BLIPMODE`, `MENUECHO` | kept and without effect |
| `NOMUTT` | 0 or 1, kept for the session, 0 at first; AutoCAD's 1 mutes the prompts of a routine's `command`, which the presenter does not, so a routine that sets it and puts it back (`(setvar 'nomutt 1)` of TE) runs as before |
| every dimension variable | `DIMASZ`, `DIMTXSTY`, `DIMSAH`, `DIMBLK1` and the rest, as system variables of the drawing's header, typed alone or set with SETVAR with AutoCAD's prompts (`Enter new value for DIMSAH <OFF>:` takes ON or OFF); the name of an arrowhead (`DIMBLK`, `DIMBLK1`, `DIMBLK2`, `DIMLDRBLK`) is read without its underscore, `"_None"` as `"None"` and `"_DOT"` as `"Dot"`, a period is the default, `""` is refused and an unknown block writes `Block "nosuch" not defined` before the refusal |

## Geometry, units and the session

`(angle p1 p2)` is the angle of the line in the XY plane, `(angle '(0 0) '(1 1))` is `0.785398` and `(angle '(1 1) '(1 1))`
is `0.0`. `(distance p1 p2)` is three-dimensional when both points are, otherwise two-dimensional: `(distance '(0 0) '(1 2
2))` is `2.23607`. `(polar pt ang dist)` keeps the dimension and the Z of the point: `(polar '(1 1 3) (/ pi 2) 2)` is `(1.0
3.0 3.0)`, and `(polar '(0 0) pi 1)` is `(-1.0 1.22465e-16)` as AutoCAD writes it. `(inters a b c d [onseg])` takes
the segments by default, so `(inters '(0 0) '(1 0) '(5 -5) '(5 5))` is `nil` and with `nil` as the fifth argument the lines
meet at `(5.0 0.0)`; parallel lines and skew lines in space give `nil`, and two-dimensional points give a two-dimensional
point. A point of the wrong kind is `bad argument type: 2D/3D point: 5` for `(polar 5 0 1)`, and for `inters` and `distance`.

`(cvunit value from to)` converts with a table of the presenter's own: lengths (mm, cm, m, km, inch or in, foot or ft, yard
or yd, mile), angles (degree, radian, grad), areas and volumes written as "square" or "cubic" before a length (`"square foot"`), masses (g,
kg, lb) and times (second, minute, hour, day). `(cvunit 1 "inch" "mm")` is `25.4`, `(cvunit 1 "square foot" "square inch")`
is `144.0`, `(cvunit '(1 2) "inch" "mm")` is `(25.4 50.8)`, and a unit that is unknown or does not match,
`(cvunit 1 "inch" "gallon")`, is `nil`. The table is the presenter's own.

`(trans pt from to [disp])` converts a point or a vector between systems: 0 is the WCS, 1 the UCS, 2 the DCS (the same as the
WCS here, since the presenter's views are in plan), 3 the paper space DCS, an entity name its ECS, or a normal as a list. With
`disp` not nil the origin is left out, so that a vector is converted. A two-dimensional point gives a three-dimensional one.
With the UCS moved to `10,10`:

```text
Command: (trans '(1 2 0) 0 1)
(-9.0 -8.0 0.0)
Command: (trans '(1 2 0) 1 0)
(11.0 12.0 0.0)
Command: (trans '(1 2 0) 1 0 T)
(1.0 2.0 0.0)
```

`(osnap pt modes)` gives the nearest snap of the modes, comma separated and with or without the `_` (`end`, `mid`, `cen`, `nod`,
`qua`, `int`, `ins`, `per`, `tan`, `nea`, `app`, `non`), within the aperture of a typed snap, or `nil`. For a LINE from
`0,0` to `10,0`, `(osnap '(1 0.1) "end")` is `(0.0 0.0 0.0)` and `(osnap '(4 0.1) "mid")` is `(5.0 0.0 0.0)`; a point far
from the line, or a mode that is no mode, gives `nil`.

`(alert text)` shows the text and gives `nil`: the window shows a message box with OK, titled GeometryHelper.Cad and owned by the
window, and a session with no window writes the lines of the text on the command line. `(grtext [box text [hl]])` gives the
text, `nil` with no arguments, and has no effect. `(redraw [ename [mode]])` gives `nil`; `(redraw nil 3)` is `bad argument
type: lentityp nil`.

## Strings, lists and files

Visual LISP's functions for lists, strings, files, the registry and the other open drawings, and the pick-first selection.
Their values, and the text of their errors, were measured in AutoCAD 2021's console, and the examples below are what the
presenter writes for them. Lists, strings, files and the registry exist in every interpreter; only `getfiled`, the
selection functions and the line typed at the command line come from a drawing.

### Lists

`(vl-sort list f)` gives a new list in the order `f` says: `f` is called with two items and answers non-nil when the first
comes before the second. The list given is not changed, and the sort is stable, so items `f` ranks alike keep the order they
had. As in AutoCAD, it also drops an integer or a symbol that repeats the one just before it in the result, and a real zero
that repeats a zero, where every other repeated real, and every string and list, stays:

```text
Command: (vl-sort '(3 1 2 3 1) '<)
(1 2 3)
Command: (vl-sort '(100.0 0.0 200.0 100.0 0.0 200.0) '<)
(0.0 100.0 100.0 200.0 200.0)
Command: (vl-sort '("b" "a" "c" "a") '<)
("a" "a" "b" "c")
Command: (vl-sort '((1 . "a") (2 . "b") (1 . "c") (2 . "d") (1 . "e")) '(lambda (a b) (< (car a) (car b))))
((1 . "a") (1 . "c") (1 . "e") (2 . "b") (2 . "d"))
```

`(vl-sort-i list f)` gives the positions of the items in that order, and drops nothing; of items that rank alike the last
one comes first: `(vl-sort-i '(3 1 2 3 1) '<)` is `(4 1 2 3 0)`, and for `'("b" "a" "c" "a")` it is `(3 1 0 2)`. Items the function
cannot compare are its error: `(vl-sort '(1 "a") '<)` is `; error: bad argument type for compare: "a" 1`. A list that is no
list is `bad argument type: listp 5`, and a function that is none is `bad function: NOSUCH`.

`vl-position` and `vl-remove` compare with the type: an integer with an integer, a real with a real exactly, a string in
its own case, a symbol by identity, a list item by item. So `(vl-position 3.0 '(1 2 3))` is `nil` where `member` takes `1.0`
for `1` and gives `(1 2)` for `(member 1.0 '(1 2))`, as in AutoCAD. `(vl-position 3 '(1 2 3 3))` is `2`, `(vl-remove 1.0 '(1 2
1.0 3))` is `(1 2 3)`, and `(vl-remove "a" '("a" "b" "A"))` is `("b" "A")`.

| Function | Gives | Example |
|---|---|---|
| `(vl-remove-if f list)` | the items `f` answers nil for | `(vl-remove-if 'minusp '(1 -2 3 -4))` is `(1 3)` |
| `(vl-remove-if-not f list)` | the items `f` answers non-nil for | `(vl-remove-if-not 'minusp '(1 -2 3 -4))` is `(-2 -4)` |
| `(vl-member-if f list)` | the tail from the first item `f` accepts, or `nil` | `(vl-member-if 'minusp '(1 -2 3))` is `(-2 3)` |
| `(vl-member-if-not f list)` | the tail from the first item `f` refuses | `(vl-member-if-not 'minusp '(-1 -2 3 4))` is `(3 4)` |
| `(vl-some f list ...)` | the first value of `f` that is not nil | `(vl-some '(lambda (x) (if (> x 1) (* x 10))) '(1 2 3))` is `20` |
| `(vl-every f list ...)` | `T` when `f` is non-nil for every item, else `nil` | `(vl-every 'numberp '(1 a))` is `nil` |
| `(vl-list* a b ... list)` | a list of the items, the last argument as its tail | `(vl-list* 1 2 3)` is `(1 2 . 3)`, `(vl-list* 1 '(2 3))` is `(1 2 3)` |
| `(vl-list-length list)` | the length, `nil` for a dotted list, `0` for `nil` | `(vl-list-length '(1 2 . 3))` is `nil` |

`vl-some` and `vl-every` take several lists and stop at the shortest: `(vl-every '= '(1 2) '(1 2 3))` is `T`. A list that is no
list is `bad argument type: listp 5`, and a dotted one is an error too: `(vl-position 1 '(0 . 1))` is `bad list: 1`.

### Strings and Unicode

Wherever one of these functions takes a string, a symbol stands for its name in capitals: `(vl-string-search "B" 'abc)` is
`1`. Any other value is `bad argument type: (or stringp symbolp): 1`. Positions count from 0.

| Function | Gives | Example |
|---|---|---|
| `(vl-string-search pattern string [start])` | where the pattern is first found from `start`, or `nil` | `(vl-string-search "cd" "abcdcd" 3)` is `4` |
| `(vl-string-subst new old string [start])` | the string with the first `old` from `start` replaced | `(vl-string-subst "X" "b" "abcb" 2)` is `"abcX"` |
| `(vl-string-position code string [start [from-end]])` | the position of the first character with that code at or after `start`, or of the last where `from-end` is not nil | `(vl-string-position 98 "abcb" 0 T)` is `3` |
| `(vl-string-mismatch s1 s2 [p1 [p2 [ignore-case]]])` | the length of the common start of the two from `p1` and `p2` | `(vl-string-mismatch "abcd" "ABcd" 0 0 T)` is `4` |
| `(vl-string-elt string i)` | the code of the character at `i` | `(vl-string-elt "abc" 1)` is `98` |
| `(vl-string-translate source target string)` | the string with the i-th character of `source` made the i-th of `target`, where `target` has one | `(vl-string-translate "abc" "AB" "aabbcc")` is `"AABBcc"` |
| `(vl-string-trim chars string)` | the string without those characters at both ends | `(vl-string-trim " " "  a b  ")` is `"a b"` |
| `vl-string-left-trim`, `vl-string-right-trim` | the same at one end | `(vl-string-left-trim "ab" "abbacab")` is `"cab"` |
| `(vl-string->list string)` | the codes of its characters | `(vl-string->list "abc")` is `(97 98 99)` |
| `(vl-list->string codes)` | the string of those codes | `(vl-list->string '(97 98 99))` is `"abc"` |

A search that starts past the end is `bad argument value: string position out of range 9`, one that starts below 0 is `bad
argument value: non-negative: -1`, and a start equal to the length finds nothing and gives `nil`. `(vl-string-elt "abc" 3)`
is `bad argument value: string position out of range 3`.

Strings are Unicode. `chr` gives the character of any code from 1 to 65535: `(strlen (chr 273))` is `1` and `(ascii (chr
273))` is `273`; a code of 65536 or more, or of 0 or less, gives `""`. (Before this stage `chr` gave `""` for 256 and up.)
`strlen`, `substr`, `ascii`, `vl-string-elt` and `vl-string-search` count UTF-16 units, and `strcase` changes the letters
beyond ASCII too, `(ascii (strcase (chr 273)))` is `272`. The seven characters of `\U+0111` stay seven characters, as in
AutoCAD: `(strlen "\U+0111x")` is `8` and `(ascii "\U+0111")` is `92`, the backslash. Only `vl-string->list` reads them as one
character, `(vl-string->list "\U+0111x")` is `(273 120)`. `vl-list->string` makes characters of the codes from 1 to 65535 and
leaves out the others: `(strlen (vl-list->string '(97 0 98)))` is `2`, and a code that is no integer is `bad char code in
list: 1.5`.

### Files

`(open name mode [encoding])` gives a file value, `nil` for a file that cannot be opened (missing to read, a folder that is
missing, a file in use). The mode is `"r"`, `"w"` or `"a"`, in either case; any other text that starts with one of those
letters gives `nil` (`"rw"`) and any other is `; error: unknown open mode: "x"`. The encoding is `"utf8"` or `"utf8-bom"`, in
either case; any other is the same error with the encoding in it. `"w"` empties a file that exists. A name with no folder is
taken from the process's current folder, the one the presenter was started in, which is not the folder of the drawing.

```text
Command: (setq f (open "C:/work/notes.txt" "w"))
#<file "C:/work/notes.txt">
Command: (type f)
FILE
Command: (write-line "line one" f)
"line one"
Command: (write-line (strcat "caf" (chr 233)) f)
"café"
Command: (write-line (chr 273) f)
"đ"
Command: (close f)
nil
Command: !f
#<FILE internal>
```

A file prints as `#<file "name">`, the name as it was given, and as `#<FILE internal>` once it is closed. Two opens of one
file are two values that are not `eq`, and `(close f)` of a file already closed gives `nil` again.

**What is written.** Every `\n` becomes a carriage return and a line feed, so the three lines above are 25 bytes. A file is
written in the machine's ANSI code page unless it says otherwise, and a character the code page does not have is written as
`\U+` and four hex digits in capitals: the é is one byte, and the đ is the seven characters `\U+0111`. A string that holds
those seven characters is written as those seven in every encoding.

| Opened | Written as |
|---|---|
| `"w"` | ANSI, with `\U+XXXX` for what the code page lacks |
| `"a"` to a new or empty file, or one that is not valid UTF-8 | ANSI |
| `"a"` to a file that has text and is valid UTF-8, plain ASCII included | UTF-8 with no byte order mark: the file stays what it was |
| `"w"` or `"a"` with `"utf8"` | UTF-8 with no byte order mark: `caféđ` and its line end are 9 bytes, and a second `đ` appended makes 13 |
| `"w"` with `"utf8-bom"` | UTF-8 with a byte order mark: `x` and its line end are 6 bytes, and `"a"` after it adds no second mark (9 bytes with `y`) |

**What is read.** A file with a UTF-8 or UTF-16 byte order mark is read as that; one without is read as UTF-8 when the whole
of it is valid UTF-8, and otherwise in the ANSI code page. With `"utf8"` or `"utf8-bom"` a file is read as UTF-8, a byte
order mark is skipped, and each byte that is no valid UTF-8 becomes a `?`. A line ends at a carriage return and line feed, at
a line feed, or at a carriage return alone; the last line needs no end, and a line of 1 000 characters reads whole.

```text
Command: (setq f (open "C:/work/notes.txt" "r"))
#<file "C:/work/notes.txt">
Command: (read-line f)
"line one"
Command: (vl-string->list (read-line f))
(99 97 102 233)
Command: (setq s (read-line f))
"đ"
Command: (list (strlen s) (vl-string->list s))
(7 (273))
Command: (read-line f)
nil
```

`(read-line f)` gives `nil` at the end, again and again. `(read-char f)` gives the code of the next character, `10` for a
line end of any kind, and `nil` at the end. `(write-line string f)` gives the string, `(write-char code f)` the code, and
`princ`, `prin1` and `print` given a file as their last argument write there and nothing on the screen (`print` writes the
line end before and a space after, as it does on the screen). `(write-line "screen")` and `(write-char 66)`, with no file or
with `nil`, write on the screen.

**The line typed.** `(read-line)` and `(read-char)` with no file read what the user types at the command line, as AutoCAD's
console does. The prompt is empty, the line is taken whole with its spaces, and text that starts with `(` is text and not
AutoLISP, with no `Can't reenter LISP.`; the line is echoed on a line of its own. `(read-line)` and then `hello world` gives
`"hello world"`, and `(setq x (read-line))` and then `(1 2)` gives `"(1 2)"`. A line typed is kept for the calls that follow:
`(read-char)` and then `ab` gives `97`, the next `(read-char)` gives `98`, and `(read-line)` then gives `""`, the rest of
the line, with no question. Esc is `Function cancelled`, as at every question. A bare `LispInterpreter` has no command line,
and gives `nil`.

**The errors** depend on what the file was opened for, and on whether it is open. `(read-line 5)` is `bad argument type:
FILE 5` and `(write-line "x" 5)` is `bad argument type: output-streamp: 5`. A file opened `"r"` and written to is `bad
argument type: output-streamp: #<file "C:/work/notes.txt">`; one opened `"w"` and read is the same with `input-streamp`.
After `close`:

| Call on a closed file | Opened `"r"` | Opened `"w"` or `"a"` |
|---|---|---|
| `read-line`, `read-char` | `stream is closed: #<FILE internal>` | `bad argument type: FILE #<FILE internal>` |
| `write-line`, `write-char` | `bad argument type: output-streamp: #<FILE internal>` | `stream is closed: #<FILE internal>` |
| `princ`, `prin1`, `print` | `bad argument type: FILE #<FILE internal>` | `stream is closed: #<FILE internal>` |

`(close nil)` is `bad argument type: streamp nil`, and `(open 5 "r")` and `(open "x.txt" 5)` are `bad argument type: stringp 5`.

A file a routine leaves open stays open, and locked, until the drawing is closed or reloaded, which closes it; a routine
that opens a file should close it.

### Finding and naming files

`(findfile name)` gives the full path of a file or folder, with backslashes, or `nil`. A name with a folder in it, absolute or
like `files/notes.txt`, is checked where it points, a relative one from the current folder. A bare name is looked for in the
current folder, then in the folder of the drawing (none for a drawing that was never saved), then in the support folders in
order, those of Global settings' Folder preference. The first place that has it wins. A bare name of a file of code
(`.lsp`, `.dcl`, `.fas` and seven more) skips the first two places while LEGACYCODESEARCH is 0, its initial value, as in
AutoCAD 2021 ([Trusted folders and SECURELOAD](#trusted-folders-and-secureload)). `(findfile "C:/work/notes.txt")` is
`"C:\\work\\notes.txt"`, and `""`, a name that ends in a separator, and a name nothing has give `nil`. `load` looks in the
support folders and not in the folder of the drawing, as AutoCAD does, so a routine that loads a file beside its drawing
names the folder.

The names are worked out from the text alone, with `\` and `/` both taken as separators:

| Function | `"C:\\a\\b.c.txt"` | `"C:\\a.d\\b"` | `".txt"` | `"b.txt"` |
|---|---|---|---|---|
| `vl-filename-base` | `"b.c"` | `"b"` | `""` | `"b"` |
| `vl-filename-directory` | `"C:\\a"` | `"C:\\a.d"` | `""` | `""` |
| `vl-filename-extension` | `".txt"` | `nil` | `".txt"` | `".txt"` |

A name that ends in a separator has the base `"."` and its folder as the directory: `(vl-filename-directory "C:\\a\\")`
is `"C:\\a"`.

`(vl-filename-mktemp [pattern [dir [ext]]])` makes up a name and creates nothing. The pattern is `"$VL~~"` where none is
given; five letters of its name and a three-digit counter make the name, the extension is `ext` or else the pattern's, and
the folder is `dir` or else the pattern's own or else the temporary folder. The counter starts at 1 in each interpreter, and
a call takes the next number that has no file of that name:

```text
Command: (setq t1 (vl-filename-mktemp "EditText.dcl"))
"C:\\Users\\you\\AppData\\Local\\Temp\\EditT001.dcl"
Command: (vl-filename-base (vl-filename-mktemp "EditText.dcl"))
"EditT002"
Command: (findfile t1)
nil
```

| Function | Gives |
|---|---|
| `(vl-file-size name)` | the size in bytes, `0` for a folder, `nil` for a file that is not there |
| `(vl-file-systime name)` | `(year month weekday day hour minute second millisecond)` in local time, the weekday 0 for Sunday; `nil` for a folder or a missing file |
| `(vl-file-directory-p name)` | `T` for a folder, a trailing separator or not |
| `(vl-file-copy from to [append])` | the bytes copied; `nil` when `to` exists, unless `append` is not nil and the file goes on at its end, when `to` is a folder, or when `from` is missing |
| `(vl-file-rename from to)` | `T`, replacing a file that is already `to`, as AutoCAD 2021 does; `nil` when `from` is missing |
| `(vl-file-delete name)` | `T` for a file or an empty folder; `nil` for one that is missing, or a folder with files in it |
| `(vl-mkdir name)` | `T`, also for a folder that is there; `nil` when its parent is missing |
| `(vl-directory-files [dir [pattern [flag]]])` | the names in `dir` (the current folder where there is none) that match `pattern` (`*` and `?`, case ignored, `*.*` where there is none); `flag` 1 the files, -1 the folders, `nil` or 0 both |

`vl-directory-files` lists `.` and `..` first wherever it lists folders, and the rest by name, case ignored and then
ordinal. For a folder `C:/work/dir` that holds `a.lsp`, `b.txt`, `C.TXT` and a folder `sub1`:

```text
Command: (vl-directory-files "C:/work/dir")
("." ".." "a.lsp" "b.txt" "C.TXT" "sub1")
Command: (vl-directory-files "C:/work/dir" "*.txt")
("b.txt" "C.TXT")
Command: (vl-directory-files "C:/work/dir" nil 1)
("a.lsp" "b.txt" "C.TXT")
Command: (vl-directory-files "C:/work/dir" nil -1)
("." ".." "sub1")
```

A missing folder or no match is `nil`. `(vl-file-copy "C:/work/dir/a.lsp" "C:/work/dir/copy.lsp")` of a file of 9 bytes is
`9`, and the same again is `nil`; with `T` after it the file grows to 18.

`(getfiled title default ext flags)` shows the window's file dialog and gives the path the user chose, or `nil` when the
user cancelled. `title` is the dialog's title, `default` the file or folder it starts at, `ext` the kinds of file offered
without dots (`"dwg;dxf"`, several with a semicolon; `""` for any), and `flags` is a sum: 1 a new file, which makes it a save
dialog that asks before it replaces a file unless 32 is added, 4 any extension, 16 `default` is a folder. A session with no
window gives `nil`, as AutoCAD's console does: `(getfiled "Select a file" "" "txt" 0)` is `nil`. FILEDIA is not read. A host
that draws its own dialog handles the request `"GETFILED"` of [`CadCommandSession.UiRequested`](xref:GeometryHelper.Cad.CadCommandSession),
whose [`CadUiRequestEventArgs.GetFile`](xref:GeometryHelper.Cad.CadUiRequestEventArgs.GetFile) is a
[`CadGetFileRequest`](xref:GeometryHelper.Cad.CadGetFileRequest) with the four arguments, and puts the path in `Answer`; the
window does it through `MainViewModel.AskGetFile`.

### The registry, the blackboard and other drawings

**The registry.** `vl-registry-write`, `-read`, `-delete` and `-descendents` do not touch the Windows registry. They keep
their keys and values in a store of the presenter's own, the file `lisp-registry.txt` in the folder of the settings file
(`%AppData%\GeometryHelper\Drawing`), which every open drawing reads before it looks and writes after it changes, so they share
what is in it. A host sets [`LispInterpreter.RegistryFile`](xref:GeometryHelper.Cad.Lisp.LispInterpreter.RegistryFile) to
pick the file; left null, as in a bare interpreter, the values live in memory for that interpreter alone. The file is UTF-8
text, a line for each value, with the key, the name, `S` or `I` and the value separated by tabs.

A routine that reads Windows' own keys, or AutoCAD's, gets `nil`: they are not in the store. What a routine writes first and
then reads back works as it does in AutoCAD:

```text
Command: (vl-registry-write "HKEY_CURRENT_USER\\Software\\Demo" "Name" "text")
"text"
Command: (vl-registry-write "HKEY_CURRENT_USER\\Software\\Demo" "Count" 42)
42
Command: (vl-registry-write "HKEY_CURRENT_USER\\Software\\Demo\\Sub" "" "def")
"def"
Command: (vl-registry-read "HKEY_CURRENT_USER\\Software\\Demo" "Count")
42
Command: (vl-registry-read "HKEY_CURRENT_USER\\Software\\Demo\\Sub")
"def"
Command: (vl-registry-descendents "HKEY_CURRENT_USER\\Software\\Demo")
("Sub")
Command: (vl-registry-descendents "HKEY_CURRENT_USER\\Software\\Demo" T)
("Count" "Name")
Command: (vl-registry-delete "HKEY_CURRENT_USER\\Software\\Demo")
nil
Command: (vl-registry-delete "HKEY_CURRENT_USER\\Software\\Demo\\Sub")
T
Command: (vl-registry-delete "HKEY_CURRENT_USER\\Software\\Demo")
T
```

The key starts with `HKEY_CURRENT_USER`, `HKEY_LOCAL_MACHINE`, `HKEY_CLASSES_ROOT`, `HKEY_USERS` or `HKEY_CURRENT_CONFIG`, in
capitals; `"HKCU\\Software\\Demo"` is `; error: bad registry key name: "HKCU\\Software\\Demo"`. The names below the root
are compared in any case, as Windows does. A value is a string or an integer: `(vl-registry-write key "Ratio" 1.5)` is
`bad argument value: unsupported registry value: "Ratio"`. With no name, or `""`, `write` and `read` use the key's default value.
`descendents` gives the names of the sub-keys, or with a second argument of any kind the names of the values, the values newest
first and the sub-keys in descending order of name. `delete` of a value is `T`; of a key that has sub-keys it is `nil` and removes
nothing; of a key without them, or a key that is not there, it is `T`.

**The blackboard.** `(vl-bb-set 'cutoff 5)` stores a value under a symbol or a string and gives it back, and `(vl-bb-ref
'cutoff)` reads it, `nil` for a name that was never set. The blackboard is one for the whole process, and no drawing's
variable: `(boundp 'cutoff)` stays `nil` after the `vl-bb-set`. What another drawing reads is a copy: symbols again by name,
lists deeply, a function the routine defined rebuilt from its parameters and body, and numbers, strings, entity names,
selection sets and files as they are.

**Other drawings.** Each drawing has its own variables (see [below](#each-drawing-its-own-variables)); two functions reach
across. `(vl-propagate 'ghd)` sets `ghd` in every other open drawing to the value it has here, and in every drawing opened
afterwards, and gives that value; `(vl-propagate 'nosuchvar)` is `nil` and sends nothing. `(vl-load-all "file.lsp")` loads
the file here as `load` does, and in every other drawing. The other drawings take it when they next run AutoLISP, and the
one opened later at its first form; AutoCAD reaches them at once, which a routine can see only by switching drawings.
`(vl-doc-set 'ghd2 9)` sets a variable in the drawing and `(vl-doc-ref "ghd")` reads one, a symbol or a string, which is all
a drawing's own namespace needs, since the presenter has no other kind.

**VLX, VBA and ObjectARX.** There are no VLX applications, and the functions about them answer as AutoCAD does with none
loaded: `vl-doc-export` gives `T` and remembers the name, `vl-list-exported-functions` lists the names in capitals (`("FOO")`
after `(vl-doc-export 'foo)`), `vl-doc-import` and `vl-unload-vlx` are `; error: LISP Application is not found foo`,
and `vl-vlx-loaded-p`, `vl-list-loaded-vlx` and `vl-get-resource` give `nil`. `.fas` and `.vlx` files are still refused by
`load`. `vl-exit-with-error` and `vl-exit-with-value`, which belong to those applications, are not defined. What belongs to
AutoCAD alone is refused, not ignored: `vl-vbaload` and `vl-vbarun` write `; error: vl-vbarun: VBA is not supported by
GeometryHelper.Cad`, and `vl-arx-import`, `vl-acad-defun` and `vl-acad-undefun` write `; error: vl-arx-import: ObjectARX is
not supported by GeometryHelper.Cad`, each with its own name.

### Pick-first and nentselp

The pick-first selection is the set of objects selected while no command runs. `(sssetfirst gripset [pickset])` sets it, as
AutoCAD 2021 does: the first argument, the objects with grips, is ignored, and the value is a list of two, `nil` and the
selection set. `(sssetfirst nil (ssadd e))` gives `(nil <Selection set: 6>)`, `(sssetfirst nil nil)` gives `(nil nil)`, `(sssetfirst
nil)` gives `(nil)`, and `(sssetfirst 5)` is `bad argument type: lselsetp 5`. `(ssgetfirst)` gives `(nil set)` of the objects
that are selected now, a new set each time over the same objects, or `(nil nil)` where none is. A selection made by `sssetfirst` keeps the order of the set it was given, as AutoCAD 2021 does (a set of the entity at 150 and then the one at 0 gives them as 150, 0), and the order of the drawing once the selection is changed otherwise. A command started after
`sssetfirst` takes the set as if it had been selected before the command, while PICKFIRST is not 0: with a line from 0,0
to 10,0 selected, `(sssetfirst nil (ssadd e))` and then `(command "ERASE")` writes `1 found` and erases it.

`(nentselp [msg] [pt])` is `nentsel` that can take its point from the routine. With a point, `(nentselp '(5 0))` picks
there with no question and gives `(<Entity name: ...> (5.0 0.0 0.0))` for that line, and `(nentselp '(50 50))` gives `nil`
where nothing is; the message and the point may come in either order. With no point it asks as `nentsel` does, `Select
object: ` or the message, and Enter gives `nil`; for an object inside a block reference it gives `nentsel`'s longer form. A DIMENSION
gives it too, the primitive of the dimension's block under the point and the dimension as the container, with the identity for a
matrix, as AutoCAD 2021 does: a DIMLINEAR from (100, 80) to (130, 80) with its line through (115, 95) gives `(4 "LINE" ("DIMENSION"))`
by `(nentselp '(115 95 0))`, at (115 96 0) and on an extension line at (100 87 0). The text of the dimension is picked only where
no other primitive is within the aperture (ours; a pick on the text itself was not measured).
One case differs from AutoCAD: a point exactly on the insertion corner of a TEXT picks the text, where AutoCAD 2021 gave
`nil` in the one measurement made. The pick is the selection engine's, shared with `entsel`.

## ActiveX objects

Visual LISP's ActiveX functions: the `vlax-` names, the `vla-` names that read and write the members of an object, and the
`vlax-curve-` functions. In AutoCAD they reach the drawing through COM; here they reach the presenter's own objects, so a
routine reads and changes the drawing the commands change, and nothing outside the process is started. What an object
can do, the values it gives, and the text of its errors were measured in two ways. Five probe files of 469 forms were
typed into AutoCAD 2021's console, which has no object model (`(vlax-get-acad-object)` is `nil` there, and every object
function of it says `bad argument type: VLA-OBJECT nil`), so they measured the variants and safearrays, the constants, the
curve functions, and the errors for a value that is no object. The objects themselves were measured in two runs of the full
AutoCAD 2021, 523 forms each written with its value to a file. The 668 tests of the stage say beside each expected value
whether AutoCAD gave it, the ActiveX reference gives it, or it is the presenter's own choice.

There are 143 `vla-` functions (84 `vla-get-`, 42 `vla-put-` and 17 for methods), 56 `vlax-` functions (18 of them
`vlax-curve-`), the 11 `vlax-vb` constants and the two old names `variant-value` and `safearray-value`. Everything is
headless: a routine that runs here in a session with no window reads the same objects.

### vl-load-com and what exists before it

AutoCAD 2021 has one ActiveX name before `(vl-load-com)`, `vlax-for`, and the presenter has the same one: `(type
vlax-make-variant)` is `nil` and `(type vlax-for)` is `SUBR`. The symbols `:vlax-true` and `:vlax-false` evaluate to
themselves always. `(vl-load-com)` defines the rest, once for each drawing (a second call does nothing more) and writes
nothing. The constants it sets are ordinary symbols: `(setq vlax-vbDouble 99)` changes one. So are the 898 constants of the
enumerations of the ActiveX library, `acSelectionSetAll` (5), `acActiveViewport`, `acPaperSpace`, `acAttachmentPointTopLeft`,
`acAlignmentCenter`, `acTrue` (1), `acFalse` (0), `acRed` and the rest, bound by the first `(vl-load-com)`; a symbol a
routine gave a value before it keeps that value. The functions it defines print in the case AutoCAD registers them in, also
for a symbol read before the load: `(vl-prin1-to-string '(vla-get-Name vlax-get-acad-object vlax-for vlax-invoke foo))` is
`"(vla-get-Name vlax-get-acad-object VLAX-FOR vlax-invoke FOO)"`, `vlax-for` staying in capitals because it exists before the
load, and `vlax-curve-getEndParam`, `vlax-map-Collection` and `vlax-vbDouble` carrying their capitals. `textbox`, which belongs to the
drawing stage and is described [below](#text-boxes), exists before it, as it does in AutoCAD.

```text
Command: (type vlax-make-variant)
nil
Command: (type vlax-for)
SUBR
Command: !:vlax-true
:vlax-true
Command: (vl-load-com)
Command: (type vlax-make-variant)
SUBR
```

### Variants and safearrays

A variant is a value with a type, a safearray an array with bounds; the ActiveX functions take and give a point as a variant
that holds a safearray of three doubles. `(type v)` of either is the symbol `VARIANT` or `SAFEARRAY`, printed in lower case
as AutoCAD prints it, and a value prints as `#<variant 3 5>` (the type number, then the value), as `#<variant 8197 ...>` for
an array and `#<safearray...>` for a safearray. `equal` compares type and value, `eq` the value itself. The types are
numbers, and `vlax-vbArray`, 8192, is added to the type of an element: 8197 is an array of doubles.

| Constant | Type | Constant | Type |
|---|---|---|---|
| `vlax-vbEmpty` | 0 | `vlax-vbString` | 8 |
| `vlax-vbNull` | 1 | `vlax-vbObject` | 9 |
| `vlax-vbInteger` | 2 | `vlax-vbBoolean` | 11 |
| `vlax-vbLong` | 3 | `vlax-vbVariant` | 12 |
| `vlax-vbSingle` | 4 | `vlax-vbArray` | 8192 |
| `vlax-vbDouble` | 5 | | |

`(vlax-make-variant [value [type]])` gives a variant of the value's own type, an integer 3, a real 5, a string 8, `nil` 0 and
`T` or `:vlax-true` 11 (`:vlax-false` is 11 too, with 0), and an array 8192 plus the type of its elements; given a type it
converts the value to it. A symbol, an entity name or a list is `lisp value has no coercion to VARIANT with this type:`
and the value, with two spaces after the colon. `(vlax-variant-change-type v type)` converts: a real to 2 or 3 rounds half to
even, so 2.5 is 2 and 3.5 is 4; a string is read as a number; a number to 8 is its text, with the exponent as AutoCAD
writes it; a string that is no number, and an array to a scalar type, give `nil`. `vlax-variant-type` is the type and
`vlax-variant-value` the value as LISP: integers and reals, strings, `:vlax-true` and `:vlax-false` for a Boolean, `nil` for
an empty variant, and the safearray for an array.

```text
Command: (setq p (vlax-3d-point 1 2 3))
#<variant 8197 ...>
Command: (type p)
variant
Command: (vlax-variant-type p)
8197
Command: (vlax-safearray->list (vlax-variant-value p))
(1.0 2.0 3.0)
Command: (vlax-safearray->list (vlax-variant-value (vlax-3d-point '(4 5))))
(4.0 5.0 0.0)
Command: (vlax-make-variant 5)
#<variant 3 5>
Command: (vlax-make-variant 1.5)
#<variant 5 1.5>
Command: (vlax-make-variant "abc")
#<variant 8 abc>
Command: (vlax-make-variant nil)
#<variant 0 >
Command: (vlax-variant-value (vlax-make-variant T))
:vlax-true
Command: (vlax-variant-value (vlax-make-variant 7 5))
7.0
Command: (vlax-variant-value (vlax-variant-change-type (vlax-make-variant 7) 8))
"7"
Command: (vlax-variant-value (vlax-variant-change-type (vlax-make-variant 2.5) vlax-vbInteger))
2
Command: (vlax-variant-value (vlax-variant-change-type (vlax-make-variant 3.5) vlax-vbLong))
4
Command: (vlax-variant-change-type (vlax-make-variant "abc") vlax-vbInteger)
nil
Command: (vlax-variant-value (vlax-variant-change-type (vlax-make-variant 1e20) vlax-vbString))
"1E+20"
Command: (vlax-make-variant 'abc)
; error: lisp value has no coercion to VARIANT with this type:  ABC
Command: (vlax-variant-type 5)
; error: bad argument type: variantp 5
Command: (equal (vlax-3d-point 1 2 3) (vlax-3d-point 1 2 3))
T
Command: (princ (vlax-make-variant "a b"))
#<variant 8 a b>#<variant 8 a b>
```

`(vlax-3d-point x y [z])`, or a list of two or three numbers, is a variant of three doubles, with Z 0.0 when it is left out.
`(vlax-3d-point 1)` is `bad 3D point: 1` and `'(1 2 3 4)` is `bad 3D point list: (1 2 3 4)`. `(vlax-tmatrix list)` takes four
lists of four numbers and gives a variant of a 4 by 4 array of doubles; anything else is `bad transform matrix list:` and
the list.

`(vlax-make-safearray type '(lower . upper) ...)` makes an array of that element type, with one pair of bounds for each
dimension. Its elements start as 0.0 for doubles, 0 for the integer types, `""` for strings, `:vlax-false` for Booleans, an
empty variant for variants and `nil` for objects. `(vlax-safearray-fill a list)` takes exactly one value for each element,
nested by dimension, and converts each to the element type: a 1 given to an array of strings is `"1"`, a 1.6 given to
integers is 2, a `"a"` given to doubles is the coercion error above. `vlax-safearray->list` gives the elements, nested by
dimension. `vlax-safearray-get-element` and `-put-element` take one index for each dimension; `-get-dim`, `-get-l-bound`,
`-get-u-bound` (the dimension counts from 1) and `-type` tell its shape. `variant-value` and `safearray-value` are the old
names of `vlax-variant-value` and `vlax-safearray->list`.

```text
Command: (setq a (vlax-make-safearray vlax-vbDouble '(0 . 2)))
#<safearray...>
Command: (vlax-safearray-fill a '(1 2 3))
#<safearray...>
Command: (vlax-safearray-put-element a 1 9.5)
9.5
Command: (vlax-safearray->list a)
(1.0 9.5 3.0)
Command: (vlax-safearray-get-u-bound a 1)
2
Command: (vlax-safearray-type a)
5
Command: (vlax-safearray-get-element a 5)
; error: ActiveX Server returned an error: Invalid index
Command: (vlax-safearray-fill a '(1 2))
; error: vlax-safearray-fill failed.  Invalid initialization list. #<safearray...> (1 2)
Command: (setq g (vlax-make-safearray vlax-vbDouble '(1 . 2) '(0 . 2)))
#<safearray...>
Command: (vlax-safearray-put-element g 2 1 7)
7
Command: (vlax-safearray->list g)
((0.0 0.0 0.0) (0.0 7.0 0.0))
Command: (vlax-safearray-get-element g 2)
; error: safearray-get-element failed. Wrong count of indexes: 1
Command: (vlax-make-safearray vlax-vbDouble '(2 . 1))
; error: Upper bound in SAFEARRAYBOUND occurred to be less than lower.
Command: (vlax-make-safearray vlax-vbDouble 5)
; error: Array specification (bounds) is not a dotted pair.
Command: (vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 1)) '(1 "a")))
(#<variant 3 1> #<variant 8 a>)
Command: (vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbString '(0 . 1)) '("a" 1)))
("a" "1")
Command: (vlax-safearray->list (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '("a" 1)))
; error: lisp value has no coercion to VARIANT with this type:  "a"
Command: (vlax-3d-point 1)
; error: bad 3D point: 1
Command: (vlax-tmatrix '((1 0 0 5) (0 1 0 0) (0 0 1 0) (0 0 0 1)))
#<variant 8197 ...>
Command: (vlax-safearray->list (vlax-variant-value (vlax-tmatrix '((1 0 0 5) (0 1 0 0) (0 0 1 0) (0 0 0 1)))))
((1.0 0.0 0.0 5.0) (0.0 1.0 0.0 0.0) (0.0 0.0 1.0 0.0) (0.0 0.0 0.0 1.0))
```

Every line above is what AutoCAD 2021's console wrote for the same form.

### The curve functions

The eighteen `vlax-curve-` functions take an entity name or the VLA-OBJECT of an entity: `getStartParam`, `getEndParam`,
`getStartPoint`, `getEndPoint`, `getPointAtParam`, `getParamAtPoint`, `getDistAtParam`, `getParamAtDist`, `getDistAtPoint`,
`getPointAtDist`, `getClosestPointTo`, `getClosestPointToProjection`, `getFirstDeriv`, `getSecondDeriv`, `getArea`,
`isClosed`, `isPeriodic` and `isPlanar`. A curve is a LINE, ARC, CIRCLE, ELLIPSE, LWPOLYLINE, POLYLINE (two- and
three-dimensional, not a mesh), SPLINE, XLINE or RAY. Any other entity, a TEXT or a POINT or an INSERT, or an erased one, is
`bad argument value: AcDbCurve 261`. The number is AutoCAD's own counter there (48 to 51 in the console's runs) and the
decimal value of the entity's handle here. A value that is no entity is `unable to get ObjectID:` and the value. Points
are in the WCS, in and out, whatever the UCS; a two-dimensional point takes a Z of 0.

The parameter is where the curves differ:

| Curve | The parameter runs |
|---|---|
| LINE | from 0 to its length: the parameter is the distance |
| CIRCLE | from 0 to 2π, and it is periodic: the parameter 7.0 on a circle of radius 10 is the point at angle 7.0, at distance 70.0 |
| ARC | from its start angle to its end angle in absolute radians: an arc from 90 to 180 degrees runs from 1.5708 to 3.14159 |
| ELLIPSE | the ellipse's own parameter (groups 41 and 42): an arc from 45 to 135 degrees of an ellipse with semi-axes 10 and 4 runs from 1.19029 to 1.9513 |
| LWPOLYLINE, POLYLINE | the number of the vertex, linear in the length on a straight segment and in the angle on a bulge, so 1.5 on a bulged segment is its middle; a closed one ends at the number of its vertices |
| SPLINE | its knot parameter: a fit spline through four points a few units apart runs to 42.4264 and is 44.2385 long |
| XLINE | no start and no end (`nil`), and the parameter is the distance from its base point: the parameter 2 on the line through 0,0 and 10,10 is `(1.41421 1.41421 0.0)` |
| RAY | from 0, with no end |

A parameter or distance past an end gives `nil`, except `getDistAtParam` of a LINE, which gives the parameter it was given
(-0.5 for -0.5). `getParamAtPoint` and `getDistAtPoint` give `nil` for a point that is off the curve: AutoCAD 2021 already
did for a point 0.0001 off, and the presenter takes a point as on the curve when it is within 1E-9 times one more than the
curve's size. `getClosestPointTo pt [extend]` extends a line, an arc and the ends of an open polyline when `extend` is
true. `getClosestPointToProjection pt normal [extend]` projects the point along `normal` first. `getFirstDeriv` and
`getSecondDeriv` are per unit of the parameter of the segment, and at a vertex the next segment's. `getArea` is the area
the curve and its chord close: 50.0 for an open L of two segments of 10, 7.13495 for a quarter circle of radius 5, 0.0 for
a line, and the width of a polyline does not count.

```text
Command: (vlax-curve-getEndParam ci)
6.28319
Command: (vlax-curve-getDistAtParam ci pi)
15.708
Command: (vlax-curve-getPointAtParam ci (/ pi 2))
(20.0 5.0 0.0)
Command: (vlax-curve-getParamAtPoint ci '(15 0 0))
3.14159
Command: (vlax-curve-getClosestPointTo ci '(30 0 0))
(25.0 0.0 0.0)
Command: (vlax-curve-getArea ci)
78.5398
Command: (vlax-curve-isPeriodic ci)
T
Command: (vlax-curve-getFirstDeriv ci 0)
(0.0 5.0 0.0)
Command: (vlax-curve-getSecondDeriv ci 0)
(-5.0 0.0 0.0)
Command: (vlax-curve-getEndParam ar)
1.5708
Command: (vlax-curve-getArea ar)
7.13495
Command: (vlax-curve-getEndParam pl)
2.0
Command: (vlax-curve-getPointAtParam pl 1.5)
(10.0 25.0 0.0)
Command: (vlax-curve-getParamAtDist pl 15)
1.5
Command: (vlax-curve-getClosestPointTo pl '(12 35 0) T)
(10.0 30.0 0.0)
Command: (vlax-curve-getArea pl)
50.0
Command: (vlax-curve-getEndParam cp)
3.0
Command: (vlax-curve-getDistAtParam cp 3)
34.1421
Command: (vlax-curve-getDistAtParam cp 3.5)
nil
Command: (vlax-curve-getParamAtPoint l1 '(5 1 0))
nil
Command: (vlax-curve-getClosestPointTo l1 '(15 1 0))
(10.0 0.0 0.0)
Command: (vlax-curve-getClosestPointTo l1 '(15 1 0) T)
(15.0 0.0 0.0)
Command: (vlax-curve-getClosestPointToProjection l1 '(5 1 7) '(0 0 1))
(5.0 0.0 0.0)
Command: (vlax-curve-getStartParam el)
1.19029
Command: (vlax-curve-getEndParam el)
1.9513
Command: (vlax-curve-getEndParam sp)
42.4264
Command: (vlax-curve-getPointAtParam xl 2)
(1.41421 1.41421 0.0)
Command: (vlax-curve-getStartPoint tx)
; error: bad argument value: AcDbCurve 261
Command: (vlax-curve-getStartPoint 5)
; error: unable to get ObjectID: 5
Command: (vlax-curve-getPointAtParam l1 "a")
; error: bad argument type: numberp: "a"
Command: (vlax-curve-getPointAtParam l1)
; error: too few arguments
Command: (vlax-curve-getClosestPointTo l1 5)
; error: bad argument type: 2D/3D point: 5
```

The drawing of these examples has a line `l1` from 0,0 to 10,0, a circle `ci` of radius 5 at 20,0, an arc `ar` of radius 5 at
40,0 from 0 to 90 degrees, an open polyline `pl` through 0,20, 10,20 and 10,30, a closed one `cp` through 20,20, 30,20 and
30,30, a text `tx`, an elliptical arc `el`, a spline `sp` and an xline `xl`. Each value above is what AutoCAD 2021's console
gave for the same shape in its probes, except the number after `AcDbCurve`.

### The object model

A VLA-OBJECT stands for an object of the drawing: `(type o)` is `VLA-OBJECT`, and it prints as `#<VLA-OBJECT IAcadLine
000001e4a7f3c720>`, the name of its interface and an address that stands for nothing. These examples use one drawing: the
layers Off (green, switched off) and Locked (red, locked) beside layer 0, a block `Tag` of an attribute definition `TAG1`
(prompt `Prompt`, default `dflt`), a constant one `CTAG` and a line, a block `Dot` of a circle, and in model space the lines
`l1`, `l2` and `l3` at 0, 10 and 12, a line `lk` on Locked at 14, a text `tx` `abc` at 0,20, an MTEXT `mt`, a circle `ci`
of radius 5 at 50,0, an arc `ar` of radius 5 at 70,0 from 90 to 180 degrees, a polyline `pl` through 0,40, 10,40 and 10,50,
and the references `bi` of `Tag`, at 100,0 at scale 2 with the attribute value `v30`, and `bi2` of `Dot` at 120,0. Each of
those names holds the entity name of its object. What AutoCAD 2021 gave for the same forms is what is written here, except
where the [list at the end](#what-is-parked-in-the-activex-stage) says otherwise.

#### Reaching the objects

`(vlax-get-acad-object)` is the application and its `ActiveDocument` the document of the session; `vlax-ename->vla-object`
gives the object of an entity name, and `vlax-vla-object->ename` the name of an entity's object back.

```text
Command: (setq app (vlax-get-acad-object))
#<VLA-OBJECT IAcadApplication 000001e4a7f3c060>
Command: (setq doc (vla-get-ActiveDocument app))
#<VLA-OBJECT IAcadDocument 000001e4a7f3c0c0>
Command: (vla-get-ActiveSpace doc)
1
Command: (setq ms (vla-get-ModelSpace doc))
#<VLA-OBJECT IAcadModelSpace 000001e4a7f3c120>
Command: (vla-get-Name ms)
"*Model_Space"
Command: (vla-get-Count ms)
11
Command: (setq o (vlax-ename->vla-object l1))
#<VLA-OBJECT IAcadLine 000001e4a7f3c180>
Command: (vla-get-ObjectName o)
"AcDbLine"
Command: (equal (vlax-vla-object->ename o) l1)
T
Command: (vlax-ename->vla-object 5)
; error: bad argument type: lentityp 5
Command: (vla-get-Name 5)
; error: bad argument type: VLA-OBJECT 5
```

The document's `ActiveSpace` is 1 in model space and 0 in a layout, and `PaperSpace` is the block of the current layout.
Its `Name` is the file name with its extension, `FullName` the path with the name, and `Path` the folder with no
separator at its end. Setting `ActiveLayer` to a layer's object makes it the current layer, as SETVAR of CLAYER does.

#### Collections

`Blocks`, `Layers`, `TextStyles`, `Linetypes` and `DimStyles` of the document are collections, and so is `SelectionSets`. A
collection has `Count`, and `Item` takes an index from 0, as an integer, a real or a variant, or a name in any case. An
index out of range is `Automation Error. Description was not provided.` and a name that is not there `Automation Error. Key
not found`. `vlax-for` and `vlax-map-collection` go through the items in order: the blocks in the order of the block table,
`*Model_Space` and `*Paper_Space` first and then the definitions as they were made, the layers and the other tables in the
order of their table. A model space or a block definition is a collection of its entities too, in the order `entnext` walks
them, with the attribute definitions of a block among them.

```text
Command: (setq blks (vla-get-Blocks doc))
#<VLA-OBJECT IAcadBlocks 000001e4a7f3c1e0>
Command: (vla-get-Count blks)
4
Command: (progn (vlax-for b blks (princ (vla-get-Name b)) (princ " ")) (princ))
*Model_Space *Paper_Space Tag Dot 
Command: (vla-get-IsLayout (vla-Item blks 0))
:vlax-true
Command: (vla-get-Name (vla-Item blks "tag"))
"Tag"
Command: (vla-Item blks "NoSuch")
; error: Automation Error. Key not found
Command: (vla-Item blks 99)
; error: Automation Error. Description was not provided.
Command: (setq lays (vla-get-Layers doc))
#<VLA-OBJECT IAcadLayers 000001e4a7f3c480>
Command: (progn (vlax-for l lays (princ (vla-get-Name l)) (princ " ")) (princ))
0 Off Locked 
Command: (vla-get-Color (vla-Item lays "Off"))
3
Command: (vla-get-LayerOn (vla-Item lays "Off"))
:vlax-false
Command: (vla-get-Lock (vla-Item lays "Locked"))
:vlax-true
Command: (vla-get-ObjectName (vla-Item lays "0"))
"AcDbLayerTableRecord"
Command: (vla-get-ObjectName lays)
"AcDbLayerTable"
Command: (vla-get-Name lays)
; error: ActiveX Server returned the error: unknown name: Name
Command: (progn (vlax-for e (vla-Item blks "Tag") (princ (vla-get-ObjectName e)) (princ " ")) (princ))
AcDbAttributeDefinition AcDbAttributeDefinition AcDbLine 
Command: (vlax-for x o (princ x))
; error: bad argument type: VLA-object collection: #<VLA-OBJECT IAcadLine 000001e4a7f3c180>
Command: (vlax-map-collection o 'princ)
; error: not a collection: #<VLA-OBJECT IAcadLine 000001e4a7f3c180>
```

#### The members

A function is defined for each member a routine can use, and it is named as the reference writes the member: `vla-get-Color`,
`vla-put-Color`, `vla-GetBoundingBox`. The name space is shared, so `vla-get-Name` works on any object that has a `Name`
and an object that has none gives `ActiveX Server returned the error: unknown name: Name`, with the name as the function or
the argument gives it (`Name` for `vla-get-Name`, `NAME` for `(vlax-get-property o 'name)`, a string as it is written). The
older `vlax-get-property`, `vlax-put-property` and `vlax-invoke-method`, and the oldest `vlax-get`, `vlax-put` and
`vlax-invoke`, take the member as a symbol or a string in any case. `vlax-property-available-p`,
`vlax-method-applicable-p` and `vlax-typeinfo-available-p` ask whether an object has a member. Too many arguments to a
`vla-` function is `Too many actual parameters`, too few `too few actual parameters`.

| Object | Properties | Methods |
|---|---|---|
| application | `ActiveDocument`, `Application` | |
| document | `Application`, `ActiveSpace`, `ActiveLayer` (writable), `ModelSpace`, `PaperSpace`, `Blocks`, `Layers`, `TextStyles`, `Linetypes`, `DimStyles`, `SelectionSets`, `ActiveSelectionSet`, `PickfirstSelectionSet`, `Name`, `FullName`, `Path` | `StartUndoMark`, `EndUndoMark` |
| a collection | `Count`, `Application`, `ObjectName` and `Handle` of a table | `Item`; `Add` of `SelectionSets` |
| a selection set | `Name`, `Count`, `Application` | `Item`, `Clear`, `Delete`, `AddItems`, `RemoveItems`, `Select`, `SelectAtPoint`, `Highlight` |
| a block, model space, a layout | `Name`, `Count`, `IsLayout`, `IsXRef`, `IsDynamicBlock`, `Origin`, and what every object has | `Item` |
| every record, block and entity | `Application`, `Document`, `Handle`, `ObjectName`, `ObjectID`, `OwnerID` | |
| a layer | `Name`, `Color`, `TrueColor`, `Linetype`, `Lineweight`, `LayerOn`, `Freeze`, `Lock`, `Plottable`, `Description` | |
| a text style, a linetype, a dimension style | `Name`; `FontFile`, `Height`, `Width`, `ObliqueAngle` of a text style; `Description` of a linetype | |
| a colour | `ColorIndex`, `ColorMethod` and `EntityColor` (writable), `Red`, `Green`, `Blue` | `SetRGB` |
| every entity | `Layer`, `Color`, `Linetype`, `LinetypeScale`, `Lineweight`, `Visible`, `TrueColor` (all writable) | `Delete`, `GetBoundingBox`, `Update`, `Highlight` |
| a line | `StartPoint`, `EndPoint`, `Thickness` (writable), `Length`, `Angle`, `Delta` | |
| a circle | `Center`, `Radius`, `Diameter`, `Circumference`, `Area` (all writable) | |
| an arc | `Center`, `Radius`, `StartAngle`, `EndAngle` (writable), `StartPoint`, `EndPoint`, `ArcLength`, `TotalAngle`, `Area` | |
| a light-weight polyline | `Coordinates`, `Closed`, `Elevation` (writable), `Length`, `Area` | |
| a text | `TextString`, `Height`, `Rotation`, `StyleName`, `InsertionPoint`, `TextAlignmentPoint`, `Alignment`, `ScaleFactor`, `ObliqueAngle` (all writable) | `FieldCode` |
| an MText | `TextString`, `Height`, `Rotation`, `StyleName`, `InsertionPoint`, `Width`, `AttachmentPoint` (all writable) | `FieldCode` |
| a block reference | `Name`, `InsertionPoint`, `Rotation`, `XScaleFactor`, `YScaleFactor`, `ZScaleFactor` (all writable), `EffectiveName`, `HasAttributes`, `IsDynamicBlock` | `GetAttributes`, `GetConstantAttributes` |
| an attribute of a reference | `TagString`, `TextString`, `Height`, `Rotation`, `InsertionPoint`, `StyleName`, `Invisible` (all writable), `Constant` | |
| an attribute definition | the same, and `PromptString` (writable) | |

The properties of a layer, a text style, a linetype and a dimension style are read only. `vla-put-Color` of a layer is
`vla-put-Color: changing a table record is not supported by GeometryHelper.Cad yet`, and the setters that only a layer has
(`LayerOn`, `Freeze`, `Lock`) are not defined; the layer commands change them. A member the table does not list is not
defined as a `vla-` function. `vlax-dump-object` is not there either.

```text
Command: (setq ly (vla-Item lays "Off"))
#<VLA-OBJECT IAcadLayer 000001e4a7f3c180>
Command: (vla-put-Color ly 2)
; error: vla-put-Color: changing a table record is not supported by GeometryHelper.Cad yet
Command: (vla-put-LayerOn ly :vlax-true)
; error: no function definition: VLA-PUT-LAYERON
Command: (vla-get-Description (vla-Item (vla-get-Linetypes doc) "Continuous"))
"Solid line"
```

#### Reading and writing

A property gives the value of its type: a Double a real, an integer property and an enumeration an integer, a String a string,
a Boolean `:vlax-true` or `:vlax-false`, a point or an array a variant, and an object a VLA-OBJECT. `GetAttributes` gives a
variant of type 8201, an array of objects. The old `vlax-get` and `vlax-invoke` give a variant or a safearray as a list and
a Boolean as -1 or 0.

```text
Command: (vla-get-Length o)
10.0
Command: (vla-get-StartPoint o)
#<variant 8197 ...>
Command: (vlax-safearray->list (vlax-variant-value (vla-get-StartPoint o)))
(0.0 0.0 0.0)
Command: (vlax-get o 'StartPoint)
(0.0 0.0 0.0)
Command: (vla-get-Visible o)
:vlax-true
Command: (vlax-get o 'Visible)
-1
Command: (vlax-get-property o "visible")
:vlax-true
Command: (vla-get-Name o)
; error: ActiveX Server returned the error: unknown name: Name
```

What a property takes depends on its type. A Double takes an integer or a real. An integer property takes a real or a
number in a string too and converts it as `vlax-variant-change-type` does, so `(vla-put-Color o 1.5)` sets 2, and a value
out of range is `Automation Error. Invalid argument color in IAcadEntity::put_Color`. A Boolean takes `:vlax-true` or `T` or
-1 for true and `:vlax-false` or 0 for false, and `nil` is `ActiveX Server returned an error: Parameter not optional`. A
point takes a variant or a safearray of three doubles; a list is `lisp value has no coercion to VARIANT with this type:` and
the list, and a safearray of two numbers is `Automation Error. Incorrect number of elements in SafeArray`. `vlax-put` of a
point is `Exception occurred` as it is in AutoCAD 2021, while `vlax-put` of a number or a Boolean works. A read-only
property written is `ActiveX Server returned an error: Type mismatch`. A layer, linetype, text style or block that is not
in the drawing is `Automation Error. Key not found`, where `entmod` would make the layer, and a layer is found by its name
in any case.

```text
Command: (vla-put-Color o 1)
nil
Command: (cdr (assoc 62 (entget l1)))
1
Command: (vla-put-Color o 256)
nil
Command: (assoc 62 (entget l1))
nil
Command: (vla-put-Color o 1.5)
nil
Command: (vla-get-Color o)
2
Command: (vla-put-Color o 300)
; error: Automation Error. Invalid argument color in IAcadEntity::put_Color
Command: (vla-put-Layer o "NoSuch")
; error: Automation Error. Key not found
Command: (vla-put-Layer o "off")
nil
Command: (vla-get-Layer o)
"Off"
Command: (vla-put-Layer o "0")
nil
Command: (vla-put-Visible o nil)
; error: ActiveX Server returned an error: Parameter not optional
Command: (vla-put-Visible o 0)
nil
Command: (vla-get-Visible o)
:vlax-false
Command: (vla-put-Visible o :vlax-true)
nil
Command: (vla-put-StartPoint o '(1 2 3))
; error: lisp value has no coercion to VARIANT with this type:  (1 2 3)
Command: (vla-put-StartPoint o (vlax-3d-point 1 2 0))
nil
Command: (cdr (assoc 10 (entget l1)))
(1.0 2.0 0.0)
Command: (vla-put-StartPoint o (vlax-make-safearray vlax-vbDouble '(0 . 2)))
nil
Command: (cdr (assoc 10 (entget l1)))
(0.0 0.0 0.0)
Command: (vla-put-StartPoint o (vlax-safearray-fill (vlax-make-safearray vlax-vbDouble '(0 . 1)) '(3 4)))
; error: Automation Error. Incorrect number of elements in SafeArray
Command: (vlax-put o 'StartPoint '(0 0 0))
; error: Exception occurred
Command: (vlax-put o 'Color 3)
nil
Command: (vlax-put-property o 'Length 5.0)
; error: ActiveX Server returned an error: Type mismatch
Command: (vla-put-Color o)
; error: too few actual parameters
Command: (vla-get-Color o o)
; error: Too many actual parameters
```

An entity's `TrueColor` is a copy of its colour, a new colour object at each read. `ColorMethod` is 192 for ByLayer, 193 for
ByBlock, 195 for an index and 194 for an RGB colour (with `Red`, `Green` and `Blue`), and `EntityColor` of a ByLayer colour
is -1073741824. `(vla-SetRGB tc 10 20 30)` makes the copy an RGB colour, and `Red`, `Green` and `Blue` read it. Changing
the copy changes nothing until it is put back with `vla-put-TrueColor`.

```text
Command: (vla-put-Color o 256)
nil
Command: (setq tc (vla-get-TrueColor o))
#<VLA-OBJECT IAcadAcCmColor 000001e4a7f3c900>
Command: (vla-get-ColorMethod tc)
192
Command: (vla-get-EntityColor tc)
-1073741824
Command: (vla-put-ColorIndex tc 5)
nil
Command: (vla-get-Color o)
256
Command: (vla-put-TrueColor o tc)
nil
Command: (vla-get-Color o)
5
```

`(vla-GetBoundingBox o 'lo 'hi)` sets the two quoted symbols to safearrays of three doubles, the lower and upper corners of
the entity's extents in the WCS, and gives `nil`; `vlax-invoke-method o 'GetBoundingBox 'lo 'hi` does the same. The old
`vlax-invoke` gives `nil` and sets nothing, and an argument that is no symbol sets nothing and is no error.

```text
Command: (vla-GetBoundingBox o 'lo 'hi)
nil
Command: (vlax-safearray->list lo)
(0.0 0.0 0.0)
Command: (vlax-safearray->list hi)
(10.0 0.0 0.0)
```

#### Identity, erased and released objects

Every read gives a new VLA-OBJECT, as AutoCAD 2021 does: `(eq o (vlax-ename->vla-object l1))` is `nil`, also for the
application, the document and the collections, and `equal` of two values of one object is `T`. A value stands for its
object, not for what the object was: `vla-Delete` erases the entity and the value then gives `Automation Error. Object was
erased`, `vlax-erased-p` is `T` and `vlax-vla-object->ename` is `nil`, while `entdel` of the name brings the object back and
the same value works again. `vlax-ename->vla-object` of an erased entity gives `nil`.

`vlax-release-object` gives 0 and the value is released: it prints as `#<VLA-OBJECT 0000000000000000>`,
`vlax-object-released-p` is `T`, and a read through it is `null interface pointer:` and that print. On a locked layer
`vlax-write-enabled-p` is `nil` and `vlax-read-enabled-p` `T`; a put or a `Delete` is `Automation Error. On locked layer`.

```text
Command: (eq o (vlax-ename->vla-object l1))
nil
Command: (equal o (vlax-ename->vla-object l1))
T
Command: (setq o2 (vlax-ename->vla-object l2))
#<VLA-OBJECT IAcadLine 000001e4a7f3ca20>
Command: (vla-Delete o2)
nil
Command: (vlax-erased-p o2)
T
Command: (vla-get-Layer o2)
; error: Automation Error. Object was erased
Command: (vlax-vla-object->ename o2)
nil
Command: (vlax-ename->vla-object l2)
nil
Command: (entdel l2)
<Entity name: 111>
Command: (vla-get-Layer o2)
"0"
Command: (setq o3 (vlax-ename->vla-object l3))
#<VLA-OBJECT IAcadLine 000001e4a7f3ca80>
Command: (vlax-release-object o3)
0
Command: (vlax-object-released-p o3)
T
Command: (vla-get-Layer o3)
; error: null interface pointer: #<VLA-OBJECT 0000000000000000>
Command: (setq ok (vlax-ename->vla-object lk))
#<VLA-OBJECT IAcadLine 000001e4a7f3cae0>
Command: (list (vlax-write-enabled-p ok) (vlax-read-enabled-p ok))
(nil T)
Command: (vla-put-Color ok 2)
; error: Automation Error. On locked layer
Command: (vla-Delete ok)
; error: Automation Error. On locked layer
Command: (vlax-curve-getEndPoint ok)
(10.0 14.0 0.0)
```

`ObjectID` and `OwnerID` are the value of the object's handle and of its owner's, so they are unique and stay as they are in
the drawing; AutoCAD's are small numbers it hands out as the drawing is opened.

#### Text, blocks and attributes

A TEXT is changed through its own fields and realigned as `entmod` does; a TextString that holds a field code makes a field,
and `FieldCode` gives the text with its codes, or the text itself where it has none. An MTEXT's `TextString` keeps its
format codes. A circle's `Area` is writable (the radius follows), a light-weight polyline's `Coordinates` takes a variant of
2n doubles and sets the vertices, and the `StartPoint` and `EndPoint` of an arc are read only.

```text
Command: (setq t1 (vlax-ename->vla-object tx))
#<VLA-OBJECT IAcadText 000001e4a7f3cb40>
Command: (vla-get-TextString t1)
"abc"
Command: (vla-FieldCode t1)
"abc"
Command: (vla-put-TextString t1 "abcd")
nil
Command: (cdr (assoc 1 (entget tx)))
"abcd"
Command: (vla-put-Height t1 5)
nil
Command: (cdr (assoc 40 (entget tx)))
5.0
Command: (vla-put-StyleName t1 "NoSuch")
; error: Automation Error. Key not found
Command: (vla-put-TextAlignmentPoint t1 (vlax-3d-point 9 20 0))
; error: Automation Error. Not applicable
Command: (setq c1 (vlax-ename->vla-object ci))
#<VLA-OBJECT IAcadCircle 000001e4a7f3cba0>
Command: (list (vla-get-Diameter c1) (vla-get-Circumference c1) (vla-get-Area c1))
(10.0 31.4159 78.5398)
Command: (vla-put-Area c1 100.0)
nil
Command: (vla-get-Radius c1)
5.6419
Command: (setq a1 (vlax-ename->vla-object ar))
#<VLA-OBJECT IAcadArc 000001e4a7f3cc00>
Command: (list (vla-get-TotalAngle a1) (vla-get-ArcLength a1) (vla-get-Area a1))
(1.5708 7.85398 7.13495)
Command: (vlax-get a1 'StartPoint)
(70.0 5.0 0.0)
Command: (vla-put-StartPoint a1 (vlax-3d-point 0 0 0))
; error: ActiveX Server returned an error: Type mismatch
Command: (setq p1 (vlax-ename->vla-object pl))
#<VLA-OBJECT IAcadLWPolyline 000001e4a7f3cc60>
Command: (vlax-get p1 'Coordinates)
(0.0 40.0 10.0 40.0 10.0 50.0)
Command: (vla-put-Closed p1 :vlax-true)
nil
Command: (vla-get-Length p1)
34.1421
```

A block reference gives `Name`, and `EffectiveName`, which is the name of a dynamic block's definition where `Name` is its
anonymous block (and `Name` otherwise). `GetAttributes` gives the reference's attributes as a variant of an array of objects;
`vlax-safearray->list` of its value is the list, and `vlax-invoke` gives the list at once. A reference with none gives an
array with upper bound -1, which `vlax-safearray->list` refuses with `Invalid index` while `vlax-invoke` gives `nil`.
`GetConstantAttributes` gives the constant attribute definitions of its block, as attribute objects. Writing an
attribute's `TextString` changes the ATTRIB of its reference, as the Properties palette does, and moving a reference
(`InsertionPoint`) moves its attributes. `vlax-ename->vla-object` of the name of an ATTRIB gives its object, and `entmod` of the
ATTRIB [changes it too](#entities-and-tables).

```text
Command: (setq bo (vlax-ename->vla-object bi))
#<VLA-OBJECT IAcadBlockReference 000001e4a7f3ccc0>
Command: (vla-get-ObjectName bo)
"AcDbBlockReference"
Command: (list (vla-get-Name bo) (vla-get-EffectiveName bo) (vla-get-HasAttributes bo) (vla-get-XScaleFactor bo))
("Tag" "Tag" :vlax-true 2.0)
Command: (setq at (vla-GetAttributes bo))
#<variant 8201 ...>
Command: (setq atl (vlax-safearray->list (vlax-variant-value at)))
(#<VLA-OBJECT IAcadAttributeReference 000001e4a7f3cd20>)
Command: (vlax-invoke bo 'GetAttributes)
(#<VLA-OBJECT IAcadAttributeReference 000001e4a7f3cd80>)
Command: (setq a (car atl))
#<VLA-OBJECT IAcadAttributeReference 000001e4a7f3cd20>
Command: (list (vla-get-ObjectName a) (vla-get-TagString a) (vla-get-TextString a) (vla-get-Height a))
("AcDbAttribute" "TAG1" "v30" 5.0)
Command: (vla-put-TextString a "NEW")
nil
Command: (cdr (assoc 1 (entget (entnext bi))))
"NEW"
Command: (setq ca (vlax-safearray->list (vlax-variant-value (vla-GetConstantAttributes bo))))
(#<VLA-OBJECT IAcadAttribute 000001e4a7f3ce40>)
Command: (list (vla-get-TagString (car ca)) (vla-get-Constant (car ca)))
("CTAG" :vlax-true)
Command: (setq bo2 (vlax-ename->vla-object bi2))
#<VLA-OBJECT IAcadBlockReference 000001e4a7f3cea0>
Command: (vla-get-HasAttributes bo2)
:vlax-false
Command: (vlax-safearray-get-u-bound (vlax-variant-value (vla-GetAttributes bo2)) 1)
-1
Command: (vlax-safearray->list (vlax-variant-value (vla-GetAttributes bo2)))
; error: ActiveX Server returned an error: Invalid index
Command: (vlax-invoke bo2 'GetAttributes)
nil
Command: (vla-put-Name bo "NoSuch")
; error: Automation Error. Key not found
```

#### Text boxes

`(textbox elist)` is the box of the words of a TEXT or MTEXT as though it stood unturned at its insertion point: the lower
left and upper right corners, relative to that point. AutoCAD 2021 gives `((0.0 0.0 0.0) (6.66667 2.5 0.0))` for `abc` 2.5
high in the font `txt`. The presenter measures the words in the fonts of the drawing, its support folders and Windows'
Fonts folder, so the same text in Arial 2.5 high gives `((0.0 -0.0409277 0.0) (5.63097 2.5 0.0))`; it gives `nil` for words
whose font it cannot find, and for something that is no text. `vla-GetBoundingBox` of a TEXT or MTEXT is that box moved to
the insertion point, and where no font is found it is the insertion point alone.

#### Selection sets

The `SelectionSets` of the document are sets that a routine names, kept apart from the sets of `ssget` and from the drawing.
`(vla-Add sets name)` makes an empty one (a name already there is `Automation Error. The named selection set exists`, and
names are case-sensitive: `"ghss"` is another set than `"GHSS"`), and `Item` takes the name or an index. `Count`, `Item` and
`vlax-for` read the set. `Clear` empties it, and `Delete` takes it out of the collection, after which its value is `Automation
Error. Selection Set was deleted`. `AddItems` takes a variant and `RemoveItems` a safearray of objects.
`SelectAtPoint pt [types data]` adds the object a pick at the point finds, once however often it is called; the point is a
variant or a safearray, a list being the coercion error. `Select mode [p1 [p2 [types data]]]` takes 0 for a window, 1 for a
crossing, 3 for the previous selection, 4 for the last and 5 for all, with the filter as a safearray of integer codes and
one of values; the fence and polygon modes are not there. The names `acSelectionSetAll` and the like are the constants `(vl-load-com)` binds:
`(vla-Select ss acSelectionSetAll)` selects every entity.

```text
Command: (setq sss (vla-get-SelectionSets doc))
#<VLA-OBJECT IAcadSelectionSets 000001e4a7f3cf00>
Command: (setq ss (vla-Add sss "GHSS"))
#<VLA-OBJECT IAcadSelectionSet 000001e4a7f3cf60>
Command: (vla-Add sss "GHSS")
; error: Automation Error. The named selection set exists
Command: (vla-SelectAtPoint ss (vlax-3d-point 5 0 0))
nil
Command: (vla-SelectAtPoint ss (vlax-3d-point 5 0 0))
nil
Command: (vla-get-Count ss)
1
Command: (vla-Item ss 0)
#<VLA-OBJECT IAcadLine 000001e4a7f3cfc0>
Command: (vla-SelectAtPoint ss '(5 0 0))
; error: lisp value has no coercion to VARIANT with this type:  (5 0 0)
Command: (vla-Clear ss)
nil
Command: (vla-Select ss 5)
nil
Command: (vla-get-Count ss)
11
Command: (vla-Clear ss)
nil
Command: (vla-Select ss 0 (vlax-3d-point -1 -1 0) (vlax-3d-point 11 3 0))
nil
Command: (vla-get-Count ss)
1
Command: (vla-Clear ss)
nil
Command: (vla-Select ss 5 nil nil (vlax-safearray-fill (vlax-make-safearray vlax-vbInteger '(0 . 0)) '(0)) (vlax-safearray-fill (vlax-make-safearray vlax-vbVariant '(0 . 0)) '("LINE")))
nil
Command: (vla-get-Count ss)
4
Command: (vla-Delete ss)
nil
Command: (vla-get-Count ss)
; error: Automation Error. Selection Set was deleted
Command: (vla-Item sss "GHSS")
; error: Automation Error. Key not found
```

`ActiveSelectionSet` of the document is a set named `"CURRENT"` that holds the last selection `ssget` or a command made, a new
value at each read: FDVAL's iteration after an `ssget` reads it. `PickfirstSelectionSet` is `"PICKFIRST"` and holds the
pick-first selection. Reading either adds it to `SelectionSets`. `vla-Delete` of the active set does no harm: the next read
holds the previous selection again.

```text
Command: (setq gx (ssget "_X"))
<Selection set: 2>
Command: (setq ass (vla-get-ActiveSelectionSet doc))
#<VLA-OBJECT IAcadSelectionSet 000001e4a7f3d020>
Command: (list (vla-get-Name ass) (vla-get-Count ass))
("CURRENT" 11)
Command: (setq gw (ssget "_W" '(-1 -1) '(11 3)))
<Selection set: 3>
Command: (vla-get-Count (vla-get-ActiveSelectionSet doc))
1
Command: (vla-get-Name (vla-get-PickfirstSelectionSet doc))
"PICKFIRST"
```

#### The current layer and the undo marks

`ActiveLayer` put with a layer's object sets CLAYER. `StartUndoMark` and `EndUndoMark` are UNDO Begin and End: the steps
between them are one `U`, and an `EndUndoMark` with none open does nothing.

```text
Command: (vla-put-ActiveLayer doc (vla-Item (vla-get-Layers doc) "Off"))
nil
Command: (getvar "CLAYER")
"Off"
Command: (vla-put-ActiveLayer doc (vla-Item (vla-get-Layers doc) "0"))
nil
Command: (vla-EndUndoMark doc)
nil
```

### Undo of typed forms

The changes made by forms typed one after the other at the Command prompt, with no command between them, are **one** undo
step, as AutoCAD 2021 gave it, whatever made them: `entmake`, `entdel`, `setvar` of a drawing variable, `entmod` and the
object functions. Two `vla-put-Color` forms typed in turn, two `entmake` forms, `setvar DIMSCALE 2` then `3`, an `entmake`
and a `vla-put-Color` after it, or a `setvar` and an `entmod`, are each taken back by one `U`. A command between two forms
ends the run, and what is typed after the command is part of that command's step, as AutoCAD 2021 does: REGEN, `entmake`,
LINE, `entmake` and three `U` take the LINE and the `entmake` after it, then the first `entmake`, then everything earlier. A
command that changes nothing, `REGEN` for one, leaves no step of its own, so the form after it is a step of its own,
where AutoCAD counts the `REGEN` as the step the form belongs to. `U` and `REDO` make no step: the forms typed after them
join the step that is last again, and the next `U` takes both. `StartUndoMark` and `EndUndoMark` around forms make one step
of those too. A form that changes nothing between two forms does not separate them. A `c:` command is one step, and the forms
typed after it are not yet part of it.

```text
Command: (setq o (vlax-ename->vla-object l1))
#<VLA-OBJECT IAcadLine 000001e4a7f3c060>
Command: (vla-get-Color o)
256
Command: (vla-put-Color o 3)
nil
Command: (vla-put-Color o 4)
nil
Command: U
(Lisp Expression)
Command: (vla-get-Color o)
256
Command: (vla-put-Color o 5)
nil
Command: REGEN
Regenerating model.
Command: (vla-put-Color o 6)
nil
Command: U
(Lisp Expression)
Command: (vla-get-Color o)
5
Command: U
(Lisp Expression)
Command: (vla-get-Color o)
256
```

No undo group stays open between the forms, so autosave, the remote server and the block editor see a drawing at rest, and an
edit of the Properties palette made between two forms is a step of its own.

### COM to other programs is refused

`vlax-get-or-create-object`, `vlax-create-object` and `vlax-get-object` start or reach another program, Excel for one, and
the presenter does not. Each is an error that says so, with the function's name and the program given:

```text
Command: (vlax-get-or-create-object "Excel.Application")
; error: vlax-get-or-create-object: COM to other programs is not supported by GeometryHelper.Cad: "Excel.Application"
Command: (vl-catch-all-error-message (vl-catch-all-apply 'vlax-create-object '("Excel.Application")))
"vlax-create-object: COM to other programs is not supported by GeometryHelper.Cad: \"Excel.Application\""
Command: (vlax-import-type-library :tlb-filename "x.tlb")
; error: vlax-import-type-library: type libraries are not supported by GeometryHelper.Cad
Command: (vlax-get-or-create-object 5)
; error: bad argument type: (or stringp symbolp): 5
```

It is an error and not `nil` for every program, where AutoCAD gives `nil` for one it cannot find, so that a routine's
`*error*` runs and says why; a routine that tests the result for `nil` and carries on without Excel needs
`vl-catch-all-apply`. The `vlax-put-property`, `vlax-invoke-method` and `vla-put-Visible` calls such a routine makes on
Excel's objects are never reached.

### What is parked in the ActiveX stage

These are not defined, and a call is `; error: no function definition: NAME`: `vlax-dump-object`, `vlax-ldata-get`,
`-put`, `-list`, `-delete` and `-test`, `vlax-add-cmd` and `vlax-remove-cmd`, the product keys (`vlax-product-key` and
the two with it) and every `vlr-` function, with `vl-load-reactors`; there are no reactors. The `Layouts` and `Documents`
collections, the `Add...` methods of a space and `InsertBlock`, `Layers.Add` and `Blocks.Add`, writes to a table record,
`Copy`, `Move`, `Rotate`, `Mirror`, `ScaleEntity`, `Explode`, `IntersectWith`, `Offset`, `GetXData` and `SetXData`,
`SelectOnScreen`, a selection by fence or polygon, `Utility` and `Preferences` are not there either: a `vla-` function of
one of them is not defined, or where its name is defined for another object, it is `unknown name`.

Where the presenter differs from the full AutoCAD 2021 that measured it:

- `vla-put-Length` and its like are defined for the writable members of the objects listed above only; AutoCAD defines a
  `vla-put-` name once any object has a writable member of it, so `(type vla-put-Length)` is `SUBR` there and `nil` here.
- `U` typed right after a `REGEN` that follows a change takes back only the `REGEN` in AutoCAD, which counts it as a step,
  and the change here, where `REGEN` is no step.
- `Item` of a model space or a block takes an integer; AutoCAD also takes a handle in a string.
- `ObjectID` and `OwnerID` are handle values, and a TEXT of a font the presenter cannot find has no box.
- A ProgID that does not exist is `nil` in AutoCAD and an error here, as above.

## Dialogs (DCL)

The fifth stage is the dialogs of a routine. A routine writes a DCL file (Dialog Control Language, the text AutoCAD reads
a dialog from), loads it with `load_dialog`, makes a dialog with `new_dialog`, fills it with `set_tile` and `start_list`,
says what each tile does with `action_tile`, and runs it with `start_dialog`. The window shows it at once, drawn in the
presenter's own colours at the sizes AutoCAD 2021 draws a dialog, and the routine goes on when the dialog ends.

AutoCAD 2021's console cannot open a dialog: `load_dialog` gives `-1` for every file there and `new_dialog` gives `nil`. So
what a dialog does was measured in two ways. Five probe files typed into the console (the 22 functions are all SUBRs, the
text of their argument errors, their values where no dialog is open, the `$` variables, and `entmod` of an attribute) and
three runs of the full AutoCAD 2021 with its window, with clicks and keys sent to the dialogs it showed and a screenshot
and the position of every control written out (loading errors, the values of every tile kind, the exits, the actions, nested
dialogs, errors in an action, and the size of every tile). Where neither measured a thing, the test beside it says so: it
follows AutoCAD's own DCL help, or it is the presenter's choice. The 435 tests of the dialog, attribute and `nentsel` classes of the
Cad suite and the 71 of the window take their expected values from those runs, and each says in a comment which.

### The language

A DCL file is a list of definitions, `name : parent { ... }`, and `@include "file";` lines. Inside the braces come
attributes (`label = "Text:";`), tiles (`: edit_box { ... }`) and the names of tiles with nothing of their own added
(`ok_cancel;`). Comments are `// ...` to the end of the line and `/* ... */`; white space is free; names are letters,
digits and `_`, not starting with a digit, and, as in AutoCAD, **upper and lower case are different**: `get_tile "Text"`
does not find the key `text`. A value is a string in double quotes (with `\"`, `\\`, `\n` and `\t`), a whole number, a real
or a bare name (`true`, `centered`, `left`). A file is read as UTF-8, or as the machine's ANSI code page where it is not
UTF-8, which is how `write-line` has written it.

A definition's parent is a tile kind or another definition, and a tile takes its parent's attributes and children and
lays its own over them: after `my_ok : ok_button { label = "Go"; }` the tile `my_ok;` is the OK button with the label
`Go`, still with the key `accept`. `@include "other.dcl";` reads another file, looked for as the including file's folder
first, then as `load_dialog` looks; `.dcl` is added where the name has no extension. `@include "base.dcl";` adds nothing,
since the predefined tiles are always there, and `@include "acad.dcl";` reads AutoCAD's own `acad.dcl` where the
support folders have one (the presenter ships none).

The predefined tiles are written out for the presenter from the description in AutoCAD's help, not copied: `row`, `column`,
`boxed_row`, `boxed_column`, `radio_row`, `radio_column`, `boxed_radio_row`, `boxed_radio_column`, `concatenation`,
`paragraph`, `text_part`, `spacer_0` and `spacer_1`, the buttons `default_button`, `retirement_button`, `ok_button` (key
`accept`, label `"  OK  "`, `is_default`), `cancel_button` (key `cancel`, `is_cancel`), `help_button` and `info_button`, the
exit rows `ok_only`, `ok_cancel`, `ok_cancel_help`, `ok_cancel_help_info`, `ok_cancel_help_errtile` and `ok_cancel_err`,
`errtile` (a text with the key `error`), `image_block`, `icon_image`, `edit12_box` and `edit32_box`. **A file that defines
one of these names is refused**, as AutoCAD refuses its own `base.dcl` when it is loaded again.

Every tile kind of AutoCAD's DCL is read:

| Kind | What it is | `get_tile` gives |
|---|---|---|
| `dialog` | the window; `label` is its title, `initial_focus` the key of the tile that starts with the focus | |
| `row`, `column`, `boxed_row`, `boxed_column` | clusters that lay their tiles left to right or top to bottom; a boxed one has a border and its label | `""` |
| `radio_row`, `radio_column`, `boxed_radio_row`, `boxed_radio_column` | a cluster of `radio_button`s of which one is on | the key of the button that is on, `""` for none |
| `concatenation`, `paragraph` | text parts laid side by side or one over the other | `""` |
| `button` | a push button; `is_default` and `is_cancel` mark the exits | `nil` |
| `edit_box` | a line of text; `edit_width` characters wide, `edit_limit` characters long (132 where not given), `password_char` hides it | its text |
| `list_box` | a list; `multiple_select`, `tabs`, `tab_truncate`, `allow_accept` | the chosen items from 0, `"0 2"`; `""` for none |
| `popup_list` | a drop-down list | one index from 0, `"0"` before a choice where it has items |
| `toggle` | a check box | `"0"` or `"1"` |
| `radio_button` | a button of a radio cluster | `"0"` or `"1"` |
| `text`, `text_part`, `errtile` | text; `is_bold` | what it shows: its `value` once it has one, else its `label`; an `errtile` `""` |
| `image`, `image_button` | a picture the routine draws with `fill_image` and `vector_image`; `aspect_ratio` | `nil` |
| `slider` | a bar between `min_value` (0) and `max_value` (10000) with `small_increment` and `big_increment`; vertical with `layout = vertical` | an integer in range |
| `spacer`, `spacer_0`, `spacer_1` | room between tiles | `""` |

`width` and `height` count characters, 6 pixels wide and 13 high as AutoCAD counts them: a list box with `width = 20` is
120 pixels wide and an image with `height = 4` is 52 high. `fixed_width`, `fixed_height`, `alignment`,
`children_alignment`, `children_fixed_width`, `children_fixed_height`, `horizontal_margin`, `vertical_margin`, `is_enabled`,
`is_tab_stop`, `mnemonic` and `action` are read. An attribute the presenter does not know is kept, and `get_attr` still
gives it.

### Loading a file and its messages

`(load_dialog file)` gives a whole number, 1 for the first file of a drawing and one more for each file loaded after it (a
file loaded twice gets two numbers, as in AutoCAD), and `-1` for a file that is not there. A name with a folder is that
file; any other is looked for in the support folders, and first in the current folder and beside the drawing where
LEGACYCODESEARCH is 1 ([Trusted folders and SECURELOAD](#trusted-folders-and-secureload)); `.dcl` is added
where the name has no extension. `(unload_dialog id)` forgets the file and gives `nil`; dialogs already made from it keep
their own copy.

A file with a mistake is not loaded: the presenter shows AutoCAD's message in an alert, which a window shows as a box and a
headless session writes on the command line, and `load_dialog` gives `-1`. The words are AutoCAD's, measured in the full
AutoCAD 2021, where it has them: a syntax error is `syntax error.` and the symbol, a `;` left out is `missing semicolon.`,
and a tile that is not defined is a semantic error. A file that defines a predefined name is
`warning: redefining object.` with the name.

```text
Command: (load_dialog "stray.dcl")          ; the second line is  label = Edit;;
Error in dialog file
"C:\Users\you\AppData\Local\Temp\stray.dcl", line 2:
syntax error.
Symbol: ";".
-1
Command: (load_dialog "nosemi.dcl")         ; an edit_box { key = "x" } with no ; before its brace
Error in dialog file
"C:\Users\you\AppData\Local\Temp\nosemi.dcl", line 2:
missing semicolon.
-1
Command: (load_dialog "nothing.dcl")        ; the file is  d : nothing { }
Semantic error(s) in DCL file C:\Users\you\AppData\Local\Temp\nothing.dcl.
Tile "nothing" is not defined.
-1
Command: (load_dialog "row.dcl")            ; the file is  row : column { }
Error in dialog file
"C:\Users\you\AppData\Local\Temp\row.dcl", line 1:
warning: redefining object.
Symbol: "row".
-1
```

AutoCAD's second line of the semantic error is `See file acad.dce for details.`, and it writes that file; the presenter
writes no `acad.dce` and says which tile it could not find instead. A name that is not on the search path is `-1` with no
message, as in AutoCAD.

A session with no window, as the console, has no dialogs: `(new_dialog "name" id)` gives `nil` whatever the file. Unlike
the console, `load_dialog` there still reads the file and finds its mistakes, so a routine can be checked for them
headless.

### The 22 functions

An argument of the wrong kind is checked before anything else, with or without a dialog, and is AutoCAD's own error
(measured in the console): `(get_tile 5)` is `bad argument type: stringp 5`, `(mode_tile "a" 1.5)` is `bad argument type:
fixnump: 1.5`, `(get_tile)` is `too few arguments`, `(get_tile "a" "b")` is `too many arguments`.

| Function | What it does | What it gives |
|---|---|---|
| `(load_dialog file)`, `(unload_dialog id)` | read a file, forget it | the number, `-1`; `nil` |
| `(new_dialog name id [action [point]])` | makes the dialog `name` of file `id`; `action` is the action of every tile that has none, `point` a list of two numbers for the top left corner on the screen, `'(-1 -1)` the centre | `T`; `nil` for an unknown name or number, for a dialog with no exit tile (no key `accept` or `cancel`, no `is_default` or `is_cancel`), and in a session with no window |
| `(start_dialog)` | shows the dialog and waits for it to end | the status, below |
| `(done_dialog [status])` | ends the dialog when the action that calls it returns | the top left corner of the window in screen pixels, `(743 461)`; `(100 120)` for a dialog given that point; `(0 0)` with no dialog |
| `(term_dialog)` | ends every open dialog | `nil` |
| `(action_tile key "forms")` | the forms to run when the tile is used; `""` takes the action away | `T`, for a missing key too |
| `(get_tile key)`, `(set_tile key value)` | read or put a tile's value as a string | the value; the value given |
| `(mode_tile key mode)` | 0 enables, 1 disables, 2 gives the focus, 3 selects the text of an edit box, 4 turns the highlight of an image button over; another number does nothing | `nil` |
| `(get_attr key attribute)` | the attribute as the DCL file has it: `(get_attr "txt" "label")` is `"&Text:"` and `(get_attr "txt" "edit_width")` is `"20"`, a prototype's too, a string's `\n` a real new line | the text; `""` for an attribute that is not there |
| `(client_data_tile key data)` | keeps a string, given to the tile's actions as `$data` | the data |
| `(start_list key [operation [index]])`, `(add_list item)`, `(end_list)` | fill a list box or popup list: 3 starts a new list (the default), 2 appends, 1 replaces the item at `index` | the key, for any tile with one; the item; `nil` |
| `(start_image key)`, `(fill_image x y width height colour)`, `(vector_image x1 y1 x2 y2 colour)`, `(slide_image x y width height name)`, `(end_image)` | draw in an image, in pixels from its top left corner, in an ACI colour 0 to 255 or `-2` (the background of the picture), `-15` (the dialog's), `-16` (its text), `-18` (its lines) | the key; the colour; the colour; `nil`; `nil` |
| `(dimx_tile key)`, `(dimy_tile key)` | the width and height of a tile in pixels as the dialog lays it out | a whole number; `0` for a missing key |

With no dialog open, which is also where `new_dialog` gave `nil`: `get_tile`, `set_tile`, `action_tile`, `mode_tile`,
`client_data_tile`, the list and the image functions give `nil`, `get_attr` gives `""`, `dimx_tile` and `dimy_tile` give `0`,
`done_dialog` gives `(0 0)`, `start_dialog` gives `0` and `term_dialog` gives `nil`. A key that no tile has gives the same.
All of this is AutoCAD's console's own, measured. In a dialog the values are those the full AutoCAD 2021 gave:

- A `toggle` keeps any text set in it (`"7"` reads back as `"7"`); a `slider` clamps to its range (500 in a slider up to
  100 is `"100"`, -5 is `"0"`).
- A multiple-selection `list_box` **adds** to its choice when set: `"0 2"` and then `"4 1"` read back as `"0 1 2 4"`; an index
  past the end is ignored and `""` clears the choice. A `radio_button` set to `"1"` moves the cluster's value, and the
  cluster set to a key moves the buttons.
- `set_tile` on a button gives the value but `get_tile` of a button is still `nil`. `set_tile` of a key no tile has is `nil`.
- `start_list` with operation 3 or 2 keeps a popup's `"0"`; operation 1, replacing item 0, makes it `""`.
- `edit_limit` stops typing, not `set_tile`: a text put in is not cut.

`slide_image` draws nothing and gives `nil`; see [what is not there](#what-is-parked-in-the-dialog-stage).

### Running a dialog

`(start_dialog)` shows the dialog and runs what the user does until it ends, then gives the status: 1 for OK, 0 for Cancel
or Esc, the number given to `done_dialog`, and -1 where `term_dialog` ended it. Each use of a tile first puts its value
into the tile, so `get_tile` in the action sees it, and then finds the action: the tile's `action_tile` forms, else its
`action` attribute in the DCL, else for a radio button its cluster's, else the `action` given to `new_dialog`. The forms
are read and run in the routine's environment: an action sees the routine's local variables, as in AutoCAD, which scopes
dynamically.

```lisp
(defun c:ed ( / path f id text status)
  (setq path (vl-filename-mktemp "ed.dcl") f (open path "w"))
  (write-line "ed : dialog { label = \"Edit text\";" f)
  (write-line "  : edit_box { key = \"txt\"; label = \"&Text:\"; edit_width = 60; allow_accept = true; }" f)
  (write-line "  ok_cancel; }" f)
  (close f)
  (setq id (load_dialog path))
  (if (new_dialog "ed" id)
    (progn
      (set_tile "txt" "old text")
      (action_tile "accept" "(setq text (get_tile \"txt\")) (done_dialog 1)")
      (setq status (start_dialog))
      (princ (strcat "\nstatus " (itoa status) ", text " (vl-prin1-to-string text))))
    (princ "\nno dialog"))
  (unload_dialog id)
  (vl-file-delete path)
  (princ))
```

Typing `new text` into the box and pressing OK prints `status 1, text "new text"`. Pressing Cancel prints `status 0, text
nil`: the routine's `accept` action never ran, and Cancel closed the dialog with 0 without any action of its own. The same
dialog on the command line of a session with no window prints `no dialog`.

**How a dialog ends** (the full AutoCAD 2021, g1 to g3):

- A tile with `is_default` or the key `accept` and no action ends the dialog with 1; with an action, the action runs and the
  dialog ends only if the action calls `done_dialog`. An accept action that does not call it **keeps the dialog open**, for
  Enter and for a click.
- **`(done_dialog)` with no status gives 0**, also from the OK action: a routine that means 1 writes `(done_dialog 1)`. The
  status is the dialog's, and the position `done_dialog` gives is that of the window when it closed.
- A tile with `is_cancel` or the key `cancel` runs its action, if it has one, and then closes the dialog with 0 if the action
  did not end it: a cancel action that only restores a value, and never calls `done_dialog`, still closes the dialog. Esc
  and the close box of the window do the same. `ok_only` is an OK that is also `is_cancel`, so Esc in it closes with 1.
- The `action` given to `new_dialog` serves OK and Cancel when they have none: OK runs it and the dialog stays open,
  Esc runs it and closes with 0.
- Enter in an edit box without `allow_accept` runs the box's action with `$reason` 1 and the dialog stays; with
  `allow_accept` it presses the default tile, whose action sees `$key` `"accept"` and `$reason` 1. Enter elsewhere presses
  the button that has the focus, else the default one.
- A box whose text was changed and which still has the focus when the dialog closes gets its action first, with `$reason` 2.
  (The order after the exit tile's action is the presenter's choice.)
- `(done_dialog)` before `(start_dialog)` gives `(0 0)` and does nothing; the dialog is then shown. A dialog that has
  already ended gives its status again at once, without being shown.

**What an action sees.** Inside an action, `$key` is the tile's key, `$value` its value after the use, `$reason` why it ran,
`$data` the client data or `nil`, and `$x` and `$y` the point of a click in an image button, in the pixels of the tile, and
0 for every other tile. They are unbound again when the action ends, and unbound before any dialog, as in AutoCAD.
`$reason` is 1 for a tile picked, a box left by Enter, and a choice made, 2 for an edit box left with a changed text, 3 for
a slider dragged, and 4 for a double click in a list box or an image button.

```lisp
(setq *log* nil)
(defun note (s) (setq *log* (cons s *log*)))
(defun c:pick ( / path f id status)
  (setq path (vl-filename-mktemp "pk.dcl") f (open path "w"))
  (foreach l '("pk : dialog { label = \"Pick\";"
               "  : list_box { key = \"tags\"; label = \"Tags\"; multiple_select = true; width = 20; height = 6; }"
               "  : toggle { key = \"all\"; label = \"All blocks\"; }"
               "  : radio_row { key = \"mode\";"
               "    : radio_button { key = \"a\"; label = \"A\"; }"
               "    : radio_button { key = \"b\"; label = \"B\"; } }"
               "  ok_cancel; }")
    (write-line l f))
  (close f)
  (setq id (load_dialog path))
  (new_dialog "pk" id)
  (start_list "tags")
  (mapcar 'add_list '("PART" "MARK" "LEN" "QTY"))
  (end_list)
  (set_tile "mode" "b")
  (foreach k '("tags" "all" "mode")
    (action_tile k "(note (list $key $value $reason))"))
  (action_tile "accept" "(note (list $key (get_tile \"tags\") (get_tile \"all\") (get_tile \"mode\"))) (done_dialog 1)")
  (setq status (start_dialog))
  (princ (strcat "\nstatus " (itoa status)))
  (princ "\nlog: ") (prin1 (reverse *log*))
  (princ "\nafter: ") (prin1 (list $key $value))
  (princ))
```

With the first and third items chosen, the toggle turned on, the first radio button clicked and OK pressed, it prints

```text
status 1
log: (("tags" "0 2" 1) ("all" "1" 1) ("mode" "a" 1) ("accept" "0 2" "1" "a"))
after: (nil nil)
```

The two radio buttons have no action of their own, so the cluster's action ran, with `$key` the cluster's key `"mode"` and
`$value` the key of the button on, `"a"`; a radio button with an `action` of its own runs that one only. A button's
action sees `$value` `"1"`, and a click puts `$x` and `$y` in it (66 and 11 for the centre of a button 132 by 22).

### Dialogs inside dialogs, and errors

`new_dialog` and `start_dialog` inside an action open a child over its parent, which stays shown and takes no input until
the child ends. `(term_dialog)` in the child's action ends both, and both `start_dialog` calls give -1:

```text
Command: NEST
outer -1, inner -1
no dialog now: (nil (0 0) 0 nil)
```

A routine that must pick a point while its dialog is open does what AutoCAD's routines do: the action calls `(done_dialog 2)`,
`start_dialog` gives 2, the routine calls `getpoint`, and makes the dialog again.

**An error in an action, and input in an action, end the whole evaluation**, as they do in AutoCAD 2021 (g2, g3): every open
dialog is closed and the routine is stopped, and a `vl-catch-all-apply` around `start_dialog`, or around the call inside the
action, does not catch it. An action `(car 5)` in a routine that runs `(vl-catch-all-apply 'start_dialog nil)` prints
`; error: bad argument type: consp 5` and the forms after it in the routine never run. The same ends a routine that calls
`command`, `vl-cmdf`, a `get` function, `entsel`, `nentsel`, `nentselp` without a point or an interactive `ssget` while a
dialog is open: `; error: AutoCAD rejected function`. `alert` and `getfiled` work in an action, since they are windows of
their own. (`getpoint` in an action was not measured and is treated as `command`; `*error*` is not run after such an
error, which the full AutoCAD run did not reach either.)

### The window

A dialog is a window over the presenter's own, owned by it and not in the taskbar, titled with the dialog's `label` and
placed at the point given to `new_dialog` or centred over the window. The window under it takes no input while a dialog
is shown, and a dialog opened from an action is shown over the one it came from. The tiles are laid out at AutoCAD 2021's
sizes and drawn as the presenter draws its own dialogs: the buttons, boxes and lists of the application's theme, text in
Segoe UI 12 pixels, a default button in the primary colour, disabled tiles greyed, and an image without a colour
dark. Tab goes through the tiles in the order of the file (not `is_tab_stop = false` ones), `&` in a label marks the access
key, and an edit box that has the initial focus shows its text selected, as AutoCAD's does.

The sizes were measured in the full AutoCAD 2021, in pixels, with the border and title of the window counted:

- Heights: a button 22, an edit box 19, a popup list 21, a toggle 15, a radio button 19 (a row of them in a box 48), a
  text 13, a slider 17, a line of a list 13 (a list `height = 6` with a label has a tile 68 high and a list 54).
- An OK button is 49 by 22 and a Cancel 55 by 22: the text and 16, or `width` characters and 1 if that is more. An
  `edit_box` with `edit_width = 60` has a box 372 wide, after its label; with `edit_width = 20`, 132, and the label `&Text:`
  is 31 wide, so the tile is 163 by 19. Tiles of a column that are not fixed stretch to its width; an image, an image
  button and buttons keep theirs.
- A dialog of an edit box with its label `Text:`, a `spacer_1` and `ok_cancel` is 433 by 109, with the box at 47,37, OK at
  151,73 and Cancel at 228,73 in the window. A dialog of a text 219 wide, a `spacer`, a multiple-selection list box, a `spacer` and
  `ok_cancel` is 249 by 227. `ok_cancel` alone is 162 by 93, with OK at 16,46 and Cancel at 93,46, 28 pixels apart; a
  dialog with a tile of every kind is 339 by 438.
- The room between tiles, as AutoCAD gives it: a `spacer_1` takes 9 pixels, a bare `spacer` 4, and a spacer has no margin
  above or below; a text stands 2 pixels from the top of its slot and 6 from the bottom; an exit row has 15 pixels above and
  17 below it in a dialog of many tiles, and the error tile of an exit row stands 28 below OK.
- `dimx_tile` and `dimy_tile` give the tile's size as the window draws it: 49 for OK. A window-less
  session lays out with a font it does not have and gives 52 for the same OK, and 402 for the edit box with `edit_width =
  60` and the label `&Text:`, where the window gives 403.

AutoCAD's own dialog text is about 0.9 of the width of Segoe UI 12 pixels, so a text of a dialog is a little wider here
than in AutoCAD (a text of 219 pixels there is 242 here). The sizes of the controls and their positions are AutoCAD's.

### Driving a dialog from a script

Only the presenter's window shows dialogs. A program that hosts a `CadCommandSession` in a window of its own gets `nil`
from `new_dialog`, as the console does. For tests, and to run a routine that has a dialog headless, the library has an
internal script host that answers a dialog from a list of steps instead of from the user, one to a line: `type key
"text"`, `enter [key]`, `click key [x y]`, `dblclick key [index]`, `select key [index ...]`, `slide key value [drag]`,
`esc`, `close` and `dump`. It logs `open`, `dump` (every tile with a key and its value), `close` with the status and
`refused` for a step on a tile that is missing or disabled; a dialog still open when the steps run out is closed as Esc
does. The 22 functions behave as they do with a window, and a routine that is run with it ends the way it would for a
user who does those things. The test projects reach it through `InternalsVisibleTo`, and the comparison harness of the
repository runs a routine in AutoCAD 2021 with a stand-in of the 22 functions and in the presenter with this host, and
compares the drawings and the transcripts.

### What is parked in the dialog stage

- `slide_image` draws nothing and gives `nil`, writing `slide_image: slides are not supported by GeometryHelper.Cad yet`
  once for the dialog; the presenter has no slide reader. A dialog with an icon still works.
- The colour tiles and dialogs of `acad.dcl` (its own file, read where it is found), `audit_level`, the file `acad.dce`,
  `password_char` beyond the look of a password box, `fixed_width_font` beyond a monospace font, the DIASTAT variable, and
  dialogs larger than the screen.
- Not measured, so as AutoCAD's help says or as the presenter chose: a double click in a list, `$x` and `$y` of a real click
  in an image button, a slider dragged, a choice of several items with Ctrl and a click (the full AutoCAD run could not post
  the key), the effect of `mode_tile` 2 and 3, `getpoint` in an action, and `*error*` after an error in an action.
- Making the dialog model public, for windows of other programs, is a later choice: the host is internal.

## While a routine waits

A routine that asks the user is suspended, and the presenter goes on: the window never blocks, and a headless session returns
from `Feed` where the routine waits and goes on with the next `Feed`. Only one thing runs at a time, so a routine and the
presenter never change the drawing together. What follows is for the window and for a host that drives a session itself.

- The prompt a routine asks is a prompt of the session's own, so object snaps, OSMODE, FROM and M2P, transparent ZOOM and PAN, and
  the rubber band work at it, and Esc ends the question with `Function cancelled`.
- A line typed that starts with `(` or `!` is plain input while the routine waits, with `Can't reenter LISP.` at a get
  function and with no message at a command's prompt reached by a PAUSE.
- Closing a drawing, reloading it, or closing all of them ends the routine that waits in it first: Esc is pressed until the
  session is idle, and a routine that catches `Function cancelled` and asks again is aborted, its next question a hard error
  that nothing in it can catch. A drawing closed does not leave a routine behind, and neither does the process: the thread
  that evaluates is a background thread.
- Hosts that call `Run`, `RunCommand` or `Load` while a routine waits are refused with `Can't reenter LISP.`; `LoadLisp` gives
  `false`.

## Each drawing its own variables

A variable or function a routine defines belongs to the drawing it was loaded into, as AutoCAD's does with LISPINIT at 1:
`(setq a 1)` in one drawing, and `!a` in another writes `nil`. A routine that serves every drawing is in a startup file,
which loads into each as it opens. The `c:` commands of a drawing are those of its own interpreter, and the other drawing
types them as an unknown command until it has loaded the file too. A value that must reach the other drawings is sent with
`vl-propagate`, a file with `vl-load-all`, and a value that is shared rather than copied lives on the
[blackboard](#the-registry-the-blackboard-and-other-drawings).

## What is not there yet

AutoLISP is built in stages, and a function that belongs to a later one is not defined yet. Calling it writes what AutoCAD
writes for a name it does not know, `; error: no function definition: NAME`, so a routine that uses only what is listed
above runs, and one that reaches for a later stage stops at its first such call. A routine that defines its commands loads
whole, and their names are offered by AutoComplete, whatever their bodies need.

The drawing, the library, the ActiveX functions and the dialogs are built, with these left out. In `entmake`: the POLYLINE sequences, an
INSERT with attributes, block definitions and the records of a table. Of the library, `acad_strlsort`, `getenv` and `setenv`
(they come with the registry store, when a routine needs them), `findtrustedfile` (not brought by L6, which built the
trusted folders it would search), `vl-load-reactors` (with the
`vlr-` functions, which the ActiveX stage leaves out: there are no reactors), and `vl-exit-with-error` and
`vl-exit-with-value`, which belong to VLX applications, are not defined. Of the ActiveX functions, `vlax-dump-object`,
`vlax-ldata-get` and its family, `vlax-add-cmd`, the product keys, the `Add` methods and the editing methods of the spaces
and a few more are parked, and a few measured differences remain; both lists are
[at the end of that section](#what-is-parked-in-the-activex-stage). Of the dialogs, `slide_image` draws nothing, and the
colour tiles of `acad.dcl`, `audit_level` and the `acad.dce` file are left out, as
[that section](#what-is-parked-in-the-dialog-stage) says. A `vl-sort` function that is no ordering can give
another list than AutoCAD's: for `'(2 1 2 1)` and a function that always answers `nil` AutoCAD gave `(1 2 1)` and the presenter
gives `(2 1 2 1)`, and for `'(1 2 3 2 1)` and one that always answers `T`, `(1 2 3 1)` and `(1 2 3 2 1)`. `vl-list->string` leaves out the codes
outside 1 to 65535 where AutoCAD printed other text, a `vl-filename-mktemp` counter does not go past 999, and Unicode typed
at the command line reaches `read-line` through the ANSI code page. Every stage of the plan is built; the trusted folders
of the last, L6, are [above](#trusted-folders-and-secureload).

A routine that opens with `(vl-load-com)` loads, and has the [ActiveX functions](#activex-objects) after it.
