# Remote API: driving a running GeometryHelper CAD from another program

The maintainer's request of 2026-11-18: another program (a Tekla plugin, a console tool, Python) connects to the running
GeometryHelper CAD the way a Tekla Open API program connects to the running Tekla Structures (`new Model()`), and reads
and edits its drawings. It is simpler than AutoCAD's COM: one assembly is referenced and its objects are used; the
channel between the two processes is hidden.

## Decisions

- **R-1 Projects.** A new `src/GeometryHelper.Cad.Remote` (netstandard2.0, referencing only `GeometryHelper`) holds the
  client API and the message protocol, so a Tekla 2020 plugin on .NET Framework 4.8 can reference it. The server lives
  in `GeometryHelper.Cad` (no UI: a `CadRemoteServer` given the open documents and a dispatcher), started by
  `GeometryHelper.Cad.Wpf`'s host (`CadAppHost`). `GeometryHelper.Cad` references `GeometryHelper.Cad.Remote` for the
  protocol.
- **R-2 Channel.** One named pipe per running app, `GeometryHelper.Cad.<process id>`, open to the current Windows user
  only (pipe ACL). Each running app writes `%LocalAppData%\GeometryHelper\Remote\<process id>.json` (process id, window
  title, the time it was last activated) and deletes it on exit; stale files (no such process) are ignored and removed.
  Messages are length-prefixed UTF-8 JSON, written and read by the repository's own JSON code (no new package).
  Entities travel as DXF text of the core library, so the client uses the core types (`DxfLine`, `DxfMText`,
  `DxfDimension`, `DxfInsert`...) and nothing new.
- **R-3 Threads.** The server reads on a background thread and runs each request on the UI thread through the
  dispatcher it is given; a headless test gives a synchronous one. One request at a time per app; a second client waits.
- **R-4 Which app.** `new CadApplication()` connects to the app the user activated last. `CadApplication.GetRunning()`
  lists the running apps (process id, title, open drawings) and `new CadApplication(running)` connects to one.
  `CadApplication.ForDocument(path)` finds the app that has that file open.
- **R-5 Which drawing.** Every open drawing has an id that holds for the session. A `CadRemoteDocument` always targets
  its own drawing, whatever tab the user switches to; a request to a drawing closed meanwhile throws
  `CadRemoteException` ("The drawing A-101.dwg is no longer open."), never falls through to another drawing.
  `app.Documents` lists them (Id, Name, Path, IsActive), `app.Documents.Get(path or name)`, `app.ActiveDocument`,
  `app.Open(path)` (an open one is returned, not opened twice), `app.New(template = null)`, `doc.Activate()`.
- **R-6 Edits as Tekla's.** `Insert(entity, layout = null)` returns the new handle, `Modify(entity)`,
  `Delete(handle or entity)`, `GetObject(handle)`, `GetObjects(filter)` (kind, layer, space or layout),
  `GetSelectedObjects()`, `Select(handles)`. Insert, Modify and Delete are queued on the client and applied by
  `CommitChanges()` as one undo step with the canvas redrawn; reads are live. An owner that refuses an edit (Part B's
  owner contract) makes `CommitChanges` throw with the refusal, nothing applied.
- **R-7 Commands and files.** `SendCommand("ZOOM E ")` runs typed input as the command line does and returns when the
  command ends (or times out); `Save()`, `SaveAs(path, version)`.
- **R-8 User picks.** `PickPoint(prompt)` and `PickObjects(prompt)` ask the user on the canvas, as Tekla's `Picker`;
  Esc gives `CadRemoteException` (cancelled).
- **R-9 Tables.** Layers (list, add, modify: on, frozen, locked, colour, linetype) and layouts (list, current).
- **R-10 Setting.** Global settings › Save and export (or a new "API" row): "Allow other programs to connect" on by
  default; off closes the pipe and removes the instance file.

## Steps

1. R1: channel, discovery, documents, edits, commands, files -> verify: client and server over a real pipe in the tests
   (headless host): two documents, the right one edited while the other is active, a closed one refused; Insert,
   Modify, Delete of line, polyline, MTEXT, dimension, block reference with attributes, hatch round-trip by value;
   CommitChanges is one undo step; SendCommand; two server instances found and told apart; a second client waits; an
   owner refusal.
2. R2: picks, selection, layers and layouts, the setting, the guide `docs/presenter/remote.md`, a console sample in
   `tests/` -> verify: picks fed by a stubbed canvas, the setting closes the pipe, the sample runs against a test host.
3. Later, only if asked: Python client, a COM wrapper for VBA.
