# C8 part 2: fonts (SHX, TrueType, substitution)

Research note of 2026-11-09, and what was built from it. Repair of damaged files (C8 part 1) is in
[73-recover.md](73-recover.md).

## What was read

- **AutoCAD's documentation of shape files** ("About Shape Descriptions", "Special Codes", "About Big Font Files",
  "About Defining an Extended Big Font File", "About Unicode Font Descriptions"), as the library's own measuring reader
  (`src/GeometryHelper/Dxf/Engine/DxfShxFont.cs`, internal, checks F1a and F3 against AutoCAD 2021) reads them:
  - three compiled forms, each a first line ended by CR LF and 0x1A: `AutoCAD-86 shapes 1.0`/`1.1` (first and last
    number, a count, an index of number and length, then the shapes; shape 0 the font's name, above, below, modes),
    `AutoCAD-86 unifont 1.0` (a count of four bytes, the header's length, the header: name, above, below, modes, encoding,
    embedding; then each letter its Unicode number, length, name and codes), `AutoCAD-86 bigfont 1.0` (the entry size,
    a count, the number of escape ranges and the ranges, then an index of number, length and file offset; shape 0 above,
    below, modes, or five bytes in an extended big font whose fourth is the cell's width);
  - the codes: a byte of 0x10 and over is a vector, its length the high nibble (0 to F) and its direction the low (16
    directions, 0 east, 4 north, 8 west, C south, the odd ones half steps); 0 end, 1 pen down, 2 pen up, 3 and 4 the scale
    divided and multiplied by the next byte, 5 and 6 push and pop the place, 7 a subshape (one byte; two big-endian in a
    unifont; `0 hi lo` in a big font; `0 hi lo x y w h` in an extended big font, the subshape stretched from the cell to
    the box), 8 one XY move of signed bytes, 9 several up to (0,0), 10 (A) an octant arc (radius, then a byte of a sign and
    a size: clockwise bit, start octant, octant count, 0 a whole circle), 11 (B) a fractional arc (start and end offsets in
    256ths of an octant, radius high and low, octants), 12 (C) a bulge arc (dx, dy, bulge -127..127, 127 a half circle,
    the bulge 2H/D), 13 (D) several bulge arcs up to (0,0), 14 (E) the next command for vertical text alone, skipped with
    its arguments in horizontal text;
  - the text height is the font's "above": romans.shx is 21 above and 7 below, txt.shx 6 and 2; a big font of no
    height above is drawn 8 to its height (gbcbig.shx).
- **ezdxf** (MIT), `src/ezdxf/fonts/shapefile.py`, read from raw.githubusercontent.com, no code copied: the same three
  readers (bigfont refused there), a `ShapeRenderer` with a stack, a pen and a vector length, a `GlyphCache` per font with
  an advance-width cache, the space width from shape 32 else the A else the cap height; it puts the pen back on the
  baseline after each letter because its fractional arcs drift. It reads a unifont's subshape number little-endian, which
  AutoCAD does not (the library's checks found big-endian).
- **mlightcad/shx-parser** (MIT), `contentParser.ts`, `shapeParser.ts`: the bigfont index as above, an extended big font's
  subshape stretched into its box, the five-byte shape 0; the same fractional arc as ezdxf (it reads the radius as
  hi x 255 + lo, which looks a slip; we take hi x 256 + lo).
- **LibreCAD** draws text in its own LFF fonts (a text format of polylines and arcs per letter, `A` lines with bulges)
  and converts SHX by an external tool; QCAD likewise with CXF. Neither reads compiled SHX at run time. Taken: a letter as
  a list of polylines in font units, scaled at drawing time, which is what we hand to the canvas.
- **How AutoCAD finds a font**: the style table's group 3 (the font file; `txt` means txt.shx) and group 4 (the big
  font), a TrueType style also by its extended data (1001 ACAD, 1000 the typeface, 1071 the flags of bold and italic and
  pitch and family), which the library reads into `DxfTextStyleTableRecord.Font`. The file is looked for in the folder of
  the drawing, then the support file search path (the Fonts folder of the installation among it), then the program
  folder; TrueType fonts are those of Windows. A font not found is mapped by `acad.fmp` (font mapping file: name;name),
  else replaced by FONTALT (simplex.shx by default), else AutoCAD asks for one. The installation folder is in the registry
  under `HKLM\SOFTWARE\Autodesk\AutoCAD\R24.0\ACAD-4101:409\AcadLocation` (D:\Autodesk\AutoCAD\AutoCAD 2021 here).

## What was built (src/GeometryHelper.Dxf.Viewer/Fonts)

- `ShxFont` reads the three forms and draws each letter into strokes (polylines in font units, arcs in chords of at most
  11.25 degrees) and its advance, kept per letter; it follows the library's measuring reader in every choice it made
  against AutoCAD (big-endian unifont subshapes, fractional-arc angles in whole degrees, octant bytes of sign and size, a
  big font's whole circle not drawn, the extended big font's stretched subshape). It does not share that reader: it is
  internal to the core, which this round did not touch.
- `CadFonts` finds a font (the drawing's folder, then the folders of the setting, case-insensitive, `.shx` added to a bare
  name), substitutes by the table, replaces a font not found by the replacement font, says that once a font through
  `CadDocument.FontMissing`, keeps read fonts for every document by path, size and time (at most 48), and gives the big
  font's code page by its name as AutoCAD does (932, 936, 949, 950).
- `ShxText` lays a text out: each letter's strokes scaled to height / above, the next where the pen ended, the width
  factor across, the slant from the baseline, lines 1.25 heights apart (as the canvas sets TrueType text), the alignment,
  turned over for backwards, turned to the angle; a big font's letter by the text's bytes in its code page; a letter
  neither font has as the font's question mark; °, ± and Ø as 127, 128 and 129 in a font of shapes, ∅ first in a unifont.
- The display builder draws a TEXT, MTEXT or ATTRIB of an SHX style found as one `DisplayText2` that carries its strokes
  in drawing coordinates (`Strokes`), its width from the advances and its frame down to the font's depth; the canvas strokes
  them in the text's colour and line weight, never dashed. A TrueType style stays with the canvas.
- Global settings: `FontSearchFolders` (default `CadFonts.DefaultFolders`: AutoCAD's Fonts folders found under Program
  Files\Autodesk or a drive's Autodesk folder, newest first, then Windows Fonts), `ReplacementFont` (simplex.shx, as
  FONTALT), `FontSubstitutions` (as acad.fmp); the Fonts page lists them.

## Left for later

- Vertical text (a style's vertical flag, code 14 drawn): drawn across.
- MTEXT font switches after the first word, and bold and italic of a TrueType style: the canvas draws the family only.
- The registry is not read for AutoCAD's folder (netstandard2.0 has no registry without a package); common folders are.
- On .NET (not .NET Framework) a big font's code page needs `CodePagesEncodingProvider` registered by the app.
- The core's measuring reader and this one could become one, if the core made its reader public.
