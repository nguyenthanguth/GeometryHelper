# Research

To study in G0, before the code of each layer. Ideas are taken, never code: OpenCascade is LGPL, FreeCAD LGPL, and this
package is MIT. Each entry gets what was learned and what is taken, when G0 reads it.

| Source | Look at | For | Learned / taken |
|---|---|---|---|
| OpenCascade, `HLRBRep` (exact) and `HLRBRep_PolyAlgo` (polyhedral) | how edges are classified, how hidden parts are found, how smooth and seam edges are handled | L1 | (G0) |
| FreeCAD TechDraw (`DrawViewPart`, `DrawViewSection`, `DrawViewDetail`, `DrawViewDimension`, `DrawProjGroup`, templates) | how a drawing is split into views, how sections and details are made, how dimensions refer to geometry | L2, L3, L4 | (G0) |
| build123d, `project_to_viewport` | a small, clear use of HLR to get visible and hidden lines | L1 | (G0) |
| A. Appel, "The notion of quantitative invisibility" (1967), and later work on polyhedral hidden-line removal | the classic method for polyhedra | L1 | (G0) |
| Tekla Structures user documentation on drawings: views, dimensioning, marks, drawing settings | the concepts and options a drawing has, and what the maintainer is used to | L2 to L6 | (G0) |
| Tekla Open API reference (`Tekla.Structures.Model`) | what the bridge can read: assemblies, solids, bolts, welds, report properties | the bridge | (G0) |
| ISO 128 (lines), ISO 129 (dimensions), ISO 2553 / AWS A2.4 (welds), ISO 7200 (title blocks) | what a correct drawing looks like | L3 to L5 | (G0) |
