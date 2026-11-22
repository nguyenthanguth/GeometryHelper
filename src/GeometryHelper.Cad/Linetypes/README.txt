GeometryHelper CAD - Linetypes folder
=====================================

Put linetype files (.lin) in this folder. LINETYPE and -LINETYPE find a file here by its name
(mine.lin, or just mine), and so does the Load or Reload Linetypes dialog.

GeometryHelper.lin is the built-in set of 39 linetypes, written out in the .lin format as a
model for your own files. It is a copy: changing it changes nothing, because the name
GeometryHelper.lin always means the built-in set. Copy it under another name to edit it.

Search order for a linetype file:

  1. The folders set in Global settings > Folder preference (Support file search folders),
     in the order listed.
  2. This folder and its subfolders, next to GeometryHelper.Cad.dll.
  3. The built-in linetypes.

Subfolders are read too, so you can keep fonts by company or project: the files directly in
this folder come first, by name, then those of each subfolder in the same way. A file at the top
comes before one in a subfolder, and of two files with one name the first is used. A subfolder
that is a junction or a symbolic link, or is hidden or a system folder, is not read.

The drawing's own folder is not searched for linetype files.

The built-in names BORDER, CENTER, DASHDOT, DASHED, DIVIDE, DOT, HIDDEN and PHANTOM (each with its 2 and X2) and
GAS_LINE have the dash lengths AutoCAD gives those names, so that a drawing looks the same in both programs. The file
here is in millimetres: CENTER is 31.75, -6.35, 6.35, -6.35 and HIDDEN 6.35, -3.175. Loaded into a drawing whose
MEASUREMENT is 0 (imperial) they are in inches instead, CENTER 1.25, -0.25, 0.25, -0.25, as AutoCAD takes acad.lin for
such a drawing and acadiso.lin for a metric one. A linetype the drawing has already is never changed by loading.
The ACAD_ISO...W100 types are in millimetres in both.

No file of Autodesk's (acad.lin, acadiso.lin) is shipped here.
