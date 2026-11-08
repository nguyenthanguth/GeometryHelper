using System;
using System.Collections.Generic;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Geometry;

namespace GeometryHelper.Drawing.Plugins
{
    /// <summary>
    /// What a drawing plugin is given for one run: the view as the drawing is made again, the objects it was given, the
    /// model, the drawing's settings, the run's settings and the tolerance, and the way to add dimensions to the view.
    /// </summary>
    /// <remarks>
    /// The dimensions added become the drawing's own, each with an id of its view, the run and its kind, as
    /// V-FRONT/R-1/D-HolesByGroup-X-1: written into the DXF tagged as the drawing's, edited in the presenter as any of its
    /// dimensions, and kept by a hand edit through later updates. They are made by <see cref="Annotator"/>, as the drawing's
    /// own are, so that they measure the model to the bit.
    /// </remarks>
    public sealed class DrawingPluginContext
    {
        private readonly List<DrawingDimension> _dimensions = new List<DrawingDimension>();
        private readonly DrawingSettings _settings;
        private readonly Dictionary<string, double> _values;

        internal DrawingPluginContext(DrawingPluginRun run, DrawingView view, IReadOnlyList<DrawingObject> objects, DrawingSource source, DrawingSettings settings, Dictionary<string, double> values, Tolerance tolerance)
        {
            RunId = run.Id;
            View = view;
            Objects = objects;
            Source = source;
            _settings = settings;
            _values = values;
            Tolerance = tolerance;
        }

        /// <summary>Gets the run's id in its drawing: R-1.</summary>
        public string RunId { get; }

        /// <summary>Gets the view the plugin runs in, its lines made again from the model as it is now.</summary>
        public DrawingView View { get; }

        /// <summary>
        /// Gets the objects the run was given, as the model has them now; where it was given none, every object of the view
        /// of the kinds the plugin asks for, in the order of the view.
        /// </summary>
        public IReadOnlyList<DrawingObject> Objects { get; }

        /// <summary>Gets the model the drawing is made from.</summary>
        public DrawingSource Source { get; }

        /// <summary>Gets a copy of the drawing's settings, which the plugin may change for its own use; the drawing keeps its own.</summary>
        public DrawingSettings Settings => _settings.Clone();

        /// <summary>Gets the tolerance the drawing is made within.</summary>
        public Tolerance Tolerance { get; }

        /// <summary>Gets the value of a setting the plugin declares, as the run has it.</summary>
        /// <param name="name">The setting's name.</param>
        /// <returns>The value.</returns>
        /// <exception cref="ArgumentException">Thrown when the plugin declares no setting of the name.</exception>
        public double Setting(string name)
        {
            if (name == null || !_values.TryGetValue(name, out double value))
            {
                throw new ArgumentException("The plugin declares no setting \"" + name + "\".", nameof(name));
            }

            return value;
        }

        /// <summary>Adds dimensions of the view, as <see cref="Annotator.Dimension"/> makes them from the view's lines.</summary>
        /// <param name="dimensions">The dimensions.</param>
        /// <exception cref="ArgumentNullException">Thrown when the dimensions or one of them is null.</exception>
        public void AddDimensions(IEnumerable<DrawingDimension> dimensions)
        {
            if (dimensions == null)
            {
                throw new ArgumentNullException(nameof(dimensions));
            }

            foreach (DrawingDimension dimension in dimensions)
            {
                _dimensions.Add(dimension ?? throw new ArgumentNullException(nameof(dimensions), "A dimension is null."));
            }
        }

        /// <summary>The dimensions added, in the order added.</summary>
        internal List<DrawingDimension> Dimensions => _dimensions;
    }
}
