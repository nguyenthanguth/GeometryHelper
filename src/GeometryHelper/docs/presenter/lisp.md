# AutoLISP

The presenter reads and runs AutoLISP, the language AutoCAD's routines are written in. A line typed at the command line,
a file loaded with `(load)` or APPLOAD, and the files a drawing loads when it opens all run on an interpreter of the
presenter's own, one for each open drawing: [`LispInterpreter`](xref:GeometryHelper.Cad.Lisp.LispInterpreter), which a
[`CadCommandSession`](xref:GeometryHelper.Cad.CadCommandSession) makes the first time it is asked for. The values, the
printing and the error messages were measured against AutoCAD 2021, so a routine that runs there writes the same text here.

This is the first stage. It is the core of the language: the data, the control forms, `defun` and the `c:` commands, errors,
and the functions for numbers, lists, strings and units. The functions that read or change the drawing, and the rest of
the language's library, come in later stages and are listed [at the end](#what-is-not-there-yet).

```csharp
var session = new CadCommandSession(document);

session.Feed("(setq a (+ 2147483647 1))");                  // Command: (setq a (+ 2147483647 1))
                                                            // -2147483648
session.Feed("(defun c:hello () (princ \"hello\") (princ))");
session.Feed("HELLO");                                      // Command: HELLO
                                                            // hello
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
| Units | `rtos`, `angtos`, `distof`, `angtof`, which read the drawing's LUNITS, LUPREC, AUNITS, AUPREC and DIMZIN |
| The rest | `type`, `eval`, `read`, `set`, `boundp`, `eq`, `equal`, `load`, `defun-q-list-ref`, `defun-q-list-set`, `vl-symbol-value`, `vl-symbolp`, `vl-consp`, `vl-load-com` (which does nothing, so that a routine that starts with it loads) |

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
AutoCAD's does at its own limit, and neither `*error*` nor `vl-catch-all-apply` sees it. The presenter keeps running.

### Speed

A loop of a million steps, `(repeat 1000000 (setq i (1+ i)))`, takes 48 ms here and 157 ms in AutoCAD 2021, both measured on
one machine, the presenter's a Release build.

## Typing at the command line

A line that starts with `(` or `!` is AutoLISP, at the Command prompt and at any prompt that does not ask for text. `!p`
writes the value of `p`. A form with an open parenthesis or string waits under the prompt `(_>`, one `(` more for each open
parenthesis and a `"` for an open string; Enter adds an empty line and Esc drops it. At a command's prompt, a value answers
it: a list of two or three numbers is a point, a number or a string is typed as it is, and nil asks again. `(defun
c:name ...)` makes `NAME` a command in either case: built-in names win, then `c:` commands, then aliases.

A line is taken whole: Space types a space in it, and no word of it is read as an object snap. A bare word or number is
still a command: `1` at the Command prompt is not the number 1.

What an evaluation writes comes first, then its value on the same line, as AutoCAD writes them: `(princ "hi")` writes
`hi"hi"`, where `(progn (princ "hi") (princ))` writes `hi` alone, the empty `(princ)` returning nothing to write. A new
line in the text starts a new line of the command line.

A `c:` command can be typed in either case and is offered by AutoComplete from its first letters. Enter repeats it, as it
does any command. A command that changes nothing leaves no undo step.

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

**APPLOAD** (Manage tab, Applications, Load Application) shows a file dialog titled Load/Unload Applications with the
filters AutoLISP files, Plugins and All files. A `.lsp` is loaded into the drawing shown, and writes what the file wrote and
then `name.lsp successfully loaded.`; a `.dll` goes to the [plugin manager](plugins.md), as PLUGINLOAD does; any other
file is refused with `name.txt is not an AutoLISP file or a plugin.` Cancelling the dialog does nothing. The Plugin manager
itself is on the Plugins tab. From code, `CadCommandSession.LoadLisp(path)` does what APPLOAD does for an AutoLISP file.

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

The Startup Suite is kept in the settings file, under `support`, next to the search folders. There is no page for it yet:
edit the JSON.

```json
"support": {
  "searchFolders": [ "D:\\Lisp" ],
  "lispStartupSuite": [ "D:\\Lisp\\layers.lsp", "D:\\Lisp\\dims.lsp" ]
}
```

Blank entries are left out and a file named twice is loaded once.

## Each drawing its own variables

A variable or function a routine defines belongs to the drawing it was loaded into, as AutoCAD's does with LISPINIT at 1:
`(setq a 1)` in one drawing, and `!a` in another writes `nil`. A routine that serves every drawing is in a startup file,
which loads into each as it opens. The `c:` commands of a drawing are those of its own interpreter, and the other drawing
types them as an unknown command until it has loaded the file too.

## What is not there yet

AutoLISP is built in stages, and a function that belongs to a later one is not defined yet. Calling it writes what AutoCAD
writes for a name it does not know, `; error: no function definition: NAME`, so a routine that uses only what is listed
above runs, and one that reaches for the drawing stops at its first such call. A routine that defines its commands loads
whole, and their names are offered by AutoComplete, whatever their bodies need.

| Stage | What it brings |
|---|---|
| L2 | the drawing: `command`, `getpoint`, `getvar`, `setvar`, `entget`, `entmod`, `entmake`, `ssget`, the selection set and entity functions, `tblsearch`, `trans` |
| L3 | the `vl-` functions of strings and files, `open` and `close`, the blackboard |
| L4 | the ActiveX functions, `vla-` and `vlax-` |
| L5 | dialogs, DCL: `load_dialog`, `new_dialog`, `start_dialog` |
| L6 | trusted folders and the secure-load check; for now `load` reads any file it is given |

A routine that opens with `(vl-load-com)` loads today: that call is defined and does nothing.
