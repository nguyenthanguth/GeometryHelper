# Test cases

Bodies built in code, in `TestBodies.cs` of the drawing tests. Every phase is checked on them. None comes from the
maintainer's models: where a real model showed a fault, a body is built here that shows the same thing.

The expected lines and values of each are worked out by hand in G0 and written into the tests.

| # | Body | What it tests |
|---|---|---|
| TC1 | a plate 300 by 200 by 20 with four holes of 22 at 50 from the edges, the circles faceted as Tekla facets them | holes as circles (D6), hidden hole walls in the side views, hole dimensions |
| TC2 | an I-section 300 deep, flanges 150 by 12, web 8, 3 000 long, with an end plate 20 thick and six holes of 22 through the plate | sharp and hidden edges of a profile, fittings dimensioned from the main part |
| TC3 | a tube of 168.3 by 6, 1 000 long, faceted | smooth edges dropped, silhouettes kept; seen along, across and at 30 degrees |
| TC4 | an angle 100 by 100 by 10 with a chamfer cut at one end and a corner notch | cut edges, edges piercing faces |
| TC5 | a column 250 by 250 (I-section) on a base plate 400 by 400 by 25 with four stiffeners and four bolts | an assembly: several objects, bolts in holes, marks of the fittings |
| TC6 | a beam with one end cut skew at 30 degrees and the other with a cope | sloping cut faces, outlines of cuts |
| TC7 | a concrete beam 300 by 600 with four bars of 20 and stirrups of 10 (later phases) | rebar drawn, hidden inside concrete |
| TC8 | two plates touching face to face and edge to edge | shared edges drawn once, visible over hidden |
| TC9 | the plate of TC1 turned so that some faces are seen exactly edge-on, and a copy a hair off that | edge-on faces, faces a hair off flat |
| TC10 | a frame of 200 assemblies like TC5 on a grid | speed; a general arrangement drawing |

Each body is checked first for what it is: closed and valid by `GeoSolid3.Validate`, and its volume equal to the one
worked out from its outline.
