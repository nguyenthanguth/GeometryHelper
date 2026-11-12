using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The canvas's part of a multi-functional grip, as AutoCAD 2021 gives a polyline's: the bar on the middle of a segment,
    /// the grip under the cursor in pink, and the menu of what the grip does, opened once the cursor has rested on it and
    /// closed when the cursor leaves it, on Esc, on a click elsewhere, or when the selection or a command changes.
    /// </summary>
    public sealed partial class CadCanvas
    {
        // AutoCAD's light popup menu, kept light on the dark canvas as AutoCAD keeps it: border, fill, the row under the cursor,
        // the ink and the grey ink of an item that is off; rows of 22 with 3 above and below, the words 35 from the left (the
        // gutter of the icons), 19 right of and 24 below the grip.
        private static readonly Brush MenuFill = Solid(0xF0F0F0);
        private static readonly Pen MenuBorder = Frozen(new Pen(Solid(0xCCCCCC), 1.0));
        private static readonly Brush MenuRowFill = Solid(0xCCE8FF);
        private static readonly Brush MenuInk = Solid(0x000000);
        private static readonly Brush MenuGreyInk = Solid(0x6D6D6D);
        private static readonly Brush MenuShadow = Frozen(new SolidColorBrush(Color.FromArgb(26, 0, 0, 0)));
        private static readonly Typeface MenuFace = new Typeface("Segoe UI");
        private const double MenuRowHeight = 22.0;
        private const double MenuPad = 3.0;
        private const double MenuGutter = 35.0;
        private const double MenuTrail = 90.0;
        private const double MenuFontSize = 12.0;

        /// <summary>Gets whether the hover menu of a grip is open.</summary>
        internal bool IsGripMenuOpen => _menu != null;

        /// <summary>Gets the box of the hover menu on the canvas, in device-independent pixels; empty where none is open.</summary>
        internal Rect GripMenuBox => _menu != null ? _menuBox : Rect.Empty;

        /// <summary>Gets the items of the hover menu open, empty where none is.</summary>
        internal IReadOnlyList<CadGripMenuItem> GripMenuItems =>
            _menu ?? (IReadOnlyList<CadGripMenuItem>)Array.Empty<CadGripMenuItem>();

        /// <summary>
        /// The way a segment middle's bar lies on the screen: along the segment (the chord of an arc segment, which is the
        /// tangent at its middle) as the view shows it, so that a rotated view turns the bar with it. A unit vector; across for
        /// a grip with no direction is not asked.
        /// </summary>
        private Vector BarAxis(CadView view, double scale, CadGrip grip, Point at)
        {
            Point ahead = ToScreen(
                view,
                new GeoPoint2(grip.Point.X + grip.Direction.X, grip.Point.Y + grip.Direction.Y),
                scale
            );
            Vector axis = ahead - at;

            if (axis.Length < 1E-12)
            {
                return new Vector(1.0, 0.0);
            }

            axis.Normalize();
            return axis;
        }

        /// <summary>Whether a point of the canvas is on a grip: in its square, or in the bar's length along its segment by the square's side across.</summary>
        private bool OnGrip(CadView view, double scale, GripAt grip, Point p)
        {
            Point at = ToScreen(view, grip.Grip.Point, scale);
            double half = GripSquare / 2.0;
            Vector d = p - at;

            if (grip.Grip.Kind != CadGripKind.SegmentMiddle)
            {
                return Math.Abs(d.X) <= half && Math.Abs(d.Y) <= half;
            }

            Vector axis = BarAxis(view, scale, grip.Grip, at);
            double along = d.X * axis.X + d.Y * axis.Y;
            double across = d.Y * axis.X - d.X * axis.Y;
            return Math.Abs(along) <= GripShapes.BarLength(_gripSize) / 2.0 && Math.Abs(across) <= half;
        }

        /// <summary>
        /// A plain grip: a square, or for the middle of a polyline segment a bar along it. The grip colour edged in dark; under
        /// the cursor or clicked, its colour and no edge. Placed on whole pixels where it is square to the screen.
        /// </summary>
        private void DrawPlainGrip(
            DrawingContext dc,
            CadView view,
            double scale,
            CadGrip grip,
            Point at,
            Brush fill,
            bool edged
        )
        {
            bool bar = grip.Kind == CadGripKind.SegmentMiddle;
            double width = bar ? GripShapes.BarLength(_gripSize) : GripSquare;
            double height = bar ? GripShapes.BarThickness(_gripSize) : GripSquare;
            Vector axis = bar ? BarAxis(view, scale, grip, at) : new Vector(1.0, 0.0);
            bool turned = Math.Abs(axis.Y) > 1E-9 && Math.Abs(axis.X) > 1E-9;

            // Along the y axis the bar's sides swap; along a slant it is turned about the grip.
            if (bar && !turned && Math.Abs(axis.Y) > 0.5)
            {
                double side = width;
                width = height;
                height = side;
            }

            Rect box = turned
                ? new Rect(at.X - width / 2.0, at.Y - height / 2.0, width, height)
                : new Rect(Math.Round(at.X - width / 2.0), Math.Round(at.Y - height / 2.0), width, height);

            if (turned)
            {
                dc.PushTransform(new RotateTransform(Math.Atan2(axis.Y, axis.X) * 180.0 / Math.PI, at.X, at.Y));
            }

            if (edged)
            {
                // The pen is 1 wide about the rectangle's edge, so the shape keeps its outer size.
                dc.DrawRectangle(fill, GripContour, new Rect(box.X + 0.5, box.Y + 0.5, width - 1.0, height - 1.0));
            }
            else
            {
                dc.DrawRectangle(fill, null, box);
            }

            if (turned)
            {
                dc.Pop();
            }
        }

        /// <summary>Starts the wait before the menu of the grip under the cursor opens (400 milliseconds).</summary>
        private void WaitForGripMenu()
        {
            _menuTimer?.Stop();

            if (_hotGrip < 0 || (_gripMultifunctional & 2) == 0 || IsRunning)
            {
                return;
            }

            if (_menuTimer == null)
            {
                _menuTimer = new DispatcherTimer(DispatcherPriority.Input) { Interval = GripMenuDelay };
                _menuTimer.Tick += (s, e) =>
                {
                    _menuTimer.Stop();
                    OpenGripMenuNow();
                };
            }

            _menuTimer.Start();
        }

        /// <summary>
        /// Opens the menu of the grip under the cursor now, as the wait for it ends: below and right of the grip, to its left or
        /// above where it would run off the canvas. Nothing opens while a command runs, with several grips hot, with bit 2 of
        /// <see cref="GripMultifunctional"/> off, or for a grip with no menu.
        /// </summary>
        /// <returns>Whether a menu is open.</returns>
        internal bool OpenGripMenuNow()
        {
            CadView view = WorkView;
            CadDocument document = Document;

            if (
                _menu != null
                || _hotGrip < 0
                || _hotGrip >= _grips.Count
                || (_gripMultifunctional & 2) == 0
                || IsRunning
                || _hot.Count > 1
                || view == null
                || document == null
            )
            {
                return _menu != null;
            }

            GripAt grip = _grips[_hotGrip];
            IReadOnlyList<CadGripMenuItem> items = CadGripMenus.Of(document, grip.Handle, grip.Grip.Index);

            if (items.Count == 0)
            {
                return false;
            }

            double scale = PixelScale;
            Point at = ToScreen(view, grip.Grip.Point, scale);
            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
            double widest = 0.0;

            foreach (CadGripMenuItem item in items)
            {
                widest = Math.Max(widest, MenuText(item.Text, MenuInk, dip).Width);
            }

            double width = Math.Ceiling(widest + MenuTrail);
            double height = 2.0 * MenuPad + MenuRowHeight * items.Count;
            double x = at.X + 19.0;
            double y = at.Y + 24.0;

            if (x + width > ActualWidth)
            {
                x = at.X - 19.0 - width;
            }

            if (y + height > ActualHeight)
            {
                y = at.Y - 24.0 - height;
            }

            _menu = items;
            _menuGrip = _hotGrip;
            _menuAt = grip;
            _menuRow = -1;
            _menuBox = new Rect(Math.Max(0.0, Math.Round(x)), Math.Max(0.0, Math.Round(y)), width, height);
            double reach = Math.Max(GripSquare, GripShapes.BarLength(_gripSize)) / 2.0;
            _menuArea = Rect.Union(_menuBox, new Rect(at.X - reach, at.Y - reach, 2.0 * reach, 2.0 * reach));
            InvalidateVisual();
            return true;
        }

        /// <summary>Closes the menu and stops the wait for it.</summary>
        private void CloseGripMenu()
        {
            _menuTimer?.Stop();

            if (_menu != null)
            {
                _menu = null;
                _menuGrip = -1;
                _menuRow = -1;
                InvalidateVisual();
            }
        }

        /// <summary>Follows the cursor over the open menu: the row under it takes the highlight.</summary>
        /// <returns>Whether the cursor is on the menu or on the way from the grip to it, so that the menu stays.</returns>
        private bool TrackMenu(Point p)
        {
            if (!_menuArea.Contains(p))
            {
                return false;
            }

            int row = MenuRowAt(p);

            if (row != _menuRow)
            {
                _menuRow = row;
                InvalidateVisual();
            }

            return true;
        }

        /// <summary>The row of the open menu under a point, or -1 for none.</summary>
        private int MenuRowAt(Point p)
        {
            if (_menu == null || !_menuBox.Contains(p))
            {
                return -1;
            }

            int row = (int)Math.Floor((p.Y - _menuBox.Y - MenuPad) / MenuRowHeight);
            return row >= 0 && row < _menu.Count ? row : -1;
        }

        /// <summary>
        /// Chooses a row of the open menu, as a click on it does: the menu closes and the item starts as
        /// <see cref="CadCommandSession.StartGripAction"/> says, its grip hot while it runs. A row that is grey does nothing.
        /// </summary>
        /// <param name="row">The row, from nought.</param>
        /// <returns>Whether an item was started.</returns>
        internal bool ChooseGripMenuRow(int row)
        {
            if (_menu == null || row < 0 || row >= _menu.Count || !_menu[row].Enabled || _menuGrip >= _grips.Count)
            {
                return false;
            }

            CadGripAction action = _menu[row].Action;
            GripAt grip = _grips[_menuGrip];
            CloseGripMenu();
            var hot = new KeyValuePair<string, int>(grip.Handle, grip.Grip.Index);
            _hot.Clear();
            _hot.Add(hot);
            _gripEditing = Session != null && Session.StartGripAction(hot.Key, hot.Value, action);

            if (!_gripEditing)
            {
                _hot.Clear();
            }

            HistoryWritten?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            return true;
        }

        /// <summary>Closes the menu where the grips found are no longer those it was opened on.</summary>
        private void KeepGripMenuIfSame()
        {
            if (_menu == null)
            {
                return;
            }

            // The grip it was of: the one at its index, if its place and its owner are the same.
            if (_menuGrip < 0 || _menuGrip >= _grips.Count || _menuAt == null)
            {
                CloseGripMenu();
                return;
            }

            GripAt now = _grips[_menuGrip];

            if (
                now.Handle != _menuAt.Handle
                || now.Grip.Index != _menuAt.Grip.Index
                || now.Grip.Kind != _menuAt.Grip.Kind
                || Math.Abs(now.Grip.Point.X - _menuAt.Grip.Point.X) > 1E-12
                || Math.Abs(now.Grip.Point.Y - _menuAt.Grip.Point.Y) > 1E-12
            )
            {
                CloseGripMenu();
            }
        }

        private static FormattedText MenuText(string text, Brush ink, double dip) =>
            new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                MenuFace,
                MenuFontSize,
                ink,
                dip
            );

        /// <summary>Draws the open menu: light, with a thin shadow, the row under the cursor in the highlight.</summary>
        private void DrawGripMenu(DrawingContext dc)
        {
            if (_menu == null)
            {
                return;
            }

            double dip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            for (int i = 1; i <= 3; i++)
            {
                dc.DrawRectangle(
                    MenuShadow,
                    null,
                    new Rect(_menuBox.X + i, _menuBox.Y + i, _menuBox.Width, _menuBox.Height)
                );
            }

            dc.DrawRectangle(
                MenuFill,
                MenuBorder,
                new Rect(_menuBox.X + 0.5, _menuBox.Y + 0.5, _menuBox.Width - 1.0, _menuBox.Height - 1.0)
            );

            for (int i = 0; i < _menu.Count; i++)
            {
                double top = _menuBox.Y + MenuPad + i * MenuRowHeight;

                if (i == _menuRow && _menu[i].Enabled)
                {
                    dc.DrawRectangle(
                        MenuRowFill,
                        null,
                        new Rect(_menuBox.X + 1.0, top, _menuBox.Width - 2.0, MenuRowHeight)
                    );
                }

                FormattedText text = MenuText(_menu[i].Text, _menu[i].Enabled ? MenuInk : MenuGreyInk, dip);
                dc.DrawText(text, new Point(_menuBox.X + MenuGutter, top + (MenuRowHeight - text.Height) / 2.0));
            }
        }
    }
}
