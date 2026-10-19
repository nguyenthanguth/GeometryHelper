using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A chain of straight pieces to draw as lines.
    /// </summary>
    public sealed class DrawPolyline
    {
        /// <summary>
        /// Initializes a polyline.
        /// </summary>
        /// <param name="points">The points, in order.</param>
        /// <param name="closed">Whether the last point joins back to the first, which is then not repeated.</param>
        public DrawPolyline(Vec3[] points, bool closed)
        {
            Points = points;
            Closed = closed;
        }

        /// <summary>
        /// Gets the points, in order.
        /// </summary>
        public Vec3[] Points { get; }

        /// <summary>
        /// Gets whether the last point joins back to the first.
        /// </summary>
        public bool Closed { get; }
    }

    /// <summary>
    /// A triangle to draw as a surface, with the unit normal it is lit by.
    /// </summary>
    public readonly struct DrawTriangle
    {
        /// <summary>
        /// Initializes a triangle; its normal is the unit vector of the right-hand rule round A, B, C, or nought where the
        /// corners are on a line.
        /// </summary>
        public DrawTriangle(Vec3 a, Vec3 b, Vec3 c)
        {
            A = a;
            B = b;
            C = c;
            Normal = Vec3.Cross(b - a, c - a).Normalized();
        }

        /// <summary>
        /// Gets the first corner.
        /// </summary>
        public Vec3 A { get; }

        /// <summary>
        /// Gets the second corner.
        /// </summary>
        public Vec3 B { get; }

        /// <summary>
        /// Gets the third corner.
        /// </summary>
        public Vec3 C { get; }

        /// <summary>
        /// Gets the unit normal.
        /// </summary>
        public Vec3 Normal { get; }
    }

    /// <summary>
    /// A text to draw at a point.
    /// </summary>
    public readonly struct DrawLabel
    {
        /// <summary>
        /// Initializes a label.
        /// </summary>
        public DrawLabel(Vec3 at, string text, double height)
        {
            At = at;
            Text = text;
            Height = height;
        }

        /// <summary>
        /// Gets where the label is.
        /// </summary>
        public Vec3 At { get; }

        /// <summary>
        /// Gets what it says.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the height of its letters in millimetres; 0 for a size that suits the screen.
        /// </summary>
        public double Height { get; }
    }

    /// <summary>
    /// What one item of a scene is drawn with: points, lines, triangles and labels, all in double precision world
    /// coordinates. The viewer subtracts the scene's origin before it hands them to the graphics card.
    /// </summary>
    public sealed class ItemPrimitives
    {
        /// <summary>
        /// Gets or sets the index of the item in <see cref="Scene.Items"/>.
        /// </summary>
        public int ItemIndex { get; set; }

        /// <summary>
        /// Gets the points to draw as dots.
        /// </summary>
        public List<Vec3> Points { get; } = new List<Vec3>();

        /// <summary>
        /// Gets the chains to draw as lines: the outline of the item, and its edges.
        /// </summary>
        public List<DrawPolyline> Polylines { get; } = new List<DrawPolyline>();

        /// <summary>
        /// Gets the triangles to draw as a surface.
        /// </summary>
        public List<DrawTriangle> Triangles { get; } = new List<DrawTriangle>();

        /// <summary>
        /// Gets the labels.
        /// </summary>
        public List<DrawLabel> Labels { get; } = new List<DrawLabel>();

        /// <summary>
        /// Gets or sets the box round the item. A ray or a plane, which are drawn out to the edge of the scene, counts for
        /// its origin only, so that fitting the view to the scene is not led by a line that has no end.
        /// </summary>
        public Bounds3 Bounds { get; set; }

        /// <summary>
        /// Gets what the library logged, at Warn and above, while the item was built: the reason it gave nothing where it cut a body
        /// and could not.
        /// </summary>
        public List<string> LibraryLog { get; } = new List<string>();

        /// <summary>
        /// Gets or sets what went wrong in drawing the item, or null. An item that could not be drawn in full still shows
        /// what could be.
        /// </summary>
        public string Warning { get; set; }
    }

    /// <summary>
    /// What a whole scene is drawn with, one <see cref="ItemPrimitives"/> to each item in the order of the scene's items.
    /// </summary>
    public sealed class Primitives
    {
        /// <summary>
        /// Gets the primitives of each item; the n-th is the n-th item of the scene.
        /// </summary>
        public List<ItemPrimitives> Items { get; } = new List<ItemPrimitives>();

        /// <summary>
        /// Gets the box round the scene.
        /// </summary>
        public Bounds3 Bounds { get; set; }

        /// <summary>
        /// Gets what went wrong, one line to an item that could not be drawn in full, naming the item.
        /// </summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>
        /// Builds the primitives of a scene, reading the shapes from the numbers in their data and asking the library only
        /// for the surfaces of faces, bodies and meshes. A shape the library refuses is drawn by its outline and warned of.
        /// </summary>
        /// <param name="scene">The scene.</param>
        /// <returns>The primitives.</returns>
        /// <exception cref="System.ArgumentNullException">Thrown when the scene is null.</exception>
        public static Primitives Build(Scene scene) => PrimitiveBuilder.Build(scene);
    }
}
