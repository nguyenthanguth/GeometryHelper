using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Projection
{
    /// <summary>
    /// The lines of one view, in view millimetres at 1:1: what is seen, what is hidden, and the faces a section cuts.
    /// </summary>
    /// <remarks>
    /// X runs to the right of the sheet and Y up it, as the view frame's axes do. Each list is in a fixed order, by object
    /// id, kind and coordinates, so that the same objects and frame give the same view to the bit whatever order the
    /// objects came in.
    /// </remarks>
    public sealed class ProjectedView2
    {
        /// <summary>The name of the layer seen lines are written on.</summary>
        public const string VisibleLayer = "Visible";

        /// <summary>The name of the layer hidden lines are written on, drawn with the HIDDEN linetype.</summary>
        public const string HiddenLayer = "Hidden";

        /// <summary>The name of the layer centre lines go on, drawn with the CENTER linetype; a view writes none yet.</summary>
        public const string CentreLayer = "Centre";

        /// <summary>The name of the layer the hatching of section faces goes on, in colour 8.</summary>
        public const string HatchLayer = "Hatch";

        private readonly ProjectedEdge2[] _visible;
        private readonly ProjectedEdge2[] _hidden;
        private readonly ProjectedFace2[] _sections;

        /// <summary>
        /// Initializes a view from its lines and faces, each list in its order already.
        /// </summary>
        internal ProjectedView2(
            ProjectedEdge2[] visible,
            ProjectedEdge2[] hidden,
            ProjectedFace2[] sections,
            GeoRectangle2 bounds,
            bool isEmpty
        )
        {
            _visible = visible;
            _hidden = hidden;
            _sections = sections;
            Bounds = bounds;
            IsEmpty = isEmpty;
        }

        /// <summary>
        /// Gets the lines seen.
        /// </summary>
        public IReadOnlyList<ProjectedEdge2> VisibleEdges => _visible;

        /// <summary>
        /// Gets the lines hidden behind faces. None lies under a seen line: where a hidden edge falls on a seen one, only the
        /// seen one is drawn.
        /// </summary>
        public IReadOnlyList<ProjectedEdge2> HiddenEdges => _hidden;

        /// <summary>
        /// Gets the faces a section plane cuts out of the objects; empty for a plain view.
        /// </summary>
        public IReadOnlyList<ProjectedFace2> SectionFaces => _sections;

        /// <summary>
        /// Gets the box, square to the view's axes, of every line and section face; at the origin with no size when the view
        /// is empty.
        /// </summary>
        public GeoRectangle2 Bounds { get; }

        /// <summary>
        /// Gets a value indicating whether the view has no line and no section face.
        /// </summary>
        public bool IsEmpty { get; }

        /// <summary>
        /// Writes the lines of the view into the model space of a drawing at 1:1, so that it opens in the presenter and in
        /// AutoCAD.
        /// </summary>
        /// <param name="database">The drawing to write into; what it holds already is left as it is.</param>
        /// <param name="hatchScale">The scale of the hatching of section faces, a finite number above nought: at 1 the lines of
        /// ANSI31 are 3.175 apart, at 2 twice as far.</param>
        /// <remarks>
        /// <para>
        /// Seen lines go on the layer <see cref="VisibleLayer"/> and hidden ones on <see cref="HiddenLayer"/>, a straight
        /// line as a LINE, an arc as an ARC and a whole circle as a CIRCLE, each by layer. The layers are added where the
        /// drawing has none of their names, Visible in colour 7, Hidden in colour 8 with the linetype HIDDEN, Centre in
        /// colour 1 with the linetype CENTER and Hatch in colour 8; the two linetypes are added as acadiso.lin defines them, a
        /// dash of 6.35 and a gap of 3.175 for HIDDEN, where the drawing has none of their names.
        /// </para>
        /// <para>
        /// Each section face is written as a HATCH on the layer <see cref="HatchLayer"/>, before the lines so that they are
        /// drawn over it: its outline the outer loop and each hole an inner one, filled with AutoCAD's ANSI31, lines at 45
        /// degrees, at the scale given. A plate 300 by 200 by 20 cut across its length gives a HATCH of 4 000 square units. The
        /// HATCH holds the lines of ANSI31 only where the drawing's <see cref="DxfDatabase.PatternSearchPath"/> finds the
        /// pattern; otherwise it holds its name, for AutoCAD to draw it from its own pattern file.
        /// </para>
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale of the hatching is not a finite number above
        /// nought.</exception>
        public void WriteTo(DxfDatabase database, double hatchScale = 1.0)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            if (!(hatchScale > 0.0) || double.IsInfinity(hatchScale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hatchScale),
                    hatchScale,
                    "The scale of the hatching has to be a finite number above nought."
                );
            }

            AddLinetype(database, "HIDDEN", "__ __ __ __ __ __ __ __", 6.35, -3.175);
            AddLinetype(database, "CENTER", "____ _ ____ _ ____ _ ____", 31.75, -6.35, 6.35, -6.35);
            AddLayer(database, VisibleLayer, 7, null);
            AddLayer(database, HiddenLayer, 8, "HIDDEN");
            AddLayer(database, CentreLayer, 1, "CENTER");
            AddLayer(database, HatchLayer, 8, null);

            foreach (ProjectedFace2 section in _sections)
            {
                var hatch = new DxfHatch { Layer = HatchLayer };
                hatch.SetHatchPattern(DxfHatchPatternType.PreDefined, "ANSI31");
                hatch.PatternScale = hatchScale;
                hatch.AppendLoop(DxfHatchLoopTypes.External, section.Face.Boundary.Vertices, null);

                foreach (GeoPolygon2 hole in section.Face.Holes)
                {
                    hatch.AppendLoop(DxfHatchLoopTypes.Default, hole.Vertices, null);
                }

                database.ModelSpace.AppendEntity(hatch);
            }

            foreach (ProjectedEdge2 edge in _visible)
            {
                Append(database, edge, VisibleLayer);
            }

            foreach (ProjectedEdge2 edge in _hidden)
            {
                Append(database, edge, HiddenLayer);
            }
        }

        /// <inheritdoc/>
        public override string ToString() =>
            $"View2(Visible: {_visible.Length}, Hidden: {_hidden.Length}, Sections: {_sections.Length})";

        private static void Append(DxfDatabase database, ProjectedEdge2 edge, string layer)
        {
            DxfEntity entity;

            if (!edge.IsArc)
            {
                GeoLine2 line = edge.Line;
                entity = new DxfLine(
                    new GeoPoint3(line.StartPoint.X, line.StartPoint.Y, 0.0),
                    new GeoPoint3(line.EndPoint.X, line.EndPoint.Y, 0.0)
                );
            }
            else
            {
                GeoArc2 arc = edge.Arc;
                var center = new GeoPoint3(arc.Center.X, arc.Center.Y, 0.0);
                entity = edge.IsCircle
                    ? (DxfEntity)new DxfCircle(center, GeoVector3.ZAxis, arc.Radius)
                    : new DxfArc(center, arc.Radius, arc.StartAngle, arc.EndAngle);
            }

            entity.Layer = layer;
            database.ModelSpace.AppendEntity(entity);
        }

        private static void AddLinetype(DxfDatabase database, string name, string description, params double[] dashes)
        {
            if (database.LinetypeTable.Has(name))
            {
                return;
            }

            var linetype = new DxfLinetypeTableRecord
            {
                Name = name,
                AsciiDescription = description,
                NumDashes = dashes.Length,
            };

            for (int i = 0; i < dashes.Length; i++)
            {
                linetype.SetDashLengthAt(i, dashes[i]);
            }

            database.LinetypeTable.Add(linetype);
        }

        private static void AddLayer(DxfDatabase database, string name, int color, string linetype)
        {
            if (database.LayerTable.Has(name))
            {
                return;
            }

            var layer = new DxfLayerTableRecord { Name = name, ColorIndex = color };

            if (linetype != null)
            {
                layer.LinetypeName = linetype;
            }

            database.LayerTable.Add(layer);
        }
    }
}
