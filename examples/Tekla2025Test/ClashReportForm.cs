using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using GeometryHelper.Clash;
using GeometryHelper.Geometry;
using GeometryHelper.IfcConvert.Models;
using GeometryHelper.TeklaConvert;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using AABB = Tekla.Structures.Geometry3d.AABB;
using LineSegment = Tekla.Structures.Geometry3d.LineSegment;

namespace Tekla2025Test
{
    /// <summary>
    /// The clashes a check found, in a window of their own: a row to a clash, with what the check measured
    /// (<see cref="ClashResult"/>), the reinforcement the bar belongs to and the IFC member it is with
    /// (<see cref="IfcProductGeometry"/>), under a summary of what was checked and how long each stage took.
    /// </summary>
    /// <remarks>
    /// Picking rows shows their clashes in the model: the outlines of each are inserted as control lines, which Tekla
    /// draws with their end points marked, the current view is zoomed to them, and they are selected, so that Tekla
    /// draws them highlighted. Picking other rows takes them out of the model again before showing those, and so does
    /// closing the window. The window leaves Tekla Structures free to use while it is open.
    /// </remarks>
    internal partial class ClashReportForm : Form
    {
        /// <summary>
        /// How far, in millimetres, the cross shown for a clash with no volume or face of its own reaches either way.
        /// </summary>
        private const double MarkerRadius = 25.0;

        /// <summary>
        /// How far, in millimetres, the model view zoomed to a clash reaches round it.
        /// </summary>
        private const double ZoomMargin = 500.0;

        /// <summary>
        /// How many faces of the volume two bodies share are outlined edge by edge; past that, the box round the volume
        /// is outlined instead, so that picking one row does not insert hundreds of control lines.
        /// </summary>
        private const int MostFacesOutlined = 64;

        /// <summary>
        /// How long, in millimetres, a line has to be to be shown: one that short shows nothing, and Tekla may refuse
        /// it.
        /// </summary>
        private const double ShortestLine = 0.01;

        private readonly IReadOnlyList<ClashResult> clashes;

        private readonly Model model = new Model();

        /// <summary>
        /// The control lines in the model now, and the clashes they show, by their places in the list.
        /// </summary>
        private readonly List<ControlLine> shown = new List<ControlLine>();
        private int[] shownClashes = new int[0];

        /// <summary>
        /// Lists the clashes a check found.
        /// </summary>
        /// <param name="title">The check that found them.</param>
        /// <param name="summary">What was checked and found, and how long each stage took.</param>
        /// <param name="clashes">
        /// The clashes, <see cref="ClashResult.First"/> indexing the bars and <see cref="ClashResult.Second"/> the IFC
        /// bodies, all in the current work plane.
        /// </param>
        /// <param name="barOwners">The reinforcement each bar belongs to, by the bar's index.</param>
        /// <param name="bodyOwners">The IFC product each body belongs to, by the body's index.</param>
        public ClashReportForm(string title, string summary, IReadOnlyList<ClashResult> clashes,
            IReadOnlyList<Reinforcement> barOwners, IReadOnlyList<IfcProductGeometry> bodyOwners)
        {
            InitializeComponent();

            this.clashes = clashes;
            Text = $"{title}: {clashes.Count} clash(es)";
            summaryTextBox.Text = summary;

            // A row to a clash, in the order found. The row keeps the clash's place in the list, since sorting by a
            // column moves the rows; a measure the clash does not have is left empty.
            for (int i = 0; i < clashes.Count; i++)
            {
                ClashResult clash = clashes[i];
                IfcProductGeometry member = bodyOwners[clash.Second];

                int row = clashGrid.Rows.Add(
                    i + 1,
                    clash.Kind.ToString(),
                    barOwners[clash.First].Identifier.ID,
                    member.IfcType,
                    member.Name,
                    member.Tag,
                    member.GlobalId,
                    Measured(clash.Volume),
                    Measured(clash.Depth),
                    Measured(clash.LengthInside),
                    Measured(clash.ContactArea),
                    Measured(clash.Distance),
                    string.Format(CultureInfo.InvariantCulture, "{0:0}, {1:0}, {2:0}", clash.Location.X, clash.Location.Y, clash.Location.Z),
                    clash.Error == null ? null : clash.Error.GetType().Name + ": " + clash.Error.Message);

                clashGrid.Rows[row].Tag = i;
            }

            statusLabel.Text = clashes.Count > 0
                ? "Pick a row to show its clash in the model; picking another, or closing this window, takes it out again."
                : "Nothing clashes.";
        }

        /// <summary>
        /// Sizes the columns to the rows in sight once the window is up, and only then starts showing what is picked:
        /// the row the grid selects on its own as it opens is no pick.
        /// </summary>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            clashGrid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            clashGrid.ClearSelection();
            clashGrid.SelectionChanged += clashGrid_SelectionChanged;
        }

        /// <summary>
        /// Takes what is shown out of the model as the window closes, leaving the model as the check found it.
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try
            {
                if (shown.Count > 0)
                {
                    RemoveShown();
                    model.CommitChanges();
                }
            }
            catch (Exception)
            {
                // Tekla Structures went first, and took the model with it.
            }

            base.OnFormClosed(e);
        }

        /// <summary>
        /// A measure to show, or nothing where the clash has none: a clash has a volume and a depth only where the two
        /// run into each other, a contact only where they touch, a distance only where they are too near.
        /// </summary>
        private static object Measured(double value) => value > 0.0 ? (object)value : null;

        /// <summary>
        /// Shows the clashes of the rows picked, in place of what was shown; sorting, which keeps the same rows picked,
        /// shows nothing new.
        /// </summary>
        private void clashGrid_SelectionChanged(object sender, EventArgs e)
        {
            int[] picked = clashGrid.SelectedRows.Cast<DataGridViewRow>().Select(row => (int)row.Tag).OrderBy(i => i).ToArray();

            if (!picked.SequenceEqual(shownClashes))
            {
                ShowInModel(picked);
            }
        }

        /// <summary>
        /// Zooms to what is shown, and selects it, again: after panning away, or after picking something else in Tekla.
        /// </summary>
        private void clashGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && shown.Count > 0)
            {
                statusLabel.Text = ZoomAndSelect() ? "Zoomed to what is shown again." : "Tekla did not zoom: open a model view and try again.";
            }
        }

        /// <summary>
        /// Picks every row, which shows every clash.
        /// </summary>
        private void showAllButton_Click(object sender, EventArgs e) => clashGrid.SelectAll();

        /// <summary>
        /// Picks no row, which takes everything shown out of the model.
        /// </summary>
        private void removeButton_Click(object sender, EventArgs e)
        {
            clashGrid.ClearSelection();

            // With no row picked already, nothing changed in the grid to take them out.
            if (shown.Count > 0)
            {
                ShowInModel(new int[0]);
            }
        }

        /// <summary>
        /// Shows clashes in the model: takes out the control lines shown before, inserts the outlines of these, zooms
        /// the current model view to them, and selects them, which Tekla draws highlighted.
        /// </summary>
        /// <param name="which">The clashes to show, by their places in the list; none takes everything out.</param>
        private void ShowInModel(int[] which)
        {
            Stopwatch showing = Stopwatch.StartNew();

            try
            {
                int removed = RemoveShown();
                int refused = 0;
                shownClashes = which;

                // An edge two faces share, or two clashes, is inserted once.
                var inserted = new HashSet<((long, long, long), (long, long, long))>();

                foreach (int i in which)
                {
                    ClashResult clash = clashes[i];

                    foreach ((GeoPoint3 from, GeoPoint3 to) in LinesOf(clash))
                    {
                        if (inserted.Add(Key(from, to)) && !Insert(from, to, ColorOf(clash.Kind)))
                        {
                            refused++;
                        }
                    }
                }

                if (removed > 0 || shown.Count > 0)
                {
                    model.CommitChanges();
                }

                if (which.Length == 0)
                {
                    statusLabel.Text = $"Took {removed} control line(s) out of the model.";
                    return;
                }

                bool zoomed = ZoomAndSelect();
                statusLabel.Text = $"{which.Length} clash(es) shown by {shown.Count} control line(s) in {showing.ElapsedMilliseconds} ms" +
                    (refused > 0 ? $"; Tekla refused {refused}" : string.Empty) +
                    (zoomed ? "." : "; open a model view to zoom to them.");
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Showing in the model failed: " + ex.Message;
            }
        }

        /// <summary>
        /// Takes the control lines shown out of the model; the caller commits.
        /// </summary>
        /// <returns>How many were taken out.</returns>
        private int RemoveShown()
        {
            int removed = 0;

            foreach (ControlLine line in shown)
            {
                if (line.Delete())
                {
                    removed++;
                }
            }

            shown.Clear();
            shownClashes = new int[0];
            return removed;
        }

        /// <summary>
        /// Inserts a line into the model as a control line, and keeps it to take out again. It is not magnetic: a
        /// magnetic line carries the handles of the objects on it along when it moves.
        /// </summary>
        private bool Insert(GeoPoint3 from, GeoPoint3 to, ControlLine.ControlLineColorEnum color)
        {
            var line = new ControlLine(new LineSegment(from.ToTeklaPoint(), to.ToTeklaPoint()), false)
            {
                Color = color,
                Extension = 0.0,
            };

            if (!line.Insert())
            {
                return false;
            }

            shown.Add(line);
            return true;
        }

        /// <summary>
        /// Zooms the current model view to what is shown, and selects the control lines showing it.
        /// </summary>
        /// <returns>Whether Tekla zoomed.</returns>
        private bool ZoomAndSelect()
        {
            // The clashes are in the current work plane, as is the box a model view is zoomed to.
            GeoAabb3 box = GeoAabb3.FromPoints(shownClashes.SelectMany(i => Extent(clashes[i]))).Expand(ZoomMargin);
            bool zoomed = ViewHandler.ZoomToBoundingBox(new AABB(box.Min.ToTeklaPoint(), box.Max.ToTeklaPoint()));

            // Tekla.Structures.Model has a ModelObjectSelector too, hence the full name.
            new Tekla.Structures.Model.UI.ModelObjectSelector().Select(new ArrayList(shown));
            return zoomed;
        }

        /// <summary>
        /// The points a clash spans: where it is, and the volume, contact or gap it has.
        /// </summary>
        private static IEnumerable<GeoPoint3> Extent(ClashResult clash)
        {
            yield return clash.Location;

            foreach (GeoSolid3 overlap in clash.Overlaps)
            {
                GeoAabb3 box = overlap.GetAabb();
                yield return box.Min;
                yield return box.Max;
            }

            foreach (GeoFace3 patch in clash.Contact)
            {
                foreach (GeoPoint3 corner in patch.Boundary.Vertices)
                {
                    yield return corner;
                }
            }

            if (clash.Gap.HasValue)
            {
                yield return clash.Gap.Value.StartPoint;
                yield return clash.Gap.Value.EndPoint;
            }
        }

        /// <summary>
        /// The lines a clash is shown by: the edges of the volume two bodies share, or of the patches they touch by;
        /// else a cross where the clash is, with the segment across the gap of a pair too near, from the bar's surface
        /// to the part. A bar checked by its centre line has no volume or face of its own, and is shown by a cross.
        /// </summary>
        private static IEnumerable<(GeoPoint3 From, GeoPoint3 To)> LinesOf(ClashResult clash)
        {
            if (clash.Overlaps.Count > 0)
            {
                // The volume the two share, face by face, or by its box when it has too many faces. A clash taken as
                // touching for being too shallow or too small keeps its volume, and is shown by it too.
                if (clash.Overlaps.Sum(overlap => overlap.Faces.Count) <= MostFacesOutlined)
                {
                    return clash.Overlaps.SelectMany(overlap => overlap.Faces).SelectMany(face => Edges(face.Boundary.Vertices));
                }

                IEnumerable<GeoPoint3> corners = clash.Overlaps.SelectMany(overlap => new[] { overlap.GetAabb().Min, overlap.GetAabb().Max });
                return BoxEdges(GeoAabb3.FromPoints(corners));
            }

            if (clash.Contact.Count > 0)
            {
                // Face to face: the patches they lie against each other by.
                return clash.Contact.SelectMany(patch => Edges(patch.Boundary.Vertices));
            }

            // Along an edge or at a corner, by a centre line, too near, or not checked at all: a cross where it is.
            var lines = new List<(GeoPoint3 From, GeoPoint3 To)>(Cross(clash.Location));

            if (clash.Gap.HasValue && clash.Gap.Value.Length > ShortestLine)
            {
                // Apart but too near: the segment across the gap, its ends marked where the gap is measured.
                lines.Add((clash.Gap.Value.StartPoint, clash.Gap.Value.EndPoint));
            }

            return lines;
        }

        /// <summary>
        /// The edges round a face, the last back to the first corner, leaving out any shorter than the shortest line.
        /// </summary>
        private static IEnumerable<(GeoPoint3 From, GeoPoint3 To)> Edges(IReadOnlyList<GeoPoint3> corners)
        {
            for (int i = 0; i < corners.Count; i++)
            {
                GeoPoint3 from = corners[i];
                GeoPoint3 to = corners[(i + 1) % corners.Count];

                if (from.DistanceTo(to) > ShortestLine)
                {
                    yield return (from, to);
                }
            }
        }

        /// <summary>
        /// The twelve edges of a box, four along each axis; an edge of no length is left out.
        /// </summary>
        private static IEnumerable<(GeoPoint3 From, GeoPoint3 To)> BoxEdges(GeoAabb3 box)
        {
            GeoPoint3 Corner(int x, int y, int z)
                => new GeoPoint3(x == 0 ? box.Min.X : box.Max.X, y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z);

            var edges = new List<(GeoPoint3 From, GeoPoint3 To)>();

            for (int a = 0; a < 2; a++)
            {
                for (int b = 0; b < 2; b++)
                {
                    edges.Add((Corner(0, a, b), Corner(1, a, b)));
                    edges.Add((Corner(a, 0, b), Corner(a, 1, b)));
                    edges.Add((Corner(a, b, 0), Corner(a, b, 1)));
                }
            }

            return edges.Where(edge => edge.From.DistanceTo(edge.To) > ShortestLine);
        }

        /// <summary>
        /// A cross where a clash is: a line along each axis, reaching the marker's radius either way.
        /// </summary>
        private static (GeoPoint3 From, GeoPoint3 To)[] Cross(GeoPoint3 at)
        {
            return new[]
            {
                (new GeoPoint3(at.X - MarkerRadius, at.Y, at.Z), new GeoPoint3(at.X + MarkerRadius, at.Y, at.Z)),
                (new GeoPoint3(at.X, at.Y - MarkerRadius, at.Z), new GeoPoint3(at.X, at.Y + MarkerRadius, at.Z)),
                (new GeoPoint3(at.X, at.Y, at.Z - MarkerRadius), new GeoPoint3(at.X, at.Y, at.Z + MarkerRadius)),
            };
        }

        /// <summary>
        /// The same line whichever way it runs, to a thousandth of a millimetre.
        /// </summary>
        private static ((long, long, long), (long, long, long)) Key(GeoPoint3 from, GeoPoint3 to)
        {
            (long, long, long) a = Rounded(from);
            (long, long, long) b = Rounded(to);
            return a.CompareTo(b) <= 0 ? (a, b) : (b, a);
        }

        private static (long, long, long) Rounded(GeoPoint3 point)
            => ((long)Math.Round(point.X * 1000.0), (long)Math.Round(point.Y * 1000.0), (long)Math.Round(point.Z * 1000.0));

        /// <summary>
        /// The colour a kind of clash is shown in: hard red, touching yellow, too near orange, unresolved magenta.
        /// </summary>
        private static ControlLine.ControlLineColorEnum ColorOf(ClashKind kind)
        {
            switch (kind)
            {
                case ClashKind.Hard:
                    return ControlLine.ControlLineColorEnum.RED;
                case ClashKind.Touch:
                    return ControlLine.ControlLineColorEnum.YELLOW;
                case ClashKind.Clearance:
                    return ControlLine.ControlLineColorEnum.YELLOW_RED;
                default:
                    return ControlLine.ControlLineColorEnum.MAGENTA;
            }
        }
    }
}
