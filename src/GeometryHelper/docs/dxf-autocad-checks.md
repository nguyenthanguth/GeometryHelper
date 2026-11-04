# Checking against AutoCAD

The editing model of [Shared types](common.md#editing-a-dxf-file) was held against ezdxf 1.4.5, which recovered and audited
every file it wrote with nothing to fix and read back every value set, and against the members AutoCAD's .NET API declares.
What neither can say is what AutoCAD itself makes of a few of the files: the points below were decided from the DXF
reference, ezdxf and the files AutoCAD writes, and are for the maintainer to try in AutoCAD. Each says what to try, what the
library does now, and what result would mean the library has to change. Open each file written with AUDIT run on it as well;
an error AUDIT reports or fixes in a file the library wrote is a change to make whatever the point.

AutoCAD 2021 answered a part of them on 29 and 30 October 2026, by COM, LISP and its .NET API: a point it settled says what
AutoCAD did, and what the library does since, in the place of what would have meant a change. Four points it found that the
list had not: AutoCAD refuses a file whose pattern HATCH has no count of its lines (78), one whose MTEXT turns its background
on without its colour and scale (63 and 45), one whose LAYER of R2000 or later has no plot style (390, "Did not receive
PlotStyleName", points 29 and 30), and one whose face of a PFACE has AcDbVertex before its AcDbFaceRecord (point 58); all
four are now written as AutoCAD reads them. Its plugin v4 found two more: a DIMENSION of R2000 that keeps the version 280
R2010 brought (points 29 and 30), and a DIMSTYLE copied into R12 without its arrows by name 5, 6 and 7 (point 55); both are
written as AutoCAD reads them now.

## Entities and copies

1. **A SOLID given a new normal.**
   - Try: make a SOLID of the square of side 2 at the origin, set its `Normal` to (0, 0, -1), save, and open it in AutoCAD.
   - Now: the corners keep their places in the world, each written anew in the system of the new normal (the corner (2, 0, 0)
     written as (-2, 0, 0)), as a polyline's are.
   - AutoCAD 2021: agrees. AUDIT finds nothing; the SOLID's corners read (0, 0, 0), (2, 0, 0), (0, 2, 0) and (2, 2, 0) in the
     world.

2. **Extended data handles on a copy into another drawing.**
   - Try: give a LINE extended data with a handle (1005) between other groups, copy it into another drawing with WBLOCK or
     COPYCLIP and PASTECLIP, and list its extended data.
   - AutoCAD 2021: keeps the 1005 in its place, null (0) where what it names stays behind, the copy's handle where that is
     copied with it (`CopyObjects` of the LINE and its CIRCLE).
   - Now: so does the library: a copy into another drawing keeps the 1005 as 0, and `WblockCloneObjects` names the copy of
     an entity copied with it.

3. **An associative HATCH copied into another drawing.**
   - Try: open a drawing the library wrote with the `Clone()` of an associative hatch appended to another drawing.
   - Now: the copy leaves its boundary objects behind, each path counting the pointers it keeps (97 of 0), and is no longer
     associative (71 of 0), as AutoCAD's COPY leaves a hatch copied alone.
   - AutoCAD 2021: agrees. AUDIT finds nothing in the library's copy; AutoCAD's own `CopyObjects` of the hatch alone keeps 71
     and 97 of 1 with a null 330, which its AUDIT reports and repairs into the library's form.

4. **The values of a table's block cells copied across.**
   - Try: copy an ACAD_TABLE whose block cells give their blocks' attributes values into another drawing with the library,
     and open it.
   - Now: where the cell's block comes with the copy, the values are kept, each pointer to a definition (331) naming the copy
     of that definition; where the drawing has a block of the name of its own, each pointer is left out with its value (300)
     and the count 179 made to agree.
   - AutoCAD 2021: agrees in what it shows. Its copy into a drawing with a BOLT of its own keeps the 300 with a null 331, but
     the value is tied to no definition of the drawing's BOLT, and the table drawn anew shows none.

5. **Two entities that point at each other, copied together.**
   - Try: copy a leader with its MTEXT, and a hatch with its four boundary lines, into another drawing with AutoCAD's
     `WblockCloneObjects`, and compare with the library's `WblockCloneObjects` of the same, and with a group of them.
   - AutoCAD 2021: the leader and the hatch copied together follow their copies, as the library's do; it copies no group, by
     `CopyObjects` or by COPYCLIP and PASTECLIP; and a leader copied alone brings its MTEXT, which it points at by a hard
     pointer (340), its copy pointing at the MTEXT's copy (73 of 0).
   - Now: so does the library's `WblockCloneObjects`: no group is copied (`db.Insert` of a whole drawing still copies its
     groups), and a leader copied alone into another drawing brings its annotation.

## Text

6. **The words of an attribute of several lines.**
   - Try: make an attribute of several lines in AutoCAD, save it as DXF, and read its group 1 and AutoCAD's `TextString`.
   - AutoCAD 2021: writes every line in the ATTRIB's group 1, joined by spaces: `LINE ONE LINE TWO LINE THREE`, the embedded
     MTEXT `LINE ONE\PLINE TWO\PLINE THREE` (not `\P` as ezdxf says, nor the first line alone).
   - Now: so does the library for an ATTRIB, by `MTextAttribute` and by `SetAttributeFromBlock` of a definition of several
     lines; an ATTDEF keeps its first line, as ezdxf writes it, AutoCAD's own definition having kept the value it had.
   - AutoCAD 2021 (plugin v5, an ATTDEF made an MTEXT attribute through its API): writes the definition's group 1 as the
     MTEXT's contents, `LINE ONE\PLINE TWO\PLINE THREE`, and the ATTRIB's empty; its `TextString` reads
     `LINE ONE LINE TWO LINE THREE` of both, and the same of the library's ATTRIB.
   - Now: the ATTDEF's group 1 is the MTEXT's contents, as AutoCAD's; the ATTRIB's stays the words joined by spaces, which
     AutoCAD reads as its own and other readers need for the words (the maintainer's decision).
   - AutoCAD 2021 (plugin v6, 06-b): the `TextString` of the library's ATTDEF of three lines, whose group 1 is
     `LINE ONE\PLINE TWO\PLINE THREE`, is `LINE ONE LINE TWO LINE THREE`, as is its ATTRIB's; the contents of both MTEXTs
     `LINE ONE\PLINE TWO\PLINE THREE`. Saved again, its ATTDEF and ATTRIB keep the groups 1 the library wrote.
   - Now: the `TextString` of an ATTRIB or ATTDEF of several lines is the words of the lines of its MTEXT joined by spaces,
     whatever its group 1 holds, the groups written as before. Setting it, which AutoCAD was not asked about, sets the words
     of its MTEXT, which keeps its place, and its group 1 to agree, rather than leave the MTEXT saying the old words.

7. **An MTEXT's background transparency.**
   - Try: read `BackgroundTransparency` of an MTEXT with no group 441.
   - AutoCAD 2021: reads BYLAYER (441 of 0), as the library does; but it refuses the whole file ("Error Decrypting Data",
     DxfIn eMissingDxfField) over an MTEXT whose 90 turns the background on with no 63 and 45, and opens it with 63 256 and 45
     1.5.
   - Now: `BackgroundFill` and `UseBackgroundColor` turned on write 63 256 and 45 1.5 where the MTEXT has none, and 441 0 from
     R2007, the values AutoCAD reads where there are none.

8. **An MTEXT's frame.**
   - Try: open in AutoCAD an MTEXT of R2004 and one of R2007 with `ShowBorders` set.
   - Now: the library sets the frame (flag 16 of group 90) from R2004 on; ezdxf from R2007.
   - A change if: AutoCAD 2004 to 2006 does not read the flag; then the library should refuse it before R2007.
   - AutoCAD 2021: reads the flag in both, AUDIT finding nothing; AutoCAD 2004 to 2006, the question, were not at hand.

9. **An MTEXT set in columns.**
   - Try: open in AutoCAD the MTEXTs set by `SetStaticColumns`, `SetDynamicColumns` and `SetColumnHeight` in a drawing of R2018
     (embedded object) and of R2000 (`ACAD_MTEXT_COLUMN_INFO` and `ACAD_MTEXT_DEFINED_HEIGHT` extended data), and look at the
     columns and their words.
   - Now: the columns are written as ezdxf writes them, and the MTEXT's own width (group 41, `Width`, and 40 of the embedded
     object) is made the width of a column: the note 120 wide set in two columns of 50 with a gutter of 10 has 41 of 50.
   - A change if: AutoCAD shows the columns otherwise, or keeps another width in 41.
   - AutoCAD 2021, still open: in R2018 it keeps in 41 (and the embedded 40) the width of all the columns with their gutters,
     110 for two of 50 and a gutter of 10, and sets the dynamic columns of no height in 13 columns; in R2000 it keeps 41 the
     width of a column, as the library does, but saved as R2000 itself writes each column after the first as an MTEXT of its
     own (`ACAD_MTEXT_COLUMNS_BEGIN`). Not changed yet.
   - AutoCAD 2021 (its saves of 09-a as R2018 and R2000): the static columns, 41 and the embedded 40 and 42 110; the dynamic
     ones of no height in 13 columns (41 770) and those 15 and 25 high in 3 (41 170, a third height of 0), as it laid the
     words out; in R2000 no `ACAD_MTEXT_DEFINED_HEIGHT` for a height of nought, and for the columns of their own heights
     the first column's, 15, after the heights, each later column's on its own MTEXT (25 on the second, none on the third).
   - Now (D16): `SetStaticColumns`, `SetDynamicColumns`, `ColumnCount`, `ColumnWidth` and `ColumnGutterWidth` make the 41 of
     an MTEXT of R2018 (and its embedded 40) the width of all the columns the MTEXT counts with their gutters, 110 for two
     of 50 apart by 10, and keep a column's before R2018; a copy into the other form takes the other width.
     `ACAD_MTEXT_DEFINED_HEIGHT` is written where the height is above nought, the first column's for columns of their own
     heights, each linked column MTEXT its own; `Height` of such columns is nought, as AutoCAD's R2018 save gives (46 0).
     The count of dynamic columns AutoCAD works out as it lays the words out, and its linked column MTEXTs of R2000, are
     not made: the library has no fonts.
   - AutoCAD 2021 (plugin v9, 09-d of R2000, the column data alone, no column MTEXTs): takes none of the three as columns,
     `ColumnType` NoColumns, `Width` 50, `Height` 0, 0 and 15 (the defined height of 8F); saved as R2000 it drops the column
     data and keeps the defined height, saved as R2018 it keeps the data as extended data, 41 50.
   - Now (D17): before R2007 the column data are columns only with the column MTEXTs linked (`ACAD_MTEXT_COLUMNS`), as AutoCAD
     and ezdxf write them; the data alone read as no columns, the defined height as `Height`, and `SetStaticColumns`,
     `SetDynamicColumns` and `ColumnType` refuse such an MTEXT columns, as the library makes no column MTEXT. Still to see:
     whether AutoCAD takes its own 09-a of R2000, with its column MTEXTs, as columns, and the library's columns of R2007,
     the extended data alone (09-e of after-d17).
   - AutoCAD 2021 (plugin v10): takes both as columns. Its own 09-a of R2000, 41 50 on each: 8D static, 2 columns, `Width`
     110; 8E dynamic of auto height, 13 columns, `Width` 770; 8F dynamic of their own heights 15, 25 and 0, 3 columns,
     `Width` 170, `Height` 15 (its defined height); `ActualWidth` 110, 770 and 170. The library's 09-e of R2007, the extended
     data alone: 8D 2 columns, `Width` 110; 8E 1 column, `Width` 50 (`ActualWidth` 770, the 13 columns it lays the words out
     in); 8F 2 columns, `Width` 110, `Height` 0 (no 46, a defined height of 15). `ColumnAutoHeight` and `GetColumnHeight` of
     static columns throw `eNotApplicable`. Saved as R2007 it writes 41 50 on each and makes a column MTEXT for each later
     column, as in R2000.
   - Now (D18): `Width` of an MTEXT in columns before R2018 is that of all its columns with their gutters, the count of its
     extended data times the column's width and the gutters between, 110, 770 and 170, as AutoCAD reads it, its 41 still
     written a column's; of R2018, and of an MTEXT of no columns, its 41 as before. `Height` before R2007 is the defined height
     whatever the columns, 15 for 8F. Still as they were, the library having no fonts: `ActualWidth`, read from 42, which
     these files lack, where AutoCAD lays the words out; and the count of dynamic columns AutoCAD makes as it lays them out
     (13 for 8E of 09-e). `ColumnAutoHeight` and `GetColumnHeight` of static columns give false and nought, as documented,
     where AutoCAD throws. Still to see (v11): R2010 and R2013 (09-f, 09-g), taken here as R2007; and what AutoCAD's `Width`
     set does to an MTEXT in columns before R2018, which the library writes into 41 alone.
   - AutoCAD 2021 (plugin v11): 09-f and 09-g, of R2010 and R2013, read as 09-e of R2007, `Width` 110, 50 and 110, as the
     library reads them. `Width` = 80 on 8D of its own 09-a of R2000 and of the library's 09-e of R2007 read 80 back, the
     columns kept (`ColumnWidth` 50, gutter 10, 2 columns, `ActualWidth` 110), but its saves (v11-09h-width80-r2000 and
     -r2007) wrote 41 50 and the column data as they were, and read again gave `Width` 110.
   - Now (D19): `Width` set on an MTEXT in columns before R2018 changes nothing in the file, as AutoCAD saves it, and reads
     110 at once, the width AutoCAD reads after the save; the 80 AutoCAD holds until then is not kept, the library's drawing
     being its file. A width below nought is still refused.

10. **Linked columns copied into R2018.**
    - Try: copy an MTEXT of linked columns of R2000 into a drawing of R2018 with the library, and open it.
    - Now: the copy holds the words of all the columns, in the embedded form of R2018.
    - AutoCAD 2021: agrees: three columns 40 wide 10 apart with all their words, though it reads the 41 of 40 as 140, as in
      point 9.

## Curves

11. **The side an offset goes to.**
    - Try: `GetOffsetCurves(5)` of a line from (0, 0, 0) to (30, 40, 0), of a counter-clockwise polyline square and of a spline,
      in AutoCAD's API.
    - AutoCAD 2021: the polyline square grows and the spline goes to the right, as the library's did; the line goes to the left,
      from (-4, 3, 0).
    - Now: a line, and a ray and an xline with it, offsets to the left of its way seen from the tip of its normal, the line from
      (-4, 3, 0); the other curves to the right, an arc's radius growing.

12. **An arc and a circle offset with the normal turned over.**
    - Try: `GetOffsetCurvesGivenPlaneNormal` of an arc with its normal negated, distance 5.
    - Now: the radius grows by 5 whichever way the normal points.
    - A change if: AutoCAD shrinks it.
    - AutoCAD 2021, so far: `Offset(5)` of the arc of normal (0, 0, -1) gives a radius of 15; `GetOffsetCurvesGivenPlaneNormal`
      threw `eNotImplementedYet` and is to be tried again.

13. **Spline fitting of a polyline.**
    - Try: `SplineFit` of a 2D and a 3D polyline at SPLINESEGS 8 and SPLINETYPE 5 and 6, and on a polyline of fewer vertices
      than the degree asks for; count the vertices made.
    - AutoCAD 2021: makes the same counts (39, 39 and 12 at SPLINETYPE 5; 31, 31 and 12 at 6), but its AUDIT reported every
      vertex that had not its POLYLINE's colour (`AcDb2dVertex color != owner's`, 89 and 106 errors).
    - Now: SPLINESEGS segments to each knot span, the degree dropped where the vertices are too few; every vertex carries its
      polyline's look (layer, linetype, colour, lineweight and the rest), written when it is made and when the polyline's is
      set.

14. **Curve fitting and surface fitting.**
    - Try: `CurveFit` of a polyline and `SurfaceFit` of a polygon mesh at SURFU 6 and SURFV 6, and count the vertices.
    - AutoCAD 2021: the curve fit agrees (7 vertices); its surface of the open mesh 4 by 4 at 6 and 6 has 49 points, 7 by 7,
      not 36; and AUDIT reports the vertices of another colour than their mesh, as in point 13.
    - Now: a pair of arcs on each segment through every vertex; SURFU by SURFV spans of the surface, one point more than the
      spans along an open direction, 49 for 6 and 6; the vertices in their mesh's look.

15. **The spline of a spline-fitted polyline.**
    - Try: `Polyline2d.Spline` of a polyline fitted by `SplineFit`.
    - AutoCAD 2021: the B-spline of the frame, of degree 3, its 5 control points the frame of 5 vertices (17 drawn).
    - Now: so does the library: the uniform B-spline the polyline is fitted with, on its frame, clamped where it is open; a
      join and a projection still take the polyline as drawn.

16. **Control points put in and taken out.**
    - Try: `InsertControlPointAt` and `RemoveControlPointAt` on a cubic, and compare the control points.
    - Now: a knot put in or taken out with the point, the rest of the curve kept where it can be.
    - AutoCAD 2021: agrees, to the control points and knots.

17. **A spline made a polyline.**
    - Try: `ToPolylineWithPrecision` at 0, 10, 30 and 99 on the cubic on (0, 0), (10, 10), (20, 10), (30, 0).
    - Now: within a thousandth of the spline's size at 0 and a tenth of that for each 10 more, never finer than a millionth of
      its size, judged at three points of each segment: 65 vertices at 10 and 513 from 30 up.
    - A change if: AutoCAD reads the precision another way.
    - AutoCAD 2021, still open: it does: SPLINEDIT's polyline (PLINECONVERTMODE 0) of the cubic has 33, 257, 1 025 and 4 097
      vertices at 0, 10, 30 and 99, a power of two of segments to its one span, finer on to 99. No rule both simple and true
      to the four fits the library's way of judging a segment (a tolerance of the size over the square of one more than the
      precision misses by a tenth); the library keeps its own reading until AutoCAD's counts on more precisions and more spans
      show the rule.
    - AutoCAD 2021 (plugin v4, the cubic of three spans 17-b): 78, 156, 224, 447, 835, 1 594, 3 888 and 7 562 vertices at 0,
      1, 2, 5, 10, 20, 50 and 99, where the library gives 29, 35, 36, 51, 91, 284, 843 and 843. Halving the parameter range
      of the whole spline from 0 to 3 while the curve's middle lies further than 0.01054 / (p + 1)^2 from the chord's middle
      gives all eight counts of the three spans, but 512 segments for the one span at 10, where AutoCAD gives 256: no rule
      found yet, parked.
    - AutoCAD 2021 (plugin v5, 17-c, `ToPolylineWithPrecision(p, false, true)`): the cubic of one span 33, 65, 65, 129, 129,
      129, 257, 513, 1 025, 2 049 and 4 097 vertices at 0, 1, 2, 3, 4, 5, 10, 20, 30, 50 and 99; the cubic of two spans on
      (0, 40), (10, 50), (20, 50), (30, 40), (40, 30), knots 0, 0, 0, 0, 1, 2, 2, 2, 2, 50, 99, 144, 197, 237, 288, 513, 973,
      1 519, 2 413 and 4 750. The tolerance of the halving above fits each curve alone as c / (p + 1)^2, c 0.0165 to 0.0179
      for the one span, 0.014907 for the two and 0.0105415 for the three: one over the largest length of each curve's second
      derivative, 60, 30 sqrt 5 (67.08) and 30 sqrt 10 (94.87).
    - Now: made lines, the whole parameter range is halved until the spline's point halfway along each piece lies within
      1 / (M (p + 1)^2) of the middle of its chord, M that largest length: every one of the 30 counts of the three curves, to
      the vertex. Made arcs, which AutoCAD was not asked for, the library's reading is kept.
    - AutoCAD 2021 (plugin v6, 17-d and four splines it made): every vertex of the three cubics at 0, 1, 2, 5, 10, 20 and 99
      within 2E-14 of the library's. Made arcs (`ToPolylineWithPrecision(p, true, true)`), each of the seven splines has the
      vertices it has made lines, at 0, 10 and 99: 33, 257 and 4 097 for the cubic of one span, where the library gave 5, 9
      and 47. Each bulge is the tangent of a quarter of the angle the spline's tangent turns through along the segment, to
      1E-16 on all seven: -0.00806 for the first arc of the one span at 0, its tangent turning from 45 degrees to 43.15. Made
      lines, the quadratic of three spans on (0, -120) ... (40, -130) gives 29, 342 and 2 732 vertices and the cubic of the
      double knot 0, 0, 0, 0, 1, 1, 2, 2, 2, 2 76, 933 and 8 011, as the rule gives them; the quintic of two spans 96, 1 050
      and 9 806 and the rational cubic of the weights 1, 2, 0.5, 3, 1 130, 1 377 and 12 078, where the rule gives 117, 1 353
      and 11 921, and 371, 4 401 and 26 854. AutoCAD's vertices of those two are the rule's halvings at a bend of 142.70 for
      the quintic (its largest second derivative 223.6) and 202.49 for the rational one (1 866.8): whence those come is not
      found. `ToPolyline(n)`: 33 vertices for the one span at every n from 2 to 50 and 65 at 99, 50 for the two spans at every
      n from 3 to 99, 78 for the three at every n from 4 to 99; n = 2, and 3 for the three spans, refused with
      `eInvalidInput`.
    - Now: made arcs, the vertices are those made lines, each arc bent by the turn of the spline's tangent along it, as
      AutoCAD's; the library's own rule for arcs is gone. The quintic and the rational spline are parked with more vertices
      than AutoCAD's. `ToPolyline(n)` stays the library's own reading of a count, n vertices set evenly along the length,
      which AutoCAD's does not keep; the difference is written in the guide.
    - AutoCAD 2021 (plugin v6): logs "Unknown header variable $PLINECONVERTMODE ignored" reading 17-d, to which the program
      that made the checks gave the variable for SPLINEDIT.
    - Now: the library writes `$PLINECONVERTMODE` only where the drawing has it, read from its file or set by `SetHeader`;
      a drawing of R2018 that has none is saved with none. The checks' files of 17 are made without it from after-d14 on.
    - AutoCAD 2021 (plugin v7, 17n): counts at 0, 1, 2, 3, 5, 10, 20, 50 and 99, of lines and arcs alike, of a quintic Bezier
      on (0, 0), (10, 10), (20, 10), (30, 0), (40, -10), (50, 0), 86 ... 7 929; a quartic on the first five, 53 ... 4 877; the
      cubic of one span rational with every weight 2, the same as not rational (33 ... 4 097); with the weights 1, 2, 2, 1,
      97 ... 9 257, and 1, 3, 1, 1, 99 ... 9 847; the quintic of v6 moved by (100, 100), the same as where v6 made it; and a
      closed periodic cubic through (0, 0), (40, 0), (50, 30), (10, 25), its knots the chords, 5, 9, 15, 17, 30, 48, 86, 252
      and 493, closed. Every vertex at 0 and 10 lies on a halving of the whole range, the test the distance of the middle
      point from the chord's middle, one tolerance for the whole curve scaled by 1 / (p + 1)^2.
    - The closed cubic: its vertices are the halving of the whole range at 1 / (M (p + 1)^2), as an open cubic's, with each
      knot standing where the vertex nearest it stood (the knot 40 in the place of 39.05 at 10), and the last vertex kept on
      the first; each arc's bulge is the turn of the tangent over the piece the halving made, not over the knots' spans
      (0.418 from (0, 0) to (40, 0) at 0, where the span from 0 to 40 turns as 0.480). Every count, and every vertex and bulge
      at 0 and 10, to 1E-12.
    - Now: so does the library for a periodic spline, and keeps the last vertex of a closed one on its first (D15).
    - AutoCAD 2021 (plugin v8, 17f): the same cubic as the library wrote it in 17-f, of no fit points (as AutoCAD's own DXF
      of v7 keeps it), read back with `IsPeriodic` false and `NumFitPoints` 0, is the halving alone at every precision,
      (0, 0), (34.42, -3.51), (50.86, 28.40), (18.15, 30.49) and the first again at 0, the counts and bulges as before; the
      knots stood in place only in v7's cubic, which ran through its fit points. Two periodic cubics of fit points: through
      (0, 0), (30, 0), (30, 15), (20, 15), knots 0, 30, 45, 55, 80, at 0 the parameters 0, 20, 45, 55, 80 (the knot 30,
      halfway between 20 and 40, at neither, 45 in the place of 40); through (0, 0), (40, 0), (40.5, 0.5), (50, 30),
      (10, 25), at 0 0, 40.71, 71.70, 112.01, 138.94 (the knot 40.71 in the place of 34.73, where the knot 40, nearer it,
      would stand); every vertex at 10 too (54 and 51).
    - Now (D16): a periodic spline of fit points has each knot, one after another, in the place of the vertex nearest it,
      the later of two as near, a later knot taking the place an earlier one took; any other closed spline is the halving
      alone, its last vertex on its first. Every vertex of the four polylines of 17f at 0 and 10, and of 17-f's, to 1E-9.
    - AutoCAD 2021 (plugin v9, 17g): its own HALFWAY of v8, its 70 3115 and no fit points in its DXF, read again as
      periodic with 5 fit points, the knots in place (0, 20, 45, 55, 80 at 0); the library's 17-g, the same cubics written
      with 70 11 and their fit points, read as not periodic and fitted anew through its fit points, another curve: 44 and 42
      vertices at 10 where the library gives 54 and 51, and NEAR at 0 at 0, 40.71, 55.57, 71.70, 111.15.
    - Now (D17): the knots stand in place too for a periodic spline of the flag 1024 of its 70 (AutoCAD's 3115), its fit
      points kept by AutoCAD though its DXF writes none: the 26 polylines AutoCAD made of its periodic cubics of v7 and v8 are
      the library's to 1E-9, where 24 were not; its closed cubic that is not periodic (70 11, no flag 1024), and 17-f's (70
      3), stay the halving alone, as AutoCAD made them. Parked, the third round: a closed spline of 70 11 with fit points, a
      form AutoCAD does not write, which it fits anew through its fit points as a spline not periodic; the library keeps its
      control points, so neither the knots in place nor the halving alone gives AutoCAD's 44 and 42 vertices.
    - Measured (D18), not changed: `IsPeriodic` reads the flag 2 of 70, true for AutoCAD's 3115 and for 11 and 3; AutoCAD read
      its 3115 as periodic and 11 (its own closed cubic and the library's 17-g) and 3 (17-f) as not, so its periodic spline
      is told by 32, 1024 or 2048, which 3115 has and 11 has not, and the flag 2 is written on its closed splines too. It
      read 5 fit points on its 3115, whose DXF has none: the points it fits through, which the library does not work out
      from the control points. Still to see (v11, 17-h): 3115 with each of 2048, 1024 and 32 taken out, and 11 with each
      put in.
    - AutoCAD 2021 (plugin v11, 17h): periodic exactly where the flag 2048 is set: 3115, 2091 (less 1024), 3083 (less 32) and
      2059 (11 and 2048) are, 11, 1067 (less 2048), 1035 (11 and 1024) and 43 (11 and 32) are not. Fit points the DXF does
      not hold exactly where the flag 1024 is set: 5 on 3115, 1067, 3083 and 1035, none on 2091, 11, 2059 and 43; they are the
      points of the curve at its knots, (0, 0), (30, 0), (30, 15), (20, 15) and (0, 0) at 0, 30, 45, 55 and 80, and (0, 0),
      (40, 0), (50, 30), (10, 25) and (0, 0) for 1035, every one within 1E-14 of the library's curve there. A periodic spline
      AutoCAD gives in its own form: 4 control points and the 5 knots 0, 30, 45, 55, 80 of 3115, 2091 and 3083, and 4 and
      0, 40, 71.62, 111.93, 138.86 of 2059, whose DXF holds 7 control points and 11 clamped knots; 1067 and 1035 read as their
      DXF holds them.
    - Now (D19): `IsPeriodic` is the flag 2048, and `NumFitPoints`, `FitPoints`, `GetFitPointAt` and `FitData` give the
      points at the knots where the flag 1024 is set and the record holds no fit points: all eight readings of 17h, and those
      of v8 and v9 (17-f and AutoCAD's closed cubic not periodic, the library's 17-g not periodic with its 5 and 6 fit points,
      AutoCAD's 3115 periodic with 5), are AutoCAD's. The flag 2 is still written for a periodic spline and read back by
      `NurbsData.Periodic` and `FitData.Periodic`, and `ToPolylineWithPrecision` places the knots by it as before, so every
      polyline of v6 to v9 that matched still matches, each the same to the bit. Not done: the 4 control points and 5 knots
      AutoCAD gives a periodic spline, the library giving those of its record.
    - AutoCAD 2021 (plugin v12, 17j): the knots of all eight are in place by the flag 1024, which is where AutoCAD gives fit
      points, as the library places them. `NurbsData.Periodic` is the flag 2048, as `IsPeriodic`: True on 3115, 2091, 3083
      and 2059, False on 11, 1067, 1035 and 43. `FitData.Periodic` is False on all four that have fit data, 3115 and 3083 of
      the flag 2048 among them, and the getter throws on the four that have none. The 4 control points AutoCAD gives a
      spline of the flag 2048 are those of its record with its right end unclamped (The NURBS Book, A12.1): of the 7 and the
      clamped knots of the record, unclamped at the right end, the points from the degree on, 3 to 6, and the knots from the
      degree on, 0, 30, 45, 55 and 80; within 2E-14 on 3115, 2091, 3083 and 2059.
    - Parked (D20): `NurbsData.Periodic` and `FitData.Periodic` still give the flag 2, and the control points and knots
      those of the record. `NurbsData.Periodic` cannot follow AutoCAD and leave every written byte as it was: a spline of 70
      11 given its own data back by `SetNurbsData(..., data.Periodic, ...)` would lose its flag 2, which the same call made
      not periodic takes off, and AutoCAD gives the flag 2048 with the 4 points and 5 knots of its own form, which
      `SetNurbsData` refuses. Still to see (v13): AutoCAD's `SetNurbsData` and `FitData` set on its 11 and 1035; and 17-m, a
      rational spline of the flag 2048, 17-n and 17-o, 2059 of a seam made not smooth at its left and at its right, of
      after-d20, whose 4 points the rule gives (`_notes-d20.txt`).
    - AutoCAD 2021 (plugin v13, 17k): `NurbsData = d` on its 11 saved 70 11; with `d.Periodic` false, 11; with `d.Closed`
      false, 11, the control points as they were and read closed: its closedness is the curve's, its ends meeting. Its
      3115 given its own form back (4 points, 5 knots, periodic) was saved 70 2059, 7 points and 11 clamped knots, the
      flags 32 and 1024 dropped. 17l: `FitData` set on its 1035, a fit point moved, not periodic, saved 70 1067 (closed, 32
      and 1024 set, its 5 fit points and its tangents written); on its 3115, of no tangents, 70 1065, closed but without the
      flag 2, its new seam a corner of 114 degrees; set periodic, 70 3115 on both, no fit points (74 0), no tangents, no 44,
      7 control points and 11 clamped knots at the lengths of the chords. Its own saves of closed splines all bear the flag 2
      where the seam runs on in one direction (11, 1067, 2059, 3115) and not where it turns (1065). 17m: 17-n and 17-o read
      not periodic, 7 points and 11 knots, the polylines of the record's curve; the rational 17-m periodic, its 4 points the
      right and the left unclamping alike.
    - Measured (D21): AutoCAD's periodic fit is the periodic cubic through the points at the lengths of the chords, each
      point at its knot, clamped at the first knot and a period on: its 17l saves of 1035 and 3115 to 1E-14. Its closed fit
      not periodic is the clamped cubic through the points at the same knots, its derivative at each end the unit tangent
      given (1067), or its second derivative nought at each end where none is given (1065), which is why its seam has a
      corner; the library fits otherwise (`GeoSpline3.Interpolate`, its knots averaged), up to 10 off AutoCAD's curve.
    - Now (D21): `SetNurbsData`, `NurbsData` set and the constructor write the flag 1 where the curve's ends meet (within
      1E-3) and the flag 2 where it also leaves its start in the direction it reaches its end (within a degree), whatever
      `closed` and `periodic` say (`periodic` still writes the flag 2), and take off the flags 32 and 1024: 11, 11 and 2059
      as AutoCAD saved them. `FitData` set keeps a closed spline closed, the flags 32 and 1024 added (1067), and set
      periodic fits AutoCAD's periodic cubic, written 70 3115 with no fit points, its points at its knots the fit points, the
      control points AutoCAD's to 1E-14 on both; an even degree through an even count of points, whose system is singular,
      and points two of them as one, fall back to the closed fit. `IsPeriodic` is the flag 2048 on a curve whose start and
      end unclamped give the same points a period apart, within 1E-3 (AutoCAD's periodic splines to 1E-13, 17-n off by 20.6,
      17-o by 48.4). Ripple over every DXF of the checks folder: the readings of 17-n and 17-o; the flags written by the
      edits above and nothing else; no polyline. Parked: AutoCAD's closed fit not periodic (natural ends or unit tangents at
      the chords' knots; the library's keeps its own, so its flag 2 on AutoCAD's 3115 refitted is its own, 1067 where AutoCAD
      wrote 1065); the 4 points and 5 knots of AutoCAD's periodic form, which `NurbsData` does not give; `NurbsData.Periodic`
      and `FitData.Periodic`, still the flag 2 (AutoCAD's are the flag 2048, and False on every spline of fit data).
      Still to see (v14): whether a seam smooth in its direction but not in its bend (1067's curve given the flag 2048) is
      periodic, which tells the unclamping from the turn of the tangent; the tolerances of both, a seam turned by 0.01, 0.5
      and 2 degrees and moved by 1E-6, 1E-4 and 1E-2; and the library's after-d21 files.
    - AutoCAD 2021 (plugin v14): 17p read periodic and closed, its 4 points AutoCAD's own periodic fit to 1.2E-13, its 5
      fit points the points at its knots, saved 70 3115 with no group 11. 17q read closed, not periodic, its 5 fit points and
      tangents as written; its control points are neither its own fit's (2.37 off) nor the library's (2.30 off), recorded.
      17s, the curve of its 1067 given the flag 2048, read not periodic: AutoCAD reads the seam by the unclamping, not by
      the tangent's direction. 17t: the 2059 of the control point 1 moved by 1E-6, 1E-4, 1E-3 and 1E-2, its seam off by
      4.1E-6 to 4.1E-2, read not periodic, all four, where the library read the first two periodic. 17u: its 11 of the seam
      turned by 0.01, 0.5 and 2 degrees, given its own NURBS data, saved 70 9 each, where the library wrote 11 for the first
      two. 17v: fit data of degree 2 set periodic through (0, 0), (40, 0), (50, 30) and (10, 25), an even degree through an
      even count of points, gave its periodic cubic, 70 3115, degree 3, its control points those of the same cubic asked a
      degree 3, where the library fell back to the closed fit. `NurbsData.Periodic` read False on every spline not periodic
      by `IsPeriodic`, the flag 2 set or not, and True on 17v's.
    - Measured (D22), over every spline AutoCAD saved in `autocad-out`: the seams of its periodic ones unclamp to 1.3E-15 of
      their size at most (7.9E-14 at 60.4), and 17t's least, off by 6.7E-8 of its size, it read as not periodic; its closed
      splines of the flag 2 leave their start in the direction they reach their end to 4E-16 of a radian, and 17u's least
      turn, a hundredth of a degree (1.75E-4 of a radian), lost the flag.
    - Now (D22): `IsPeriodic` reads the seam within 1E-10 of the spline's size, one and its largest coordinate (6.2E-9 at
      17t's 61.5), well inside both, so that a spline scaled to millions or moved far from the origin reads as before;
      `SetNurbsData` writes the flag 2 where the seam turns by 1E-9 of a radian at most, whatever the size; and periodic fit
      data of any degree give the periodic cubic, three points a quadratic and two a line, falling back to the closed fit
      only where two points are one, within 1E-3. 17t, 17u and 17v are AutoCAD's answers now. Of 4 000 random rings of 3 to
      60 points, degree 1 to 10 asked, sizes 0.01 to a million and up to a million from the origin, every one of distinct
      points fitted the periodic cubic and read periodic, its fit points within 7.9E-16 of its size. Ripple over every DXF
      of the checks folder: the readings of 17t's 1E-6 and 1E-4 and the flags written by edits of 17t and 17u, nothing else.
    - AutoCAD 2021 (plugin v15, after-d22): 17w: the 2059 of size 61.5 with the control point 1 moved by 1E-12 and 1E-11
      (the seam off by 4.1E-12 and 4.1E-11) it read as periodic, of 4 control points, and moved by 1E-10 to 1E-7 as not, of
      7: its own point tolerance, 1E-10, where the library read up to 1E-9 as periodic. 17x: the 11 turned by 1E-8 degrees
      (1.75E-10 of a radian) to 1E-3, given its NURBS data, it saved 70 9 each, where the library kept the flag 2 at 1E-8.
      17y: fit data set periodic of degree 1, 2, 4 and 5 through six points it took, and wrote each its periodic cubic,
      70 3115, 9 control points and 13 knots, the library's to 3.6E-14; the library's files it read periodic, of degree 3.
    - Now (D22b): `IsPeriodic` reads the seam within 1E-10 and 1E-14 of the spline's size for rounding (1.006E-10 at 61.5),
      and `SetNurbsData` writes the flag 2 where the seam turns by 1E-12 of a radian at most. 17w and 17x are AutoCAD's
      answers now. The spline writers are parked from here.
    - Degree 4, 5 and rational, parked: the tolerance the counts fit is the larger of 1 / M and the spline's length over
      10 000, over (p + 1)^2: the quartic (M 120, length 47.7) takes 1 / M, every one of its nine counts; the quintic Bezier
      (M 400, length 55.00), the quintic of two spans (M 223.6, length 70.085), the rational cubics 1, 2, 2, 1 (M 600, length
      35.457), 1, 3, 1, 1 (M 1 910, length 35.388) and 1, 2, 0.5, 3, 1 (M 1 867, length 49.366) take the length. The rule
      gives 88 of the 92 counts of all thirteen curves and misses four by one to three vertices: the quintic Bezier at 5 (476
      where AutoCAD gives 478), the cubic 1, 3, 1, 1 at 5 (595 for 594) and the rational of v6 at 10 and 99 (1 378 for 1 377,
      12 081 for 12 078), each a piece whose middle lies within 0.2 % of the tolerance. The three of the rational curves
      would fit a length 0.02 % above the true one, as a length AutoCAD works out by its own sum might be; the quintic at 5
      fits no length, its count at 3 asking a tolerance 0.2 % larger than its count at 5 allows. The library keeps 1 / M for
      every spline (117 where AutoCAD gives 96 for the quintic of two spans at 0) until the four are understood.
    - AutoCAD 2021 (D32, its own plugin run headless in accoreconsole on 2 November 2026, side databases): fitting. Its
      constructors and `FitData` fit a spline not periodic as one cubic whatever degree they are given (1 to 5, `FitData.Degree`
      3), its knots the lengths of the chords, at an end of a tangent the derivative that tangent at its own length (the
      constructors make it unit, `FitData` keeps it: 5 stays 5), at an end of none, or a tangent of no length, no second
      derivative (ezdxf's `fit_points_to_cad_cv`): its control points of 44 fits, open, closed, 3D, of two to seven points,
      with tangents at both ends, one or none, are that rule's to 3E-13. Points one after another a trillionth apart are one;
      a millionth apart, two (that fit the rule's to 4E-8, its system near singular). A fit tolerance t gives a curve up to 1.5 t off the points (1E-12 and its default 1E-10 none).
      Reading: a SPLINE of fit points is fitted anew so, whatever its control points (moved by 1), knots (doubled), degree (2,
      5), flags (8, 40 for 1064) or its having none, which is why the library's 17-g of v9 read as another curve. Edits: a
      move, turn, scale or stretch keeps the length of a tangent; `ReverseCurve`, `InsertFitPointAt`, `RemoveFitPointAt`,
      `SetFitPointAt` give the fit anew; `SetControlPointAt`, `UpdateFitData` and `PurgeFitData` leave no fit data (1064 saved
      8, 1067 11, 3115 2059); `InsertKnot` keeps the fit points at the knots (1064 saved 1288, 1067 1291, 3115 3339);
      `SetWeightAt` keeps the fit data and weights, which AutoCAD drops when it reads the file again.
    - Now (D32): fit data not periodic, the reader's spline of fit points alone and `DxfSpline`'s are AutoCAD's cubic, and a
      record whose control points are not its fit is read as the fit (its own to the bit where they are); the library's 87
      edited files of AutoCAD's cubics through five points read back in AutoCAD as the library reads them to 3.5E-14, but
      those of a fit tolerance of 0.5 (0.49 off, the fit within a tolerance not done) and of a weight set (AutoCAD's own
      inconsistency); 17-g's HALFWAY at 0 and 10 (44 vertices) and NEAR at 10 (42) are AutoCAD's, NEAR at 0 is not (parked).
    - AutoCAD 2021 (D32): precision. A hairpin cubic 100 by 1 over the knots 0 to 1 gives 129 vertices at 0, over 0 to 100
      5, the same at 1 by 10; a quintic Bezier and the same ten times its size 86 at 0 and 7 929 at 99; a rational cubic of
      the weights 1, 3, 1, 1 and the same ten times its size 112 and 10 729; an S of three spans whose middle lies on its
      chord 173 at 0. Closed cubics of fit points not periodic (70 1065), of 3 to 8 points: each knot in the place of the
      vertex of the halving nearest it, as on a periodic one.
    - Now (D32): the tolerance is max(1 / M, L / 10 000) / (p + 1)^2 and each piece judged at its middle and its quarters:
      147 of AutoCAD's 156 counts at hand, where 1 / M and the middle alone gave 50; the 9 others within 3 (the rational cubics
      by 1 to 3, the S at 20 by 2, the quintic over 0 to 50 at 0 by 1). A closed spline of fit points has its knots in
      place, the flag 2 or not: 52 of the 72 polylines of the twelve closed and periodic cubics at 0, 1, 2, 3, 5 and 10, where
      27 were. Parked: the closed cubics at 0 of 5 points or more (AutoCAD's vertices at 0.4 and 0.8 of the range, no closing
      vertex), the periodic cubic through 3 points, and 7 polylines a vertex off.

18. **The narrowest ellipse.**
    - Try: squash an ellipse to a ratio of 1E-7 by `TransformBy`, and project a circle onto a plane seen almost edge on.
    - Now: `TransformBy` writes a ratio of 1E-6 at the least, the DXF reference's floor, and a projection whose minor axis comes
      within 1E-6 of its major one's length of nothing is refused as a line.
    - AutoCAD 2021: agrees: it reads 1E-6, and refuses to explode the INSERT scaled (1, 1E-7, 1) rather than make a thinner
      ellipse.

19. **A 3DFACE's hidden edges in an intersection.**
    - Try: `IntersectWith` of a line and a 3DFACE whose crossed edge is invisible.
    - Now: the face meets along all four of its edges, its hidden ones among them, as its shape has no hidden edges.
    - AutoCAD 2021: agrees: the line meets the face at (10, 5, 0) on its hidden edge.

20. **A text in an intersection.**
    - Try: `IntersectWith` of a line and a TEXT or an MTEXT.
    - AutoCAD 2021: crosses the line with the box of the words, at (10, 0, 0) and (10, 5, 0) for the TEXT and (10, 6.67, 0) and
      (10, 20, 0) for the MTEXT.
    - Now: refused with `NotSupportedException`: the box needs the size of the words, which the library cannot have without
      fonts. The guide says so.

## Objects and tables

21. **The rotation of a linetype's shape.**
    - Try: a complex linetype whose text is turned by 30 degrees, its group 50 read in AutoCAD.
    - Now: the file holds degrees (30), as ezdxf writes them; `ShapeRotationAt` gives radians.
    - A change if: AutoCAD writes radians in group 50.
    - AutoCAD 2021 (plugin v2): the check stopped at `TextAt(0)`, which threw eNotApplicable for the first dash of
      GH_GAS30, a dash of no text (74 of 0); the file was not saved, so what AutoCAD writes in group 50 is still not seen. No
      file AutoCAD saved here holds a linetype of a text or a shape.
    - Others: ezdxf 1.4.5 writes the `R=` of a .lin as it stands, degrees (`tools/complex_ltype.py`, the code 50 of `r`), and
      netDxf 2023.11.10 wrote 50 = 30.0 for a `LinetypeTextSegment` turned by 30 (its `Rotation` in degrees); both read the
      group so. The library is unchanged.
    - Still open: `TextAt` of a dash of no text gives an empty text, where AutoCAD's throws eNotApplicable; kept, as the
      library's own reading, until it is asked for.

22. **The current transparency.**
    - Try: set CETRANSPARENCY to the alpha 100 through `Cetransparency` and read it in AutoCAD.
    - AutoCAD 2021: reads the 61 the library wrote, the per cent, as BYLAYER: its `Transparency` is the number 61, its method
      nought. The DICTIONARYVAR holds the number an entity's 440 holds, the method in its top byte and the alpha in its low
      one.
    - Now: written as that number, 33554532 for the alpha 100, which reads back as 100; a number with no method in its top
      byte, a per cent the library wrote before among them, reads BYLAYER, as AutoCAD reads it.

23. **Binary data under a key.**
    - Try: `SetBinaryDataForKey` of 300 bytes, then AutoCAD's `GetBinaryDataForKey` of the same key.
    - Now: an XRECORD of the key in the extension dictionary, its bytes in groups 310 of 127 bytes each.
    - AutoCAD 2021: agrees: `GetBinaryDataForKey` gives the 300 bytes back.

24. **A hyperlink to a place in a drawing.**
    - Try: a hyperlink with a `SubLocation`, read in AutoCAD's HYPERLINK dialog.
    - Now: written in the extended data of PE_URL as AutoCAD writes it.
    - AutoCAD 2021: agrees, through its API: the name, description and place as written.

25. **A block's preview.**
    - Try: set `PreviewIcon` of a block and look at it in AutoCAD's INSERT dialog.
    - Now: its bytes in the BLOCK_RECORD's 310 groups, after its other groups.
    - A change if: AutoCAD wants them in another place of the record.

26. **The drawing's properties.**
    - Try: set `SummaryInfo` with a hyperlink base and two custom properties in a drawing of R2004 or later and of R2000, and
      open DWGPROPS in AutoCAD; save from AutoCAD as DXF and see whether it writes DWGPROPS or `$CUSTOMPROPERTYTAG` in the
      header.
    - AutoCAD 2021: reads the title and the author of the DWGPROPS of both; the custom properties of the ten groups 300 to
      309 of R2000, and none of the list after the group 90 of R2018; and the hyperlink base of neither, reading it from the
      header's `$HYPERLINKBASE`, empty in both. Saved as R2018, it writes no DWGPROPS but `$TITLE` and `$AUTHOR` after
      `$DWGCODEPAGE`; saved as R2000, a DWGPROPS of the ten groups, the dates, the hyperlink base, then a 90 of the count of
      the custom properties, 2.
    - Now: the DWGPROPS of R2000's form in every version, the custom properties in the ten groups (ten at most) and their
      count after the hyperlink base, which goes to `$HYPERLINKBASE` too; each text a header holds already (a drawing AutoCAD
      saved) is set there as well. A drawing with no DWGPROPS gives the texts of its header (`$TITLE`, `$SUBJECT`,
      `$AUTHOR`, `$COMMENTS`, `$KEYWORDS`, `$REVISIONNUMBER`). The custom properties of a header (`$CUSTOMPROPERTYTAG`), which
      no file of AutoCAD's here holds, are not read.
    - AutoCAD 2021 (plugin v4): reads the title, the author, the hyperlink base and both custom properties of the library's
      26-a (R2018) and 26-b (R2000); its SAVEAS of either as R2018 writes no DWGPROPS, but `$TITLE` and `$AUTHOR` and then
      each custom property as a pair `$CUSTOMPROPERTYTAG` and `$CUSTOMPROPERTY` after `$DWGCODEPAGE`, and `$HYPERLINKBASE`.
      Writing the header's form needs a variable named more than once in the header, which the library's header keeps by
      name: parked, the library's DWGPROPS being read as written.
    - Now (D15): a drawing of R2004 or later keeps its properties in the header, as AutoCAD 2021 saves one of R2018 (and as
      its R2007 save of point 73 holds no DWGPROPS): set, the texts not empty in `$TITLE`, `$SUBJECT`, `$AUTHOR`,
      `$KEYWORDS`, `$COMMENTS` and `$REVISIONNUMBER`, then a `$CUSTOMPROPERTYTAG` and a `$CUSTOMPROPERTY` for each custom
      property, as many as there are, after `$LASTSAVEDBY` where the header has one (where ezdxf writes its pairs) else after
      `$DWGCODEPAGE`, and the DWGPROPS taken out; read, a drawing with no DWGPROPS gives the header's pairs in their order. A
      drawing of R2000 keeps the DWGPROPS of the ten groups, as AutoCAD saves one. Who saved it last is not written in the
      header's form.
    - Still open: where AutoCAD puts `$SUBJECT`, `$KEYWORDS`, `$COMMENTS`, `$REVISIONNUMBER` and `$LASTSAVEDBY` among the
      others, which its files here do not hold, and whether it reads more than ten custom properties.
    - AutoCAD 2021 (plugin v8): reads all three files of after-d15, the twelve custom properties of 26-e among them; saved as
      R2018, R2010 and R2004, after `$DWGCODEPAGE`: `$TITLE`, `$SUBJECT`, `$AUTHOR`, `$KEYWORDS`, `$COMMENTS`,
      `$LASTSAVEDBY`, `$REVISIONNUMBER`, then the pairs; saved as R2000, the twelve in a DWGPROPS, ten in 300 to 309 and
      the two more as 300 after the count, `90 12`.
    - Now (D16): the header's `$LASTSAVEDBY` moved into the run before `$REVISIONNUMBER`, its value kept, and the run after
      `$DWGCODEPAGE`, as AutoCAD writes it. A DWGPROPS of R2000 still takes ten custom properties at most (parked: the two
      more after the 90 that AutoCAD writes are not written yet).

27. **Empty groups.**
    - Try: in AutoCAD, empty a group by erasing its last member, by removing it, and make one with no member; save.
    - AutoCAD 2021: keeps all three, G_ERASE, G_REMOVE and G_NEW_EMPTY, each of no member, after it saves the drawing and
      opens it again (through its API; the ERASE and GROUP commands not run).
    - Now: so does the library for a named group. An anonymous group left empty is still dropped when the drawing is saved,
      which AutoCAD was not asked about.
    - AutoCAD 2021 (plugin v4, 27x): keeps the anonymous *A1, *A2 and *A3 too, emptied by an erase, by `Remove` and made
      with no member, still anonymous and of no member after it saves the drawing and opens it again.
    - Now: so does the library: no group left empty is dropped.
    - AutoCAD 2021 (plugin v5, 27x): kept GH_ANON_ERASE, its only line erased, where the library's 27x-b had dropped it: an
      anonymous group emptied by an erase still went when the drawing was saved.
    - Now: it is kept, of no member, as one emptied by `Remove` or `Clear`.

28. **An anonymous group renamed.**
    - Try: rename an anonymous group in AutoCAD.
    - Now: given a name, it is anonymous no longer (group 70 of 0), by `DxfGroup.Name` and, since it was found making the
      files that it did not, by `GroupDictionary.SetName`.
    - A change if: AutoCAD keeps it anonymous.

29. **Records of R2018 copied into R2000.**
    - Try: open in AutoCAD 2000 a drawing of R2000 into which the library copied a DIMSTYLE and a layer from a drawing of R2018.
    - Now: the groups R2000 has not are left out, as AutoCAD's SAVEAS to R2000 leaves them out, said in the log at Debug: a
      DIMSTYLE's 49, 50, 69, its second 70, 290, 294 and 345 to 347, and a LAYER's 420 and 430 (R2004) and 347 and 348
      (R2007), the versions as ezdxf gives them; so are the overrides of a dimension style (ACAD DSTYLE) of a variable R2000
      has not, as DIMLTYPE and DIMFXLEN.
    - A change if: AutoCAD 2000 or AUDIT still reports the records, or AutoCAD's SAVEAS keeps a group the library leaves out.
    - AutoCAD 2021: refused the library's copy, "Did not receive PlotStyleName": the layer GH_TRUECOLOR came with no plot
      style (390). The library had not found the plot style Normal the source's layer named, as the drawing keeps
      ACAD_PLOTSTYLENAME in a dictionary with a default (ACDBDICTIONARYWDFLT), which it did not look in; any entity copied
      between drawings of R2000 or later onto a layer the drawing had not brought that layer so.
    - Now: the plot style of the name found there, and a layer whose plot style the drawing has none of given layer 0's, so
      that every LAYER copied into R2000 or later has a 390.
    - AutoCAD 2021 (plugin v4, `after-d11`): refused 29-b again, "in AcDbRotatedDimension starting at line 5890: Unexpected
      DXF group code: 280": the DIMENSION copied from R2018 kept its version (280 of AcDbDimension), which R2010 brought.
    - Now: a dimension copied into a drawing before R2010 is written without it, as AutoCAD's SAVEAS writes one.

30. **A dimension copied into another drawing.**
    - Try: copy a dimension with the library into a drawing that has none of its text style or arrow blocks, and open it.
    - Now: the text style, the arrow blocks and the dimension style come with it by name.
    - A change if: AutoCAD shows another text style or other arrows than in the drawing it came from.
    - AutoCAD 2021: refused the copy as it refused point 29's, the layer Defpoints come with no plot style; the same change
      lets it open the file now (`after-d11`).
    - AutoCAD 2021 (plugin v4): refused 30-b for the dimension's 280 at line 1686, as point 29's; written without it now.

## Viewports and layouts

31. **A viewport's flags.**
    - Try: set `FrontClipAtEyeOn`, `GridBoundToLimits` and `GridSubdivisionRestricted` on a viewport, and look at it in
      AutoCAD; set an isometric snap.
    - Now: the three flags AutoCAD keeps inverted (0x10, 0x40000, 0x100000) are written inverted; the isometric pair as ezdxf
      writes it.
    - AutoCAD 2021: agrees, through its API (`FrontClipAtEyeOn`, `GridBoundToLimits`, `GridSubdivisionRestricted`,
      `SnapIsometric`); the grid as drawn is for the eye to see.

32. **A viewport's light and number.**
    - Try: read the ambient light colour of a viewport with no group 63, and the `Number` of a viewport turned off.
    - AutoCAD 2021: the colour index 250, as the library; `Number` -1 for the viewport turned off, and -1 for every viewport
      until its layout is shown, then 1 and 2 for those on.
    - Now: the colour index 250; the number from group 69 as written, on or off: AutoCAD's `Number` is the number as the
      layout is shown, which a file does not say. The guide and `Number` say so.
    - Now (D16): `Number` follows AutoCAD's rule for a layout shown: the id of group 69 for a viewport on, -1 for one turned
      off (the viewport 94 of id 3 gives -1); the id of one turned off is kept and not given to a new viewport, and the
      viewport 1 is still found by its id.

33. **A view of R12, R13 or R14.**
    - Try: set the view of a viewport in a drawing of R12 (its extended data MVIEW) and of R14, and open them.
    - AutoCAD 2021: reads the centre and height of R12 as written, but the twist the library wrote, 30 for 30 degrees, as
      1 718.87 degrees: 30 radians.
    - Now: the twist in MVIEW is written and read in radians (group 51 of R2000 on in degrees, as before); the view of R14 is
      still to be tried.
    - AutoCAD 2021 (plugin v4, 33-b): reads the snap angle in MVIEW in radians as well, the library's 30 as 1 718.87 degrees,
      and writes a snap of pi/6 there as 0.5235987755982988.
    - Now: the snap angle in MVIEW is written and read in radians too.

## Hatches, leaders and multileaders

34. **A predefined pattern with no lines.**
    - Try: open a HATCH made in code with `SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31")`, which holds no pattern
      lines.
    - Now: its name and type are written for AutoCAD to take the lines from acad.pat.
    - A change if: AutoCAD draws nothing until the hatch is edited; the library should then write the lines of the common
      patterns.

35. **Where a pattern is scaled and turned from.**
    - Try: a hatch of ANSI31 at an elevation and a normal other than up, its `PatternScale` and `PatternAngle` set, against the
      same done in AutoCAD.
    - Now: the lines are scaled and turned about the origin of the hatch's own system.
    - A change if: AutoCAD's lines start elsewhere.

36. **A hatch's background colour and gradient.**
    - Try: set `BackgroundColor` to an index and `SetGradient` on a new hatch, and open it.
    - Now: the background in the extended data HATCHBACKGROUNDCOLOR, an index packed as 0xC3 and the index; a new gradient blue
      (0, 0, 255) to pale yellow (255, 255, 153).
    - A change if: AutoCAD shows another colour.
    - AutoCAD 2021, so far: its DxfIn refused the whole file ("expected group code 78"): the pattern HATCH set by
      `SetHatchPattern` had no 78. The count of no lines is now written; the colours are to be tried again on the new file.

37. **A leader's annotation offset.**
    - Try: a leader whose `AnnotationOffset` is set, moved and turned, against the same in AutoCAD.
    - Now: groups 213, 223 and 233 in the world, turned with the leader.
    - A change if: AutoCAD reads the offset another way round, or does not turn it.

38. **A multileader's lines and vertices.**
    - Try: `AddLeaderLine`, `AddLastVertex` and `SetLastVertex` on a multileader, and open it.
    - Now: a line's last vertex is the point its leader's dogleg starts at, shared by every line of the leader; line indices are
      unique across the multileader.
    - A change if: AutoCAD keeps the last vertex per line, or numbers lines per leader.

39. **A multileader's scale.**
    - Try: set `Scale` to 2 on a multileader and open it.
    - Now: the words, the arrows and the landing gap of its context data scaled by 2, as ezdxf scales them, and the doglegs too,
      the multileader's (41) and each leader's (40), which ezdxf leaves; the arrow size in the context (140) is `ArrowSize`.
    - A change if: AutoCAD keeps a dogleg or the landing gap at its size, or holds 140 as the arrow size times the scale.

40. **A multileader's overrides.**
    - Try: set a property of a multileader, open it in AutoCAD, and change the property in its style.
    - Now: the property is written in the multileader and its context data, and its bit of the overrides of the style set.
    - AutoCAD 2021: through its API neither multileader follows the style's arrow size made 10: the one of its own 6 keeps 6,
      the other its 4, exploded with arrows 6.08 and 4.06 long. The library reads each multileader's own size (42) and has no
      setter of a style's, so it agrees; whether the MLEADERSTYLE dialog carries the change on is AutoCAD's editor's.

41. **A new multileader and one of a block.**
    - Try: open a `new DxfMLeader()` with words and one with a block placed at its base point.
    - Now: written as ezdxf writes them.
    - A change if: AutoCAD reports or fixes either; or draws the block of a multileader from its group 15 and not from its matrix
      47, or the other way.
    - Found making the files: a multileader made in code starts its leader's dogleg at the base point of its content, which
      is the words' own point, and at the origin for the block placed after its leader line was added. Where AutoCAD lands
      it, and whether it lands it anew on opening, is to be seen in this point's answer.
    - Was open: no file AutoCAD saved held a MULTILEADER, so the landing was left as it was. ezdxf 1.4.5's builder
      (`render/mleader.py`, `_build_leader`) lands a leader at the connection point of the side of the content it comes to,
      the landing gap within it, less the dogleg: `connection_point - dogleg_direction * dogleg_length`, the dogleg pointing
      at the content.
    - AutoCAD 2021 (D31, headless, plugin GHD31 rounds A, B, D, G and H): ezdxf's reading. AutoCAD keeps a file's landing
      as it is on opening, and lands a leader it adds (`AddLeaderLine(point)`) the gap and the dogleg short of the side of the
      content facing the point, chosen by the content's middle; the words' side is as wide as its font makes them, so only
      the left side of words held at their top left is known without a font. See point 80.

## Dimensions and tables

42. **A dimension or a table whose data changed.**
    - Try: open a drawing in which a dimension's point and its style's text height, and a table's cell, were changed by the library.
    - Now: the blocks that draw them (`*D`, `*T`) are left as they were.
    - A change if: AutoCAD shows the old picture even after REGEN; then the library would have to draw them anew, or mark them for
      AutoCAD to.

43. **A table of R2007 written by hand.**
    - Try: open a table whose cells were set in a drawing of R2007 or later, with a cell of overrides.
    - Now: the cells between `301 CELL_VALUE` and `304 ACVALUE_END`, the overrides in groups 177 and 91, bit k of the flags the
      property 130 + k, grouped by property.
    - AutoCAD 2021: agrees: the cell reads SET, 4 high, red, held at its middle centre.
    - AutoCAD 2021 (D28b, headless): it writes a cell's overrides before its 92 (`170 5`, `7 GH_TXT`, `140 3.5`) and reads
      one written after its value too, the last of two (D28 e14); a merge's top left cell `173 0` and its spans (175, 176),
      the others `173 1`, and reads a merge by the spans alone (g2), not by a `173 1` of no spans (g3); a line of a cell
      only where the cell's edge flags (172: 1 top, 2 right, 4 bottom, 8 left) name its edge, wherever its group stands,
      with or without a TABLECONTENT (G1 to G10), and a line between two cells as the top or left of the one below or right
      (x3, x4). Its new cells of `InsertRows`, `InsertColumns` and `SetSize` it writes `93 7`, `90 0`, read as of no value,
      those of `InsertRowsAndInherit` `93 5`, `90 4`; the title merged across the table it grows over columns put in first,
      last or among them, and over those of `SetSize`, a merge of another row moved on (c1 to c8, s1, s2); a column of
      `SetSize` as wide as the text height of the first row whose new cell is merged with none and the margins either side
      (7.5 of the header's 4.5 and 1.5; 12 for a header of 9, its own or its style's; 10.5 for margins of 3; 9 of the
      title's 6 where it is unmerged; s1 to s16); the heights and widths of its TABLECONTENT as reals (`10.0`); a formula's
      field of a row it takes out out of the TABLECONTENT's list (`92 1`, `360 192` to `92 0`, d1, d2).
    - Now (D28b): the library writes and reads so; AutoCAD read its files of each (v01 to v14) as its own.
    - AutoCAD 2021 (D28c, headless): a line between two cells is the edge of one of them: it read the top of (2, 1) green
      and the bottom of (1, 1) red as green either side, and the right of a cell red and the left of the one right of it
      green as green (B1, B2, B8 of R2000); the top of (2, 1) of a colour alone and the bottom of (1, 1) of a lineweight
      alone as the colour and no lineweight (B12, B15); an edge flag with no override, or an override with no flag or no
      edge flag, as no line (B3, B10, B13, B14); a showing of 0 hidden, 1 and 2 drawn (B5 to B7), though its own DXF writes
      1 for a line it hides and reads it back drawn (x4 written, s17 read). Its `RemoveAllOverrides` took a cell's overrides
      out from before its 92 and its edge flags to 0 (x1 to x3). Its `InsertRowsAndInherit` gave each new cell the
      overrides of the cell above it was given, before its 92, and its edge flags (172 9), an empty value of the kind 512
      for one of no kind, and in the TABLECONTENT that cell's format and an empty value of the flags 5 with the format of
      its content (i1 to i7). Its `InsertColumns` and `SetSize` gave each new cell of a column the table's own looks of its
      row's kind as its own (the header `63 3`, `283 0`, `140 9.0`, 91 38), and a new row's cells none, which it read by the
      style alone (o1 to o7); it wrote the table's own looks in its head, after 96 and before its heights (`93 6886016`,
      `7 GH_TXT`, `140 9.0`, `170 1`, `63 3`, `64 1`, `140 9.0`, `283 0`). A table of one row, its title merged across it,
      it gave a column 1 wide (w1); a title merged across some columns and a column merged down to the last row it grew
      only by what went in among them (w2, w3, w7, m1 to m7).
    - Now (D28c): the library reads and writes so; AutoCAD read its files of each (v01 to v14) as its own edits, but for
      what is listed under "Not yet".
    - Not yet (D28c): the height of a row `SetSize` puts in, which AutoCAD made 9 under rows of 15 (o1: the data's 4.5
      times 4/3 and two margins of 1.5, it seems), where the library takes the last row's; the rows put in by
      `InsertRows(0)` or `InsertRows(1)`, which AutoCAD read as data while the title or header moved down kept its look by
      cell overrides (m7, o7), where the library reads the kind of a row by its place; `InsertRowsAndInherit` from the
      title, which AutoCAD merged as the title is and left the header out of (i3); and the lines of
      `InsertColumnsAndInherit`, which AutoCAD copied but for the top of a cell under the merged title (i5).
    - AutoCAD 2021 (D28d, headless, 66 edits and 82 files read back): `InsertColumnsAndInherit` and `InsertRowsAndInherit`
      copy every line of a cell, rows and columns alike (b1 to b8); what i5 lost was the title's: columns put in before the
      title's first column or after its last merge it anew, its other cells of no value (91 0, 93 7, 90 0) under its top
      line, and the line under it the title's bottom across every column (yellow under Qty where it was green; b1, b3, b8,
      b9, g1, g2, g4, g5), where a column among its columns grows it as it is (b2, g3); a block the library merged, its
      cells holding their text, is emptied alike only where the title is merged anew (l1, l2). Its own file of an
      inherited column holds each line between two cells once, on the cell right of it, so that it reads the left cell's
      own right line back as the right cell's left (b1, b2, b8; D28c i5 read back so too). `SetSize` makes a new row 4/3 of
      its style's data text height and twice the vertical margin, the table's own where it has one: 9, 12 for a margin of
      3, 6.333333333333333 for a style of 2.5, 8 for a style's margin of 1, whatever the rows above or the table's own text
      heights (h1 to h9). `InsertRows(0)` writes `93 3`, `280 1`, `281 1`, every row data, the title's cells given
      `170 5` and `140 6.0` and the header's `170 5`; `InsertRows(1)` `93 2`, `281 1`, the header's cells `170 5` (r1 to r7);
      a table of its own looks of the header and the data with none in its cells gave the header moved down those of its
      own too, and where a row went in at or before its first row of data took its own looks of the data out of its head
      and gave them the old rows of data (d1 to d5). `InsertRowsAndInherit` from the title or the header gave the new row
      the looks of that kind the style's data has not (`170 5`, `140 6.0`; `170 5`) unless it went in that kind's place,
      the title's merge, and left the header out where a title went in before it (i1 to i9); it merged a row of data
      merged across alike, and `InsertColumnsAndInherit` a column merged down (m1 to m3). It reads a table of no margin
      of its own by its style's (2 of a style of 2, h10). Its `MergeCells` empties the other cells of a block (p8, p9),
      where the library keeps them unseen.
    - Now (D28d): the library reads and writes so; AutoCAD read its files of 44 of the 45 edits as it read its own files
      of them back (r3), the one left a table of one row given rows by `SetSize`, whose header AutoCAD's own file of the
      table of one row leaves out (`281 1`) and AutoCAD gave back with the rows (s1), where the library keeps it out.
      Where AutoCAD's own file reads back other than it read in memory (b1, b2, b8, r1, r3, r5 to r7, d2, d4, d5, h2,
      i6, i7), the library's reads as AutoCAD's file does.
    - AutoCAD 2021 (D28e, headless, 126 edits and three read-back rounds): it keeps a kind with each row, the style of
      its row in the TABLECONTENT (1, 2, 3), and writes the title and header shown by the first rows' styles: its
      `DeleteRows` of the title wrote `93 1`, `280 1`, of the header `93 2`, `281 1`, of both `93 3` (x1, x2, s12), and of
      the row put in before a title moved down the title and header shown again, `93 0` (x12 to x14). The first row put into
      a table of one row is the header and the rest data, whatever the one row and wherever the row goes: `SetSize(3, 2)` of
      a table of its title alone wrote `93 0` and no `281`, whether `IsHeaderSuppressed` was set before or not (s1, s3 to
      s6, s14), `InsertRows(0)` of it a header before the title, the title read as data (s15), and a row after a lone row of
      data a header read as data with `170 5` (e09); a table of two rows or more gets rows of data (e01 to e22). The
      table's own looks of a kind went from its head with the last row of the kind: the header's with the header (x8,
      `93 4784258`), the data's with the last row of data (x9). A block whose top left cell went kept the content of the
      cell left, empty as its `MergeCells` had left it (x5, k1, k3, k4). Its `MergeCells` empties the other cells of the
      block, `91 0` and a value of none, their block taken out, their text style, height and colour taken away but where
      they are the table's own looks of the row's kind (q7), their alignment kept (q5, Mark's `170 5`), and gives the edges
      of the block the lines of its corners, the top and left of the top left cell, the right of the top right and the
      bottom of the bottom left, the lines within kept (q3, q4, m2 to m5). Its `ResetValue` and `SetCellType(TextCell)`
      write a value of none (`91 0`, `93 7`, `90 0`; v10, v19); it writes a number's text of six places, 12.500000, and of
      a decimal format's, 12.500 for `%lu2%pr3` (v09, v11, w30 to w33), and showed the library's 12.5 as it was. It writes
      a cell's background filled `283 0` with `63 0` where it has no colour (v18, w18 of R2000 alike), unfilled `283 1` with
      `63 0` in a row whose kind is filled (m1) and no override where it is not (v47), and reads `283 1` as unfilled: the
      library wrote and read 283 the other way about. It writes the table's own lines of a kind of row in its head after
      96 and reads them onto each cell of the kind there, but not after the last cell, where the library wrote them (v27,
      read in the head as `V-v27`). Of the other setters of the table it wrote and read as the library does: `SetBlockScale`,
      `SetBlockRotation`, `SetBlockAttributeValue`, `SetTextString`, `SetRowHeight` and `SetColumnWidth` of one and of
      all, `SetValue` of a whole number, `SetAlignment`, `SetTextHeight`, `SetTextStyle`, `SetContentColor` and
      `SetBackgroundColor` of a cell and of a content, `SetTextRotation`, `SetRotation`, `SetScale`, `SetAutoScale`,
      `SetIsAutoScale`, `SetDataFormat` of a content and `ClearTableStyleOverrides(0)` and `(1)` (v01 to v46).
    - Now (D28e): the library writes and reads so; AutoCAD read its files of 104 of the 114 edits it compared as it read
      its own files of them back, the ten left those under "Not yet".
    - Not yet (D28e): AutoCAD's own file lists the overrides of a cell's lines under other flags than the library's
      (`CellStyleOverrides` of v24, v25, v28 and v33 differ, every line read alike), and reads a line it hid back drawn
      (v26, v29); its `Fill` changed nothing or threw eInvalidInput in four edits (v35, f1 to f3), where the library fills;
      it formats a number by a format of feet and inches, `1'-0 1/2"` for `%lu4%pr2` (w32), which the library leaves 12.5;
      `SetDataType(Double)` of the text B-12 made it 0 (v12) and `SetOverrides(TextHeight)` gave Qty a height of 0.18 (v42),
      both of which the library refuses; and its `DeleteRows` of a merged row with a row above it merged that row in its
      place (x15, the title moved down by `InsertRows(0)` taken out), where the library drops the block.
    - AutoCAD 2021 (D33, headless, 177 edits, in-memory matrices of 700 formats and kinds, one read-back of 311 files): its
      `Fill` of the options `None` alone fills nothing (f01, v35); `CopyContent` copies the content alone, the cell keeping
      its look and flags, and wins over `GenerateSeries` (f03, f06, g14); `CopyFormat` copies the look, the flags and the kind
      of value, a cell of no value given one of none of that kind (`93 5`, f04, f20, g15); `GenerateSeries` alone carries the
      numbers on from the source's last through the fill range wherever it stands, below, above or past a gap (3.5, 4.5 and
      5.5 of 1.5 and 2.5, f02, g01, g03), the cell given its kind of value (0x40000) and the value's flags 4, shown of six
      places (0.300000, g06), and with `Reverse` back from the source's first, the fill range's last cell next to it (-1.5,
      -0.5, 0.5; 10 and 20 to -20, -10, 0; g02, g05, g26), a text no number copied (g07); each cell the source cell its place
      repeats from the fill range's first cell (f16); the source's columns filled whatever columns the fill range names (g10,
      g24); a fill range holding a cell of the source refused with eInvalidInput (f09, D28e f1, f2). Its format `%lu4` shows
      12.5 as 1'-0 1/2" (`%pr2`), to the nearest 1/2^N of an inch (four with no `%pr`), a half away from nought, the minus
      only where the number rounded is not nought, `%zs1` leaving nought feet out and `%zs2` nought inches, a whole number
      left as it is; feet beyond 32 bits it writes as garbage (-2147483648'--2147483648 -1/0" for 1E11) and from 5E11 in
      powers of ten. Its `SetValue` takes a cell's format away (u7). Its `SetDataType` refuses nothing: a value not of the
      kind becomes one of none (`93 7`, no unit written), a whole number is cut towards nought (12.7 12, 3E9 -2147483648), a
      text read as a number takes `%lu6` (12.0000 under LUNITS 2 and LUPREC 4) or `%lu1` (1.0000E+03), every turn but a
      text of a whole number dropping the value's flag 2 (d1 to d12, t1 to t16). Its `SetOverrides` refuses nothing: the
      flags become those named, the alignment and background kept whatever is named, one added of AutoCAD's value (the
      alignment its row shows, `63 0 64 0 283 1`, `64 0`, `140 0.18`, a text style's flag alone), the colour, text style,
      height and kind of value not named taken away (o1 to o28). Set one at a time, its overrides of a cell are named bit k
      the override 130 + k, as the library names them (s01 to s26); it writes a cell's overrides before its 92 in the order
      170, 7, 63, 64, 283, 140, then the right, bottom, left and top edge, each lineweight, showing and colour (s30 to s35).
    - Now (D33): the library writes and reads so; AutoCAD read its files of 113 of the 134 edits it compared as it read its
      own (every `Fill`, format, `SetDataType` and `SetOverrides` edit among them), the rest under "Not yet".
    - Not yet (D33): a line set on a cell AutoCAD writes with all three flags of its edge (`279 -2`, `69 1` and the flag of
      its showing for a colour alone) and on the cell below or right where the line is between two, so that its
      `CellStyleOverrides` of its own file lists two zeros more than of the library's (s08 to s33; every line read alike);
      its own file of a line it hid reads back drawn (`288 1`, s16, s18, s19, and R2000 alike, s19b), where the library's
      reads hidden as AutoCAD showed it before saving; the first row of a table read from a file it merges across the table
      where the title is shown but unmerged or a row put in before a title was left (b-n1, b-n3, n9), the library's file and
      its own alike, but writes that merge only after an edit, so that the cell (0, 1) of the library's file reads at the
      table's corner (m01, n1 to n15); its `Fill` into the header filled nothing (f14, one case), where the library fills.

44. **The arrow block of a dimension style.**
    - Try: set `Dimtxsty` and `Dimblk` on a dimension style read from a file and on one made in code, open the drawing and run
      AUDIT.
    - Now: from R2000 the text style is written by handle in group 340 and the arrows in 341 to 344, at their places in the
      record.
    - AutoCAD 2021: agrees: each style reads the text style and arrow set.

45. **The defaults of a dimension style of inches.**
    - Try: read a variable a DIMSTYLE of a drawing in inches gives no group for.
    - Now: AutoCAD's default of acad.dwt, the template in inches, whatever the drawing's units: arrowheads 0.18 long, four
      decimal places.
    - AutoCAD 2021: agrees: arrowheads 0.18, four places, in a drawing in inches and in one in millimetres alike.

46. **The angle of a jog.**
    - Try: a jogged radius dimension's `JogAngle` and a style's `Dimjogang` of 60 degrees.
    - Now: the dimension's group 40 in radians; the style's group 50 in degrees, read as radians.
    - AutoCAD 2021: agrees: the dimension's `JogAngle` and the style's `Dimjogang` both read 60 degrees.

47. **An ordinate dimension moved.**
    - Try: move and turn an ordinate dimension and read its `Measurement` in AutoCAD.
    - Now: measured along the user's axes its group 51 gives, which the move carries, so it is the same.
    - A change if: AutoCAD measures along the axes of the normal's own system.

## Exploding, copying and older versions

48. **A polyline exploded.**
    - Try: EXPLODE in AutoCAD a LWPOLYLINE and a 2D POLYLINE with widths, a thickness and a bulged segment, and compare with
      `Explode()` of the same.
    - Now: a LINE for each straight segment and an ARC for each bulged one, each with the polyline's look and thickness and on
      its normal, the widths left out: a polyline on (0, 0, -1) at elevation 5 with a thickness of 2 gives walls from z = -5
      down to -7. A POLYLINE fitted to a curve or a spline breaks along the vertices it is drawn through.
    - AutoCAD 2021: agrees, its `Explode()` giving the library's parts of all four, the spline-fitted POLYLINE the four LINEs
      from (80, 30) to (110, 40) in the library's order.

49. **An MTEXT exploded, and saved as R12.**
    - Try: EXPLODE in AutoCAD an MTEXT of three paragraphs, one wrapped at its width, held at its middle centre, and SAVEAS R12
      a drawing holding it; compare with `Explode()` and with the MTEXT appended to a drawing of R12.
    - Now: one TEXT for each paragraph, not wrapped, its formatting and the heights of its codes left out, the lines spaced as
      `LineSpaceDistance` says and held at the left, the middle or the right as the MTEXT is held; two lines 2.5 high held at
      the top left at (10, 20) stand on the baselines 17.5 and 13.33 high.
    - AutoCAD 2021: wraps the long paragraph by its font into six lines at the width 40, eight TEXTs in all, each held at its
      left (no 72, 73 or 11) where its font puts it; the baselines, from 33.33 for eight lines 4.17 apart about the middle 20,
      follow the library's rule for that many lines. Where the lines break and where each starts needs AutoCAD's font, which
      the library has not; it still gives a TEXT a paragraph. Where `DxfDatabase.FontSearchPath` finds the font, it now
      wraps and places the lines as AutoCAD does, a TEXT each held at its left (point 107).

50. **A dimension and a table exploded.**
    - Try: EXPLODE in AutoCAD a dimension in a tilted plane and a table, each drawn last by AutoCAD, and compare with
      `Explode()`.
    - AutoCAD 2021: its `Explode()` of the dimension in the tilted plane gives seven parts where the library placed them, its
      block's ten but the three POINTs on Defpoints; the table it draws anew when it opens the file, its rows 11, 9 and 9 by
      its font, and explodes that, 13 parts.
    - Now: a dimension's POINTs on Defpoints are left out; a table still gives its picture as drawn last, as drawing it anew
      needs AutoCAD's font.

51. **A MINSERT exploded.**
    - Try: `Explode()` in AutoCAD's API of a MINSERT of 2 rows by 3 columns, and EXPLODE of it.
    - Now: `Explode()` gives the block in every cell, six circles for a block of one; `ExplodeToOwnerSpace` refuses a MINSERT
      of more than one cell, as AutoCAD's EXPLODE refuses one.
    - A change if: AutoCAD's `Explode()` gives the first cell alone, or its EXPLODE takes a MINSERT.
    - AutoCAD 2021 (plugins v1 and v2, 51-minsert-2x3, a circle of radius 3 in 2 rows 8 apart by 3 columns 10 apart):
      `Explode()` threw eNotApplicable; `ExplodeToOwnerSpace()` made one CIRCLE, at (0, 0, 0) of radius 3, the first cell.
    - Now (D15): so does the library: `Explode()` of a MINSERT of more than one cell throws `NotSupportedException`, and
      `ExplodeToOwnerSpace()` appends the first cell's entities; `GeometryExtentsBestFit` still measures every cell. A MINSERT
      of one cell explodes as an INSERT.
    - Still open: whether AutoCAD's `Explode()` takes a MINSERT of one cell, and its EXPLODE command, which needs the editor.
    - AutoCAD 2021 (plugin v8, 51-b): `Explode()` refused the MINSERT of one cell at (60, 0) too (eNotApplicable);
      `TreatAsBlockRefForExplode` false for both; `GeometryExtentsBestFit()` threw eInvalidExtents for both, and
      `GeometricExtents` gave the first cell alone, (-3, -3, 0) to (3, 3, 0) and (57, -3, 0) to (63, 3, 0);
      `ExplodeToOwnerSpace()` the first cell, as the library's.
    - Now (D16): so does the library: a MINSERT is one by its subclass AcDbMInsertBlock, of one cell too (by its counts in
      R12); its `Explode()` throws `NotSupportedException`, its `TreatAsBlockRefForExplode` is false, its
      `GeometryExtentsBestFit` throws `InvalidOperationException` and its `GeometricExtents` is its first cell's. The
      extents of the drawing (`UpdateExt`) still take in every cell, which AutoCAD was not asked about.

52. **A leader exploded.**
    - Try: EXPLODE in AutoCAD a straight leader with the closed filled arrow, one with an arrow block (DIMLDRBLK) and one with a
      hook line, and compare with `Explode()`.
    - AutoCAD 2021: each leader gives the arrow first, a SOLID of the corner on the right, the tip and the corner on the left,
      or the INSERT of GH_ARROW; then one Polyline from the arrow's back, 2.5 along the first segment, or for the splined
      leader one SPLINE from there, its knots the lengths of the chords (0 to 21.64), with three fit points. A leader with
      words has one vertex more, where AutoCAD's font ends the words (x = -11.46 for CLOSED FILLED), and its last moved to the
      words where it has a hook.
    - Now: so does the library, but the vertex at the words, which needs the font, and the spline's fit points, which a
      `DxfSpline` made in code has not.
    - AutoCAD 2021 (plugin v4): every part of colour 256 (BYLAYER), where the library's file had 1: the generator of the
      check gave the library's parts colour 1 to tell them apart (`p.ColorIndex = 1`), so the library's `Explode()` agrees.
    - Now: the spline keeps its fit data, the three vertices it runs through, the first at the arrow's back, and the
      directions of the first and last segments, as AutoCAD's.

53. **A named block brought by a copy.**
    - Try: copy an INSERT of a block that inserts another block into a drawing that has neither, with WBLOCK or COPYCLIP and
      PASTECLIP, and compare its BLOCK and BLOCK_RECORD with the library's copy by `Clone()` and `AppendEntity`.
    - Now: the block comes under its name, with the blocks it inserts that the drawing has not and its description, units,
      explodability and scaling; a drawing with a block of the name places its own.
    - A change if: AutoCAD brings something else of the block, or renames it where the drawing has one of the name.

54. **A table scaled and mirrored.**
    - Try: SCALE by 2 and MIRROR in AutoCAD a table of rows 10, 8 and 8 high, with a cell of its own text height and a block
      cell, and compare with `TransformBy` of the same.
    - AutoCAD 2021 (`TransformBy`): scaled by 2, rows 22, 18 and 18 and every cell's text height doubled, those its style
      gives (6 and 4.5) too; it measures a row by its text, reading rows 10, 8 and 8 as 11, 9 and 9. Mirrored in x = 280, the
      table at (300, -100) stands at (190, -100), its normal (0, 0, 1) and its direction (1, 0, 0), readable, in the box the
      mirror makes of it.
    - Now: mirrored, as AutoCAD; scaled, the heights the table and its cells override doubled, those of its style kept, as
      the library reads no table style, and its rows as written, as it measures none by a font.
    - Now (D12): scaled, the heights the table style gives are doubled too, written as the table's own for each kind of row
      it gives none of (its 140 after its last cell, flags 0x100000 to 0x400000 of 93): the title 12 and the header and data
      9. The style is the one the table names, else the drawing's Standard, else AutoCAD's own Standard, which 43 drawings
      AutoCAD 2021 saved show: data, title and header 4.5, 6 and 4.5 of millimetres ($MEASUREMENT 1), 0.18, 0.25 and 0.18 of
      inches. The rows 22, 18 and 18 need AutoCAD's font: parked. To see: whether AutoCAD reads a height of the table's own
      for a kind of row from a file (no file of AutoCAD's here has one), and where its own save of the scaled table puts it.
    - AutoCAD 2021 (plugin v4, 54x): mirrored in y = -150 the table stands at (300, -200) along (1, 0, 0), and in the line at
      45 degrees through (280, -100) at (280, -80) along (0, 1, 0), its normal (0, 0, 1) in both, its flow up: its saves
      write 70 of 1 in the table's head after 96, the flag 0x04 set in 93.
    - Now: so does the library: mirrored so that the image of its direction still reads left to right or upwards, a table
      stands at the image of its point along that image, its flow turned over; otherwise as in x = 280.
    - AutoCAD 2021 (plugin v5): read the heights D12 wrote after the last cell as 0.18 in each cell, the default of inches,
      the cell (1, 0) its own 7. Its own scaled table keeps the heights in every cell (12 in the title's two, 9 in the
      header's and the data's, 7 in (1, 0), its block cell 9 too), and three more in its head after 96.
    - Now: each cell that has no height of its own is given its kind of row's, the table's own or its style's, scaled, as
      its own (140, flag 0x20); no height is written as the table's own.

55. **Arrow blocks of a dimension saved as R12.**
    - Try: SAVEAS R12 in AutoCAD a dimension whose DIMBLK is overridden, and compare its ACAD extended data with the library's
      copy of it into a drawing of R12.
    - Now: the override names the block by its name (a 1070 of 5, 6 or 7, then a 1000) where the drawing has a block of the name,
      and is left out, said in the log, where it has none.
    - A change if: AutoCAD writes the arrow otherwise in R12.
    - AutoCAD 2021 (plugin v4, `after-d9`): refused 55-b and 55-c, "Omitted group 5 on line 2978": the DIMSTYLE GH_DIM
      copied in the form of R12 had no 5, 6 and 7 where its source had no arrow by name; AutoCAD's own R12 writes 3, 4, 5, 6
      and 7 after the flags, empty where there is none.
    - Now: so does the library.
    - Found making the files: the copy into R12 named a DIMSTYLE the drawing did not have, the style being left behind. It is
      now copied in the form of R12: its name, flags and the variables R12 has, its arrows by name.

56. **The overall viewport of a layout.**
    - Try: in AutoCAD, try to erase the viewport 1 of a layout, and erase the last active viewport of a layout; save as DXF.
    - AutoCAD 2021: erasing the last active viewport leaves the LAYOUT's 331 naming the viewport 1; its API's `Erase()` takes
      the viewport 1 too (the ERASE command not run).
    - Now: the LAYOUT names the viewport 1 of the layout; the viewport 1 is still refused, as AutoCAD's editor keeps it.

57. **A text style across R12 and R2018.**
    - Try: copy a TEXT in a style of its own from a drawing of R2018 into one of R12 with AutoCAD, and the other way.
    - Now: the style is copied by name, as a layer is.
    - A change if: AutoCAD puts the text in Standard, or copies the style otherwise.

58. **A polyface and a polygon mesh exploded.**
    - Try: EXPLODE in AutoCAD a polyface mesh with a triangle whose third number is below nought and a face of its own colour,
      and a polygon mesh smoothed by PEDIT into a cubic surface; compare with `Explode()`.
    - Now: a 3DFACE for each face, the triangle ending on its third corner with its edges 3 and 4 hidden (70 of 12), the face
      in its own colour; the smoothed mesh breaks into the cells of its surface, a grid of its densities, not of its frame.
    - A change if: AutoCAD hides edge 3 alone, gives the face the mesh's colour, or explodes the frame of a smoothed mesh.
    - AutoCAD 2021: refused the input file, "Class separator for class AcDbFaceRecord expected": the library wrote a face
      made in code with AcDbVertex before its AcDbFaceRecord. AutoCAD writes the face's marker alone, and so does the
      library now; the exploding is to be seen again (`after-d11`).
    - AutoCAD 2021 (plugin v4): the faces of the PFACE 8E all of colour 256, the face of colour 1 of its own among them; the
      mesh 9B smoothed at 6 and 6 into 25 faces, its 7 by 7 points read as a grid of 6 by 6 from the first, the first face
      of the points 0, 1, 7 and 6.
    - Now: so does the library: a face's own colour is not taken, and a smoothed mesh breaks as AutoCAD's. A file written by
      13.0.0 or 13.1.0 with AcDbVertex before a face's AcDbFaceRecord is read and saved with the marker alone.
    - AutoCAD 2021 (plugin v5, 58-e): the faces of the PFACE on INPUT_PFACE_LAYER whose face records stand on FACE_OWN_LAYER
      on INPUT_PFACE_LAYER too, colour 256.
    - Now: so does the library: neither the layer nor the colour of a face record is taken.

59. **An MLINE exploded.**
    - Try: EXPLODE in AutoCAD an MLINE of a style of six elements with inner arcs at both ends, one with its caps suppressed,
      and one cut by MLEDIT (Cut Single); compare with `Explode()`.
    - Now: inner arcs join every inner pair, the second and the fifth, the third and the fourth, where ezdxf joins the first
      pair alone; the suppressed caps are left out; the cut element gives a LINE for each piece, its parameters after the first
      read as a start and a stop by turns from the point the element stands at; the fill is left out.
    - AutoCAD 2021: agrees, by its API's `Explode()`: 8 arcs and 16 lines, 12 lines, and 8 arcs and 10 lines.

60. **A HATCH exploded.**
    - Try: EXPLODE in AutoCAD a HATCH of DOTS, one of ANSI31 round a circle with an island in it in the outer style, a solid
      fill and a gradient; compare with `Explode()`.
    - AutoCAD 2021: the DOTS give 228 LINEs of no length, none on the bottom edge its row of dots runs along (the library
      gave 247); the ANSI31 in the outer style 17 LINEs, one inside the island of radius 3, as it cuts by the loops flagged
      external or outermost, as ezdxf does, and both circles were flagged outermost; the solid fill and the gradient it
      refuses (eNotApplicable).
    - Now: so does the library, line for line.

61. **A multileader exploded.**
    - Try: EXPLODE in AutoCAD a multileader of words with two leaders on the left, one splined, one of a block with an
      attribute, and one whose words are underlined (attachment 5 or 6); compare with `Explode()`.
    - AutoCAD 2021: the words or the block first, then for each leader its dogleg, a LINE from its last point, then for each
      line a Polyline from the arrow's back to the last point of the leader, or a SPLINE whose knots are the lengths of the
      chords, and its arrow, a SOLID of the corner on the right, the tip and the corner on the left. BYBLOCK is made the
      multileader's own colour, BYLAYER here, the lines keep the lineweight BYBLOCK and the arrows and words are BYLAYER. The
      underlined words get a Polyline under them as long as the words are wide, and the INSERT of the block no ATTRIB.
    - Now: so does the library, but the underline, which needs AutoCAD's font, and the INSERT, which keeps its ATTRIB, as
      the API's `Explode()` may leave the attributes to the EXPLODE command.
    - AutoCAD 2021 (plugin v4): the splined line's SPLINE has three fit points, the arrow's back, the vertex and the last
      point, and its tangents at the ends; the library's had none.
    - Now: the SPLINE keeps them, its control points and knots as they were.
    - AutoCAD 2021 (D31, 137 multileaders it made, exploded by its API against the library's `Explode()` of its own files):
      the same to 1E-6 but for five things, all now as AutoCAD gives them: a multileader whose leader lines are splined has
      no dogleg drawn, and its SPLINEs run to the end the dogleg would have (c15, d19); a dogleg of no length is a LINE of no
      length (c08); the words and the block are in the multileader's own linetype and lineweight (d17, h2), the SOLID of an
      arrow in its own linetype (all), an INSERT of an arrow block in its own linetype and the line's lineweight, turned by
      an angle from nought to a whole turn (d18, g1); and the words are as wide as the record's 43, which the scale has
      scaled already (h1c), where the library scaled them again. What remains apart is the line AutoCAD draws under, over
      or round the words (attachments 5, 6 and 8, the vertical lined centre, the frame), as long as its font makes them.

62. **A leader exploded in the look of its dimension style.**
    - Try: EXPLODE in AutoCAD a leader of colour 3 whose style has DIMCLRD BYBLOCK, one with DIMCLRD 1 and DIMLWD 0.35 as
      overrides, and one of DIMCLRD BYBLOCK whose record writes a 77; compare with `Explode()`.
    - AutoCAD 2021: each leader gives a SOLID and one Polyline in the leader's own colour, true colour and lineweight: the
      style's DIMCLRD BYBLOCK, the overrides DIMCLRD 1 and DIMLWD 0.35, and the 77 change nothing.
    - Now: so does the library: the parts have the leader's own look.

63. **A splined leader as a curve.**
    - Try: in AutoCAD's API, `GetPointAtParameter`, `EndParam`, `GetDistanceAtParameter(EndParam)` and `GeometricExtents` of a
      splined leader through (0, 0, 0), (10, 10, 0) and (20, 10, 0); compare with the library's.
    - AutoCAD 2021: the same curve, its parameter run over the lengths of the chords, 0 to 24.14, its point at 12.07 the
      library's at 0.5, 24.3737 long (the library 24.37377, within two millionths); its box (0, 0, 0) to (20, 10, 0), the box of
      the vertices, though its own spline rises to 10.42 between them.
    - Now: so does the library: the parameter from 0 to the sum of the chords, and the box of the vertices. `Explode()` gives
      the spline from the arrow's back, its knots the chords too, as AutoCAD's (point 52).

64. **A MESH exploded.**
    - Try: EXPLODE in AutoCAD a MESH of triangles and quads, one with a face of five corners (a mesh made by MESHSMOOTH of a
      solid's flat face), and one with a face given a colour of its own; compare with `Explode()`.
    - AutoCAD 2021: the cube as the library's; the face of the five corners 0 to 4 as three triangles fanned from its first
      corner, the last first and each the other way round, (4, 3, 0), (2, 0, 3) and (0, 2, 1), every edge drawn; the face
      given red (an override of the MESH: 91 1, 92 1, 90 0, 63 1) a red 3DFACE.
    - Now: so does the library. A face of six corners or more is fanned by the same rule, which one face of five settles
      only in part.
    - AutoCAD 2021 (plugin v4, 64x, three MESHes made through its API): the base of six corners of a pyramid as (5, 4, 0),
      (3, 0, 4), (0, 3, 1), (2, 1, 3); one face of six alone as (0, 1, 5), (2, 5, 1), (5, 2, 4), (3, 4, 2); one of five alone
      as (0, 1, 4), (2, 4, 1), (4, 2, 3). Each is a strip: two corners, then the rest by turns from the far end and the near
      one, each triangle after the first the other way round; alone from the first two corners, as a base from the last two
      back the other way.
    - Now: so does the library, the strip from the first two corners in a MESH of one face and from the last two back in a
      MESH of more. Whether a MESH of more faces that is not closed reads its faces as the pyramid's is still to be tried.
    - AutoCAD 2021 (plugin v5, 64x-a): the open MESH A3 of a face of six corners and one of five sharing an edge, both
      facing up, breaks each from its first two corners, as a face alone. The pyramid AutoCAD made in v4 was handed every
      face facing in and saved with every face facing out, its base (5, 4, 3, 2, 1, 0); the pyramids A0 and 8B, whose base
      faces in and sides out, break their bases from the last two corners back.
    - Now: a face of more than four corners breaks from its first two corners, or from its last two back where it faces
      into a closed MESH, the faces of each closed piece turned to agree and to face out of the room they close: one rule for
      A0, A1, A2 and A3, 8B, and the pyramid AutoCAD made. A face of four corners or three is broken as before, as the file
      holds it.
    - AutoCAD 2021 (plugin v6, 64x-a): A0 to A4 as the library's. The square pyramid A5, its base (0, 1, 2, 3) and its side
      (0, 3, 4) facing in, its three other sides out, gave the quad (3, 2, 1, 0) and the triangle (4, 3, 0), and the other
      sides as held; the pyramid on five corners A6, every face facing in, its base from its last two corners back and each
      side (k + 1, k, 5) as (5, k, k + 1). AutoCAD turns every face that faces into a closed MESH, a quad and a triangle too,
      each read from its last corner back; it already holds them turned once it has read the file.
    - Now: so does the library: every face that faces into a closed MESH is read from its last corner back, a quad or a
      triangle as one of more corners; the tetrahedron of ezdxf's drawing, two of whose faces face in, gives those two turned.
      A0 to A4, 8B and the pyramid AutoCAD made in v4 are as before.

## Geographic location, layer filters and layer states

65. **The GEODATA AutoCAD writes.**
    - Try: in AutoCAD 2018 or later, GEOGRAPHICLOCATION on a drawing in millimetres, with three points of the mesh, saved as
      DXF of R2018; read the GEODATA record.
    - AutoCAD 2021: writes version 3, the groups otherwise in the library's order, the mesh interleaved.
    - Now: a GEODATA made in code is of version 3; one of version 2 or 3 is read alike.

66. **The class GEODATA.**
    - Try: in the same file, read the CLASS whose DXF name is GEODATA.
    - AutoCAD 2021: ObjectDBX Classes, 4095, a count of 1 for its one GEODATA, 0 and 0; it reads the library's count of 0
      with no word in its log.
    - Now: the class is listed with a count of 1, the GEODATA it is listed for.

67. **The entry ACAD_GEOGRAPHICDATA and the host of the GEODATA.**
    - Try: in the same file, read model space's extension dictionary and the GEODATA's group 330 after its version.
    - Now: the library names the GEODATA by a 360, an owning pointer, and writes model space's BLOCK_RECORD handle as its
      host; ezdxf writes a 350 and a host of 0, and the library reads both.
    - A change if: AutoCAD writes a 350, or a host other than model space's handle; the library should then write it so.

68. **A new GeoLocationData, and one in paper space.**
    - Try: in AutoCAD's API, read every property of `new GeoLocationData()`; then set its `BlockTableRecordId` to paper
      space's, `PostToDb()`, save and open the file.
    - AutoCAD 2021: its new one is of no known type (`CoordinateTypeUnknown`), in metres both ways, its scale judged at the
      reference point (`ScaleEstMethodReferencePoint`), the rest as ezdxf's; and it refuses paper space as the block
      (`eInvalidInput` from `BlockTableRecordId`). The library's GEODATA in paper space it opens, but `GeoDataObject` finds
      none.
    - Now: a `DxfGeoData` made in code is AutoCAD's new one, and `BlockTableRecord` and `PostToDb` refuse any block but model
      space.

69. **Where AutoCAD 2018 keeps layer filters.**
    - Try: in AutoCAD 2018 or later, make a property filter and a group filter, save as DXF of R2018, and read the layer
      table's extension dictionary; in AutoCAD's API, list `db.LayerFilters.Root.NestedFilters`.
    - AutoCAD 2021: keeps the property filter in ACAD_LAYERFILTERS and in ACLYDICTIONARY (an XRECORD `*A1`: 1
      AcLyLayerFilter, 90 1, 300 its name, 301 its expression), the group in ACLYDICTIONARY alone (`*A2`: 1 AcLyLayerGroup, 90
      1, 300 its name, a 330 for each layer); `NestedFilters` gives the two, then All Used Layers, Unreconciled New Layers and
      Viewport Overrides, which it makes.
    - Now: so does the library: the filters of ACLYDICTIONARY by their 300, then those of ACAD_LAYERFILTERS not among them,
      then the three AutoCAD makes.

70. **The record of a layer state, and its hidden and xref states.**
    - Try: save a layer state, one marked hidden, and one of an attached xref; save as DXF of R2018 and read the XRECORDs of
      ACAD_LAYERSTATES; in AutoCAD's API compare `GetLayerStateNames(false, false)` with `GetLayerStateNames(true, true)`.
    - AutoCAD 2021: the lists differ. A state saved hidden has the mask 0x8001 (group 91 of 32769); the state of an attached
      xref, named `70-b-xref-source__GH_XR_STATE`, names its layers by name (8) where the others name them by handle (330),
      and is not kept once the drawing is saved and opened again. A record is 91 the mask, 301 the description, 290, 302, then
      for each layer 330 (or 8), 90, 62, 370, 331, 1 and 440.
    - Now: `GetLayerStateNames(includeHidden, includeXref)` takes the flags so, `GetLayerStateNames()` gives every state; the
      layers, mask and description are still not read.

71. **A GEODATA with no class.**
    - Try: open in AutoCAD a DXF of R2010 or later with no CLASSES section, written by a program that leaves it out, given a
      GEODATA by `PostToDb()`.
    - Now: the GEODATA is written and no class for it, since there is no CLASSES section to list it in.
    - A change if: AutoCAD refuses the file, drops the GEODATA, or AUDIT reports it; `PostToDb` should then write a CLASSES
      section, or refuse such a file.

72. **The angle of the north direction.**
    - Try: in AutoCAD's API, read `NorthDirection` of a GeoLocationData whose `NorthDirectionVector` is (0, 1), (1, 0) and
      (-1, 0) by turns.
    - AutoCAD 2021: 0, 90 and -90 degrees: from the Y axis, positive towards X.
    - Now: `NorthDirection` gives that angle, in radians, from `NorthDirectionVector` (groups 12 and 22).

## Data links of tables

The library reads the data links of a table, takes them away and puts them in, in the table's TABLECONTENT, where AutoCAD
keeps them (points 73 to 78); a table of no TABLECONTENT is given one from R2007 on (D28).

73. **Where a table's cell names its data link.**
    - Try: link the cells (1, 0) to (2, 1) of a table to a spreadsheet with DATALINK, save as DXF of R2007 and of R2018, and
      read the cells of the ACAD_TABLE.
    - Now: the range's top left cell is read as naming its link after its 92, made odd: `92 1`, `340` the DATALINK's handle,
      `93 2`, `94 2`, `96 0`, before `301 CELL_VALUE`; a block cell names none, its 340 being its block. That run is the one
      LibreDWG gives a cell of a TABLECONTENT, whose 92 says it has linked data; on a cell of an ACAD_TABLE it is a guess, and
      what the other bits of that 92 mean is not known.
    - A change if: AutoCAD names the link elsewhere (in a TABLECONTENT, in the table's own part, in other groups) or gives the
      92 other bits; the reading then follows AutoCAD, and the writing is made on it.
    - AutoCAD 2021 (its own links of point 77, made through the API over (1, 0) to (2, 1), which it read back as (1, 0) alone):
      the ACAD_TABLE's cells name no link, `92 0` in every one. The link is named in the TABLECONTENT (149) the table's
      extension dictionary holds, under ACAD_XREC_ROUNDTRIP, by an XRECORD whose 102 is ACAD_ROUNDTRIP_2008_TABLE_ENTITY: its
      cell (1, 0) holds `92 1`, `340 142`, `93 1`, `94 1`, `96 1` after its custom data. AutoCAD reads no link from the
      library's cells. So the guess is wrong, and the writing held back, which writes the guess, is not taken: writing a link
      AutoCAD reads means writing the table's TABLECONTENT, which AutoCAD builds anew on a detach (every cell's format written
      out, new handles), and keeping it with the table's edits.
    - Now (D16): a link is read from the TABLECONTENT, while it has the table's rows and columns: the cell whose 92 after
      its custom data is odd names the link (340) over its rows (93) and columns (94), AutoCAD's link of 73 read as (1, 0)
      alone, as AutoCAD reads it. Each cell of a TABLECONTENT carries a checksum, ACAD_ROUNDTRIP_2008_CELL_CHECKSUM, the sum
      of each letter's code times its place from 1 of the cell's text (1 655 for Parts, 676 for Qty, 1 041 for Mark, 503 for
      B-12, nought for an empty or block cell) or of its formula's field code (5 577 for `\AcExpr (1+2)`). The guessed run
      in the ACAD_TABLE's cells is still read, but AutoCAD misreads the cells after it: 74-a saved again had its cells a cell
      along (Qty in (0, 0), the bolt in (1, 0)). Rows or columns put in or taken out leave the TABLECONTENT as it was, and
      its link is then no longer read.
    - AutoCAD 2021 (plugin v9): 73-d, the title set Bolts in the ACAD_TABLE and Parts left in the TABLECONTENT of the
      checksum 1 655: shows Bolts and keeps the link of (1, 0); 73-e, a fourth row put in by the library, its TABLECONTENT
      and TABLEGEOMETRY of three left: opened, four rows, the link kept at (1, 0), saved with a TABLECONTENT of four rows.
    - Now (D17): a cell of the TABLECONTENT is the table's of its row and column whatever rows it has, as AutoCAD read 73-e;
      one past the table's last row or column names none. Writing the TABLECONTENT anew as rows and columns change, as
      AutoCAD's save of 73-e does (every cell of the new row, its format and its geometry), is not done: parked.
      `SetDataLink(row, column, link)` and `SetDataLink(range, link)` put a link in as AutoCAD linked 73: the range's top
      left cell of the TABLECONTENT given the flag 4 in its state, its 92 made odd and `340` link, `93` rows, `94` columns,
      `96 1` after it, and the DATALINK tied to the table and its TABLECONTENT (`94 2`, `330 F6`, `330 187`), the cells
      left as they are, as AutoCAD takes the ACAD_TABLE's where the checksum does not agree (73-d); AutoCAD's 73, its link
      taken away and set again, is AutoCAD's file to the byte. A table of no TABLECONTENT is refused: AutoCAD writes it whole
      when it links one (its 73 of an ezdxf table, 469 groups for three rows of two columns, and a TABLEGEOMETRY of every
      cell's place and extents). To check: 73-f to 73-j of after-d17.
    - AutoCAD 2021 (plugin v10): 73-f, a row put in above AutoCAD's linked cell, keeps the link at (1, 0), by the
      TABLECONTENT's cell; 73-g, 73-i and 73-j, a cell linked by the library in R2018 and R2007, named or anonymous, read
      linked, the link's targets the table, no line in the log; 73-h, the library's range (1, 0) to (2, 1): `IsLinked` True at
      (1, 0) alone and False at (1, 1), (2, 0) and (2, 1), though each gives `GetDataLinkRange` (1, 0)-(2, 1) and the link
      142. Saved again, every cell but the top left keeps the state 0 and `92 0` the library wrote, the ACAD_TABLE's cells as
      written, and the top left `96 0` where the library wrote `96 1`, as in its saves of 73-g, 73-i and 73-j.
    - Now (D18): `IsLinked` of a range of the TABLECONTENT is true at its top left cell alone, as AutoCAD read 73-h;
      `GetDataLinkRange` and `GetDataLink` give the range and the link at every cell of it. A range named in the cells of the
      ACAD_TABLE, which AutoCAD reads no link from, is linked at every cell, as before. Still to see (v11): whether AutoCAD's
      `IsLinked` follows the top left cell or the flag 4 of a cell's state, which the top left alone has in every file so
      far (73-k, the flag given to (2, 1) as well; 73-l, taken from (1, 0)).
    - AutoCAD 2021 (plugin v11): both. 73-k is linked at (1, 0) and at (2, 1), whose `State` reads Linked, and not at (1, 1)
      or (2, 0); 73-l, the top left of the state 0, is still linked at (1, 0) alone, its `State` None. Every cell of the range
      gives it and the link 142 in both. Saved again (v11-73k-resaved, -73l-resaved), each cell's state is kept as written.
    - Now (D19): `IsLinked` of a range of the TABLECONTENT is true at its top left cell, whatever its state, and at any other
      cell of it whose state has the flag 4, as AutoCAD read 73-k and 73-l.
    - AutoCAD 2021 (plugin v12): 73-m, the flag 4 given to (1, 1) inside the range and to (0, 1) outside it, is linked at
      (1, 0) and (1, 1), not at (0, 1), which gives no range and a `State` of none, as the library reads it. 73-n, 73-k given
      `RemoveDataLink()` by the library, which cleared the top left's flag alone: (2, 1), left with `90 4`, reads linked with
      no range, its `State` Linked, and its save kept `90 4` there; the table has no link and the DATALINK no target.
    - Now (D20): `RemoveDataLink()` and `RemoveDataLink(row, column)` clear the flag 4 from the state of every cell of the
      range they let go of, the one byte of 73-n that differs; a cell of the flag 4 outside every range is left as it is, and
      reads not linked, as 73-m. Still to see (v13): 73-o, 73-p and 73-q of after-d20 read with no cell linked; and 73-r, the
      flag 4 on (2, 1), a cell with a content in a row of data outside the range (1, 0) to (1, 1), and on the title (0, 0),
      which the library reads as not linked, where AutoCAD read 73-n's (2, 1) of no range as linked and 73-m's (0, 1),
      of no content, as not.
    - AutoCAD 2021 (plugin v13): 73-o, 73-p and 73-q read with no cell linked, no range, the table of no link and the
      DATALINK of no target, as the library reads them; its save of 73-p wrote the state of (0, 1), the flag 4 of no content
      outside every range, as `90 12` where the library wrote `90 4` (recorded; `Table.IsLinked(0, 1)` True there,
      `Cells[0, 1].IsLinked` False). 73-r: (0, 0), the title of Parts, and (2, 1), a cell of data of a content of no text,
      both of the state 4 outside the range, read linked (`Cells` and `Table` alike), with no range and no link, their
      `State` Linked; (1, 0) linked and (1, 1) not, as the library reads them.
    - Now (D21): `IsLinked` of a cell in no range of the TABLECONTENT is true where its state has the flag 4 and it holds a
      content (its 95 above nought), as AutoCAD read 73-r's (0, 0) and (2, 1) and 73-n's (2, 1), and false where it holds
      none, as 73-m's (0, 1); a cell of a range reads as before. Ripple over every DXF of the checks folder: 73-r's (0, 0)
      and (2, 1), and (2, 1) of 73-n and of AutoCAD's save of it, read linked; every other cell as before, no byte moved.
      The library writes the state as it is (no 12). Still to see (v14): `RemoveDataLink()` on 73-r, whether AutoCAD clears
      the flag 4 of (0, 0) and (2, 1), cells of no range it reads linked, which the library leaves as they are.
    - AutoCAD 2021 (D28, headless, the library's edits of its 50 and 73 read, and its own edits saved): a cell's text, value,
      format, the heights and widths and the merged cells are read from the ACAD_TABLE whatever the TABLECONTENT holds (Ntz
      of Qty's checksum 676 read as Ntz; a merge taken out of either alone read as the ACAD_TABLE has it); the links, the
      style of each row (90 of TABLEROW: 1 title, 2 header, 3 data) and a cell's borders from the TABLECONTENT, cell by cell:
      a row put in by the library above AutoCAD's link, the TABLECONTENT left as it was, read linked at the new row (c07), a
      row taken out left the next row the header's style (e21). AutoCAD's own row and column edits write the TABLECONTENT in
      step (b01 to b15: a new row of data of empty cells, a link and a border moving with their cells, a link taken out with
      its row and the TABLECONTENT out of the DATALINK's targets, `94 1`, `330 F6`). The TABLECONTENT is read only for a
      table ACDB_RECOMPOSE_DATA lists (`90 1`, then a 330 for each table and table style, by handle); its 73 read its link
      without its TABLEGEOMETRY or any cell's content (d01 to d04), and none without that record (d05). A value of the flag 1
      (93 of 1, 5 or 7) reads as none whatever its text (v, e19, c03, c04).
    - Now (D28): rows and columns put in and taken out (`InsertRows`, `InsertRowsAndInherit`, `DeleteRows`, the columns'
      alike, `SetSize`) are put in and taken out of the TABLECONTENT as AutoCAD writes them, its links and merged ranges with
      them; `MergeCells` and `UnmergeCells` list the merges there too. `SetDataLink` of a table of no TABLECONTENT gives it
      one as AutoCAD gave its 73 one, with the XRECORD, ACDB_RECOMPOSE_DATA and the class, no TABLEGEOMETRY and no content:
      AutoCAD read the link of (1, 0) so made in R2018 and R2007 (p1, p3, p4), and the library's edits of rows and columns as
      it reads its own (n1 to n11). A range of more cells (p2) it read linked at every cell of no content, where the library
      reads its top left alone: a cell of a range whose TABLECONTENT cell holds no content, or a content of a text, reads
      linked, one of an empty value (93 7) not (s1 to s4); the library's reading is left as it was, to be settled. A text or
      value given to a cell of the flag 1 takes the flags 6 (q1 to q3 read M16, Plain and 5).
    - AutoCAD 2021 (D28b, headless, hand-made files read and its own edits saved): a cell of a range other than its top left,
      of the state 0, read linked where its TABLECONTENT cell held no content (p2, s3, L8: no such cell at all), or a
      content whose checksum disagreed with the cell's text in the ACAD_TABLE (L10, L11, L14), and not linked where it held
      a content of an agreeing checksum, or of none (L1 to L7, L9, L12: an empty value of 93 1, 5 or 7, a text, an empty
      text, a number): AutoCAD lays a cell out from the ACAD_TABLE, and links it, where it cannot take the TABLECONTENT's.
      A cell of the state 4 outside every range read linked with a content whatever its checksum (L13). The record
      ACDB_RECOMPOSE_DATA listing the table style alone, or nothing, AutoCAD read no link of its 73; listing the table
      alone, its link (R1 to R3).
    - Now (D28b): `IsLinked` follows that; the D18 to D21 tests whose fixtures held no content in the range's other cells
      read them linked. A table ACDB_RECOMPOSE_DATA does not list has no link of its TABLECONTENT, and `SetDataLink` lists it
      (v11 read linked at both its links).

74. **The groups of a DATALINK.**
    - Try: read the DATALINK of the drawing of point 73; then open in AutoCAD, with AUDIT, a drawing whose DATALINK has no 70
      and a count of nought, `94 0`, followed by `305 CUSTOMDATA`, `1 DATAMAP_BEGIN` and `309 DATAMAP_END`.
    - Now: read as LibreDWG gives AcDbDataLink: 1, 300, 301, 302, 90, 91, 92, 170 to 176, 177, 93, 304, then the count 94
      of custom data, its pairs of a 330 and a 304 between the markers, and the 360; LibreDWG gives no 70.
    - A change if: AutoCAD writes a 70 or another group, the pairs after the 94 are not a 330 of an object the link owns and
      a 304, or AUDIT fixes a link of `94 0`.
    - AutoCAD 2021: no 70; 1, 300, 301, 302, 90, 91, 92, 170 to 176, 177, 93 and 304 as LibreDWG gives them; then the 94
      counts the objects the link is tied to, each a 330 (`94 2`, `330 F6` the table, `330 149` its TABLECONTENT), then the
      360 (`360 0` where it owns nothing), then `305 CUSTOMDATA`, `1 DATAMAP_BEGIN`, `90 0`, `309 DATAMAP_END`. The library
      reads the groups it gives as before, and takes a table out of that list when its link is taken away (point 77).

75. **The class of a DATALINK.**
    - Try: read the CLASS of DATALINK in the CLASSES section of the drawing of point 73.
    - Now: links are read whatever the CLASSES section lists, and no class is added, as no link is written; the writing held
      back listed DATALINK, AcDbDataLink, ObjectDBX Classes, of the flags 1153.
    - A change if: AutoCAD's flags or names differ; the writing takes AutoCAD's.
    - AutoCAD 2021: `DATALINK`, `AcDbDataLink`, `ObjectDBX Classes`, flags 1152 (not 1153), 91 1, 280 0, 281 0; and
      `TABLECONTENT`, `AcDbTableContent`, of the same flags.
    - Now: unchanged, as the library writes no link and no class of one; the writing held back is to take 1152 and the
      count of the links.

76. **What a linked table keeps of its link.**
    - Try: in the drawing of point 73, look in the ACAD_TABLE for a reactor or a pointer to the DATALINK other than the
      cell's 340, and in the DATALINK for the table's handle.
    - Now: the cell's 340 alone is read and taken away; the DATALINK is counted as let go of when no record of the file points
      at it, its dictionary aside.
    - A change if: AutoCAD keeps a reactor or a pointer either way; taking a link away then takes it out too.
    - AutoCAD 2021: the ACAD_TABLE holds no pointer to the link; its TABLECONTENT names it (340, point 73), and the DATALINK
      lists the table and the TABLECONTENT among the objects it is tied to (`330 F6`, `330 149` after its 94).

77. **A link detached.**
    - Try: detach the link of a table in AutoCAD, once for a link named in the Data Link Manager and once for one whose
      `DataLinkOption` is Anonymous, and save.
    - AutoCAD 2021: its `RemoveDataLink()` keeps the DATALINK in ACAD_DATALINK, named or anonymous, after it is saved and
      opened again; the link's count of the objects it is tied to (94) goes from 2 to nought. It reads no link from the
      library's cells of point 73 (to be settled there).
    - Now: the cell's groups of the link taken out and its 92 made even; the DATALINK kept, named or anonymous, and the table
      and what it owns, through its extension dictionary at any depth, taken out of the objects the link lists itself tied to:
      a link of the table F6 alone, `94 2`, `330 F6`, `330 149`, comes out `94 0`, its 360 and custom data as they were. A
      link AutoCAD made is still not read (point 73), so it is not taken away either.
    - Now (D16): a link of AutoCAD's is taken away in its TABLECONTENT: the cell's 92 made even, its run (340, 93, 94, 96)
      taken out and the flag 4 of a linked cell cleared from its state (90), in place, every other group as it was;
      AutoCAD's own detach writes the TABLECONTENT anew, every cell's format written out, under new handles. The DATALINK
      comes out `94 0` as before.

78. **A formula in a linked cell.**
    - Try: give the top left cell of a linked range a formula in AutoCAD, save, and read the cell's groups.
    - Now: `SetFormula` writes the cell's 344 after the link's run: `92 1`, `340`, `93`, `94`, `96 0`, then `344`.
    - A change if: AutoCAD writes the 344 before the 340, or keeps no formula in a linked cell.
    - AutoCAD 2021 (plugin v3, 78-autocad-formula.dxf): keeps the formula in a linked cell it was let unlock: its ACAD_TABLE
      cell (1, 0) holds `145 0.0`, `344 192`, `170 5`, `92 0`, the field before the cell's overrides and its 92, no link;
      its TABLECONTENT's cell keeps the link (`92 1`, `340 142`, `93 1`, `94 1`, now `96 0`), its state 76, its content a
      field of its own (195), and its checksum that of the field code. The library's 78 file, of the guessed run, it read
      as an empty cell of no link.
    - Now (D16): `SetFormula` writes the 344 after the cell's 145 and before its 92, as AutoCAD does, in every cell; the
      TABLECONTENT of a table AutoCAD saved is left as it was, its cell's checksum no longer that of the cell. `GetFormula`
      of a cell whose 344 names no record, as AutoCAD's 192, reads the field of the cell's first content in the TABLECONTENT:
      `=1+2` from AutoCAD's 78, where it read none.
    - AutoCAD 2021 (plugin v9): 78-b, AutoCAD's 78 with its link taken away by the library: no formula, no value, even after
      `EvaluateFields()`, and saved with the TABLECONTENT's cell pointing at the 192 of no record and no FIELD at all; 78-c,
      the formula set by the library in the ACAD_TABLE's cell of AutoCAD's linked 73, its TABLECONTENT's cell of checksum 0:
      the formula `(1+2)`, 3 after `EvaluateFields()`, and the link, both.
    - Now (D17): `RemoveDataLink` writes the formula of a cell whose 344 names no record, read from the TABLECONTENT, in a
      FIELD of the cell's own, owned by the table, as `SetFormula` writes one (78c's form); the TABLECONTENT's field is kept.
      To check: 78-d of after-d17.

## Offsets of polylines that cross themselves

79. **The corner at a crossing of a closed polyline offset.**
    - Try: `GetOffsetCurves(1)` in AutoCAD's API of the closed polyline (-10, 1), (10, -1), (10, 1), (-10, -1), a bow tie
      crossing itself at the origin, of the same bow tie 0.02 high at its ends, and of the square (0, 0), (6, 0.06),
      (4, 0.06), (10, 0), (10, 10), (0, 10), whose bottom curls into a loop; note the vertices beside each crossing.
    - Before D12: the crossing is no corner. The two lobes' offsets are joined where they cross, the bow tie's at (0, ±1.004988),
      and the thin one's at (0, ±1); its tips are not run on, which with the unlimited miter `GetOffsetCurves` uses
      would reach x = ±1 000. The square grows to the square from (-1, -1.01005) to (11, 11), its bottom rising from
      -1.01005 at the corners to (5, -0.95005). `Offset2.Offset` of a `GeoPolygonArc2` grown with miters or chamfers miters or cuts each tip of two
      lobes as a corner, as the straight path does.
    - AutoCAD 2021: `GetOffsetCurves` gives no curve of either bow tie, either way; of the curled square, outwards (+1) one
      closed polyline of 11 vertices that runs out to x = 206 and -196 at the level of the crossing, inwards none.
    - Now (the maintainer's decision): a closed polyline that crosses itself has no offset, either way, said in one Debug
      line: both bow ties and the curled square, whose first and third sides cross at (5, 0.05), give none. A corner that
      only touches another side, both its own sides on one side of it, is no crossing, and a polyline that only touches
      itself, or an open one that crosses itself, is offset as before. `Offset2.Offset` of the library's own loops is not
      changed.

## Multileader landings

80. **Where a multileader's leader lands, read and made.**
    - Try (D31, headless, plugin GHD31): multileaders made by AutoCAD's API on a Standard of words 4 high, arrows 4, a gap of
      2 and doglegs of 8, words "ABC" or three lines held at (100, 50): leaders from (0, 0), (250, 0), (50, 100), (103, 0)
      and (108, 0); every attachment of the sides; no dogleg, no landing, a gap of 5 and a dogleg of 15; words moved, the
      gap, dogleg, height, attachment and scale set after the leader; leaders splined; blocks met at their extents and base;
      words met at their top and bottom; `SetLastVertex`, `AddLastVertex`, `TransformBy` and `Scale`; each dumped through the
      API (`GetVertex`, `GetDogleg`, `GetDoglegLength`, `TextLocation`, `Explode`) and saved by DXFOUT, and read and exploded
      by the library. Then the library's own multileaders made the same way, read by DXFIN.
    - AutoCAD 2021: from a point on the left of the words, the dogleg along x ends the gap short of their left edge (90 to 98
      at y 48 for one line met at the middle of its top line), as far below their top as the attachment says: 0, h/2 and h
      for the top of the top line, its middle and bottom, 1.2h under it (attachments 6 and 8), and for the whole words of
      height H (lines 5/3 h apart) H/2, H - h/2, H and H + 0.2h (attachments 2 to 5); with a gap of 5 and a dogleg of 15
      from 80. On the right, as far right as its font makes the words wide (from 120.67 for words 10.67 wide); the side is
      chosen by the middle of the words (a point at x 103 under words from 100 to 110.67 lands left, one at 108 right). A
      block met at its extents or base is met at the side of its extents, with no gap. Words met at their top or bottom have
      no dogleg (`GetDoglegLength` 0). Words moved move the doglegs with them (to 110, 78 for words moved to 120, 80); a gap,
      dogleg, height, attachment or scale set after the leader, and `SetLastVertex`, move the words and keep the landing.
      Without a dogleg (`EnableDogleg` false, or the leader lines splined, which makes it false), `GetLastVertex` is the end
      the dogleg would have, and `SetLastVertex` puts the dogleg's start a dogleg short of the point. The multileader's own
      dogleg (41) and arrows (42) are unscaled: `Scale` 2 keeps 41 at 8 and draws the dogleg 16 (40 of the leader), and
      `GetDoglegLength` gives 8; `DoglegLength` 3 writes 41 of 3 and 40 of 6; `SetDoglegLength(0, 1)` 40 of 2. `TransformBy`
      by 2 leaves the scale (40 of the context, 45), 41, 42 and a line's own arrow size, and scales the rest, the words' width
      (43) among them. AutoCAD reads the library's files as their records say, landings and all, and keeps them so.
    - Now: the library reads and explodes AutoCAD's multileaders so, to 1E-6, but for the lines under, over and round the
      words; `AddLeaderLine(point)` lands a new leader on the left of words held at their top left as AutoCAD does;
      `TextLocation` moves the doglegs; the dogleg lengths and `TransformBy` are written as AutoCAD writes them. Of 30
      multileaders the library made as AutoCAD's plugin made them, AutoCAD read every one as the library reads it.
    - Still apart: a leader on the right of the words, or under them between their left edge and middle, which needs the
      font; words turned, held otherwise, or met at their top or bottom. A block's landing, the words AutoCAD moves after a
      property set or `SetLastVertex`, and the underline of attachment 8 (`\L`) are now as AutoCAD has them: see point 82.
      A leader on the right of words `FontSearchPath` measures is landed as AutoCAD lands it: see point 102.

## Inserting a drawing

81. **An INSERT of a block its own drawing has not, inserted with the drawing.**
    - Try (D31, plugin GHD31 rounds C, E and F): DXFIN of a drawing of R12 holding an INSERT of a block Z it has not, in model
      space, in a named block M and in an anonymous one; then, made in AutoCAD's API by erasing Z's record under its INSERT,
      `Insert(transform)`, `Insert(name)` and `WblockCloneObjects` of each, into a drawing with and without a Z of its own.
    - AutoCAD 2021: DXFIN refuses each file (`eInvalidBlockName`, "Block "Z" was not defined"). `Insert`, both ways, copies
      such an INSERT wherever it stands, in model space, in M and in the anonymous block, as an INSERT of no block, even where
      the drawing has a Z; DXFOUT then refuses the drawing (`eNullObjectId`). `WblockCloneObjects` refuses a set holding or
      reaching one (`eWasErased`).
    - Before: `Insert` refused one in model space or in a block it merged ("The drawing has no block Z for the INSERT to
      place"), and copied one in a block a copy brings.
    - Now: `Insert` copies each as it is, naming Z, and says so in one Debug line; where the drawing has a Z the copy places
      it, as a DXF names its block by name. `WblockCloneObjects` refuses one given among the entities, as before.

## Multileader landings kept

82. **A block's landing, the content moved to keep its landings, the underline of attachment 8, the style's look, the
    arrows set again and the scale of words in columns.**
    - Try (D31b, headless, plugin GHD31B rounds A and B, on the Standard of point 80): blocks of four shapes (a box, a circle
      with a line across it, a slanting line, two small circles off the middle) placed at (100, 50), with their origin at
      (5, 5), scaled by 2 and turned by 30 degrees, met at their extents or base from either side and from points under
      them; the gap, the dogleg (all and one), the height, the scale, an attachment and the words set after a leader on the
      left, on the right and on both sides, the last vertex set, the words given anew; attachment 8 set before and after the
      words and leader, back again, on words underlined already and of no letters; a style of its own linetype and arrow
      block made current or set; `ArrowSize` set to the size it has, after `Scale` and after `TransformBy`; `Scale` on
      words 60 by 30 and in static and dynamic columns. Then round I: 60 of the library's multileaders, made and edited by
      the library the same way (the edits on AutoCAD's own files of point 80), read by DXFIN.
    - AutoCAD 2021: a block is met with no gap where its shapes meet the line along x through the middle of its own box,
      carried as it is drawn (or through its position, at its base), the outermost point on the side of the box's middle the
      point is on, else at that point of the line: the box from 90 to 120 at y 55, the circle with a line across it at 90
      and 110 though the line reaches 120, the slanting line at 110 from both sides, the two small circles at the middle of
      the box, 110 (at the position, 100, at its base). The gap moves the words by its change, with or without leaders, and
      a dogleg on their right by twice it; the dogleg moves the content away from leaders on one side and lands leaders on
      both sides anew; the last vertex set carries the content and the other landings; the height and the scale move words
      met on the left, and words met on the right by their font's width; an attachment moves the words or the right
      leaders by the change below the top; words given anew stay landed, the place given not taken. Attachment 8 writes
      `\L` before the words where a leader meets its side, once, and takes it off when set away; 5 and 6 write nothing. A
      new multileader, or one set to a style, takes the style's linetype (341) and arrow block (342). `ArrowSize` set to the
      size it has sets bit 9 and writes the arrows drawn its size times the scale (4 after `TransformBy` by 2 draws them 4
      again, 5 at a scale of 2 draws them 10). `Scale` scales the height of the words (44) always and their width (43) only
      where they have no columns; columns, gutter and heights (142 to 144) are kept, but dynamic columns of manual heights,
      which AutoCAD flows anew (65 wide becoming 100, a third column added). `BlockConnectionType` is read from the context
      data (177), where the library had not written it.
    - Now: all of it but the dynamic columns of manual heights and what needs the font, as AutoCAD has it; of the 60
      multileaders of round I, AutoCAD read 59 as it made its own, the 60th having no case of its own to match (a height of
      10 on words met on the left: (100, 53), as AutoCAD put words given anew 10 high).
    - Still apart: the words AutoCAD moves by their font's width (met on the right while their height or scale changes,
      given anew wider) and the moves of `SetFromStyle()`, not read. A block scaled by `Scale`, `TextAttachmentType` and the
      sizes read unscaled are now as AutoCAD has them: see point 83.

## Multileader sizes and overrides

83. **The sizes read unscaled, the bit of a value set again, the text style, a block scaled, a style of no arrow and one
    leader of several given its dogleg.**
    - Try (D31c, headless, plugin GHD31C rounds A and B, 111 multileaders on the Standard of point 80): `TextAttachmentType`
      set on words met on the left, the right and both sides; the gap, height, arrows and doglegs set at a scale of 2, after
      `TransformBy` by 2, with no leader and on a line of its own; `BlockScale` set at a scale of 2; the words given anew at
      a scale of 2; a style whose words are of a text style TS2, made current, set before and after the words, and a text
      style of the multileader's own; 31 properties and 4 looks of a line set to the value they have, read by the bits of
      the overrides (90) and the line's flags (93); `Scale` 2 and 3, twice and back, on the box, the circle with a line
      across it and the box of origin (5, 5), met from the left, the right, both sides, none, at its base, turned, moved;
      `BlockScale` and `BlockRotation` set after a leader; a style of an arrow block set to one of none; two leaders on the
      left of their own given their doglegs. Then round I: 30 of the library's multileaders made and edited the same way
      (the edits on AutoCAD's own files), read by DXFIN.
    - AutoCAD 2021: `TextAttachmentType` sets the left alone (173 and 174), as `SetTextAttachmentType` of the left. The API
      reads and sets the sizes unscaled: `TextHeight` and `LandingGap` are the context's 41 and 145 over the scale (40), and
      written times it (a gap of 3 at a scale of 2 writes 145 of 6, a height of 5 41 of 10); `ArrowSize` is 140 over the scale
      (8 after `TransformBy` by 2, 42 still 4), a line's `GetArrowSize` 42; `DoglegLength` the first leader's 40 over the
      scale (16 after `TransformBy`, 15 after `SetDoglegLength(0, 15)`), the multileader's own 41 where it has no leader;
      `BlockScale` the multileader's own 10 (1 for a block drawn 2 at a scale of 2), set written 10 and times the scale 16.
      A property set to the value it has sets its bit all the same: the leader lines' type, colour, linetype and lineweight
      (0 to 3), the landing, gap, dogleg and its length (4 to 7), the arrow block (8), the text style (11), the attachments
      (12, 25, 28, 29), the turn and alignment (13, 14), the height and frame (16, 17), the block's colour, scale, turn and
      connection (20 to 23), the scale (24) and the leaders run on to the words (30); the direction of the attachment, the
      annotation scale, the places, the style and the words given as they were set none; `SetDoglegLength` sets 7, and a
      line's look set to the one it has flags it (93 of 1, 2, 8; 16 with its 40). A new multileader writes the text style
      of its words in 343 and in their 340 (Standard's 55); one set to a style takes the style's text style in both unless
      it has its own (bit 11), a block's multileader of a style of TS2 TS2. A style of no arrow block takes the arrow block
      away (no 342), unless the multileader has its own (bit 8). `Scale` scales the block as drawn (16) and its matrix (47)
      is made its transform where it stood; met by one leader the block moves so that the leader's longer dogleg, its
      start kept, meets it at its end (the box at (100, 50) met from the left to (118, 45) at 2 and (136, 40) at 3, from the
      right to (72, 45)), the base point of the content (10) where a leader from the left meets it; met on both sides or
      by none it stays, its leaders landed anew. Of two leaders on a side, one given its dogleg starts that much further
      away and the words stay, and `DoglegLength` does so for both. `BlockScale` and `BlockRotation` set after a leader move
      the block so that a leader from the left would meet it at the base point of the content, and land the leaders anew.
    - Now: all of it but what is still apart below; of the 30 multileaders of round I, AutoCAD read 29 as it made its own.
    - Still apart: `BlockScale` and `BlockRotation` set after a leader, which the library writes without moving the block;
      `BlockPosition` set, which AutoCAD carries the doglegs' starts with (as `TextLocation` does) and the library does not;
      `TransformBy` of a block, which AutoCAD writes leaving the multileader's own scale of the block (10) and making the
      matrix the block's transform, where the library writes 10 as drawn and multiplies the matrix; words given with a text
      style of their own (AutoCAD takes it, overriding the style's); a leader of several not landed, which AutoCAD lands
      anew when one of them is given its dogleg; and whether the colour of the words, the content and the block set to what
      they are set their bits, which every multileader AutoCAD makes has set already.

## Dynamic blocks: expressions, attributes and a block of a network alone

84. **The functions of a block's constraints, round on a half, and the remainder.**
    - Try (D34, headless, plugin GHD34EXPR, rounds r1 and r2, 150 expressions): a variable of a network of model space given
      each expression by `AssocVariable.SetExpression`, worked out by `EvaluateExpression`, the value and its kind (90 or 40)
      read: round of halves either side of nought; trunc, floor and ceil; min and max of one, two and three values, a
      semicolon or a comma between, of equal values of either kind; exp, ln, log, log10, exp10, pow, sinh to atanh, d2r, r2d,
      sign; pi and e, beside variables named pi and e; the remainder %; values out of each function's range; random.
    - AutoCAD 2021: round goes to the whole number at or below the value and a half, round(-2.5) -2 and round(2.5) 3,
      round(0.49999999999999994) 1; trunc, floor, ceil, round, sign and % are whole, the others real but abs, of its value's
      kind; min and max take two values, by ; or by , (one or three refused), and give the one they pick of its kind, the
      first of two equal (max(2;2.0) the whole 2, max(2.0;2) the real 2); pi and e, of any case, come before a variable of
      their name; % is of the level of * and /, of whole values only (7.0%2 1, 7.5%2 refused), of the sign of the first
      (-7%3 -1, 7%-3 1); log10 is no function; 1/0, ln(-1), sqrt(-1), acos(2), atanh(1), 7%0 and round(3000000000.5) are
      refused, trunc and floor past 32 bits give -2147483648, and 2147483647+1 is the real 2147483648; random gives a new
      value each time. Its ln and log come up to 2E-13 of their size apart from the C library's for arguments past 1 000
      (ln(12345) 9.4210064017810993 against 9.42100640177928).
    - Now: all of it, values within 1E-12 of their size of AutoCAD's, but random, refused, as are trunc, floor and ceil
      past 32 bits, which AutoCAD gives as -2147483648.

85. **Attribute definitions in a flip, an array and a polar stretch.**
    - Try (D34, plugin GHD34A, rounds r1 and r2): blocks of the library's writer with two locked ATTDEFs, left at (x, y + 10)
      and centred on (x, y - 10), in a flip, a linear array, an XY array and a polar stretch (one in the frame, one turned
      only; and unlocked), INSERTs plain, turned a quarter at the scale 2 and mirrored, set as the library sets them.
    - AutoCAD 2021: an array holding an ATTDEF is not set (eInvalidInput, linear and XY alike). A flip mirrors the box of
      each ATTDEF's words and keeps them readable, the left one at (63.0, 60) for one at (20, 60) about x 45, as its font's
      extents give. A polar stretch turns and moves a locked ATTDEF it holds and turns one it turns only, as it does a text,
      each INSERT's ATTRIB following; it leaves one not locked and its ATTRIB where they are.
    - Now: the polar stretch as AutoCAD, within 3E-13 on 42 dumps; the writer takes a locked ATTDEF into a polar stretch.
      Still refused: an ATTDEF in a flip, whose place the library would need the font to work out, and in an array, which
      AutoCAD does not set. An ATTDEF in a flip is taken where `FontSearchPath` measures its words: see point 102.

86. **A justified TEXT of no second point, and a block of a network and no constraint parameter set again.**
    - Try (D34): a block of TEXTs of nine justifications whose groups 11 were taken out, read by DXFIN. Then the library's
      INSERTs of AutoCAD's Parking sample (R2010 and R2018), set to a Width, a Length, a row or a number of Spaces, set again
      by AutoCAD to a Width or a Length beside its own sets of the same two values (plugin GHD34RUN, round p1).
    - AutoCAD 2021: each TEXT's alignment point reads (0, 0, 0), its position worked out about it from its font. Each of the
      10 INSERTs it set again as it set its own, 0 apart, AUDIT finding the 2 errors of the sample and no more.
    - Now: the library reads the alignment point so; its position stays as the file has it, AutoCAD's needing the font.
      Parking is copied with its network as D27b copies it, and nothing had to change.

## Multileader blocks placed anew

87. **A block scaled, turned, moved and transformed, words of a text style, and leaders snapped back to their landing.**
    - Try (D31d, headless, plugin GHD31D rounds C and D, 56 multileaders on the Standard of point 80): `BlockScale` and
      `BlockRotation` set on the box, the circle with a line across it and the box of origin (5, 5), met from the left, the
      right, both sides and none, at its extents and at its base, after `Scale` 2, after `BlockPosition`, before the leaders;
      `BlockPosition` set with leaders on either side, both and none; `TransformBy` a scaling, a quarter turn, a move and a
      mirror, then `BlockScale`; words of a text style TS2 given to a multileader of Standard's words and of S4's (TS2), given
      again as read; two leaders on the left, the second moved off its landing, then a dogleg length given to either, the same
      length, `DoglegLength`, `LandingGap`, `TextHeight` and `Scale`, on words and on a block; two leaders on a side landed,
      then `Scale`. Then round I: 64 of the library's multileaders made and edited the same way (the leaders of several on
      AutoCAD's own files), read by DXFIN; and a block turned as drawn (46) alone, read by `BlockRotation`.
    - AutoCAD 2021: `BlockScale` and `BlockRotation` move the block so that a leader from the left meets it at the base point
      of the content (10), whatever leaders it has, and land each leader on a side anew: the box at (100, 50), its base point
      (90, 55), to (110, 45) scaled by 2, to (105.49, 48.17) turned by 30 degrees; met on both sides at a scale of 2, when the
      base point stayed (90, 55) and the box (100, 50), to (150, 25) for a `BlockScale` of 3. The matrix (47) is written as the
      block's transform there. `BlockPosition` moves the base point and every leader's dogleg by as much as the block, the
      matrix left. A block given and placed has its base point where a leader from the left meets it. `TransformBy` leaves the
      multileader's own 10 and 43 and writes the matrix as the block's transform; `BlockScale` and `BlockRotation` read 10 and
      43 (a block drawn turned 15 degrees, 43 of nought, reads nought). A mirror turns the block's normal over (14 of
      (0, 0, -1)), its scales kept (16 of 1) and its turn read in the new plane. Words given with a text style make it the
      multileader's (343 and the words' 340) and set bit 11 where it differs, words given as read setting nothing; a style
      set later keeps it. Every leader on a side is landed anew at the content when it is laid out again: a dogleg length
      given to any leader, the same or another, `DoglegLength`, `LandingGap`, `TextHeight`, `Scale`, `BlockScale`; with
      several leaders on the left of words, `Scale` moves the words by the gap (and up by the height), not by the doglegs, the
      base point kept; a block met by several leaders on a side stays under `Scale`, as D31c guessed.
    - Now: all of it; AutoCAD read the 64 multileaders of round I as it made its own, their extents too. The library lands
      a leader anew on the left of words held at their top left and on either side of a block; one on the right of words is
      left as it is, AutoCAD landing it by the font's width.
    - Still apart: `AddFirstVertex` on a leader line of no vertex, which AutoCAD takes as the leader's landing and the
      library puts before it (the library's own multileaders of two leaders made as GHD31D made AutoCAD's do not match it);
      `SetLastVertex` on a leader on the right of a block, which AutoCAD lands at the block's right side and the library moves
      by as much as the landing; words given to a multileader in no drawing, which take its text style when it is added.

## Tables: rows merged on load, formats, fills and overrides cleared

88. **The rows AutoCAD merges when it loads a table, a format on a value that is no number, a fill's look onto a value of
    another kind, the flag of a cell's kind of value, and a table cleared of its cells' overrides.**
    - Try (D33b, headless, plugin GHD33B rounds W, W2, E, E2, E3 and two read-backs of 206 files): 35 tables made in memory
      and saved, each read back from its file: the title unmerged with the title, the header or both left out, over three
      columns, partly merged, merged down with the header, with X, a space, nought, a block, a colour, a background or an
      alignment beside Parts, a row put in before the title, a row of the title's kind put in further down; then rows and
      columns taken out of each (m01 to m27), and a row put in, a value set and a size set (m30 to m32). Fills into and from
      the header and the title, above the source and past it, of content, format and series (f01 to f33, p1 to p11);
      `SetFormat` on a whole number, a text, a value of none, a number and a block cell (s01 to s19);
      `ClearTableStyleOverrides` of 0, 1 and 2 on AutoCAD's table of its own row looks, on tables of other own looks and
      lines, and on the fill table of empty cells (c01 to c18).
    - AutoCAD 2021: on loading a table it merges across it the first row, whatever its kind and whether the title is shown,
      and any other row of the title's kind (TABLEROW 90 1), where no block reaches from the row into another and each cell
      after the block of its first cell holds no value or an empty text (its colour, background and alignment whatever they
      are); a text, a space, nought or a block leaves the row alone, and a data row of empty cells is never merged. It
      writes that merge only after an edit, every edit, so that the hidden cells of a file it merged itself are placed at
      the table's corner. `Fill` fills the header and the title as it fills data; `CopyFormat` onto a value of another kind
      turns it as `SetDataType` does (Mark under 1.5 a number of none, the text 12 the number 12 shown 12.0000 under
      `%lu6`, 7 the number 7.000000, 1.5 under 10 the whole number 1, X1 under 20 a whole number of none, 2.5
      under X1 the text 2.500000). Above its source, a fill longer than the source repeats it from the fill range's last
      cell back, the source's last next to it (1.5 and 2.5 into rows 1 to 3 give 2.5, 1.5, 2.5), but sometimes leaves the
      far cell (rows 1 to 4 under three, columns 0 to 2 left of two). `SetFormat` keeps a format on a number alone; a
      whole number, a text or another kind is written with none (7 given `%lu2%pr2` reads 7), a value of none becomes
      `93 5` `90 512` even for an empty format, a block cell loses its flag 0x40000; each value given a format loses its
      flag 2 (`93 6` made 4). A cell of the flag 0x40000 read from a file gives `DataType | DataFormat`. Of 0 and 2,
      `ClearTableStyleOverrides` makes each cell of no value read from the file `93 5` `90 512` and takes the flag 0x40000
      off each block cell; of 2 it gives each cell the table's own looks of its row's kind as its own (the header 63 3,
      283 0, 140 9.0, the data 170 1, 7 GH_TXT, 64 1, 140 9.0); of 1 it leaves the cells alone.
    - Now: `DeleteRows` and `DeleteColumns` merge those rows first and write them merged; `Fill`'s `CopyFormat` turns a
      value of another kind; `SetFormat`, `GetOverrides` and `ClearTableStyleOverrides` as above. AutoCAD read the
      library's files of 85 of the 102 edits it compared as it read its own.
    - Still apart: a row merged on load is written only by `DeleteRows` and `DeleteColumns`, not by the other edits (m30 to
      m32, f12 to f14), so that a fill into a cell AutoCAD merged on load fills it in the library's file (f12); a fill above
      its source longer than the source (f07, p2, p6, p9 to p11), whose rule three rounds did not settle; a cell of the
      title's kind further down merged on load keeps its `140 6.0` in AutoCAD's file and loses it in the library's (m18,
      m23, read alike but for `GetOverrides`); a cell `ResetValue` emptied in the same session keeps `93 7` when AutoCAD
      clears it, and loses its content looks (c16 to c18), where the library, which reads files, writes `93 5` and keeps
      them; the per-cell lines AutoCAD writes for a table's own lines of a kind of row (v27, v28, c12), read alike.

## Splines cut evenly, fitted by other parameters, and read as their fit

89. **Closed and open splines cut evenly, the periodic cubic through three points, fits of other parameters, fit data kept
    through edits, and the curve AutoCAD reads a record of fit points as.**
    - Try (D32b, AutoCAD's own plugin headless in accoreconsole, side databases): `ToPolylineWithPrecision` at 0 to 12, 15
      and 20 of sixteen closed cubics of 3 to 12 fit points (70 1065) and of the same cubics of control points alone, and of
      the same as periodic cubics; of five closed cubics of a smooth seam (70 1067) and of eight open curves of few pieces
      (straight, gentle, a quadratic, a quartic and a quintic over 0 to 50); every vertex's parameter and AutoCAD's length
      of seven open curves; fits within a fit tolerance of 0.2 to 5 through nineteen point sets; fits of the parameters
      `Uniform` and `SqrtChord`, open, closed and periodic, with and without tangents, saved and read back; split pieces,
      `ModifyPositionAndTangent` and `InsertKnot` on open, closed, periodic and uniform fits; and the library's own files of
      those read back (GHD32BA, GHD32BB).
    - AutoCAD 2021: a spline not periodic of n control points is cut into (n - 3) + (n - 2) k pieces even in its
      parameter, k half the precision rounded down, where that is more pieces than the halving: where and as the rule says
      in all 448 polylines of the closed cubics, of fit points or not, and all 80 of the open curves (a straight cubic of 4
      points 1 segment at 0 and 1, 3 at 2 and 3; the quintic over 0 to 50 in thirds at 0). A closed one is then left with
      no last vertex on its first, and of a spline of fit points only as many of its knots as its vertices less two are put
      in place: the closed cubic through (0, 0), (40, 0), (40.5, 0.5), (50, 30) and (10, 25) at 0 is the vertices at 0,
      40.71, 55.57, 71.70 and 111.15, the knot 112.01 left out. Its periodic cubic through (0, 0), (30, 0) and (20, 15) is
      of 3 control points to a period, read periodic. Its lengths (`GetDistanceAtParameter`) are longer than the curve by
      2E-4 for the rational cubics, 3E-5 for the S and the quintics; with them the rule's tolerance max(1 / M, L / 10 000)
      gives all 8 counts the true length misses. Fits of `Uniform` and `SqrtChord` are its cubic of knots one step apart or
      the square roots of the chords, to 3E-14, saved with no fit points, tangents or fit tolerance, 70 1160 and 1096 (3211
      and 3147 periodic); read back, their fit points are the points at their knots and their tangents the derivatives at
      their ends (44.29, -9.29 and 7.01, -1.77 at the start through five points), their parameters 128 one step, 64 the
      square root, 256 its own. `ModifyPositionAndTangent` saves a fit spline 70 1288 (closed 1289), the pieces of
      `GetSplitCurves` keep the points at their knots as fit points, of its own parameters, and a knot put into a uniform
      fit saves 1288. The degree, control points and knots it gives a record of fit points it reads as their fit are those
      of the fit (of degree 3 and 7 control points for five fit points alone of 71 2). Within a fit tolerance t it moves
      each inner fit point by t at most (the ends kept) along a direction that depends on neither t, nor a move, turn or
      scale of the points, nor their order, and fits the cubic through the points so moved, its knots their chords; a
      periodic fit takes no tolerance.
    - Now: the even cut, the closed spline's last vertex and knots; the periodic cubic through three points; fits of
      `Uniform` and `SqrtChord`, their flags and their reading back; the fit data through `ModifyPositionAndTangent`,
      `GetSplitCurves` and `InsertKnot`; the getters of a record read as its fit. 65 of D32's 72 polylines of its rings and
      148 of its 156 counts are AutoCAD's; AutoCAD read the library's 10 files of fits, edits and pieces as the library
      reads them, 44 of their 48 polylines at 0 to 3 to the vertex.
    - Still apart: a piece the halving splits where AutoCAD keeps it whole, its middle 0.2 to 5 % over the tolerance, on
      closed and periodic curves (AutoCAD's tolerance on each ring fits one factor 1.00 to 1.06 of 1 / M across its
      precisions, which no reading of the bend tried gives); the lengths of rational curves, which AutoCAD measures by a rule
      not found (no Gauss or Simpson rule of a fixed count gives them); the direction a fit tolerance moves a fit point (not
      the curve's normal, its bend's gradient or a least-squares fit of fewer points); `ModifyPositionAndTangent` of a
      periodic spline, which AutoCAD works on its periodic form (70 3339) and the library on its clamped one.
## Tables: every edit made on the rows merged on load, fills above their source, `ResetValue`'s looks

90. **The rows AutoCAD merges when it loads a table, written at every edit; a fill into a hidden cell and above its source;
    the looks `ResetValue` takes; the hidden cell of a title moved down.**
    - Try (D33c, headless, plugin GHD33C rounds E, F, F2, F3 and a read-back of 102 files): 20 edits of a table read with
      its title unmerged (a move, a colour, none at all, a value into the hidden cell, a fill into it, a merge, a height, a
      line, a column put in), a merge in the same session of a title and of a header moved down (k01 to k05), `ResetValue`
      of a cell of every look of its own, of Mark, of the title and of a block cell, and the table cleared after it and after
      other edits (r01 to r14); 589 fills in memory of 1 to 4 sources by 1 to 7 cells, down and up a column, right and left
      along a row, of a gap of 0 or 1, the source 0 to 3 cells from the table's end, and fills whose last length reaches the
      table's first row or column.
    - AutoCAD 2021: it writes a table merged as it loaded it whether the table was edited or not, even a file read and saved
      untouched (e05); a title unmerged in the session stays unmerged (e06). `SetValue` and `SetTextString` write into a
      cell a merge hides; `Fill` leaves it (f12, e09, e15, e16). Below or right of its source a fill repeats it from the
      fill range's first cell, to the table's end; above or left of it, one no longer than the source does too, and one
      longer repeats it back from the fill range's last cell, the source's last next to it, in whole lengths of the source,
      leaving as they are the cells of a length that would reach before the table's first row or column (four sources from
      column 7 into columns 0 to 6 filled 3 to 6); a series is not cut so. `ResetValue` takes the content's text style, text
      height and colour away with the value, but those alike the table's own looks of the row's kind, and makes its turn
      nought (`145 0.0`), keeping alignment, background and lines; a block cell it makes an empty text cell (r04).
      `ClearTableStyleOverrides` of 0 or 2 keeps `93 7`, `90 0` in a cell emptied in the same session (by `ResetValue`,
      `InsertRows`, `SetSize` or `MergeCells`) where it writes `93 5`, `90 512` for one read so; of 1 it takes from a cell
      reset in the session the looks it kept alike the table's. A row of the title's kind moved down, merged on load or in
      the session, keeps `170 5` and `140 6.0` in its hidden cell, `140 6.0` again where the cell held 3.0 (k02).
    - Now: the first edit of a table merges those rows (`MergeAsLoaded`), at `DeleteRows` and every other edit; `Fill`,
      `ResetValue`, `ClearTableStyleOverrides` and `MergeCells` as above. Every one of the 589 fills reads as AutoCAD's, and
      AutoCAD read the library's files of 46 of 51 edits as it read its own.
    - Still apart: a cell emptied in the session by anything but `ResetValue`, or reset before rows or columns went in or
      out, is written `93 5`, `90 512` by `ClearTableStyleOverrides` of 0 or 2, read as an empty text where AutoCAD reads no
      value (r08, r10, r12); `SetTextString` of an empty text writes an empty text where AutoCAD writes a value of none
      (r09); `ResetValue` of a block cell is refused where AutoCAD empties it (r04); the per-cell lines of a cell's own line
      (e13, settled); and edits the table's base entity makes (a colour, a layer) do not merge, though AutoCAD writes the
      merge after them too (e02).

## Multileader leaders of no landing, the block's scale and turn set as they are, mirrors

91. **A leader added with no landing, a dogleg turned, the last vertex of a leader on the right of a block, the block's scale
    and turn set to what they are, the block moved, a mirror across the XY and YZ planes, and words given before the
    multileader is added.**
    - Try (D31e, headless, plugin GHD31E rounds E and G, 60 + 17 multileaders on the Standard of point 80): `AddLeader`
      alone, with an empty line, then `AddFirstVertex` once, twice, on a second line, with no dogleg, with `SetDogleg` along
      or against x before, with `SetDoglegLength` before, `AddLastVertex` in its place, then `SetFirstVertex` or
      `SetLastVertex`, on words and on the box; `SetDogleg` turning a landed leader; a leader of no landing under
      `DoglegLength`, `BlockScale`, `TransformBy`, `SetLastVertex` of another and `AddLeaderLine(point)`; `SetLastVertex` of
      a leader on the right, left and both sides of the box and of a turned box; `BlockScale` 1 and `BlockRotation` 0 after
      `BlockPosition`, `Scale`, `TransformBy` of 2 and of a quarter turn, two leaders and none; `BlockPosition` after
      `BlockScale` and `Scale`; `TransformBy` of a mirror across the XY plane (`Matrix3d.Mirroring` of a plane) on the box,
      turned and not, met on either side, then `BlockScale`, `BlockRotation` and `BlockPosition`, on words, then
      `DoglegLength`; the same across the YZ plane; words of TS2 or Standard given to a multileader made in memory, with and
      without `SetDatabaseDefaults`, on Standard and on S4 (TS2) current. Then round I: the library's 60 files of round E
      read by DXFIN.
    - AutoCAD 2021: `AddLeader` writes a leader of 290 0 that draws no dogleg; a line added to it has no vertex
      (`VerticesCount` 0), and the first vertex given it, by `AddFirstVertex` or `AddLastVertex`, becomes the leader's
      landing (290 1, its 10 the point, (-28, 80) for (-20, 80) with no dogleg), the content left; a vertex given after, or
      to another line of the leader, goes before it. `SetDoglegLength` does not land it, `SetDogleg` does, on the side of
      the content the dogleg runs from (from (90, 48) on the left of "ABC" at (100, 50), from (128, 55) on the right of the
      box), and lands a landed leader whose dogleg it turns (k-left-setdogleg-neg); a landed leader of no line draws its
      dogleg. `SetLastVertex` moves the content and the other landings by as much as the landing moves, on either side of a
      block too: the box's leader added and landed on its right by `SetDogleg`, its last vertex set to (200, 70), moves the
      box from (100, 50) to (172, 65). A leader of no landing counts among the leaders of its side under `DoglegLength` and
      is not landed by it; alone, the words stay; `AddLeaderLine(point)` never adds a line to it. `BlockScale` and
      `BlockRotation` set to the value they have do all that another value does: the matrix written as the block's
      transform, the block placed to be met at the base point, the leaders landed anew, the block drawn at 1 again after a
      `TransformBy` of 2. `BlockRotation` writes 46 as the turn plus the angle of the plane's x axis in the block's own plane
      (120 for 30 on a quarter turn, 210 for 30 after the XY mirror). `BlockPosition` and the moves of a landing leave the
      matrix. A mirror across the XY plane turns the block's normal over and its x scale negative (16 of -1, 1, 1), its turn
      read from its y axis (330 for 30), the plane's y axis (0, -1, 0) and 297 0; across the YZ plane the normal stays,
      the x scale is -1, the plane's x axis stays (1, 0, 0) and the base point is put at the new left, (-120, 55); a
      `BlockScale` after either drops the sign. Words mirrored across the XY plane are written of normal (0, 0, -1),
      direction (-1, 0, 0), at their right top; across the YZ plane of attachment 3 and the bit of the alignment (14).
      Words of a text style given to a multileader of `SetDatabaseDefaults` made in memory keep it when it is added, with bit
      11 where it is not the style's; without `SetDatabaseDefaults` they take the style's and are laid out from (110, 52).
    - Now: the leader of no landing, `SetDogleg`'s landing, the dogleg of a landed leader of no line, the second leader on
      the right of a block, the scale and turn set as they are, 46 from the plane, the matrix left by moves, the block under
      both mirrors, and the words' text style kept. 54 of the 60 cases of round E read alike (22 at base), every block
      number and matrix of them alike but the bit 24 of the overrides AutoCAD sets on `TransformBy` of a scaling; AutoCAD
      read the library's 60 files as it read its own but those 6.
    - Still apart: a leader landed on the right of words, which AutoCAD lands by their font's width (a1-negdogleg-*); words
      mirrored, which AutoCAD keeps readable by their width and attachment and reads in the library's files 4 higher
      (e5-words-*, g-words-mirroryz); words that name no text style given before the multileader is added, which take the
      style's where AutoCAD's words carry the drawing's current text style (f6-def-s4-words-standard); bit 24 of the
      overrides set by `TransformBy` of a scaling (c3-k-transform2-blockscale1); `RemoveFirstVertex` down to one vertex,
      which AutoCAD allows (addfirst-removefirst) and the library refuses.

## Annotative texts and attributes read and moved at their contexts

92. **An annotative attribute, text and definition read at its context of 1:1 or its default, whatever CANNOSCALE says;
    set, filled from its definition and moved, its INSERT moved, turned and scaled.**
    - Try (D35, headless, plugin GHD35A rounds a, b, c and c2): AutoCAD's sample Mechanical - Metric read as it is, and with
      its attribute ATT_DVSUP given a context of 1:2 at (-100, 265.33) turned 30 degrees beside its context of 1:1, either
      the default, first or second in the dictionary, of 1:2 alone, of 1:2 and 1:4, with CANNOSCALE 1:1, 1:2 and 1:4 in the
      file and set after reading; its context of 1:1 turned, its record raised to 7, a mode and a second point in the
      context alone, the INSERT's own context of 1:1 moved, turned and scaled; the INSERT moved by (10, 5), turned 30 degrees
      and scaled by 2 about its point, its `Position` set, the attribute moved alone, given a `Position`, an
      `AlignmentPoint`, a height and a turn, filled by `SetAttributeFromBlock`; a drawing AutoCAD made with an annotative
      TEXT, a centred TEXT, an MTEXT, an ATTDEF and an annotative block's INSERT, given contexts of 1:1 and 1:2, either the
      default, read, moved, turned, set and given a height; the weld symbol's `Position X` and `Offset`.
    - AutoCAD 2021: it reads an annotative object at its context of the scale 1:1, else at its default context, never at
      CANNOSCALE's, and CANNOSCALE set after reading changes nothing. The point (10, 20), second point (11, 21) and angle (50)
      are the context's, the elevation the record's, the horizontal mode the context's 70; the height is the record's times
      the default's scale over 1:1's (3 at the default 1:2 is 1.5). Saved, the record holds the default context's data:
      ATT_DVSUP was written at (-6.151, 265.33), its context of 1:1, where its record had said (0.678, 265.33). A property
      set writes the context shown, and the record where that is the default; a height writes the record at the default's
      scale (5 at 1:1 is 10 at 1:2). `TransformBy` of the INSERT or of the attribute carries every context: of 1:2 to (-90,
      270.33) for the move, to (-77.519, 210.322) turned 60 degrees for the turn, to (-213.883, 250.881) for the scale; the
      second point of a text aligned left stays (0, 0). `SetAttributeFromBlock` writes the context shown alone, the record
      too where it is the default, else the record keeps the default's point and takes the height at its scale. The
      INSERT's `Position` set moves none of its attributes. A text not aligned left has its point worked out again from its
      font after any change, in every context. An MTEXT is read at its context alike (its location 11, its direction 10, its
      height scaled), and an INSERT at its own context of 1:1 (B5: (113.88, 279.78), turned 45 degrees, scale 2). The weld
      symbol's two properties moved none of its attributes.
    - Now: TEXT, ATTRIB and ATTDEF as above (`Position`, `AlignmentPoint`, `Height`, `Rotation`, their setters,
      `TransformBy`, the INSERT's moves, `SetAttributeFromBlock`). Of 1 336 lines AutoCAD gave, 597 read apart at base and 65
      now; the 12 lines of INSERTs 5 and 6 of the sample that D34 left apart read alike.
    - Still apart: the point of a text not aligned left, which AutoCAD works out from the font; the horizontal mode of a
      context that differs from its record's (B4, not in any drawing AutoCAD saved); an MTEXT and an INSERT, still read
      from their records (G1, G2, B5); `HasContext` of 1:1 of an INSERT of an annotative block that has no context record,
      true in AutoCAD; `Annotative` of an attribute with contexts whose data AcadAnnotative says 0 or that has none, true
      in AutoCAD; the INSERT's `Position` set, which the library documents as carrying its attributes; an attribute of
      several lines made from or into an MTEXT, whose contexts are not written; and a dynamic property that moves an
      annotative attribute, carried by `TransformBy` but not checked, the sample's weld symbol being refused by the library.

## Tables: cells emptied in the session, an empty text, a block cell reset, the table's own look

93. **Which cells a clear of the table's overrides keeps of no value, as rows and columns move them; an empty text set; a
    block cell reset; a change of the table's colour.**
    - Try (D33d, headless, plugin GHD33D rounds E, E2 and a read-back of 70 files): a clear of 0 after the merge AutoCAD
      makes on load, after columns put in at the title's edge and among its columns, a row put in taking another's look,
      `SetSize`, a merge of a cell of no value, a merge undone, rows put in and taken out, a `ResetValue` before columns or
      rows went in or out, and a fill from a cell reset (a01 to a17); `SetTextString` of an empty text into a text, a number,
      the title, the header, the title's hidden empty cell and a cell of every look, then cleared (s01 to s10); `ResetValue`
      of the block cell M12, then cleared, set or read with the table's own looks (b01 to b05); a reset or an empty text of
      a cell read of no value, and an empty text and a block reset cleared of 1 (c01 to c04).
    - AutoCAD 2021: `ClearTableStyleOverrides` of 0 or 2 keeps `93 7`, `90 0` in every cell emptied in the session, wherever
      it moved since: one reset or given an empty text, even of no value before (c01, c02), one put in by `InsertRows`,
      `InsertColumns` or `SetSize`, each cell of the title merged anew by columns put in at its edge, and each cell a merge
      emptied, the merge on load too (a01) and one unmerged since (a11); a cell read of no value it writes `93 5`, `90 512`.
      Of 1, it takes from a cell reset the looks it kept as the table's, even after a row put in before the first row of
      data took those looks from the table (a07). A row put in taking another's look is `93 5`, `90 4` before and after
      (a04). `SetTextString` of an empty text is `ResetValue`: `91 0`, `93 7`, `90 0`, the looks of the content gone as
      there (s02, s07, s09), whatever the cell held; `SetValue` of an empty text writes an empty text (s06). `ResetValue`
      of a block cell makes it a text cell of no value, its block gone and its looks but the table's (b03), and empties it
      in the TABLECONTENT. A colour given to the table writes the rows it merged on load merged (D33c e02), as any edit.
    - Now: as AutoCAD for each of them; the cells carry their marks of the session through every edit that moves them.
      AutoCAD read the library's files of 32 of the 35 edits as it read its own, D33c's r04, r08, r09, r10, r12 and e02
      among them.
    - Still apart: a fill from a cell of no value, which AutoCAD leaves as it was and the library empties (a16); a clear of
      0 keeps a cell's own turn (145), which AutoCAD takes away (b01); and a clear of 0 after a row put in before the title
      takes the title's and the header's `170 5` and `140 6.0` from the rows moved down, and AutoCAD then merges the new
      first row on load where it does not merge the library's (s08).

## Annotative MTEXTs and INSERTs read and moved at their contexts

94. **An annotative MTEXT and an INSERT read at their context of 1:1 or their default; set, moved, turned and scaled; an
    attribute made of several lines and back, its MTEXT moved; whether attributes and INSERTs of no context are annotative and
    shown at 1:1; the horizontal mode of a context; copies of annotative objects.**
    - Try (D35b, headless, plugin GHD35BA rounds r1, r2 and r3): the drawing of D35 with its MTEXT given a record 40 wide in
      a box 20 and contexts of 1:1, 30 wide in a box 15 held by its middle, and of 1:2, 80 by 40, either the default; moved
      by (10, 5), turned 30 degrees and scaled by 2 about the origin, given a location, a turn, a direction, a text height, a
      width, a box and an attachment. AutoCAD's sample with the valve's context of 1:1 at (113.883, 279.776) turned 45
      degrees and scaled by 2, the default or beside a default of 1:2 at (-50, 279.776) turned 10 degrees and scaled by 3;
      moved, turned and scaled, given a point, a turn and scale factors, cloned and copied moved. CANNOSCALE 1:2 in the
      file; a TEXT of contexts flagged 0, an MTEXT of contexts and no AcadAnnotative, a TEXT and an ATTDEF of no contexts; an
      ATTRIB of no contexts and no AcadAnnotative, the block made not annotative, an ATTDEF of contexts flagged 0. A centred
      TEXT given the horizontal mode right; an ATTRIB and an ATTDEF of contexts made of several lines, and back, moved,
      scaled, given a point and a height; a TEXT and an MTEXT cloned and copied moved; seven drawings the library wrote, read
      back.
    - AutoCAD 2021: an MTEXT stands at its context's 11, reads along its 10, is as wide as its 40 and its box as high as its
      41, held by its 70, each the context's own (30 wide where the record says 40, whichever is the default), and as high
      as its record times the default's scale over 1:1's (2.5 at 1:2 is 1.25). Each set writes the context shown alone where
      it is not the default, a text height the record at the default's scale (5 at 1:1 is 10); `TransformBy` carries every
      context, its direction kept of unit length, its width and box scaled (of 1:2, (60, 44), 80 by 40 to (120, 88), 160 by
      80). An INSERT stands, turns and scales at its context (B5); a point and a turn set write the context shown alone; scale
      factors 3 set at 1:1 write 3 there and 6 at 1:2 and in the record; `TransformBy` carries every context (of 1:2 to
      (-183.189, 217.293) turned 40 for the turn). An INSERT of an annotative block holding no context, and its attribute
      holding none, are shown at 1:1, not at 1:2, whatever CANNOSCALE says; a TEXT holding none is shown at none. An ATTRIB is
      annotative as its block is: flagged 0 or not flagged, true in an annotative block, false in one that is not, contexts or
      not; a TEXT, an MTEXT or an ATTDEF by its own flag, false where it says 0 or is not flagged. A context's horizontal mode
      is read, and set in the context shown, its record too where that is the default. Made of several lines, an annotative
      attribute's every context takes the MTEXT's location as its second point, its 290 1 and the MTEXT's own context after
      it; made of one line again, its contexts are left as they were; moved, every context's MTEXT moves. A copy keeps every
      context and reads at its 1:1 (a TEXT's copy moved by `GetTransformedCopy` moves its context of 1:1 alone). The seven
      drawings the library wrote read back as the library reads them.
    - Now: as AutoCAD for MTEXT (`Location`, `Direction`, `Rotation`, `TextHeight`, `Width`, `Height`, `Attachment`, their
      setters, `TransformBy`), INSERT (`Position`, `Rotation`, `ScaleFactors` and so `BlockTransform`, their setters,
      `TransformBy`), `HasContext` of 1:1, `Annotative` of attributes and of a dynamic block's INSERT, the horizontal mode,
      an attribute made of several lines and moved, its `MTextAttribute`; a copy reads where its original is shown. Of 532
      lines AutoCAD gave in rounds r1 and r2, 337 read apart at base and 47 now.
    - Still apart: a copy holds no contexts (`HasContext` false, read at its record once added), where AutoCAD's keeps them;
      the INSERT's `Position`, `Rotation` and `ScaleFactors` set carry its attributes, as the library documents, where
      AutoCAD's move none; the MTEXT an attribute is made of is placed by its baseline (held by its bottom left), where
      AutoCAD places it by its top left one height above; the point of a text whose horizontal mode is set, and an MTEXT's
      extents, which AutoCAD works out from the font; an ATTRIB flagged 1 in a block that is not annotative, not seen (AutoCAD
      read nothing of the drawing made for it); and `SetAttachmentMovingLocation`, `Normal` and `BlockTransform` set on an
      annotative MTEXT or INSERT of contexts, which still write the record alone.

## Tables: copies of a cell emptied in the session, the turn and the rows out of place a clear keeps, a value's flag

95. **A fill or a copy of a cell emptied in the session; a series of a cell of no value; the turn a clear takes; a title
    and a header moved down, cleared; the flags of a value set; the merging groups of a merge and of a new cell.**
    - Try (D33e, headless, plugin GHD33E rounds EA, EB and read-backs R1 to R3): fills of every option and `CopyFrom` of
      none, `SkipFormat` and `SkipContent` from B-12 reset, a cell given an empty text, put in by `SetSize` or
      `InsertRows`, emptied by a merge undone, the block cell M12 reset, B-12 reset and given a value again, and from a cell
      read of no value; a series of such cells; `ClearTableStyleOverrides` of 0, 1 and 2 and `RemoveAllOverrides` of B-12
      turned 90 degrees and of M12 turned 0.5; the tables given a row before the title or the header, or one taking the
      title's look, then cleared or their cells' overrides removed; `IsTitleSuppressed` and `IsHeaderSuppressed` set and
      cleared; `SetValue` and `SetTextString` of cells reset, read of no value and put in; a merge and a merge undone (f01
      to f19, g01 to g21, t01 to t13, m01 to m10, n01 to n09, w01 to w11), and the library's files of the 75 edits and of
      D33b to D33d's 65 read back, nine of them variants of s08.
    - AutoCAD 2021: a cell emptied in the session holds no content: `Fill` with `CopyContent`, with `CopyFormat` or not,
      and `CopyFrom` leave the target as it was (Z stays Z), and a format copied gives it the cell's own alignment,
      background and lines alone, the target keeping its text style, height, colour and kind of value (B-12 reset of the
      table of its own data looks gives Z its top left alone, `91 262145`; Mark given B-12 by `CopyFrom` loses its middle
      centre and keeps GH_TXT and 3.5). A cell reset and given a value again is copied as any. A cell read of no value is
      copied as none, the target keeping its flags. With `GenerateSeries`, `CopyContent` given or not, a source of no value
      of either kind empties the target of no kind (`91 0`), and a clear then writes it `93 5`, `90 512`; so does a cell put
      in and given the whole of one read of no value. `ClearTableStyleOverrides` of 0 or 2 and `RemoveAllOverrides` turn
      the text and the block back to nought (145), set in the session or read; of 1 they stay; `RemoveAllOverrides` of a
      block cell takes its flag 0x40000. A clear of any option keeps the title and the header left out (`93 3`, `280 1`,
      `281 1`, and `93 1` or `93 2` set by the property), and of 0 or 2, and `RemoveAllOverrides`, keep in a row of the
      title's or the header's kind out of its place the looks of its kind (`170 5`, `140 6.0`; the header `170 5`), a row
      moved down or put in taking the title's look alike, and
      AutoCAD merges the new first row on load where the head says the title is left out (V-s08: the head alone decides).
      `SetValue` and `SetTextString` give a cell of `91 0` the flag `91 262144`. A merge's top left cell is `173 0`, a cell
      unmerged and a new cell `175 1`, `176 1`.
    - Now: as AutoCAD for each of them but those below. AutoCAD read the library's files of 73 of the 75 edits, and of 64
      of D33b to D33d's 65, as it read its own, D33d's a16, b01 and s08 among them.
    - Still apart: a clear of 2 of the table of its own data looks after a row put in before its title (n05), where AutoCAD
      gives the old rows of data the data's looks back as their own, which the row put in had moved into them and the
      library takes away; `RemoveAllOverrides` of a row put in taking the title's look, the title shown (n07), where
      AutoCAD keeps `170 5` and `140 6.0` and the library, which reads the kinds of the rows for it only where the table
      leaves its title or header out, takes them; the row of M12 turned 0.5, which AutoCAD makes higher for the block
      (layout); and D33c's e13, settled.

## Annotative copies, setters and attributes of several lines

96. **Copies of annotative objects and what they hold; `SetAttachmentMovingLocation`, `Normal` and `BlockTransform` set on
    annotative MTEXTs and INSERTs; attributes made of several lines of every justification; the box of an MTEXT scaled; an
    ATTRIB flagged annotative in a block that is not.**
    - Try (D35c, headless, plugin GHD35CA rounds r1, r2 and r3): D35b's drawings, the TEXT, MTEXT, ATTDEF and ATTRIB of two
      contexts, 1:1 or 1:2 the default, and the valve of AutoCAD's sample, cloned, copied by `GetTransformedCopy` moved by
      (10, 5), turned 30 degrees and scaled by 2, cloned and then moved, and copied and moved again once appended; the
      MTEXT held by its middle left or top right, given the normal (0, 0.6, 0.8); the valve given that normal and the
      placing to (50, 300) turned 0.5 and scaled by 3; drawings AutoCAD made: blocks not annotative of definitions left,
      centred, right, top left, top centre, top right, middle, bottom left, middle left, turned, 5 high, in a TrueType
      style and 0.7 wide, inserted turned 0.3 and scaled by 2 and plainly, each attribute and definition made of several
      lines; an MTEXT in a box 20 high scaled by 2 and one turned 30 degrees given a normal; a block not annotative whose
      definition was made annotative, its ATTRIB read flagged 1 with its context, without it, and flagged 0 with it; nine
      drawings the library wrote read back.
    - AutoCAD 2021: a copy keeps every context, in no database (`HasContext` of 1:1 and 1:2) and appended, under an
      extension dictionary, AcDbContextDataManager and ACDB_ANNOTATIONSCALES of its own, the context records as they were;
      an INSERT's copy appended has its attribute shown at both too. `TransformBy` of a copy, in no database or appended,
      moves every context; `GetTransformedCopy` of an MTEXT or an INSERT so too, but of a TEXT or an ATTDEF it moves the
      context shown alone, and the record's point and angle only where that is the default, its height, width factor and
      lean in the record whichever it is (scaled by 2 where 1:2 is the default, its record 5 high, its context of 1:2 left
      at (40, 12)). `SetAttachmentMovingLocation` moves by the width of the context shown (30, not the record's 40) and
      writes that context, and the record where it is the default. `Normal` set on an MTEXT, annotative or not, keeps its
      location and makes it read along the x axis of the new plane (turned 30 degrees, given (0, 0.6, 0.8), it reads along
      (-1, 0, 0)), the context shown written, the others left. `Normal` set on an INSERT, annotative or not, keeps its points
      in their plane: the record's point and every context left, so that it stands at (-113.883, -223.821, 167.866), or a
      plain one at (-100, -80, 60). `BlockTransform` set writes the context shown, and the record where it is the default,
      the others left. Made of several lines, an attribute or definition left, centred or right on its baseline is held by
      its MTEXT's top left, centre or right one text height above its point (left) or its second point, turned with it,
      whatever the font, its first point kept and its second point the MTEXT's location; a definition of any other
      justification is held by that point at its second point; an ATTRIB of one is held where its font puts it. Scaled by 2,
      an MTEXT 40 wide in a box 20 is 80 wide in one 40 high. An ATTRIB in a block not annotative is annotative where it
      is flagged 1 and holds a context, shown at 1:1, and not where it is flagged 1 and holds none or flagged 0.
    - Now: as AutoCAD for each of them but those below. Of 1 244 AutoCAD lines in rounds r1 and r2, 179 read apart at base
      and 61 now; AutoCAD read the nine library drawings, copies with their written contexts among them, as the library reads
      them (114 lines alike).
    - Still apart: the noise of AutoCAD's matrices on a copy's height, width factor and lean (a lean of 360 degrees); an
      ATTRIB made of several lines that is middle, bottom, aligned or fitted, or top and not of a definition, which AutoCAD
      places by its font; the width factor an attribute keeps when it is made of several lines, which the library takes
      out; `Normal` set on an MTEXT or an INSERT of no contexts, where the library keeps the MTEXT's turn and the INSERT's
      place in the world; and the INSERT's `Position`, `Rotation`, `ScaleFactors`, `Normal` and `BlockTransform` set
      carrying its attributes, the library's documented behaviour.

## Tables: the looks a row of data keeps, a row out of place cleared one cell at a time, the rows a text makes higher

97. **The table's own looks of the data a row put in before the first row of data takes, and what gives them back; a row
    of the title's or the header's kind out of its place, the title shown, its cells' overrides taken away one by one;
    the heights AutoCAD gives a row for a taller text, more lines, a larger or turned block.**
    - Try (D33f, headless, plugin GHD33F rounds FA and FR2): D28c's o0 (its own data looks of 9, GH_TXT, red and top
      left) given a row at 0, at 2 or at 3, then `ClearTableStyleOverrides` of 2, of 1 or 0 then 2, of 2 twice,
      `RemoveAllOverrides` of B-12 and of M12, B-12 given a height of 5, the row put in taken out again, a column put
      in, a second row at 0; the library's own file of o0 given a row at 0, cleared of 2 by AutoCAD; the drawn table
      given a row taking the header's look and one taking the title's, their cells' overrides taken away; B-12 given a
      text height of 3, 5 and 8, two and three lines (`A\PB`, `A\PB\PC`), two lines at 5, Mark of romans 9 high and of
      two lines, the title 9 high, M12 scaled by 2.5 and turned a quarter turn, a height of 8 cleared of 0 (a01 to a14,
      b01, b02, h01 to h12); and the library's files of the 28 edits and of D33e's 75 read back.
    - AutoCAD 2021: the table's own looks of the data are kept with each row of data. A row put in before the first row
      of data takes them from the table and leaves them with the old rows; a clear of 2 and `RemoveAllOverrides` give
      B-12 and M12 back GH_TXT, red, 9 and top left (`91 262201`, the block cell `91 57`), after a second row at 0, the
      row taken out again, B-12 given 5, or a clear of 2 before; a clear of 0 or 1 takes them from the rows, and a clear
      of 2 after it gives none. A row put in after the data keeps none of them through a clear of 2. A column put in
      gives its cell in such a row the looks too. A clear of 2 by AutoCAD of the library's file of o0 given a row at 0,
      read as AutoCAD reads a file of the library, gives none. `RemoveAllOverrides` of a row put in taking the title's
      look keeps `170 5` and `140 6.0`, and of one taking the header's `170 5`. A row is made as high as its text needs:
      one line of a height h of txt or romans `4/3 h` and the margins above and below (9.666667 for 5, 13.666667 for 8,
      15 for Mark 9 high), n lines `h + (n - 1) 5/3 h` (15 for two lines of 4.5, 22.5 for three, 16.333333 for two of
      5, 12.333333 for two of 3.5); a lower text leaves it as it is, and a clear of 0 after a height of 8 makes it 9
      again; M12 scaled by 2.5 makes its row 13 (the circle of 4 across), turned a quarter turn 11.5 and turned 0.5
      10.146656 (the width of the text M12 counted).
    - Now: as AutoCAD for the rows of data while the drawing is open, and for the row taking the title's or the header's
      look. AutoCAD read the library's files of 24 of the 28 edits, and of all 75 of D33e's, n05 and n07 among them, as it
      read its own.
    - Still apart: the row put in after the data, which the library's clear of 2 gives the table's own looks of the data
      (a04; AutoCAD's own cells read 0.18 high after it); the cell of a column put in in such a row, which the library
      gives none (a09); the table's own looks of the data, which AutoCAD takes again from the first row of data when the
      row put in before it goes and the library does not (a07, its cells alike); AutoCAD's clear of 2 of the library's
      file, which reads its title and data 0.18 high (a06); and the heights of rows, which the library leaves as they are
      written, as AutoCAD reads the library's files of h01 to h12 alike all the same: matching them needs the widths of the
      letters of the font, to know where a text breaks and how wide a turned block's attribute is.

## A normal set on entities of no contexts, a narrow attribute of several lines, annotative copies into another drawing, `HandOverTo`

98. **What a `Normal` set keeps, in the world or in the plane, on each entity that has one; the width factor of an attribute
    made of several lines; the scales an annotative copy stands on in another drawing; what an entity handed over to a copy
    of contexts holds.**
    - Try (D35d, headless, plugin GHD35DA rounds r1 to r5): a LINE, a POINT, a CIRCLE, an ARC, an LWPOLYLINE and a POLYLINE
      at an elevation of 3, a SOLID, a TEXT turned 0.3, a centred TEXT, an ATTDEF, an INSERT turned 0.3 and its ATTRIB, an
      MTEXT turned 30 degrees, a HATCH at an elevation of 2 and a rotated DIMENSION, all in the plane z = 0, given the normal
      (0, 0.6, 0.8), and (0.6, 0, 0.8) first; D35c's attribute A7, 0.7 wide, and its definition made of several lines; a
      drawing of the scales Q5 of 1:5 and S7 of 1:3 holding a TEXT of 1:1, 1:2, Q5 and S7, a TEXT of S7 and 1:1, an MTEXT of
      1:1 and Q5 and an INSERT of an annotative block of Q5 with its attribute, copied by `WblockCloneObjects`, by
      `Insert(Matrix3d.Identity, ...)` and by `Insert("XB", ...)` into a drawing of S7 of 1:7 and R5 of 1:5, and D35b's G2
      into it; a LINE, a plain TEXT, T1 and T2 of G2 handed over to a clone of T1, moved or not, of a LINE of the extended
      data GHA, keeping the extended data and the extension dictionary or not; AutoCAD's files of them, and nineteen of the
      library's, read back.
    - AutoCAD 2021: a `Normal` set keeps in the world the ends of a LINE, a POINT, the centre of a CIRCLE and of an ARC, whose
      angles are kept in the new plane, and the location of an MTEXT, which reads along the x axis of the new plane, turned
      nought ((-1, 0, 0) for (0, 0.6, 0.8)); it keeps in the plane, their coordinates written as they were, the vertices and
      elevation of an LWPOLYLINE and a POLYLINE, the corners of a SOLID (from (200, 0) to (-200, 0, 0)), the two points and
      the turn of a TEXT, an ATTDEF and an ATTRIB (from (11, 12) to (-11, -9.6, 7.2), turned 0.3), the point and turn of an
      INSERT (from (300, 100) to (-300, -80, 60)), whose ATTRIBs stay where they were, and the loops and elevation of a
      HATCH. A rotated DIMENSION keeps its extension line points in the world, gives its dimension line point and text point
      in the world laid on the new plane through the origin ((515, 10, 0) to (515, 6.4, -4.8)) and its elevation nought,
      but its text point given (0.6, 0, 0.8) first is kept in the plane. Made of several lines, an attribute or definition
      0.7 wide keeps its 41 of 0.7, its MTEXT's words opening with `\W0.7000;`; one of width 1 has no `\W`. Copied into
      another drawing, each context is put on that drawing's scale of its scale's name, letters of either case the same,
      where the two give the same scale (1:1 onto 1:1); a scale the drawing has not of that name is added to its list, of
      the same units (Q5 of 1:5 added though R5 of 1:5 is there, 1:2 added for G2's copies); one of the name of other units
      is added as the name and `_1` (S7 of 1:3 onto an S7_1 of 1:3 beside the drawing's S7 of 1:7); the copy is shown where
      its original was; the block of an annotative INSERT comes annotative, its definitions with their contexts. Handed over:
      keeping the extension dictionary, the old entity's dictionary is the new one's, with the contexts it had or none (a
      LINE's none, read back at the copy's record, (40, 12) 2.5 high), though AutoCAD shows the copy's until the file is
      read again; not keeping it, the copy's contexts are written under a dictionary of its own, read back at (11, 12) 1.25
      high; keeping the extended data, the old entity's replaces the copy's where it has some (GHA alone, not annotative),
      and the copy keeps its own where it has none. AutoCAD wrote an MTEXT handed over to T1's dictionary, of TEXT contexts,
      that it crashed reading back.
    - Now: as AutoCAD for each of them but those below. Of 196 AutoCAD lines in round r1, 59 read apart at base and 8
      now; of 106 in r2, 16 and 10; of 32 in r3, 24 and none; of 82 in r4, 14 and 8. AutoCAD read the nineteen library
      drawings of r5 as the library reads them (231 of 232 lines alike).
    - Still apart: the DIMENSION, which the library keeps where it stands in the world, its elevation the height of its
      points in the new plane; the ATTRIBs of an INSERT given a normal, which the library carries with the block, as it
      documents; what AutoCAD shows of an entity handed over keeping its extension dictionary before the file is read again
      (the library reads what the file holds); the point of a centred TEXT copied into another drawing, which AutoCAD works
      out again from the font; whether an INSERT of no context record has an extension dictionary (AutoCAD says it has); and
      the names of the entries AutoCAD gives the scales it adds (`*A1`, `*A2`), where the library's `AddContext` gives
      `A0`, `A1` and on.

## Tables: the looks each row keeps, and AutoCAD's clear of a table it read

99. **The looks AutoCAD keeps with each row (its ROWTABLEFORMAT) through rows and columns put in and taken out; AutoCAD's
    clear of 2 of its own file of a table given a row before its title.**
    - Try (D33g, headless, plugin GHD33G rounds GA, GB, GR1 and GR3): D28c's o0 (its own data looks of 9, GH_TXT, red and
      top left) given a row at 3, after B-12, then a clear of 2, a column, a row taking its look, or B-12 taken out; `SetSize`
      of 4 rows; a row at 0 then a column, a row taking B-12's look, the row at 0 taken out again, or a second row at 0; two
      rows at 0 taken out together, or one of them; `SetTextHeight(5.0, DataRow)` and `SetContentColor(blue, DataRow)`
      after a row at 0 or 3; AutoCAD's own file of o0 given a row at 0 (D33f a12) and of the drawn table given one (D33e
      m09) read again and cleared of 2 or 0, or given `RemoveAllOverrides`; the library's file of it, and that file with
      AutoCAD's TABLECONTENT put in its place, cleared of 2; and the library's files of 35 edits, and of D33f's 103, read
      back.
    - AutoCAD 2021: each row keeps its looks, the table's own of a kind those of its first row of that kind. A row put in
      by `InsertRows` or `SetSize` keeps none: cleared of 2, its cells are written `91 0`, and B-12 above keeps GH_TXT, red,
      9 and top left; a column put in gives its cell none there, the cell of B-12's row the data's (`91 57`), the title
      moved down `170 5`, `140 6.0` (`91 33`) and the header moved down `170 5`, its green and its 9 (`91 39`). A row put in
      taking a row's look keeps that row's looks, none of a row put in. The row at 0 taken out again, or both rows at 0
      together, the table's head reads `93 6886016` again, its data's looks as before; B-12 taken out after a row put in
      after it, they go (`93 2101760`). A second row at 0 takes them from the table again, the rows that kept them keeping
      them; a setter of the data's looks sets each row of data. AutoCAD's own file of o0 given a row at 0, read again and
      cleared of 2, reads the title and B-12 0.18 and 4.5 high and the header without its looks, as AutoCAD reads the
      library's file of it: its head given the title's `63 0`, `140 0.18`, `283 1` (`93 1050883`), the header's gone; the
      library's file with AutoCAD's TABLECONTENT alike. The library's TABLECONTENT is not what differs: AutoCAD's clear of
      a table it read is.
    - Now: as AutoCAD for the rows put in, the rows taking a look, the columns and the rows taken out. AutoCAD read 17 of
      29 of the library's files of round GA as its own (5 at base), all 6 of GB's, and 102 of D33f's 103 (99 at base).
    - Still apart: the clear of 2 of a table AutoCAD read (a06, c01 to c04; the library gives the title moved down 6 and
      the header its green, as AutoCAD does while the drawing is open); a title or header moved back into its place by a
      row taken out, whose cells AutoCAD writes without the looks of its kind it was given (k02, the title's `170 5` and
      `140 6.0`); the title moved down merged anew over a column `SetSize` puts in (g05); the value of a cell of a row put in
      taking the look of a row put in (h03, h04, `String:` for none); and the setters of a kind's looks, which the library
      has not.

## `HandOverTo` keeping a dictionary of another kind's contexts, the names of the scales added, an annotative INSERT of no context

100. **What AutoCAD makes of an entity handed over to one of another kind keeping its extension dictionary, and whether it
    reads the drawing; the names of the entries of the scales it adds; the extension dictionary of an annotative INSERT
    of no context.**
    - Try (D35e, headless, plugin GHD35EA rounds r1 and r2, each drawing that might crash AutoCAD read in a run of its own
      by GHD35ER): in D35d's H0, the TEXT T1 handed over to a clone of the MTEXT M1, M1 to a clone of T1, the INSERT of ANB
      to T1 and T1 to the INSERT, the ATTDEF MSDEF to T1 and T1 to MSDEF, T1 to a new LINE, keeping the extended data and
      the extension dictionary, and the first three not keeping the dictionary; AutoCAD's files of them read back; the
      library's files of seven handovers its guard lets through, read back; a new drawing given the scales Z1, Z2 and
      Z3, one of them taken out and Z4 added, and AutoCAD's file of it read again; an INSERT of an annotative block made in
      code, written, and read again.
    - AutoCAD 2021: keeping the dictionary, the new entity holds the old one's contexts as they were, unconverted, of
      whatever kind: an MTEXT of ACDB_TEXTOBJECTCONTEXTDATA_CLASS, a TEXT of ACDB_MTEXTOBJECTCONTEXTDATA_CLASS or of
      ACDB_BLKREFOBJECTCONTEXTDATA_CLASS, an INSERT of a TEXT's, a LINE of a TEXT's, each shown at 1:1 and 1:2 while the
      drawing is open. It crashed reading four of its own drawings: the MTEXT of a TEXT's contexts (FATAL access violation,
      D35d's A5), the TEXT of an MTEXT's and the TEXT of an INSERT's (exception 0xc0000409 in acdb24, no message), and the
      INSERT of a TEXT's (FATAL access violation). It read the TEXT of an ATTDEF's contexts
      (ACDB_MTEXTATTRIBUTEOBJECTCONTEXTDATA_CLASS) and the ATTDEF of a TEXT's at 1:1 and 1:2, and the LINE of a TEXT's at
      none. Not keeping it, the new entity writes contexts of its own kind. Its `AddContext` names the entries `*A1`, `*A2`,
      `*A3`, beside its own scales named A0 to B6; Z3 taken out, Z4 is `*A4`, and Z1 and Z4 taken out, Z5 is `*A5`: a number
      given is not given again while the drawing is open. Read again, the entries are numbered anew, `*A1`, `*A3` and `*A4`
      reading `*A1`, `*A2` and `*A3`. An INSERT of an annotative block made in code has no extension dictionary, and
      AutoCAD writes none; reading the drawing, AutoCAD gives it one of a context of 1:1, its default, at the INSERT's
      point, angle and scale.
    - Now: `HandOverTo` keeping the dictionary refuses one of contexts of another kind, before anything is written
      (`ArgumentException`): a TEXT, an ATTDEF and an ATTRIB take one another's, an MTEXT and an INSERT their own, an
      entity never annotative any, a dimension, a leader, a multileader, a tolerance and a hatch those of their own class.
      AutoCAD read the library's seven files of what is let through, each in a run of its own, as it read its own. The
      entries of the scales the library adds are named as AutoCAD names them; AutoCAD read the library's `*A1`, `*A3` and
      `*A4` beside AutoCAD's own. The library writes no extension dictionary for an annotative INSERT of no context, as
      AutoCAD writes none, and reads it at 1:1.
    - Still apart: the entries of the scales a drawing read holds, which the library keeps as they are where AutoCAD
      numbers them anew; the extension dictionary AutoCAD makes for an annotative INSERT of no context as it reads a
      drawing, which the library does not make, its context of 1:1 read all the same; and a LINE holding contexts, which
      `HasContext` reads where AutoCAD reads none.

## A multileader scaled, a leader line of one vertex, a table's reals, the contexts of an entity never annotative

101. **Which `TransformBy` marks a multileader's scale as its own; what `RemoveFirstVertex` and `RemoveLastVertex` leave of a
    leader line, down to one vertex and to none; how a table writes the heights and widths it puts in; whether an entity
    never annotative is shown at the scales of the contexts it holds.**
    - Try (D37, headless, plugin GHD37 rounds r1 to r3, each case that might crash AutoCAD in a run of its own): on a
      Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8, the words "ABC" at (100, 50) and the box of lines from
      (-10, -5) to (20, 15) there, each met from (0, 0), moved by (10, 20), turned 30 degrees, mirrored across the XY and the
      YZ plane, scaled by 1, 2 and 0.5, about (100, 50), turned and scaled by 3, mirrored and scaled by 2, scaled by 2 and
      back by 0.5; the line from (0, 0) given `RemoveFirstVertex` and `RemoveLastVertex` once, twice and three times, with
      and without its dogleg drawn, on the right, of the box, of a leader of two lines, of three vertices, then given
      vertices again, moved and scaled; a line just added to a landed leader and the line of a leader landed by its first
      vertex given `RemoveFirstVertex`; AutoCAD's own drawing of a line of one vertex read and given it again; the library's
      files of seven of these read back. D35e's TEXT of contexts of 1:1 and 1:2 handed over to a new LINE, CIRCLE, ARC,
      POINT, ELLIPSE, XLINE, RAY, SOLID, 3DFACE, LWPOLYLINE and SPLINE, keeping its extension dictionary.
    - AutoCAD 2021: every `TransformBy` that scales sets bit 24 of the overrides (90), the scale itself still 1, and the
      bit stays once scaled back; a move, a turn, a mirror, a scaling by 1 and the identity leave it. A mirror across the YZ
      plane sets bit 14 too, of the words and of the box. `RemoveFirstVertex` takes a line of two down to its landing:
      (90, 48) beside the words, (82, 55) beside the box, written as a LEADER_LINE of no vertex of its own under a leader
      landed there (290 1), drawn as its dogleg alone, the content left. `RemoveLastVertex` makes the vertex before the last
      the leader's point (10) as it stands, with no dogleg drawn too: (0, 0), the vertex then read (8, 0), from a line of two
      as from one of three. Taken down to none in the session, the leader is written of no landing (290 0, its point kept)
      and draws no dogleg; given a vertex again, it is landed there. It crashed (FATAL access violation, no event in the log)
      taking the vertex out of a line just added to a landed leader, out of the line of a leader landed by its first vertex,
      and out of its own drawing's line of one vertex once read, and took nothing out of a line of none it had emptied
      itself. `RemoveLastVertex` of a leader of two lines answered eInvalidContext, of either line, of two vertices or of
      three, and changed nothing; `RemoveFirstVertex` of one of them took it down to one. Each entity handed the TEXT's
      place held both records and answered `HasContext` of neither scale, `Annotative` NotApplicable, while the drawing was
      open, as D35e's LINE did both then and read back. Every real of an ACAD_TABLE AutoCAD wrote in D33 to D33g bears its point: `141
      9.0`, `142 20.0`, `141 12.0`, `140 7.0`, the double of a cell `140 12.0` and `140 1000.0`, the insertion point moved
      `10 310.0`.
    - Now: a `TransformBy` that scales sets bit 24, and AutoCAD read it back so; the library's 15 cases of the box give
      AutoCAD's overrides but for bit 14 of the YZ mirror. `RemoveFirstVertex` and `RemoveLastVertex` take a line down to
      one vertex, written and drawn as AutoCAD writes and draws it, and AutoCAD read the library's files of them as its own;
      the one vertex is kept (`InvalidOperationException`), as AutoCAD cannot take it out of a drawing it has read, and so is
      the last vertex of a leader of more lines than one. `RemoveLastVertex` writes the leader's point as the vertex
      stands. A table writes as reals the heights and widths `InsertRows`, `InsertColumns`, `SetRowHeight`,
      `SetColumnWidth` and `SetSize` put in, the sizes `TransformBy` scales, the text heights it and `SetTextHeight` give a
      cell, a cell's double, block scale and turn. `HasContext` answers false for every object never annotative.
    - Still apart: bit 14 of a mirror across the YZ plane (now as AutoCAD's, point 104); the insertion point and direction a table moved or turned
      writes, which the library writes as it writes every point of an entity (`10 310`); a leader taken down to none, which
      the library refuses where AutoCAD, in the session that emptied it, allows it.

## The words measured in SHX fonts

102. **SHX fonts read and measured: TEXTs placed and boxed, MTEXTs of one line, multileaders met on the right, attribute
    definitions flipped, a leader's hook exploded.**
    - Try (F1a, headless, plugin GHF1AA and GHF1AB, fonts of AutoCAD 2021 read in place, none copied): every letter of txt,
      romans, simplex, isocp, gdt, complex, bold and times.shx boxed by `TextStyle.ExtentsBox`; TEXTs of 26 words, 15
      justifications, heights 1 and 4, width factors 1 and 0.7 and slants 0 and 15, after `AdjustAlignment`; MTEXTs of one line
      and their codes; multileaders of nine words met on either side. Then (F1b, plugin GHF1BA and GHF1BB): multileaders of
      txt and romans words "Ag", "gA", "g", "ABC", "_A" and "Il1" at every attachment of either side, and "ABC" given
      "ABCDEF" and "AB" anew met on the right and on both sides; a drawing of the library's of blocks of a flip about x = 45
      holding locked ATTDEFs of txt.shx, txt and romans.shx, of every justification and of spaces before and after, read by
      DxfIn with the drawing made the working database and without, INSERTs plain and turned a quarter at the scale 2
      flipped; LEADERs of an MTEXT of txt, their words beside and above the line, exploded. Each compared with the library
      measuring the same fonts (`FontSearchPath`).
    - AutoCAD 2021: the fonts read as ezdxf reads them but for four things (a shape within a unifont numbered big-endian,
      the pen put down inking its point, the angles of an arc of fractions whole degrees, the octants of an arc of a sign
      and a size), an arc boxed by its chord and height up to half a turn and by its circle beyond; a TEXT placed and boxed
      by its ink, its box from the start of its baseline to its last pen where its last letter is a space; an MTEXT of one
      line as wide as from its first ink, or its start where it starts with a space, to the ink of its last letter that is
      no space; a leader met on the right the gap past the words' ink, and words reaching below their baseline as high as
      their height and that depth together ("Ag" of txt met 2.667 below its top at the middle of its top line, 5.333 at its
      bottom and under it); words met on the right moved to keep their landing as their height, scale or words change, met
      on both sides kept as on the left and the right landed anew. A flip keeps an ATTDEF readable, upright and turned as
      before: one aligned right, aligned or fitted on its baseline put with its first point at its second mirrored, one
      held by the middle of its words with its second point mirrored, any other with the box of its ink mirrored ("L1" of
      txt 5 high at (20, 60) at (63.33, 60)), its ATTRIBs moved as much; `txt` reads as `txt.shx`. Read with DxfIn but not
      made the working database, the drawing's fonts are not loaded for the flip: AutoCAD flipped the txt.shx ATTDEF to
      (63.0014, 60), a box 2052 units of 1466 wide, and the txt and romans ones as boxes of no width, as D34 saw (point 85).
      A leader whose words sit above its line (DIMTAD 1) explodes its hook on to their far end, (28.667, 8.125) for "ABC"
      2.5 high held at (22, 10); beside the line (DIMTAD 0), the polyline ends at the hook.
    - Now: the library reads and measures so where `FontSearchPath` finds the font: 1 798 of 1 814 letters to 1E-6 of a
      unit, 21 596 of 21 600 TEXTs, 170 of the 170 MTEXTs it measures, 196 of 196 multileaders of SHX words (108 of F1a, 88
      of F1b), 90 of 90 flipped ATTDEFs and ATTRIBs, and the leader of "ABC" above its line exploded as AutoCAD exploded
      it (the other five of the round follow the same rule and were not run through the library).
    - Still apart: TrueType fonts (Arial's 18 multileaders, every Arial TEXT), the letters of a big font and a TEXT held at
      its bottom with one, an MTEXT of several lines, of codes other than `\W` and `\H`, or of a font of shapes (times.shx
      "A" 3.667 wide in an MTEXT, its ink 3.933), the lines of `%%u`, `%%o` and `%%k`, which AutoCAD boxes a TEXT by
      (txt "%%u%%oA" centred on -1.4); bold's circles of a radius past 12, the ogonek letters of romans and simplex, the
      quotes of times.shx slanted, isocp's stand-in for a letter it has not, and txt "i" aligned, as F1a left them; words
      mirrored across their plane whose dogleg is then set, which AutoCAD moved (to 117.67 for 110.67 and a dogleg of 15)
      and the library leaves, their words running against the plane's x (now moved: point 104). TrueType fonts are measured
      now: see point 103; MTEXTs of several lines and of more codes are laid out: point 107.

## The words measured in TrueType fonts

103. **TrueType fonts read and measured: TEXTs placed and boxed, MTEXTs of one line, multileaders met on the right, attribute
    definitions flipped, a leader's hook exploded.**
    - Try (F2, headless, plugin GHF2A, GHF2B and GHF2C, fonts of Windows read in place, none copied): TEXTs of 38 words (spaces
      before, between and after, letters above and below the baseline, %% codes, a letter the font has not), 15
      justifications, width factors 1 and 0.7 and slants 0 and 15, after `AdjustAlignment`, and MTEXTs of one line of 36
      words and codes, in arial.ttf, times.ttf, calibri.ttf, malgun.ttf, symbol.ttf, wingding.ttf, isocpeur.ttf,
      romans__.ttf, seguisym.ttf and tahoma.ttf; in arial.ttf named Arial bold, Arial italic, Times New Roman and arial, in
      arialbd.ttf, and in simsun.ttc and msgothic.ttc named by no typeface and by SimSun, NSimSun and MS Gothic;
      multileaders of words of Arial, Times New Roman, Calibri and isocpeur.ttf met on either side at every attachment and
      given anew; LEADERs of an MTEXT of Arial above and beside their line, exploded; a drawing of the library's of blocks of
      a flip about x = 45 holding locked ATTDEFs of Arial, Times New Roman and Calibri of every justification and of spaces
      before and after, read by DxfIn as the working database. With them, the TrueType rows of F1a: TEXTs and MTEXTs of
      Arial, Times New Roman, Verdana, Calibri, Courier New, Consolas and Tahoma, and Arial's 18 multileaders.
    - AutoCAD 2021: a text's height is the top of the font's A, no pair of letters is kerned, and each letter is boxed as
      its glyf header boxes it, at least a unit wide and high as Windows boxes it (a space, of no outline, a unit below the
      baseline at the pen), as high as it is from the lower of its bottom and the baseline, and up to the baseline at least:
      Arial's degree sign, inked from 936 to 1491 units up, is boxed from 0 to 555, and its low line, from -407 to -277,
      from -407 to 0. A TEXT is boxed, for its place and its `GeometricExtents`, from the start of its baseline or its ink
      left of it to its last pen or its ink past it, slanted whole by its oblique; at its bottom it stands on the font's
      `usWinDescent`; `%%u` and `%%o` draw no line in its box. `%%c` is the font's U+2205 where it has one (isocpeur.ttf,
      seguisym.ttf), else U+2205 of a font Windows links the font to (Tahoma, Malgun Gothic, SimSun), else U+00D8
      (Arial). A letter the font has not is drawn from another font (Arial "中" 5.514 wide at 4 high, not its stand-in's
      4.191). An MTEXT of one line leaves ink left of its start out, counts a space between letters a unit below the
      baseline and those before and after not, and sets letters after a change of height on from the ink of those before
      it (Arial "A\H2x;BC" 18.819 wide, not 18.810). A multileader is met on the right at that width, lower by the words'
      depth; a flip mirrors the box of an ATTDEF's words without the spaces at either end ("L1 " and " L1" of Arial as
      "L1"); a leader's hook runs on to the far end of its words. A style that names a typeface is drawn in that typeface,
      its file name written anew: arial.ttf named Arial bold in arialbd.ttf, named Times New Roman in times.ttf; a
      collection named by no typeface is not loaded (simsun.ttc drawn in simplex.shx, FONTALT), and one named by its
      typeface written as SimSun.ttf, a file that is none. Symbol and Wingdings, read through their subtable of symbols,
      have letters drawn from other fonts (Symbol "_" of Times New Roman) and widths a unit off.
    - Now: the library measures so, where `FontSearchPath` finds the font and it is the face AutoCAD draws: of the F2 rounds,
      30 840 of 31 200 TEXTs it measures, 436 of 445 MTEXTs, 280 of 288 multileaders, 142 of 144 flipped ATTDEFs and 20 of
      20 exploded LEADERs; of F1a's TrueType rows, 14 400 of 14 460 TEXTs, 152 of 153 MTEXTs and 18 of 18 multileaders. It
      leaves unmeasured a letter the font has not, `%%k`, a change of width or height within an MTEXT, Symbol and
      Wingdings, a collection named by no typeface or by its second face, and a style whose typeface, bold or italic is not
      its file's (arial.ttf named Arial bold, since AutoCAD draws arialbd.ttf).
    - Still apart: `%%c` of a font Windows links to others that have U+2205 (Tahoma, Malgun Gothic, SimSun: 360 TEXTs and 6
      MTEXTs), which the library, reading no such link, draws as the font's U+00D8; Calibri's degree sign, whose glyf header
      reaches 103 units left of the outline AutoCAD draws (3 MTEXTs, 8 multileaders, 2 ATTDEFs); a style of a multileader
      other than its words', by whose font AutoCAD lands a leader added at first and then moves the words (Arial words on a
      style of txt.shx; now moved so: point 104). Not tried: CFF outlines, which the library does not read, and .otf files.

## Multileaders: words mirrored across their plane, words of another text style, a new multileader's groups

104. **How words mirrored across their plane, which run against its x axis, move when the dogleg, gap, height, scale,
    attachment, words or a leader's last vertex are set; where a leader is landed on words of a text style other than the
    multileader's; what a new multileader writes in 295, in a new leader line's 92 and in bits 14, 15 and 18 of its
    overrides.**
    - Try (D38, headless, plugin GHD38A rounds r1 to r3 and GHD38R, fonts of AutoCAD 2021 and Windows read in place, each
      database made the working one while measured): on a Standard of words 4 high, arrows 4, a gap of 2 and doglegs of 8,
      met at the middle of the top line, "ABC" of txt.shx at (100, 50) met from (0, 0), (250, 0) or both, mirrored across the
      XY plane, then given a dogleg of 15 (the multileader's and a leader's own), a gap of 5, a height of 10, a scale of 2,
      the attachments 0 and 6, "ABCDEF" and "ABC" anew, a last vertex at (40, 70), a leader from (0, 100) and from (250, 100),
      and three of these at once; words turned half round, and turned 180 degrees about (100, 50); "Ag", "ABC" and "_A" of
      Arial, Times New Roman and romans.shx on a multileader style of txt.shx, Arial or romans.shx, at attachments 1 and 4,
      met on either side or both, the words given in memory, after the multileader is added, or with the leader in a later
      transaction, the multileader's own text style set before them to theirs or to a third, and the same of no leader,
      saved, read again and given one; multileaders made with SetDatabaseDefaults and without, of words given BYLAYER,
      BYBLOCK, red, of no colour and by default, of a block and of none; leaders run on to the words or not, with doglegs or
      none, exploded; files of R2010 and R2007; a line given BYBLOCK and BYLAYER, on a multileader of green lines, and a line
      added to one; mirrors across the YZ, XZ and XY planes, planes of normal (1, 1, 0), (1, 0.2, 0), (0.2, 1, 0), (1, 0, 1)
      and (0.2, 0, 1), twice across YZ, a half turn about y, a quarter turn then YZ, the alignment set to the centre or the
      right first, of words and of a block; and 62 of the library's files of these, read back.
    - AutoCAD 2021: mirrored words say so (294 of 1) and move as words along the plane's x axis do, held at their right (the
      top left of their reading) and their up against the plane's y axis: a dogleg of 15 moves them from (110.67, 50) to
      (117.67, 50), a gap of 5 to (113.67, 50), the top of the top line to (110.67, 48), the vertex set to (40, 70) to
      (60.67, 72); keeping their left edge at 100, a height of 10 to (126.67, 53), a scale of 2 to (131.33, 52) and "ABCDEF" to
      (122.67, 50). Met on the right, held at their right edge: a dogleg of 15 to (103.67, 50), a height of 10 to (110.67, 53)
      with the base point of the content moved with their left edge, by 16, a scale of 2 to (100.67, 52); met on both sides,
      the right landed anew past their right edge, from (136.67, 48) at a height of 10; a leader from (250, 100) landed from
      (120.67, 48). Words turned half round turn the plane with them (111 of (-1, 0, 0)), and move as words along it. Words
      given in a text style other than the multileader's are put where a leader would meet them in the multileader's font,
      then moved to meet that place in their own: "Ag" of Arial on txt.shx stands at (100, 49.92) for (100, 50), a leader
      landed at (90, 47.33) or (116.46, 47.33), twice as low at the bottom of the bottom line; "ABC" 0.03 higher; given in
      memory, after the multileader is added or with the leader later, alike; the multileader's own text style set first is
      the one measured by; read again, the file's words and base point agree, and a leader added lands where they meet. Every
      new multileader writes 295 1 (`ExtendLeaderToText` true), made with SetDatabaseDefaults or not, of words, a block or
      none, and no 295 in R2010 or R2007; run on or not, they explode alike. A new leader line writes no 92, and a line given
      BYBLOCK its flag (93 2) and no 92. Words given set bit 18 and give the multileader their colour (92 and the MTEXT's 90),
      with bit 15 but for BYBLOCK, whose MTEXT is written BYLAYER. A mirror that turns the plane's x axis back (across YZ, or
      a plane of normal (1, 0.2, 0)) sets bit 14 and the context's alignment (176) to 2 for 0 and 0 for 2, a centred one
      kept; twice across YZ, 0 again, the bit kept; across XY, XZ, (1, 1, 0) and the tilted planes, the half turn and the
      quarter turn then YZ leave both.
    - Now: the library moves mirrored words that say so as AutoCAD does: by the dogleg, gap, attachment and last vertex
      with no font, by their height, the scale and the words given anew where `FontSearchPath` measures them (left with
      none, as before); it writes 294 of the words it keeps readable and lands a new leader on either side of them. It moves
      words of another text style as AutoCAD does where both fonts are measured, writes 295 1 on a new multileader of R2013
      or later, no 92 on a new leader line, and bits 14, 15 and 18 and the alignment 176 as above. Of round 1's 119 cases,
      103 give AutoCAD's places, vertices, doglegs, bits, 294, 295 and lines' 92 (none at base); AutoCAD read 53 of 62 of the
      library's files as it made its own, eight of the others the cases still apart and the ninth a file made before the
      words of another style were moved.
    - Still apart: words turned half round in code, whose plane the library keeps where AutoCAD turns it; words mirrored
      within their plane across XZ or a plane of normal (0.2, 1, 0), which AutoCAD sets upright ((100, -46) for
      (110.67, -50)); 294 of a block whose plane's x axis turns back, which AutoCAD sets; a multileader made without
      SetDatabaseDefaults, which AutoCAD lays out otherwise (bit 16, no text colour taken, the leader at the words); the
      base point of words given (10 of the context data), which the library puts at the words and AutoCAD where a leader from
      the left would meet them, so that AutoCAD, adding a leader to the library's file, lands it at the words and moves them;
      295 0 in a drawing older than R2013, where AutoCAD writes none; a line given BYBLOCK, which the library writes 92 of;
      `GetLeaderLineColor` of a line of no colour of its own, BYBLOCK in AutoCAD and the multileader's in the library;
      `TextAlignmentType`, read from 175 where AutoCAD reads the context's 176; a multileader of no content, which AutoCAD
      reads as one of words. All but the multileader made without SetDatabaseDefaults are as AutoCAD's now: point 105.

## Multileaders: the base point of words given, words turned in code, words mirrored upside down

105. **Where the base point of the content (10 of the context data) of words given goes, so that AutoCAD lands a leader it
    adds to the library's file and leaves the words; how words turned in code turn the plane; which way words mirrored
    across a plane that turns them upside down are read; and the small readings D38 left.**
    - Try (D38b, headless, plugin GHD38BA rounds r1 and r3 and GHD38BR rounds r2 and r4, fonts of AutoCAD 2021 read in
      place, each database made the working one while measured): on D38's Standard, "ABC", "Ag" and "A\PB\PC" of txt.shx at
      (100, 50), of no leader and met from (0, 0), (250, 0) or (105, 0), at the attachments 0 to 8, held at the top left,
      centre and right, the alignment set to the centre first, given anew at (150, 80), moved, 10 high, a gap of 5, a dogleg
      of 15, a scale of 2, the left set to the bottom line, given after the multileader is added and after its leaders;
      words turned 30, 90, 135, 180 and 270 degrees by their rotation or their direction, of no leader and met on either
      side, given turned anew, then edited, mirrored across XY, moved, given a second leader, scaled and attached anew;
      words mirrored across XZ, met from either side, "Ag", held at the centre and the right, at the attachments 0 and 4,
      then given a dogleg, a height or a leader, mirrored back, turned 30 degrees first; planes of normal (0.6, 0.8, 0),
      (0.8, 0.6, 0), (0.7, 0.71, 0), (0.3, 0.5, 0.8), (0.1, 0.6, 0.8), (0.6, 0.1, 0.8) and (0.1, 0.7, 0.7); words turned
      then mirrored across XY, XZ, (1, 0, 1) and (0.6, 0.1, 0.8), and turned a quarter by `TransformBy` then mirrored; a
      multileader of no content given one line from (30, 40), two, and words after; and 216 of the library's files of these,
      made with the fonts and with none, read back and given a leader, a gap, a scale, a move, a dogleg or a height.
    - AutoCAD 2021: the base point of words given is where a leader from the left would meet them held at their top left
      where they are given, the gap short and as far below as the left is met ((98, 48) for "ABC", (98, 47.33) for "Ag",
      (98, 41.33) for three lines met at their middle, (98, 44.67) for "Ag" at the underline of attachment 8); words held at
      the top centre or right have their left edge there ((105.33, 50) and (110.67, 50)), the context's alignment theirs and
      bits 14 and 26; given anew with no leader, the base point goes with them; a gap or a scale moves words of no leader
      to meet it again ((103, 50), (102, 52)), the left set to the bottom line too ((100, 52.8)), a height and a dogleg do
      not; given after their leaders, they meet the first ((10, 2) for a line landed at (0, 0), (229.33, 2) for one from
      (250, 0)). Adding a leader, AutoCAD ends its dogleg at the base point and moves the words to meet it. Words turned in
      code turn the plane (111 their direction) and are met in it, for any turn; given so where leaders are landed, the
      doglegs turn too and the words move to meet them. Of the two ways to read words mirrored, AutoCAD keeps the one turned,
      in the system of their normal, nearer the turn they had: across XZ "ABC" runs along x, met again by the mirrored leader
      at its attachment ((100, -46), (100, -50) at the top of the top line, (100, -44.67) for "Ag"), the plane and 294 kept;
      across XY against x; a quarter turn mirrored across XY up y still; as near both ways (a quarter turn either side), the
      way turned back but once. A block's plane turned back says so (294 of 1, then of nought); a line given BYBLOCK writes
      93 2 and no 92, and a line of no colour reads BYBLOCK; `TextAlignmentType` reads the context's 176; no 295 before
      R2013; a multileader of no content is of words (172 of 2), its first line landed at its point (one vertex), its
      dogleg towards the base point.
    - Now: the library puts the base point, holds words at the top centre and right, turns the plane with words turned where
      no leader is landed, keeps the way AutoCAD keeps of words mirrored, and makes the small readings as above. Of D38b
      round 1's 84 cases, 82 give AutoCAD's places, vertices, doglegs, base points, planes and groups (at base 31 by places and
      1 by base points); of round 3's 23, 22 (1); of D38's 120, 116 (104); with an empty path, where widths and
      depths are not measured, 42 of 84 (31). AutoCAD read 25 of the library's files and added a leader to each, made with
      the fonts: the words stayed in all 25, the leader landed where AutoCAD lands its own; made with an empty path, AutoCAD
      moved the words whose place needs a font (their depth, or their width held at the centre or right) to meet the base
      point.
    - Still apart: words turned in code given where leaders are landed already (r-anew180-L, r-rot180-o4-L), whose doglegs
      AutoCAD turns with the plane; words turned 135 degrees mirrored across XZ, as near both ways, where AutoCAD keeps the
      way the library does not; `AddLeader`'s leader of no landing, written at the base point where AutoCAD writes the
      origin; the multileader made without SetDatabaseDefaults (point 104); words mirrored where no font measures them,
      carried as ezdxf carries them, as before.

## The words measured in big fonts

106. **Big fonts read and measured beside a style's SHX font: which font each letter is drawn from, in which code page, at
    what height, and how TEXTs and MTEXTs of them are placed and boxed.**
    - Try (F3, headless, plugin GHF3A to GHF3F, fonts of AutoCAD 2021 read in place, none copied into the repository): 691
      letters of four code pages boxed by `TextStyle.ExtentsBox` beside each of the 13 big fonts of AutoCAD's Fonts folder
      (bigfont, chineset, extfont, extfont2, @extfont2, gbcbig, special, spec_bar, spec_sl and the four wh*.shx), and every
      letter of chineset and extfont2 drawn with a whole circle; TEXTs of 19 to 49 words (ASCII and CJK mixed, spaces before
      and after, `%%` codes, `\M+`, the ideographic space, `~` codes), 15 justifications, width factors 1 and 0.7 and slants
      0 and 15, after `AdjustAlignment`, and MTEXTs of one line, beside txt, romans, isocp, times.shx and arial.ttf; big
      fonts written for the check (headers of a height of nought, of five bytes, ranges of one byte or none, letters of one
      byte, shapes stretched into boxes, the pen moved before them), named as AutoCAD's own and by their folders, and copies
      of gbcbig.shx and extfont.shx under other names.
    - AutoCAD 2021: the code page of a big font is that of its name, AutoCAD's own ten CJK big fonts by name (gbcbig.shx
      GBK, chineset.shx Big5, bigfont, extfont, extfont2 and @extfont2.shx Shift-JIS, the wh*.shx Korean), in any case and
      without `.shx`; a big font named with its folder, even gbcbig.shx itself, or of another name, is read in none, its CJK
      letters drawn as question marks. A letter of two bytes the big font has is drawn from it before the style's font (Ж
      beside extfont.shx), whatever the ranges of the font (gbcbig.shx's 9DAE), but for °, ± and ∅; a letter the code page
      has not is the font's (¥ beside chineset.shx); a byte alone is the font's letter, else the big font's (ｱ beside
      extfont.shx); a space is the big font's where it has one (extfont.shx's 9 of 15, whgtxt.shx's 62 of 96); a byte the
      ranges open takes the next letter's byte with it (special.shx "~!" its 7E21, "~ " its 7E20, of none, a question mark);
      the ideographic space is a space where the big font has it not; a letter neither font has is the big font's question
      mark where it has one, else the font's. A big font's height above of nought is 8 (gbcbig.shx, 中 4 high 2.912 wide); a
      TEXT at its bottom stands on the big font's depth, whatever its letters (64 of 8, 32 up at 4 high). An extended big
      font's shape `7 0 hi lo x y width height` is drawn from the pen moved by (x, y), its cell stretched to the box, the pen
      put back across after it; a whole circle of a big font is not drawn (chineset.shx's 。 is its dot). The first letter of
      the style's font after letters of the big font, where it puts its pen down before it moves (txt's A and "."), is inked
      too where they began: "中A" of txt and gbcbig boxed from 0, " 中A" from 4. An MTEXT draws a letter neither font has, and a
      multibyte code, from other fonts, spaces an ideographic space otherwise, and leaves ink left of its start out. A
      TrueType style's big font is not drawn; times.shx beside gbcbig.shx inks "~!" a third of a unit higher than the
      library reads its letters.
    - Now: the library reads big fonts (the form `AutoCAD-86 bigfont 1.0`, its index read whole, not by its hash) and draws
      each letter as above. Of the 691 letters beside each of the ten CJK big fonts, all 6 910 are boxed as AutoCAD boxes
      them, and the 158 with whole circles (at base: none read). Of the TEXTs in styles with a big font, 15 382 measured give
      AutoCAD's place and box (F3: 10 226, at base 3 240; F1a's txt and gbcbig: 5 156, at base 3 692), and 68 do not: 64 that
      the font gives without a big font too (isocp.shx's stand-in, romans.shx's Ą, txt.shx's "i" aligned) and 4 of an
      ideographic space alone aligned on two points. Of F3's MTEXTs, 640 measured give AutoCAD's widths and heights and 1
      does not (romans.shx's Ą, as without a big font; at base 479 and 65). Left unmeasured: a big font of no code page
      beyond ASCII, a font of shapes beside a big font, special.shx's letters drawn from the style's font, `\M+` codes, and
      in an MTEXT a letter neither font has and the ideographic space.
    - Still apart: an ideographic space alone aligned or fitted on two points, which AutoCAD makes 1 high as it makes txt.shx's
      "i" (F1a); the code page AutoCAD reads a big font of another name in, which on this machine (code page 1252) draws every
      CJK letter as a question mark and takes ° as the byte B0, but not × as D7: not modelled.

## MTEXTs of several lines laid out in their fonts

107. **How AutoCAD 2021 lays out an MTEXT of several lines by its font: where its lines break, how far apart they stand,
    how wide and high it is, the box it fills, and the TEXTs it explodes to.**
    - Try (F4, headless, plugin GHF4A to GHF4E, fonts of AutoCAD 2021 and Windows read in place, none copied into the
      repository; each MTEXT 4 high, or 2.5, 3.5, 5 and 10, at (10, 20), read for `ActualWidth`, `ActualHeight`,
      `GeometricExtents`, `Explode` and `ExplodeFragments`): round A, 22 paragraphs and words (two paragraphs, empty ones,
      a pangram, double spaces, a word longer than the width, hyphens, spaces before and after, `\~`, digits, punctuation,
      %% codes, Chinese) at the widths 0, 15, 30, 41 and 70, line spacing factors 0.5 to 2.5 at least and exactly, the nine
      attachments, turned 30 degrees, in txt, romans, simplex, isocp and gdt.shx, arial and times.ttf and txt.shx beside
      gbcbig.shx; round B, "H H H H" at widths swept across the ink and the advance of its first three words, 0.02 apart, at
      2.5, 4 and 10 high, and paragraphs empty or of spaces; round C, first lines and lines after a wrap swept 0.01 apart,
      their first and last letters A, t, y and f and spaces before; round D, codes of height, width, spread, slant, hold,
      lines and colour, fonts, stacks and indents within the words, lines of other heights, 33 Chinese and Japanese signs
      before and after a break, kana beside extfont.shx and Hangul beside whgtxt.shx; round E, 900 MTEXTs of words and codes
      drawn at random, of every width, height, spacing, attachment and turn, as a check on rules fitted to the others.
    - AutoCAD 2021: a line breaks after the spaces between words, which stay at the end of the line they break, and before
      or after a Chinese, Japanese or Korean letter (kana and Hangul too), but not before ，。、；：？！．…・ー々, the
      closing brackets and the quotes “”‘’, nor after the opening brackets （「『【〔〈《, the letter next to such a sign going
      with it; not at a hyphen. A word moves down where the line would reach the width, measured from the start of the line
      to the pen at that word and the word's own ink, from its first ink, of a TrueType font from its pen at least: romans
      "H H t" 4 high, inked 17.524 wide, holds t from a width of 17.91, "H H H" from 19.05, its ink's 19.048; equal, the word
      moves down (txt "fox jumps over the" 70.000 at 70). A word longer than the width stays whole on a line of its own.
      Spaces before the first word are a word, a line of their own where the next word does not fit. Spaced at least, the
      first baseline stands the first line's height, or its ink's top where higher, below the top, and each line's top the
      factor times the top of the line before, a third of that line's height and a third of the MTEXT's height below the
      top of the line before (gdt "x\Py": 8.667; txt "\H2x;ABC\PDEF": 12; "ABC\P{\H3x;g}\PDEF": 14.667 and 9.333); the
      MTEXT as high as from the top to its last line's foot. Spaced exactly, the MTEXT's own height spaces the lines, and it
      is its height, the spacing and a third of its height high. An empty paragraph at the end is no line, a second is. The
      width is that of the widest line; the extents are the box of the width (or of that width) and height held by the
      attachment; each line stands on its own at the left, the centre or the right of the location, its ink at that edge,
      but a line wrapped where its letters' height changes starts at the edge unmoved. `Explode` gives a TEXT for each run
      of letters of one look (height, width, spread, slant, hold, lines, colour) of each line, held at its left where the
      pen stands at it, `%%U`, `%%O` and `%%K` written for its lines, a run of spaces between runs a TEXT of its own, the
      spaces at a line's end left out; letters held at the middle or top of taller ones stand up by half the difference or
      all of it, of the MTEXT's height where spaced exactly. A line under letters reaches a fifth of their height below the
      baseline, one over them a fifth above it; a spread lengthens every move of the pen after a letter, of a TrueType font
      otherwise ("\T2;ABC DEF" of Arial 41.228 wide). With %% codes on a wrapped line, the TEXTs AutoCAD explodes to do not
      follow its own lines (txt "%%c50 %%d %%p %%c50 %%c50 %%c50" 15 wide: five lines of fragments, two TEXTs).
    - Now: the library lays out an MTEXT so where `FontSearchPath` measures its words (`ActualWidth`, `ActualHeight`,
      `GeometricExtents`, `Explode` and a drawing of R12). Round A: 1 650 measured of 1 655, 1 626 of AutoCAD's sizes and
      extents and 1 604 every exploded TEXT too (at base: none laid out, 243 sizes of one line); round B 1 492 of 1 492;
      round C 9 550 of 9 550; round D 789 measured, all 789; round E 753 measured of 936, 747 sizes, 748 extents and 647
      every TEXT too. Of F1a's MTEXTs, 130 of 154 and 242 of 312 where 104 and 221 were; every other F1a, F1b, F2 and F3
      result as before.
    - Still apart: the TEXTs of a wrapped line with %% codes (103), which the library gives a line each; isocp.shx's
      heights (27, up to 0.01), as for a line; letters held at the middle of taller ones whose ink reaches above the line,
      which AutoCAD spaces lower; a line wrapped at a change of look other than height that AutoCAD moves; and lowercase
      letters whose line starts with a change of height (romans "g\P\H3x;g", the g set in by its bearing at 4 high, not 12).
      Not laid out: slanted letters (`\Q`, AutoCAD boxing them otherwise: txt "\Q12;A/B jig..." 27.758 wide, not 27.900),
      `\f`, `\S`, `\p`, columns, a TrueType font's spread and its changes of size after its first letter; nor a
      multileader's words of several lines, or a table's cells that wrap.
