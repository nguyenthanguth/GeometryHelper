using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Arranging;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Annotation
{
    /// <summary>
    /// Marks the parts, assemblies and bolt groups of a view: makes each mark's text from its object, places it through
    /// <see cref="Arranger"/> clear of the view's dimensions, of the other marks and, where it can be, of the view's lines,
    /// and draws its leader to a point on the object's visible outline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every object marked has at least one visible line in the view; an object hidden whole gets no mark, and an object
    /// gets one mark in a view, never two. A part that is the main part of an assembly gets an assembly mark, any other part
    /// a part mark, as <see cref="MarkSettings.MainParts"/> and <see cref="MarkSettings.Fittings"/> switch them on; the bolts
    /// of a group get one bolt mark, the group being the bolts of one attribute "BoltGroup", or where they have none, the
    /// bolts of one assembly of one diameter and length. The text is made of the fields <see cref="MarkContent"/> gives the
    /// kind, and an object whose text comes out empty is not marked.
    /// </para>
    /// <para>
    /// Each text is tried beside the longest visible straight line of its object, <see cref="MarkSettings.Offset"/> off it on
    /// paper, at the places <see cref="Arranger"/> lists; a group of bolts seen along their axes is tried beside the line
    /// through their centres. The dimensions' texts, lines and extension lines are kept clear of always, the view's lines
    /// where a place clear of them is found. The leader then runs from the nearest point of the text's box to the point of
    /// the object's visible lines nearest the middle of the text, or, for bolts seen along their axes with their rims seen
    /// whole, to the centre of the bolt nearest it.
    /// </para>
    /// <para>
    /// The marks are placed in four rounds, each taking only the marks the rounds before could not. The first tries both
    /// sides of the line, four rows deep, and keeps a mark only where it is clear of the view's lines and of the box of
    /// every object of the view, so that a text stands off the parts rather than over the face of one; the second tries the
    /// side of the line away from the object, three rows deep, and keeps a mark only where it is clear of the view's lines;
    /// the third tries both sides, eight rows deep, on the same terms; the fourth tries both sides sixteen rows deep, no
    /// more than half the text's height off the line, and keeps a mark over the view's lines where it must. In every
    /// round a mark is kept only where its text is clear of the dimensions and of the marks kept before, its leader crosses
    /// no text, and no leader kept before crosses its text. What the rounds leave is tried at the places of the last one,
    /// its text and margin clear of everything kept clear of; and what is left then, where marks crowd round it, once more
    /// with the lines of the symbols kept clear of by its text alone, as <see cref="FindOverlaps(IEnumerable{DrawingMark}, IEnumerable{DrawingDimension}, IEnumerable{GeoRectangle2}, IEnumerable{GeoLine2}, Tolerance)"/>
    /// counts them: in the general arrangement of a frame of 100 column bases at 1:200 this places 6 of the 15 bolt marks
    /// the crowd of the others left out. A mark that finds no such place is left out and named in one Debug line.
    /// </para>
    /// <para>
    /// The plate P1 of TC1, 300 by 200 with four holes, seen from above at 1:5 with its default dimensions above it and to
    /// its right, gets the part mark "P1" 17.5 high in the view under its lower side, the top of the text 20 below it, the
    /// offset of 4 at 1:5, and its leader running 20 straight up to that side. Each mark's text keeps 0.8 of its height
    /// clear of every other mark's and at least 0.4 of it clear of the dimensions.
    /// </para>
    /// </remarks>
    public static class Marker
    {
        /// <summary>
        /// Marks the objects of a view, placed clear of its dimensions and of one another.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from; those with no visible line in it are not marked.</param>
        /// <param name="frame">The frame the view was projected in.</param>
        /// <param name="dimensions">The dimensions of the view, as <see cref="Annotator.Dimension"/> gives them; none for a view
        /// with no dimensions.</param>
        /// <param name="settings">Which parts are marked, the height of the text and how far it stands off its part on paper.</param>
        /// <param name="content">What the text of each kind of mark shows.</param>
        /// <param name="scale">The scale the view is drawn at: 20 for 1:20.</param>
        /// <param name="tolerance">The tolerance points are told apart within, and overlaps measured to.</param>
        /// <returns>The marks, in the ordinal order of their ids.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view, the objects, the dimensions, the settings or the
        /// content are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is not a frame of three unit axes square to each other,
        /// an object or a dimension is null, or two objects have the same id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        public static IReadOnlyList<DrawingMark> Mark(
            ProjectedView2 view,
            IEnumerable<DrawingObject> objects,
            GeoCoordinateSystem3 frame,
            IEnumerable<DrawingDimension> dimensions,
            MarkSettings settings,
            MarkContent content,
            double scale,
            Tolerance tolerance
        ) =>
            Mark(
                view,
                objects,
                frame,
                dimensions,
                new GeoRectangle2[0],
                new GeoLine2[0],
                settings,
                content,
                scale,
                tolerance
            );

        /// <summary>
        /// Marks the objects of a view, placed clear of its dimensions, of boxes and lines given, as the texts and lines of
        /// the view's symbols, and of one another.
        /// </summary>
        /// <param name="view">The lines of the view, as <see cref="ViewProjector"/> gives them.</param>
        /// <param name="objects">The objects the view was projected from; those with no visible line in it are not marked.</param>
        /// <param name="frame">The frame the view was projected in.</param>
        /// <param name="dimensions">The dimensions of the view, as <see cref="Annotator.Dimension"/> gives them; none for a view
        /// with no dimensions.</param>
        /// <param name="clearOfBoxes">Boxes no text may overlap and no leader cross, in view millimetres: the letters of a
        /// section mark, a weld symbol; none for none.</param>
        /// <param name="clearOfLines">Lines no text may stand over, in view millimetres: centre lines, a section's cut line; a
        /// leader may cross them, as it may a dimension line; none for none.</param>
        /// <param name="settings">Which parts are marked, the height of the text and how far it stands off its part on paper.</param>
        /// <param name="content">What the text of each kind of mark shows.</param>
        /// <param name="scale">The scale the view is drawn at: 20 for 1:20.</param>
        /// <param name="tolerance">The tolerance points are told apart within, and overlaps measured to.</param>
        /// <returns>The marks, in the ordinal order of their ids.</returns>
        /// <remarks>
        /// The boxes and lines are kept clear of in every round as the dimensions are, so that a mark found no place clear of
        /// them is left out rather than laid over them: the mark P1 of the plate of TC1, seen on its face at 1:2 with the
        /// section A-A cut across its middle, stands beside the plate clear of the cut line and its letters.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the view, the objects, the dimensions, the boxes, the lines, the
        /// settings or the content are null.</exception>
        /// <exception cref="ArgumentException">Thrown when the frame is not a frame of three unit axes square to each other,
        /// an object or a dimension is null, or two objects have the same id.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        public static IReadOnlyList<DrawingMark> Mark(
            ProjectedView2 view,
            IEnumerable<DrawingObject> objects,
            GeoCoordinateSystem3 frame,
            IEnumerable<DrawingDimension> dimensions,
            IEnumerable<GeoRectangle2> clearOfBoxes,
            IEnumerable<GeoLine2> clearOfLines,
            MarkSettings settings,
            MarkContent content,
            double scale,
            Tolerance tolerance
        )
        {
            if (clearOfBoxes == null)
            {
                throw new ArgumentNullException(nameof(clearOfBoxes));
            }

            if (clearOfLines == null)
            {
                throw new ArgumentNullException(nameof(clearOfLines));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (!frame.IsValid)
            {
                throw new ArgumentException(
                    "The frame has to have three unit axes square to each other.",
                    nameof(frame)
                );
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(scale),
                    scale,
                    "The scale has to be a finite number above nought."
                );
            }

            List<DrawingObject> sorted = Sorted(objects);
            List<DrawingDimension> dims = Dimensions(dimensions);
            double tol = tolerance.EqualPoint;
            double height = settings.TextHeight * scale;
            var placer = new MarkPlacer(
                view,
                sorted,
                frame,
                dims,
                new List<GeoRectangle2>(clearOfBoxes),
                new List<GeoLine2>(clearOfLines),
                height,
                settings.Offset * scale,
                tolerance
            );
            List<MarkPlacer.Candidate> candidates = placer.Candidates(settings, content);
            List<DrawingMark> marks = placer.Place(candidates, scale, out List<string> dropped);

            if (dropped.Count > 0)
            {
                GeometryHelperLog.Debug(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Marker: {0} marks found no place clear of the dimensions and the other marks and were left out: {1}.",
                        dropped.Count,
                        string.Join(", ", dropped)
                    )
                );
            }

            GeometryHelperLog.Info(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "Marker: {0} objects gave {1} marks, {2} left out (tolerance {3}).",
                    sorted.Count,
                    marks.Count,
                    dropped.Count,
                    tol
                )
            );
            return marks;
        }

        /// <summary>
        /// Finds where marks overlap one another or the dimensions of their view: a text over another text, over a dimension's
        /// text, line, arrow or extension line; a leader across the text of another mark or of a dimension.
        /// </summary>
        /// <param name="marks">The marks of one view.</param>
        /// <param name="dimensions">The dimensions of the same view; none where it has none.</param>
        /// <param name="tolerance">The tolerance: boxes and lines meeting within it are not counted.</param>
        /// <returns>One line of words for each overlap, the marks in their order, each pair once; empty where none overlaps.</returns>
        /// <remarks>
        /// Each text is taken as its box, <see cref="DrawingMark.TextBox"/> and <see cref="DrawingDimension.TextBox"/>, and a
        /// dimension line shorter than two arrows as run on by an arrow, the text's height, at each end, as it is drawn with
        /// its arrows outside: the line of a 25 dimension at 1:20, its text 2.5 high on paper and so 50 in the view, is taken
        /// run on by 50 at each end. Each arrow is taken as the box round it, as long as the text is high and a third of that
        /// wide, inside its line or outside it as it is drawn: the arrows of that dimension are boxes 50 by 16.7. A leader
        /// across a dimension's line, arrow or extension line is not counted, as a draughtsman lets one cross; nor is a mark over
        /// the view's own lines, which <see cref="Mark(ProjectedView2, IEnumerable{DrawingObject}, GeoCoordinateSystem3, IEnumerable{DrawingDimension}, MarkSettings, MarkContent, double, Tolerance)"/> keeps clear of only where it can.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the marks or the dimensions are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a mark or a dimension is null.</exception>
        public static IReadOnlyList<string> FindOverlaps(
            IEnumerable<DrawingMark> marks,
            IEnumerable<DrawingDimension> dimensions,
            Tolerance tolerance
        )
        {
            if (marks == null)
            {
                throw new ArgumentNullException(nameof(marks));
            }

            var list = new List<DrawingMark>();

            foreach (DrawingMark mark in marks)
            {
                list.Add(mark ?? throw new ArgumentException("A mark cannot be null.", nameof(marks)));
            }

            List<DrawingDimension> dims = Dimensions(dimensions);
            double tol = tolerance.EqualPoint;
            var found = new List<string>();

            for (int i = 0; i < list.Count; i++)
            {
                DrawingMark a = list[i];
                DimensionClash(a.Id, a.TextBox, a.Leader[0], a.Anchor, dims, tol, found);

                for (int j = i + 1; j < list.Count; j++)
                {
                    MarkClash(
                        a.Id,
                        a.TextBox,
                        a.Leader[0],
                        a.Anchor,
                        list[j].Id,
                        list[j].TextBox,
                        list[j].Leader[0],
                        list[j].Anchor,
                        tol,
                        found
                    );
                }
            }

            return found;
        }

        /// <summary>
        /// Finds where marks overlap one another, the dimensions of their view, or boxes and lines kept clear of: a text over
        /// a box or a line, a leader across a box, as well as all <see cref="FindOverlaps(IEnumerable{DrawingMark}, IEnumerable{DrawingDimension}, Tolerance)"/>
        /// finds.
        /// </summary>
        /// <param name="marks">The marks of one view.</param>
        /// <param name="dimensions">The dimensions of the same view; none where it has none.</param>
        /// <param name="clearOfBoxes">The boxes the marks were to keep clear of, as given to <see cref="Mark(ProjectedView2, IEnumerable{DrawingObject}, GeoCoordinateSystem3, IEnumerable{DrawingDimension}, IEnumerable{GeoRectangle2}, IEnumerable{GeoLine2}, MarkSettings, MarkContent, double, Tolerance)"/>.</param>
        /// <param name="clearOfLines">The lines the marks were to keep clear of.</param>
        /// <param name="tolerance">The tolerance: boxes and lines meeting within it are not counted.</param>
        /// <returns>One line of words for each overlap, the marks in their order; empty where none overlaps.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the marks, the dimensions, the boxes or the lines are null.</exception>
        /// <exception cref="ArgumentException">Thrown when a mark or a dimension is null.</exception>
        public static IReadOnlyList<string> FindOverlaps(
            IEnumerable<DrawingMark> marks,
            IEnumerable<DrawingDimension> dimensions,
            IEnumerable<GeoRectangle2> clearOfBoxes,
            IEnumerable<GeoLine2> clearOfLines,
            Tolerance tolerance
        )
        {
            if (clearOfBoxes == null)
            {
                throw new ArgumentNullException(nameof(clearOfBoxes));
            }

            if (clearOfLines == null)
            {
                throw new ArgumentNullException(nameof(clearOfLines));
            }

            var found = new List<string>(FindOverlaps(marks, dimensions, tolerance));
            var boxes = new List<GeoRectangle2>(clearOfBoxes);
            var lines = new List<GeoLine2>(clearOfLines);

            foreach (DrawingMark mark in marks)
            {
                SymbolClash(
                    mark.Id,
                    mark.TextBox,
                    mark.Leader[0],
                    mark.Anchor,
                    boxes,
                    lines,
                    tolerance.EqualPoint,
                    found
                );
            }

            return found;
        }

        /// <summary>
        /// Adds a line for each way a mark meets the boxes and lines kept clear of: its text over a box or a line, its leader
        /// across a box; with no list, stops at the first and says whether there was one.
        /// </summary>
        internal static bool SymbolClash(
            string id,
            GeoRectangle2 text,
            GeoPoint2 a,
            GeoPoint2 b,
            List<GeoRectangle2> boxes,
            List<GeoLine2> lines,
            double tol,
            List<string> found
        )
        {
            bool any = false;

            for (int i = 0; i < boxes.Count; i++)
            {
                if (Box2.Overlap(text, boxes[i], tol) || Box2.Crosses(boxes[i], a, b, tol))
                {
                    any = true;

                    if (found == null)
                    {
                        return true;
                    }

                    found.Add(id + " over the symbol box " + i.ToString(CultureInfo.InvariantCulture));
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                if (Box2.Crosses(text, lines[i].StartPoint, lines[i].EndPoint, tol))
                {
                    any = true;

                    if (found == null)
                    {
                        return true;
                    }

                    found.Add(id + " text over the symbol line " + i.ToString(CultureInfo.InvariantCulture));
                }
            }

            return any;
        }

        /// <summary>
        /// Adds a line for each way a mark meets the dimensions: its text over a dimension's text, line, arrow or extension
        /// line, its leader across a dimension's text; with no list, stops at the first and says whether there was one. A
        /// dimension line shorter than two arrows is taken run on by an arrow at each end, as it is drawn with its arrows
        /// outside, and each arrow as the box round it.
        /// </summary>
        internal static bool DimensionClash(
            string id,
            GeoRectangle2 text,
            GeoPoint2 a,
            GeoPoint2 b,
            List<DrawingDimension> dims,
            double tol,
            List<string> found
        )
        {
            bool any = false;

            foreach (DrawingDimension d in dims)
            {
                if (Box2.Overlap(text, d.TextBox, tol))
                {
                    any = true;
                    found?.Add(id + " text over the text of dimension " + d.Id);
                }

                GeoLine2? line = d.DrawnLine;

                if (line.HasValue && Box2.Crosses(text, line.Value.StartPoint, line.Value.EndPoint, tol))
                {
                    any = true;
                    found?.Add(id + " text over the line of dimension " + d.Id);
                }

                foreach (GeoRectangle2 arrow in d.Arrowheads)
                {
                    if (Box2.Overlap(text, arrow, tol))
                    {
                        any = true;
                        found?.Add(id + " text over an arrow of dimension " + d.Id);
                        break;
                    }
                }

                foreach (GeoLine2 extension in d.ExtensionLines)
                {
                    if (Box2.Crosses(text, extension.StartPoint, extension.EndPoint, tol))
                    {
                        any = true;
                        found?.Add(id + " text over an extension line of dimension " + d.Id);
                        break;
                    }
                }

                if (Box2.Crosses(d.TextBox, a, b, tol))
                {
                    any = true;
                    found?.Add(id + " leader across the text of dimension " + d.Id);
                }

                if (any && found == null)
                {
                    return true;
                }
            }

            return any;
        }

        /// <summary>
        /// Adds a line for each way two marks meet: text over text, a leader across the other's text; with no list, says
        /// whether they meet.
        /// </summary>
        internal static bool MarkClash(
            string idA,
            GeoRectangle2 textA,
            GeoPoint2 a0,
            GeoPoint2 a1,
            string idB,
            GeoRectangle2 textB,
            GeoPoint2 b0,
            GeoPoint2 b1,
            double tol,
            List<string> found
        )
        {
            bool any = false;

            if (Box2.Overlap(textA, textB, tol))
            {
                any = true;
                found?.Add(idA + " text over the text of " + idB);
            }

            if (Box2.Crosses(textB, a0, a1, tol))
            {
                any = true;
                found?.Add(idA + " leader across the text of " + idB);
            }

            if (Box2.Crosses(textA, b0, b1, tol))
            {
                any = true;
                found?.Add(idB + " leader across the text of " + idA);
            }

            return any;
        }

        private static List<DrawingObject> Sorted(IEnumerable<DrawingObject> objects)
        {
            if (objects == null)
            {
                throw new ArgumentNullException(nameof(objects));
            }

            var sorted = new List<DrawingObject>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (DrawingObject item in objects)
            {
                if (item == null)
                {
                    throw new ArgumentException("An object of a view cannot be null.", nameof(objects));
                }

                if (!seen.Add(item.Id))
                {
                    throw new ArgumentException("The id \"" + item.Id + "\" is given to two objects.", nameof(objects));
                }

                sorted.Add(item);
            }

            // Ids are unique, so their order is a full order.
            sorted.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return sorted;
        }

        private static List<DrawingDimension> Dimensions(IEnumerable<DrawingDimension> dimensions)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            var list = new List<DrawingDimension>();

            foreach (DrawingDimension d in dimensions)
            {
                list.Add(d ?? throw new ArgumentException("A dimension cannot be null.", nameof(dimensions)));
            }

            return list;
        }
    }
}
