GeometryHelper CAD - Lisps folder
=================================

Put AutoLISP files (.lsp) in this folder, and the dialog files (.dcl) they use. Every .lsp
file directly in this folder is loaded into each drawing when it opens, so the commands and
functions in them are there without APPLOAD. A routine that changes the drawing, or reads a
file, runs for every drawing you open: put here only what you want everywhere.

Load order when a drawing opens:

  1. acad.lsp, once for the program (every drawing with ACADLSPASDOC 1).
  2. acaddoc.lsp, for every drawing.
  3. The .lsp files of this folder, in the order of their names, ignoring case.
  4. The files of the Startup Suite (APPLOAD > Contents).
  5. The function S::STARTUP, if a file defined one.

An error in a file is written to the command line and the next file loads. A file in a
subfolder is not loaded at start; load it with (load "Sub/x") in one of your routines.
acad.lsp and acaddoc.lsp put in this folder are found as those two files and load once.
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
shipped here, and no sample routine, since whatever is here runs for every drawing.
