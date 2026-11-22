GeometryHelper CAD - Lisps folder
=====================================

Put AutoLISP files (.lsp) in this folder, and the dialog files (.dcl) they use. Every .lsp
file in this folder, at any depth, is loaded into each drawing when it opens, so the commands and
functions in them are there without APPLOAD. A routine that changes the drawing, or reads a
file, runs for every drawing you open: put here only what you want everywhere.

Load order when a drawing opens:

  1. acad.lsp, once for the program (every drawing with ACADLSPASDOC 1).
  2. acaddoc.lsp, for every drawing.
  3. The .lsp files of this folder, in the order of their names, ignoring case, then those of
     each subfolder in the same way (depth first).
  4. The files of the Startup Suite (APPLOAD > Contents).
  5. The function S::STARTUP, if a file defined one.

An error in a file is written to the command line and the next file loads. A file at the top
loads before one in a subfolder; a subfolder that is a junction or a symbolic link, or is hidden
or a system folder, is not read. A subfolder is not on the search path below: reach a file in it
with (load "Sub/x"). acad.lsp and acaddoc.lsp put in this folder itself are found as those two
files and load once.
Files with the extensions .fas and .vlx are compiled AutoLISP, which the presenter cannot
run: each writes its refusal line and the rest load.

Search order for load, findfile and load_dialog with a name that is not a full path (this
holds for a name with a relative folder, as in "Sub/x" or "./x.lsp", too):

  1. The current folder and the drawing's folder, only with LEGACYCODESEARCH 1.
  2. The folders set in Global settings > Folder preference (Support file search folders),
     in the order listed.
  3. This folder, next to GeometryHelper.Cad.dll.

This folder lies under the program folder, which SECURELOAD always trusts: its files load
with no question at SECURELOAD 1 and 2.

No file of Autodesk's (acad.lsp, acaddoc.lsp, the files of AutoCAD's Support folder) is
shipped here. One sample routine is: GH-DrawingInfo.lsp defines the command GHINFO and writes
nothing when it loads. GHINFO reads the drawing and writes its name, units, current layer and
text style, the objects of model space by type, the number of layers and blocks, and the
extents; it changes nothing, and the same file runs in AutoCAD 2021. Delete it if you do not
want the command: whatever is here runs for every drawing.
