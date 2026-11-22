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
  2. This folder and its subfolders, next to GeometryHelper.Cad.dll.
  3. The built-in patterns.

Subfolders are read too, so you can keep fonts by company or project: the files directly in
this folder come first, by name, then those of each subfolder in the same way. A file at the top
comes before one in a subfolder, and of two files with one name the first is used. A subfolder
that is a junction or a symbolic link, or is hidden or a system folder, is not read.

The drawing's own folder is not searched for pattern files.

No file of Autodesk's (acad.pat, acadiso.pat) is shipped here.
