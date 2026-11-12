using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Model space worked in through a viewport of a layout, as AutoCAD's MSPACE: the layout is still the one shown, but
    /// picks, snaps and commands work in model space as the viewport shows it, and zooming changes the viewport's view.
    /// </summary>
    public sealed partial class CadDocument
    {
        /// <summary>Raised when model space is entered through a viewport, another viewport is made the one worked through, or the paper is worked on again.</summary>
        public event EventHandler ActiveViewportChanged;

        /// <summary>
        /// Gets the viewport of the layout shown whose model space is worked in (MSPACE), as AutoCAD's current viewport; null
        /// on the paper (PSPACE), in model space, and where that viewport has been erased or switched off since.
        /// </summary>
        public DxfViewport ActiveViewport => WorkedThrough(CurrentSpace, CurrentSpace?.Through);

        /// <summary>
        /// Gets the space edits go into: model space while a viewport is worked through (<see cref="ActiveViewport"/>), else the
        /// space shown (<see cref="CurrentSpace"/>). A line drawn in a viewport of Layout1 goes into model space.
        /// </summary>
        public CadSpace WorkingSpace => ActiveViewport != null ? ModelSpace : CurrentSpace;

        /// <summary>
        /// Works in model space through a viewport of the layout shown, as a double click inside it does in AutoCAD: the
        /// selection is let go, and <see cref="ActiveViewportChanged"/> raised where it was not already the one.
        /// </summary>
        /// <param name="handle">The handle of the viewport.</param>
        /// <returns>False, nothing changed, where the layout shown has no such viewport, or the viewport cannot be worked
        /// through: the sheet's own, one switched off, or one whose view is not a plan, which the canvas cannot show the cursor
        /// in. A plan twisted by DVIEW TWist is worked through turned, as AutoCAD works through it.</returns>
        public bool EnterViewport(string handle)
        {
            CadSpace space = CurrentSpace;
            DxfViewport viewport = WorkedThrough(space, handle);

            if (viewport == null)
            {
                return false;
            }

            if (!string.Equals(space.Through, viewport.Handle, StringComparison.OrdinalIgnoreCase))
            {
                space.Through = viewport.Handle;
                space.LastThrough = viewport.Handle;
                Selection.Clear();
                ActiveViewportChanged?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        /// <summary>Works on the paper of the layout shown again, as PSPACE: the selection let go; nothing where no viewport is worked through.</summary>
        public void LeaveViewport()
        {
            CadSpace space = CurrentSpace;

            if (space == null || space.Through == null)
            {
                return;
            }

            space.Through = null;
            Selection.Clear();
            ActiveViewportChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the viewports of the layout shown that can be worked through, in the order the layout holds them, as Ctrl+R
        /// cycles them; empty in model space.
        /// </summary>
        public IReadOnlyList<DxfViewport> EnterableViewports
        {
            get
            {
                var found = new List<DxfViewport>();
                CadSpace space = CurrentSpace;

                if (space == null || space.IsModel)
                {
                    return found;
                }

                foreach (DxfEntity entity in space.Block)
                {
                    if (entity is DxfViewport viewport && !viewport.IsErased && CadViewports.CanWorkThrough(viewport))
                    {
                        found.Add(viewport);
                    }
                }

                return found;
            }
        }

        /// <summary>
        /// Works through the next viewport of the layout shown, in the order of <see cref="EnterableViewports"/>, the first after
        /// the last, as Ctrl+R does in AutoCAD.
        /// </summary>
        /// <returns>False, nothing changed, on the paper or in model space.</returns>
        public bool NextViewport()
        {
            DxfViewport active = ActiveViewport;

            if (active == null)
            {
                return false;
            }

            IReadOnlyList<DxfViewport> all = EnterableViewports;
            int at = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (string.Equals(all[i].Handle, active.Handle, StringComparison.OrdinalIgnoreCase))
                {
                    at = i;
                }
            }

            return EnterViewport(all[(at + 1) % all.Count].Handle);
        }

        /// <summary>
        /// The viewport MSPACE enters: the one last worked through where it still can be, else the first of the layout's.
        /// </summary>
        internal DxfViewport ViewportToEnter()
        {
            CadSpace space = CurrentSpace;
            DxfViewport last = WorkedThrough(space, space?.LastThrough);

            if (last != null)
            {
                return last;
            }

            IReadOnlyList<DxfViewport> all = EnterableViewports;
            return all.Count > 0 ? all[0] : null;
        }

        /// <summary>
        /// Sets the view a viewport shows of model space, one undo step: the point at the middle of its frame and the height it
        /// shows, so its scale is its height over that one. A viewport 150 high given a view 3 000 high shows model space at
        /// 1:20. The view it showed goes on the viewport's ZOOM Previous list, 20 at most.
        /// </summary>
        /// <param name="handle">The handle of the viewport.</param>
        /// <param name="viewCenter">The point of model space at the middle of the frame, as the viewport keeps it
        /// (<see cref="DxfViewport.ViewCenter"/>, about the view's target and turned by its twist; see <see cref="CadViewports.ToView"/>).</param>
        /// <param name="viewHeight">The height of model space shown, above nought.</param>
        /// <param name="name">The name of the undo step, as ZOOM or PAN.</param>
        /// <returns>What came of it: refused for no such viewport, a height not above nought, or a viewport whose display is locked.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadEditResult SetViewportView(string handle, GeoPoint2 viewCenter, double viewHeight, string name) =>
            SetViewportView(handle, viewCenter, viewHeight, name, false);

        /// <summary>
        /// Sets the view a viewport shows of model space, as <see cref="SetViewportView(string, GeoPoint2, double, string)"/>; a
        /// real-time zoom or pan, the wheel's or the middle button's, made straight after another joins its undo step, as
        /// AutoCAD 2021 combines consecutive real-time zooms and pans (UNDO Control Combine, on by default: UNDOCTL 61 has
        /// its 16). ZOOM and PAN typed stay a step each, as AutoCAD's console undoes ZOOM 2X, ZOOM 2X and -PAN one U each.
        /// </summary>
        /// <param name="handle">The handle of the viewport.</param>
        /// <param name="viewCenter">The view's centre, as the viewport keeps it.</param>
        /// <param name="viewHeight">The height of model space shown, above nought.</param>
        /// <param name="name">The name of the undo step, as ZOOM or PAN; a step joined keeps the name of the first.</param>
        /// <param name="realTime">Whether the change is a real-time zoom or pan, which joins the step of the real-time one
        /// before it where no other step, a save or an undo came between.</param>
        /// <returns>What came of it, refused as the other overload refuses.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the name is null.</exception>
        public CadEditResult SetViewportView(
            string handle,
            GeoPoint2 viewCenter,
            double viewHeight,
            string name,
            bool realTime
        )
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string refused = ViewRefused(handle, viewCenter, viewHeight);

            if (refused != null)
            {
                return CadEditResult.Refuse(refused);
            }

            var was = (DxfViewport)FindEntity(handle);
            GeoPoint2 wasCenter = was.ViewCenter;
            double wasHeight = was.ViewHeight;
            _realTime = realTime;

            try
            {
                CadEditResult result = ApplyEdit(
                    name,
                    ViewRecord(handle, viewCenter, viewHeight),
                    ViewWriter(handle, viewCenter, viewHeight)
                );

                // The view it showed goes on the viewport's own ZOOM Previous list, one entry for each wheel gesture or pan.
                if (result.Succeeded && !SameView(wasCenter, wasHeight, viewCenter, viewHeight))
                {
                    KeepViewportView(result, handle, wasCenter, wasHeight);
                }

                return result;
            }
            finally
            {
                _realTime = false;
            }
        }

        /// <summary>Whether a viewport's view is where it was, within a hundred-thousandth of its height.</summary>
        private static bool SameView(GeoPoint2 center, double height, GeoPoint2 otherCenter, double otherHeight)
        {
            double tolerance = Tolerance.Default.EqualVector * height;
            return Math.Abs(center.X - otherCenter.X) <= tolerance
                && Math.Abs(center.Y - otherCenter.Y) <= tolerance
                && Math.Abs(height - otherHeight) <= tolerance;
        }

        /// <summary>Why a view cannot be given to a viewport; null where it can.</summary>
        internal string ViewRefused(string handle, GeoPoint2 viewCenter, double viewHeight)
        {
            if (!(FindEntity(handle) is DxfViewport viewport))
            {
                return "The drawing has no viewport " + handle + ".";
            }

            if (!viewCenter.IsValid || !(viewHeight > 0.0) || double.IsInfinity(viewHeight))
            {
                return "A view needs a centre of finite numbers and a height above nought.";
            }

            return viewport.Locked ? "The viewport's display is locked." : null;
        }

        /// <summary>The record of a viewport given a view: its centre and height before and after.</summary>
        internal IReadOnlyList<CadEditRecord> ViewRecord(string handle, GeoPoint2 viewCenter, double viewHeight)
        {
            var viewport = (DxfViewport)FindEntity(handle);
            string was = CadCommands.Values(viewport.ViewCenter.X, viewport.ViewCenter.Y, viewport.ViewHeight);
            return new[]
            {
                new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    handle,
                    "View",
                    was,
                    CadCommands.Values(viewCenter.X, viewCenter.Y, viewHeight)
                ),
            };
        }

        /// <summary>The writer of a viewport's view, found through the scope.</summary>
        internal static Action<CadEditScope> ViewWriter(string handle, GeoPoint2 viewCenter, double viewHeight) =>
            s =>
            {
                if (s.IsHandled(handle))
                {
                    return;
                }

                var viewport = (DxfViewport)s.Document.FindEntity(handle);
                viewport.ViewCenter = viewCenter;
                viewport.ViewHeight = viewHeight;
            };

        /// <summary>The viewport of a handle a layout can be worked in through; null for none.</summary>
        private DxfViewport WorkedThrough(CadSpace space, string handle)
        {
            if (space == null || space.IsModel || string.IsNullOrEmpty(handle))
            {
                return null;
            }

            return
                FindEntity(handle) is DxfViewport viewport
                && ReferenceEquals(FindSpace(handle), space)
                && CadViewports.CanWorkThrough(viewport)
                ? viewport
                : null;
        }
    }

    /// <summary>
    /// The arithmetic of model space seen through a viewport of a layout: the view of the canvas in model units, the view a
    /// viewport takes from it back, a box of model space fitted to the frame, and the scales a viewport is set to.
    /// </summary>
    public static class CadViewports
    {
        private static readonly string[] Scales = BuildScales();

        /// <summary>
        /// Gets the standard scales a viewport is set to, as the status bar and the Properties' Standard scale offer them: 1:1,
        /// 1:2, 1:4, 1:5, 1:8, 1:10, 1:16, 1:20, 1:30, 1:40, 1:50, 1:100, 2:1, 4:1, 8:1, 10:1 and 100:1.
        /// </summary>
        public static IReadOnlyList<string> StandardScales => Scales;

        /// <summary>
        /// Gets whether a viewport's model space can be worked in through it: on and not the sheet's own, with a frame and a
        /// view of some size, its view a plan seen from above (the canvas shows model space through it at one scale). A plan
        /// twisted (group 51) is one: the canvas shows it turned, the cursor, the crosshair and the UCS icon with it.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <returns>Whether it can.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static bool CanWorkThrough(DxfViewport viewport)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            if (
                viewport.IsErased
                || viewport.Number <= 1
                || !(viewport.Width > 0.0)
                || !(viewport.Height > 0.0)
                || !(viewport.ViewHeight > 0.0)
            )
            {
                return false;
            }

            GeoVector3 direction = viewport.ViewDirection;
            return Math.Abs(direction.X) <= 1E-12 && Math.Abs(direction.Y) <= 1E-12 && direction.Z > 0.0;
        }

        /// <summary>
        /// The view of the canvas in model units through a viewport: the paper view scaled by the viewport's scale, about the
        /// point of model space under the middle of the canvas. A viewport at 1:20 seen at 2 pixels to the paper unit is seen at
        /// a tenth of a pixel to the model unit.
        /// </summary>
        /// <param name="paper">The view of the layout.</param>
        /// <param name="viewport">The viewport.</param>
        /// <returns>A new view, which zooms and pans as the paper's would.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the view or the viewport is null.</exception>
        public static CadView ModelView(CadView paper, DxfViewport viewport)
        {
            if (paper == null)
            {
                throw new ArgumentNullException(nameof(paper));
            }

            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double scale = viewport.Height / viewport.ViewHeight;
            GeoPoint2 middle = ToModel(viewport, new GeoPoint2(paper.CenterX, paper.CenterY));
            var model = new CadView(paper.Width, paper.Height);
            model.Set(middle.X, middle.Y, paper.PixelsPerUnit * scale, viewport.TwistAngle);
            return model;
        }

        /// <summary>
        /// The view a viewport takes from a view of the canvas in model units, the paper's staying as it is: the scale the two
        /// zooms give and the model point at the middle of the frame. The way back from <see cref="ModelView"/>, so a model
        /// view zoomed about the cursor gives the viewport's view zoomed there.
        /// </summary>
        /// <param name="paper">The view of the layout.</param>
        /// <param name="model">The view of the canvas in model units.</param>
        /// <param name="viewport">The viewport.</param>
        /// <param name="viewCenter">The centre of the view as the viewport keeps it (<see cref="DxfViewport.ViewCenter"/>): the
        /// point of model space at the middle of the frame, about the view's target and turned by its twist.</param>
        /// <param name="viewHeight">The height of model space the frame shows.</param>
        /// <exception cref="ArgumentNullException">Thrown when a view or the viewport is null.</exception>
        public static void ViewOf(
            CadView paper,
            CadView model,
            DxfViewport viewport,
            out GeoPoint2 viewCenter,
            out double viewHeight
        )
        {
            if (paper == null)
            {
                throw new ArgumentNullException(nameof(paper));
            }

            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double scale = model.PixelsPerUnit / paper.PixelsPerUnit;
            GeoPoint3 frame = viewport.CenterPoint;
            viewHeight = viewport.Height / scale;
            GeoPoint2 middle = ToView(viewport, new GeoPoint2(model.CenterX, model.CenterY));
            viewCenter = new GeoPoint2(
                middle.X - (paper.CenterX - frame.X) / scale,
                middle.Y - (paper.CenterY - frame.Y) / scale
            );
        }

        /// <summary>
        /// A point of model space in the coordinates of a viewport's view, as AutoCAD's display coordinates (TRANS 0 to 2): about
        /// the view's target and turned by its twist. Through a viewport twisted 30 degrees about the origin, (100, 0) is
        /// (86.603, 50).
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="model">The point of model space.</param>
        /// <returns>The point in the view's coordinates, where <see cref="DxfViewport.ViewCenter"/> is given.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static GeoPoint2 ToView(DxfViewport viewport, GeoPoint2 model)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            GeoPoint3 target = viewport.ViewTarget;
            double x = model.X - target.X;
            double y = model.Y - target.Y;
            double twist = viewport.TwistAngle;

            if (twist == 0.0)
            {
                return new GeoPoint2(x, y);
            }

            double cos = Math.Cos(twist);
            double sin = Math.Sin(twist);
            return new GeoPoint2(cos * x - sin * y, sin * x + cos * y);
        }

        /// <summary>The point of model space a point in the coordinates of a viewport's view stands for: the way back from <see cref="ToView"/>.</summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="view">The point in the view's coordinates.</param>
        /// <returns>The point of model space.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static GeoPoint2 FromView(DxfViewport viewport, GeoPoint2 view)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            GeoPoint3 target = viewport.ViewTarget;
            double twist = viewport.TwistAngle;
            double x = view.X;
            double y = view.Y;

            if (twist != 0.0)
            {
                double cos = Math.Cos(twist);
                double sin = Math.Sin(twist);
                x = cos * view.X + sin * view.Y;
                y = -sin * view.X + cos * view.Y;
            }

            return new GeoPoint2(target.X + x, target.Y + y);
        }

        /// <summary>The point of model space a point of the paper shows through a viewport.</summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="paper">The point of the paper.</param>
        /// <returns>The point of model space.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static GeoPoint2 ToModel(DxfViewport viewport, GeoPoint2 paper)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double scale = viewport.Height / viewport.ViewHeight;
            GeoPoint3 frame = viewport.CenterPoint;
            GeoPoint2 view = viewport.ViewCenter;
            return FromView(
                viewport,
                new GeoPoint2(view.X + (paper.X - frame.X) / scale, view.Y + (paper.Y - frame.Y) / scale)
            );
        }

        /// <summary>The point of the paper a point of model space is shown at through a viewport.</summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="model">The point of model space.</param>
        /// <returns>The point of the paper.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static GeoPoint2 ToPaper(DxfViewport viewport, GeoPoint2 model)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double scale = viewport.Height / viewport.ViewHeight;
            GeoPoint3 frame = viewport.CenterPoint;
            GeoPoint2 view = viewport.ViewCenter;
            GeoPoint2 at = ToView(viewport, model);
            return new GeoPoint2(frame.X + (at.X - view.X) * scale, frame.Y + (at.Y - view.Y) * scale);
        }

        /// <summary>The box of model space a viewport's frame shows: through a twisted view, the box about the frame turned.</summary>
        /// <param name="viewport">The viewport.</param>
        /// <returns>The box.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static DisplayBox ViewBox(DxfViewport viewport)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double height = viewport.ViewHeight;
            double width = height * viewport.Width / viewport.Height;
            GeoPoint2 view = viewport.ViewCenter;
            double twist = viewport.TwistAngle;

            if (twist != 0.0)
            {
                double cos = Math.Abs(Math.Cos(twist));
                double sin = Math.Abs(Math.Sin(twist));
                double turned = width * cos + height * sin;
                height = width * sin + height * cos;
                width = turned;
            }

            view = FromView(viewport, view);
            return new DisplayBox(
                view.X - width / 2.0,
                view.Y - height / 2.0,
                view.X + width / 2.0,
                view.Y + height / 2.0
            );
        }

        /// <summary>
        /// The view that shows a box of model space wholly in a viewport's frame, as ZOOM Window or Extents in a viewport: about
        /// the box's middle, as high as the box or as its width needs in the frame's proportion; through a twisted view, the box
        /// as the view turns it.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="box">The box of model space; not empty.</param>
        /// <param name="viewCenter">The centre of the view as the viewport keeps it (<see cref="DxfViewport.ViewCenter"/>).</param>
        /// <param name="viewHeight">The height of model space the frame shows.</param>
        /// <exception cref="ArgumentNullException">Thrown when the viewport is null.</exception>
        public static void Fit(DxfViewport viewport, DisplayBox box, out GeoPoint2 viewCenter, out double viewHeight)
        {
            if (viewport == null)
            {
                throw new ArgumentNullException(nameof(viewport));
            }

            double width = box.Width;
            double height = box.Height;
            double twist = viewport.TwistAngle;

            if (twist != 0.0)
            {
                double cos = Math.Abs(Math.Cos(twist));
                double sin = Math.Abs(Math.Sin(twist));
                double turned = width * cos + height * sin;
                height = width * sin + height * cos;
                width = turned;
            }

            viewCenter = ToView(viewport, new GeoPoint2((box.MinX + box.MaxX) / 2.0, (box.MinY + box.MaxY) / 2.0));
            viewHeight = Math.Max(Math.Max(height, width * viewport.Height / viewport.Width), 1E-9);
        }

        /// <summary>
        /// The name of a viewport's scale, the paper's units to the model's: a standard one as the list writes it, 1:50; another
        /// to the third place, 1:37.5 or 3.125:1.
        /// </summary>
        /// <param name="scale">The paper length of one model unit, as <see cref="DxfViewport.CustomScale"/> gives it; above nought.</param>
        /// <returns>The name; empty for a scale not above nought.</returns>
        public static string ScaleName(double scale)
        {
            if (!(scale > 0.0) || double.IsInfinity(scale))
            {
                return string.Empty;
            }

            return scale < 1.0
                ? "1:" + (1.0 / scale).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)
                : scale.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + ":1";
        }

        /// <summary>The paper length of one model unit a scale's name gives, 0.02 for 1:50; false for a name that is none.</summary>
        /// <param name="name">The name, as 1:50, 2:1 or 1:37.5.</param>
        /// <param name="scale">The scale.</param>
        /// <returns>Whether the name was read.</returns>
        public static bool TryReadScale(string name, out double scale)
        {
            scale = 0.0;
            string[] parts = (name ?? string.Empty).Trim().Split(':');
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            if (
                parts.Length != 2
                || !double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, culture, out double paper)
                || !double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, culture, out double model)
                || !(paper > 0.0)
                || !(model > 0.0)
                || double.IsInfinity(paper)
                || double.IsInfinity(model)
            )
            {
                return false;
            }

            scale = paper / model;
            return true;
        }

        private static string[] BuildScales()
        {
            var names = new List<string>();

            foreach (DxfStandardScaleType type in (DxfStandardScaleType[])Enum.GetValues(typeof(DxfStandardScaleType)))
            {
                string name = CadProperties.ScaleText(type);

                if (name.IndexOf(':') > 0)
                {
                    names.Add(name);
                }
            }

            return names.ToArray();
        }
    }
}
