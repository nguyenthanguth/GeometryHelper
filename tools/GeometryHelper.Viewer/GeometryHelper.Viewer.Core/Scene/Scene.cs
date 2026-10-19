using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// A set of shapes to look at: layers, and items on them, each item a library shape kept as the exact numbers that
    /// rebuild it. A scene is what a script or a test harness writes with <see cref="SceneWriter"/> and the viewer reads.
    /// </summary>
    public sealed class Scene
    {
        /// <summary>
        /// The point tolerance a scene is read at when it does not say, in millimetres.
        /// </summary>
        public const double DefaultEqualPoint = 1E-3;

        /// <summary>
        /// Gets or sets the title shown in the window.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the point tolerance, in millimetres, the shapes are rebuilt and measured at.
        /// </summary>
        public double EqualPoint { get; set; } = DefaultEqualPoint;

        /// <summary>
        /// Gets or sets the vector tolerance the shapes are rebuilt at.
        /// </summary>
        public double EqualVector { get; set; } = GeometryHelper.Tolerance.DefaultEqualVector;

        /// <summary>
        /// Gets or sets the angle tolerance, in radians, the shapes are rebuilt at.
        /// </summary>
        public double EqualAngleRad { get; set; } = GeometryHelper.Tolerance.DefaultEqualAngleRad;

        /// <summary>
        /// Gets or sets the planar tolerance, in millimetres, the faces and polygons are judged flat at.
        /// </summary>
        public double EqualPlanar { get; set; } = GeometryHelper.Tolerance.DefaultEqualPlanar;

        /// <summary>
        /// Makes the scene ask for a tolerance: all four thresholds are kept, so a shape built under a planar tolerance
        /// of 0.01 is rebuilt under it and not refused.
        /// </summary>
        /// <param name="tolerance">The tolerance.</param>
        public void SetTolerance(Tolerance tolerance)
        {
            EqualPoint = tolerance.EqualPoint;
            EqualVector = tolerance.EqualVector;
            EqualAngleRad = tolerance.EqualAngleRad;
            EqualPlanar = tolerance.EqualPlanar;
        }

        /// <summary>
        /// Gets the tolerance the shapes are rebuilt and measured at the least, below which the library is asked nothing:
        /// 1E-12 in every threshold. It is not <c>new Tolerance()</c>, whose thresholds are all nought.
        /// </summary>
        public static Tolerance MinimalTolerance => new Tolerance(1E-12, 1E-12, 1E-12, 1E-12);

        /// <summary>
        /// Checks that the four thresholds are finite numbers not below nought; a tolerance of nought, as a script sets with
        /// <c>Tol = new Tolerance(0, 0)</c>, is one the scene can keep and be read at.
        /// </summary>
        /// <exception cref="FormatException">Thrown with a message saying which threshold is wrong.</exception>
        public void ValidateTolerance()
        {
            Check("equalPoint", EqualPoint, false);
            Check("equalVector", EqualVector, false);
            Check("equalAngle", EqualAngleRad, false);
            Check("equalPlanar", EqualPlanar, false);
        }

        private static void Check(string name, double value, bool positive)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0.0 || (positive && value == 0.0))
            {
                throw new FormatException("The scene's " + name + " must be a finite number " + (positive ? "above nought" : "not below nought") + ", not " + value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ".");
            }
        }

        /// <summary>
        /// Runs a piece of work with the library at the tolerance of the scene; where the library refuses a shape at it, runs
        /// it again at <see cref="MinimalTolerance"/> and says so.
        /// </summary>
        /// <typeparam name="T">What the work gives back.</typeparam>
        /// <param name="work">The work, on this thread; it may be run twice.</param>
        /// <param name="note">"rebuilt and measured at 1E-12" when the second run was the one that worked, otherwise null.</param>
        /// <returns>What the work gave.</returns>
        /// <exception cref="ArgumentException">Thrown when the library refuses the shape at both tolerances; the first refusal is the one thrown.</exception>
        /// <exception cref="InvalidOperationException">Thrown likewise.</exception>
        public T Run<T>(Func<T> work, out string note)
        {
            note = null;

            try
            {
                using (GeometryHelper.Tolerance.Use(Tolerance))
                {
                    return work();
                }
            }
            catch (Exception first) when (first is ArgumentException || first is InvalidOperationException)
            {
                try
                {
                    T result;

                    using (GeometryHelper.Tolerance.Use(MinimalTolerance))
                    {
                        result = work();
                    }

                    note = "rebuilt and measured at 1E-12 (the library refused the shape at the tolerance of the scene: " + first.Message + ")";
                    return result;
                }
                catch (Exception second) when (second is ArgumentException || second is InvalidOperationException)
                {
                    throw first;
                }
            }
        }

        /// <summary>
        /// Rebuilds the library object an item holds, at the tolerance of the scene and, where that is refused, at 1E-12.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="note">"rebuilt and measured at 1E-12" when that was needed, otherwise null.</param>
        /// <returns>The object, as <see cref="SceneItem.ToGeometry"/> gives it.</returns>
        public object Rebuild(SceneItem item, out string note) => Run(() => item.ToGeometry(), out note);

        /// <summary>
        /// Gets the layers, in the order they were made.
        /// </summary>
        public List<Layer> Layers { get; } = new List<Layer>();

        /// <summary>
        /// Gets the items, in the order they were added.
        /// </summary>
        public List<SceneItem> Items { get; } = new List<SceneItem>();

        /// <summary>
        /// Gets the saved views, in the order they were made: named looks at the scene. A scene written before there were any has none, and a scene
        /// that has none is written without the list, so that it is the file it always was. The viewer opens a scene at its first view.
        /// </summary>
        public List<SavedView> Views { get; } = new List<SavedView>();

        /// <summary>
        /// Gets what was left out of the scene when it was read, one line each: a saved view that was not one is dropped with a line saying which and why, and
        /// the rest of the file is kept. A scene that was made, and not read, has none. They are not written again.
        /// </summary>
        public List<string> LoadWarnings { get; } = new List<string>();

        /// <summary>
        /// Gets the library tolerance the scene asks for: its four thresholds.
        /// </summary>
        public Tolerance Tolerance => new Tolerance(EqualPoint, EqualVector, EqualAngleRad, EqualPlanar);

        /// <summary>
        /// Gets the kinds of the items this viewer does not know how to draw, each once, in the order they first appear.
        /// Such items are kept in the scene and written out again unchanged.
        /// </summary>
        public IReadOnlyList<string> UnknownKinds
            => Items.Select(item => item.Kind).Where(kind => !SceneKinds.IsKnown(kind)).Distinct(StringComparer.Ordinal).ToList();

        /// <summary>
        /// Gets the layer of a name, or null where there is none.
        /// </summary>
        public Layer FindLayer(string name) => Layers.FirstOrDefault(layer => string.Equals(layer.Name, name, StringComparison.Ordinal));
    }

    /// <summary>
    /// A named group of items drawn in one colour that is shown or hidden together.
    /// </summary>
    public sealed class Layer
    {
        /// <summary>
        /// Gets or sets the name of the layer.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the colour of the layer's items, as #RRGGBB.
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets whether the layer is shown.
        /// </summary>
        public bool Visible { get; set; } = true;
    }

    /// <summary>
    /// One shape of a scene: its library kind and the numbers that rebuild it.
    /// </summary>
    public sealed class SceneItem
    {
        /// <summary>
        /// Gets or sets the name shown in the scene tree.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets whether <see cref="Name"/> was made up by the writer (the kind and a number) because the script gave none. Such a name says only
        /// how many items of the kind came before, so it is not what two scenes are paired by.
        /// </summary>
        public bool AutoName { get; set; }

        /// <summary>
        /// Gets or sets the name of the layer the item is on.
        /// </summary>
        public string Layer { get; set; }

        /// <summary>
        /// Gets or sets the colour of this item as #RRGGBB, or null for the colour of its layer.
        /// </summary>
        public string Color { get; set; }

        /// <summary>
        /// Gets or sets the kind: the name of the library type (<c>GeoPolygonArc2</c>, <c>GeoSolid3</c>, ...), or
        /// <c>Text</c> for a label.
        /// </summary>
        public string Kind { get; set; }

        /// <summary>
        /// Gets or sets the numbers that rebuild the shape; what they are for each kind is set out in
        /// <see cref="SceneKinds"/>.
        /// </summary>
        public JsonElement Data { get; set; }

        /// <summary>
        /// Gets the properties the script attached to the item, as text. They are written with their keys sorted.
        /// </summary>
        public Dictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Rebuilds the library object the item holds, with the library's own constructors and the tolerance in force on
        /// this thread, so a caller reading a scene at its own tolerance opens a <c>Tolerance.Use</c> scope first.
        /// </summary>
        /// <returns>
        /// The object. A mesh comes back as the array of polygons of its faces, since the library builds a mesh only from a
        /// shape and offers no constructor to rebuild one. Whatever a library constructor does to what it is given, such as
        /// scaling a direction to length one again, it does here, so a direction already of length one can come back a
        /// last digit away.
        /// </returns>
        /// <exception cref="NotSupportedException">Thrown for a label, and for a kind the viewer does not know.</exception>
        /// <exception cref="FormatException">Thrown when the numbers are not those the kind needs.</exception>
        /// <exception cref="ArgumentException">Thrown when the library refuses the shape, as it does a polygon of two points.</exception>
        public object ToGeometry() => GeometryDecoder.Decode(this);
    }
}
