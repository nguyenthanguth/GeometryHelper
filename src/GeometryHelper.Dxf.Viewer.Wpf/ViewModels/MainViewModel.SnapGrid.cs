using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// SNAP and GRID typed, with AutoCAD 2021's options and words. SNAP asks "Specify snap spacing or
    /// [ON/OFF/Aspect/Legacy/Style/Type] &lt;10.0000&gt;:", Aspect left out while the snap is isometric; GRID asks "Specify
    /// grid spacing(X) or [ON/OFF/Snap/Major/aDaptive/Limits/Follow/Aspect] &lt;10.0000&gt;:", the same. Every answer but
    /// OFF turns the snap or the grid on, as AutoCAD's does.
    /// </summary>
    public sealed partial class MainViewModel
    {
        private static readonly double Root3 = Math.Sqrt(3.0);

        /// <summary>
        /// Gets whether SNAP holds the cursor on the snap grid at all times, AutoCAD's SNAPGRIDLEGACY 1 as SNAP Legacy Yes sets
        /// it, kept in the settings file (<see cref="GlobalSettings.SnapGridLegacy"/>) as AutoCAD keeps it in the registry;
        /// off, SNAP holds the cursor only while a point is asked.
        /// </summary>
        public bool SnapGridLegacy => _settings.SnapGridLegacy;

        /// <summary>SNAPGRIDLEGACY set, by SNAP Legacy or SETVAR: kept in the settings file, the canvas following.</summary>
        private void SetSnapGridLegacy(bool on)
        {
            ChangeSettings(s => s.SnapGridLegacy = on);
            Raise(nameof(SnapGridLegacy));
        }

        /// <summary>
        /// SNAP typed: a spacing, which with an isometric snap is the vertical one (5 keeps 8.6603 by 2.5, the drawing holding
        /// half the vertical spacing); ON, OFF; Aspect, the horizontal and vertical spacings apart; Legacy, "Maintain legacy
        /// behavior of always snapping to the grid? [Yes/No] &lt;No&gt;:"; Style, Standard or Isometric, turning the snap and
        /// the grid spacing over as AutoCAD does; Type, Polar with its tracking distance (POLARDIST) or Grid (SNAPTYPE). The
        /// spacing offered is "A" where the two spacings differ, which Enter takes as Aspect. Nought or less is refused with
        /// "Value must be positive and nonzero." and asked again.
        /// </summary>
        private IEnumerable<CadPrompt> SnapBody(CadCommandContext c)
        {
            StatusToggleViewModel toggle = FindToggle("SNAP");

            while (true)
            {
                bool isometric = Layout.Isometric;
                string offered = isometric ? CadCommandContext.Length(2.0 * Snap.GridY) : SnapOffered();
                string[] keywords = isometric
                    ? new[] { "ON", "OFF", "Legacy", "Style", "Type" }
                    : new[] { "ON", "OFF", "Aspect", "Legacy", "Style", "Type" };
                yield return CadPrompt.ForDistance("Specify snap spacing", null, keywords, offered);

                if (c.Input.Is("ON") || c.Input.Is("OFF"))
                {
                    toggle.IsOn = c.Input.Is("ON");
                    yield break;
                }

                if (c.Input.Kind == CadInputKind.Number)
                {
                    if (!Positive(c, c.Input.Number))
                    {
                        continue;
                    }

                    SetSnapSquare(c.Input.Number, isometric);
                }
                else if (c.Input.Is("Aspect") || (c.Input.IsNone && offered == "A"))
                {
                    foreach (CadPrompt prompt in SnapAspect(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Legacy"))
                {
                    yield return CadPrompt.ForKeyword(
                        "Maintain legacy behavior of always snapping to the grid?",
                        new[] { "Yes", "No" },
                        SnapGridLegacy ? "Yes" : "No"
                    );

                    if (c.Input.Is("Yes") || c.Input.Is("No"))
                    {
                        SetSnapGridLegacy(c.Input.Is("Yes"));
                    }
                }
                else if (c.Input.Is("Style"))
                {
                    foreach (CadPrompt prompt in SnapStyle(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Type"))
                {
                    foreach (CadPrompt prompt in SnapType(c))
                    {
                        yield return prompt;
                    }
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }

                toggle.IsOn = true;
                yield break;
            }
        }

        /// <summary>The snap spacing SNAP offers while the snap is rectangular: the X spacing, or "A" where the Y spacing differs.</summary>
        private string SnapOffered() =>
            Math.Abs(Snap.GridX - Snap.GridY) <= Snap.Tolerance.EqualPoint ? CadCommandContext.Length(Snap.GridX) : "A";

        /// <summary>A spacing typed for SNAP: square, or for an isometric snap the vertical one, half of it kept.</summary>
        private void SetSnapSquare(double spacing, bool isometric)
        {
            if (isometric)
            {
                SetSnapSpacing(spacing * Root3, spacing / 2.0);
            }
            else
            {
                SetSnapSpacing(spacing, spacing);
            }
        }

        /// <summary>SNAP Aspect: "Specify horizontal spacing &lt;10.0000&gt;:", then "Specify vertical spacing", Enter keeping each.</summary>
        private IEnumerable<CadPrompt> SnapAspect(CadCommandContext c)
        {
            double x = Snap.GridX;
            double y = Snap.GridY;

            foreach (bool across in new[] { true, false })
            {
                while (true)
                {
                    double old = across ? x : y;
                    yield return CadPrompt.ForDistance(
                        across ? "Specify horizontal spacing" : "Specify vertical spacing",
                        null,
                        null,
                        CadCommandContext.Length(old)
                    );

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        if (!Positive(c, c.Input.Number))
                        {
                            continue;
                        }

                        x = across ? c.Input.Number : x;
                        y = across ? y : c.Input.Number;
                    }
                    else if (!c.Input.IsNone)
                    {
                        yield break;
                    }

                    break;
                }
            }

            SetSnapSpacing(x, y);
        }

        /// <summary>
        /// SNAP Style: "Enter snap grid style [Standard/Isometric] &lt;S&gt;:". Isometric asks the vertical spacing, offering the
        /// Y spacing as SNAPUNIT reads it, and keeps a snap of it times the root of 3 by half of it; a rectangular grid's
        /// spacing turns over the same way from its Y spacing (a grid of 6 is kept as 10.3923 by 3). Standard asks "Specify
        /// snap spacing or [Aspect]", offering twice the half kept, and the grid takes twice its kept Y spacing either way.
        /// </summary>
        private IEnumerable<CadPrompt> SnapStyle(CadCommandContext c)
        {
            bool isometric = Layout.Isometric;
            yield return CadPrompt.ForKeyword(
                "Enter snap grid style",
                new[] { "Standard", "Isometric" },
                isometric ? "I" : "S"
            );

            if (!c.Input.IsNone && !c.Input.Is("Isometric") && !c.Input.Is("Standard"))
            {
                yield break;
            }

            bool toIsometric = c.Input.IsNone ? isometric : c.Input.Is("Isometric");

            double vertical = isometric ? 2.0 * Snap.GridY : Snap.GridY;

            if (toIsometric)
            {
                while (true)
                {
                    yield return CadPrompt.ForDistance(
                        "Specify vertical spacing",
                        null,
                        null,
                        CadCommandContext.Length(vertical)
                    );

                    if (c.Input.Kind == CadInputKind.Number)
                    {
                        if (!Positive(c, c.Input.Number))
                        {
                            continue;
                        }

                        vertical = c.Input.Number;
                    }
                    else if (!c.Input.IsNone)
                    {
                        yield break;
                    }

                    break;
                }

                if (!isometric)
                {
                    double grid = Layout.GridY;
                    SetGridSpacing(grid * Root3, grid / 2.0);
                    SetIsometricSnap(true);
                }

                SetSnapSpacing(vertical * Root3, vertical / 2.0);
                yield break;
            }

            double offered = isometric ? vertical : Snap.GridX;

            while (true)
            {
                yield return CadPrompt.ForDistance(
                    "Specify snap spacing",
                    null,
                    new[] { "Aspect" },
                    CadCommandContext.Length(offered)
                );

                if (c.Input.Kind == CadInputKind.Number && !Positive(c, c.Input.Number))
                {
                    continue;
                }

                if (c.Input.Kind != CadInputKind.Number && !c.Input.IsNone && !c.Input.Is("Aspect"))
                {
                    yield break;
                }

                if (isometric)
                {
                    double grid = 2.0 * Layout.GridY;
                    SetGridSpacing(grid, grid);
                    SetIsometricSnap(false);
                }

                if (c.Input.Is("Aspect"))
                {
                    if (isometric)
                    {
                        SetSnapSpacing(offered, offered);
                    }

                    foreach (CadPrompt prompt in SnapAspect(c))
                    {
                        yield return prompt;
                    }

                    yield break;
                }

                double spacing = c.Input.Kind == CadInputKind.Number ? c.Input.Number : offered;
                SetSnapSpacing(spacing, spacing);
                yield break;
            }
        }

        /// <summary>
        /// SNAP Type: "Enter snap type [Polar/Grid] &lt;Grid&gt;:"; Polar then asks "Enter polar snap tracking distance
        /// &lt;0.0000&gt;:", POLARDIST, and steps the snap along the polar angles (SNAPTYPE 1); Grid puts it back on the grid.
        /// </summary>
        private IEnumerable<CadPrompt> SnapType(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter snap type",
                new[] { "Polar", "Grid" },
                _settings.PolarSnap ? "Polar" : "Grid"
            );

            bool polar = c.Input.Is("Polar") || (c.Input.IsNone && _settings.PolarSnap);

            if (!polar)
            {
                if (c.Input.Is("Grid") || c.Input.IsNone)
                {
                    SetPolarSnap(false);
                }

                yield break;
            }

            SetPolarSnap(true);
            yield return CadPrompt.ForDistance(
                "Enter polar snap tracking distance",
                null,
                null,
                CadCommandContext.Length(_settings.PolarDistance)
            );

            if (c.Input.Kind == CadInputKind.Number)
            {
                SetPolarDistance(c.Input.Number);
            }
        }

        /// <summary>
        /// GRID typed: a spacing, square or, while the snap is isometric, the vertical one kept as SNAP keeps it; a number
        /// followed by x, a multiple of the snap spacing (2x with a snap of 10 a grid of 20); nought for the snap's; ON, OFF;
        /// Snap, the snap's spacing; Major, GRIDMAJOR; aDaptive, Limits and Follow, the bits of GRIDDISPLAY; Aspect, the two
        /// spacings apart, not offered with an isometric snap. A spacing below nought is refused with "Value must be
        /// positive.", text with "Requires a distance, numberX, or option keyword.", and asked again.
        /// </summary>
        private IEnumerable<CadPrompt> GridBody(CadCommandContext c)
        {
            StatusToggleViewModel toggle = FindToggle("GRID");

            while (true)
            {
                bool isometric = Layout.Isometric;
                string offered = isometric ? CadCommandContext.Length(2.0 * Layout.GridY) : GridOffered();
                var keywords = new List<string> { "ON", "OFF", "Snap", "Major", "aDaptive", "Limits", "Follow" };

                if (!isometric)
                {
                    keywords.Add("Aspect");
                }

                // A point prompt that takes text, as ZOOM's, so that 2x reaches the command.
                yield return CadPrompt.ForPoint("Specify grid spacing(X)", null, keywords, offered, true);

                if (c.Input.Is("ON") || c.Input.Is("OFF"))
                {
                    toggle.IsOn = c.Input.Is("ON");
                    yield break;
                }

                if (c.Input.Kind == CadInputKind.Text || c.Input.IsPoint)
                {
                    if (!GridSpacingTyped(c, out double spacing, out bool times))
                    {
                        continue;
                    }

                    if (times)
                    {
                        SetGridSpacing(spacing * Snap.GridX, spacing * Snap.GridY);
                    }
                    else if (isometric)
                    {
                        SetGridSpacing(spacing * Root3, spacing / 2.0);
                    }
                    else
                    {
                        SetGridSpacing(spacing, spacing);
                    }
                }
                else if (c.Input.Is("Snap"))
                {
                    SetGridSpacing(0.0, 0.0);
                }
                else if (c.Input.Is("Major"))
                {
                    foreach (CadPrompt prompt in GridMajorAsked(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("aDaptive"))
                {
                    foreach (CadPrompt prompt in GridAdaptive(c))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Limits"))
                {
                    foreach (CadPrompt prompt in GridBit(c, "Display grid beyond Limits", 1))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Follow"))
                {
                    foreach (CadPrompt prompt in GridBit(c, "Follow dynamic UCS", 8))
                    {
                        yield return prompt;
                    }
                }
                else if (c.Input.Is("Aspect") || (c.Input.IsNone && offered == "A"))
                {
                    foreach (CadPrompt prompt in GridAspect(c))
                    {
                        yield return prompt;
                    }
                }
                else if (!c.Input.IsNone)
                {
                    yield break;
                }

                toggle.IsOn = true;
                yield break;
            }
        }

        /// <summary>The grid spacing GRID offers while the snap is rectangular: the X spacing, or "A" where the Y spacing differs.</summary>
        private string GridOffered() =>
            Math.Abs(Layout.GridX - Layout.GridY) <= Snap.Tolerance.EqualPoint
                ? CadCommandContext.Length(Layout.GridX)
                : "A";

        /// <summary>
        /// A grid spacing typed: a number from nought up, or one followed by x, a multiple of the snap spacing; below nought
        /// refused with "Value must be positive.", anything else with "Requires a distance, numberX, or option keyword.".
        /// </summary>
        private static bool GridSpacingTyped(CadCommandContext c, out double spacing, out bool times)
        {
            spacing = 0.0;
            times = false;
            string typed = c.Input.IsPoint ? string.Empty : c.Input.Text.Trim();

            if (typed.EndsWith("x", StringComparison.OrdinalIgnoreCase))
            {
                times = true;
                typed = typed.Substring(0, typed.Length - 1);
            }

            if (
                !double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out spacing)
                || double.IsNaN(spacing)
                || double.IsInfinity(spacing)
            )
            {
                c.Write("Requires a distance, numberX, or option keyword.");
                return false;
            }

            if (spacing < 0.0)
            {
                c.Write("Value must be positive.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// GRID Aspect: "Specify the horizontal spacing(X) &lt;10.0000&gt;:", then "Specify the vertical spacing(Y)", each a
        /// distance from nought up or a multiple of the snap spacing along its axis, Enter keeping it.
        /// </summary>
        private IEnumerable<CadPrompt> GridAspect(CadCommandContext c)
        {
            double x = Layout.GridX;
            double y = Layout.GridY;

            foreach (bool across in new[] { true, false })
            {
                while (true)
                {
                    yield return CadPrompt.ForPoint(
                        across ? "Specify the horizontal spacing(X)" : "Specify the vertical spacing(Y)",
                        null,
                        null,
                        CadCommandContext.Length(across ? x : y),
                        true
                    );

                    if (c.Input.Kind == CadInputKind.Text || c.Input.IsPoint)
                    {
                        if (!GridSpacingTyped(c, out double spacing, out bool times))
                        {
                            continue;
                        }

                        double value = times ? spacing * (across ? Snap.GridX : Snap.GridY) : spacing;
                        x = across ? value : x;
                        y = across ? y : value;
                    }
                    else if (!c.Input.IsNone)
                    {
                        yield break;
                    }

                    break;
                }
            }

            SetGridSpacing(x, y);
        }

        /// <summary>
        /// GRID Major: "Enter the number of grid divisions per major grid line &lt;5&gt;:", 1 to 100, anything else refused with
        /// "Requires an integer between 1 and 100." and asked again.
        /// </summary>
        private IEnumerable<CadPrompt> GridMajorAsked(CadCommandContext c)
        {
            while (true)
            {
                yield return CadPrompt.ForInteger(
                    "Enter the number of grid divisions per major grid line",
                    null,
                    GridMajor.ToString(CultureInfo.InvariantCulture)
                );

                if (c.Input.Kind != CadInputKind.Number)
                {
                    yield break;
                }

                if (c.Input.Number < 1.0 || c.Input.Number > 100.0)
                {
                    c.Write("Requires an integer between 1 and 100.");
                    continue;
                }

                SetGridMajor((int)c.Input.Number);
                yield break;
            }
        }

        /// <summary>
        /// GRID aDaptive: "Turn adaptive behavior on [Yes/No] &lt;Yes&gt;:" (GRIDDISPLAY 2), and where Yes, "Allow subdivision
        /// below grid spacing [Yes/No] &lt;No&gt;:" (GRIDDISPLAY 4).
        /// </summary>
        private IEnumerable<CadPrompt> GridAdaptive(CadCommandContext c)
        {
            foreach (CadPrompt prompt in GridBit(c, "Turn adaptive behavior on", 2))
            {
                yield return prompt;
            }

            if ((GridDisplay & 2) != 0 && (c.Input.Is("Yes") || c.Input.IsNone))
            {
                foreach (CadPrompt prompt in GridBit(c, "Allow subdivision below grid spacing", 4))
                {
                    yield return prompt;
                }
            }
        }

        /// <summary>A bit of GRIDDISPLAY asked Yes or No, the bit set offered.</summary>
        private IEnumerable<CadPrompt> GridBit(CadCommandContext c, string message, int bit)
        {
            yield return CadPrompt.ForKeyword(message, new[] { "Yes", "No" }, (GridDisplay & bit) != 0 ? "Yes" : "No");

            if (c.Input.Is("Yes") || c.Input.Is("No"))
            {
                int bits = c.Input.Is("Yes") ? GridDisplay | bit : GridDisplay & ~bit;

                if (bits != GridDisplay)
                {
                    SetGridDisplay(bits);
                }
            }
        }

        /// <summary>A spacing above nought, or "Value must be positive and nonzero." written.</summary>
        private static bool Positive(CadCommandContext c, double value)
        {
            if (value > 0.0 && !double.IsInfinity(value))
            {
                return true;
            }

            c.Write("Value must be positive and nonzero.");
            return false;
        }
    }
}
