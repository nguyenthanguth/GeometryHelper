using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Dxf.Viewer;
using GeometryHelper.Dxf.Viewer.Plugins;

[assembly: CadPluginApi("1.0")]

namespace GeometryHelper.Dxf.Viewer.SamplePlugin
{
    /// <summary>
    /// The sample command plugin: NUMCIRCLES numbers the circles selected, a text at the centre of each, and FAILNOW fails
    /// on purpose, after an edit, to show that a failed run changes nothing.
    /// </summary>
    public sealed class CircleTools
    {
        /// <summary>What NUMCIRCLES writes when it is done; the plugin tests build a second version of the sample that writes another.</summary>
        private const string Done = "Numbered {0} circles.";

        /// <summary>Gets or sets how high the numbers are written: 2.5 until changed.</summary>
        [CadSetting("Text height", Minimum = 0.1, Maximum = 1000, Description = "How high the numbers are written.")]
        public double TextHeight { get; set; } = 2.5;

        /// <summary>Gets or sets the layer the numbers are put on, made where the drawing has none: Mark until changed.</summary>
        [CadSetting("Layer", Description = "The layer the numbers are put on.")]
        public string Layer { get; set; } = "Mark";

        /// <summary>
        /// Numbers the circles selected, in the order they were drawn: asks for the circles (those selected before are
        /// taken), then the number to start at, and writes a TEXT of each number centred on its circle, on <see cref="Layer"/>.
        /// Three circles started at 7 are numbered 7, 8 and 9, in one undo step.
        /// </summary>
        /// <param name="c">The command's context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("NUMCIRCLES", Alias = "NC", Description = "Number the selected circles", UsesSelection = true)]
        public IEnumerable<CadPrompt> NumberCircles(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            var circles = new List<DxfCircle>();

            foreach (string handle in c.Selected)
            {
                if (c.Document.FindEntity(handle) is DxfCircle circle)
                {
                    circles.Add(circle);
                }
            }

            if (circles.Count == 0)
            {
                c.Write("No circle was selected.");
                yield break;
            }

            yield return CadPrompt.ForInteger("Start at", null, "1");
            int start = c.Input.Kind == CadInputKind.Number ? (int)c.Input.Number : 1;
            string layer = Layer;
            double height = TextHeight;
            bool newLayer = !c.Database.LayerTable.Has(layer);
            CadEditRecord[] planned = newLayer ? new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New layer", null, layer) } : null;

            // A refusal or failure is written on the command line by Apply itself, and nothing is changed.
            CadEditResult result = c.Apply(planned, s =>
            {
                if (newLayer)
                {
                    s.Database.LayerTable.Add(new DxfLayerTableRecord { Name = layer, ColorIndex = 7 });
                }

                int number = start;

                foreach (DxfCircle circle in circles)
                {
                    var text = new DxfDBText();
                    text.SetDatabaseDefaults(c.Database);
                    text.Layer = layer;
                    text.Height = height;
                    text.TextString = number.ToString(CultureInfo.InvariantCulture);
                    text.Justify = DxfAttachmentPoint.MiddleCenter;
                    text.AlignmentPoint = circle.Center;
                    s.Append(c.Space, text);
                    number++;
                }
            });

            if (result.Succeeded)
            {
                c.Write(string.Format(CultureInfo.InvariantCulture, Done, circles.Count));
            }
        }

        /// <summary>Adds a text at the origin, then fails on purpose: the text is taken back and the drawing stays as it was.</summary>
        /// <param name="c">The command's context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("FAILNOW", Description = "Fail on purpose, after an edit")]
        public IEnumerable<CadPrompt> FailNow(CadCommandContext c)
        {
            c.Apply(null, s =>
            {
                var text = new DxfDBText();
                text.SetDatabaseDefaults(c.Database);
                text.Height = TextHeight;
                text.TextString = "never kept";
                s.Append(c.Space, text);
            });

            yield return CadPrompt.ForString("Enter anything, and the command fails");
            throw new InvalidOperationException("The sample fails on purpose.");
        }
    }
}
