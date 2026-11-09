using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The linetype commands, in AutoCAD's words: LINETYPE (the Linetype Manager of the window, or -LINETYPE where there is
    /// none), -LINETYPE with ?, Create, Load and Set, CELTYPE, LTSCALE and CELTSCALE.
    /// </summary>
    internal static partial class CadCommands
    {
        /// <summary>LINETYPE: the window's Linetype Manager; with no window, the prompts of -LINETYPE.</summary>
        private static IEnumerable<CadPrompt> Linetype(CadCommandContext c) => c.Session.Request("LINETYPE", c.Document) ? Array.Empty<CadPrompt>() : LinetypeTyped(c);

        /// <summary>
        /// -LINETYPE: "Enter an option [?/Create/Load/Set]:" until Enter. ? lists a file's linetypes; Create appends one typed
        /// to a file, as AutoCAD does, and does not load it; Load loads linetypes of a file by name, wildcards taken, as one undo
        /// step; Set makes a loaded one current ($CELTYPE).
        /// </summary>
        private static IEnumerable<CadPrompt> LinetypeTyped(CadCommandContext c)
        {
            c.Write("Current line type:  \"" + c.Database.Celtype + "\"");

            while (true)
            {
                yield return CadPrompt.ForKeyword("Enter an option", new[] { "?", "Create", "Load", "Set" });
                IEnumerable<CadPrompt> option;

                if (c.Input.Is("?"))
                {
                    option = LinetypeList(c);
                }
                else if (c.Input.Is("Create"))
                {
                    option = LinetypeCreate(c);
                }
                else if (c.Input.Is("Load"))
                {
                    option = LinetypeLoad(c);
                }
                else if (c.Input.Is("Set"))
                {
                    option = LinetypeSet(c, "Specify linetype name or [?]");
                }
                else
                {
                    yield break;
                }

                foreach (CadPrompt prompt in option)
                {
                    yield return prompt;
                }
            }
        }

        /// <summary>-LINETYPE ?: the linetypes of a file listed, name and description.</summary>
        private static IEnumerable<CadPrompt> LinetypeList(CadCommandContext c)
        {
            string offered = CadSupportFiles.DefaultLinetypeFile(c.Document);
            yield return CadPrompt.ForString("Enter linetype file to list", Path.GetFileName(offered));
            IReadOnlyList<CadLinetypeDefinition> linetypes = LinetypeFile(c, c.Input.Kind == CadInputKind.Text ? c.Input.Text : offered, out string path);

            if (linetypes == null)
            {
                yield break;
            }

            c.Write("Linetypes defined in file " + path + ":");

            foreach (CadLinetypeDefinition linetype in linetypes)
            {
                c.Write(linetype.Name.PadRight(16) + " " + (linetype.Fault ?? linetype.Description));
            }
        }

        /// <summary>
        /// -LINETYPE Create: a name, the file to create or append to, a description and the pattern (<c>A,6,-1.5</c> or
        /// <c>6,-1.5</c>), appended to the file as AutoCAD writes a .lin, "New linetype definition saved to file."; a name the
        /// file has already, or a pattern that cannot be read, refused in words.
        /// </summary>
        private static IEnumerable<CadPrompt> LinetypeCreate(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter name of linetype to create");
            string name = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

            if (name.Length == 0 || name.IndexOfAny("<>/\\\":;?*|,=`".ToCharArray()) >= 0)
            {
                if (name.Length > 0)
                {
                    c.Write("Invalid linetype name.");
                }

                yield break;
            }

            string folder = c.Document.DrawingFolder;
            string offered = folder == null ? null : Path.Combine(folder, "user.lin");
            yield return CadPrompt.ForString("Enter linetype file to create or append to", offered);
            string file = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim().Trim('"') : offered;

            if (string.IsNullOrEmpty(file))
            {
                yield break;
            }

            file = Path.HasExtension(file) ? file : file + ".lin";

            if (!Path.IsPathRooted(file) && folder != null)
            {
                file = Path.Combine(folder, file);
            }

            c.Write("Wait, checking if linetype already defined...");
            IReadOnlyList<CadLinetypeDefinition> there = null;

            try
            {
                there = File.Exists(file) ? CadLinetypeFile.Read(file) : Array.Empty<CadLinetypeDefinition>();
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                c.Write("Cannot read linetype file \"" + file + "\": " + e.Message);
            }

            if (there == null)
            {
                yield break;
            }

            if (there.Any(l => string.Equals(l.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                c.Write("Linetype " + name + " is already defined in " + Path.GetFileName(file) + ".");
                yield break;
            }

            yield return CadPrompt.ForString("Descriptive text");
            string description = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;
            yield return CadPrompt.ForString("Enter linetype pattern (on next line)", "A,");
            CadLinetypeDefinition linetype = CadLinetypeFile.ParsePattern(name, description, c.Input.Kind == CadInputKind.Text ? c.Input.Text : string.Empty);

            if (linetype.Fault != null)
            {
                c.Write(linetype.Fault);
                yield break;
            }

            string written = null;

            try
            {
                string start = File.Exists(file) && new FileInfo(file).Length > 0 && !File.ReadAllText(file).EndsWith("\n", StringComparison.Ordinal) ? "\r\n" : string.Empty;
                File.AppendAllText(file, start + linetype.ToString().Replace("\n", "\r\n") + "\r\n", new UTF8Encoding(false));
                written = file;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                c.Write("Cannot write linetype file \"" + file + "\": " + e.Message);
            }

            if (written != null)
            {
                c.Write("New linetype definition saved to file.");
            }
        }

        /// <summary>-LINETYPE Load: names (parted by commas, wildcards taken) loaded from a file as one undo step, each said.</summary>
        private static IEnumerable<CadPrompt> LinetypeLoad(CadCommandContext c)
        {
            yield return CadPrompt.ForString("Enter linetype(s) to load");
            string names = c.Input.Kind == CadInputKind.Text ? c.Input.Text.Trim() : string.Empty;

            if (names.Length == 0)
            {
                yield break;
            }

            string offered = CadSupportFiles.DefaultLinetypeFile(c.Document);
            yield return CadPrompt.ForString("Enter linetype file to search", Path.GetFileName(offered));
            IReadOnlyList<CadLinetypeDefinition> linetypes = LinetypeFile(c, c.Input.Kind == CadInputKind.Text ? c.Input.Text : offered, out string path);

            if (linetypes == null)
            {
                yield break;
            }

            var wanted = new List<CadLinetypeDefinition>();

            foreach (string pattern in names.Split(',').Select(n => n.Trim()).Where(n => n.Length > 0))
            {
                List<CadLinetypeDefinition> matched = linetypes.Where(l => WildcardMatches(l.Name, pattern)).ToList();

                if (matched.Count == 0)
                {
                    c.Write("Linetype " + pattern + " not found in file " + Path.GetFileName(path) + ".");
                }

                wanted.AddRange(matched.Where(m => !wanted.Contains(m)));
            }

            c.OpenUndoStep();
            CadLinetypeLoad load = CadLinetypes.Load(c.Document, wanted, path, c.Command.Name);
            Say(c, load);
        }

        /// <summary>
        /// -LINETYPE Set and CELTYPE: the linetype new objects take ($CELTYPE): ByLayer, ByBlock or one loaded, as one undo step;
        /// ? lists those loaded; one not loaded refused, "Linetype X not found. Use the Load option to load it.".
        /// </summary>
        private static IEnumerable<CadPrompt> LinetypeSet(CadCommandContext c, string message)
        {
            while (true)
            {
                string old = c.Database.Celtype;
                yield return CadPrompt.ForString(message, old);

                if (c.Input.Kind != CadInputKind.Text)
                {
                    yield break;
                }

                string typed = c.Input.Text.Trim().Trim('"');

                if (typed == "?")
                {
                    c.Write("Loaded linetypes: " + string.Join(", ", CadLinetypes.Names(c.Database)) + ".");
                    continue;
                }

                string name = string.Equals(typed, "ByLayer", StringComparison.OrdinalIgnoreCase) ? "ByLayer"
                    : string.Equals(typed, "ByBlock", StringComparison.OrdinalIgnoreCase) ? "ByBlock"
                    : c.Database.LinetypeTable.Select(l => l.Name).FirstOrDefault(n => string.Equals(n, typed, StringComparison.OrdinalIgnoreCase));

                if (name == null)
                {
                    c.Write("Linetype " + typed + " not found. Use the Load option to load it.");
                    yield break;
                }

                SetCurrentLinetype(c, name);
                yield break;
            }
        }

        /// <summary>CELTYPE: the linetype new objects take, asked as AutoCAD asks a variable.</summary>
        private static IEnumerable<CadPrompt> CurrentLinetype(CadCommandContext c) => LinetypeSet(c, "Enter new value for CELTYPE");

        /// <summary>LTSCALE: the scale of every linetype of the drawing ($LTSCALE), above nought; every space drawn again.</summary>
        private static IEnumerable<CadPrompt> LinetypeScale(CadCommandContext c)
        {
            double old = c.Database.Ltscale;
            yield return CadPrompt.ForDistance("Enter new linetype scale factor", null, null, Length(old));

            if (ScaleTyped(c, old, out double scale) && scale != old)
            {
                CadEditResult result = c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$LTSCALE", Values(old), Values(scale)) }, s => s.Database.Ltscale = scale);

                if (result.Succeeded)
                {
                    c.Write("Regenerating model.");
                }
            }
        }

        /// <summary>Makes a linetype current ($CELTYPE) as one undo step, where it is another than the current.</summary>
        internal static void SetCurrentLinetype(CadCommandContext c, string name)
        {
            string old = c.Database.Celtype;

            if (!string.Equals(old, name, StringComparison.Ordinal))
            {
                c.Apply(new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "$CELTYPE", old, name) }, s => s.Database.Celtype = name);
            }
        }

        /// <summary>A scale typed: Enter keeps the old one; one not above nought refused, "Value must be positive and nonzero.".</summary>
        private static bool ScaleTyped(CadCommandContext c, double old, out double scale)
        {
            scale = old;

            if (c.Input.Kind != CadInputKind.Number)
            {
                return false;
            }

            if (!(c.Input.Number > 0.0) || double.IsInfinity(c.Input.Number))
            {
                c.Write("Value must be positive and nonzero.");
                return false;
            }

            scale = c.Input.Number;
            return true;
        }

        /// <summary>The linetypes of a file named on the command line: the presenter's own set, or a file found on the support path; null, said, where none is.</summary>
        private static IReadOnlyList<CadLinetypeDefinition> LinetypeFile(CadCommandContext c, string typed, out string path)
        {
            string name = (typed ?? string.Empty).Trim().Trim('"');
            path = null;

            if (string.Equals(name, CadLinetypeFile.BuiltInName, StringComparison.OrdinalIgnoreCase) || string.Equals(name, Path.GetFileNameWithoutExtension(CadLinetypeFile.BuiltInName), StringComparison.OrdinalIgnoreCase))
            {
                path = CadLinetypeFile.BuiltInName;
                return CadLinetypeFile.BuiltIn;
            }

            path = CadSupportFiles.Find(c.Document, name.Length > 0 && !Path.HasExtension(name) ? name + ".lin" : name);

            if (path == null)
            {
                c.Write("Cannot find linetype file \"" + name + "\".");
                return null;
            }

            try
            {
                return CadLinetypeFile.Read(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                c.Write("Cannot read linetype file \"" + path + "\": " + e.Message);
                return null;
            }
        }

        /// <summary>Says what a load came to, as AutoCAD says it: each loaded, each already loaded, each refused.</summary>
        private static void Say(CadCommandContext c, CadLinetypeLoad load)
        {
            foreach (string name in load.Loaded)
            {
                c.Write("Linetype " + name + " loaded.");
            }

            foreach (string name in load.Present)
            {
                c.Write("Linetype " + name + " is already loaded.");
            }

            foreach (string fault in load.Refused)
            {
                c.Write(fault);
            }

            if (!load.Result.Succeeded && load.Result.Message != null)
            {
                c.Write(load.Result.Message);
            }
        }

        /// <summary>Whether a name matches a pattern of AutoCAD's wildcards, * any run of letters and ? one, letters of either case the same.</summary>
        private static bool WildcardMatches(string name, string pattern)
        {
            var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return System.Text.RegularExpressions.Regex.IsMatch(name, regex, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        }

        private static string Count(int count, string one, string many) => count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? one : many);
    }
}
