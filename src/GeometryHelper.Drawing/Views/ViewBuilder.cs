using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Drawing.Projection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing
{
    /// <summary>
    /// Makes the views a drawing's settings ask for (L2): the views along the axes and the isometric one, the sections and
    /// the details, each with its lines from L1; and makes a view again when its crop, direction or depth changes, the other
    /// views left as they are.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every view is set in the source's frame (<see cref="DrawingSource.Frame"/>), so a beam lying along any axis of the
    /// model gives the same views. A view sees only the objects whose box in it meets its crop: a crop of the end of a beam
    /// 3 000 long, from 2 800 to 3 100 across, leaves out a stiffener at 500. A view with no lines in it is not made.
    /// </para>
    /// <para>
    /// The lines are made by <see cref="ViewProjector.Project(IEnumerable{ProjectionInput}, GeoCoordinateSystem3, ProjectionOptions, Tolerance)"/>
    /// unless another projector is given, as a test counting the calls gives one: the three views of a part are three calls,
    /// and changing the crop of one of them is one more.
    /// </para>
    /// </remarks>
    public sealed class ViewBuilder
    {
        private readonly DrawingSource _source;
        private readonly DrawingSettings _settings;
        private readonly Tolerance _tolerance;
        private readonly Func<IEnumerable<ProjectionInput>, GeoCoordinateSystem3, ProjectionOptions, Tolerance, ProjectedView2> _projector;
        private readonly Dictionary<string, GeoPoint3[]> _corners = new Dictionary<string, GeoPoint3[]>(StringComparer.Ordinal);

        /// <summary>Makes a builder of the views of a source, its lines made by <see cref="ViewProjector"/>.</summary>
        /// <param name="source">The objects drawn.</param>
        /// <param name="settings">The settings; read as they are when a view is made.</param>
        /// <param name="tolerance">The tolerance the views are made within.</param>
        /// <exception cref="ArgumentNullException">Thrown when the source or the settings are null.</exception>
        public ViewBuilder(DrawingSource source, DrawingSettings settings, Tolerance tolerance)
            : this(source, settings, tolerance, ViewProjector.Project)
        {
        }

        /// <summary>Makes a builder of the views of a source, its lines made by a projector given.</summary>
        /// <param name="source">The objects drawn.</param>
        /// <param name="settings">The settings; read as they are when a view is made.</param>
        /// <param name="tolerance">The tolerance the views are made within.</param>
        /// <param name="projector">What makes the lines of a view: the objects, the view's coordinate system, the options and
        /// the tolerance in, the lines out, as <see cref="ViewProjector"/> makes them.</param>
        /// <exception cref="ArgumentNullException">Thrown when the source, the settings or the projector is null.</exception>
        public ViewBuilder(
            DrawingSource source,
            DrawingSettings settings,
            Tolerance tolerance,
            Func<IEnumerable<ProjectionInput>, GeoCoordinateSystem3, ProjectionOptions, Tolerance, ProjectedView2> projector)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _projector = projector ?? throw new ArgumentNullException(nameof(projector));
            _tolerance = tolerance;
        }

        /// <summary>
        /// Makes the views the settings ask for, in their order: the views along the axes and the isometric one, then the
        /// sections, then the details. Each is at the smallest scale allowed, 1:1 by default, until the drawing sets its scale.
        /// </summary>
        /// <returns>The views that have lines; a detail of a view not made is left out.</returns>
        public IReadOnlyList<DrawingView> Build()
        {
            double scale = _settings.SortedScales()[0];
            var views = new List<DrawingView>();
            var made = new HashSet<ViewKind>();

            foreach (ViewKind kind in _settings.Views.Kinds)
            {
                if (kind == ViewKind.Section || kind == ViewKind.Detail || kind == ViewKind.Free || !made.Add(kind))
                {
                    continue;
                }

                ViewFrame3 frame = StandardViews.GetFrame(_source.Frame, kind);
                DrawingView view = Make("V-" + kind.ToString().ToUpperInvariant(), kind, kind.ToString().ToUpperInvariant(), frame, null, null, null, scale);

                if (view != null)
                {
                    views.Add(view);
                }
            }

            foreach (SectionViewRequest request in _settings.Views.Sections)
            {
                DrawingView view = Section(request, scale);

                if (view != null)
                {
                    views.Add(view);
                }
            }

            foreach (DetailViewRequest request in _settings.Views.Details)
            {
                DrawingView parent = views.Find(v => v.Kind == request.Of);

                if (parent == null)
                {
                    GeometryHelperLog.Debug("ViewBuilder: the detail " + request.Name + " is of a " + request.Of + " view the drawing does not have; it is left out.");
                    continue;
                }

                DrawingView view = Make("V-DETAIL-" + Clean(request.Name), ViewKind.Detail, "DETAIL " + request.Name, parent.Frame, null, parent.Id, request.Area, scale);

                if (view != null)
                {
                    views.Add(view);
                }
            }

            GeometryHelperLog.Info(string.Format(CultureInfo.InvariantCulture, "ViewBuilder: {0} objects gave {1} views.", _source.Objects.Count, views.Count));
            return views;
        }

        /// <summary>
        /// Makes a view again with another crop: its lines made again from the objects its crop now meets.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="crop">The new crop, in view millimetres at 1:1.</param>
        /// <returns>The view with the new crop and lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the crop has no area.</exception>
        public DrawingView WithCrop(DrawingView view, GeoRectangle2 crop)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (!(crop.Width > 0.0) || !(crop.Height > 0.0))
            {
                throw new ArgumentException("A crop has a width and a height.", nameof(crop));
            }

            return Remake(view, view.Kind, view.Frame, crop);
        }

        /// <summary>
        /// Makes a view again looking in another direction: a free view from then on, its id kept, its crop the box of its new
        /// lines and the margin.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="frame">The new frame of the view.</param>
        /// <returns>The view with the new direction and lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view or the frame is null.</exception>
        public DrawingView WithDirection(DrawingView view, ViewFrame3 frame)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (frame == null)
            {
                throw new ArgumentNullException(nameof(frame));
            }

            return Remake(view, ViewKind.Free, frame, null);
        }

        /// <summary>
        /// Makes a view again with another depth: how far in front of its cut plane and behind it it sees.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="depthInFront">How far towards the viewer it sees; nought or more, or infinity.</param>
        /// <param name="depthBehind">How far away from the viewer it sees; nought or more, or infinity.</param>
        /// <returns>The view with the new depth and lines.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when a depth is negative or not a number.</exception>
        public DrawingView WithDepth(DrawingView view, double depthInFront, double depthBehind)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            GeoCoordinateSystem3 cs = view.Frame.CoordinateSystem;
            var frame = new ViewFrame3(cs.Origin, cs.ZAxis.Negate(), cs.YAxis, depthInFront, depthBehind, _tolerance);
            return Remake(view, view.Kind, frame, view.Crop);
        }

        /// <summary>
        /// Gives a view at another scale. Its lines are not made again: only how large it stands on the sheet changes.
        /// </summary>
        /// <param name="view">The view.</param>
        /// <param name="scale">The new scale: 20 for 1:20.</param>
        /// <returns>The view at the new scale, the same lines in it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale is not a finite number above nought.</exception>
        public DrawingView WithScale(DrawingView view, double scale)
        {
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "A scale has to be a finite number above nought.");
            }

            return view.With(view.Kind, view.Frame, view.Crop, scale, view.Lines, Copy(view.ObjectIds));
        }

        /// <summary>
        /// Chooses the scale of a view: the largest of the scales allowed, the smallest denominator, at which a box of the view
        /// fits a space on the sheet.
        /// </summary>
        /// <param name="box">The box of what the view draws, in view millimetres at 1:1.</param>
        /// <param name="width">The width of the space, in paper millimetres.</param>
        /// <param name="height">The height of the space, in paper millimetres.</param>
        /// <returns>The scale; the smallest allowed, the largest denominator, where none fits.</returns>
        /// <remarks>A box 3 200 by 400 in a space 380 by 250 fits at 1:10, the 320 by 40 of it, and not at 1:5.</remarks>
        public double ChooseScale(GeoRectangle2 box, double width, double height)
        {
            double[] scales = _settings.SortedScales();
            double tol = _tolerance.EqualPoint;

            foreach (double scale in scales)
            {
                if (box.Width / scale <= width + tol && box.Height / scale <= height + tol)
                {
                    return scale;
                }
            }

            return scales[scales.Length - 1];
        }

        private DrawingView Section(SectionViewRequest request, double scale)
        {
            GeoCoordinateSystem3 axes = StandardViews.GetFrame(_source.Frame, request.LookingAs).CoordinateSystem;
            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;

            foreach (DrawingObject item in _source.Objects)
            {
                foreach (GeoPoint3 corner in Corners(item))
                {
                    double z = axes.ToLocal(corner).Z;
                    low = Math.Min(low, z);
                    high = Math.Max(high, z);
                }
            }

            if (low > high)
            {
                return null;
            }

            double at = low + request.Share * (high - low);
            GeoPoint3 origin = axes.Origin.Add(axes.ZAxis.Multiply(at));
            var frame = new ViewFrame3(origin, axes.ZAxis.Negate(), axes.YAxis, 0.0, _settings.Views.SectionDepth, _tolerance);
            var plane = new GeoPlane3(origin, axes.ZAxis);
            return Make("V-SECTION-" + Clean(request.Name), ViewKind.Section, "SECTION " + request.Name + "-" + request.Name, frame, plane, null, null, scale);
        }

        private DrawingView Remake(DrawingView view, ViewKind kind, ViewFrame3 frame, GeoRectangle2? crop)
        {
            DrawingView made = Make(view.Id, kind, view.Name, frame, view.SectionPlane, view.ParentViewId, crop, view.Scale);

            if (made == null)
            {
                // A view that sees nothing keeps its frame and crop, with no lines.
                return view.With(kind, frame, crop ?? view.Crop, view.Scale, view.Lines, new string[0]);
            }

            return made;
        }

        /// <summary>Projects the objects a view's crop meets; null where the view has no lines.</summary>
        private DrawingView Make(string id, ViewKind kind, string name, ViewFrame3 frame, GeoPlane3? section, string parentId, GeoRectangle2? crop, double scale)
        {
            GeoCoordinateSystem3 cs = frame.CoordinateSystem;
            double tol = _tolerance.EqualPoint;
            var inputs = new List<ProjectionInput>();
            var ids = new List<string>();

            foreach (DrawingObject item in _source.Objects)
            {
                if (crop.HasValue && !Meets(item, cs, crop.Value, tol))
                {
                    continue;
                }

                inputs.Add(new ProjectionInput(item.Id, item.Solid));
                ids.Add(item.Id);
            }

            if (inputs.Count == 0)
            {
                return null;
            }

            double near = double.IsPositiveInfinity(frame.DepthInFront) ? double.PositiveInfinity : frame.DepthInFront;
            double far = double.IsPositiveInfinity(frame.DepthBehind) ? double.NegativeInfinity : -frame.DepthBehind;
            var options = new ProjectionOptions(_settings.Views.SmoothAngleRad, near, far, section);
            ProjectedView2 lines = _projector(inputs, cs, options, _tolerance);

            if (lines == null || lines.IsEmpty)
            {
                return null;
            }

            GeoRectangle2 box = crop ?? Grow(lines.Bounds, _settings.Views.CropMargin);
            return new DrawingView(id, kind, name, frame, section, parentId, box, scale, _settings.Views.HiddenLines, lines, ids.ToArray())
            {
                Box = box,
            };
        }

        /// <summary>Whether the box of an object in a view meets a crop.</summary>
        private bool Meets(DrawingObject item, GeoCoordinateSystem3 cs, GeoRectangle2 crop, double tol)
        {
            double x0 = double.PositiveInfinity, y0 = double.PositiveInfinity, x1 = double.NegativeInfinity, y1 = double.NegativeInfinity;

            foreach (GeoPoint3 corner in Corners(item))
            {
                GeoPoint3 p = cs.ToLocal(corner);
                x0 = Math.Min(x0, p.X);
                y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X);
                y1 = Math.Max(y1, p.Y);
            }

            double cx0 = crop.Center.X - crop.Width / 2.0, cx1 = crop.Center.X + crop.Width / 2.0;
            double cy0 = crop.Center.Y - crop.Height / 2.0, cy1 = crop.Center.Y + crop.Height / 2.0;
            return x0 <= cx1 + tol && x1 >= cx0 - tol && y0 <= cy1 + tol && y1 >= cy0 - tol;
        }

        /// <summary>The corners of an object's body, read once.</summary>
        private GeoPoint3[] Corners(DrawingObject item)
        {
            if (_corners.TryGetValue(item.Id, out GeoPoint3[] corners))
            {
                return corners;
            }

            var list = new List<GeoPoint3>();

            foreach (GeoFace3 face in item.Solid.Faces)
            {
                foreach (GeoPoint3 p in face.Boundary.Vertices)
                {
                    list.Add(p);
                }
            }

            corners = list.ToArray();
            _corners.Add(item.Id, corners);
            return corners;
        }

        internal static GeoRectangle2 Grow(GeoRectangle2 box, double margin)
            => new GeoRectangle2(box.Center, box.Width + 2.0 * margin, box.Height + 2.0 * margin);

        /// <summary>A name of a section or detail as part of an id: letters, digits, - and _ kept, others written as _.</summary>
        internal static string Clean(string name)
        {
            var chars = name.ToUpperInvariant().ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];

                if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }

        private static string[] Copy(IReadOnlyList<string> ids)
        {
            var copy = new string[ids.Count];

            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = ids[i];
            }

            return copy;
        }
    }
}
