# Fonts and text

## Fonts

A drawing reads no font unless it is told where to look. `DxfDatabase.FontSearchPath` takes folders separated by semicolons,
searched in their order as AutoCAD searches its support path, and is empty when a drawing is opened or made: empty, nothing is
read from the disk, nothing is logged, and every entity is laid out as the library lays it out without fonts. Given folders,
the SHX or TrueType font of a text style (`FileName`, `txt` read as `txt.shx`, `arial.ttf`) is read once from the first
folder that has it, and the words of the drawing are measured in it where AutoCAD 2021 needs their width, as checks F1a and
F2 measured AutoCAD doing it:

```csharp
DxfDatabase db = DxfDatabase.Open("plan.dxf");
db.FontSearchPath = @"C:\Program Files\Autodesk\AutoCAD 2021\Fonts;D:\Fonts";
var text = new DxfDBText { TextString = "ABC", Height = 4, HorizontalMode = DxfTextHorizontalMode.TextCenter, AlignmentPoint = new GeoPoint3(100, 50, 0) };
db.ModelSpace.AppendEntity(text);   // Position (94.667, 50, 0): the ink of "ABC" in txt.shx is 10.667 wide
```

- A TEXT, ATTRIB or ATTDEF justified other than left on its baseline stands where its words put it about its second point,
  worked out again when its words, second point, height, width factor, turn, justification or style are set, and when one
  made in code is added: across, by the box of its ink (from the start of the baseline to the last pen where the words end
  in a space, so that romans "A " centres on 0 to 7.429), up by the height, half of it or the font's depth below its
  baseline. An aligned text takes its height from its two points, a fitted one its width factor: "ABC" of txt.shx aligned
  on two points 30 apart is 11.25 high. An annotative one is put so at every context. Its `GeometricExtents` are the ink of
  its words, the corners slanted by its oblique: "A" 4 high slanted 15 degrees reaches 3.738.
- An MTEXT gives the `ActualWidth` and `ActualHeight` of its words, plain letters and the codes `\W`, `\H`, `\T`, `\A`,
  `\L`, `\O`, `\K`, `\C` and `\~`: "ABC" of txt.shx 4 high is 10.667 wide and 4 high, "   ABC" 22.667 wide, its spaces
  before counted and those after left out, and "g" 5.333 high. Its paragraphs are laid out in lines, wrapped at its width,
  as the next list says, and its `GeometricExtents` and `Explode` follow them.
- A multileader lands a leader on the right of its words, the gap past their ink: "ABC" at (100, 50) met from (250, 0) by a
  dogleg from (120.67, 48). Words whose ink reaches below their baseline are met lower, "Ag" 2.67 below its top at the middle
  of its top line; words met on the right move to keep that landing when their height, the scale or the words change;
  mirrored words are kept readable, and those mirrored across their plane keep their left edge as they grow (check D38).
  Words given in a text style other than the multileader's are moved to where its font would meet them, as AutoCAD 2021
  moves them, so that a leader is landed by that font: "Ag" of arial.ttf 4 high given at (100, 50) to a multileader of
  txt.shx stand at (100, 49.92), a leader from the left landed at (90, 47.33).
- A locked attribute definition is taken into a flip and kept readable, its box mirrored: "L1" 5 high at (20, 60) flipped
  about x = 45 stands at (63.33, 60). A leader whose words sit above its line explodes with its hook running on to their end.

A TrueType font (.ttf, .ttc or .otf of glyf outlines) is measured by the boxes its letters' glyf headers give, as AutoCAD 2021
measures it (check F2):

- The height of a text is the top of the font's A, not its capital height or its em: Arial's A reaches 1466 units up, so a
  text 4 high is 4/1466 of a unit to the unit, and Times New Roman's 1387, though its H stops at 1356. No pair of letters is
  kerned: "AV" is as wide as A's advance and V's.
- A TEXT is boxed from the start of its baseline, or its ink left of it, to its last pen, or its ink past it: "H" of Arial 4
  high is centred on 0 to 4.035, its advance, not on its ink from 0.447 to 3.585, and "ABC" centred on (100, 50) stands at
  (94.259, 50). Each letter is boxed from the lower of its bottom and the baseline, as high as it is, and up to the baseline
  at least: the degree sign, inked from 2.554 to 4.068, is boxed from 0 to 1.514; a space a unit of the font below the
  baseline. At its bottom a text stands on the font's `usWinDescent`. `%%c` is the font's diameter sign U+2205, else its
  U+00D8, and `%%u` and `%%o` draw no line in the box.
- An MTEXT of one line leaves the ink left of its start out: Arial "A" 4 high is 3.735 wide. A multileader met on the right
  lands at that width, "ABC" of Arial at (100, 50) from (250, 0) by a dogleg from (121.27, 47.97), its C reaching 25 units
  below the baseline. A flip mirrors the box of an attribute definition's words without the spaces at either end.
- A style that names a typeface (`DxfTextStyleTableRecord.Font`) is measured where its file is that typeface, bold and italic
  as the style says, as AutoCAD draws such a style by its typeface: arial.ttf named Arial bold is not measured, since AutoCAD
  draws it in arialbd.ttf. A collection (.ttc) is measured by its first face where the style names that face's typeface;
  AutoCAD loads none of a style that names no typeface.

A style's big font (`BigFontFileName`: gbcbig.shx, chineset.shx, extfont.shx) is read from the same folders, and its letters
are drawn beside the style's SHX font as AutoCAD 2021 draws them (check F3):

- A text's letters are read in the code page AutoCAD gives the big font's name: GBK for gbcbig.shx, Big5 for chineset.shx,
  Shift-JIS for bigfont, extfont, extfont2 and @extfont2.shx, and Korean for whgtxt, whgdtxt, whtgtxt and whtmtxt.shx,
  named bare, in any case, with or without `.shx`. So 中 is gbcbig.shx's letter D6D0 and chineset.shx's A4A4. AutoCAD reads
  a big font of any other name, or one named with its folder, in no code page: its texts are measured where every letter is
  ASCII. On .NET, not .NET Framework, the code pages need `CodePagesEncodingProvider` registered, as a DXF of them does.
- A letter of two bytes the big font has is drawn from it before the style's font: Ж beside extfont.shx is its own. The
  degree, plus-minus and diameter signs stay the font's. A byte alone is the font's letter, else the big font's (the
  half-width kana of extfont.shx). A space is the big font's where it has one: extfont.shx's is 9 of 15 wide, so "A B" of
  txt.shx 4 high is 9.067 wide, not 10.667. A byte that opens a letter of two takes the next letter with it (special.shx's
  "~!"). A letter neither font has is the big font's question mark where it has one, else the font's.
- The big font's letters are as high as its height above. gbcbig.shx's is nought, read as 8, so 中 4 high is half a unit to
  the unit, and "A中B" of txt.shx and gbcbig.shx centred on (100, 50) stands at (95.211, 50). A TEXT at its bottom stands on
  the big font's depth: gbcbig.shx's 64 of 8 puts it 32 up at 4 high, whatever its letters. The shapes of an extended big font
  (extfont.shx) are stretched into the box each letter gives them, and a whole circle of a big font is not drawn
  (chineset.shx's full stop is its dot alone).
- A TEXT of txt.shx and gbcbig.shx "中A" is boxed from 0, not from 中's ink at 0.529: the first letter of the font after letters
  of the big font, where it puts its pen down before it moves, is inked where they began. An MTEXT is not boxed so, and
  leaves ink left of its start out (whgdtxt.shx's "한국어" 4 high is 14.083 wide, though inked from -0.208).

Not measured beside a big font: a font of shapes (times.shx beside gbcbig.shx), a letter of a shape the big font draws from the
style's font (special.shx's), a multibyte code (`\M+`), which AutoCAD draws by its two bytes whatever its code page, and in an
MTEXT a letter neither font has and the ideographic space, which AutoCAD draws from other fonts or spaces otherwise. A TrueType
style's big font is not drawn at all. Of AutoCAD 2021's ten Chinese, Japanese and Korean big fonts read in place, 6 910 of 6 910
letters are boxed as AutoCAD boxes them, and 158 of 158 letters with whole circles. Of the TEXTs of checks F3 and F1a in styles
with a big font, 15 382 measured give AutoCAD's place and box and 68 do not: 64 that the font gives without a big font too
(isocp.shx's stand-in for a letter it has not, romans.shx's Ą, txt.shx's "i" aligned on two points) and 4 of an ideographic
space alone aligned on two points.

An MTEXT of several lines is laid out as AutoCAD 2021 lays it out (check F4), in any of the fonts above:

- A paragraph (`\P`) begins a line, and where the MTEXT has a width its words wrap at it. A line breaks after the spaces
  between words, which stay at the end of the line they break, and before or after a Chinese, Japanese or Korean letter,
  but not before a sign a line does not begin with (，。、；：？！．, the closing brackets and quotes, …, ・, ー, 々) nor
  after an opening bracket, which take the letter next to them along. A word moves down where the line would reach the
  width, the line measured from its start to where the pen stands at that word and that word's own ink: "H H t" of
  romans.shx 4 high is inked 17.524 wide but fits a width of 17.91 and not of 17.9. A word longer than the width stays
  whole on a line of its own, and the MTEXT is then wider than its width. Spaces before the first word are a word of their
  own: "  ABC DEF  GHI" of txt.shx 15 wide begins with a line of its two spaces. An empty paragraph at the end is no line,
  but a second one is: "ABC\P" is 4 high and "ABC\P\P" 10.667.
- Spaced at least (`LineSpacingStyle` `AtLeast`), the first baseline stands the first line's height, or the top of its ink
  where that is higher, below the top; the top of each line after it stands the factor (`LineSpacingFactor`) times the top
  of the line before, a third of that line's height and a third of the MTEXT's `TextHeight` below the top of the line
  before. Lines of one height 4 are 6.667 apart, as 5/3 of the height says; "ABC\P{\H3x;g}\PDEF" of txt.shx 4 high has its
  baselines 14.667 and 9.333 apart and is 28 high, the last line's ink below its baseline counted. Spaced exactly, the
  baselines stand the `TextHeight` below the top and the factor times 5/3 of it apart, and the MTEXT is its height, that
  spacing for each line after the first and a third of its height high, whatever its letters: "ABC\PDE" 4 high is 12 high.
- The `ActualWidth` is that of the widest line. The `GeometricExtents` are the box as wide as the width, or the
  `ActualWidth` where it has none, and `ActualHeight` high, held by the attachment at the location: "ABC\PDE" 41 wide held
  at its middle centre at (10, 20) is boxed from (-10.5, 14.667) to (30.5, 25.333). Each line stands on its own at the left
  of the box, centred on the location or ending at it, as the attachment says, its ink at that edge.
- `Explode` gives a TEXT for each run of letters of one height, width, spread, hold, line and colour of each line, held at
  its left where the pen stands at the run, as AutoCAD 2021 explodes the MTEXT: "A\H2x;B\H0.5x;C\PDEF" of txt.shx 4 high
  at (10, 20) gives A at (10, 12), B 8 high at (14, 12), C at (22, 12) and DEF at (10, 4). Letters held at the middle or
  top of a line of taller ones (`\A1;`, `\A2;`) stand up by half the difference or all of it, a line under, over or
  through them is written `%%U`, `%%O` or `%%K`, and a run of spaces between runs is a TEXT of its own; a colour within the
  words splits the runs but is not given to the TEXTs. A drawing of R12 is given the same TEXTs.
- A line under letters (`\L`) reaches a fifth of their height below the baseline and one over them (`\O`) a fifth above
  their height: "\LABC\l DEF" 4 high is 4.8 high. A spread (`\T1.5;`) lengthens each move of the pen after a letter; a
  TrueType font's spread is not measured, AutoCAD spreading it otherwise than its advances say.

Not laid out: slanted letters (`\Q`), which AutoCAD boxes otherwise in an MTEXT than in a TEXT, other fonts (`\f`, `\F`),
stacks (`\S`), indents and tabs (`\p`), columns, and of a TrueType font a change of width or height after the first
letter; their MTEXTs keep groups 42 and 43 and explode a TEXT a paragraph, as with no fonts. A multileader is landed by
its words of one line only. Of 14 750 MTEXTs AutoCAD 2021 measured in five rounds, 14 234 are laid out, and 14 082 of
them give AutoCAD's sizes, extents and every exploded TEXT; the most of the others explode with %% codes, where AutoCAD's
own TEXTs do not follow its lines, or are of isocp.shx, whose heights are up to 0.01 off as they were for a line.

The results follow the fonts supplied, so they differ between machines and between versions of a font, as AutoCAD's drawings
do. Fonts of shapes are not measured, nor the lines of `%%u`, `%%o` and `%%k` of an SHX font
and of `%%k` of a TrueType one; nor TrueType fonts of CFF outlines or of symbols (Symbol, Wingdings, some of whose letters
AutoCAD draws from other fonts), a style named by its typeface alone, a letter a TrueType font has not, which Windows may
draw from another font, and in an MTEXT a change of width or height after its first letter; their entities stay as they are
without fonts. Windows draws `%%c` of a font that has no U+2205 from the fonts it links that font to, where it links it
(Tahoma, Segoe UI, Malgun Gothic, SimSun): the library, which reads no such link, draws the font's U+00D8. A font in none of
the folders is said once at Debug in the log, and a file that is found but is no font, or cannot be read, once at Warn;
nothing throws. No other folder is looked in, not the Fonts folder of Windows, and no font is put in for another as
AutoCAD's FONTALT puts one.

## Text

A TEXT is a `DxfDBText` (`TextString`, `Position`, `AlignmentPoint`, `Height`, `Rotation`, `WidthFactor`, `Oblique`,
`HorizontalMode`, `VerticalMode`, `IsMirroredInX` and the rest of AutoCAD's) and an MTEXT a `DxfMText` (`Contents` with
its codes, `Text` without them, `Location`, `TextHeight`, `Width`, `Attachment`). `Contents` reads a letter written as its
escape as the letter, as AutoCAD 2021 reads it in a drawing of any version, where a drawing of R2004 or older writes so
each letter the code page has not: `a\U+0110b` is aĐb, and `\U+D83D\U+DE00`, the escapes of the two halves of a letter
beyond U+FFFF, is that letter, U+1F600. Every escape of four digits is read so, before the formatting, `\u+0110` too, so
that `\\U+0110` is a backslash and Đ, `\U+0041` an A and `\U+005CP` a new paragraph, as AutoCAD reads them; only
`\U+0000` and an escape of fewer digits are kept as written. A multibyte code, `\M+`, the number of a code page (1 Shift-JIS,
2 Big5, 3 Wansung, 4 Johab, 5 GB 2312) and two bytes in four digits, is read as AutoCAD 2021 reads it, in every text and
name: `\M+182A0` is the Japanese letter あ, `\M+10041` an A and `\M+1A400`, a letter alone and nought, that letter,
U+FF64; one whose two bytes are letters alone, as `\M+1A4A1`, one with a byte that is no letter of the page (a letter of
private use alone, as FF in Shift-JIS, counted as none), and one of a code page the runtime has not (.NET without
CodePagesEncodingProvider) are kept as written. AutoCAD gives each code of bytes that are no letter a letter of its own,
U+E800 and on, in the order it meets them in all it has read since it started, `\M+1FFFF` U+E800 in one session and
U+E809 in another, so the library cannot give the same and keeps the code. A caret and the character after it are read
before the escapes, as AutoCAD reads them in every text: `^J` a line feed, `^I` a tab, `^` and a space a caret, any other
the character 64 below it (`^a` an exclamation mark, `^4` U+FFF4), and `^@` ends the words, so `x^Iy^@z` is x, a tab and
y. A TEXT's and an attribute's `TextString`, a dimension's
`DimensionText`, a MULTILEADER's words and a table cell's `TextString` and `Value` read them the same way. `Text` leaves
`\U+0000` out, a backslash before it with it, and reads a code kept as written whose first byte is a letter alone as that
letter, U+FF64 of `\M+1A4A1`, as AutoCAD's `Text` does. Set, a caret is written `^` and a space and a tab `^I`, as
AutoCAD writes them, so `\S1^2;` is written `\S1^ 2;` and `m^2` reads back as m^2; the letters
are written as they are from R2007 on, but one beyond U+FFFF written as the escapes of its halves, and as escapes before,
as `TextString` writes those of a TEXT. Words that hold the seven characters `\U+0110` themselves cannot be kept in any
DXF: every reader reads them as Đ, and AutoCAD's own writer writes them as they are too. `LineSpaceDistance` is the spacing factor
times single spacing, 5/3 of the letters' height: 5 for letters 3 high. `SetAttachmentMovingLocation` holds the MTEXT by
another of its points where it stands, by its width and the height of its words (43): the MTEXT 40 wide at (20, 40, 0) held at
its top centre stands at (40, 40, 0). `ShowBorders` draws its text frame, and `BackgroundTransparency` is a
`DxfTransparency`. `BackgroundFill` or `UseBackgroundColor` turned on writes the background's colour (63) and scale (45)
where the MTEXT has none, BYLAYER and 1.5, and from R2007 its transparency (441) by layer: AutoCAD 2021 refuses the whole
file over an MTEXT whose background is on without a 63 and a 45. A text's, an attribute's and an MTEXT's style is set by its name, `TextStyleName`, written as the
drawing's table names it, letters of either case the same, or as its record, `TextStyle` (AutoCAD's `TextStyleId`):
`text.TextStyle = db.TextStyleTable["GH_TXT"]` writes `7 GH_TXT`. A style the drawing has not, or a record of another
drawing, is refused. A text, an MTEXT or an attribute made in code takes any style name, and is refused when it is added to a
drawing that has no style of that name, by `AppendEntity`, by `AppendAttribute`, or as an INSERT's attribute when the INSERT is
appended; a copy of a drawing that has the style brings it instead. In a drawing of R12 an MTEXT of a style the drawing has not
is refused before it would become TEXTs.

A TEXT, an attribute definition or an attribute not aligned left on its baseline is held by its `AlignmentPoint`, the
origin where its record gives none, and always writes it, as AutoCAD 2021 wrote `11 0 21 0 31 0` for the TEXT it made
centred on its middle with no alignment point given (check D30): `Justify`, `HorizontalMode` and `VerticalMode` give the
origin to a text of none, and `AlignmentPoint` set to the origin writes it. Moved or placed, such a text carries its
alignment point, the origin among them: an attribute definition centred on (0, 0) gives the attribute
`SetAttributeFromBlock` fills for an INSERT at (6000, 1000) the alignment point (6000, 1000), where the library wrote none
and AutoCAD put the attribute at the world's origin.

An MTEXT is set in columns as AutoCAD sets one: `SetStaticColumns(12.5, 2.5, 3)` is three columns 12.5 wide with gutters of
2.5, 42.5 across, the MTEXT's group 41 (and 40 of its embedded object) made 42.5 from R2018 and 12.5, the width of a
column, before it, as AutoCAD 2021 wrote two columns 50 wide with a gutter of 10 as 110 in R2018 and 50 in R2000, and so
do `SetDynamicColumns`, `ColumnCount`, `ColumnWidth` and `ColumnGutterWidth`, over the columns the MTEXT counts (AutoCAD
counts dynamic columns as it lays the words out, which the library does not); an MTEXT copied from one form to the other
takes the other width. `Width` is 42.5 in either form, as AutoCAD 2021 reads an MTEXT in columns before R2018 as wide as
all its columns with their gutters, though its 41 holds a column's: its own 09-a of R2000, 41 50 on each, gave 110 for two
static columns, 770 for thirteen dynamic ones and 170 for three of their own heights; one of no columns wraps at its 41.
`Width` set on an MTEXT in columns before R2018 leaves it as it is, as AutoCAD 2021 saves one: given the width 80, its two
static columns of R2000 and of R2007 read 80 until they were saved, and were saved with 41 50 and their columns as they
were, 110 wide when opened again; the library's drawing is its file, so it reads 110 straight away, and the columns are
given their widths by `ColumnWidth`, `ColumnGutterWidth` and `ColumnCount`. Before R2018 `ACAD_MTEXT_DEFINED_HEIGHT` is written where the height is above nought, the first
column's for columns of their own heights and each later column's on its own MTEXT, as AutoCAD 2021 writes it, and none
for a height of nought; `SetDynamicColumns`, `SetColumnHeight` and the column properties (`ColumnType`, `ColumnCount`,
`ColumnWidth`, `ColumnGutterWidth`, `ColumnAutoHeight`, `ColumnFlowReversed`) the rest, written in its embedded object from
R2018 and in the extended data of ACAD before (`ACAD_MTEXT_COLUMN_INFO`, `ACAD_MTEXT_DEFINED_HEIGHT`), as AutoCAD and ezdxf
write them. Before R2018 the columns after the first are MTEXTs of their own, linked to the first (`ACAD_MTEXT_COLUMNS`),
which carries them: moved, turned, given a new normal, letter height or box height, or erased, it takes them with it, while
its `Contents` holds its own column's words alone. They are part of that MTEXT, as AutoCAD's database has them: a block walks
and counts the main MTEXT alone, so that moving every MTEXT of model space moves each column once; an MTEXT that lists its
own handle among its columns is walked still. A copy of the first holds the words of them all and links to none, so no two
MTEXTs claim the same columns; and `Height`, before R2007, is the height of the columns in the extended data of ACAD, 30 for
ezdxf's three static columns 30 high, and the first column's for dynamic columns of their own heights, 15 for AutoCAD's
columns 15, 25 and 0 high, as AutoCAD 2021 read them; from R2007 such columns with no group 46 are of no height, as it read
the library's. Before R2007, which brought columns, the column data are columns only with those linked
MTEXTs: AutoCAD 2021 read three MTEXTs of R2000 given the column data alone as of no columns, each 50 wide, the one of a
defined height of 15 that high, and so does the library, which refuses columns to such an MTEXT (`SetStaticColumns`,
`SetDynamicColumns`, `ColumnType`), as it makes no column MTEXT.

The words of a text can hold fields, kept as AutoCAD keeps them: a `DxfField`, AutoCAD's `Field`, is a FIELD the object's
extension dictionary names in its dictionary ACAD_FIELD under the property TEXT. `GetField()` gives the field of an object's
words, `GetField(propertyName)` that of another property, and `SetField(new DxfField("Drawn %<\AcVar Date>%", true))` gives
a TEXT, an MTEXT, an attribute or a dimension one, from R2004 on, written in the groups the DXF reference gives a FIELD: the
text field (`_text`) of the code `Drawn %<\_FldIdx 0>%` and its child of AcVar, each owned by what names it, marked not
evaluated, and the class FIELD listed. That layout has not yet been read by AutoCAD itself: its state (94 of 0), the empty
value's flag (93 of 1) and the format of R2004 (group 4, which libredwg reads and the library does not write) are to be
checked on a drawing AutoCAD saved, and so is which group `GetStringValue()` reads the value AutoCAD kept from (302, else 1;
the 301 after it is not read). A field made of an object's property, `%<\AcObjProp Object(%<\_ObjId 2130>%).Area>%`, names no
object here and cannot be evaluated. A field or one it holds already given to an object is refused. The words of the text are not changed; the library does not evaluate a field, and AutoCAD writes its value into
them when it next evaluates its fields. `RemoveField()` takes a field out with its children, out of the drawing's
ACAD_FIELDLIST too where it has one, and ACAD_FIELD with it once it is empty. `GetTextWithFieldCodes()` of a TEXT and
`GetMTextWithFieldCodes()` of an MTEXT give the words with the codes of their
fields in their places, `Drawn %<\AcVar Date \f "dd/MM/yyyy">%`, and `ConvertFieldToText()` makes the fields words, keeping
what AutoCAD last showed. A dimension's `FieldFromMText` and `FieldToMText` copy the field of its text from and to an MTEXT of
its own drawing, every group of it kept and a field two others name copied once, and `RemoveTextField` takes it out. A field's
`GetChildren()` gives its children in the order of its 360s, null in the place of a handle that names no FIELD, so that each
keeps the index of its placeholder `%<\_FldIdx n>%`.

An object can be annotative, which AutoCAD draws at the size its scale gives it on the sheet. The drawing's list of scales
is `db.ObjectContextManager.GetContextCollection("ACDB_ANNOTATIONSCALES")`, AutoCAD's `ObjectContextCollection`: each a
`DxfAnnotationScale`, a SCALE of ACAD_SCALELIST with its `Name`, `PaperUnits` and `DrawingUnits`, so that 1:50 is 1 to 50 and
its `Scale` 0.02, in the order of the dictionary; `CurrentContext` is the one `Cannoscale` names, and renaming it renames
CANNOSCALE, refused before anything is written where the drawing cannot write that variable.
`AddContext(new DxfAnnotationScale { Name = "1:100", PaperUnits = 1, DrawingUnits = 100 })` adds a scale under the entry
name AutoCAD 2021 gives one, `*A1`, `*A2` and on, one more than the highest the list has or was given while the drawing is
open, so that a number taken out is not given again (AutoCAD's own scales, named A0 to B6, are read as they are), the list
made where a drawing of R2007 or later has none, and a scale of as many units of the paper as of the drawing made the unit
scale where the list has none; `RemoveContext("1:100")` takes it out, refused for
the unit scale, the current scale and a scale an object points at. `Annotative` reads the extended data AcadAnnotative of a
TEXT, an MTEXT, an attribute, a dimension, a leader, a multileader (or its own 293), a tolerance, a hatch, a style or a block,
an INSERT reading its block's, and gives `NotApplicable` for a line. Set true, it makes a text style or a dimension style
annotative by that data alone; an entity is refused, since AutoCAD shows an annotative entity at a scale by a context record
of the OBJECTS section that the library does not write until that record is checked on a drawing AutoCAD saved. `HasContext`
reads an object's contexts, of any kind, under ACDB_ANNOTATIONSCALES of AcDbContextDataManager of its extension dictionary, by
the scale each points at (its 340), and none of an object never annotative, as a LINE or a CIRCLE handed a TEXT's contexts by
`HandOverTo`, which keeps them, as AutoCAD 2021 reads it; `RemoveContext` erases one, the default and the last refused, and `Annotative =
DxfAnnotativeStates.False` erases them all with the data. `PaperOrientation` reads whether an annotative object keeps the way
up of the sheet from the extended data AcadAnnoPO, a reading of AutoCAD's that is not yet checked either.

An annotative TEXT, attribute or attribute definition that has contexts is read where AutoCAD 2021 shows it once it has
read the drawing: at its context of the scale 1:1, else at its default context, whatever CANNOSCALE says. Its `Position`,
`AlignmentPoint` and `Rotation` are that context's, at the elevation of its record, and its `Height` the record's (which
holds the height at the default scale) times the default's scale over 1:1's: the attribute ATT_DVSUP of AutoCAD's sample
Mechanical - Metric, whose record says (0.678, 265.33) and whose context of 1:1 says (-6.151, 265.33), stands at
(-6.151, 265.33), and a text 3 high in a record whose default is 1:2 is 1.5 high. Setting one of those writes the context
it is read at, and its record too where that context is the default; `SetAttributeFromBlock` places the attribute so;
`TransformBy`, of the text or of the INSERT that holds the attribute, and so a dynamic property that moves the attribute,
carries every context with the record, each its point, its angle and, where the text is not aligned left on its baseline,
its second point. Its `HorizontalMode`, and so its `Justify`, is that of the context's 70 where the context holds one.

An annotative MTEXT is read so too: its `Location` is its context's 11, its `Direction` and `Rotation` its context's 10,
its `Width` its context's 40, its `Height` (the box) its context's 41 and its `Attachment` its context's 70, each that
context's own at its scale, not the record's scaled, and its `TextHeight` the record's times the default's scale over
1:1's. An MTEXT whose record says (10, 40), 40 wide, and whose context of 1:1 says (14, 44) turned 30 degrees, 30 wide and
held by its middle, reads so, and 1.25 high where its record is 2.5 at the default 1:2. Set, each writes its context, and
the record where the context is the default, the text height at the default's scale; `TransformBy` carries every context,
its location and direction, its width and box stretched: scaled by 2 about the origin, a context of 1:2 at (60, 44), 80
wide in a box 40, goes to (120, 88), 160 by 80. An INSERT of contexts stands at its context's point, turned by its angle
and scaled by its factors, so its `BlockTransform` and what it places: the valve of the sample whose record says (13.883,
279.776) unturned, and whose context of 1:1 says (113.883, 279.776) turned 45 degrees and scaled by 2, stands there.
`Position` and `Rotation` set write its context, and its record where that is the default; `ScaleFactors` set writes every
context, each the factors times the scale shown over its own (3 at 1:1 is 6 in the context of 1:2); `TransformBy` carries
each context from its own point, angle and factors. An INSERT of an annotative block, or an attribute of one, that holds no
context is shown at 1:1 (`HasContext`), and an ATTRIB is annotative as its INSERT is, whatever its own AcadAnnotative says;
in a block that is not annotative, an ATTRIB is annotative where its own data says 1 and it holds a context, as AutoCAD
2021 read the attribute it made of an annotative definition in such a block, and not where it holds none.
An annotative attribute made of several lines writes its MTEXT into every context, its second point the MTEXT's location
and the MTEXT's own context embedded after the 290, and `TransformBy` carries that MTEXT; its `MTextAttribute` is that of
the context it is read at, as tall as the attribute. Any attribute made of several lines (`IsMTextAttribute`) left,
centred or right on its baseline is held by the top left, centre or right of its MTEXT one text height above its point,
or its second point where it is not left, whatever the font, its first point kept: the value B-12 2.5 high at (50, 59)
becomes the MTEXT held by its top left at (50, 61.5).

A copy (`Clone`, `GetTransformedCopy`) of an annotative text, attribute, MTEXT or INSERT carries the contexts of its
original, and those of an INSERT's ATTRIBs, as AutoCAD 2021's copy keeps them: in no file it is shown at its original's
scales and reads where its original is read; added to a drawing, it is given an extension dictionary of those contexts, as
AutoCAD writes its copy's. Added to another drawing, by `AppendEntity`, `WblockCloneObjects` or `Insert`, each context stands
on that drawing's scale of its scale's name, as AutoCAD 2021 puts it: one of the same units as it is, one the list has not
added to it, and one of the name of other units added as the name and `_1` (S7 of 1:3 copied into a drawing whose S7 is 1:7
stands on an S7_1 of 1:3); an annotative INSERT's block comes annotative. A drawing older than R2007, which holds no list of
scales, takes the copy given the placing it is read at. Moved by
`TransformBy`, a copy carries every context. `GetTransformedCopy` of an MTEXT or an INSERT moves every context too, but
that of a TEXT, ATTDEF or ATTRIB moves the context it is shown at alone, and its record's point and angle only where that
is the default, as AutoCAD 2021 moves it: moved by (10, 5), the TEXT shown at (11, 12) at 1:1 beside its default of 1:2 at
(40, 12) is shown at (21, 17), its context of 1:2 left at (40, 12). An annotative MTEXT's `SetAttachmentMovingLocation`
moves it by the width of the context it is read at and writes that context, its record where it is the default; an INSERT's
`BlockTransform` set writes the context it is read at, as its point, angle and scale factors are. Scaled, an MTEXT's box
(46) is stretched with it, in columns or not. The point of a text not aligned left and an MTEXT's extents, which AutoCAD
works out from the font, are not read as AutoCAD reads them.

`Normal` set keeps what AutoCAD 2021 keeps, annotative or not: a TEXT, an ATTDEF, an ATTRIB, an INSERT, a SOLID, an LWPOLYLINE,
a POLYLINE and a HATCH keep their points, turn and contexts in their plane, the new normal written alone, so that a TEXT at
(11, 12, 0) turned 0.3 given (0, 0.6, 0.8) stands at (-11, -9.6, 7.2), still turned 0.3, and an INSERT at (300, 100, 0) at
(-300, -80, 60), its ATTRIBs carried with it; an MTEXT keeps its location and reads along the x axis of the new plane,
(-1, 0, 0); a LINE, a POINT, a CIRCLE and an ARC keep their points or centre in the world. A DIMENSION keeps its points in
the world, where AutoCAD lays its dimension line and text on the new plane. An attribute of letters 0.7 wide made of
several lines keeps its width factor, its MTEXT's words opening with `\W0.7000;`.

`DxfMText` names the codes of an MTEXT as AutoCAD's `MText` does: `ParagraphBreak` and `LineBreak` are `\P`, `UnderlineOn` is
`\L`, `HeightChange` `\H`, `BlockBegin` `{`, and so the 19 of them. `ContentsRTF` gives its words as RTF, the style's font
first in the font table, and `SetContentsRtf` reads RTF into them: paragraphs, columns, groups, fonts with bold and italic,
colours of AutoCAD's index, the lines under and through, tabs, line feeds (as `\line`), spaces that do not break and
letters are carried over, %% and what follows it kept as written, `x%%dy` and `x%%%y` as they are, as AutoCAD 2021's
`ContentsRTF` keeps them; the
heights, widths, slants, tracking and alignments of the letters, the paragraph's settings and the line over them are left
out, and a stack is its two parts with a slash between. `B-12\P{\C1;\LM16\l}` in the style Standard of the font txt is
`{\rtf1\ansi\deff0{\fonttbl{\f0{\*\ghfontfile}txt;}}{\colortbl ;\red255\green0\blue0;}B-12\par {\cf1 \ul M16\ulnone }\par}`,
a font's file marked by the group `{\*\ghfontfile}` other readers pass over and the last paragraph closed by a mark, which
`SetContentsRtf` leaves out; that RTF comes back as `B-12\P\C1;\LM16`, the same words in the same look, `abc\P` as `abc\P`
and `\Fromans;abc` as `\Fromans;abc`.

**Symbols.** `DxfTextSymbols` holds the symbols of AutoCAD's text editor, so that the words of a TEXT, an MTEXT, an
attribute or a dimension, written by hand or by an AI, take one without its code: the 21 letters its Symbol menu enters
and its help lists, each the letter itself, `Degree` °, `PlusMinus` ±, `Diameter` ∅ (U+2205, not the Latin letter Ø),
`AlmostEqual`, `Angle`, `BoundaryLine`, `CenterLine`, `Delta`, `ElectricalPhase`, `FlowLine`, `Identity`,
`InitialLength`, `MonumentLine`, `NotEqual`, `Ohm`, `Omega`, `PropertyLine`, `Subscript2`, `Squared`, `Cubed` and
`NonBreakingSpace`, and the menu's control codes `DegreeCode` (`%%d`), `PlusMinusCode` (`%%p`) and `DiameterCode`
(`%%c`), which the words keep as written and AutoCAD draws as the signs:

```csharp
var label = new DxfMText { Contents = DxfTextSymbols.Diameter + "20", Location = new GeoPoint3(0, 0, 0), TextHeight = 2.5 };
```

A letter is written as AutoCAD writes it: as it is from R2007 on, and before as itself where the code page has it (°, ±,
², ³ and the space that does not break, in ANSI_1252) and as its escape where it has not, so that ∅20 in an MTEXT of R2004
is `\U+220520`. Each of the 21, written by the library into an MTEXT, a TEXT, an attribute and a dimension of R2004 and of
R2018, was read back by AutoCAD 2021 as the same letter in all 210 readings, and AutoCAD wrote its own texts of them in the
same bytes. The four of the private range, `BoundaryLine`, `FlowLine`, `InitialLength` and `MonumentLine`, show only in
AutoCAD's own fonts. `%%c` is read as ∅, `Diameter`, by `Text` and `DxfReader`, as AutoCAD 2021 gives it in an MTEXT's
`Text`.
