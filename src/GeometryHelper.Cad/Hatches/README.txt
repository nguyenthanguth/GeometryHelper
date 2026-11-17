GeometryHelper CAD - Hatches folder
===================================

Put hatch pattern files (.pat) in this folder. The patterns in them are listed by the Hatch
dialog as files to take a pattern from.

GeometryHelper.pat is the built-in set of 48 patterns, written out in the .pat format as a
model for your own files. It is a copy: changing it changes nothing, because the name
GeometryHelper.pat always means the built-in set. Copy it under another name to edit it.

Search order for a pattern file:

  1. The folders set in Global settings > Folder preference (Support file search folders),
     in the order listed.
  2. This folder, next to GeometryHelper.Cad.dll.
  3. The built-in patterns.

The drawing's own folder is not searched for pattern files.

No file of Autodesk's (acad.pat, acadiso.pat) is shipped here.
