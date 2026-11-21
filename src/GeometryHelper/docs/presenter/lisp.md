# AutoLISP

The presenter reads and runs AutoLISP, the language AutoCAD's routines are written in. A line typed at the command line,
a file loaded with `(load)` or APPLOAD, and the files a drawing loads when it opens all run on an interpreter of the
presenter's own, one for each open drawing: [`LispInterpreter`](xref:GeometryHelper.Cad.Lisp.LispInterpreter), which a
[`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) makes the first time it is asked for. The values, the
printing and the error messages were measured against AutoCAD 2021, so a routine that runs there writes the same text here.

This covers two stages. The first is the core of the language: the data, the control forms, `defun` and the `c:` commands,
errors, and the functions for numbers, lists, strings and units. The second is the drawing: a routine can run the
presenter's commands with `command`, ask the user for points, distances and words, pick objects, select, read and change
entities and the tables, read and set system variables, and convert between coordinate systems. The rest of the language's
library comes in later stages and is listed [at the end](#what-is-not-there-yet).

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
| The rest | `type`, `eval`, `read`, `set`, `boundp`, `eq`, `equal`, `load`, `defun-q-list-ref`, `defun-q-list-set`, `vl-symbol-value`, `vl-symbolp`, `vl-consp`, `vl-load-com` (which does nothing, so that a routine that starts with it loads) |
| Commands | `command`, `vl-cmdf`, and the symbol `PAUSE` |
| Asking | `getpoint`, `getcorner`, `getdist`, `getangle`, `getorient`, `getint`, `getreal`, `getstring`, `getkword`, `initget`, `entsel`, `nentsel` |
| Selection sets | `ssget`, `sslength`, `ssname`, `ssadd`, `ssdel`, `ssmemb` |
| Entities | `entget`, `entmod`, `entmake`, `entdel`, `entupd`, `entnext`, `entlast`, `handent` |
| Tables | `tblsearch`, `tblnext`, `tblobjname` |
| Variables | `getvar`, `setvar` |
| The session | `trans`, `osnap`, `alert`, `grtext`, `redraw` |

The functions of the last seven rows read or change the drawing, so they exist in the interpreter of a drawing and not in a bare
[`LispInterpreter`](xref:GeometryHelper.Cad.Lisp.LispInterpreter): `(getvar "OSMODE")` there is `; error: no function
definition: GETVAR`. The pure ones, `angle`, `distance`, `polar`, `inters` and `cvunit`, need no drawing and are in both.

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
for a form typed. AutoCAD 2021 folds them into the step before; a step of their own loses nothing. `UNDO BEgin` and `End`
run by `command` group what lies between them as when typed. Esc at a routine's wait ends the routine's step; what `*error*`
then changes is a step of its own, and U undoes it first.

<!-- TODO (lead, at the merge of parts F1 and F2): describe here what the routines needed of the presenter's own commands:
the header defaults of a blank drawing (F1), -LAYER Make, UCS E, -PURGE's prompts, -HATCH, REVCLOUD _A and DIMLINEAR's
lines (F2). -->

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
of a space, and for one inside a block reference gives `(ename pt matrix (insert-ename))`, with the insert's name last.

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
handle's text breaking a tie, as the selection engine of AutoCAD walks the drawing; windows and crossings keep their order.

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
value of the wrong kind. A partial list changes only the groups it has: `(entmod (list (cons -1 e) '(10 1.0 1.0 0.0)))` moves
the start of a LINE and nothing else. A layer named in group `8` that does not exist is made, white and Continuous. A
changed DIMENSION gets its picture made again, and a TEXT whose `72`, `73` or `11` change gets its `10` computed again.
`(entupd e)` gives the name and redraws.

`(entmake list)` gives the list, or `nil` for a type the presenter cannot make or a required group that is missing:
`(entmake '((0 . "LINE") (10 0.0 0.0)))` is `nil`, a LINE with no end. The defaults are AutoCAD's (a TEXT is made with
rotation 0, width factor 1, style Standard, justification left, and the normal `(0 0 1)`), a layer that is missing is
made, the entity goes into the current space, and `(entlast)` is it. A list straight from `entget` is taken: the leading
`-1`, and the `5` and `330` groups, are ignored, and a two-dimensional point is completed. The presenter makes LINE, POINT,
CIRCLE, ARC, ELLIPSE, TEXT, MTEXT, LWPOLYLINE, SOLID, TRACE, 3DFACE, RAY, XLINE, SPLINE and INSERT without attributes. A
POLYLINE with its VERTEX and SEQEND, an INSERT with ATTRIB, a BLOCK definition and the records of a table give `nil`.

`(entdel e)` gives the name and erases; a second `entdel` of the same entity restores it, as AutoCAD's does. `(entnext)`
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
string, or a point as a list of reals (three for LASTPOINT and UCSORG). A name the presenter has no variable of gives `nil`:
the ones AutoCAD has and the presenter lacks, PROGRAM, PLATFORM, VIEWCTR, UNDOCTL and ERRNO among them. `(getvar 5)` is
`bad argument type: (or stringp symbolp): 5`.

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
| every dimension variable | `DIMASZ`, `DIMTXSTY`, `DIMSAH`, `DIMBLK1` and the rest, as system variables of the drawing's header, typed alone or set with SETVAR with AutoCAD's prompts (`Enter new value for DIMSAH <OFF>:` takes ON or OFF) |

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
types them as an unknown command until it has loaded the file too.

## What is not there yet

AutoLISP is built in stages, and a function that belongs to a later one is not defined yet. Calling it writes what AutoCAD
writes for a name it does not know, `; error: no function definition: NAME`, so a routine that uses only what is listed
above runs, and one that reaches for a later stage stops at its first such call. A routine that defines its commands loads
whole, and their names are offered by AutoComplete, whatever their bodies need.

The drawing is built, with these left out: `sssetfirst`, `ssgetfirst` and `nentselp`, which come with the next stage, and in
`entmake` the POLYLINE sequences, an INSERT with attributes, block definitions and the records of a table. A few variables
AutoCAD has, PROGRAM, PLATFORM, VIEWCTR, UNDOCTL and ERRNO, read as `nil`.

| Stage | What it brings |
|---|---|
| L3 | the `vl-` functions of strings and files, `open` and `close`, the blackboard |
| L4 | the ActiveX functions, `vla-` and `vlax-` |
| L5 | dialogs, DCL: `load_dialog`, `new_dialog`, `start_dialog` |
| L6 | trusted folders and the secure-load check; for now `load` reads any file it is given |

A routine that opens with `(vl-load-com)` loads today: that call is defined and does nothing.
