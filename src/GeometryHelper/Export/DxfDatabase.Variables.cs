using System;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The system variables AutoCAD's <c>Database</c> has as properties and keeps out of the HEADER section: each a
    /// DICTIONARYVAR of the dictionary <c>AcDbVariableDictionary</c> of the named object dictionary, its value the text of its
    /// group 1, as AutoCAD writes it; or the header's variable of the same name, where a file keeps one there. Where the
    /// drawing gives none, the value AutoCAD gives a new drawing.
    /// </summary>
    /// <remarks>
    /// Setting <c>db.Cannoscale = "1:50"</c> writes <c>1:50</c> into the DICTIONARYVAR CANNOSCALE, every other byte of the file
    /// kept; where the drawing has none, as a drawing of ezdxf, the DICTIONARYVAR is added, owned by the dictionary
    /// AcDbVariableDictionary, which is added to the named object dictionary where that has none, as AutoCAD adds them. A
    /// value the variable has changes nothing. A file of R12, or one whose CLASSES section lists no DICTIONARYVAR, has none to
    /// set.
    /// </remarks>
    public sealed partial class DxfDatabase
    {
        private const string VariableDictionary = "AcDbVariableDictionary";

        // The groups of a DICTIONARYVAR in the order AutoCAD writes them.
        private static readonly IReadOnlyList<int> VariableOrder = Array.AsReadOnly(new[] { 100, 280, 1 });

        /// <summary>Gets or sets whether every annotative object is shown, whatever the scale of its view (ANNOALLVISIBLE), as AutoCAD's <c>AnnoAllVisible</c>; true where the drawing gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public bool AnnoAllVisible
        {
            get => Flag("ANNOALLVISIBLE", true);
            set => SetVariableFlag("ANNOALLVISIBLE", value, AnnoAllVisible);
        }

        /// <summary>
        /// Gets or sets the annotation scale of model space (CANNOSCALE), as AutoCAD's <c>Cannoscale</c> gives it, by name:
        /// <c>1:1</c> where the drawing gives none.
        /// </summary>
        /// <value>Set, the name of a scale of the drawing's list of scales (the SCALE objects of ACAD_SCALELIST), such as <c>1:50</c>.</value>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing's list of scales has no scale of the name.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public string Cannoscale
        {
            get => Variable("CANNOSCALE") ?? "1:1";

            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (string.Equals(value, Cannoscale, StringComparison.Ordinal))
                {
                    return;
                }

                Holdable("CANNOSCALE");

                if (!Scales().Contains(value))
                {
                    throw new ArgumentException("The drawing's list of scales has no scale named " + value + ".", nameof(value));
                }

                SetVariable("CANNOSCALE", value);
            }
        }

        /// <summary>
        /// Gets or sets the transparency of new entities (CETRANSPARENCY), as AutoCAD's <c>Cetransparency</c>: BYLAYER where the
        /// drawing gives none. AutoCAD keeps it as ByLayer, ByBlock or a whole per cent, 0 to 90: 50 per cent is the alpha 127.
        /// </summary>
        /// <value>Set, written as AutoCAD writes it; an alpha as the nearest whole per cent, so that the alpha 100 reads back as 99.</value>
        /// <exception cref="ArgumentException">Thrown when the transparency set is an error value, or an alpha more than 90 per cent clear.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public DxfTransparency Cetransparency
        {
            get
            {
                string value = Variable("CETRANSPARENCY")?.Trim();

                if (value == null || string.Equals(value, "ByLayer", StringComparison.OrdinalIgnoreCase))
                {
                    return DxfTransparency.ByLayer;
                }

                if (string.Equals(value, "ByBlock", StringComparison.OrdinalIgnoreCase))
                {
                    return DxfTransparency.ByBlock;
                }

                return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int percent) && percent >= 0 && percent <= 100
                    ? new DxfTransparency((byte)(255 * (100 - percent) / 100))
                    : new DxfTransparency(DxfTransparencyMethod.ErrorValue);
            }

            set
            {
                if (value == Cetransparency)
                {
                    return;
                }

                if (value.IsInvalid)
                {
                    throw new ArgumentException("A transparency is by layer, by block or by alpha: an error value is not written.", nameof(value));
                }

                int percent = (int)Math.Round((255 - value.Alpha) * 100.0 / 255.0, MidpointRounding.AwayFromZero);

                if (value.IsByAlpha && percent > 90)
                {
                    throw new ArgumentException("AutoCAD makes new entities at most 90 per cent clear: the alpha " + value.Alpha.ToString(CultureInfo.InvariantCulture) + " is " + percent.ToString(CultureInfo.InvariantCulture) + ".", nameof(value));
                }

                SetVariable("CETRANSPARENCY", value.IsByLayer ? "ByLayer" : value.IsByBlock ? "ByBlock" : percent.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>Gets or sets the table style new tables take (CTABLESTYLE), as AutoCAD's <c>Tablestyle</c> gives its id, by name: <c>Standard</c> where the drawing gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no table style of the name (ACAD_TABLESTYLE).</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public string Tablestyle
        {
            get => Variable("CTABLESTYLE") ?? "Standard";
            set => SetStyle("CTABLESTYLE", "ACAD_TABLESTYLE", value, Tablestyle);
        }

        /// <summary>Gets or sets the multileader style new multileaders take (CMLEADERSTYLE), as AutoCAD's <c>MLeaderstyle</c> gives its id, by name: <c>Standard</c> where the drawing gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no multileader style of the name (ACAD_MLEADERSTYLE).</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public string MLeaderstyle
        {
            get => Variable("CMLEADERSTYLE") ?? "Standard";
            set => SetStyle("CMLEADERSTYLE", "ACAD_MLEADERSTYLE", value, MLeaderstyle);
        }

        /// <summary>Gets or sets the detail view style new detail views take (CVIEWDETAILSTYLE), as AutoCAD's <c>DetailViewStyle</c> gives its id, by name; null where the drawing gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no detail view style of the name (ACAD_DETAILVIEWSTYLE).</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public string DetailViewStyle
        {
            get => Variable("CVIEWDETAILSTYLE");
            set => SetStyle("CVIEWDETAILSTYLE", "ACAD_DETAILVIEWSTYLE", value, DetailViewStyle);
        }

        /// <summary>Gets or sets the section view style new section views take (CVIEWSECTIONSTYLE), as AutoCAD's <c>SectionViewStyle</c> gives its id, by name; null where the drawing gives none.</summary>
        /// <exception cref="ArgumentNullException">Thrown when the name set is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no section view style of the name (ACAD_SECTIONVIEWSTYLE).</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public string SectionViewStyle
        {
            get => Variable("CVIEWSECTIONSTYLE");
            set => SetStyle("CVIEWSECTIONSTYLE", "ACAD_SECTIONVIEWSTYLE", value, SectionViewStyle);
        }

        /// <summary>Gets or sets the scale of multileaders that are not annotative (MLEADERSCALE), as AutoCAD's <c>MLeaderscale</c>; 1 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public double MLeaderscale
        {
            get => Real("MLEADERSCALE", 1.0);
            set => SetReal("MLEADERSCALE", value, MLeaderscale, true);
        }

        /// <summary>Gets or sets whether linetypes in model space are scaled by the annotation scale (MSLTSCALE), as AutoCAD's <c>MsLtScale</c>; true where the drawing gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public bool MsLtScale
        {
            get => Flag("MSLTSCALE", true);
            set => SetVariableFlag("MSLTSCALE", value, MsLtScale);
        }

        /// <summary>Gets or sets the scale of OLE objects in model space (MSOLESCALE), as AutoCAD's <c>MsOleScale</c>; 1 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the scale set is not a finite number of nought or more.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public double MsOleScale
        {
            get => Real("MSOLESCALE", 1.0);
            set => SetReal("MSOLESCALE", value, MsOleScale, true);
        }

        /// <summary>Gets or sets when the layer list is checked for new layers (LAYEREVAL), as AutoCAD's <c>LayerEval</c>: 0 never, 1 for new layers of external references, 2 for all; 0 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public int LayerEval
        {
            get => Whole("LAYEREVAL", 0);
            set => SetWhole("LAYEREVAL", value, LayerEval, 0, 2);
        }

        /// <summary>Gets or sets the units of lighting (LIGHTINGUNITS), as AutoCAD's <c>LightingUnits</c>: 0 generic, 1 American, 2 international; 2 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public byte LightingUnits
        {
            get => (byte)Whole("LIGHTINGUNITS", 2);
            set => SetWhole("LIGHTINGUNITS", value, LightingUnits, 0, 2);
        }

        /// <summary>Gets or sets whether the lights in blocks are drawn (LIGHTSINBLOCKS), as AutoCAD's <c>LightsInBlocks</c>; true where the drawing gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public bool LightsInBlocks
        {
            get => Flag("LIGHTSINBLOCKS", true);
            set => SetVariableFlag("LIGHTSINBLOCKS", value, LightsInBlocks);
        }

        /// <summary>Gets or sets how stacked text is aligned (TSTACKALIGN), as AutoCAD's <c>TStackAlign</c>: 0 bottom, 1 centre, 2 top; 1 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public int TStackAlign
        {
            get => Whole("TSTACKALIGN", 1);
            set => SetWhole("TSTACKALIGN", value, TStackAlign, 0, 2);
        }

        /// <summary>Gets or sets how high stacked text is, in per cent of the text (TSTACKSIZE), as AutoCAD's <c>TstackSize</c>: 25 to 125; 70 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from 25 to 125.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public int TstackSize
        {
            get => Whole("TSTACKSIZE", 70);
            set => SetWhole("TSTACKSIZE", value, TstackSize, 25, 125);
        }

        /// <summary>Gets or sets whether a hatch takes the origin of the hatch it matches (HPINHERIT), as AutoCAD's <c>HpInherit</c>; false where the drawing gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public bool HpInherit
        {
            get => Flag("HPINHERIT", false);
            set => SetVariableFlag("HPINHERIT", value, HpInherit);
        }

        /// <summary>Gets or sets how many lines a curved face of a body is drawn with (ISOLINES), as AutoCAD's <c>Isolines</c>: 0 to 2047; 4 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from 0 to 2047.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public int Isolines
        {
            get => Whole("ISOLINES", 4);
            set => SetWhole("ISOLINES", value, Isolines, 0, 2047);
        }

        /// <summary>Gets or sets how smooth shaded bodies are drawn (FACETRES), as AutoCAD's <c>Facetres</c>: 0.01 to 10; 0.5 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not from 0.01 to 10.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public double Facetres
        {
            get => Real("FACETRES", 0.5);

            set
            {
                if (!(value >= 0.01 && value <= 10.0))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "FACETRES is from 0.01 to 10.");
                }

                SetReal("FACETRES", value, Facetres, false);
            }
        }

        /// <summary>Gets or sets whether the frame of PDF underlays is shown and plotted (PDFFRAME), as AutoCAD's <c>PdfFrame</c>: 0 hidden, 1 shown and plotted, 2 shown; 1 where the drawing gives none.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the value set is not 0, 1 or 2.</exception>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public int PdfFrame
        {
            get => Whole("PDFFRAME", 1);
            set => SetWhole("PDFFRAME", value, PdfFrame, 0, 2);
        }

        /// <summary>Gets or sets whether an ellipse is drawn as a polyline (PELLIPSE), as AutoCAD's <c>PlineEllipse</c>; false where the drawing gives none.</summary>
        /// <exception cref="InvalidOperationException">Thrown when set in a file with no variables of the OBJECTS section to hold it.</exception>
        public bool PlineEllipse
        {
            get => Flag("PELLIPSE", false);
            set => SetVariableFlag("PELLIPSE", value, PlineEllipse);
        }

        /// <summary>
        /// Gets the picture of the drawing kept for previews, as AutoCAD's <c>ThumbnailBitmap</c> gives it as a bitmap: here the
        /// bytes of the THUMBNAILIMAGE section, its groups 310 one after another, a bitmap of Windows without the file's header;
        /// null where the file has none.
        /// </summary>
        public byte[] ThumbnailBitmap => File.Thumbnail();

        /// <summary>The value of a variable kept out of the header: the header's of its name where a file keeps one there, else the group 1 of its DICTIONARYVAR; null where there is none.</summary>
        private string Variable(string name)
        {
            IReadOnlyList<DxfTypedValue> header = File.HeaderOf("$" + name);

            if (header != null && header.Count == 1)
            {
                return header[0].Value;
            }

            DxfDBObject variable = Variables(false)?.Contains(name) == true ? Variables(false).GetAt(name) : null;

            if (variable == null || variable.Item.Type != "DICTIONARYVAR")
            {
                return null;
            }

            foreach (DxfTypedValue group in File.ObjectGroupsOf(variable.Item))
            {
                if (group.TypeCode == 1)
                {
                    return group.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Sets a variable kept out of the header: the header's where the file keeps it there, else its DICTIONARYVAR's group 1,
        /// the DICTIONARYVAR added where there is none, and the dictionary of variables with it where there is none.
        /// </summary>
        private void SetVariable(string name, string value)
        {
            IReadOnlyList<DxfTypedValue> header = File.HeaderOf("$" + name);

            if (header != null && header.Count == 1)
            {
                File.SetHeader("$" + name, new[] { new DxfTypedValue(header[0].TypeCode, value) });
                return;
            }

            DxfDBDictionary variables = Variables(true);
            DxfDBObject variable = variables.Contains(name) ? variables.GetAt(name) : null;

            if (variable != null && variable.Item.Type == "DICTIONARYVAR")
            {
                File.SetValues(variable.Item, new[] { new KeyValuePair<int, string>(1, value) }, VariableOrder, false);
                return;
            }

            variables.Add(name, "DICTIONARYVAR", b =>
            {
                b.Group(100, "DictionaryVariables");
                b.Group(280, "0");
                b.Group(1, value);
            });
        }

        /// <summary>The dictionary of variables of the named object dictionary; made where it is asked to be and there is none; null where it is not, or the file holds no objects.</summary>
        /// <exception cref="InvalidOperationException">Thrown when one is to be made and the file has no OBJECTS section, no named object dictionary, or no class DICTIONARYVAR.</exception>
        private DxfDBDictionary Variables(bool make)
        {
            DxfItem rootItem = File.HoldsObjects ? File.ObjectItem(File.RootDictionaryHandle()) : null;
            var root = rootItem == null ? null : DxfDBObject.OfObject(rootItem) as DxfDBDictionary;

            if (root != null && root.Contains(VariableDictionary) && root.GetAt(VariableDictionary) is DxfDBDictionary variables)
            {
                if (make && !File.HasClass("DICTIONARYVAR"))
                {
                    throw new InvalidOperationException("The file's CLASSES section lists no DICTIONARYVAR, which a variable kept out of the header is.");
                }

                return variables;
            }

            if (!make)
            {
                return null;
            }

            if (root == null)
            {
                throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no named object dictionary to hold a variable kept out of the header: R13 (AC1012) brought it.");
            }

            if (!File.HasClass("DICTIONARYVAR"))
            {
                throw new InvalidOperationException("The file's CLASSES section lists no DICTIONARYVAR, which a variable kept out of the header is.");
            }

            DxfItem made = root.Add(VariableDictionary, "DICTIONARY", b =>
            {
                b.Group(100, "AcDbDictionary");
                b.Group(281, "1");
            });

            return (DxfDBDictionary)DxfDBObject.OfObject(made);
        }

        /// <summary>Refuses a variable the file has nowhere to hold: neither in its header nor, having no OBJECTS section, as a DICTIONARYVAR.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the file has neither.</exception>
        private void Holdable(string name)
        {
            if (File.HeaderOf("$" + name) == null && !File.HoldsObjects)
            {
                throw new InvalidOperationException("A DXF " + (Version ?? "with no version") + " has no OBJECTS section to hold the variable " + name + ": R13 (AC1012) brought it.");
            }
        }

        /// <summary>The names of the entries of a dictionary of the named object dictionary, as ACAD_TABLESTYLE's styles; none where there is none.</summary>
        private HashSet<string> Entries(string dictionary)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DxfItem rootItem = File.HoldsObjects ? File.ObjectItem(File.RootDictionaryHandle()) : null;

            if (rootItem != null && DxfDBObject.OfObject(rootItem) is DxfDBDictionary root && root.Contains(dictionary) && root.GetAt(dictionary) is DxfDBDictionary entries)
            {
                foreach (DxfDBDictionaryEntry entry in entries)
                {
                    names.Add(entry.Key);
                }
            }

            return names;
        }

        /// <summary>The names of the scales of the drawing's list of scales: the 300 of each SCALE of ACAD_SCALELIST.</summary>
        private HashSet<string> Scales()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DxfItem rootItem = File.HoldsObjects ? File.ObjectItem(File.RootDictionaryHandle()) : null;

            if (rootItem != null && DxfDBObject.OfObject(rootItem) is DxfDBDictionary root && root.Contains("ACAD_SCALELIST") && root.GetAt("ACAD_SCALELIST") is DxfDBDictionary list)
            {
                foreach (DxfDBDictionaryEntry entry in list)
                {
                    foreach (DxfTypedValue group in entry.Value == null ? new DxfTypedValue[0] : (IEnumerable<DxfTypedValue>)File.ObjectGroupsOf(entry.Value.Item))
                    {
                        if (group.TypeCode == 300)
                        {
                            names.Add(group.Value.Trim());
                        }
                    }
                }
            }

            return names;
        }

        private void SetStyle(string variable, string dictionary, string value, string now)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (string.Equals(value, now, StringComparison.Ordinal))
            {
                return;
            }

            Holdable(variable);

            if (!Entries(dictionary).Contains(value))
            {
                throw new ArgumentException("The drawing has no style named " + value + " in " + dictionary + ".", nameof(value));
            }

            SetVariable(variable, value);
        }

        private bool Flag(string name, bool fallback) => int.TryParse(Variable(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value != 0 : fallback;

        private int Whole(string name, int fallback) => int.TryParse(Variable(name)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

        private double Real(string name, double fallback) => Numbers.TryRead(Variable(name), out double value) && Guard.IsFinite(value) ? value : fallback;

        private void SetVariableFlag(string name, bool value, bool now)
        {
            if (value != now)
            {
                SetVariable(name, value ? "1" : "0");
            }
        }

        private void SetWhole(string name, int value, int now, int least, int most)
        {
            if (value < least || value > most)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, name + " is from " + least.ToString(CultureInfo.InvariantCulture) + " to " + most.ToString(CultureInfo.InvariantCulture) + ".");
            }

            if (value != now)
            {
                SetVariable(name, value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void SetReal(string name, double value, double now, bool check)
        {
            if (BitConverter.DoubleToInt64Bits(value) == BitConverter.DoubleToInt64Bits(now))
            {
                return;
            }

            if (check)
            {
                Guard.NonNegative(value, nameof(value), name + " has to be a finite number of nought or more.");
            }

            SetVariable(name, Numbers.Exact(value + 0.0));
        }
    }
}
