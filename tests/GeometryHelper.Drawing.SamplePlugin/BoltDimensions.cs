using System.Collections.Generic;
using GeometryHelper.Drawing.Annotation;
using GeometryHelper.Drawing.Plugins;

namespace GeometryHelper.Drawing.SamplePlugin
{
    /// <summary>
    /// The sample drawing plugin, Bolt dimensions: in the view picked, every row of bolts dimensioned by group, the distance
    /// from the end of the part the bolts sit in to the first, the pitches, and the distance from the last to the part's
    /// other end, as <see cref="Annotator"/>'s holes-by-group rule makes them. In the front view of TC5 it gives 50, 300 and
    /// 50 along the base plate; with the bolts moved 20 further apart and the drawing updated, 30, 340 and 30.
    /// </summary>
    public sealed class BoltDimensions : DrawingPlugin
    {
        /// <summary>Gets the plugin's name: Bolt dimensions.</summary>
        public override string Name => "Bolt dimensions";

        /// <summary>Asks for a view and its bolts (every bolt of the view where none are picked), and the rounding of the texts.</summary>
        /// <param name="input">The input.</param>
        public override void DefineInput(DrawingPluginInput input)
        {
            input.AddView("Pick a view");
            input.AddObjects("Pick bolts", DrawingObjectKind.Bolt);
            input.AddSetting("Rounding", 1.0, 0.01, 100.0);
        }

        /// <summary>Dimensions the rows of the bolts given, with the parts of the view they sit in.</summary>
        /// <param name="context">The run's context.</param>
        public override void Run(DrawingPluginContext context)
        {
            var objects = new List<DrawingObject>(context.Objects);

            foreach (string id in context.View.ObjectIds)
            {
                if (context.Source.TryGetObject(id, out DrawingObject part) && part.Kind == DrawingObjectKind.Part)
                {
                    objects.Add(part);
                }
            }

            DimensionSettings settings = context.Settings.Dimensions;
            settings.Scale = context.View.Scale;
            settings.Rounding = context.Setting("Rounding");
            settings.Overall = false;
            settings.Chain = false;
            settings.Cumulative = false;
            settings.Ordinate = false;
            settings.HolesByGroup = true;
            context.AddDimensions(
                Annotator.Dimension(
                    context.View.Lines,
                    objects,
                    context.View.Frame.CoordinateSystem,
                    settings,
                    context.Tolerance
                )
            );
        }
    }
}
