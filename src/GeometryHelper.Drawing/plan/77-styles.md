# 77 Style forms (C10)

The Text Style dialog, the Dimension Style Manager with its New, Modify, Override and Compare dialogs, the style boxes of
the Annotate tab, and the True Color tab of Select Color, coded from the design boards (project v45: `TextStyle`,
`DimStyleManager`, `DimStyleDialog` with its seven tab instances, `AnnotateTab`, `SelectColor` with `SelectColorTrue` and
`SelectColorRgb`).

## Commands

| Command | Aliases | What it does |
|---|---|---|
| STYLE | ST | the Text Style dialog; with no window, -STYLE's prompts |
| -STYLE | | name, font, height, width factor, obliquing angle, backwards, upside down, vertical; `?` lists the styles |
| DIMSTYLE | D, DST, DDIM | the Dimension Style Manager |

TEXT and MTEXT write in the current text style ($TEXTSTYLE): a style of a fixed height is not asked for one, and its width,
slant and mirroring are the text's own. DIMLINEAR, DIMALIGNED, DIMANGULAR, DIMRADIUS and DIMDIAMETER draw in the current
dimension style ($DIMSTYLE): its arrowheads, colours, lineweights, suppressed lines, text style and units.

## Text Style dialog

Title "Text style · file". Styles: the filter (All styles, Styles in use), the list (current ticked, the font after the
name), Set current, New…, Rename…, Delete, and the note that Standard, the current style and a style in use cannot be
deleted. Preview · name: the sample text drawn by the canvas's renderer on black. Font: Font name (SHX or TT mark), Font
style (TrueType; "Regular · not for SHX fonts" otherwise), Use big font, Big font, the note on the font folders. Size:
Height, the note that 0 asks each time. Effects: Upside down, Backwards, Vertical ("(not for this font)" for TrueType),
Width factor, Oblique angle. A change not applied shows "Unsaved changes to X. Apply redraws the N texts that use it, as one
undo step." Footer: "Current style: X · N styles", Cancel, Apply, Close.

## Dimension Style Manager

Title "Dimension style manager · file". Styles (current ticked, MODEL beside GH_MODEL_ styles), "List: All styles", the
preview and its description ("ISO-25 · arrows closed filled 2.5 · text 2.5 above the line · overall scale 1 · Used by 38
dimensions."), the GH_MODEL notice where the drawing has such a style, Set current, New…, Modify… (also a double click),
Override…, Compare…; Rename and Delete on the list's right-click menu (the board has no buttons for them). Footer "N styles
· double-click a style to modify it", Close.

## Modify, New and Override dialogs

Seven tabs; each field shows its variable beside the label. Fields and variables:

| Tab | Fields |
|---|---|
| Lines | DIMCLRD, DIMLTYPE, DIMLWD, DIMDLE, DIMDLI, DIMSD1, DIMSD2; DIMCLRE, DIMLTEX1, DIMLTEX2, DIMLWE, DIMSE1, DIMSE2, DIMEXE, DIMEXO, DIMFXLON, DIMFXL (greyed while DIMFXLON is off) |
| Symbols and arrows | DIMBLK1, DIMBLK2 (through DIMBLK and DIMSAH), DIMLDRBLK, DIMASZ; centre mark type and size (DIMCEN's sign and size); DIMARCSYM; DIMJOGANG |
| Text | DIMTXSTY (with "…" for the Text Style dialog), DIMCLRT, DIMTFILL with DIMTFILLCLR, DIMTXT, DIMTFAC (fraction height, greyed unless architectural or fractional), DIMGAP<0 (frame); DIMTAD, DIMJUST, DIMTXTDIRECTION, DIMGAP; DIMTIH · DIMTOH |
| Fit | DIMATFIT, DIMTMOVE, DIMSCALE, DIMUPT, DIMTOFL |
| Primary units | DIMLUNIT, DIMDEC, DIMFRAC (greyed unless architectural or fractional), DIMDSEP, DIMRND, DIMPOST, DIMLFAC, DIMZIN; DIMAUNIT, DIMADEC, DIMAZIN |
| Alternate units | DIMALT, DIMALTU, DIMALTD, DIMALTF, DIMALTRND, DIMAPOST, DIMALTZ, placement (all greyed while DIMALT is off) |
| Tolerances | DIMTOL · DIMLIM (method), DIMTDEC, DIMTP, DIMTM, DIMTFAC, DIMTOLJ, DIMTZIN (greyed while the method is None) |

Zero suppression is one list (None, Leading, Trailing, Leading and trailing) over bits 4 and 8 (DIMAZIN 1 and 2); the bits
of feet and inches are kept. The alternate units' placement "Below primary value" is `\X` at the end of DIMPOST, as AutoCAD
writes it; the Prefix and suffix box shows DIMPOST without it. The board's "DIMAPOST · DIMALTTD" for the placement was a
placeholder: DIMALTTD is the alternate tolerance's precision.

The description under the preview follows the tab shown; the notice says what OK does (the dimensions it redraws), the
GH_MODEL notice for a model drawing's style, or why OK was refused.

## Style boxes of the Annotate tab

Text: Style (the text styles, the current ticked, then "Text style…" under a line) and Height (the height of the next
TEXT). Dimensions: Style (the dimension styles, GH_MODEL_ marked "model", then "Dimension style…"). Leaders: Style (the
multileader styles; no dialog item, as no MLEADERSTYLE dialog is built; greyed where the drawing has none).

## Select Color, True Color tab

Color model HSL or RGB. HSL: the field of hue (0 to 360 across) and saturation (100 at the top to 0 at the foot) at
luminance 50, with a crosshair; the luminance bar of the hue and saturation picked, with an arrow; both clicked or dragged;
Hue, Saturation and Luminance boxes. RGB: a bar for each of red, green and blue, from none to full with the other two as
they are, a marker under it, and its box. The Color box takes "r,g,b". The field is drawn once per size, the luminance bar
again only when the hue or saturation changes, each RGB bar only when the other two change. OK writes the true colour as
before: group 420 with the nearest index in group 62.

## Checked

AutoCAD 2021 AUDIT on a drawing with four new text styles, four new dimension styles (one modified, DIMPOST `<> mm\X`,
DIMALT on, its text style ISO), three dimensions and two texts: 0 errors.
