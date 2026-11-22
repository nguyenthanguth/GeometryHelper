GeometryHelper CAD - DrawingTemplates folder
============================================

Save a drawing as a template (SAVEAS, Files of type: Drawing Template (*.dwt)) and put it in
this folder, and it is listed when you start a new drawing from a template: the New from
template button of the start screen, Ctrl+Shift+N, or the command NEW. Choose it, and the new
drawing, named Drawing1 and on, takes the template's layers, linetypes, text, dimension and
other styles, blocks, layouts with their page setups, and units. The template itself is not
changed. Ctrl+N (and QNEW) starts a blank drawing, or the default template named in Global
settings (Save and export > Default template for QNEW).

GeometryHelper-mm.dwt is a sample template, metric and in millimetres, made by the program's own
writer. It holds the layers Axis, Outline, Thin, Hidden, Dimension, Text, Hatch, Rebar, Title
block, Viewport and Defpoints (colours, linetypes and lineweights set; Viewport and Defpoints are
not plotted), the text styles Standard, Notes and Titles in ghsimplex.shx and Arial in Arial, the
dimension styles GH (current; text and arrowheads 2.5), GH-Annotative and Standard, the multileader
style GH, and the layouts Model and A3 (420 by 297 on its side). Use it as it is, or save your own
next to it.

Where to find it: Global settings > Folder preference lists this folder, with Open folder.
Save Drawing As to the Drawing Template type offers this folder first.

What is listed: the .dwt, .dwg and .dxf files in this folder, by name, and then those of its
subfolders, each in the same way, so a template at the top comes before one in a subfolder and,
of two with one file name, the first is the one listed. A template named with no folder (as typed
at NEW with FILEDIA 0, or as the default template for QNEW) is looked for here and in its
subfolders, then as typed.

A different folder: Global settings > Save and export > Template folder (TEMPLATEPATH) names
one, and it is used in place of this one.

An older Template folder beside the settings file (%AppData%\GeometryHelper\Drawing\Template)
is used only while it holds a template and this folder holds none. Put a template here and
this folder is used from then on.

This folder lies beside the program, so saving into it needs a program folder you may write
to. Where the program sits in a folder that is read-only for you (Program Files), name a
folder of your own in Template folder instead.

No template of Autodesk's (acad.dwt, acadiso.dwt and the others of AutoCAD's Template folder)
is shipped here, and the sample names no font of Autodesk's. NEW offers the program's own metric
and imperial templates through Open with no Template.
