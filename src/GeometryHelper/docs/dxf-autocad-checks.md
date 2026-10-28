# Checking against AutoCAD

The editing model of [Shared types](common.md#editing-a-dxf-file) was held against ezdxf 1.4.5, which recovered and audited
every file it wrote with nothing to fix and read back every value set, and against the members AutoCAD's .NET API declares.
What neither can say is what AutoCAD itself makes of a few of the files: the points below were decided from the DXF
reference, ezdxf and the files AutoCAD writes, and are for the maintainer to try in AutoCAD. Each says what to try, what the
library does now, and what result would mean the library has to change. Open each file written with AUDIT run on it as well;
an error AUDIT reports or fixes in a file the library wrote is a change to make whatever the point.

## Entities and copies

1. **A SOLID given a new normal.**
   - Try: make a SOLID of the square of side 2 at the origin, set its `Normal` to (0, 0, -1), save, and open it in AutoCAD.
   - Now: the corners keep their places in the world, each written anew in the system of the new normal (the corner (2, 0, 0)
     written as (-2, 0, 0)), as a polyline's are.
   - A change if: AutoCAD draws the SOLID elsewhere than its corners stood; the corners should then keep their coordinates
     in the file's system as the normal changes, as AutoCAD keeps them.

2. **Extended data handles on a copy into another drawing.**
   - Try: give a LINE extended data with a handle (1005) between other groups, copy it into another drawing with WBLOCK or
     COPYCLIP and PASTECLIP, and list its extended data.
   - Now: the library leaves the 1005 out of the copy, so the groups after it move up one place.
   - A change if: AutoCAD keeps the 1005 with a null or mapped handle, so that the data keeps its positions; then the library
     should write it so too.

3. **An associative HATCH copied into another drawing.**
   - Try: open a drawing the library wrote with the `Clone()` of an associative hatch appended to another drawing.
   - Now: the copy leaves its boundary objects behind, each path counting the pointers it keeps (97 of 0), and is no longer
     associative (71 of 0), as AutoCAD's COPY leaves a hatch copied alone.
   - A change if: AUDIT reports the hatch, or AutoCAD expects something else of a hatch copied without its boundary.

4. **The values of a table's block cells copied across.**
   - Try: copy an ACAD_TABLE whose block cells give their blocks' attributes values into another drawing with the library,
     and open it.
   - Now: where the cell's block comes with the copy, the values are kept, each pointer to a definition (331) naming the copy
     of that definition; where the drawing has a block of the name of its own, each pointer is left out with its value (300)
     and the count 179 made to agree.
   - A change if: AutoCAD keeps the values in the second case too, against the drawing's own block's definitions of the same
     tags.

5. **Two entities that point at each other, copied together.**
   - Try: copy a leader with its MTEXT, and a hatch with its four boundary lines, into another drawing with AutoCAD's
     `WblockCloneObjects`, and compare with the library's `WblockCloneObjects` of the same, and with a group of them.
   - Now: what one points to among the others points to its copy: the leader to the MTEXT's copy, the hatch associative to
     the lines' copies, each listed among the reactors of what it points to; a group all of whose entities are copied into
     model space is copied under its name, or the name and `_1` where the drawing has it. Copied alone, a leader points to
     nothing (`NoAnnotation`) and a hatch is no longer associative.
   - A change if: AutoCAD names the copied group otherwise, or keeps a leader copied alone pointing at something.

## Text

6. **The words of an attribute of several lines.**
   - Try: make an attribute of several lines in AutoCAD, save it as DXF, and read its group 1 and AutoCAD's `TextString`.
   - Now: `MTextAttribute` and `IsMTextAttribute` write the first line of the words as the attribute's own text (group 1), as
     ezdxf's `set_mtext` does; the MTEXT embedded in it holds them all. ezdxf notes that AutoCAD writes every line there,
     joined by `\P`.
   - A change if: AutoCAD writes the lines joined (as ezdxf says it does); then group 1 should hold them so.

7. **An MTEXT's background transparency.**
   - Try: read `BackgroundTransparency` of an MTEXT with no group 441.
   - Now: BYLAYER.
   - A change if: AutoCAD gives another default.

8. **An MTEXT's frame.**
   - Try: open in AutoCAD an MTEXT of R2004 and one of R2007 with `ShowBorders` set.
   - Now: the library sets the frame (flag 16 of group 90) from R2004 on; ezdxf from R2007.
   - A change if: AutoCAD 2004 to 2006 does not read the flag; then the library should refuse it before R2007.

9. **An MTEXT set in columns.**
   - Try: open in AutoCAD the MTEXTs set by `SetStaticColumns`, `SetDynamicColumns` and `SetColumnHeight` in a drawing of R2018
     (embedded object) and of R2000 (`ACAD_MTEXT_COLUMN_INFO` and `ACAD_MTEXT_DEFINED_HEIGHT` extended data), and look at the
     columns and their words.
   - Now: the columns are written as ezdxf writes them, and the MTEXT's own width (group 41, `Width`, and 40 of the embedded
     object) is made the width of a column: the note 120 wide set in two columns of 50 with a gutter of 10 has 41 of 50.
   - A change if: AutoCAD shows the columns otherwise, or keeps another width in 41.

10. **Linked columns copied into R2018.**
    - Try: copy an MTEXT of linked columns of R2000 into a drawing of R2018 with the library, and open it.
    - Now: the copy holds the words of all the columns, in the embedded form of R2018.
    - A change if: AutoCAD shows the columns otherwise than they were.

## Curves

11. **The side an offset goes to.**
    - Try: `GetOffsetCurves(5)` of a line from (0, 0, 0) to (30, 40, 0), of a counter-clockwise polyline square and of a spline,
      in AutoCAD's API.
    - Now: every curve offsets to the right of its way seen from the tip of its normal, where an arc's radius grows: the line runs
      from (4, -3, 0), the square grows.
    - A change if: AutoCAD's line or polyline goes the other way for a positive distance.

12. **An arc and a circle offset with the normal turned over.**
    - Try: `GetOffsetCurvesGivenPlaneNormal` of an arc with its normal negated, distance 5.
    - Now: the radius grows by 5 whichever way the normal points.
    - A change if: AutoCAD shrinks it.

13. **Spline fitting of a polyline.**
    - Try: `SplineFit` of a 2D and a 3D polyline at SPLINESEGS 8 and SPLINETYPE 5 and 6, and on a polyline of fewer vertices
      than the degree asks for; count the vertices made.
    - Now: SPLINESEGS segments to each knot span, the degree dropped where the vertices are too few.
    - A change if: AutoCAD makes another count.

14. **Curve fitting and surface fitting.**
    - Try: `CurveFit` of a polyline and `SurfaceFit` of a polygon mesh at SURFU 6 and SURFV 6, and count the vertices.
    - Now: a pair of arcs on each segment through every vertex; SURFU by SURFV points of the surface.
    - A change if: AutoCAD's counts or the points differ.

15. **The spline of a spline-fitted polyline.**
    - Try: `Polyline2d.Spline` of a polyline fitted by `SplineFit`.
    - Now: the spline of degree 1 through the vertices drawn.
    - A change if: AutoCAD gives the B-spline of the frame.

16. **Control points put in and taken out.**
    - Try: `InsertControlPointAt` and `RemoveControlPointAt` on a cubic, and compare the control points.
    - Now: a knot put in or taken out with the point, the rest of the curve kept where it can be.
    - A change if: AutoCAD places the new point elsewhere.

17. **A spline made a polyline.**
    - Try: `ToPolylineWithPrecision` at 0, 10, 30 and 99 on the cubic on (0, 0), (10, 10), (20, 10), (30, 0).
    - Now: within a thousandth of the spline's size at 0 and a tenth of that for each 10 more, never finer than a millionth of
      its size, judged at three points of each segment: 65 vertices at 10 and 513 from 30 up.
    - A change if: AutoCAD reads the precision another way.

18. **The narrowest ellipse.**
    - Try: squash an ellipse to a ratio of 1E-7 by `TransformBy`, and project a circle onto a plane seen almost edge on.
    - Now: `TransformBy` writes a ratio of 1E-6 at the least, the DXF reference's floor, and a projection whose minor axis comes
      within 1E-6 of its major one's length of nothing is refused as a line.
    - A change if: AutoCAD keeps a smaller ratio.

19. **A 3DFACE's hidden edges in an intersection.**
    - Try: `IntersectWith` of a line and a 3DFACE whose crossed edge is invisible.
    - Now: the face meets along all four of its edges, its hidden ones among them, as its shape has no hidden edges.
    - A change if: AutoCAD leaves the hidden edges out.

20. **A text in an intersection.**
    - Try: `IntersectWith` of a line and a TEXT or an MTEXT.
    - Now: refused with `NotSupportedException`.
    - A change if: AutoCAD crosses the line with the box of the text; the library would need the size of the words, which it
      cannot have without fonts.

## Objects and tables

21. **The rotation of a linetype's shape.**
    - Try: a complex linetype whose text is turned by 30 degrees, its group 50 read in AutoCAD.
    - Now: the file holds degrees (30), as ezdxf writes them; `ShapeRotationAt` gives radians.
    - A change if: AutoCAD writes radians in group 50.

22. **The current transparency.**
    - Try: set CETRANSPARENCY to the alpha 100 through `Cetransparency` and read it in AutoCAD.
    - Now: written as the nearest whole per cent, so the alpha 100 reads back as 99.
    - A change if: AutoCAD rounds otherwise.

23. **Binary data under a key.**
    - Try: `SetBinaryDataForKey` of 300 bytes, then AutoCAD's `GetBinaryDataForKey` of the same key.
    - Now: an XRECORD of the key in the extension dictionary, its bytes in groups 310 of 127 bytes each.
    - A change if: AutoCAD does not read them back.

24. **A hyperlink to a place in a drawing.**
    - Try: a hyperlink with a `SubLocation`, read in AutoCAD's HYPERLINK dialog.
    - Now: written in the extended data of PE_URL as AutoCAD writes it.
    - A change if: AutoCAD shows another name, description or place.

25. **A block's preview.**
    - Try: set `PreviewIcon` of a block and look at it in AutoCAD's INSERT dialog.
    - Now: its bytes in the BLOCK_RECORD's 310 groups, after its other groups.
    - A change if: AutoCAD wants them in another place of the record.

26. **The drawing's properties.**
    - Try: set `SummaryInfo` with a hyperlink base and two custom properties in a drawing of R2004 or later and of R2000, and
      open DWGPROPS in AutoCAD; save from AutoCAD as DXF and see whether it writes DWGPROPS or `$CUSTOMPROPERTYTAG` in the
      header.
    - Now: the DWGPROPS record, the hyperlink base as its second text, the custom properties in the list after group 90 from
      R2004 and in groups 300 to 309 before; a record that has both is written in the form it has.
    - A change if: AutoCAD shows them otherwise, writes the header form, or reads one form where a record has both.

27. **Empty groups.**
    - Try: in AutoCAD, empty a group by erasing its last member, by removing it, and make one with no member; save.
    - Now: each emptied group is dropped when the drawing is saved; a group the file was read with empty is kept.
    - A change if: AutoCAD keeps an emptied group in one of those cases.

28. **An anonymous group renamed.**
    - Try: rename an anonymous group in AutoCAD.
    - Now: given a name, it is anonymous no longer (group 70 of 0).
    - A change if: AutoCAD keeps it anonymous.

29. **Records of R2018 copied into R2000.**
    - Try: open in AutoCAD 2000 a drawing of R2000 into which the library copied a DIMSTYLE and a layer from a drawing of R2018.
    - Now: the groups R2000 has not are left out, as AutoCAD's SAVEAS to R2000 leaves them out, said in the log at Debug: a
      DIMSTYLE's 49, 50, 69, its second 70, 290, 294 and 345 to 347, and a LAYER's 420 and 430 (R2004) and 347 and 348
      (R2007), the versions as ezdxf gives them; so are the overrides of a dimension style (ACAD DSTYLE) of a variable R2000
      has not, as DIMLTYPE and DIMFXLEN.
    - A change if: AutoCAD 2000 or AUDIT still reports the records, or AutoCAD's SAVEAS keeps a group the library leaves out.

30. **A dimension copied into another drawing.**
    - Try: copy a dimension with the library into a drawing that has none of its text style or arrow blocks, and open it.
    - Now: the text style, the arrow blocks and the dimension style come with it by name.
    - A change if: AutoCAD shows another text style or other arrows than in the drawing it came from.

## Viewports and layouts

31. **A viewport's flags.**
    - Try: set `FrontClipAtEyeOn`, `GridBoundToLimits` and `GridSubdivisionRestricted` on a viewport, and look at it in
      AutoCAD; set an isometric snap.
    - Now: the three flags AutoCAD keeps inverted (0x10, 0x40000, 0x100000) are written inverted; the isometric pair as ezdxf
      writes it.
    - A change if: AutoCAD shows any of them the other way.

32. **A viewport's light and number.**
    - Try: read the ambient light colour of a viewport with no group 63, and the `Number` of a viewport turned off.
    - Now: the colour index 250; the number from group 69 as written.
    - A change if: AutoCAD gives another default, or another number for one that is off.

33. **A view of R12, R13 or R14.**
    - Try: set the view of a viewport in a drawing of R12 (its extended data MVIEW) and of R14, and open them.
    - Now: written as the DXF reference has it; ezdxf reads no MVIEW, so only the library read them back.
    - A change if: AutoCAD shows another view.

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
    - A change if: AutoCAD follows the style's change, so that the bit is wrong.

41. **A new multileader and one of a block.**
    - Try: open a `new DxfMLeader()` with words and one with a block placed at its base point.
    - Now: written as ezdxf writes them.
    - A change if: AutoCAD reports or fixes either; or draws the block of a multileader from its group 15 and not from its matrix
      47, or the other way.

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
    - A change if: AutoCAD reads a cell's overrides otherwise.

44. **The arrow block of a dimension style.**
    - Try: set `Dimtxsty` and `Dimblk` on a dimension style read from a file and on one made in code, open the drawing and run
      AUDIT.
    - Now: from R2000 the text style is written by handle in group 340 and the arrows in 341 to 344, at their places in the
      record.
    - A change if: AUDIT fixes a handle of the record, or AutoCAD shows another style or arrow.

45. **The defaults of a dimension style of inches.**
    - Try: read a variable a DIMSTYLE of a drawing in inches gives no group for.
    - Now: AutoCAD's default of acad.dwt, the template in inches, whatever the drawing's units: arrowheads 0.18 long, four
      decimal places.
    - A change if: AutoCAD takes those of acadiso.dwt for a drawing in millimetres; the defaults should then follow `$MEASUREMENT`.

46. **The angle of a jog.**
    - Try: a jogged radius dimension's `JogAngle` and a style's `Dimjogang` of 60 degrees.
    - Now: the dimension's group 40 in radians; the style's group 50 in degrees, read as radians.
    - A change if: AutoCAD writes either in the other unit.

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
    - A change if: AutoCAD keeps something the library leaves out, or breaks a fitted polyline otherwise.

49. **An MTEXT exploded, and saved as R12.**
    - Try: EXPLODE in AutoCAD an MTEXT of three paragraphs, one wrapped at its width, held at its middle centre, and SAVEAS R12
      a drawing holding it; compare with `Explode()` and with the MTEXT appended to a drawing of R12.
    - Now: one TEXT for each paragraph, not wrapped, its formatting and the heights of its codes left out, the lines spaced as
      `LineSpaceDistance` says and held at the left, the middle or the right as the MTEXT is held; two lines 2.5 high held at
      the top left at (10, 20) stand on the baselines 17.5 and 13.33 high.
    - A change if: AutoCAD's baselines or alignments differ for the lines it does not wrap.

50. **A dimension and a table exploded.**
    - Try: EXPLODE in AutoCAD a dimension in a tilted plane and a table, each drawn last by AutoCAD, and compare with
      `Explode()`.
    - Now: the entities of the `*D` or `*T` block placed where the dimension or the table draws them, as ezdxf's
      `virtual_entities()` places a dimension's; the picture drawn last, not the data set since.
    - A change if: AutoCAD's parts stand elsewhere, or AutoCAD draws the picture anew from the data before it explodes it.

51. **A MINSERT exploded.**
    - Try: `Explode()` in AutoCAD's API of a MINSERT of 2 rows by 3 columns, and EXPLODE of it.
    - Now: `Explode()` gives the block in every cell, six circles for a block of one; `ExplodeToOwnerSpace` refuses a MINSERT
      of more than one cell, as AutoCAD's EXPLODE refuses one.
    - A change if: AutoCAD's `Explode()` gives the first cell alone, or its EXPLODE takes a MINSERT.

52. **A leader exploded.**
    - Try: EXPLODE in AutoCAD a straight leader with the closed filled arrow, one with an arrow block (DIMLDRBLK) and one with a
      hook line, and compare with `Explode()`.
    - Now: a LINE between each two vertices, then the arrowhead at the first vertex, `Dimasz` times `Dimscale` long: an INSERT
      of the arrow block turned and scaled so, or a SOLID a third as wide as long; no hook line. A splined leader gives one
      SPLINE through its vertices, fitted as ezdxf fits it: of degree 3 by the chord, its inner knots the parameters of the inner
      vertices, leaving along the first segment and reaching along the last at the speed of the length of the chain.
    - A change if: AutoCAD's arrow is of another size or shape, a hook line comes out of it, or its spline has other control
      points or keeps its fit points.

53. **A named block brought by a copy.**
    - Try: copy an INSERT of a block that inserts another block into a drawing that has neither, with WBLOCK or COPYCLIP and
      PASTECLIP, and compare its BLOCK and BLOCK_RECORD with the library's copy by `Clone()` and `AppendEntity`.
    - Now: the block comes under its name, with the blocks it inserts that the drawing has not and its description, units,
      explodability and scaling; a drawing with a block of the name places its own.
    - A change if: AutoCAD brings something else of the block, or renames it where the drawing has one of the name.

54. **A table scaled and mirrored.**
    - Try: SCALE by 2 and MIRROR in AutoCAD a table of rows 10, 8 and 8 high, with a cell of its own text height and a block
      cell, and compare with `TransformBy` of the same.
    - Now: scaled, its rows 20, 16 and 16 high, its column widths, cell margins and the text heights and block scales it and its
      cells override doubled; mirrored, its normal turned round and its cells standing where the mirror puts them.
    - A change if: AutoCAD keeps the text heights, or mirrors a table some other way.

55. **Arrow blocks of a dimension saved as R12.**
    - Try: SAVEAS R12 in AutoCAD a dimension whose DIMBLK is overridden, and compare its ACAD extended data with the library's
      copy of it into a drawing of R12.
    - Now: the override names the block by its name (a 1070 of 5, 6 or 7, then a 1000) where the drawing has a block of the name,
      and is left out, said in the log, where it has none.
    - A change if: AutoCAD writes the arrow otherwise in R12.

56. **The overall viewport of a layout.**
    - Try: in AutoCAD, try to erase the viewport 1 of a layout, and erase the last active viewport of a layout; save as DXF.
    - Now: the viewport 1 is refused; a layout whose last active viewport (331) is erased no longer names it.
    - A change if: AutoCAD writes another viewport in the LAYOUT's 331 after the erase.

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

59. **An MLINE exploded.**
    - Try: EXPLODE in AutoCAD an MLINE of a style of six elements with inner arcs at both ends, one with its caps suppressed,
      and one cut by MLEDIT (Cut Single); compare with `Explode()`.
    - Now: inner arcs join every inner pair, the second and the fifth, the third and the fourth, where ezdxf joins the first
      pair alone; the suppressed caps are left out; the cut element gives a LINE for each piece, its parameters after the first
      read as a start and a stop by turns from the point the element stands at; the fill is left out.
    - A change if: AutoCAD joins one pair, draws suppressed caps, measures a cut from elsewhere, or gives the fill a part.

60. **A HATCH exploded.**
    - Try: EXPLODE in AutoCAD a HATCH of DOTS, one of ANSI31 round a circle with an island in it in the outer style, a solid
      fill and a gradient; compare with `Explode()`.
    - Now: the pattern's dashes as LINEs cut by the loops its style fills, a dot a LINE of no length; the solid fill and the
      gradient refused.
    - A change if: AutoCAD makes a dot a POINT, cuts by other loops in the outer style, or breaks a solid fill or a gradient
      into parts the library can make.

61. **A multileader exploded.**
    - Try: EXPLODE in AutoCAD a multileader of words with two leaders on the left, one splined, one of a block with an
      attribute, and one whose words are underlined (attachment 5 or 6); compare with `Explode()`.
    - Now: the MTEXT or the INSERT with its attribute, then for each leader line its arrow, a SOLID for the closed filled arrow
      or an INSERT of the arrow block, where the first segment is more than twice the arrow, and the LINEs from the back of the
      arrow to the end of the dogleg, or one SPLINE; no underline.
    - A change if: AutoCAD makes the closed filled arrow a block, starts the line at the tip, or draws an underline.

62. **A leader exploded in the look of its dimension style.**
    - Try: EXPLODE in AutoCAD a leader of colour 3 whose style has DIMCLRD BYBLOCK, one with DIMCLRD 1 and DIMLWD 0.35 as
      overrides, and one of DIMCLRD BYBLOCK whose record writes a 77; compare with `Explode()`.
    - Now: the overrides' colour and lineweight on the lines and the arrow, the true colour taken off; DIMCLRD BYBLOCK the
      leader's own colour, or the colour of its 77 where it has one, as ezdxf reads it (ezdxf gives 7 where there is none).
    - A change if: AutoCAD gives the parts of a BYBLOCK style BYBLOCK, or colour 7, or keeps the leader's true colour.

63. **A splined leader as a curve.**
    - Try: in AutoCAD's API, `GetPointAtParameter`, `EndParam`, `GetDistanceAtParameter(EndParam)` and `GeometricExtents` of a
      splined leader through (0, 0, 0), (10, 10, 0) and (20, 10, 0); compare with the library's.
    - Now: the spline ezdxf fits, its parameter from 0 to 1, its box (0, 0, 0) to (20, 10.43, 0), the arrowhead not counted.
    - A change if: AutoCAD's parameter runs over the chord lengths, or its box takes in the arrowhead.

64. **A MESH exploded.**
    - Try: EXPLODE in AutoCAD a MESH of triangles and quads, one with a face of five corners (a mesh made by MESHSMOOTH of a
      solid's flat face), and one with a face given a colour of its own; compare with `Explode()`.
    - Now: a 3DFACE for each face of three or four corners, a triangle ending on its third, every edge drawn; the face of five
      corners and the face of its own colour refused.
    - A change if: AutoCAD hides the edges inside a face, or breaks a face of five corners in a way the library can follow
      (a fan from its first corner), which would let it take one.

## Geographic location, layer filters and layer states

65. **The GEODATA AutoCAD writes.**
    - Try: in AutoCAD 2018 or later, GEOGRAPHICLOCATION on a drawing in millimetres, with three points of the mesh, saved as
      DXF of R2018; read the GEODATA record.
    - Now: the library writes a GEODATA of version 2 (group 90), as ezdxf's new one, its groups in the order 90, 330, 70, 10
      to 31, 40, 91, 41, 92, 210 to 230, 12, 22, 95, 141, 294, 142, 143, 303, 301, 302, 305 to 307, 93, then each point of
      the mesh with the point it is taken to, interleaved (13, 23, 14, 24 for each), then the faces (96 to 99).
    - A change if: AutoCAD writes version 3, another order, or every point of the drawing before every point taken to; the
      library should then write so, and read a version 3 by what it holds.

66. **The class GEODATA.**
    - Try: in the same file, read the CLASS whose DXF name is GEODATA.
    - Now: the library lists it, where the file lists none, as ezdxf does: ObjectDBX Classes, proxy flags (90) 4095, the
      count of objects (91) 0, not a proxy and not an entity.
    - A change if: AutoCAD writes other proxy flags, or AUDIT reports a count of 0 with a GEODATA in the file.

67. **The entry ACAD_GEOGRAPHICDATA and the host of the GEODATA.**
    - Try: in the same file, read model space's extension dictionary and the GEODATA's group 330 after its version.
    - Now: the library names the GEODATA by a 360, an owning pointer, and writes model space's BLOCK_RECORD handle as its
      host; ezdxf writes a 350 and a host of 0, and the library reads both.
    - A change if: AutoCAD writes a 350, or a host other than model space's handle; the library should then write it so.

68. **A new GeoLocationData, and one in paper space.**
    - Try: in AutoCAD's API, read every property of `new GeoLocationData()`; then set its `BlockTableRecordId` to paper
      space's, `PostToDb()`, save and open the file.
    - Now: a `DxfGeoData` made in code is ezdxf's new one: geographic, its two points at the origin, inches of scale 1, up
      along Z, north along Y, no scale estimated, no coordinate system and no mesh. `PostToDb` takes any block, paper space
      among them; `db.GeoDataObject` reads model space's only.
    - A change if: AutoCAD's new one differs, which the library's should then follow; or AutoCAD refuses a GEODATA in paper
      space, or drops it on opening, when `PostToDb` should refuse every block but model space.

69. **Where AutoCAD 2018 keeps layer filters.**
    - Try: in AutoCAD 2018 or later, make a property filter and a group filter, save as DXF of R2018, and read the layer
      table's extension dictionary; in AutoCAD's API, list `db.LayerFilters.Root.NestedFilters`.
    - Now: the library reads the names of the entries of ACAD_LAYERFILTERS, the dictionary of AutoCAD 2004 and earlier, and
      nests them in All; AcLyDictionary is not read, and no "All Used Layers" is made.
    - A change if: the two filters are only in AcLyDictionary, which the library should then read; or `NestedFilters` holds
      "All Used Layers", which it should then give, made by AutoCAD like All.

70. **The record of a layer state, and its hidden and xref states.**
    - Try: save a layer state, one marked hidden, and one of an attached xref; save as DXF of R2018 and read the XRECORDs of
      ACAD_LAYERSTATES; in AutoCAD's API compare `GetLayerStateNames(false, false)` with `GetLayerStateNames(true, true)`.
    - Now: the library lists every entry of ACAD_LAYERSTATES; `GetLayerStateNames()` takes no flags, since where a state
      keeps whether it is hidden or of an xref is not known.
    - A change if: the two lists differ, as they should; the groups that mark a state hidden or of an xref then let the
      library take the flags, and the layout of the record its layers, mask and description.

71. **A GEODATA with no class.**
    - Try: open in AutoCAD a DXF of R2010 or later with no CLASSES section, written by a program that leaves it out, given a
      GEODATA by `PostToDb()`.
    - Now: the GEODATA is written and no class for it, since there is no CLASSES section to list it in.
    - A change if: AutoCAD refuses the file, drops the GEODATA, or AUDIT reports it; `PostToDb` should then write a CLASSES
      section, or refuse such a file.

72. **The angle of the north direction.**
    - Try: in AutoCAD's API, read `NorthDirection` of a GeoLocationData whose `NorthDirectionVector` is (0, 1), (1, 0) and
      (-1, 0) by turns.
    - Now: `NorthDirection` is not given; `NorthDirectionVector` is read and set as groups 12 and 22.
    - A change if: the three angles show from which axis and which way AutoCAD measures it; the library can then give
      `NorthDirection` from the vector.

## Data links of tables

The library reads the data links of a table and takes them away, and writes no new one until the points below are settled.

73. **Where a table's cell names its data link.**
    - Try: link the cells (1, 0) to (2, 1) of a table to a spreadsheet with DATALINK, save as DXF of R2007 and of R2018, and
      read the cells of the ACAD_TABLE.
    - Now: the range's top left cell is read as naming its link after its 92, made odd: `92 1`, `340` the DATALINK's handle,
      `93 2`, `94 2`, `96 0`, before `301 CELL_VALUE`; a block cell names none, its 340 being its block. That run is the one
      LibreDWG gives a cell of a TABLECONTENT, whose 92 says it has linked data; on a cell of an ACAD_TABLE it is a guess, and
      what the other bits of that 92 mean is not known.
    - A change if: AutoCAD names the link elsewhere (in a TABLECONTENT, in the table's own part, in other groups) or gives the
      92 other bits; the reading then follows AutoCAD, and the writing is made on it.

74. **The groups of a DATALINK.**
    - Try: read the DATALINK of the drawing of point 73; then open in AutoCAD, with AUDIT, a drawing whose DATALINK has no 70
      and a count of nought, `94 0`, followed by `305 CUSTOMDATA`, `1 DATAMAP_BEGIN` and `309 DATAMAP_END`.
    - Now: read as LibreDWG gives AcDbDataLink: 1, 300, 301, 302, 90, 91, 92, 170 to 176, 177, 93, 304, then the count 94
      of custom data, its pairs of a 330 and a 304 between the markers, and the 360; LibreDWG gives no 70.
    - A change if: AutoCAD writes a 70 or another group, the pairs after the 94 are not a 330 of an object the link owns and
      a 304, or AUDIT fixes a link of `94 0`.

75. **The class of a DATALINK.**
    - Try: read the CLASS of DATALINK in the CLASSES section of the drawing of point 73.
    - Now: links are read whatever the CLASSES section lists, and no class is added, as no link is written; the writing held
      back listed DATALINK, AcDbDataLink, ObjectDBX Classes, of the flags 1153.
    - A change if: AutoCAD's flags or names differ; the writing takes AutoCAD's.

76. **What a linked table keeps of its link.**
    - Try: in the drawing of point 73, look in the ACAD_TABLE for a reactor or a pointer to the DATALINK other than the
      cell's 340, and in the DATALINK for the table's handle.
    - Now: the cell's 340 alone is read and taken away; the DATALINK is counted as let go of when no record of the file points
      at it, its dictionary aside.
    - A change if: AutoCAD keeps a reactor or a pointer either way; taking a link away then takes it out too.

77. **A link detached.**
    - Try: detach the link of a table in AutoCAD, once for a link named in the Data Link Manager and once for one whose
      `DataLinkOption` is Anonymous, and save.
    - Now: the cell's groups of the link taken out and its 92 made even; the DATALINK erased, with what it owns, only where it
      is anonymous and nothing else of the file points at it; a named one kept in ACAD_DATALINK.
    - A change if: AutoCAD erases a named link too, or keeps an anonymous one no table names.

78. **A formula in a linked cell.**
    - Try: give the top left cell of a linked range a formula in AutoCAD, save, and read the cell's groups.
    - Now: `SetFormula` writes the cell's 344 after the link's run: `92 1`, `340`, `93`, `94`, `96 0`, then `344`.
    - A change if: AutoCAD writes the 344 before the 340, or keeps no formula in a linked cell.

## Offsets of polylines that cross themselves

79. **The corner at a crossing of a closed polyline offset.**
    - Try: `GetOffsetCurves(1)` in AutoCAD's API of the closed polyline (-10, 1), (10, -1), (10, 1), (-10, -1), a bow tie
      crossing itself at the origin, of the same bow tie 0.02 high at its ends, and of the square (0, 0), (6, 0.06),
      (4, 0.06), (10, 0), (10, 10), (0, 10), whose bottom curls into a loop; note the vertices beside each crossing.
    - Now: the crossing is no corner. The two lobes' offsets are joined where they cross, the bow tie's at (0, ±1.004988),
      and the thin one's at (0, ±1); its tips are not run on, which with the unlimited miter `GetOffsetCurves` uses
      would reach x = ±1 000. The square grows to the square from (-1, -1.01005) to (11, 11), its bottom rising from
      -1.01005 at the corners to (5, -0.95005). `Offset2.Offset` of a `GeoPolygonArc2` grown with miters or chamfers miters or cuts each tip of two
      lobes as a corner, as the straight path does.
    - A change if: AutoCAD runs the two tips at a bow tie's waist on as corners; the DXF offset then joins them as
      `Offset2.Offset` does, with AutoCAD's limit, if it has one.
