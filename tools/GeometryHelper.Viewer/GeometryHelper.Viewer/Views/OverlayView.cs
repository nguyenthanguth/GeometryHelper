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

            var labels = _drawings.SelectMany(d => d.Labels).ToList();

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
            _snapMarker.Visibility = Visibility.Collapsed;
            Rebuild();
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
