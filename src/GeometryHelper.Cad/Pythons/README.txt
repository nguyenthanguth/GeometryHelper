GeometryHelper CAD - Pythons folder
===================================

Put .py files that define commands here: a function marked @command("NAME") becomes a command, and
cmd("CIRCLE", ...) types a command to the end. Every .py file directly in this folder loads once when the
app starts. The files in its subfolders do not: they are packages the .py files here import.
