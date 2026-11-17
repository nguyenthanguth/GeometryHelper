using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// COPY's Array and mOde options and the variable COPYMODE, as AutoCAD 2021 asks them: "Enter number of items to array:" and
    /// "Specify second point or [Fit]:", where the point is the step from one copy to the next, or with Fit the place of the
    /// last copy, the others spaced evenly between it and the base point.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>The key of COPYMODE in <see cref="CadCommandSession.Remembered"/>: 0 copies again and again (Multiple), 1 makes one copy (Single).</summary>
        private const string CopyModeKey = "copymode";

        /// <summary>The key of the number of items COPY's Array asked last in this run, offered again as its default.</summary>
        private const string CopyArrayKey = "copy.array";

        private static void AddCopyModesTo(CadCommandRegistry registry) =>
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "COPYMODE",
                    false,
                    0,
                    1,
                    s => (int)Kept(s, CopyModeKey, 0.0),
                    (c, v) => c.Remember(CopyModeKey, v)
                )
            );

        /// <summary>
        /// COPY's Array: the number of items, the original counted, from 2 to 32767 (a number outside is "Requires an integer
        /// between 2 and 32767." and asked again), then the second point; Fit and Array turn between the two ways of reading the
        /// point. Enter at the number without one offered, or at the point, makes none and goes back to COPY's prompt. The copies are added to
        /// <paramref name="made"/>.
        /// </summary>
        private static IEnumerable<CadPrompt> CopyArray(CadCommandContext c, GeoPoint2 from, List<string> made)
        {
            const string refused = "Requires an integer between 2 and 32767.";
            int last = (int)c.Remembered(CopyArrayKey, 0.0);
            int count;

            while (true)
            {
                yield return CadPrompt
                    .ForInteger(
                        "Enter number of items to array",
                        null,
                        last >= 2 ? last.ToString(CultureInfo.InvariantCulture) : null
                    )
                    .Refusing(refused);

                if (c.Input.IsNone && last >= 2)
                {
                    count = last;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number >= 2.0 && c.Input.Number <= MostItems)
                {
                    count = (int)c.Input.Number;
                    break;
                }

                c.Write(refused);
            }

            c.Remember(CopyArrayKey, count);
            bool fit = false;

            while (true)
            {
                yield return CadPrompt.ForPoint("Specify second point", from, new[] { fit ? "Array" : "Fit" });

                if (c.Input.Is("Fit") || c.Input.Is("Array"))
                {
                    fit = c.Input.Is("Fit");
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoVector2 step = c.Input.Point - from;

                if (fit)
                {
                    step = step / (count - 1);
                }

                for (int i = 1; i < count; i++)
                {
                    made.AddRange(
                        Transform(
                            c,
                            c.Selected,
                            GeoTransform3.Translation(new GeoVector3(step.X * i, step.Y * i, 0.0)),
                            true
                        )
                    );
                }

                yield break;
            }
        }
    }
}
