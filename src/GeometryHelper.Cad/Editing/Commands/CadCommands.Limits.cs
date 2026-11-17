using System.Collections.Generic;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// LIMITS: the grid limits of model space, the rectangle the grid shows and LIMCHECK keeps new points inside
    /// (<see cref="CadLimits"/>). It asks "Reset Model space limits:" and "Specify lower left corner or [ON/OFF]
    /// &lt;0.0000,0.0000&gt;:", then "Specify upper right corner &lt;12.0000,9.0000&gt;:"; ON and OFF switch LIMCHECK, as AutoCAD's
    /// do; an upper right corner that is not above and to the right of the lower left is refused with "Invalid limits." and the
    /// questions are asked again. Both limits change in one undo step. In a layout the limits are those of its sheet, which
    /// PAGESETUP sets: only ON and OFF work there.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Limits(CadCommandContext c)
        {
            bool model = c.Space.IsModel;

            while (true)
            {
                CadLimits.Of(c.Document, out GeoPoint2 min, out GeoPoint2 max);
                c.Write("Reset " + (model ? "Model" : "Paper") + " space limits:");
                yield return CadPrompt.ForPoint(
                    "Specify lower left corner",
                    null,
                    new[] { "ON", "OFF" },
                    Length(min.X) + "," + Length(min.Y)
                );

                if (c.Input.Is("ON") || c.Input.Is("OFF"))
                {
                    bool on = c.Input.Is("ON");
                    SetSwitch(c, "$LIMCHECK", c.Database.Limcheck, on, d => d.Limcheck = on);
                    yield break;
                }

                GeoPoint2 lower;

                if (c.Input.IsNone)
                {
                    lower = min;
                }
                else if (c.Input.IsPoint)
                {
                    lower = c.Input.Point;
                }
                else
                {
                    yield break;
                }

                if (!model)
                {
                    c.Write("The limits of a layout are those of its sheet: PAGESETUP sets them.");
                    yield break;
                }

                yield return CadPrompt.ForPoint(
                    "Specify upper right corner",
                    lower,
                    null,
                    Length(max.X) + "," + Length(max.Y)
                );

                GeoPoint2 upper;

                if (c.Input.IsNone)
                {
                    upper = max;
                }
                else if (c.Input.IsPoint)
                {
                    upper = c.Input.Point;
                }
                else
                {
                    yield break;
                }

                if (!(upper.X > lower.X) || !(upper.Y > lower.Y))
                {
                    c.Write("Invalid limits.");
                    continue;
                }

                if (lower.Equals(min) && upper.Equals(max))
                {
                    yield break;
                }

                c.Apply(
                    new[]
                    {
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "$LIMMIN",
                            Values(min.X, min.Y),
                            Values(lower.X, lower.Y)
                        ),
                        new CadEditRecord(
                            CadEditRecordKind.SettingChanged,
                            null,
                            "$LIMMAX",
                            Values(max.X, max.Y),
                            Values(upper.X, upper.Y)
                        ),
                    },
                    s =>
                    {
                        s.Database.Limmin = lower;
                        s.Database.Limmax = upper;
                    }
                );
                yield break;
            }
        }
    }
}
