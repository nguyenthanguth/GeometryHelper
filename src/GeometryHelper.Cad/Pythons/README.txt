GeometryHelper CAD - Pythons folder
===================================

Put .py files that define commands here: a function marked @command("NAME") becomes a command, and
cmd("CIRCLE", ...) types a command to the end. Every .py file directly in this folder loads once when the
app starts. The files in its subfolders do not: they are packages the .py files here import.

One sample is shipped: GH-DrawingInfo.py defines the command GHPYINFO and writes nothing when it loads. Type GHPYINFO and it
writes the drawing's name and folder, the current layer, the objects of model space by type, the number of layers and blocks,
and the extents; it changes nothing. A .py file in this folder starts the Python engine when the app starts (about half a
second), and with no Python installed the start writes one line, "Python files were not loaded: ...". Delete the sample if
you do not want either.
