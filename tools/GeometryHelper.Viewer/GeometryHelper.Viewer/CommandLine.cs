using System;
using System.Globalization;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What the viewer was started with: <c>GeometryHelper.Viewer.exe [file] [--check] [--screenshot png] [--view v] [--mode m] [--views n] [--scale n] [--background b] [--do commands]</c>.
    /// </summary>
    internal sealed class CommandLine
    {
        /// <summary>
        /// Gets the script or scene file to open, or null.
        /// </summary>
        internal string File { get; private set; }

        /// <summary>
        /// Gets whether to load the file and say whether it loads, without a window.
        /// </summary>
        internal bool Check { get; private set; }

        /// <summary>
        /// Gets the path of the picture to write, or null; the viewer then opens the file, fits it, writes the picture and exits.
        /// </summary>
        internal string Screenshot { get; private set; }

        /// <summary>
        /// Gets the standard view to take the picture from: iso, top, front or right.
        /// </summary>
        internal string View { get; private set; }

        /// <summary>
        /// Gets the render mode of the picture: wireframe, shaded, edges or transparent.
        /// </summary>
        internal string Mode { get; private set; }

        /// <summary>
        /// Gets how many views the window shows, 1, 2 or 4, or null for the one view it starts with: <c>--views 4</c> takes the picture of the plan, the front, the right and the iso.
        /// </summary>
        internal int? Views { get; private set; }

        /// <summary>
        /// Gets how many times the size of the window the picture is: 1, 2 or 4.
        /// </summary>
        internal int Scale { get; private set; } = 1;

        /// <summary>
        /// Gets what is behind the scene in the picture: the colour of the views in the window unless it says white or transparent.
        /// </summary>
        internal ScreenshotBackground Background { get; private set; } = ScreenshotBackground.Window;

        /// <summary>
        /// Gets the commands to do after the view is fitted and before the picture is taken, or null: a box select, a hover, a measurement
        /// done through the code a click goes through. They are for pictures, so that the overlays can be looked at without a hand on the mouse.
        /// </summary>
        internal string Commands { get; private set; }

        /// <summary>
        /// Gets what is wrong with the arguments, or null.
        /// </summary>
        internal string Problem { get; private set; }

        /// <summary>
        /// Gets whether the viewer runs without a person at it, and so leaves the user's settings alone.
        /// </summary>
        internal bool Headless => Check || Screenshot != null;

        internal static CommandLine Parse(string[] args)
        {
            var line = new CommandLine();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                switch (arg.ToLowerInvariant())
                {
                    case "--check":
                        line.Check = true;
                        break;

                    case "--screenshot":
                    case "--view":
                    case "--mode":
                    case "--do":
                    case "--views":
                    case "--scale":
                    case "--background":
                        if (i + 1 >= args.Length)
                        {
                            line.Problem = arg + " needs a value after it.";
                            return line;
                        }

                        string value = args[++i];

                        if (arg.Equals("--views", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int views) || (views != 1 && views != 2 && views != 4))
                            {
                                line.Problem = "--views is 1, 2 or 4, not '" + value + "'.";
                                return line;
                            }

                            line.Views = views;
                        }
                        else if (arg.Equals("--scale", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int scale) || !ScreenshotOptions.IsScale(scale))
                            {
                                line.Problem = "--scale is 1, 2 or 4, not '" + value + "'.";
                                return line;
                            }

                            line.Scale = scale;
                        }
                        else if (arg.Equals("--background", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Enum.TryParse(value, true, out ScreenshotBackground background) || !Enum.IsDefined(typeof(ScreenshotBackground), background))
                            {
                                line.Problem = "--background is window, white or transparent, not '" + value + "'.";
                                return line;
                            }

                            line.Background = background;
                        }
                        else if (arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
                        {
                            line.Screenshot = value;
                        }
                        else if (arg.Equals("--view", StringComparison.OrdinalIgnoreCase))
                        {
                            line.View = value;
                        }
                        else if (arg.Equals("--do", StringComparison.OrdinalIgnoreCase))
                        {
                            line.Commands = value;
                        }
                        else
                        {
                            line.Mode = value;
                        }

                        break;

                    default:
                        if (arg.StartsWith("--", StringComparison.Ordinal))
                        {
                            line.Problem = "Unknown switch " + arg + ".";
                            return line;
                        }

                        line.File = arg;
                        break;
                }
            }

            if (line.Check && line.File == null)
            {
                line.Problem = "--check needs a file to check.";
            }

            return line;
        }
    }
}
