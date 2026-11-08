using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer.Plugins.UnitTest
{
    /// <summary>The kinds a choice setting of <see cref="TestTools"/> offers.</summary>
    public enum TestKind
    {
        /// <summary>The first.</summary>
        First,

        /// <summary>The second.</summary>
        Second,
    }

    /// <summary>
    /// The test assembly read as a plugin: DRAWLINE asks two points and draws a line, as C3 step 1 of the plan checks, and
    /// the others are each wrong in one way, or take a name the presenter has.
    /// </summary>
    public sealed class TestTools
    {
        /// <summary>Gets or sets a number from 0 to 10.</summary>
        [CadSetting("Offset", Minimum = 0.0, Maximum = 10.0, Description = "How far.")]
        public double Offset { get; set; } = 1.5;

        /// <summary>Gets or sets a whole number.</summary>
        [CadSetting("Count", Minimum = 1.0)]
        public int Count { get; set; } = 3;

        /// <summary>Gets or sets yes or no.</summary>
        [CadSetting("Show")]
        public bool Show { get; set; } = true;

        /// <summary>Gets or sets a choice.</summary>
        [CadSetting("Kind")]
        public TestKind Kind { get; set; } = TestKind.Second;

        /// <summary>Gets or sets a setting of a type a setting cannot have.</summary>
        [CadSetting("When")]
        public DateTime When { get; set; }

        /// <summary>Asks two points and draws a line between them.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("DRAWLINE", Alias = "DL", Description = "Draw a line through two points")]
        public IEnumerable<CadPrompt> DrawLine(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify first point");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 first = c.Input.Point;
            yield return CadPrompt.ForPoint("Specify second point", first);

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 second = c.Input.Point;
            c.Apply(null, s => s.Append(c.Space, new DxfLine(new GeoPoint3(first.X, first.Y, 0.0), new GeoPoint3(second.X, second.Y, 0.0))));
        }

        /// <summary>Adds the layer Temp through the document's own layer editor, not the context, then fails.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("LAYERTHENFAIL")]
        public IEnumerable<CadPrompt> LayerThenFail(CadCommandContext c)
        {
            CadLayerEditor.Add(c.Document, "Temp");
            yield return CadPrompt.ForString("Enter anything, and the command fails");
            throw new InvalidOperationException("Failed after a layer was added.");
        }

        /// <summary>Takes the name of a command of the presenter.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("MOVE")]
        public IEnumerable<CadPrompt> Move(CadCommandContext c)
        {
            yield break;
        }

        /// <summary>Takes the alias of MOVE as its own.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("TESTALIAS", Alias = "M")]
        public IEnumerable<CadPrompt> TakesAnAlias(CadCommandContext c)
        {
            c.Write("TESTALIAS ran.");
            yield break;
        }

        /// <summary>Blocks on its prompts, as AutoCAD's commands do.</summary>
        /// <param name="c">The context.</param>
        [CadCommand("VOIDCMD")]
        public void Blocking(CadCommandContext c)
        {
            c.Write("never");
        }

        /// <summary>Takes the wrong argument.</summary>
        /// <param name="text">A text.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("BADARGS")]
        public IEnumerable<CadPrompt> WrongArgument(string text)
        {
            yield break;
        }

        /// <summary>Takes the name of DRAWLINE again, in small letters.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("drawline")]
        public IEnumerable<CadPrompt> DrawLineAgain(CadCommandContext c)
        {
            yield break;
        }

        /// <summary>A static command.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("STATICCMD", Description = "Write a line")]
        public static IEnumerable<CadPrompt> Static(CadCommandContext c)
        {
            c.Write("STATICCMD ran.");
            yield break;
        }

        /// <summary>Not public, so not a command.</summary>
        /// <param name="c">The context.</param>
        /// <returns>The prompts.</returns>
        [CadCommand("HIDDEN")]
        internal IEnumerable<CadPrompt> Hidden(CadCommandContext c)
        {
            yield break;
        }
    }
}
