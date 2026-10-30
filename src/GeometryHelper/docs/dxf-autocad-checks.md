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
    - Still open: no file AutoCAD saved holds a MULTILEADER yet, so the landing is left as it is. ezdxf 1.4.5's builder
      (`render/mleader.py`, `_build_leader`) lands a leader at the connection point of the side of the content it comes to,
      the landing gap within it, less the dogleg: `connection_point - dogleg_direction * dogleg_length`, the dogleg pointing
      at the content. To settle it, save from AutoCAD a multileader it made and this point's file opened and moved, and read
      each leader's 10 against the content's.

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
      the library has not; it still gives a TEXT a paragraph.

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
keeps them (points 73 to 78); a table of no TABLECONTENT is not linked, as the library would have to write one whole.

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
