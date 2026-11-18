# C8 part 1: repairing damaged DXF files (RECOVER, AUDIT)

Research note of 2026-11-09, and what was built from it. Fonts (C8 part 2) are not here.

## What was read

- **ezdxf** (MIT), `src/ezdxf/recover.py` and `src/ezdxf/audit.py`, read from raw.githubusercontent.com; no code copied.
  - `synced_bytes_loader`: a line where a group code should stand that is not one is skipped, and the next line tried as the
    code. It cannot tell a missing value line inside a pair; nor can we.
  - `rebuild_sections`: a SECTION inside a section closes the one before (missing ENDSEC), an ENDSEC that closes nothing is
    dropped, tags outside every section are collected as orphans; sections of the same name are merged.
  - `_search_int` / `_search_float`: a number is read from the first number in its text, as C's `stoll` and `stod` read
    "42xyz" as 42, and the repair is reported with its line.
  - `detect_encoding`: `$DWGCODEPAGE` and `$ACADVER` may stand anywhere; R2007 and later are UTF-8 whatever the page says.
  - `Auditor`: a numbered list of error codes (`AuditError`), each entry fixed or only found; undefined linetype and text
    style are removed from the entity, an undefined dimension style replaced by Standard; an entity whose owner does not
    exist is deleted; a POLYLINE without SEQEND gets one.
- **AutoCAD**: RECOVER opens a damaged DWG or DXF and audits it with fixing, then reports "Total errors found N fixed N"
  and the drawing opens modified; RECOVERALL also recovers nested xrefs; AUDIT asks "Fix any errors detected? [Yes/No]
  <N>:" and lists what it finds; AUDITCTL writes an .adt file. AutoCAD refuses a DXF with an undefined linetype on DXFIN
  and creates an undefined layer silently.
- **ACadSharp** (MIT), `CadReaderConfiguration.Failsafe` (on by default): the reader catches an exception, notifies it and
  goes on, so the document may lack the objects that failed; a report by notification, no repair. We took the idea of
  never giving up on the whole file for one bad object (`DxfRepairKind.Unreadable` keeps the entities alone, then an empty
  drawing). netDxf and LibreDWG were not read this round.

## What we took and where we differ

- Our pass is in two halves, both reporting through one `DxfRepairKind`: a text pass over the bytes
  (`DxfRecovery`, the structure, numbers, handles, owners; it writes the file again only where it fixed something, so a
  sound file keeps every byte), then an object pass over the opened drawing (`DxfDatabase.Audit`, the tables, blocks,
  types, dictionaries). `Audit` alone runs the text pass on the saved drawing to find, not fix.
- Undefined layer, linetype, text style and dimension style are **added** to their tables (the maintainer's ask), not
  removed from the entity as ezdxf does: the drawing keeps the names it meant.
- An entity whose owner names nothing is **re-owned** by the block, table or polyline it stands in, not deleted as ezdxf
  does; an object of OBJECTS with such an owner is only reported.
- A dictionary entry that points at an object the file does not hold is **not** an error: AutoCAD writes such entries for
  objects it leaves out of a DXF (300 of them in a real architectural drawing of AutoCAD 2010, all AEC display
  representations). Only a malformed entry (a name with no pointer, a pointer with no name or not a handle) is taken out.
- A number that is not finite leaves its entity out (the reader draws nothing of it anyway); elsewhere it becomes 0.

## Not done (what AutoCAD's RECOVER does that this does not)

- A missing TABLES, BLOCKS or OBJECTS section of a file of R13 or later is reported, not rebuilt (no root dictionary, no
  layouts made anew); AutoCAD makes them.
- Pointers other than the owner (340, 360, reactors) that name nothing are not checked; references to a handle two records
  had keep naming the first holder, except the owners the place of the record decides.
- No block reference cycles, no proxy graphics, no binary DXF, no DWG, no xrefs (RECOVERALL), no .adt file.
- A missing value line inside a pair cannot be told from a value that is a number (ezdxf has the same limit).
