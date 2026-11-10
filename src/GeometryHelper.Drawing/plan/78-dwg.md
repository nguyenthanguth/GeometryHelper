# 78 Our own DWG reader (W1: the bit layer and R2000; W2: R2004 to R2018; W3: R2007; W4: the types left out)

A DWG is read by code of our own into the records the library already has for a DXF, so that everything built on a DXF
document works on a DWG: `DxfDatabase`, `DxfReader`, the presenter. Stage W1, done here, is the bit layer and the format
of AutoCAD 2000 (AC1015), which every later AutoCAD still writes on "Save as AutoCAD 2000". The later formats (W2, W3),
the entities left out and the writer follow; what each needs from this stage is at the end.

## Sources

| Source | Use |
|---|---|
| Open Design Specification for .dwg files 5.4.1 (the public PDF of the ODA) | the main source: the file layout, the bit codes, the check sums, the fields of each object |
| ACadSharp (MIT), commit `c780699` | read where the specification leaves the order or the meaning of fields open; a side check in scratch only, never a dependency. Its notice is in `src/GeometryHelper/THIRD-PARTY-NOTICES.md` |
| AutoCAD 2021 | the answer key: each test drawing is saved as DWG, read back and written as DXF by AutoCAD itself |
| LibreDWG, libdxfrw (GPL) | not read: no source of either was opened |

## Where the code is

| Path | What it holds |
|---|---|
| `src/GeometryHelper/Dwg/DwgBitReader.cs` | the bit codes over a byte array, or a range of its bits |
| `src/GeometryHelper/Dwg/DwgCrc.cs` | the 16-bit check sum |
| `src/GeometryHelper/Dwg/DwgContainer.cs` | version sniffing; the R2000 container: file header, section locator, the bytes of the header variables and the classes, the object map |
| `src/GeometryHelper/Dwg/DwgHeaderVariables.cs`, `DwgClass` | the header variables and the classes, decoded |
| `src/GeometryHelper/Dwg/DwgObjectReader*.cs` | one object at an offset decoded into a `DwgObject`: the groups of its DXF record, and the handles that tie it to others |
| `src/GeometryHelper/Dwg/DwgTranscoder.cs`, `DwgDxfWriter.cs` | the objects put in the order of a DXF file and written as one |
| `src/GeometryHelper/Dwg/DwgReader.cs` | the public face: `Read` (a `DxfDocument`) and `Open` (a `DxfDatabase`), from a path or a stream |
| `tests/GeometryHelper.UnitTest/Dwg` | the tests, the test drawings (`Data`, gzipped) and the comparer `DwgAnswerKey` |

DECIDED: the reader lives in the core library, namespace `GeometryHelper.Dwg`, beside `GeometryHelper.Dxf`; only
`DwgReader` is public.

DECIDED: a DWG is turned into the bytes of the DXF that says the same, and the DXF reader makes the drawing from them.
One model of a drawing, one set of rules; a DWG read is by construction what its DXF read would be, and the answer keys
compare text with text. The cost is one pass more in memory.

DECIDED: texts are not decoded on the way. The bytes of a DWG string are copied into the DXF and `$DWGCODEPAGE` is
written from the file header, so the DXF reader decodes them, `\U+XXXX` and `\M+nXXXX` included, exactly as it does for
a DXF.

DECIDED: the container and the decoding of objects are apart. `DwgContainer` hands over the header bytes, the class
bytes and the map of handle to offset; `DwgObjectReader` knows nothing of sections. The version is carried by the
container and is to become a parameter of the decoders in W2, where the fields differ by version.

DECIDED: a broken file is a `FormatException`, a version not read a `NotSupportedException` that names the version and
the release. `DwgReader` turns whatever else a decoder may throw on broken bytes into a `FormatException`, so a caller
catches two types.

## The R2000 file

| At | What |
|---|---|
| 0x00 | `AC1015`, six bytes |
| 0x0B | the maintenance version, a byte (`$ACADMAINTVER`) |
| 0x0D | the seeker of the preview image, a raw long |
| 0x13 | the code page, a raw short, as AutoCAD numbers them: 30 is ANSI_1252 |
| 0x15 | the count of section records, a raw long |
| 0x19 | the records, nine bytes each: number (byte), seeker (long), size (long) |
| after them | the check sum of the bytes so far (seed 0, XORed by a mask the count of records chooses), then a sentinel of 16 bytes |

The sections: 0 the header variables, 1 the classes, 2 the object map, 3 a second header (not read), 4 the template,
which holds `$MEASUREMENT`, 5 the auxiliary header (not read). The header variables and the classes each stand between
a start and an end sentinel: sentinel, size (raw long), data, check sum (raw short, seed 0xC0C1, over the size and the
data), sentinel.

The object map is a run of pages of at most 2032 bytes: the size as a big-endian short, then pairs of modular chars,
the step of the handle (unsigned) and the step of the offset (signed), both starting again from 0 in each page, then the
check sum as a big-endian short. A page of size 2 ends the map.

An object at its offset: the size as a modular short, the data, the check sum (seed 0xC0C1, over the size and the data).
The data: the type (bit short), the size of the object's own data in bits (raw long; the handles start there), the
handle, the extended data (size, application handle, items; repeated to a size of 0), for an entity the proxy graphics,
then the common part and the object's own fields, and from the bit named above the handles: owner, reactors, extension
dictionary, and for an entity layer, linetype, previous and next entity, plot style.

DECIDED: every check sum is checked. A wrong one in the file header, the header variables, the classes or the object
map refuses the file, as nothing past it can be trusted. An object with a wrong check sum, or one its decoder cannot
read to the end, is left out, the others are read, and one Warn line gives the count and the first fault.

DECIDED: where an entity is left out, the chain of its owner is broken (each entity of R2000 names the next). The
entities the chain did not reach are found by their owner and written after it in the order of their handles.

## Bit codes

All in `DwgBitReader`; bits are read from the high bit of each byte down, raw numbers low byte first.

| Code | Method | Form |
|---|---|---|
| B, BB, 3B | `Bit`, `Bits2`, `Bits3` | one, two, three bits |
| RC, RS, RL, RD | `RawChar`, `RawShort`, `RawLong`, `RawDouble` | 8, 16, 32, 64 bits, at any bit |
| BS | `BitShort` | 00 a raw short, 01 a byte, 10 the value 0, 11 the value 256 |
| BL | `BitLong` | 00 a raw long, 01 a byte, 10 the value 0; 11 refused |
| BLL | `BitLongLong` | three bits of count, then that many bytes |
| BD | `BitDouble` | 00 a raw double, 01 the value 1, 10 the value 0; 11 refused |
| DD | `BitDoubleDefault` | 00 the default, 01 four bytes over its low four, 10 six bytes, 11 a raw double |
| MC | `ModularChar` | seven bits a byte, the high bit says another follows; bit 0x40 of the last is the sign |
| MS | `ModularShort` | fifteen bits a short, the high bit says another follows |
| H | `Handle` | four bits of code, four of count, the bytes high first; codes 6, 8, 10 and 12 count from the handle of the object |
| TV | `Text` | a bit short of count, then the bytes; the count takes in the closing zero, which is cut |
| BT, BE | `BitThickness`, `BitExtrusion` | one bit for 0 and for (0, 0, 1), else the values |
| 3BD, 2RD, 3RD | `Point3`, `RawPoint2`, `RawPoint3` | points |

Every read past the end, every count below zero and every code refused throws `FormatException`; no loop runs on a
count the data cannot hold.

DECIDED: the text of R2000 is bytes of the drawing's code page; the reader returns bytes, not a string. W3 adds the
UTF-16 text of R2007 as a second form of `Text`.

## From DWG to DXF records

| DWG | DXF |
|---|---|
| header variables | HEADER, in the order and with the group codes AutoCAD writes; handles become the names of their records |
| classes | CLASSES |
| the nine control objects and their records | TABLES: VPORT, LTYPE, LAYER, STYLE, VIEW, UCS, APPID, DIMSTYLE, BLOCK_RECORD |
| block headers with BLOCK, the chain of entities, ENDBLK | BLOCKS; the entities of the model space and of the current paper space go to ENTITIES |
| every other object | OBJECTS, the root dictionary first, then by handle |

Read in W1: LINE, POINT, CIRCLE, ARC, ELLIPSE, SPLINE, RAY, XLINE, LWPOLYLINE, POLYLINE (2D, 3D, polygon mesh,
polyface mesh) with VERTEX and SEQEND, TEXT, MTEXT, ATTDEF, INSERT and MINSERT with ATTRIB, SOLID, TRACE, 3DFACE, the
seven DIMENSION kinds, BLOCK, ENDBLK; DICTIONARY, ACDBDICTIONARYWDFLT, DICTIONARYVAR, XRECORD, LAYOUT, GROUP,
MLINESTYLE, ACDBPLACEHOLDER, SCALE; with the layer, linetype, colour, linetype scale, visibility, lineweight, owner,
reactors, extension dictionary and extended data of each.

What AutoCAD does that the bytes do not say, found by comparing with the answer keys:

DECIDED: the count of a TV text takes in its closing zero.

DECIDED: AutoCAD 2021 writes LAYOUT, XRECORD, ACDBPLACEHOLDER, SCALE, DICTIONARYVAR and ACDBDICTIONARYWDFLT into an
R2000 file under class numbers (500 and up), not the fixed type numbers; an object is dispatched by the DXF name of its
class, a fixed number by its own.

DECIDED: the two dates of the header are universal time, and are written as `$TDUCREATE` and `$TDUUPDATE`; `$TDCREATE`
and `$TDUPDATE` (local time) are not in the file and are not made up.

DECIDED: of a table record's flags, bit 64 (referenced) is dropped, as AutoCAD drops it on DXF; bit 16 (from an xref)
is kept. Group 70 of a TABLE is the count the control object gives. The DIMSTYLE table adds `100 AcDbDimStyleTable`,
`71` and a `340` for each record; the LTYPE control's two extra handles are ByBlock and ByLayer.

DECIDED: a DWG calls the block of every layout `*Paper_Space`; DXF keeps the name for the current one and numbers the
others `*Paper_Space0`, `*Paper_Space1` in the order of the block table. Anonymous blocks, kept as `*D` or `*U`, are
numbered from 1 in the same order with one counter.

DECIDED: a 2D VERTEX leaves its widths (40, 41) out where they are those of its polyline; the fourth bit of a view's
mode is the inverse of DXF's bit 16.

DECIDED: of an XRECORD, groups 0 to 9, 100 to 105, 300 to 309, 410 to 419, 430 to 439, 470 to 479 and 1000 to 1003 are
texts, 310 to 319 and 1004 chunks of bytes, 320 to 369, 390 to 399 and 1005 to 1009 handles held as eight raw bytes.

DECIDED: a SPLINE of fit points holds no control points in a DWG; AutoCAD's DXF has them. They are made by the
library's own cubic interpolation with chord-length knots (`DxfNurbsEdit.InterpolateCubic`), and agree with AutoCAD's
to 1E-9 on the test drawings. The planar flag and the normal (210) are worked out from the points
(`DxfCurveTools.TryPlane`).

DECIDED: the round-trip data by which AutoCAD keeps a true colour in an R2000 file is left where the file has it:
extended data that starts with `RTT` on an entity, an extension dictionary holding `ACAD_XREC_ROUNDTRIP` on a layer or
a view. AutoCAD takes it away when it reads the file and gives the entity its true colour; the reader does not yet (see
Left open).

DECIDED: a type that is not read is not dropped silently: one Debug line counts them by type
(`DwgReader: types that are not read yet were left out: HATCH 1 LEADER 1 …`), and one Info line sums the call.

## Version sniffing

`DwgContainer.VersionOf` gives the six bytes at the start where they are `AC` and four letters, digits or dots, else null;
`ReleaseOf` names the release (AC1012 R13, AC1014 R14, AC1015 2000, AC1018 2004, AC1021 2007, AC1024 2010, AC1027 2013,
AC1032 2018).

DECIDED: `DwgReader` does not guess between DWG and DXF. Data that does not start with a DWG version is refused ("not a
DWG file"); a caller that takes both looks at the first bytes or the extension, and W2 may add one entry point for both
if the presenter wants it.

## Proof

Eight drawings, built in code by a probe plugin inside AutoCAD 2021 (`accoreconsole`, no SAVEAS or DXFOUT command: the
plugin calls `Database.SaveAs` and, on the file read back, `Database.DxfOut` with 16 places): `empty`, `curves`,
`polys`, `text`, `blocks`, `props`, `tables`, `later`. Each is in the tests as its DWG and the DXF AutoCAD wrote of it,
gzipped, 200 KB in all; a DWG of AutoCAD is about 100 KB however little is drawn, as it carries the whole template.

`DwgAnswerKey.Compare` pairs the records of the two DXF files (header variables, classes, tables, blocks and entities by
name or handle, objects by following handles from the root dictionary, since AutoCAD gives the objects of newer types
new handles on the way) and compares them group by group, numbers to 1E-9 relative. All eight drawings compare with no
difference but these, which the comparer allows and names in its remarks: the header variables of the moment of saving
(`$ACADMAINTVER`, `$HANDSEED`, the four times), `$CMLSTYLE` in case, the round-trip data above, a letter as `\U+00B0`
or as its byte, the order of OBJECTS, and the records of types not read. One drawing (`tables`) has `$TEXTSIZE` 3.5 in
the DWG and 0.2 in AutoCAD's DXF, a quirk of AutoCAD's own read.

Robustness: files cut short, other versions, noise, a DXF, a byte changed in each section, a byte changed in an entity
(the others are read, the chain mended), and a seeded fuzz of 700 changed files in the suite, of which 420 change bytes
inside objects under a repaired check sum so that the decoders see them. A longer run of the same fuzz, 10 500 files
read both as document and as database: 0 faults of another kind, none over 0.14 s.

## W2: R2004, R2010, R2013, R2018

| Path | What it holds |
|---|---|
| `src/GeometryHelper/Dwg/DwgContainer.R2004.cs` | the container of AC1018, AC1024, AC1027, AC1032: masked file header, page map, section map, pages |
| `src/GeometryHelper/Dwg/DwgLz77.cs` | the decompression of a page |
| `src/GeometryHelper/Dwg/DwgCrc.cs` | also the CRC-32 of the file header and the sums of a page |

The file of R2004 and later: the version and a few fields at 0x00 (maintenance release 0x0B, code page 0x13, security
flags 0x18), then 0x6C bytes at 0x80 masked by the series of a linear congruential generator from 1, which hold the place
of the page map, the number of the page of the section map, and their CRC-32. The page map gives each page its number
and size from 0x100 on; the section map gives each named section its size, the size of its pages, whether they are
compressed, and its pages with their starts in the section. A data page has a head of 32 bytes masked by
`0x4164536B ^ address`, with the sums of its data and of its head. The sections read are AcDb:Header, AcDb:Classes,
AcDb:Handles (the object map, in the form of R2000), AcDb:AcDbObjects and AcDb:Template.

DECIDED: every sum is checked: the CRC-32 of the file header (zip's, inverted before and after, over the 0x6C bytes with
its own four zero), the sum of every system page (seeded by its head) and of every data page and its head. A wrong one
refuses the file with a `FormatException`.

DECIDED: a hostile length cannot make the reader allocate without bound. The sections of one file may decompress to at
most 64 times the size of the file and 8 MB more, a page to at most 1 MB; a page that would write past its size, copy
from before its start or read past its input is refused. AutoCAD's test drawings take 5 times their size or less.

DECIDED: a file with the security flag of encrypted data, or a section marked encrypted, is a `NotSupportedException`.
R2007 (AC1021) keeps its own container and is refused by name until W3.

DECIDED: the decoders take the version. From R2004 an entity's colour is ENC (index, true colour, a colour book in the
handles, transparency) and a table record's is CMC; an owner lists the entities it owns, which no longer name the next.
From R2007 the texts are UTF-16 in a string stream at the end of the data, found from the flag before the handles; the
header variables and the classes have their own string stream, and the header its own handle stream. From R2010 an
object's head gives the size of its handles, and its type is two bits and bytes. R2013 adds a bit for data in the data
store and the spline flags and knot parameterisation; R2018 an attribute type, the columns of an MTEXT and the linetypes
of an MLINESTYLE by handle.

DECIDED: BLL takes its count of bytes from three plain bits, not from the 3B code (as ACadSharp reads it); with the 3B
code the header of R2013 and R2018 read wrongly.

DECIDED: the DXF a DWG is turned into is of the DWG's own version: header variables, classes (with their count, group
91), records and groups as AutoCAD writes that version. From R2007 its texts are UTF-8, as AutoCAD's DXF of those
versions is; the bytes of one UTF-8 character are not split between the pieces of a long MTEXT.

DECIDED: a true colour of a table record is written with the nearest colour index (62), which AutoCAD writes and the file
does not hold (it holds 0 beside it). The palette is the library's but for the greys 250 to 255, which the library has as
ezdxf does (250 black) and AutoCAD as 51, 80, 105, 130, 190, 255; the reader uses AutoCAD's there. A layer of RGB
10, 200, 30 gets 102 and a viewport's ambient 0x333333 gets 250, as in AutoCAD's DXF.
Superseded in W3: the palette of the library is now AutoCAD's, all of it, and the reader has no greys of its own.

DECIDED: before R2010 `$XCLIPFRAME` is group 290, on or off; the value 2 of later releases is written 0, as AutoCAD
writes it.

DECIDED: from R2013 a closed spline is written periodic too (group 70 bit 2): AutoCAD keeps a periodic spline clamped and
marked closed alone, and writes it periodic. Inferred from the one periodic spline of the test drawings.

DECIDED: an attribute of several lines of R2018 (attribute type 2 or 4) holds an MTEXT of its own and is not read yet: it
is counted with the types not read, not as broken.

DECIDED: a colour of a book (flag 0x4000 of ENC) is read as the specification gives it, its RGB value following; no test
drawing has one, and ACadSharp reads no RGB there. Group 430 is not looked up from the DBCOLOR object.

DECIDED (W1's open questions, the maintainer's answers): the round-trip data of a true colour in an R2000 file is left as
it is; three drawings a version are in the repository (curves, blocks, props: 284 KB gzipped for the four later
versions), the other five run from a local test where `DWG_ANSWER_KEYS` names their folder and pass empty elsewhere;
`DwgReader` stays apart from `DxfReader` (the app side is W6).

DECIDED: anonymous blocks are numbered with one counter for every letter, in the order of the block table. A test drawing
of *U, *D, *U, *D, *U blocks compares clean in all five versions.

DECIDED: the normal of a planar spline off the XY plane keeps W1's rule (`DxfCurveTools.TryPlane`, its largest part
positive). Of the ten splines of a test drawing in five tilted planes, by control points and by fit points, AutoCAD
agrees on eight; for the plane facing (-1, -1, -2) it keeps the normal facing down. A rule by the turning of the points
(Newell's) agreed on four only: AutoCAD's choice does not follow the turning, since the turning relative to the plane is
the same in all five. Parked.

What AutoCAD adds as it reads a drawing or writes its DXF, which the comparer leaves out on both sides and names in its
remarks: the counts of the classes (group 91), the extended data `DbSaveVer` on the head of the VPORT table (R2013), the
entry `ACDB_RECOMPOSE_DATA` of the root dictionary with its record, the dictionary
`ACAD_ROUNDTRIP_2008_TABLESTYLE_CELLSTYLEMAP` with what it holds, the class and the object CELLSTYLEMAP (R2010 on), and
`$INTERFEREOBJVS` and `$INTERFEREVPVS`, which a file of R2010 and later holds as no handle (its header data are read to
their last bit, so the place is right).

Proof: the eight drawings of W1 in AC1018, AC1024, AC1027 and AC1032, and a drawing of anonymous blocks, one of tilted
splines and one of 100 000 lines in all five versions (probe `GhDwg2`, W1's route), compare with AutoCAD's DXF with no
difference but the spline normals above. Fuzz: 12 800 changed copies of the 32 later drawings, half with bytes changed in
the decompressed objects under a repaired check sum, each read or refused in under 0.14 s, the process under 36 MB; in
the suite 3 000 pages of noise, 160 files changed anywhere and 480 changed inside objects.

Speed, best of five, 100 000 lines: `DwgReader.Read` takes 720 to 771 ms in every version (418 to 477 ms of it to DXF),
`DxfReader.Read` of AutoCAD's 14 MB DXF 246 to 303 ms.

## W3: R2007 (AC1021) and the palette

| Path | What it holds |
|---|---|
| `src/GeometryHelper/Dwg/DwgContainer.R2007.cs` | the container of AC1021: the file header, the page map and section map, the pages coded by Reed-Solomon |
| `src/GeometryHelper/Dwg/DwgLz21.cs` | the decompression of R2007 |
| `src/GeometryHelper/Dwg/DwgCrc.cs` | also the CRCs of 64 bits, normal and mirrored, their two seeds, and the sum of a data page of R2007 |

The file of R2007: the version and a few fields at 0x00 as in R2004, then 0x400 bytes at 0x80, three Reed-Solomon
(255, 239) blocks interleaved, whose data holds the CRC of a check sequence, its key, the CRC and the size of the
compressed data, and the data, 0x110 bytes once decompressed: the places, sizes, CRCs and repeat factors of the page map
and the section map, and the file's CRC seed. The pages start at 0x480. The page map and the section map are system
pages: their data, compressed where that is shorter, padded to eight bytes, repeated as often as the page holds it and
coded by Reed-Solomon (255, 239), interleaved. The page map is pairs of size and number from 0x480 on; the section map
gives each section its size, its page size, its coding (4 interleaved, 1 not), its name in UTF-16 (the length counts
bytes) and its pages with their offset, number, sizes, sum and CRC. A data page is Reed-Solomon (255, 251) blocks. The
sections read are those of R2004.

DECIDED: the parity bytes of the Reed-Solomon code are not used to correct anything. The data bytes are taken out of
their blocks and the CRCs and sums decide whether they are whole: a DWG with a changed data byte is broken, as it is in
every other version, and one whose parity alone is changed reads as if whole.

DECIDED: every sum of R2007 is checked, and each was found to hold on AutoCAD's files before it was made a refusal: the
normal 64-bit CRC of the check sequence (seed 1 from 0 over the key and the key turned by itself), of the compressed
data of the file header and of its data (seed 2 from 0); the mirrored 64-bit CRC of the page map and the section map,
compressed and not (seed 1 from the file's CRC seed); and of each data page the mirrored CRC of its compressed bytes
(seed 1 from the file's CRC seed) and the 32-bit sum of its data (from 0, the bytes of each eight in the order of the
specification). AutoCAD's CRC seed is 0 in every test drawing. The tables of the two CRCs are made from their
polynomials (0x42F0E1EBA9EA3693, and 0x95AC9329AC4BC9B5 reflected), not copied.

DECIDED: the decompression is ported from the specification and ACadSharp (MIT): a run of literal bytes comes out in an
order of its own for each length from 1 to 31 (a table of 31 orders, checked against both sources), in groups of 32
reversed by eights. 3 000 slices of AutoCAD's objects compressed by ACadSharp's compressor come back byte for byte.

DECIDED: a data page of R2007 may be larger than its section's page size: in the eight test drawings AutoCAD 2021 wrote
pages of the objects of up to 68 480 bytes under a page size of 63 488. A page is held to 1 MB, as in R2004, not to the
section's page size.

DECIDED: from R2007 the description in AcDb:Template is counted in UTF-16 characters, as ACadSharp reads it; AutoCAD's
is empty.

DECIDED: the decoders needed nothing new for AC1021: every branch W2 wrote for it from the specification (the string
stream, the UTF-16 texts, the object head with its size in bits, the string stream of the header and the classes) read
the eleven drawings right the first time.

DECIDED: the palette of `DxfColor` is AutoCAD 2021's, all 255 indexes: a probe wrote
`Color.FromColorIndex(ColorMethod.ByAci, i).ColorValue` and `EntityColor.LookUpRgb(i)` for 1 to 255, which agree. The
table taken from ezdxf differed at 197 of them: eight of the ten shades of each hue (12 is 0xCC0000 in AutoCAD, 0xA50000
in ezdxf), a few odd entries of ezdxf (109 0x135817, 149 0x135858), and the greys 250 to 255, which are 0x333333,
0x5B5B5B, 0x848484, 0xADADAD, 0xD6D6D6 and white. W2's greys (51, 80, 105, 130, 190, 255) were right for 250 and 255
only; its workaround in the reader is gone, and the nearest index of a true colour comes from `DxfColor` alone. The test
drawings of every version still compare clean: a layer of RGB 10, 200, 30 gets 102 and an ambient 0x333333 gets 250.

Proof: the eight drawings of W1 and the three of W2 (anonymous blocks, tilted splines, 100 000 lines, made in AC1021 by
probe `GhDwg3`, W2's route) compare with AutoCAD's DXF of them with no difference but W2's two spline normals.

DECIDED: an object whose decoded values the library's geometry refuses is one broken object, not a broken file. The fuzz
of AC1021 found a SPLINE changed so that `DxfCurveTools.TryPlane` throws an `InvalidOperationException` ("Cannot
normalize a zero-length vector"), which went past the transcoder's catch of `FormatException` and refused the whole file
(in every version). The transcoder now leaves such an object out with the Warn line; `TryPlane` throwing on points no
drawing has is the core's to settle (it is outside this stage).

Fuzz: 8 000 changed copies of the ten small AC1021 drawings, half with bytes changed inside the decompressed objects
under a repaired check sum: 2 faults of another kind before the fix above, both that spline, 0 after; each read or
refused in under 0.15 s, the process under 58 MB (GCHeapHardLimit 4 GB). The decompression: 3 000 slices round trip,
and the same with bytes changed returned or refused, none past its output, in under 5 ms. In the suite: the two AC1021
drawings in both seeded fuzz theories, 3 000 inputs of noise to `DwgLz21`, and a byte changed every 97 bytes of a file.

Speed, best of five, 100 000 lines in AC1021: `DwgReader.Read` 750 to 816 ms (471 to 502 ms of it to DXF), beside 747 to
796 ms (448 to 470 ms) for AC1018 in the same runs on a machine under other load; `DxfReader.Read` of AutoCAD's DXF 270
to 305 ms. Taking the data out of the blocks one block at a time, not by a division per byte, took 829 ms to 750 ms.

## W4: the entities and objects left out

| Path | What it holds |
|---|---|
| `src/GeometryHelper/Dwg/DwgObjectReader.Entities.cs` | also VIEWPORT, HATCH, LEADER, TOLERANCE, MLINE, IMAGE and WIPEOUT, REGION, 3DSOLID and BODY, ACAD_TABLE, the MTEXT of an attribute of several lines |
| `src/GeometryHelper/Dwg/DwgObjectReader.Leaders.cs` | MULTILEADER and MLEADERSTYLE |
| `src/GeometryHelper/Dwg/DwgObjectReader.Objects.cs` | also IMAGEDEF, IMAGEDEF_REACTOR, DBCOLOR, TABLESTYLE |

Probe `GhDwg4` (W1's route: side databases, `Database.SaveAs`, `ReadDwgFile`, `DxfOut` with 16 places, no system
variable set) drew eight drawings in all six versions: `vports`, `hatch`, `leaders`, `images`, `mline`, `table`,
`solids`, `meshes`. AC1015 and AC1032 of each (AC1018 and AC1021 of `table` too) are in the tests, 365 KB gzipped.

DECIDED: a DWG holds no number (69) and no status (68) of a viewport. From R2004 the layout lists its viewports, the one
showing the paper first, and they are numbered from 1 in that order; R2000 lists none, and the viewport whose view
centre and height are its own centre and height (the one showing the paper) is number 1, the others following by
handle. The status is the number from R2004 and -1 in R2000 (AutoCAD 2021's console writes "on but off screen" there),
0 for a viewport that is off (flag 0x20000) or of another layout. A layout of R2004 and later whose last active viewport
is null names the first of its list under 331. One drawing of four viewports in Layout1 and one in Layout2.

DECIDED: frozen layers of a viewport are 341 in a DXF of R2000 and 331 later, as AutoCAD writes them;
`DxfViewport.GetFrozenLayers` now reads both (a surgical change in the DXF entity, outside the stage's folder).

DECIDED: an entity's proxy graphics are kept and written (92, from R2010 160, then 310 pieces of 127 bytes), after the
common groups. AutoCAD saves them for a multileader, an image, a wipeout and a table in every version.

DECIDED: from R2010 a LEADER keeps no box of its text; AutoCAD writes the extents the annotating MTEXT keeps (its two
values after the flow direction), and so does the reader; 0 where the annotation is no MTEXT.

DECIDED: a MULTILEADER's colours are written as the one number of a CMC colour (0xC0000000 by layer), made from the index
before R2004; its context goes between `CONTEXT_DATA{` and `}`, its leaders after its content, a leader line's own
properties only where its override flags are set (no drawing has one). An MLEADERSTYLE holds "annotative" before
"property changed": the Standard style of the R2018 template has the second bit set and AutoCAD writes 295 1, 296 0.

DECIDED: a clip polygon of an IMAGE or WIPEOUT is kept open in the file; DXF closes it with its first vertex again. A
wipeout's definition and reactor (340, 360) are written as 0. RASTERVARIABLES and WIPEOUTVARIABLES are not read: the probe
could make neither, so nothing would check them.

DECIDED: an MLINESTYLE before R2018 names an element's linetype by its place among the records of the table (32767
ByLayer, 32766 ByBlock); the records follow ByBlock and ByLayer in the control's list. W1 wrote BYLAYER for every place.

DECIDED: the ACIS data of R2000 is SAT text, each character but the line ends kept as 0x9F less it, and DXF keeps it so:
a line a group 1, its carriage return dropped, pieces past 255 bytes under 3. R2004 to R2010 keep SAB (version 2),
which DXF does not carry before R2013 (AutoCAD writes SAT text made by its modeller): not read. From R2013 the data is in
the data store (AcDs), which DXF writes in an ACDSDATA section; the entity is written with the GUID of its record (290,
2), read after the wireframe and silhouettes, and a 3DSOLID with its history (350). The ACDSDATA section is not written.

DECIDED: a colour of a book sets both 0x8000 and 0x4000 of an entity's colour, but its RGB is in the DBCOLOR the handles
name, not after the index; W2 read a bit long too many and left the entity out. The index, the RGB and the book$name
(62, 420, 430) are the DBCOLOR's, which is read too (a CMC from R2004; before, an index DXF does not write).

DECIDED: an attribute of several lines of R2018 (attribute type 2, a definition 4) holds an MTEXT with its own common
entity data, read past, its MTEXT data and its annotative data; DXF writes 71, 72 (the last annotative value), the
alignment point under 11 and the MTEXT's groups after `101 Embedded Object`. Its text style handle comes before the
MTEXT's handles in the handle stream.

DECIDED: ACAD_TABLE is read to R2007 and TABLESTYLE to R2007, in the form DXF writes in every version. In an R2000 file
a table's and a table style's colours are CMC of the R2004 form (a class newer than R2004). A cell's override flags
keep bit 0x40000, which R2000's DXF masks (177) and R2007's writes whole (91). From R2007 a cell's content is a value:
flags, type, the value (a UTF-16 string counted in bytes, a long, a double), units, format and text, between `301
CELL_VALUE` and `304 ACVALUE_END`; another kind of value is refused as broken (no drawing has one). AutoCAD 2021 names
the class DATATABLE in an R2000 DWG and ACDBDATATABLE in its DXF; the reader writes the DXF name.

DECIDED (parked, with numbers):
- ACAD_TABLE and TABLESTYLE from R2010: the table keeps a TABLECONTENT of cell styles, contents and merged ranges, and
  DXF still writes the form of R2007 (each cell's 171 to 178, 91 262144 for a cell with content); the mapping needs a
  drawing of every cell kind, and the R2010 TABLESTYLE's cell styles have to be folded into three row styles. Not read.
- SHAPE: decodes, but DXF names the shape (2 TRACK1), and only the SHX file of its style gives the name of its number.
- MATERIAL: the order of ACadSharp reads the three materials of the template to their end in every version, but which
  groups AutoCAD writes (it left out the projections, wrote one source of 0 and not another) cannot be settled from three
  identical materials: a probe of varied materials is needed. Not read.
- VISUALSTYLE (a property bag from R2013, undocumented), the detail and section view styles, CELLSTYLEMAP, TABLECONTENT,
  TABLEGEOMETRY, DATATABLE, ACDBASSOCPERSSUBENTMANAGER: not read.
- OLE2FRAME, proxies (ACAD_PROXY_ENTITY and OBJECT), the data of dynamic blocks: the probe cannot make them in the
  console (an OLE object needs the GUI, a proxy the missing application, a dynamic block's parameters have no API).

What AutoCAD changes as it writes the DXF, which the comparer allows and names in its remarks: the proxy graphics of an
entity, compared by size (AutoCAD draws them anew and the padding after a text holds whatever its memory held); the
date at the end of the second line of SAT data, which AutoCAD writes empty; the entry AcDsDecomposeData of an R2013 root
dictionary, dropped with what it holds (the drop now follows ownership to any depth); R2000 round-trip records of which
AutoCAD keeps a part (a viewport's keeps SHADEPLOT, the rest it takes into the viewport); the extended data DBColXD of a
DBCOLOR, which AutoCAD takes away from R2000 and doubles in R2004 to R2013; and the flag 4 of an empty cell value of
R2007, written for some cells and not others.

Proof: the eight drawings of W4 compare with AutoCAD's DXF in all six versions with 0 differences but the types listed
above as not read; the drawings of W1 to W3 compare as before. The polygon mesh, polyface mesh and MINSERT, decoded in
W1 from the specification alone, compare with no change.

Fuzz: 19 200 changed copies of the 48 drawings of W4, 400 each, half with raw bytes of the file changed and half with
bytes changed inside objects of the new types (and the layouts, MTEXTs, dictionaries and attributes they lean on)
under a repaired check sum: 11 867 read, 7 333 refused, 0 faults of another kind, each in under 0.22 s, the process
under 75 MB. In the suite: sixteen W4 drawings more in the two seeded theories of bytes changed inside objects.

## Left open by W1

| What | State |
|---|---|
| VIEWPORT, HATCH, LEADER, TOLERANCE, MLINE, SHAPE, REGION, 3DSOLID, BODY, OLE2FRAME, IMAGE, WIPEOUT, proxies | W4: read but SHAPE, OLE2FRAME, proxies and the ACIS data of R2004 to R2010 (see W4) |
| MATERIAL, VISUALSTYLE, TABLESTYLE, MLEADERSTYLE, CELLSTYLEMAP, the two view styles, and other objects of classes | W4: MLEADERSTYLE read, TABLESTYLE to R2007; the rest not read (see W4) |
| true colour in R2000 | the round-trip data is passed on as it is; it is not turned into group 420 |
| polygon mesh, polyface mesh, MINSERT | W4: compared with AutoCAD's DXF in all six versions, no change needed |
| `$TDCREATE`, `$TDUPDATE` | not written (the file holds universal time only) |
| anonymous blocks | settled in W2: one counter for every letter, as AutoCAD |
| normal of a spline off the XY plane | W2: AutoCAD agrees on 8 of 10 tilted splines; parked (see W2) |
| the preview image, the second header, the auxiliary header | skipped |
| R13 and R14 (AC1012, AC1014) | refused by name; the container is the same as R2000's and the fields differ little, so they are cheap to add once the decoders take a version |

## What the later stages need

| Stage | Needs |
|---|---|
| W2: R2004, R2010, R2013, R2018 (AC1018 to AC1032) | done, see W2 above. As planned: a second container behind the same hand-over (`Header`, `Classes`, `ObjectMap`, the bytes of the objects): the encrypted file header, the page map and section map, the LZ77 decompression of pages, the 32-bit check sum. Objects are no longer at offsets of the file but of the decompressed AcDbObjects section, so `DwgObjectReader` is to take that array. The decoders take the version: R2004 colours (CMC with a true colour and a book name), the count of owned objects in place of first and last handles, no next and previous links; R2010 the type as two bits and bytes, the size of the handle stream in the object's head; R2013 and R2018 a few more fields. The probe already wrote the eight drawings in AC1018, AC1021, AC1024, AC1027 and AC1032 with their answer keys (scratch, `out/`), ready to be the proof |
| W3: R2007 (AC1021) | done, see W3 above |
| W4: the entities and objects left out | done in part, see W4 above; what is parked is listed there |
| later: writing a DWG | the bit writer as the mirror of `DwgBitReader`, and the tables of `DwgHeaderVariables` and the decoders read the other way |

W1's open questions for W2 were answered by the maintainer and are DECIDED in the W2 section.
