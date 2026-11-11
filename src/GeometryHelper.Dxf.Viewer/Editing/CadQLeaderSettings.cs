using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What QLEADER puts at the end of its leader, as the Annotation Type of AutoCAD's Leader Settings (group 60).</summary>
    public enum CadQLeaderAnnotation
    {
        /// <summary>An MTEXT of the lines typed: "Enter first line of annotation text &lt;Mtext&gt;:".</summary>
        MText = 0,

        /// <summary>A copy of an MTEXT, a TEXT or an INSERT picked: "Select an object to copy:".</summary>
        CopyObject = 1,

        /// <summary>A geometric tolerance, which needs AutoCAD's Geometric Tolerance dialog: the presenter draws no annotation.</summary>
        Tolerance = 2,

        /// <summary>An INSERT placed as INSERT places one: "Enter block name or [?]:".</summary>
        BlockReference = 3,

        /// <summary>No annotation: the leader ends at its last point.</summary>
        None = 4,
    }

    /// <summary>Whether QLEADER reuses an annotation, as the Annotation Reuse of AutoCAD's Leader Settings (group 61).</summary>
    public enum CadQLeaderReuse
    {
        /// <summary>Each leader gets an annotation of its own.</summary>
        None = 0,

        /// <summary>The next annotation made is reused for every later leader, the setting then turned to Reuse Current.</summary>
        ReuseNext = 1,

        /// <summary>The annotation kept (<see cref="CadQLeaderSettings.ReusedHandle"/>) is copied to the end of each leader.</summary>
        ReuseCurrent = 2,
    }

    /// <summary>An angle a segment of a QLEADER leader is held to, as the Angle Constraints of AutoCAD's Leader Settings.</summary>
    public enum CadQLeaderAngle
    {
        /// <summary>Any angle: the point is taken where it is picked.</summary>
        Any = 0,

        /// <summary>Level: the point is taken straight above or below where it is picked.</summary>
        Horizontal = 1,

        /// <summary>A multiple of 90 degrees.</summary>
        Ninety = 2,

        /// <summary>A multiple of 45 degrees.</summary>
        FortyFive = 3,

        /// <summary>A multiple of 30 degrees.</summary>
        Thirty = 4,

        /// <summary>A multiple of 15 degrees.</summary>
        Fifteen = 5,
    }

    /// <summary>Where the leader meets its MTEXT, as the Multi-line Text Attachment of AutoCAD's Leader Settings (groups 62 and 63).</summary>
    public enum CadQLeaderAttachment
    {
        /// <summary>Top of top line.</summary>
        TopOfTopLine = 0,

        /// <summary>Middle of top line.</summary>
        MiddleOfTopLine = 1,

        /// <summary>Middle of multi-line text.</summary>
        MiddleOfText = 2,

        /// <summary>Middle of bottom line.</summary>
        MiddleOfBottomLine = 3,

        /// <summary>Bottom of bottom line.</summary>
        BottomOfBottomLine = 4,
    }

    /// <summary>
    /// The settings QLEADER draws by, as AutoCAD 2021 keeps those of its Leader Settings dialog: per drawing, in an XRECORD
    /// under the key "AcadDim" of the named object dictionary, whose group 90 holds 990106, and the defaults where the drawing
    /// has none. QLEADER reads them and never writes them itself; the dialog writes them on OK. The arrowhead is not among
    /// them: it is DIMLDRBLK, an override of the current dimension style.
    /// </summary>
    /// <remarks>
    /// The groups, as AutoCAD 2021's QLEADER reads them (checked in its console): 3 a user arrow's block name, 40 a width,
    /// 60 <see cref="Annotation"/>, 61 <see cref="Reuse"/> (2 is Reuse Current, with 340 the annotation reused), 62 the text
    /// on the right side and 63 on the left side (<see cref="CadQLeaderAttachment"/>), 64 Underline bottom line, 65 Spline,
    /// 66 No Limit, 67 Maximum points, 68 Prompt for width, 69 Always left justify, 70 and 71 the angle constraints, 72 Frame
    /// text and 170 the tab shown.
    /// </remarks>
    public sealed class CadQLeaderSettings
    {
        /// <summary>The key of the settings in the named object dictionary.</summary>
        public const string DictionaryKey = "AcadDim";

        /// <summary>The number in group 90 that marks the settings.</summary>
        public const int Marker = 990106;

        private int _maxPoints = 3;

        /// <summary>Gets or sets what the leader points to; MText by default.</summary>
        public CadQLeaderAnnotation Annotation { get; set; }

        /// <summary>Gets or sets whether an annotation is reused; None by default.</summary>
        public CadQLeaderReuse Reuse { get; set; }

        /// <summary>Gets or sets the handle of the annotation Reuse Current copies (group 340); null for none.</summary>
        public string ReusedHandle { get; set; }

        /// <summary>Gets or sets "Prompt for width": QLEADER asks "Specify text width &lt;0.0000&gt;:"; on by default.</summary>
        public bool PromptForWidth { get; set; } = true;

        /// <summary>Gets or sets the width group 40 holds, which AutoCAD 2021 keeps but does not offer; nought by default.</summary>
        public double Width { get; set; }

        /// <summary>
        /// Gets or sets "Always left justify": the words are left justified on either side of the leader, and QLEADER asks no
        /// width, as AutoCAD 2021 asks none then; off by default.
        /// </summary>
        public bool AlwaysLeftJustify { get; set; }

        /// <summary>Gets or sets "Frame text": the leader frames its words, by a DIMGAP override below nought; off by default.</summary>
        public bool FrameText { get; set; }

        /// <summary>Gets or sets whether the leader is a spline rather than straight; straight by default.</summary>
        public bool Spline { get; set; }

        /// <summary>Gets or sets "No Limit": points are asked until Enter; off by default.</summary>
        public bool NoLimit { get; set; }

        /// <summary>Gets or sets the most points of a leader, counting the arrow's, from 2 up; 3 by default.</summary>
        public int MaxPoints
        {
            get => _maxPoints;
            set => _maxPoints = Math.Max(2, Math.Min(999, value));
        }

        /// <summary>Gets or sets the angle the first segment is held to; Any by default.</summary>
        public CadQLeaderAngle FirstSegment { get; set; }

        /// <summary>Gets or sets the angle the second segment is held to; Any by default.</summary>
        public CadQLeaderAngle SecondSegment { get; set; }

        /// <summary>Gets or sets where the leader meets words on its right (group 62); Middle of top line by default.</summary>
        public CadQLeaderAttachment RightSide { get; set; } = CadQLeaderAttachment.MiddleOfTopLine;

        /// <summary>Gets or sets where the leader meets words on its left (group 63); Middle of bottom line by default.</summary>
        public CadQLeaderAttachment LeftSide { get; set; } = CadQLeaderAttachment.MiddleOfBottomLine;

        /// <summary>Gets or sets "Underline bottom line": the leader runs under the last line of the words; off by default.</summary>
        public bool Underline { get; set; }

        /// <summary>Gets or sets the tab the dialog opens on: 0 Annotation, 1 Leader Line &amp; Arrow, 2 Attachment.</summary>
        public int Tab { get; set; }

        /// <summary>
        /// Gets or sets the name of a user arrow block (group 3), the block Leader Settings' "User Arrow..." chose last; empty
        /// for none. The arrow drawn is DIMLDRBLK all the same.
        /// </summary>
        public string UserArrow { get; set; } = string.Empty;

        /// <summary>Gets a copy of the settings.</summary>
        /// <returns>The copy.</returns>
        public CadQLeaderSettings Clone() => (CadQLeaderSettings)MemberwiseClone();

        /// <summary>The increment, in radians, a segment held to an angle is held to multiples of; nought for any angle.</summary>
        internal static double Increment(CadQLeaderAngle angle)
        {
            switch (angle)
            {
                case CadQLeaderAngle.Horizontal:
                    return Math.PI;
                case CadQLeaderAngle.Ninety:
                    return Math.PI / 2.0;
                case CadQLeaderAngle.FortyFive:
                    return Math.PI / 4.0;
                case CadQLeaderAngle.Thirty:
                    return Math.PI / 6.0;
                case CadQLeaderAngle.Fifteen:
                    return Math.PI / 12.0;
                default:
                    return 0.0;
            }
        }

        /// <summary>
        /// Reads a drawing's settings, as QLEADER reads them: those of its "AcadDim" XRECORD, each group it lacks, and all of
        /// them where it has none, the defaults.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static CadQLeaderSettings Read(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var settings = new CadQLeaderSettings();
            DxfXrecord record = Record(database);

            if (record == null)
            {
                return settings;
            }

            foreach (DxfTypedValue value in record.Data)
            {
                string text = value.Value ?? string.Empty;
                int whole = int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int w)
                    ? w
                    : 0;

                switch (value.TypeCode)
                {
                    case 3:
                        settings.UserArrow = text;
                        break;
                    case 40:
                        settings.Width = double.TryParse(
                            text.Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out double width
                        )
                            ? width
                            : 0.0;
                        break;
                    case 60:
                        settings.Annotation = (CadQLeaderAnnotation)Clamp(whole, 4);
                        break;
                    case 61:
                        settings.Reuse = (CadQLeaderReuse)Clamp(whole, 2);
                        break;
                    case 62:
                        settings.RightSide = (CadQLeaderAttachment)Clamp(whole, 4);
                        break;
                    case 63:
                        settings.LeftSide = (CadQLeaderAttachment)Clamp(whole, 4);
                        break;
                    case 64:
                        settings.Underline = whole != 0;
                        break;
                    case 65:
                        settings.Spline = whole != 0;
                        break;
                    case 66:
                        settings.NoLimit = whole != 0;
                        break;
                    case 67:
                        settings.MaxPoints = whole;
                        break;
                    case 68:
                        settings.PromptForWidth = whole != 0;
                        break;
                    case 69:
                        settings.AlwaysLeftJustify = whole != 0;
                        break;
                    case 70:
                        settings.FirstSegment = (CadQLeaderAngle)Clamp(whole, 5);
                        break;
                    case 71:
                        settings.SecondSegment = (CadQLeaderAngle)Clamp(whole, 5);
                        break;
                    case 72:
                        settings.FrameText = whole != 0;
                        break;
                    case 170:
                        settings.Tab = Clamp(whole, 2);
                        break;
                    case 340:
                        string handle = text.Trim();
                        settings.ReusedHandle = handle.Length == 0 || handle == "0" ? null : handle;
                        break;
                }
            }

            return settings;
        }

        /// <summary>
        /// Writes the settings into a drawing, as the dialog's OK writes them: the "AcadDim" XRECORD of the named object
        /// dictionary made or its data replaced, group 90 of 990106 first, then 3, 40, 60 to 72, 170 and, for Reuse Current,
        /// 340. A drawing of no named object dictionary (R12) keeps none. Call it inside an edit of the document.
        /// </summary>
        /// <param name="database">The drawing.</param>
        internal void Write(DxfDatabase database)
        {
            DxfDBDictionary root = database.NamedObjectsDictionary;

            if (root == null)
            {
                return;
            }

            var data = new List<DxfTypedValue>
            {
                new DxfTypedValue(90, Marker.ToString(CultureInfo.InvariantCulture)),
                new DxfTypedValue(3, UserArrow ?? string.Empty),
                new DxfTypedValue(40, Width.ToString("R", CultureInfo.InvariantCulture)),
                Small(60, (int)Annotation),
                Small(61, (int)Reuse),
                Small(62, (int)RightSide),
                Small(63, (int)LeftSide),
                Small(64, Underline ? 1 : 0),
                Small(65, Spline ? 1 : 0),
                Small(66, NoLimit ? 1 : 0),
                Small(67, MaxPoints),
                Small(68, PromptForWidth ? 1 : 0),
                Small(69, AlwaysLeftJustify ? 1 : 0),
                Small(70, (int)FirstSegment),
                Small(71, (int)SecondSegment),
                Small(72, FrameText ? 1 : 0),
                Small(170, Tab),
            };

            if (Reuse == CadQLeaderReuse.ReuseCurrent && !string.IsNullOrEmpty(ReusedHandle))
            {
                data.Add(new DxfTypedValue(340, ReusedHandle));
            }

            DxfXrecord record = Record(database);

            if (record != null)
            {
                record.Data = data;
            }
            else
            {
                root.SetAt(DictionaryKey, new DxfXrecord { Data = data });
            }
        }

        private static DxfXrecord Record(DxfDatabase database)
        {
            DxfDBDictionary root = database.NamedObjectsDictionary;
            return root != null && root.Contains(DictionaryKey) ? root.GetAt(DictionaryKey) as DxfXrecord : null;
        }

        private static DxfTypedValue Small(int code, int value) =>
            new DxfTypedValue(code, value.ToString(CultureInfo.InvariantCulture));

        private static int Clamp(int value, int most) => Math.Max(0, Math.Min(most, value));
    }

    /// <summary>
    /// AutoCAD's Leader Settings of QLEADER, as data its command and the window share: the drawing's settings, which the dialog
    /// changes as it likes, and the arrowhead (DIMLDRBLK) by its name. OK writes both into the drawing as one undo step;
    /// Cancel writes nothing.
    /// </summary>
    public sealed class CadQLeaderRequest
    {
        internal CadQLeaderRequest(CadDocument document, CadQLeaderSettings settings, string arrow)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Arrow = arrow ?? CadArrowheads.ClosedFilled;
            var arrows = new List<string>(CadArrowheads.Names);

            if (!arrows.Contains(Arrow))
            {
                arrows.Add(Arrow);
            }

            Arrows = arrows;
            var blocks = new List<string>();

            foreach (DxfBlockTableRecord record in document.Database.BlockTable)
            {
                if (!record.IsLayout && !record.IsAnonymous && !record.IsFromExternalReference)
                {
                    blocks.Add(record.Name);
                }
            }

            blocks.Sort(
                (a, b) =>
                {
                    int order = StringComparer.OrdinalIgnoreCase.Compare(a, b);
                    return order != 0 ? order : StringComparer.Ordinal.Compare(a, b);
                }
            );
            Blocks = blocks;
        }

        /// <summary>Gets the document.</summary>
        public CadDocument Document { get; }

        /// <summary>Gets the settings: the drawing's when the dialog opens, the dialog's on OK.</summary>
        public CadQLeaderSettings Settings { get; }

        /// <summary>Gets the arrowheads offered, as <see cref="CadArrowheads.Names"/>, and a user arrow the drawing's DIMLDRBLK names.</summary>
        public IReadOnlyList<string> Arrows { get; }

        /// <summary>
        /// Gets the blocks of the drawing a user arrow can be, as AutoCAD's Select Custom Arrow Block lists them: each block but
        /// the layouts, the anonymous ones and the external references, by name, letters of either case together.
        /// </summary>
        public IReadOnlyList<string> Blocks { get; }

        /// <summary>
        /// Gets or sets the arrowhead, by its name in <see cref="Arrows"/>, or a block of <see cref="Blocks"/> for a user arrow:
        /// DIMLDRBLK's when the dialog opens.
        /// </summary>
        public string Arrow { get; set; }

        /// <summary>Gets or sets what the dialog was closed with: OK writes the settings and the arrow; Cancel until set.</summary>
        public CadDialogAction Action { get; set; } = CadDialogAction.Cancel;
    }
}
