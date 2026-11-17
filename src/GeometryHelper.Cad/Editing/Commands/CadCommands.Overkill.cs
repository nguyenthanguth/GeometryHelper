using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// OVERKILL and -OVERKILL as AutoCAD 2021 asks them (probed through its console): the objects, then the settings, then the
    /// deletion of duplicates and of overlaps, written as "1 duplicate(s) deleted" and "2 overlapping object(s) or segment(s)
    /// deleted". The window's Delete Duplicate Objects dialog sets the settings; with no window, and for -OVERKILL, they are
    /// changed through the options. The settings stay with the session from one run to the next, as AutoCAD keeps them.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] OverkillKeywords =
        {
            "Done",
            "Ignore",
            "tOlerance",
            "optimize Plines",
            "combine parTial overlap",
            "combine Endtoend",
            "Associativity",
        };

        private static readonly string[] OverkillIgnoreKeywords =
        {
            "None",
            "All",
            "Color",
            "LAyer",
            "Ltype",
            "ltScale",
            "LWeight",
            "Thickness",
            "TRansparency",
            "plotSTyle",
            "Material",
        };

        private static IEnumerable<CadPrompt> OverkillDialog(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            var request = new CadOverkillRequest(c.Session.Overkill.Clone(), c.Selected.Count);

            if (!c.Session.Request("OVERKILL", c.Document, request))
            {
                foreach (CadPrompt prompt in OverkillOptions(c))
                {
                    yield return prompt;
                }

                yield break;
            }

            if (request.Action == CadDialogAction.Ok)
            {
                c.Session.Overkill = request.Settings;
                OverkillRun(c, request.Settings);
            }
        }

        private static IEnumerable<CadPrompt> OverkillTyped(CadCommandContext c)
        {
            foreach (CadPrompt prompt in c.SelectObjects())
            {
                yield return prompt;
            }

            if (c.Selected.Count == 0)
            {
                yield break;
            }

            foreach (CadPrompt prompt in OverkillOptions(c))
            {
                yield return prompt;
            }
        }

        /// <summary>The settings shown and the options offered until Done, then the run; the settings kept for the session once it is made.</summary>
        private static IEnumerable<CadPrompt> OverkillOptions(CadCommandContext c)
        {
            CadOverkillSettings settings = c.Session.Overkill.Clone();

            while (true)
            {
                c.Write("Current settings: " + settings.Describe());
                yield return CadPrompt.ForKeyword("Enter an option to change", OverkillKeywords, "done");

                if (c.Input.Is("Done") || c.Input.IsNone)
                {
                    break;
                }

                if (c.Input.Is("Ignore"))
                {
                    c.Write("Specify properties to ignore when comparing objects...");
                    yield return CadPrompt.ForKeyword(
                        string.Empty,
                        OverkillIgnoreKeywords,
                        CadOverkillSettings.IgnoreWords(settings.Ignore)
                    );

                    if (c.Input.Is("None"))
                    {
                        settings.Ignore = CadOverkillIgnore.None;
                    }
                    else if (c.Input.Is("All"))
                    {
                        settings.Ignore = CadOverkillIgnore.All;
                    }
                    else if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        foreach (CadOverkillIgnore one in CadOverkillSettings.Each)
                        {
                            if (c.Input.Is(one.ToString()))
                            {
                                settings.Ignore = one;
                            }
                        }
                    }
                }
                else if (c.Input.Is("tOlerance"))
                {
                    yield return CadPrompt
                        .ForDistance(
                            "Specify tolerance for object comparison precision",
                            null,
                            null,
                            settings.Tolerance.ToString("0.000000", CultureInfo.InvariantCulture)
                        )
                        .Refusing("Requires numeric value.");

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number >= 0.0)
                    {
                        settings.Tolerance = c.Input.Number;
                    }
                }
                else if (c.Input.Is("optimize Plines"))
                {
                    while (true)
                    {
                        yield return CadPrompt.ForKeyword(
                            "Optimize segments within plines",
                            new[] { "segment wiDth", "Break polyline", "Yes", "No" },
                            settings.OptimizePolylines ? "Yes" : "No"
                        );

                        if (c.Input.Is("segment wiDth"))
                        {
                            yield return CadPrompt.ForKeyword(
                                "Ignore polyline segment width",
                                new[] { "Yes", "No" },
                                settings.IgnorePolylineWidths ? "Yes" : "No"
                            );
                            settings.IgnorePolylineWidths = YesNo(c, settings.IgnorePolylineWidths);
                            continue;
                        }

                        if (c.Input.Is("Break polyline"))
                        {
                            yield return CadPrompt.ForKeyword(
                                "Break polylines",
                                new[] { "Yes", "No" },
                                settings.DoNotBreakPolylines ? "No" : "Yes"
                            );
                            settings.DoNotBreakPolylines = !YesNo(c, !settings.DoNotBreakPolylines);
                            continue;
                        }

                        settings.OptimizePolylines = YesNo(c, settings.OptimizePolylines);
                        break;
                    }
                }
                else if (c.Input.Is("combine parTial overlap"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Combine co-linear objects that partially overlap",
                        new[] { "Yes", "No" },
                        settings.CombinePartialOverlaps ? "Yes" : "No"
                    );
                    settings.CombinePartialOverlaps = YesNo(c, settings.CombinePartialOverlaps);
                }
                else if (c.Input.Is("combine Endtoend"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Combine co-linear objects when aligned end to end",
                        new[] { "Yes", "No" },
                        settings.CombineEndToEnd ? "Yes" : "No"
                    );
                    settings.CombineEndToEnd = YesNo(c, settings.CombineEndToEnd);
                }
                else if (c.Input.Is("Associativity"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Maintain associative objects",
                        new[] { "Yes", "No" },
                        settings.MaintainAssociativity ? "Yes" : "No"
                    );
                    settings.MaintainAssociativity = YesNo(c, settings.MaintainAssociativity);
                }
            }

            c.Session.Overkill = settings;
            OverkillRun(c, settings);
        }

        /// <summary>A Yes or No answered, the value kept where Enter took the default.</summary>
        private static bool YesNo(CadCommandContext c, bool current) =>
            c.Input.Is("Yes") ? true
            : c.Input.Is("No") ? false
            : current;

        /// <summary>Runs OVERKILL over the objects selected and writes what it deleted, as AutoCAD does.</summary>
        private static void OverkillRun(CadCommandContext c, CadOverkillSettings settings)
        {
            CadOverkillResult result = CadOverkill.Run(c, c.Selected, settings);
            c.Write(result.Duplicates.ToString(CultureInfo.InvariantCulture) + " duplicate(s) deleted");

            if (settings.CombinePartialOverlaps || settings.CombineEndToEnd)
            {
                c.Write(
                    result.Overlaps.ToString(CultureInfo.InvariantCulture)
                        + " overlapping object(s) or segment(s) deleted"
                );
            }
        }
    }
}
