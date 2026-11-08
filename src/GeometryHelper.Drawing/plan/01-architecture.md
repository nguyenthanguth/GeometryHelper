# Architecture

Two parts (A16): Part A, the 2D presenter, and Part B, the model drawing, which writes its sheets into Part A's
document. The layers of each part are below; Part A's are numbered K, Part B's L (Part A's phases are C1 to C4 in [04-phases.md](04-phases.md)).

## Part A: the 2D presenter

| # | Layer | Job | Details |
|---|---|---|---|
| — | DXF database (exists) | `DxfDatabase`: entities, layers, linetypes, styles, blocks, layouts, viewports; reading and writing DXF | the library's `GeometryHelper.Dxf` |
| K1 | Document | `CadDocument`: one `DxfDatabase` open for editing, its file, its undo and redo, its selection, the owners of its entities | [70-presenter.md](70-presenter.md) |
| K2 | Display | entities turned into 2D primitives (lines, arcs, fills, glyphs) per space and viewport, cached, with a spatial index for picking | [70-presenter.md](70-presenter.md) |
| K3 | Editing | commands with prompts (as AutoCAD's), object snaps, grips, the edits themselves, each edit one undo step | [70-presenter.md](70-presenter.md) |
| K4 | Command plugins | outside code that adds commands, as AutoCAD's .NET plugins do | [60-plugins.md](60-plugins.md) |
| K5 | UI | the canvas, Properties, the drawing-files panel, ribbon, command line, status bar, Global settings | [50-ui.md](50-ui.md) |

## Part B: the model drawing

| # | Layer | Job | Details |
|---|---|---|---|
| L0 | Source | the objects drawn, from any source: a `GeoSolid3` each, with its kind, name, mark, profile, material, parent and attributes | [10-source.md](10-source.md) |
| L1 | Projection | one view direction gives 2D edges, visible or hidden, and the faces a section cuts, each edge knowing its object | [11-projection.md](11-projection.md) |
| L2 | Views | a view is a window into the model (like a Tekla view or AutoCAD's MVIEW): direction, scale, crop, depth; its lines made again from the model | [12-views.md](12-views.md) |
| L3 | Annotation | dimensions by rule, marks, centre lines, bolt and weld symbols, placed with nothing overlapping and tied to points of the model | [13-annotation.md](13-annotation.md) |
| L4 | Sheet | the sheet, its title block, part list and revision table, and where the views go on it | [14-sheet.md](14-sheet.md) |
| L5 | Output | the sheet written into a `DxfDatabase`: layers, linetypes, dimension styles, blocks, DIMENSION entities, hatches, layouts and viewports, every entity tagged with its owner | [15-output-dxf.md](15-output-dxf.md) |
| L6 | Settings | drawing settings (presets); Part B's categories of Global settings | [16-settings.md](16-settings.md) |
| L7 | Drawing document | a `Drawing` is an editable document: every view, dimension and mark has an id and links to the model; hand edits are recorded and kept through an update | this file, section L7 |
| L8 | UI | what Part B adds to Part A's window: the drawing list, model properties, the Drawing ribbon tab, its dialogs | [50-ui.md](50-ui.md) |
| P | Drawing plugins | outside code that works on drawings as Tekla's drawing plugins do, on model objects | [60-plugins.md](60-plugins.md) |

Sources are converters outside the module: `DrawingSourceConvert` in TeklaConvert
([17-tekla-bridge.md](17-tekla-bridge.md)), later one in IfcConvert, and objects built in code.

## How data flows

```
Tekla model ──DrawingSourceConvert (TeklaConvert)──┐
IFC (later) ──IfcConvert───────────────────────────┼──► DrawingSource (L0)
objects built in code ─────────────────────────────┘          │
                                                              ▼
          drawing settings + preset (L6) ──► DrawingBuilder
                                                              │
  ViewBuilder (L2) ── per view ──► ViewProjector (L1) ──► projected edges, section faces
        │                                                     │
        ▼                                                     ▼
  Annotator (L3) ◄────────────────────────── points of interest, tied to the model
        │
        ▼
  SheetLayout (L4) ──► Drawing: the document (L7) ◄──── drawing plugins (P)
                              │      ▲
                              │      │ edits on entities it owns (owner contract)
              DrawingDxfWriter (L5)  │
                              ▼      │
   ═══════════════════ Part A ═══════╪═══════════════════════════════════════════════
                              ▼      │
  any .dxf ──DxfReader──► DxfDatabase in a CadDocument (K1) ◄──── commands, command plugins (K3, K4)
                              │
                              ▼
                     Display (K2) ──► canvas (K5)      save ──► .dxf
```

## The owner contract between the parts

Part A shows and edits entities; some of them belong to someone else. Every entity Part B writes carries its owner and
its object's id (in the entity's XDATA, under an application name of the module's). Part A keeps a list of owners, and
before it changes an entity that has one, it asks:

```csharp
namespace GeometryHelper.Dxf.Viewer
{
    public interface ICadEntityOwner
    {
        string Name { get; }                                   // the XDATA application name it owns
        CadEditRule GetEditRule(DxfEntity entity);             // ReadOnly, MoveOnly, Edit, ...
        bool TryApply(CadDocument document, CadEdit edit);     // the owner records the edit and redraws its entities
    }
}
```

What follows from it:

- **Entities with no owner** are Part A's own: lines and texts drawn by hand, everything in a plain DXF. Part A edits
  them directly.
- **Entities Part B owns** are edited through Part B. A projected line is read-only (it is the model; to change it,
  change the view or hide the line). A mark can be moved; a dimension can be moved, its text edited, a point added or
  removed. Part B records each as a hand edit (L7) and writes its entities again.
- **On an update from the model**, Part B removes the entities it owns and writes them again, with the hand edits that
  still apply. Entities it does not own stay as they are, so what the user drew by hand on a model drawing survives.
- **Undo** stays Part A's: an edit that went through an owner is one undo step, and undoing it asks the owner again.

The contract is the only place Part A knows that owners exist; it never refers to Part B's types. The names are
proposals, settled in G0 (D24).

## L7: the drawing document

A UI that edits and plugins mean a `Drawing` is not only the result of one build but a document:

- **Stable ids** for every view, dimension, mark and table, so that the UI can select them, plugins can refer to them,
  and the next update can recognise them. The id is what Part B writes into each entity's XDATA.
- **Links to the model:** a dimension remembers which two points of which objects it measures; a mark remembers its
  object; a view remembers its direction and crop. When the model changes, values and places are worked out again.
- **Hand edits are recorded:** every change from the UI or a plugin is a record (object, property, old value, new value).
  The next update makes the drawing again and applies the records that still apply, and reports those that no longer
  can.
- **Plugin runs are recorded:** a drawing plugin's inputs and settings are kept in the document, so that an update runs
  the plugin again instead of replaying its old results (see [60-plugins.md](60-plugins.md)).
- **Save and open (A19):** a model drawing is one DXF. The document is carried in XRECORDs of a named dictionary and in the
  XDATA of the entities, so the file opens in AutoCAD and in Part A as any DXF; G0 settles the layout of the records.

## The public API in outline

```csharp
// Part A: open, edit, save any DXF
CadDocument doc = CadDocument.Open("plan.dxf");
doc.Run("MOVE", selection, from, to);                                 // or through the canvas
doc.Save("plan.dxf");

// Part B: a drawing from a model
DrawingSource source = DrawingSourceConvert.FromAssembly(assembly);   // Tekla, or new DrawingSource(objects) in code
Drawing drawing = DrawingBuilder.Build(source, DrawingSettings.Assembly);
drawing.Save("B-101.dxf");                                              // one DXF: the sheet and the document
drawing.Update(newSource);                                            // rebuild from the model, keep hand edits

CadDocument shown = drawing.OpenInPresenter();                        // Part B's sheet in Part A, owner registered
```

Each layer can be used alone, as the library's other modules are:

```csharp
ProjectedView2 view = ViewProjector.Project(solids, viewFrame, tolerance);   // L1 alone: lines of a view
```

Names are proposals until G0 fixes them in the spec; names carry their dimension as a digit, as everywhere in the
library.

## Rules every layer keeps

- **One way.** Part B refers to Part A; Part A never refers to Part B. Both refer to the core `GeometryHelper`.
- **No Tekla below L0.** Only `GeometryHelper.TeklaConvert` touches Tekla. Everything else takes `GeoSolid3` and plain
  data, so every test runs in code.
- **No UI in the cores.** K1 to K4, L0 to L7 and both plugin APIs do not reference WPF; the UI uses only the cores'
  public APIs.
- **Millimetres.** The model is in millimetres; the sheet is in paper millimetres. A view's scale maps one to the other.
- **Tolerance.** Every comparison of coordinates goes through a `Tolerance` passed in; nothing compares with `==`.
- **Determinism.** The same source and settings give the same DXF to the byte: every sort has a full tie-break, and no
  result follows the order of a `Dictionary` or `HashSet`.
- **Guard and log.** Arguments are checked through `Guard`; `GeometryHelperLog` takes one Info summary per public call,
  a Warn for a caught exception and one aggregated Debug line for expected skips.
- **Speed traps.** No sort-and-sweep along a fixed axis (sweep the axis with the fewest pairs), no `(a << 32) | b` keys in
  a `Dictionary` without their own comparer.
- **Reuse.** Reading and writing go through `DxfDatabase`, placing marks through `Arranging`, placing views through
  `Packing`. A fault found in one of them is fixed there, with its own test, not worked around here.
