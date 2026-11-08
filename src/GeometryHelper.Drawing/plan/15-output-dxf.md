# L5: the DXF

`DrawingDxfWriter` turns a `Drawing` into a `DxfDatabase` (Part A's document type) and saves it. Part B writes no DXF
text of its own: `DxfDatabase` does, and Part A shows the result.

## Owner and id on every entity (D24)

Every entity the writer makes carries XDATA under the application name of the module's (registered in the database's
application table): the owner's name, the drawing's id and the id of the view, dimension or mark it belongs to. That is
what lets Part A ask the owner before an edit, and lets an update remove and write again exactly the entities the
drawing owns. A DXF written this way opens in AutoCAD as any other: XDATA is ignored by what does not know it. Entities
a user draws by hand on top carry none, and an update leaves them alone.

**Settled in G2 (the layout A19 left to G0).** The names and the layout as the code has them:

- **The tag** (`CadOwnerTag` in Part A, written by `DrawingEntityTags` in Part B) is the XDATA of one application,
  `GH_OWNER`, registered in the drawing's APPID table when the tag is written: `1001 GH_OWNER`, then three strings,
  `1000` the owner (`GeometryHelper.Drawing`), `1000` the id of the element the entity draws (a view, a dimension, a mark),
  and `1000` the id of its view, left out for an entity of the sheet. The owner's name sits inside the data, not in the
  application name, so every owner shares the one application. The drawing's own id is not on the entities: one DXF holds
  one drawing, and its id is in the document's HEADER below.
- **The document** (`DrawingDocumentStore`) is the dictionary `GH_DRAWING` of the named object dictionary, holding two
  XRECORDs. `HEADER` is `90` the format (1) and `1` the drawing's id. `RECORDS` holds every record, each a list opened by
  `102 {GH_VIEW`, `{GH_ANNOTATION`, `{GH_EDIT` or `{GH_PLUGIN_RUN` and closed by `102 }`, in that order, then the records
  this version does not read, as they came.
- **One XRECORD for all the records, not one each.** The library's `DxfDBDictionary.SetAt` reads and writes every entry
  again on each call, so 10 000 entries in one dictionary take 108 s (1 000 take 0.39 s); one XRECORD of 10 000 lists
  is written, saved, opened and read in 0.14 s. The slow `SetAt` is reported as a fault of the library, with a test.
- **Fields have no names; their order is the layout.** A view or an annotation is its id, kind and view id, its links and
  its values; an edit its element id, kind (`Move`, `Property`, ... as `CadEditKind` names them), property, old and new
  value; a plugin run its id, plugin, version and view id, its inputs (links) and its settings (values). Links are a
  count (`90`) and that many object ids, then a count and, for each point, its object id and `10`, `20`, `30`; values
  a count and, for each, a name and a value.
- **Exact.** A text is a `1` with a `3` for each 250 characters more; `%`, `\` and control characters are written as
  `%` and two hexadecimal digits (a line feed is `%0A`), a lone half of a surrogate pair as `%u` and four, and null as a
  lone `%`. Without it, a `\U+0041` in a text read back as an `A`. Coordinates are written in the fewest digits that read
  back to the same bits (`"R"`, or 17 digits where `"R"` misses under .NET Framework), negative zero as `-0`. A save by
  AutoCAD writes reals with its own 16 digits, which may move the last bit.
- **Forward.** A newer layout of a kind is written under a new list name (`{GH_VIEW2`), so that an older version keeps it
  rather than misreading it. A record of a kind not known, or one that fails to read (a group too many, a count that
  does not parse), is kept and written back as it was, and those that failed are said in one Warn line; a list left
  open by another program is dropped, since it cannot be written back. Entries of `GH_DRAWING` other than `HEADER` and
  `RECORDS` are left as they are.

## Layout (D10)

Two ways, chosen in Global settings › Save and export:

- **Layout and viewports (the default).** The lines of each view are placed at 1:1 (true model lengths) in model space,
  each view in a place of its own so that none overlap. The sheet is a layout in paper space: the frame, the title block,
  the part list, and one VIEWPORT per view at its scale looking at its lines, as MVIEW does. Dimensions in model space read
  true lengths without `DIMLFAC`; text and arrow sizes follow the annotation scale or a dimension style per scale (settled
  in G5).
- **The sheet in model space.** The sheet at 1:1 in model space, in millimetres. Each view is drawn at its scale where L4
  placed it. Dimensions use a dimension style whose `DIMLFAC` is the view's scale (one style per scale used), so they read
  model lengths. This suits viewers that show model space only.

## What goes where

| Drawn | Entity | Layer (names a setting) |
|---|---|---|
| visible lines | LINE, ARC, CIRCLE | Visible |
| hidden lines | LINE, ARC, CIRCLE with the HIDDEN linetype | Hidden |
| centre lines | LINE with the CENTER linetype | Centre |
| section faces | HATCH | Hatch |
| dimensions | DIMENSION (rotated, aligned, radial, diametric, ordinate) | Dimension |
| marks | MLEADER | Mark |
| title block | INSERT of a block with attributes | Sheet |
| part list, revisions | TABLE | Sheet |
| frame, view titles, texts | LWPOLYLINE, MTEXT | Sheet |

The linetypes come from the standard definitions loaded into the drawing (`DxfDatabase`'s linetypes from a file, or
written in code). The text style and the dimension styles are made by the writer from the settings.

## Checks

- Opened in AutoCAD 2021 through accoreconsole: no error, AUDIT finds nothing to fix, the counts of entities per layer as
  expected.
- Read back by `DxfReader`: the lines of every view are the ones of the `Drawing`, to the point tolerance.
- The same drawing saved twice gives the same bytes (handles and order fixed).
- Version AC1027 (D8) unless G0 finds a reason for another.
