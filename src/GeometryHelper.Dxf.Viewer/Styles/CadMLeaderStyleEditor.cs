using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// What the Modify Multileader Style dialog sets of a multileader style, its three tabs: Leader Format (the type, colour,
    /// linetype and lineweight of the leaders, the arrowhead and its size, the break size), Leader Structure (the most points
    /// of a leader, the angles of its segments, the landing and its distance, the scale) and Content (the type, the default
    /// text, the text style, angle, colour and height, the justification and frame, and where the leaders meet the text).
    /// </summary>
    /// <remarks>A new style of the presenter is metric: arrows 2.5, break 3.75, landing 8, text 2.5 high, a landing gap of 2.</remarks>
    public sealed class CadMLeaderStyleSettings
    {
        /// <summary>Gets or sets how the leaders are drawn: straight, splined or not drawn.</summary>
        public DxfLeaderType LeaderType { get; set; } = DxfLeaderType.StraightLeader;

        /// <summary>Gets or sets the colour of the leaders.</summary>
        public DxfColor LeaderColor { get; set; } = DxfColor.ByBlock;

        /// <summary>Gets or sets the name of the linetype of the leaders: ByBlock, ByLayer or one of the drawing's.</summary>
        public string LeaderLinetype { get; set; } = "ByBlock";

        /// <summary>Gets or sets the lineweight of the leaders.</summary>
        public DxfLineWeight LeaderLineweight { get; set; } = DxfLineWeight.ByBlock;

        /// <summary>Gets or sets the arrowhead, by the name <see cref="CadArrowheads.Names"/> gives it: Closed filled, Dot, Open 30.</summary>
        public string Arrowhead { get; set; } = CadArrowheads.ClosedFilled;

        /// <summary>Gets or sets the size of the arrowhead.</summary>
        public double ArrowSize { get; set; } = 2.5;

        /// <summary>Gets or sets the size of the gap a leader is broken by (DIMBREAK).</summary>
        public double BreakSize { get; set; } = 3.75;

        /// <summary>Gets or sets the most points a leader is drawn through; 0 for no limit.</summary>
        public int MaxLeaderPoints { get; set; } = 2;

        /// <summary>Gets or sets the angle the first segment of a leader is kept to.</summary>
        public DxfAngleConstraint FirstSegmentAngle { get; set; } = DxfAngleConstraint.DegreesAny;

        /// <summary>Gets or sets the angle the second segment of a leader is kept to.</summary>
        public DxfAngleConstraint SecondSegmentAngle { get; set; } = DxfAngleConstraint.DegreesAny;

        /// <summary>Gets or sets whether a leader ends in a landing.</summary>
        public bool IncludeLanding { get; set; } = true;

        /// <summary>Gets or sets whether the landing is of the distance set (<see cref="LandingDistance"/>).</summary>
        public bool SetLandingDistance { get; set; } = true;

        /// <summary>Gets or sets the distance of the landing.</summary>
        public double LandingDistance { get; set; } = 8.0;

        /// <summary>Gets or sets whether the style is annotative, as the drawing has it; not written.</summary>
        public bool Annotative { get; set; }

        /// <summary>Gets or sets the scale of the multileaders; 0 for Scale multileaders to layout.</summary>
        public double Scale { get; set; } = 1.0;

        /// <summary>Gets or sets what the leaders point to: words or nothing; a block is not offered.</summary>
        public DxfContentType ContentType { get; set; } = DxfContentType.MTextContent;

        /// <summary>Gets or sets the default text; empty for none.</summary>
        public string DefaultText { get; set; } = string.Empty;

        /// <summary>Gets or sets the name of the text style.</summary>
        public string TextStyle { get; set; } = "Standard";

        /// <summary>Gets or sets how the text is turned.</summary>
        public DxfTextAngleType TextAngle { get; set; } = DxfTextAngleType.HorizontalAngle;

        /// <summary>Gets or sets the colour of the text.</summary>
        public DxfColor TextColor { get; set; } = DxfColor.ByBlock;

        /// <summary>Gets or sets the height of the text.</summary>
        public double TextHeight { get; set; } = 2.5;

        /// <summary>Gets or sets whether the text is always left justified.</summary>
        public bool AlwaysLeftJustify { get; set; }

        /// <summary>Gets or sets whether a frame is drawn round the text.</summary>
        public bool FrameText { get; set; }

        /// <summary>Gets or sets which sides of the text the leaders meet: the left and right (horizontal) or the top and bottom (vertical).</summary>
        public DxfTextAttachmentDirection AttachmentDirection { get; set; } = DxfTextAttachmentDirection.AttachmentHorizontal;

        /// <summary>Gets or sets where a leader on the left meets the text.</summary>
        public DxfTextAttachmentType LeftAttachment { get; set; } = DxfTextAttachmentType.AttachmentMiddleOfTop;

        /// <summary>Gets or sets where a leader on the right meets the text.</summary>
        public DxfTextAttachmentType RightAttachment { get; set; } = DxfTextAttachmentType.AttachmentMiddleOfTop;

        /// <summary>Gets or sets where a leader at the top meets the text.</summary>
        public DxfTextAttachmentType TopAttachment { get; set; } = DxfTextAttachmentType.AttachmentCenter;

        /// <summary>Gets or sets where a leader at the bottom meets the text.</summary>
        public DxfTextAttachmentType BottomAttachment { get; set; } = DxfTextAttachmentType.AttachmentCenter;

        /// <summary>Gets or sets the gap between the landing and the text.</summary>
        public double LandingGap { get; set; } = 2.0;

        /// <summary>Gets or sets whether a leader runs on to the text (R2013 and later).</summary>
        public bool ExtendLeaderToText { get; set; }

        /// <summary>Reads the settings of a style.</summary>
        /// <param name="style">The style.</param>
        /// <returns>Its settings.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        public static CadMLeaderStyleSettings Of(DxfMLeaderStyle style)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            DxfLinetypeTableRecord linetype = style.LeaderLineLinetype;
            return new CadMLeaderStyleSettings
            {
                LeaderType = style.LeaderLineType,
                LeaderColor = style.LeaderLineColor,
                LeaderLinetype = linetype == null ? "ByBlock" : linetype.Name,
                LeaderLineweight = style.LeaderLineWeight,
                Arrowhead = style.ArrowSymbol == null ? CadArrowheads.ClosedFilled : CadArrowheads.NameOf(style.ArrowSymbol),
                ArrowSize = style.ArrowSize,
                BreakSize = style.BreakSize,
                MaxLeaderPoints = style.MaxLeaderSegmentsPoints,
                FirstSegmentAngle = style.FirstSegmentAngleConstraint,
                SecondSegmentAngle = style.SecondSegmentAngleConstraint,
                IncludeLanding = style.EnableLanding,
                SetLandingDistance = style.EnableDogleg,
                LandingDistance = style.DoglegLength,
                Annotative = style.Annotative == DxfAnnotativeStates.True,
                Scale = style.Scale,
                ContentType = style.ContentType,
                DefaultText = style.DefaultMText?.Contents ?? string.Empty,
                TextStyle = style.TextStyle?.Name ?? "Standard",
                TextAngle = style.TextAngleType,
                TextColor = style.TextColor,
                TextHeight = style.TextHeight,
                AlwaysLeftJustify = style.TextAlignAlwaysLeft,
                FrameText = style.EnableFrameText,
                AttachmentDirection = style.TextAttachmentDirection,
                LeftAttachment = style.GetTextAttachmentType(DxfLeaderDirectionType.LeftLeader),
                RightAttachment = style.GetTextAttachmentType(DxfLeaderDirectionType.RightLeader),
                TopAttachment = style.GetTextAttachmentType(DxfLeaderDirectionType.TopLeader),
                BottomAttachment = style.GetTextAttachmentType(DxfLeaderDirectionType.BottomLeader),
                LandingGap = style.LandingGap,
                ExtendLeaderToText = style.ExtendLeaderToText,
            };
        }

        /// <summary>Gets a copy of the settings.</summary>
        /// <returns>The copy.</returns>
        public CadMLeaderStyleSettings Clone() => (CadMLeaderStyleSettings)MemberwiseClone();

        /// <summary>Gets whether two settings write the same style.</summary>
        /// <param name="other">The other.</param>
        /// <returns>True where every value is the same, names of either case alike.</returns>
        public bool SameAs(CadMLeaderStyleSettings other)
            => other != null && LeaderType == other.LeaderType && SameColor(LeaderColor, other.LeaderColor)
                && string.Equals(LeaderLinetype, other.LeaderLinetype, StringComparison.OrdinalIgnoreCase) && LeaderLineweight == other.LeaderLineweight
                && string.Equals(Arrowhead, other.Arrowhead, StringComparison.OrdinalIgnoreCase) && ArrowSize.Equals(other.ArrowSize) && BreakSize.Equals(other.BreakSize)
                && MaxLeaderPoints == other.MaxLeaderPoints && FirstSegmentAngle == other.FirstSegmentAngle && SecondSegmentAngle == other.SecondSegmentAngle
                && IncludeLanding == other.IncludeLanding && SetLandingDistance == other.SetLandingDistance && LandingDistance.Equals(other.LandingDistance)
                && Annotative == other.Annotative && Scale.Equals(other.Scale) && ContentType == other.ContentType
                && string.Equals(DefaultText ?? string.Empty, other.DefaultText ?? string.Empty, StringComparison.Ordinal)
                && string.Equals(TextStyle, other.TextStyle, StringComparison.OrdinalIgnoreCase) && TextAngle == other.TextAngle && SameColor(TextColor, other.TextColor)
                && TextHeight.Equals(other.TextHeight) && AlwaysLeftJustify == other.AlwaysLeftJustify && FrameText == other.FrameText
                && AttachmentDirection == other.AttachmentDirection && LeftAttachment == other.LeftAttachment && RightAttachment == other.RightAttachment
                && TopAttachment == other.TopAttachment && BottomAttachment == other.BottomAttachment && LandingGap.Equals(other.LandingGap)
                && ExtendLeaderToText == other.ExtendLeaderToText;

        internal static bool SameColor(DxfColor a, DxfColor b)
            => a == null ? b == null : b != null && a.ColorMethod == b.ColorMethod && a.ColorIndex == b.ColorIndex && (a.ColorMethod != DxfColorMethod.ByColor || a.ColorValue == b.ColorValue);
    }

    /// <summary>
    /// The edits of the multileader styles, as AutoCAD's Multileader Style Manager (MLEADERSTYLE) makes them: each one undo
    /// step through <see cref="CadDocument.ApplyEdit"/>, refused where AutoCAD refuses it. A style changed gives its values to
    /// every multileader of it, but those a multileader has of its own.
    /// </summary>
    public static class CadMLeaderStyleEditor
    {
        /// <summary>
        /// Gets the multileader styles of a drawing, by name, letters of either case alike first: the entries of its dictionary
        /// ACAD_MLEADERSTYLE that are styles; none where it has none.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The names.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<string> Styles(DxfDatabase database)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var names = new List<string>();

            try
            {
                DxfDBDictionary styles = database.MLeaderStyleDictionary;

                if (styles != null)
                {
                    foreach (DxfDBDictionaryEntry entry in styles)
                    {
                        if (entry.Value is DxfMLeaderStyle)
                        {
                            names.Add(entry.Key);
                        }
                    }
                }
            }
            catch (InvalidOperationException exception)
            {
                GeometryHelperLog.Debug("CadMLeaderStyleEditor: the multileader styles were not read: " + exception.Message);
            }

            names.Sort((a, b) =>
            {
                int byCase = string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
                return byCase != 0 ? byCase : string.CompareOrdinal(a, b);
            });
            return names;
        }

        /// <summary>Gets the style of a name; null where the drawing has none.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The name, letters of either case the same.</param>
        /// <returns>The style, or null.</returns>
        public static DxfMLeaderStyle Find(DxfDatabase database, string name)
        {
            DxfDBDictionary styles = database?.MLeaderStyleDictionary;
            return styles != null && name != null && styles.Contains(name.Trim()) ? styles[name.Trim()] as DxfMLeaderStyle : null;
        }

        /// <summary>Gets the name of the current multileader style (CMLEADERSTYLE), as the dictionary spells it; Standard where the drawing names none it has.</summary>
        /// <param name="database">The drawing.</param>
        /// <returns>The name.</returns>
        public static string Current(DxfDatabase database)
        {
            string name;

            try
            {
                name = database.MLeaderstyle;
            }
            catch (Exception exception) when (exception is InvalidOperationException || exception is ArgumentException)
            {
                name = null;
            }

            DxfMLeaderStyle style = Find(database, name);
            return style != null ? style.Name : "Standard";
        }

        /// <summary>
        /// Gets the handles of the multileaders drawn in a style: those of model space, the layouts and the blocks that name it,
        /// in the order of the blocks and their entities, as AutoCAD counts what keeps a style from being deleted.
        /// </summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The handles.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static IReadOnlyList<string> Uses(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            var handles = new List<string>();
            string set = Find(database, name)?.Name;

            if (set == null)
            {
                return handles;
            }

            foreach (DxfBlockTableRecord block in database.BlockTable)
            {
                foreach (DxfEntity entity in block)
                {
                    if (entity is DxfMLeader mleader && string.Equals(StyleName(mleader), set, StringComparison.OrdinalIgnoreCase))
                    {
                        handles.Add(mleader.Handle);
                    }
                }
            }

            return handles;
        }

        /// <summary>
        /// Makes a style current (CMLEADERSTYLE), as Set Current does. Refused where the drawing has no such style, or cannot
        /// hold the variable: a drawing whose CLASSES section lists no DICTIONARYVAR, as one the library makes new.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult SetCurrent(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            DxfMLeaderStyle style = Find(document.Database, name);

            if (style == null)
            {
                return CadEditResult.Refuse("The drawing has no multileader style \"" + name + "\".");
            }

            string set = style.Name;
            string old = Current(document.Database);

            if (string.Equals(old, set, StringComparison.Ordinal))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            CadEditResult result = document.ApplyEdit("MLEADERSTYLE", new[] { new CadEditRecord(CadEditRecordKind.SettingChanged, null, "CMLEADERSTYLE", old, set) }, s => s.Database.MLeaderstyle = set);
            return result.Succeeded ? result : CadEditResult.Refuse("\"" + set + "\" cannot be made current: " + result.Message);
        }

        /// <summary>
        /// Adds a style, as New of the Multileader Style Manager: a copy of the settings of another (Start with), under a name
        /// no style of the drawing has, letters of either case the same; the dictionary ACAD_MLEADERSTYLE made where the drawing
        /// has none. A copy of an annotative style is not annotative: the library does not write the flag.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The new name, as Detail notes.</param>
        /// <param name="from">The style whose settings it takes; the presenter's metric Standard where null or none.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult Add(CadDocument document, string name, string from)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string trimmed = (name ?? string.Empty).Trim();

            if (trimmed.Length == 0)
            {
                return CadEditResult.Refuse("A multileader style needs a name.");
            }

            if (trimmed.IndexOfAny(new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', ',', '=', '`' }) >= 0)
            {
                return CadEditResult.Refuse("The name \"" + trimmed + "\" has a letter AutoCAD does not take in a name.");
            }

            if (Find(document.Database, trimmed) != null)
            {
                return CadEditResult.Refuse("Multileader style \"" + trimmed + "\" already exists.");
            }

            DxfMLeaderStyle source = from == null ? null : Find(document.Database, from);
            CadMLeaderStyleSettings settings = source == null ? new CadMLeaderStyleSettings() : CadMLeaderStyleSettings.Of(source);
            settings.Annotative = false;
            return document.ApplyEdit("MLEADERSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New multileader style", null, trimmed) }, s =>
            {
                var style = new DxfMLeaderStyle();
                Dictionary(s.Database).SetAt(trimmed, style);
                Write(s.Database, style, settings);
            });
        }

        /// <summary>
        /// Writes the settings into a style, as OK of the Modify Multileader Style dialog, and gives every multileader of the
        /// style its values, those it has of its own kept (a value that differed from the style's before). Nothing is written,
        /// and nothing refused, where they are the style's already.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <param name="settings">The settings.</param>
        /// <returns>What came of it; refused for a text height not above nought, a size below nought, a text style or a linetype the drawing has not.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the settings are null.</exception>
        public static CadEditResult Apply(CadDocument document, string name, CadMLeaderStyleSettings settings)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            DxfMLeaderStyle style = Find(document.Database, name);

            if (style == null)
            {
                return CadEditResult.Refuse("The drawing has no multileader style \"" + name + "\".");
            }

            string wrong = Check(document.Database, settings);

            if (wrong != null)
            {
                return CadEditResult.Refuse(wrong);
            }

            CadMLeaderStyleSettings old = CadMLeaderStyleSettings.Of(style);

            if (old.SameAs(settings))
            {
                return CadEditResult.Done(Array.Empty<CadEditRecord>());
            }

            string set = style.Name;
            IReadOnlyList<string> uses = Uses(document.Database, set);
            var planned = new List<CadEditRecord> { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Multileader style " + set, null, set) };
            planned.AddRange(uses.Select(handle => new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, "Multileader style", set, set)));
            return document.ApplyEdit("MLEADERSTYLE", planned, s =>
            {
                Write(s.Database, Find(s.Database, set), settings);

                foreach (string handle in uses)
                {
                    if (!s.IsHandled(handle) && s.Document.FindEntity(handle) is DxfMLeader mleader)
                    {
                        Restyle(mleader, old);
                    }
                }
            });
        }

        /// <summary>
        /// Deletes a style, as Delete of the Multileader Style Manager: refused for Standard, the current style and a style a
        /// multileader is drawn in, as AutoCAD greys Delete for them.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult Delete(CadDocument document, string name)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string reason = WhyNotDeleted(document.Database, name);

            if (reason != null)
            {
                return CadEditResult.Refuse(reason);
            }

            string set = Find(document.Database, name).Name;
            return document.ApplyEdit("MLEADERSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "Delete multileader style", set, null) }, s => s.Database.MLeaderStyleDictionary.Remove(set));
        }

        /// <summary>Gets why a style may not be deleted, as Delete is greyed: Standard, the current style, a style a multileader uses; null where it may.</summary>
        /// <param name="database">The drawing.</param>
        /// <param name="name">The style's name.</param>
        /// <returns>The reason, or null.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing is null.</exception>
        public static string WhyNotDeleted(DxfDatabase database, string name)
        {
            if (database == null)
            {
                throw new ArgumentNullException(nameof(database));
            }

            DxfMLeaderStyle style = Find(database, name);

            if (style == null)
            {
                return "The drawing has no multileader style \"" + name + "\".";
            }

            if (string.Equals(style.Name, "Standard", StringComparison.OrdinalIgnoreCase))
            {
                return "The Standard multileader style cannot be deleted.";
            }

            if (string.Equals(style.Name, Current(database), StringComparison.OrdinalIgnoreCase))
            {
                return "The current multileader style cannot be deleted.";
            }

            int uses = Uses(database, style.Name).Count;
            return uses > 0 ? "\"" + style.Name + "\" is in use by " + uses + (uses == 1 ? " multileader" : " multileaders") + " and cannot be deleted." : null;
        }

        /// <summary>
        /// Adds the presenter's metric Standard where the drawing has no multileader style of that name, as AutoCAD's drawing
        /// always has it; one undo step, nothing where it has one.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>What came of it.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document is null.</exception>
        public static CadEditResult AddStandard(CadDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            return Find(document.Database, "Standard") != null
                ? CadEditResult.Done(Array.Empty<CadEditRecord>())
                : document.ApplyEdit("MLEADERSTYLE", new[] { new CadEditRecord(CadEditRecordKind.TableChanged, null, "New multileader style", null, "Standard") }, s =>
                {
                    var style = new DxfMLeaderStyle();
                    Dictionary(s.Database).SetAt("Standard", style);
                    Write(s.Database, style, new CadMLeaderStyleSettings());
                });
        }

        /// <summary>
        /// Makes a multileader in a style as MLEADER draws one, not yet added: its arrow at a point, its words of the style's
        /// height times its scale with their landing at another, the landing gap, the dogleg and the arrows the style's; added
        /// to the drawing, it is to be given the rest of the style by <see cref="DxfMLeader.SetFromStyle"/>.
        /// </summary>
        /// <param name="style">The style.</param>
        /// <param name="arrow">Where the arrow points.</param>
        /// <param name="landing">Where the leader lands.</param>
        /// <param name="words">The words; null or empty for a style of no content.</param>
        /// <returns>The multileader.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the style is null.</exception>
        internal static DxfMLeader Make(DxfMLeaderStyle style, GeoPoint3 arrow, GeoPoint3 landing, string words)
        {
            if (style == null)
            {
                throw new ArgumentNullException(nameof(style));
            }

            double scale = style.Scale > 0.0 ? style.Scale : 1.0;
            var mleader = new DxfMLeader();

            if (scale != 1.0)
            {
                mleader.Scale = scale;
            }

            mleader.LandingGap = style.LandingGap;
            mleader.DoglegLength = style.DoglegLength;
            mleader.ArrowSize = style.ArrowSize;

            if (!string.IsNullOrEmpty(words))
            {
                mleader.MText = new DxfMText { Contents = words, Location = landing, TextHeight = style.TextHeight * scale };
            }

            mleader.AddLeaderLine(arrow);
            return mleader;
        }

        /// <summary>Adds the presenter's metric Standard where the drawing has no multileader style of that name; within an edit.</summary>
        internal static void EnsureStandard(DxfDatabase database)
        {
            if (Find(database, "Standard") != null)
            {
                return;
            }

            var style = new DxfMLeaderStyle();
            Dictionary(database).SetAt("Standard", style);
            Write(database, style, new CadMLeaderStyleSettings());
        }

        /// <summary>
        /// Makes the sample multileader of the Manager's preview: a drawing of its own whose one style, Standard, has the
        /// settings (its text style Standard given the font of the one they name in the drawing edited, its leaders ByBlock),
        /// and a multileader of the words "Default Text" in it, its arrow at the origin and its landing 6 by 4 text heights up
        /// to the right, made as MLEADER makes one.
        /// </summary>
        /// <param name="source">The drawing edited, whose text style's font the words take; null for Standard's own.</param>
        /// <param name="settings">The settings of the style.</param>
        /// <returns>The drawing of the sample; of no multileader where the settings do not make a style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public static DxfDatabase Sample(DxfDatabase source, CadMLeaderStyleSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var database = new DxfDatabase("AC1027");

            try
            {
                if (source != null && source.TextStyleTable.Has(settings.TextStyle ?? string.Empty))
                {
                    DxfTextStyleTableRecord from = source.TextStyleTable[settings.TextStyle];
                    DxfTextStyleTableRecord standard = database.TextStyleTable["Standard"];
                    standard.FileName = from.FileName ?? string.Empty;
                    standard.BigFontFileName = from.BigFontFileName ?? string.Empty;
                    standard.XScale = from.XScale > 0.0 ? from.XScale : 1.0;
                    standard.Font = from.Font;
                }

                var style = new DxfMLeaderStyle();
                Dictionary(database).SetAt("Standard", style);
                CadMLeaderStyleSettings shown = settings.Clone();
                shown.TextStyle = "Standard";
                shown.LeaderLinetype = "ByBlock";
                Write(database, style, shown);

                double size = settings.TextHeight > 0.0 && !double.IsInfinity(settings.TextHeight) ? settings.TextHeight : 2.5;
                string words = settings.ContentType == DxfContentType.MTextContent ? "Default Text" : null;
                DxfMLeader mleader = Make(style, new GeoPoint3(0, 0, 0), new GeoPoint3(6.0 * size, 4.0 * size, 0), words);
                database.ModelSpace.AppendEntity(mleader);
                mleader.SetFromStyle();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug("CadMLeaderStyleEditor: the sample multileader was not made: " + exception.Message);
            }

            return database;
        }

        /// <summary>Why settings cannot be written into a drawing; null where they can.</summary>
        private static string Check(DxfDatabase database, CadMLeaderStyleSettings settings)
        {
            bool Size(double value) => value >= 0.0 && !double.IsInfinity(value);

            if (!(settings.TextHeight > 0.0) || double.IsInfinity(settings.TextHeight))
            {
                return "Text height must be a number above nought.";
            }

            if (!Size(settings.ArrowSize) || !Size(settings.BreakSize) || !Size(settings.LandingDistance) || !Size(settings.LandingGap) || !Size(settings.Scale))
            {
                return "Sizes and the scale must be numbers of nought or more.";
            }

            if (settings.MaxLeaderPoints != 0 && settings.MaxLeaderPoints < 2)
            {
                return "Maximum leader points must be 2 or more.";
            }

            if (!database.TextStyleTable.Has(settings.TextStyle ?? string.Empty))
            {
                return "The drawing has no text style \"" + settings.TextStyle + "\".";
            }

            if (!database.LinetypeTable.Has(settings.LeaderLinetype ?? string.Empty))
            {
                return "The drawing has no linetype \"" + settings.LeaderLinetype + "\".";
            }

            return null;
        }

        /// <summary>The dictionary of multileader styles, made where the drawing has none.</summary>
        private static DxfDBDictionary Dictionary(DxfDatabase database)
        {
            if (database.MLeaderStyleDictionary == null)
            {
                database.NamedObjectsDictionary.SetAt("ACAD_MLEADERSTYLE", new DxfDBDictionary());
            }

            return database.MLeaderStyleDictionary;
        }

        /// <summary>The name of the style a multileader names (its 340); null where it names none of its drawing's.</summary>
        private static string StyleName(DxfMLeader mleader)
        {
            try
            {
                return mleader.MLeaderStyle;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>
        /// Gives a multileader the values of its style, those it had of its own kept: a size, a colour or a type that differed
        /// from the old settings of the style is written again after <see cref="DxfMLeader.SetFromStyle"/>, as an override.
        /// </summary>
        private static void Restyle(DxfMLeader mleader, CadMLeaderStyleSettings old)
        {
            double arrow = mleader.ArrowSize;
            double dogleg = mleader.DoglegLength;
            double gap = mleader.LandingGap;
            double height = mleader.TextHeight;
            DxfColor leaderColor = mleader.LeaderLineColor;
            DxfColor textColor = mleader.TextColor;
            DxfLeaderType type = mleader.LeaderLineType;
            DxfLineWeight weight = mleader.LeaderLineWeight;
            bool frame = mleader.EnableFrameText;

            if (!mleader.SetFromStyle())
            {
                return;
            }

            if (!arrow.Equals(old.ArrowSize))
            {
                mleader.ArrowSize = arrow;
            }

            if (!dogleg.Equals(old.LandingDistance))
            {
                mleader.DoglegLength = dogleg;
            }

            if (!gap.Equals(old.LandingGap))
            {
                mleader.LandingGap = gap;
            }

            if (!height.Equals(old.TextHeight))
            {
                mleader.TextHeight = height;
            }

            if (!CadMLeaderStyleSettings.SameColor(leaderColor, old.LeaderColor))
            {
                mleader.LeaderLineColor = leaderColor;
            }

            if (!CadMLeaderStyleSettings.SameColor(textColor, old.TextColor))
            {
                mleader.TextColor = textColor;
            }

            if (type != old.LeaderType)
            {
                mleader.LeaderLineType = type;
            }

            if (weight != old.LeaderLineweight)
            {
                mleader.LeaderLineWeight = weight;
            }

            if (frame != old.FrameText)
            {
                mleader.EnableFrameText = frame;
            }
        }

        /// <summary>Writes settings into a style of a drawing: every value the dialog sets, the arrowhead's block made where the drawing has none.</summary>
        private static void Write(DxfDatabase database, DxfMLeaderStyle style, CadMLeaderStyleSettings settings)
        {
            style.LeaderLineType = settings.LeaderType;
            style.LeaderLineColor = settings.LeaderColor ?? DxfColor.ByBlock;
            style.LeaderLineLinetype = database.LinetypeTable.Has(settings.LeaderLinetype ?? string.Empty) ? database.LinetypeTable[settings.LeaderLinetype] : null;
            style.LeaderLineWeight = settings.LeaderLineweight;
            style.ArrowSymbol = string.Equals(settings.Arrowhead, CadArrowheads.ClosedFilled, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(settings.Arrowhead)
                ? null
                : CadArrowheads.Ensure(database, settings.Arrowhead);
            style.ArrowSize = settings.ArrowSize;
            style.BreakSize = settings.BreakSize;
            style.MaxLeaderSegmentsPoints = settings.MaxLeaderPoints;
            style.FirstSegmentAngleConstraint = settings.FirstSegmentAngle;
            style.SecondSegmentAngleConstraint = settings.SecondSegmentAngle;
            style.EnableLanding = settings.IncludeLanding;
            style.EnableDogleg = settings.SetLandingDistance;
            style.DoglegLength = settings.LandingDistance;
            style.Scale = settings.Scale;
            style.ContentType = settings.ContentType == DxfContentType.BlockContent ? style.ContentType : settings.ContentType;
            style.DefaultMText = string.IsNullOrEmpty(settings.DefaultText) ? null : new DxfMText { Contents = settings.DefaultText };
            style.TextStyle = database.TextStyleTable[settings.TextStyle];
            style.TextAngleType = settings.TextAngle;
            style.TextColor = settings.TextColor ?? DxfColor.ByBlock;
            style.TextHeight = settings.TextHeight;
            style.TextAlignAlwaysLeft = settings.AlwaysLeftJustify;
            style.EnableFrameText = settings.FrameText;
            style.TextAttachmentDirection = settings.AttachmentDirection;
            style.SetTextAttachmentType(settings.LeftAttachment, DxfLeaderDirectionType.LeftLeader);
            style.SetTextAttachmentType(settings.RightAttachment, DxfLeaderDirectionType.RightLeader);
            style.SetTextAttachmentType(settings.TopAttachment, DxfLeaderDirectionType.TopLeader);
            style.SetTextAttachmentType(settings.BottomAttachment, DxfLeaderDirectionType.BottomLeader);
            style.LandingGap = settings.LandingGap;

            if (string.CompareOrdinal(database.Version ?? string.Empty, "AC1027") >= 0)
            {
                style.ExtendLeaderToText = settings.ExtendLeaderToText;
            }
        }
    }
}
