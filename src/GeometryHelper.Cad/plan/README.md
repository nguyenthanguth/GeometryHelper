# GeometryHelper.Cad: the plan

A 2D presenter like AutoCAD (Part A, A16). It opens, shows and edits any 2D drawing held in a `DxfDatabase`: a DXF from
anywhere, model space and layouts with viewports, layers, AutoCAD's commands and shortcuts, and command plugins loaded at
any time as NETLOAD does. It knows nothing of models or Tekla.

Part B, the model drawing like Tekla's, moved to the private repository TeklaHelper.DrawingManager on 2026-11-18. It
uses this presenter and this presenter never refers to it; what it needs from here is a few generic seams: the owner
contract (`ICadEntityOwner`, `CadOwnerRegistry`, `CadOwnerTag`), `CadWorkspaceExtension` with its `CadNewDocumentOffer`,
and `CadAppHost`.

**Status: built, not yet packed.** `GeometryHelper.Cad` (the core, no window), `GeometryHelper.Cad.Wpf` (the controls),
`GeometryHelper.Cad.App` (its own window, GeometryHelper CAD) and `GeometryHelper.Cad.Remote` (another program drives the
running app). The guides start at [The 2D presenter](../../GeometryHelper/docs/presenter/index.md).

Where the plan and the code differ, the code and the guides are right.

## The plan

| File | What it holds |
|---|---|
| [04-phases.md](04-phases.md) | the phases C1 to C19, each step with its check |
| [70-presenter.md](70-presenter.md) | the document, display, editing, layers, reading and writing, checks |
| [72-autocad-gap.md](72-autocad-gap.md) | superseded by 73-autocad-parity.md; kept for its history |
| [73-autocad-parity.md](73-autocad-parity.md) | a check of the presenter against AutoCAD 2021's 2D drafting and annotation set |
| [73-recover.md](73-recover.md) | C8 part 1: repairing damaged DXF files (RECOVER, AUDIT) |
| [74-fonts.md](74-fonts.md) | C8 part 2: SHX and TrueType fonts, substitution |
| [75-ucs.md](75-ucs.md) | C7: user coordinate systems |
| [76-linetype-hatch.md](76-linetype-hatch.md) | C9: linetypes and hatches as AutoCAD makes them |
| [77-styles.md](77-styles.md) | C10: the style forms (Text Style, Dimension Style Manager) |
| [78-dwg.md](78-dwg.md) | our own DWG reader and writer |
| [79-remote-api.md](79-remote-api.md) | driving a running GeometryHelper CAD from another program |
