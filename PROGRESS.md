# Progress

The list of work on GeometryHelper, the core library (geometry, DXF and DWG) and its bridges.

- `[x]` done and merged into `main`; `[ ]` to do; `(running)` an agent is on it now.
- The maintainer adds, removes or reorders items here; the lead takes the next open item from the top, ticks it when it
  is merged, and adds what a finished item leaves behind.

## The CAD app moved out (2026-11-23)

The CAD app, its libraries, its Python package and its work list moved to their own repository, OpenCAD2D
(`D:\GitHub\opencad2d`, https://github.com/nguyenthanguth/opencad2d), which uses this library as
`Lib\GeometryHelper.dll`. Its work list, with everything of Parts A and C that was here, is
`D:\GitHub\opencad2d\PROGRESS.md`; the list as it stood here is `git show 6bb8136e:PROGRESS.md`. Work on DXF or DWG that
the app needs is done here, committed, and taken there with `tools\update-lib.ps1`.

## Open work

- [x] Core: Try audit follow-ups 6 (NaN-shape members), done 2026-11-23 (`852287a5`): 993 members that reported a
  meeting with a wholly NaN shape or probe down to 47; 132 own messages down to 0 (Guard.FiniteShape); GetPlaneWith names
  `inPlaneDirection`. DECIDED by recommendation: a region with a non-finite vertex holds nothing, the booleans treat such
  an operand as empty, a distance with nothing finite to measure is NaN.
- [ ] Core: Try audit follow-ups 7 (later): the closest-point and parameter-of-point family (47 + about 120 members, about
  13 one-line guards at the ProjectTo* sites); a cached finite flag on GeoPolygon2 to take back the +3 % (net48) / +9 %
  (net10) on polygon Contains and Locate; an all-NaN fuzz pass over the 3D solid and mesh families.
