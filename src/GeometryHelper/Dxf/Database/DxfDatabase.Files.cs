using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>Linetypes and multiline styles loaded from AutoCAD's files of them, as AutoCAD's <c>Database</c> loads them.</summary>
    public sealed partial class DxfDatabase
    {
        /// <summary>
        /// Loads linetypes from a file of linetypes, as AutoCAD's <c>LoadLineTypeFile</c> loads them from acad.lin: each
        /// linetype of the name given added to the <see cref="LinetypeTable"/> with its description and dashes, a dash's text
        /// or shape with its style, scale, rotation and offset, as <see cref="DxfLinetypeTableRecord"/> writes them.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A linetype of the file is <c>*NAME,description</c> on a line and its pattern on the next: <c>A,</c> then the lengths
        /// of its dashes, above nought a dash, below a gap, nought a dot, and a dash's text or shape in brackets after it,
        /// <c>["GAS",STANDARD,S=.1,U=0.0,X=-0.1,Y=-.05]</c> or <c>[CIRC1,ltypeshp.shx,x=-.1,s=.1]</c>; a line that begins with
        /// a semicolon is a comment. AutoCAD's gas line, <c>A,.5,-.2,["GAS",STANDARD,S=.1,U=0.0,X=-0.1,Y=-.05],-.25</c>, is
        /// three dashes and 0.95 long, its gap of 0.2 carrying the text GAS, written <c>49 -0.2, 74 2, 75 0, 340</c> the handle
        /// of Standard<c>, 46 0.1, 50 0, 44 -0.1, 45 -0.05, 9 GAS</c>. A rotation R or U is from the line and A from the x axis,
        /// in degrees, or radians with r after it, grads with g.
        /// </para>
        /// <para>
        /// A text is drawn in the text style the file names, which the drawing is to have. A shape is drawn from the file of
        /// shapes the file names, in the drawing's style of that file, added with no name (70 of 1) where it has none, as
        /// AutoCAD adds one; a shape named rather than numbered is found by its name in that compiled file (.shx), in the folder
        /// of the file of linetypes. A file of R12 has no complex linetypes.
        /// </para>
        /// <para>
        /// The name may hold the wildcards * and ?, <c>*</c> loading every linetype of the file: those the drawing has already,
        /// and those it cannot take (a style or shape it has not, more than 12 dashes), are passed over, the log saying which in
        /// one line. A name with no wildcard is loaded or refused.
        /// </para>
        /// </remarks>
        /// <param name="lineTypeName">The name of the linetype, letters of either case the same, or a pattern of them.</param>
        /// <param name="filename">The path of the file of linetypes.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown, for a name with no wildcard, when the file has no linetype of the name or
        /// cannot be read there, the drawing has a linetype of the name already (AutoCAD's eDuplicateRecordName), or the
        /// linetype is one the drawing cannot take: a text style it has not, a shape not found, more than 12 dashes, a complex
        /// linetype in a file of R12 or in one that writes no handles to point at its styles by, or no linetype table to hold
        /// it. Every dash is checked first, so that a linetype refused adds nothing, not even the style of a file of
        /// shapes.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read, a <see cref="FileNotFoundException"/> where there is none.</exception>
        public void LoadLineTypeFile(string lineTypeName, string filename)
        {
            if (lineTypeName == null)
            {
                throw new ArgumentNullException(nameof(lineTypeName));
            }

            if (filename == null)
            {
                throw new ArgumentNullException(nameof(filename));
            }

            List<DxfSupportFiles.Linetype> linetypes = DxfSupportFiles.ReadLinetypes(DxfSupportFiles.ReadText(filename));
            string folder = Path.GetDirectoryName(Path.GetFullPath(filename));
            bool pattern = lineTypeName.IndexOf('*') >= 0 || lineTypeName.IndexOf('?') >= 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var passed = new List<string>();
            int loaded = 0;

            foreach (DxfSupportFiles.Linetype linetype in linetypes)
            {
                if (!Matches(linetype.Name, lineTypeName.Trim()) || !seen.Add(linetype.Name))
                {
                    continue;
                }

                string fault = LoadLinetype(linetype, folder);

                if (fault == null)
                {
                    loaded++;
                }
                else if (!pattern)
                {
                    throw new ArgumentException(fault, nameof(lineTypeName));
                }
                else
                {
                    passed.Add(linetype.Name);
                }
            }

            if (!pattern && loaded == 0)
            {
                throw new ArgumentException("The file " + Path.GetFileName(filename) + " has no linetype named " + lineTypeName + ".", nameof(lineTypeName));
            }

            if (passed.Count > 0)
            {
                GeometryHelperLog.Debug("DxfDatabase: " + passed.Count.ToString(CultureInfo.InvariantCulture) + " linetypes of " + Path.GetFileName(filename) + " were passed over, the drawing having them already or not taking them: " + string.Join(", ", passed) + ".");
            }

            GeometryHelperLog.Info("DxfDatabase: " + loaded.ToString(CultureInfo.InvariantCulture) + " linetypes were loaded from " + Path.GetFileName(filename) + ".");
        }

        /// <summary>
        /// Loads a multiline style from a file of them, as AutoCAD's <c>LoadMlineStyleFile</c> loads one from acad.mln: an
        /// MLINESTYLE of the style's groups added to the dictionary of multiline styles (<see cref="MLStyleDictionary"/>) under
        /// its name, as a <see cref="DxfMlineStyle"/>, written as AutoCAD writes one: <c>100 AcDbMlineStyle</c>, then its
        /// name (2), flags (70), description (3), fill (62), angles (51, 52), count of elements (71) and each element's offset
        /// (49), colour (62) and linetype (6), as the file gives them. A drawing with no dictionary of multiline styles is given
        /// one, ACAD_MLINESTYLE of its named object dictionary.
        /// </summary>
        /// <remarks>
        /// A style of the file is opened by <c>MLSTYLE</c> and given as its groups, each a code on one line and its value on
        /// the next, up to a group 0, as AutoCAD writes acad.mln; its STANDARD is two elements 0.5 either side of the line, by
        /// layer.
        /// </remarks>
        /// <param name="mlineStyleName">The name of the style, letters of either case the same.</param>
        /// <param name="fileName">The path of the file of multiline styles.</param>
        /// <exception cref="ArgumentNullException">Thrown when the name or the path is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the file has no style of the name, the drawing's dictionary has one of
        /// it already, or an element is drawn with a linetype the drawing has not.</exception>
        /// <exception cref="FormatException">Thrown when the file does not give a group code where it should.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the drawing has no named object dictionary, as one of R12.</exception>
        /// <exception cref="IOException">Thrown when the file cannot be read, a <see cref="FileNotFoundException"/> where there is none.</exception>
        public void LoadMlineStyleFile(string mlineStyleName, string fileName)
        {
            if (mlineStyleName == null)
            {
                throw new ArgumentNullException(nameof(mlineStyleName));
            }

            if (fileName == null)
            {
                throw new ArgumentNullException(nameof(fileName));
            }

            List<DxfTypedValue> style = null;

            foreach (List<DxfTypedValue> each in DxfSupportFiles.ReadMlineStyles(DxfSupportFiles.ReadText(fileName)))
            {
                string name = each.Find(g => g.TypeCode == 2).Value;

                if (style == null && name != null && string.Equals(name.Trim(), mlineStyleName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    style = each;
                }
            }

            if (style == null)
            {
                throw new ArgumentException("The file " + Path.GetFileName(fileName) + " has no multiline style named " + mlineStyleName + ".", nameof(mlineStyleName));
            }

            string written = style.Find(g => g.TypeCode == 2).Value.Trim();
            DxfDBDictionary root = NamedObjectsDictionary ?? throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no named object dictionary to hold multiline styles: R13 (AC1012) brought it.");
            DxfDBDictionary styles = MLStyleDictionary;

            if (styles != null && styles.Contains(written))
            {
                throw new ArgumentException("The drawing has a multiline style named " + written + " already, letters of either case the same.", nameof(mlineStyleName));
            }

            foreach (DxfTypedValue group in style)
            {
                string linetype = group.TypeCode == 6 ? DxfReader.NameOf(group.Value).Trim() : null;

                if (linetype != null && !IsOneOf(linetype, "BYLAYER", "BYBLOCK") && !LinetypeTable.Has(linetype))
                {
                    throw new ArgumentException("The multiline style " + written + " draws an element with the linetype " + linetype + ", which the drawing has not: load it first.", nameof(mlineStyleName));
                }
            }

            if (styles == null)
            {
                root.SetAt("ACAD_MLINESTYLE", new DxfDBDictionary());
                styles = MLStyleDictionary;
            }

            styles.Add(written, "MLINESTYLE", b =>
            {
                b.Group(100, "AcDbMlineStyle");

                foreach (DxfTypedValue group in style)
                {
                    // The handle, owner and markers are the drawing's to give.
                    if (group.TypeCode != 5 && group.TypeCode != 100 && group.TypeCode != 102 && group.TypeCode != 330 && group.TypeCode != 360)
                    {
                        b.Group(group.TypeCode, group.Value);
                    }
                }
            });

            GeometryHelperLog.Info("DxfDatabase: the multiline style " + written + " was loaded from " + Path.GetFileName(fileName) + ".");
        }

        /// <summary>
        /// Adds a linetype of a file of linetypes to the linetype table, every dash's style and shape found first, as
        /// <see cref="LoadLineTypeFile"/> says; the fault that keeps it out, else null.
        /// </summary>
        private string LoadLinetype(DxfSupportFiles.Linetype linetype, string folder)
        {
            string name = linetype.Name;

            if (linetype.Fault != null)
            {
                return linetype.Fault;
            }

            try
            {
                DxfSymbolTable.CheckName(name, "lineTypeName");
            }
            catch (ArgumentException e)
            {
                return e.Message;
            }

            if (LinetypeTable.Has(name))
            {
                return "The drawing has a linetype named " + name + " already, as AutoCAD answers eDuplicateRecordName.";
            }

            List<DxfSupportFiles.Dash> dashes = linetype.Dashes;

            if (dashes.Count > 12)
            {
                return "The linetype " + name + " has " + dashes.Count.ToString(CultureInfo.InvariantCulture) + " dashes, and AutoCAD takes 12 at most.";
            }

            bool complex = dashes.Exists(d => d.IsComplex);

            if (complex && !File.HasMarkers)
            {
                return "The linetype " + name + " is complex, and a DXF " + (Version ?? "R12") + " has no complex linetypes, which came with R13 (AC1012).";
            }

            if (File.TableItem("LTYPE") == null)
            {
                return "The drawing has no linetype table to add the linetype " + name + " to.";
            }

            // Every style and shape found before anything is written: a linetype refused adds nothing.
            var styles = new DxfTextStyleTableRecord[dashes.Count];
            var shapes = new int[dashes.Count];
            var shapeFiles = new string[dashes.Count];

            for (int i = 0; i < dashes.Count; i++)
            {
                DxfSupportFiles.Dash dash = dashes[i];

                if (dash.Text != null)
                {
                    styles[i] = TextStyleNamed(dash.Style);

                    if (styles[i] == null)
                    {
                        return "The linetype " + name + " writes its text in the text style " + dash.Style + ", which the drawing has not.";
                    }
                }
                else if (dash.Shape != null)
                {
                    styles[i] = ShapeStyleOf(dash.Style);
                    shapeFiles[i] = dash.Style;
                    string fault = ShapeNumber(dash.Shape, dash.Style, folder, out shapes[i]);

                    if (fault != null)
                    {
                        return "The linetype " + name + " draws the shape " + dash.Shape + ": " + fault;
                    }

                    // The style of the file of shapes is added below, once every dash has been checked, so the table is to hold it.
                    if (styles[i] == null && File.TableItem("STYLE") == null)
                    {
                        return "The drawing has no text style table to hold the file of shapes " + dash.Style + ".";
                    }
                }

                if (dash.IsComplex && (styles[i] == null ? !File.WritesHandles : styles[i].Handle == null))
                {
                    return "The drawing writes no handles, by which a dash of the linetype " + name + " points at its style.";
                }
            }

            var record = new DxfLinetypeTableRecord { Name = name, AsciiDescription = linetype.Description, NumDashes = dashes.Count };

            for (int i = 0; i < dashes.Count; i++)
            {
                DxfSupportFiles.Dash dash = dashes[i];
                record.SetDashLengthAt(i, dash.Length);

                if (!dash.IsComplex)
                {
                    continue;
                }

                if (styles[i] == null)
                {
                    // A file of shapes the drawing has no style of is given one with no name, as AutoCAD gives it, once: two dashes
                    // of the file find the one the first added.
                    styles[i] = ShapeStyleOf(shapeFiles[i]) ?? DxfTextStyleTableRecord.Of(File.AddTextStyle(string.Empty, 1, 0.0, 1.0, 0.0, shapeFiles[i], string.Empty));
                }

                if (dash.Text != null)
                {
                    record.SetTextAt(i, dash.Text);
                }
                else
                {
                    record.SetShapeNumberAt(i, shapes[i]);
                }

                record.SetShapeStyleAt(i, styles[i]);

                if (dash.Scale.HasValue)
                {
                    record.SetShapeScaleAt(i, dash.Scale.Value);
                }

                // The degrees as the file gives them, not taken through radians and back.
                record.SetShapeRotationDegreesAt(i, dash.RotationDegrees);
                record.SetShapeOffsetAt(i, new GeoVector2(dash.X, dash.Y));
                record.SetShapeIsUcsOrientedAt(i, dash.Absolute);
            }

            LinetypeTable.Add(record);
            return null;
        }

        /// <summary>The text style of letters of a name, letters of either case the same; null where the drawing has none.</summary>
        private DxfTextStyleTableRecord TextStyleNamed(string name)
        {
            foreach (DxfTextStyleTableRecord style in TextStyleTable)
            {
                if (!style.IsShapeFile && string.Equals(style.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return style;
                }
            }

            return null;
        }

        /// <summary>The text style of a file of shapes, by the file's name, letters of either case the same, its folder and .shx aside; null where the drawing has none.</summary>
        private DxfTextStyleTableRecord ShapeStyleOf(string file)
        {
            foreach (DxfTextStyleTableRecord style in TextStyleTable)
            {
                if (style.IsShapeFile && string.Equals(ShapeFileName(style.FileName), ShapeFileName(file), StringComparison.OrdinalIgnoreCase))
                {
                    return style;
                }
            }

            return null;
        }

        /// <summary>The name of a file of shapes without its folder and its .shx.</summary>
        private static string ShapeFileName(string file)
        {
            string name = file.Replace('\\', '/');
            name = name.Substring(name.LastIndexOf('/') + 1);
            return name.EndsWith(".shx", StringComparison.OrdinalIgnoreCase) ? name.Substring(0, name.Length - 4) : name;
        }

        /// <summary>The number of a shape: as written where it is a number, else found by its name in the compiled file of shapes beside the file of linetypes; the fault where it is not found, else null.</summary>
        private static string ShapeNumber(string shape, string file, string folder, out int number)
        {
            if (int.TryParse(shape, NumberStyles.Integer, CultureInfo.InvariantCulture, out number) && number > 0)
            {
                return null;
            }

            string path = Path.Combine(folder, ShapeFileName(file) + ".shx");

            if (!System.IO.File.Exists(path))
            {
                return "its file of shapes " + file + " is not in the folder of the file of linetypes, " + folder + ".";
            }

            Dictionary<string, int> numbers = DxfSupportFiles.ShapeNumbers(System.IO.File.ReadAllBytes(path));

            if (numbers == null)
            {
                return "the file " + path + " is not a compiled file of shapes.";
            }

            return numbers.TryGetValue(shape, out number) ? null : "the file " + path + " has no shape of that name.";
        }

        /// <summary>Whether a name matches a pattern of AutoCAD's wildcards, * any run of letters and ? one, letters of either case the same.</summary>
        private static bool Matches(string name, string pattern)
        {
            int n = 0;
            int p = 0;
            int star = -1;
            int mark = 0;

            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || char.ToUpperInvariant(pattern[p]) == char.ToUpperInvariant(name[n])))
                {
                    n++;
                    p++;
                }
                else if (p < pattern.Length && pattern[p] == '*')
                {
                    star = p++;
                    mark = n;
                }
                else if (star >= 0)
                {
                    p = star + 1;
                    n = ++mark;
                }
                else
                {
                    return false;
                }
            }

            while (p < pattern.Length && pattern[p] == '*')
            {
                p++;
            }

            return p == pattern.Length;
        }
    }
}
