using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using HelixToolkit.SharpDX.Core;
using HelixToolkit.Wpf.SharpDX;
using SharpDX;
using Color = System.Windows.Media.Color;

namespace GeometryHelper.Viewer.Views
{
    /// <summary>
    /// What is drawn over the scene: the dimensions of the measurements until they are cleared, the point picked so far, the rotation
    /// centre, and the marker of the snap under the cursor. It is drawn in front of everything, since a dimension between two corners of
    /// a body runs through the body. The coordinates are held in double precision and handed to the graphics card relative to the
    /// render origin of the scene, so they are made again when the origin moves.
    /// </summary>
    public sealed class OverlayView
    {
        private const int InFront = -8000000;
        private static readonly Color DimensionColor = Color.FromRgb(0xD0, 0x02, 0x1B);
        private static readonly Color LibraryColor = Color.FromRgb(0x00, 0x89, 0x7B);
        private static readonly Color CentreColor = Color.FromRgb(0x1F, 0x4F, 0xD8);
        private static readonly Color SnapColor = Color.FromRgb(0xC8, 0x00, 0xA0);
        private static readonly Color TextColor = Color.FromRgb(0x20, 0x20, 0x20);

        private readonly Viewport3DX _viewport;
        private readonly SceneView _sceneView;
        private readonly List<Element3D> _elements = new List<Element3D>();
        private readonly PointGeometryModel3D _snapMarker;
        private List<DimensionDrawing> _drawings = new List<DimensionDrawing>();
        private Vec3? _centre;
        private Vec3? _snapAt;
        private DiagnosticOverlay _diagnostics;
        private DiagnosticOverlay _lens;
        private Vec3? _marker;
        private WorkPlane _workPlane = WorkPlane.Global;
        private ClipSet _clips = ClipSet.Empty;
        private int _clipSelected = -1;

        /// <summary>
        /// Initializes the overlay over a viewport.
        /// </summary>
        /// <param name="viewport">The viewport.</param>
        /// <param name="sceneView">The view of the scene, which gives the render origin.</param>
        public OverlayView(Viewport3DX viewport, SceneView sceneView)
        {
            _viewport = viewport;
            _sceneView = sceneView;
            _snapMarker = new PointGeometryModel3D
            {
                Color = SnapColor,
                Size = new Size(15, 15),
                Figure = PointFigure.Rect,
                FigureRatio = 0.6,
                IsHitTestVisible = false,
                DepthBias = InFront,
                Visibility = Visibility.Collapsed,
            };
            _viewport.Items.Add(_snapMarker);
        }

        /// <summary>
        /// Shows the dimensions, replacing the ones before; none clears them.
        /// </summary>
        /// <param name="drawings">The drawings of the measurements, and of the point picked so far.</param>
        public void SetDrawings(IEnumerable<DimensionDrawing> drawings)
        {
            _drawings = drawings.ToList();
            Rebuild();
        }

        /// <summary>
        /// Shows what the diagnostics found, in the colours of the checks, or takes it away.
        /// </summary>
        /// <param name="overlay">The strokes, dots and labels of a report, or null.</param>
        public void SetDiagnostics(DiagnosticOverlay overlay)
        {
            _diagnostics = overlay;
            Rebuild();
        }

        /// <summary>
        /// Shows the gap magnifier, or takes it away.
        /// </summary>
        /// <param name="lens">The magnified gap, or null.</param>
        public void SetLens(DiagnosticOverlay lens)
        {
            _lens = lens;
            Rebuild();
        }

        /// <summary>
        /// Marks the finding that was clicked, or takes the mark away.
        /// </summary>
        /// <param name="at">Where the finding is, or null.</param>
        public void SetMarker(Vec3? at)
        {
            _marker = at;
            Rebuild();
        }

        /// <summary>
        /// Shows the axes of the work plane at its origin, which the global plane has no use for.
        /// </summary>
        /// <param name="plane">The work plane; null or the global plane shows nothing.</param>
        public void SetWorkPlane(WorkPlane plane)
        {
            _workPlane = plane ?? WorkPlane.Global;
            Rebuild();
        }

        /// <summary>
        /// Shows the clip planes: a square outline on each, the one that is selected thicker with an arrow along its normal and a handle to take hold of.
        /// </summary>
        /// <param name="clips">The planes.</param>
        /// <param name="selected">The number of the plane that is selected, or -1.</param>
        public void SetClipPlanes(ClipSet clips, int selected)
        {
            _clips = clips ?? ClipSet.Empty;
            _clipSelected = selected;
            Rebuild();
        }

        /// <summary>
        /// Shows the rotation centre as a cross, or takes it away.
        /// </summary>
        /// <param name="centre">The rotation centre, or null.</param>
        public void SetRotationCentre(Vec3? centre)
        {
            _centre = centre;
            Rebuild();
        }

        /// <summary>
        /// Shows the marker of the snap under the cursor, or takes it away.
        /// </summary>
        /// <param name="point">The snap point, or null for none.</param>
        /// <param name="kind">The kind of snap, which sets the shape of the marker.</param>
        public void SetSnap(Vec3? point, SnapKinds kind)
        {
            _snapAt = point;

            if (!point.HasValue)
            {
                _snapMarker.Visibility = Visibility.Collapsed;
                return;
            }

            Vec3F f = RenderSpace.Relative(point.Value, _sceneView.Origin);
            _snapMarker.Geometry = new PointGeometry3D
            {
                Positions = new Vector3Collection { new Vector3(f.X, f.Y, f.Z) },
                Indices = new IntCollection { 0 },
            };
            _snapMarker.Figure = kind == SnapKinds.Centre || kind == SnapKinds.Nearest ? PointFigure.Ellipse : kind == SnapKinds.Mid || kind == SnapKinds.Intersection ? PointFigure.Cross : PointFigure.Rect;
            _snapMarker.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Makes the geometry again, relative to the render origin as it stands.
        /// </summary>
        public void Rebuild()
        {
            foreach (Element3D element in _elements)
            {
                _viewport.Items.Remove(element);
                element.Dispose();
            }

            _elements.Clear();
            Vec3 origin = _sceneView.Origin;
            Vector3 Rel(Vec3 v)
            {
                Vec3F f = RenderSpace.Relative(v, origin);
                return new Vector3(f.X, f.Y, f.Z);
            }

            Lines(_drawings.SelectMany(d => d.Lines), DimensionColor, 2.2, Rel);
            Lines(_drawings.SelectMany(d => d.LibraryLines), LibraryColor, 2.6, Rel);
            Points(_drawings.SelectMany(d => d.Points), DimensionColor, PointFigure.Ellipse, 9.0, Rel);

            DrawDiagnostics(_diagnostics, Rel);
            DrawDiagnostics(_lens, Rel);

            if (_marker.HasValue)
            {
                // A cross, which hides little of what it marks: the tolerance spheres and the vertices under it stay in sight.
                Points(new[] { _marker.Value }, ParseColor(DiagnosticColors.Marker, DimensionColor), PointFigure.Cross, 40.0, Rel);
            }

            var clipLabels = new List<DrawLabel>();
            DrawWorkPlane(Rel);
            DrawClipPlanes(clipLabels, Rel);

            var labels = _drawings.SelectMany(d => d.Labels).Concat(_diagnostics?.Labels ?? new List<DrawLabel>()).Concat(_lens?.Labels ?? new List<DrawLabel>()).Concat(clipLabels).ToList();

            if (labels.Count > 0)
            {
                var text = new BillboardText3D();

                foreach (DrawLabel label in labels)
                {
                    text.TextInfo.Add(new TextInfo(label.Text, Rel(label.At))
                    {
                        Foreground = new Color4(TextColor.R / 255f, TextColor.G / 255f, TextColor.B / 255f, 1f),
                        Background = new Color4(1f, 1f, 1f, 0.85f),
                        Scale = (float)(LabelSize.Pixels(label.Height, 0.0) / SceneView.LabelFontPixels),
                    });
                }

                Add(new BillboardTextModel3D { Geometry = text, FixedSize = true, IsHitTestVisible = false, DepthBias = InFront });
            }

            if (_centre.HasValue)
            {
                Points(new[] { _centre.Value }, CentreColor, PointFigure.Cross, 16.0, Rel);
            }

            if (_snapAt.HasValue)
            {
                _snapMarker.Geometry = new PointGeometry3D
                {
                    Positions = new Vector3Collection { Rel(_snapAt.Value) },
                    Indices = new IntCollection { 0 },
                };
            }
        }

        /// <summary>
        /// Takes everything off the view and lets go of the elements.
        /// </summary>
        public void Clear()
        {
            _drawings = new List<DimensionDrawing>();
            _centre = null;
            _snapAt = null;
            _diagnostics = null;
            _lens = null;
            _marker = null;
            _snapMarker.Visibility = Visibility.Collapsed;
            Rebuild();
        }

        private void DrawWorkPlane(Func<Vec3, Vector3> rel)
        {
            if (_workPlane == null || _workPlane.IsGlobal)
            {
                return;
            }

            Bounds3 scene = _sceneView.Primitives?.Bounds ?? default(Bounds3);
            double length = Math.Max(1.0, scene.IsEmpty ? 100.0 : scene.Size.Length * 0.08);
            Vec3 o = _workPlane.Origin;

            Lines(new[] { new DrawPolyline(new[] { o, o + _workPlane.XAxis * length }, false) }, Color.FromRgb(0xD0, 0x20, 0x20), 3.0, rel);
            Lines(new[] { new DrawPolyline(new[] { o, o + _workPlane.YAxis * length }, false) }, Color.FromRgb(0x20, 0xA0, 0x20), 3.0, rel);
            Lines(new[] { new DrawPolyline(new[] { o, o + _workPlane.ZAxis * (length * 0.6) }, false) }, Color.FromRgb(0x20, 0x40, 0xD0), 3.0, rel);
            Points(new[] { o }, Color.FromRgb(0x20, 0x20, 0x20), PointFigure.Ellipse, 9.0, rel);
        }

        private void DrawClipPlanes(List<DrawLabel> labels, Func<Vec3, Vector3> rel)
        {
            if (_clips == null || _clips.Count == 0)
            {
                return;
            }

            Bounds3 scene = _sceneView.Primitives?.Bounds ?? default(Bounds3);
            double diagonal = scene.IsEmpty ? 100.0 : Math.Max(scene.Size.Length, 1.0);
            double half = diagonal;
            ClipSet sceneBox = scene.IsEmpty ? ClipSet.Empty : ClipSet.FromBox(scene);

            for (int i = 0; i < _clips.Count; i++)
            {
                ClipPlane plane = _clips.Planes[i];
                Vec3 handle = plane.HandleFor(scene);
                WorkPlane axes = WorkPlane.FromNormal(handle, plane.Normal, new Vec3(1.0, 0.0, 0.0));
                Vec3 u = axes.XAxis * half;
                Vec3 v = axes.YAxis * half;
                bool selected = i == _clipSelected;
                Color color = plane.Enabled ? Color.FromRgb(0xE0, 0x80, 0x00) : Color.FromRgb(0xA0, 0xA0, 0xA0);

                // A square larger than the scene is laid on the plane and cut to what the other planes leave, so that the six planes of a clip box are the six faces of the box, and to the box round the scene,
                // so that it is the patch of the plane that the scene is in and lies inside the depth that every view draws.
                List<Vec3> face = sceneBox.ClipPolygon(_clips.ClipPolygonExcept(new[] { handle - u - v, handle + u - v, handle + u + v, handle - u + v }, i));

                if (face.Count >= 3)
                {
                    Lines(new[] { new DrawPolyline(face.ToArray(), true) }, color, selected ? 3.2 : 1.6, rel);
                    labels.Add(new DrawLabel(face[0], (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), 0.0));
                }

                if (selected)
                {
                    // An arrow from the handle along the normal, towards the side that stays, with a head of four short lines.
                    double arrow = diagonal * 0.15;
                    Vec3 tip = handle + plane.Normal * arrow;
                    Vec3 back = plane.Normal * (arrow * 0.2);
                    Vec3 wing = axes.XAxis * (arrow * 0.1);
                    Vec3 wing2 = axes.YAxis * (arrow * 0.1);

                    Lines(
                        new[]
                        {
                            new DrawPolyline(new[] { handle, tip }, false),
                            new DrawPolyline(new[] { tip, tip - back + wing }, false),
                            new DrawPolyline(new[] { tip, tip - back - wing }, false),
                            new DrawPolyline(new[] { tip, tip - back + wing2 }, false),
                            new DrawPolyline(new[] { tip, tip - back - wing2 }, false),
                        },
                        color,
                        3.2,
                        rel);
                    Points(new[] { handle }, color, PointFigure.Ellipse, 15.0, rel);
                }
            }
        }

        private static Color ParseColor(string text, Color fallback)
            => SceneColor.TryParse(text, out byte r, out byte g, out byte b) ? Color.FromRgb(r, g, b) : fallback;

        /// <summary>
        /// Draws the strokes and dots of a diagnostic overlay, one element to each colour and thickness so that the graphics card is given a few
        /// large lists and not a small one to each finding.
        /// </summary>
        private void DrawDiagnostics(DiagnosticOverlay overlay, Func<Vec3, Vector3> rel)
        {
            if (overlay == null)
            {
                return;
            }

            foreach (var group in overlay.Strokes.GroupBy(s => new { s.Color, s.Thickness }).OrderBy(g => g.Key.Color, StringComparer.Ordinal).ThenBy(g => g.Key.Thickness))
            {
                Lines(group.Select(s => s.Line), ParseColor(group.Key.Color, DimensionColor), group.Key.Thickness, rel);
            }

            foreach (var group in overlay.Dots.GroupBy(d => new { d.Color, d.Size }).OrderBy(g => g.Key.Color, StringComparer.Ordinal).ThenBy(g => g.Key.Size))
            {
                Points(group.Select(d => d.At), ParseColor(group.Key.Color, DimensionColor), PointFigure.Ellipse, group.Key.Size, rel);
            }
        }

        private void Add(Element3D element)
        {
            _elements.Add(element);
            _viewport.Items.Add(element);
        }

        private void Lines(IEnumerable<DrawPolyline> lines, Color color, double thickness, Func<Vec3, Vector3> rel)
        {
            var positions = new Vector3Collection();
            var indices = new IntCollection();

            foreach (DrawPolyline line in lines)
            {
                int first = positions.Count;
                int count = line.Points.Length;

                foreach (Vec3 point in line.Points)
                {
                    positions.Add(rel(point));
                }

                int segments = line.Closed ? count : count - 1;

                for (int s = 0; s < segments; s++)
                {
                    indices.Add(first + s);
                    indices.Add(first + (s + 1) % count);
                }
            }

            if (positions.Count == 0)
            {
                return;
            }

            Add(new LineGeometryModel3D
            {
                Geometry = new LineGeometry3D { Positions = positions, Indices = indices },
                Color = color,
                Thickness = thickness,
                Smoothness = 0,
                IsHitTestVisible = false,
                DepthBias = InFront,
            });
        }

        private void Points(IEnumerable<Vec3> points, Color color, PointFigure figure, double size, Func<Vec3, Vector3> rel)
        {
            var positions = new Vector3Collection();
            var indices = new IntCollection();

            foreach (Vec3 point in points)
            {
                indices.Add(positions.Count);
                positions.Add(rel(point));
            }

            if (positions.Count == 0)
            {
                return;
            }

            Add(new PointGeometryModel3D
            {
                Geometry = new PointGeometry3D { Positions = positions, Indices = indices },
                Color = color,
                Figure = figure,
                Size = new Size(size, size),
                IsHitTestVisible = false,
                DepthBias = InFront,
            });
        }
    }
}
