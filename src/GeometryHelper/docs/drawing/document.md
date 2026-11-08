# The document in the DXF, and its owner

A model drawing is one DXF file. Besides the sheet it holds the drawing's own document: every view, dimension and mark with a
stable id and its links to the model, the hand edits made to it, the settings and the plugin runs. This page says where that is
kept and how the entities it describes are found again, and what [`DrawingOwner`](xref:GeometryHelper.Drawing.DrawingOwner) does
when the presenter is asked to change one. The file opens in AutoCAD and in the presenter as any DXF; the document is data
that AutoCAD keeps and ignores.

## Where it is

The document is a dictionary GH_DRAWING of the drawing's named object dictionary, holding two XRECORDs.
[`DrawingDocumentStore`](xref:GeometryHelper.Drawing.DrawingDocumentStore) writes and reads them:

```csharp
DrawingDocumentRecords records = DrawingDocumentStore.Read(drawing.Database);    // null where the DXF holds none
DrawingDocumentStore.Write(database, records);                                   // the same twice changes nothing the second time
```

| XRECORD | Holds |
|---|---|
| `HEADER` | the format of the records (`DrawingDocumentStore.FormatVersion`, 1) as a group 90, and the drawing's id |
| `RECORDS` | every record, each a list opened by a group 102 of its kind and closed by `102 }`: the views `{GH_VIEW`, the annotations `{GH_ANNOTATION`, the hand edits `{GH_EDIT` and the plugin runs `{GH_PLUGIN_RUN`, in that order, then the records it does not read, as they came |
| `VALUES` | what the app keeps with the drawing, as named values in order: `Source`, `Preset` and a `WholeAssembly` for each assembly drawn whole (`DrawingDocumentStore.WriteValues`, `ReadValues`). A file written before them has none, and none is written for a drawing that has none |

One XRECORD holds all the records because a dictionary takes its entries one at a time: a document of 10 000 records is written
twice, saved, opened and read back in 0.14 s, where an XRECORD of its own for each record took 108 s. Each record holds its
fields one after the other with no names: a view or an annotation its id, kind and view id, its links and its values; an edit its
object id, its kind (the name of a [`CadEditKind`](xref:GeometryHelper.Dxf.Viewer.CadEditKind)), its property and its old and new values; a plugin run its id, plugin, version
and view id, its inputs (links) and its settings. A text is a group 1 and a group 3 for each 250 characters past the first, with a
per cent sign, a backslash and control characters written as `%` and two hexadecimal digits, so a line feed is `%0A` and a null
text a lone `%`. Coordinates are written in the fewest digits that read back to the same bits.

Everything comes back exactly: texts letter for letter, accented letters, line breaks and backslashes among them, and
coordinates to the bit, negative zero included.

A record of a kind this version does not know, as one a newer version writes, or one that fails to read, is kept in
`UnknownRecords` and written back as it was; a record that fails to read is a Warn line of
[`GeometryHelperLog`](xref:GeometryHelper.GeometryHelperLog) and stops none of the others. A newer layout of a kind is written under a
new name, so that this version keeps it rather than misreading it. Entries of GH_DRAWING other than HEADER and RECORDS are left as
they are. The dictionary needs a named object dictionary, so a drawing of R12 holds none.

The document of the beam of the first look reads `B1: 3 views, 13 annotations, 0 edits, 0 plugin runs, 0 unknown`.

## What it holds

| Type | Is |
|---|---|
| [`DrawingDocumentRecords`](xref:GeometryHelper.Drawing.DrawingDocumentRecords) | the whole document: `DrawingId`, `Views`, `Annotations`, `Edits`, `PluginRuns` and `UnknownRecords`; two are equal when every record is |
| [`DrawingElement`](xref:GeometryHelper.Drawing.DrawingElement) | a view, dimension, mark or other part: an `Id`, a `Kind`, the `ViewId` it is in, its `Links` to the model and its own `Values` by name, in order |
| [`DrawingLinks`](xref:GeometryHelper.Drawing.DrawingLinks), [`DrawingLinkPoint3`](xref:GeometryHelper.Drawing.DrawingLinkPoint3) | what an element is tied to: object ids, and points of the model with the object each lies on |
| [`DrawingEdit`](xref:GeometryHelper.Drawing.DrawingEdit) | a hand edit: the element's id, the kind, the property and the old and new values |
| [`DrawingPluginRun`](xref:GeometryHelper.Drawing.DrawingPluginRun) | a run of a drawing plugin: its id, plugin, version, view, inputs and settings |

The elements of the beam of the first look are 16: three `View`, four `Dimension`, one `Mark`, four `Symbol`, and one each of
`Settings`, `Sheet`, `TitleBlock` and `PartList`. Their ids are in [Settings](settings.md#ids). A view's values are its kind, name,
scale, origin and axes, depths, crop, box, where it stands on the sheet and in model space, its parent and whether it draws hidden
lines; the settings are kept as values by name too, so that [`Drawing`](xref:GeometryHelper.Drawing.Drawing)`.Open` gives back the settings the drawing was made with.

A dimension is tied to the points of the model it measures, and a mark to the point its leader ends on, so that when the model
changes the values and places are worked out again, and a link whose object has gone says so when an update drops an edit.

## The tag on each entity

Each entity the drawing writes carries a [`CadOwnerTag`](xref:GeometryHelper.Dxf.Viewer.CadOwnerTag) of the owner
`GeometryHelper.Drawing`, the id of the element it draws and the id of the view it is in. [`DrawingEntityTags`](xref:GeometryHelper.Drawing.DrawingEntityTags)
makes and reads it:

```csharp
DrawingEntityTags.Tag(line, "B1", "V-FRONT");                                      // the line draws object B1 in the front view
DrawingEntityTags.TryRead(entity, out string objectId, out string viewId);        // false for an entity that is not the drawing's
IReadOnlyList<DxfEntity> own = DrawingEntityTags.Find(database);                  // 95 on the beam of the first look
int taken = DrawingEntityTags.RemoveAll(database);                                // what an update does before it writes anew
```

The first tagged entity of the beam is a LINE on the layer Visible, `B1` in `V-FRONT`. An entity drawn by hand on the sheet has no
tag, and nothing here touches it: that is how an update leaves a line you drew where it was. `Find` looks at model space and at every
layout, model space first and the layouts in the order of their tabs, and not inside block definitions, since a block is the
drawing's, not one entity's; in a drawing of 10 000 lines, 9 000 of them tagged, finding them takes 0.025 s and taking them
out 0.027 s. `RemoveAll` erases the anonymous block a dimension was drawn with (`*D1`) as well, where no dimension left draws with it.

## The owner

`DrawingOwner` is the [owner](../presenter/owners.md) of those entities in the presenter, made by
[`Drawing.OpenInPresenter`](update.md#showing-a-drawing-in-the-presenter), or by `Drawing.Open(document, ...)` over a document the app already holds, and named `GeometryHelper.Drawing`
(`DrawingEntityTags.OwnerName`). It reads the id in the tag, finds the element, and answers by the element's kind:

| Element | Rule | Why |
|---|---|---|
| a dimension | `Edit` | it keeps a move, a text override and its erasing |
| a mark, a view (its viewport and its title) | `MoveOnly` | dragged off its part, or on the sheet |
| a projected line, a section's hatch, a symbol, the frame, the title block, the part list | `ReadOnly` | they are the model and the sheet |
| an entity with no element of its id | `ReadOnly` | the owner does not know it |

An edit it takes is one undo step of the presenter: its entities are changed (a dimension
moved, given its text override or erased; a view's entities moved by the displacement; a mark erased and written again from its
values, with its leader's arrow still ending on its part) and the hand edit is recorded in the document in the DXF at once, so
that undoing the step takes the record back with it. A mark P1 dragged 40 up is the hand edit `V-FRONT/M-P1 Move: (none) -> 0,40`, and
its text and the start of its leader stand 40 above where the drawing puts them, after an update too.

An edit its element does not keep (see [the table of hand edits](update.md#hand-edits)), or a move that does not read as `dx,dy`, is refused and nothing changes. A
kind of edit the rule does not allow never reaches the owner: the gate refuses it with the words the command line shows.

## Written afresh

[`DrawingDxfWriter.Write(drawing)`](xref:GeometryHelper.Drawing.DrawingDxfWriter) writes a drawing into a new DXF of AutoCAD 2013 (AC1027): its sheet as its settings lay it
out, and its document. It is the DXF that `Drawing.Database` holds but for what was drawn there by hand. A drawing opened from a
file and not updated has no views to write and throws `InvalidOperationException`. Its constants name what it writes:
`LayoutName` "Sheet", `SheetLayer` "Sheet" for the frame, the title block, the part list and the view titles, `ViewportLayer`
"Viewport", and `TitleBlockName` "GH_TITLE", the block of the title block, whose attributes PROJECT, TITLE, DRAWING_NO,
REVISION, SCALE, SHEET, COMPANY, DRAWN_BY and DATE hold its texts.

## See also

- [Owners](../presenter/owners.md): the contract `DrawingOwner` keeps.
- [Making it again](update.md): what an update does with the edits kept here.
- [Settings, scale and sheet](settings.md): the ids, and how the DXF is laid out.
- [Blocks and attributes](../dxf/blocks.md) and [Editing rules](../dxf/rules.md): the entities the drawing is written as.
