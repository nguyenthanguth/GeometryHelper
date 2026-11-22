GeometryHelper CAD - Fonts folder
=================================

Put the fonts you want the drawings to use in this folder:

  - SHX fonts and big fonts (romans.shx, simplex.shx, gbcbig.shx, ...)
  - TrueType and OpenType files (.ttf, .ttc, .otf)

ghsimplex.shx is the presenter's own font, GeometryHelper Simplex, written out as a compiled SHX
file for you to use elsewhere, for example in the Fonts folder of AutoCAD. It is a copy: the
name ghsimplex always means the built-in font, so changing or deleting the file changes
nothing here.

A font is found by its name: a text style that names romans.shx finds romans.shx here, and a
text style of the typeface "Foo Sans" finds a font file whose family is "Foo Sans" even when
the font is not installed in Windows. Nothing has to be registered or restarted; a file added
is seen the next time the drawing is opened or the settings are applied.

Search order for a font:

  1. The folders set in Global settings > Folder preference, in the order listed.
  2. This folder, next to GeometryHelper.Cad.dll.
  3. The built-in font (GeometryHelper Simplex).

A TrueType typeface installed in Windows is found first, as before; the folders above are
looked in only for a typeface Windows does not have, and for a style's .ttf file name before
the Fonts folder of Windows.

The drawing's own folder is not searched for fonts.

No font of Autodesk's is shipped here. Copy yours into this folder yourself where your licence allows it.
