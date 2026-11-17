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
  2. This folder, next to GeometryHelper.Cad.dll.
  3. The built-in linetypes.

The drawing's own folder is not searched for linetype files.

No file of Autodesk's (acad.lin, acadiso.lin) is shipped here.
