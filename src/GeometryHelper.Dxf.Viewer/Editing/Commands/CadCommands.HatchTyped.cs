using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// -HATCH, the command-line HATCH: points picked or objects selected, its Properties, Advanced options and draw order
    /// typed, in the words of AutoCAD 2021's.
    /// </summary>
    internal static partial class CadCommands
    {
        private const string HatchIslandsKey = "hatch.islands";
        private const string HatchRetainKey = "hatch.retain";
        private const string HatchSeparateKey = "hatch.separate";
        private const string HatchOrderKey = "hatch.order";

        /// <summary>The draw orders -HATCH offers, as AutoCAD writes them; the fourth, behind the boundary, is its start.</summary>
        private static readonly string[] HatchOrders =
        {
            "do Not assign",
            "send to Back",
            "bring to Front",
            "send beHind boundary",
            "bring in front of bounDary",
        };

        /// <summary>
        /// -HATCH: "Current hatch pattern:  ANSI31", then "Specify internal point or [Properties/Select objects/draW
        /// boundary/remove Boundaries/Advanced/DRaw order/Origin/ANnotative/hatch COlor/LAyer/Transparency]:" until Enter, each
        /// point finding the area round it with its islands (Undo takes the last back; a point in an area already specified
        /// says so), Select objects taking closed objects, Properties asking the pattern typed
        /// (<see cref="HatchPropertiesTyped"/>), draW boundary a shape drawn point by point (<see cref="HatchDrawBoundary"/>),
        /// remove Boundaries taking loops out (<see cref="HatchRemoveBoundaries"/>), Advanced options Boundary set, Retain
        /// boundary, Island detection, Style, Associativity, Gap tolerance and separate Hatches, DRaw order where the hatch goes
        /// in the draw order (behind its boundary as AutoCAD starts), Origin, hatch COlor, LAyer and Transparency (kept in the
        /// document's <see cref="CadHatchSettings"/>, as AutoCAD keeps HPCOLOR and the rest), and ANnotative, which makes each
        /// hatch annotative at the annotation scale of its space. Enter makes the hatch, or a hatch of each area where they are to be separate, as one undo step. The
        /// settings are the document's, as HATCH's are.
        /// </summary>
        private static IEnumerable<CadPrompt> HatchTyped(CadCommandContext c)
        {
            CadHatchSettings settings = c.Document.HatchSettings.Clone();
            var picks = new List<IReadOnlyList<CadBoundaryLoop>>();
            var run = new HatchRun();

            while (true)
            {
                c.Write("Current hatch pattern:  " + HatchPatternShown(settings));
                yield return CadPrompt
                    .ForPoint(
                        "Specify internal point",
                        null,
                        new[]
                        {
                            "Properties",
                            "Select objects",
                            "draW boundary",
                            "remove Boundaries",
                            "Advanced",
                            "DRaw order",
                            "Origin",
                            "ANnotative",
                            "hatch COlor",
                            "LAyer",
                            "Transparency",
                        }
                    )
                    .Hiding("Undo");

                if (c.Input.IsPoint)
                {
                    c.Write("Selecting everything visible...");
                    c.Write("Analyzing the selected data...");
                    c.Write("Analyzing internal islands...");
                    IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.Find(
                        c.Document,
                        c.Space,
                        c.Input.Point,
                        c.Session.Tolerance,
                        c.Session.WorkView?.VisibleBox,
                        run.Candidates,
                        settings.GapTolerance
                    );

                    if (loops == null)
                    {
                        c.Write("Valid hatch boundary not found.");
                    }
                    else if (Specified(picks, loops[0], c.Session.Tolerance))
                    {
                        c.Write("This area has already been specified.");
                    }
                    else
                    {
                        picks.Add(
                            c.Remembered(HatchIslandsKey, 1.0) != 0.0 ? loops : loops.Where(l => l.Depth == 0).ToList()
                        );
                    }
                }
                else if (c.Input.Is("Undo"))
                {
                    if (picks.Count > 0)
                    {
                        picks.RemoveAt(picks.Count - 1);
                    }
                    else
                    {
                        c.Write("Nothing to undo.");
                    }
                }
                else if (c.Input.Is("Properties"))
                {
                    foreach (CadPrompt prompt in HatchPropertiesTyped(c, settings))
                    {
                        yield return prompt;
                    }

                    c.Document.HatchSettings = settings.Clone();
                }
                else if (c.Input.Is("Select objects"))
                {
                    yield return CadPrompt.ForSelection();
                    IReadOnlyList<CadBoundaryLoop> loops = CadBoundaryFinder.FromObjects(
                        c.Document,
                        c.Input.Handles,
                        c.Session.Tolerance,
                        out int passed
                    );

                    if (passed > 0)
                    {
                        c.Write(
                            passed.ToString(CultureInfo.InvariantCulture)
                                + (passed == 1 ? " object is" : " objects are")
                                + " not closed and cannot be a hatch boundary."
                        );
                    }

                    if (loops.Count > 0)
                    {
                        picks.Add(loops);
                    }
                }
                else if (c.Input.Is("draW boundary"))
                {
                    foreach (CadPrompt prompt in HatchDrawBoundary(c, settings, run, picks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("remove Boundaries"))
                {
                    foreach (CadPrompt prompt in HatchRemoveBoundaries(c, picks))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Advanced"))
                {
                    foreach (CadPrompt prompt in HatchAdvanced(c, settings, run))
                    {
                        yield return prompt;
                    }

                    c.Document.HatchSettings = settings.Clone();
                }
                else if (c.Input.Is("DRaw order"))
                {
                    int order = (int)c.Remembered(HatchOrderKey, 3.0);
                    yield return CadPrompt.ForKeyword("Enter draw order", HatchOrders, HatchOrders[order]);
                    int chosen = Array.FindIndex(HatchOrders, o => c.Input.Is(o));

                    if (chosen >= 0)
                    {
                        c.Remember(HatchOrderKey, chosen);
                    }
                }
                else if (c.Input.Is("Origin"))
                {
                    foreach (CadPrompt prompt in HatchOrigin(c, settings, run))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("ANnotative"))
                {
                    foreach (CadPrompt prompt in HatchAnnotative(c, settings))
                    {
                        yield return prompt;
                    }

                    c.Document.HatchSettings = settings.Clone();
                }
                else if (c.Input.Is("hatch COlor"))
                {
                    foreach (CadPrompt prompt in HatchColors(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("LAyer"))
                {
                    foreach (CadPrompt prompt in HatchLayer(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Transparency"))
                {
                    foreach (CadPrompt prompt in HatchTransparency(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else
                {
                    break;
                }
            }

            if (picks.Count > 0)
            {
                MakeHatches(
                    c,
                    picks,
                    settings,
                    c.Remembered(HatchSeparateKey, 0.0) != 0.0,
                    c.Remembered(HatchRetainKey, 0.0) != 0.0,
                    (int)c.Remembered(HatchOrderKey, 3.0),
                    run
                );
            }
        }

        /// <summary>
        /// The colours of a gradient asked as "Single color" and "Two color" ask them: "New gradient color
        /// [Truecolor/COlorbook] &lt;0,0,255&gt;:" and, for two colours, "New gradient second color:[Truecolor/COlorbook]
        /// &lt;255,255,153&gt;:", or, for one, "Specify a shade or tint value (0-1) &lt;1.0000&gt;:". Each colour is a name from
        /// red to white, an index 1 to 255, Truecolor and its "Red,Green,Blue:", or red, green and blue as 255,128,0; a colour
        /// that is none is said so and asked again, and one of a gradient is of its own, not ByLayer or ByBlock. A tint outside 0
        /// to 1 is taken as the nearer end.
        /// </summary>
        private static IEnumerable<CadPrompt> GradientColorsTyped(
            CadCommandContext c,
            CadHatchSettings settings,
            bool one
        )
        {
            foreach (
                CadPrompt prompt in ColorAsked(
                    c,
                    "New gradient color [Truecolor/COlorbook]",
                    HatchColorOffered(settings.GradientColor1),
                    false,
                    false,
                    color => settings.GradientColor1 = color
                )
            )
            {
                yield return prompt;
            }

            if (!one)
            {
                foreach (
                    CadPrompt prompt in ColorAsked(
                        c,
                        "New gradient second color:[Truecolor/COlorbook]",
                        HatchColorOffered(settings.GradientColor2),
                        false,
                        false,
                        color => settings.GradientColor2 = color
                    )
                )
                {
                    yield return prompt;
                }

                yield break;
            }

            while (true)
            {
                yield return CadPrompt.ForString(
                    "Specify a shade or tint value (0-1)",
                    settings.GradientTint.ToString("0.0000", CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                if (
                    double.TryParse(
                        c.Input.Text.Trim(),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out double tint
                    )
                    && !double.IsNaN(tint)
                    && !double.IsInfinity(tint)
                )
                {
                    settings.GradientTint = Math.Max(0.0, Math.Min(1.0, tint));
                    yield break;
                }

                c.Write("Requires numeric value.");
            }
        }

        /// <summary>The name "Current hatch pattern" gives: SOLID, _USER, the gradient's or the pattern's.</summary>
        private static string HatchPatternShown(CadHatchSettings settings) =>
            settings.Type == CadHatchType.Solid ? "SOLID"
            : settings.Type == CadHatchType.UserDefined ? "_USER"
            : settings.Type == CadHatchType.Gradient ? settings.GradientName
            : settings.PatternName;

        /// <summary>Whether the outer loop of a new area is one of the loops of the picks already made: the same objects round the same area.</summary>
        private static bool Specified(
            List<IReadOnlyList<CadBoundaryLoop>> picks,
            CadBoundaryLoop outer,
            Tolerance tolerance
        )
        {
            string sources = string.Join(",", outer.Sources.OrderBy(s => s, StringComparer.Ordinal));

            foreach (IReadOnlyList<CadBoundaryLoop> pick in picks)
            {
                foreach (CadBoundaryLoop loop in pick)
                {
                    if (
                        Math.Abs(loop.Area - outer.Area) <= Math.Max(tolerance.EqualPoint, 1E-9) * 1E3
                        && string.Join(",", loop.Sources.OrderBy(s => s, StringComparer.Ordinal)) == sources
                    )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The Advanced options of -HATCH, each asked as AutoCAD asks it, until Enter: Boundary set
        /// (<see cref="HatchBoundarySet"/>), "Retain derived boundaries? [Yes/No] &lt;N&gt;:", "Do you want island detection?
        /// [Yes/No] &lt;Y&gt;:", "Enter hatching style [Ignore/Outer/Normal] &lt;Normal&gt;:", "Do you want associativity?
        /// [Yes/No] &lt;Y&gt;:", Gap tolerance (<see cref="HatchGapTolerance"/>) and "Create separate hatches? [Yes/No]
        /// &lt;N&gt;:".
        /// </summary>
        private static IEnumerable<CadPrompt> HatchAdvanced(
            CadCommandContext c,
            CadHatchSettings settings,
            HatchRun run
        )
        {
            while (true)
            {
                yield return CadPrompt.ForKeyword(
                    "Enter an option",
                    new[]
                    {
                        "Boundary set",
                        "Retain boundary",
                        "Island detection",
                        "Style",
                        "Associativity",
                        "Gap tolerance",
                        "separate Hatches",
                    }
                );

                if (c.Input.Is("Boundary set"))
                {
                    foreach (CadPrompt prompt in HatchBoundarySet(c, run))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Gap tolerance"))
                {
                    foreach (CadPrompt prompt in HatchGapTolerance(c, settings))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Retain boundary"))
                {
                    foreach (CadPrompt prompt in YesNoKept(c, "Retain derived boundaries?", HatchRetainKey, 0.0))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Island detection"))
                {
                    foreach (CadPrompt prompt in YesNoKept(c, "Do you want island detection?", HatchIslandsKey, 1.0))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Style"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Enter hatching style",
                        new[] { "Ignore", "Outer", "Normal" },
                        settings.IslandStyle.ToString()
                    );

                    if (c.Input.Is("Ignore"))
                    {
                        settings.IslandStyle = DxfHatchStyle.Ignore;
                    }
                    else if (c.Input.Is("Outer"))
                    {
                        settings.IslandStyle = DxfHatchStyle.Outer;
                    }
                    else if (c.Input.Is("Normal"))
                    {
                        settings.IslandStyle = DxfHatchStyle.Normal;
                    }
                }
                else if (c.Input.Is("Associativity"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Do you want associativity?",
                        new[] { "Yes", "No" },
                        settings.Associative ? "Y" : "N"
                    );

                    if (c.Input.Is("Yes") || c.Input.Is("No"))
                    {
                        settings.Associative = c.Input.Is("Yes");
                    }
                }
                else if (c.Input.Is("separate Hatches"))
                {
                    foreach (CadPrompt prompt in YesNoKept(c, "Create separate hatches?", HatchSeparateKey, 0.0))
                    {
                        yield return prompt;
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>A Yes/No asked and kept in the session's memory under a key; the start is shown as its letter, as AutoCAD writes &lt;Y&gt;.</summary>
        private static IEnumerable<CadPrompt> YesNoKept(CadCommandContext c, string message, string key, double start)
        {
            yield return CadPrompt.ForKeyword(
                message,
                new[] { "Yes", "No" },
                c.Remembered(key, start) != 0.0 ? "Y" : "N"
            );

            if (c.Input.Is("Yes") || c.Input.Is("No"))
            {
                c.Remember(key, c.Input.Is("Yes") ? 1.0 : 0.0);
            }
        }

        /// <summary>
        /// Makes the hatches of the picks as one undo step: one hatch of every pick, or one of each pick where they are to be
        /// separate; the loops of each kept as its island style keeps them; polylines of the loops kept as well where the
        /// derived boundaries are retained; each hatch put in the draw order <paramref name="order"/> names (an index of
        /// <see cref="HatchOrders"/>): behind or in front of the lowest or topmost object of its boundary, or to the back or the front.
        /// </summary>
        private static void MakeHatches(
            CadCommandContext c,
            List<IReadOnlyList<CadBoundaryLoop>> picks,
            CadHatchSettings settings,
            bool separate,
            bool retain,
            int order,
            HatchRun run
        )
        {
            var groups = new List<List<IReadOnlyList<CadBoundaryLoop>>>();

            if (separate)
            {
                groups.AddRange(picks.Select(p => new List<IReadOnlyList<CadBoundaryLoop>> { p }));
            }
            else
            {
                groups.Add(picks);
            }

            string fault = null;
            CadEditResult result = c.Apply(null, s =>
            {
                foreach (List<IReadOnlyList<CadBoundaryLoop>> group in groups)
                {
                    List<CadBoundaryLoop> loops = KeptLoops(group, settings);
                    DxfHatch hatch = AppendHatch(c, s, loops, settings, run.OriginOnce, out string made);
                    fault = fault ?? made;

                    // The polylines of the loops derived are kept where asked, those drawn where draW boundary was asked to.
                    foreach (CadBoundaryLoop loop in loops)
                    {
                        bool drawn = run.Drawn.Contains(loop);

                        // A loop of an ellipse or a spline has no corners to make a polyline of.
                        if (loop.Vertices.Count > 0 && (drawn ? run.RetainDrawn : retain))
                        {
                            DxfPolyline kept = Current(c, LoopPolyline(loop));
                            kept.Closed = !run.Open.Contains(loop);
                            s.Append(c.Space, kept);
                        }
                    }

                    PlaceHatch(c, s, hatch, loops, order);
                }
            });

            if (fault != null)
            {
                c.Write(fault);
            }

            if (!result.Succeeded && result.Message != null)
            {
                c.Write(result.Message);
            }
        }
    }
}
