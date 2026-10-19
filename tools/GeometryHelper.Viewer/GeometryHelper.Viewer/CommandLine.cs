using System;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What the viewer was started with: <c>GeometryHelper.Viewer.exe [file] [--check] [--screenshot png] [--view v] [--mode m]</c>.
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
                        if (i + 1 >= args.Length)
                        {
                            line.Problem = arg + " needs a value after it.";
                            return line;
                        }

                        string value = args[++i];

                        if (arg.Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
                        {
                            line.Screenshot = value;
                        }
                        else if (arg.Equals("--view", StringComparison.OrdinalIgnoreCase))
                        {
                            line.View = value;
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
