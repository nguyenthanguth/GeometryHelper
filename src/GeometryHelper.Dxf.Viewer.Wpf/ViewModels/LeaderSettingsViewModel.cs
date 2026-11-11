using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>A row of the Attachment tab of Leader Settings: its words and whether it is chosen for the text on either side.</summary>
    public sealed class LeaderAttachmentRowViewModel : ObservableObject
    {
        private readonly LeaderSettingsViewModel _owner;

        internal LeaderAttachmentRowViewModel(LeaderSettingsViewModel owner, CadQLeaderAttachment row, string name)
        {
            _owner = owner;
            Row = row;
            Name = name;
        }

        /// <summary>Gets the row, as the settings keep it.</summary>
        public CadQLeaderAttachment Row { get; }

        /// <summary>Gets the words of the row, as "Middle of top line".</summary>
        public string Name { get; }

        /// <summary>Gets or sets whether the row is chosen for text on the left side of the leader; set true, it is.</summary>
        public bool IsLeft
        {
            get => _owner.LeftSide == Row;
            set
            {
                if (value)
                {
                    _owner.LeftSide = Row;
                }
            }
        }

        /// <summary>Gets or sets whether the row is chosen for text on the right side of the leader; set true, it is.</summary>
        public bool IsRight
        {
            get => _owner.RightSide == Row;
            set
            {
                if (value)
                {
                    _owner.RightSide = Row;
                }
            }
        }

        internal void Refresh()
        {
            Raise(nameof(IsLeft));
            Raise(nameof(IsRight));
        }

        /// <inheritdoc/>
        public override string ToString() =>
            Name + (IsLeft ? " left" : string.Empty) + (IsRight ? " right" : string.Empty);
    }

    /// <summary>
    /// An entry of the arrowhead list of Leader Settings: its name and its picture, as AutoCAD's list shows them, or the
    /// last entry, "User Arrow...", which asks for a block of the drawing.
    /// </summary>
    public sealed class LeaderArrowChoiceViewModel : ObservableObject
    {
        private readonly Func<string, BitmapSource> _draw;
        private BitmapSource _picture;
        private bool _drawn;

        internal LeaderArrowChoiceViewModel(string name, Func<string, BitmapSource> draw)
        {
            Name = name;
            _draw = draw;
        }

        /// <summary>Gets the name, as "Closed filled", a user arrow's block, or "User Arrow...".</summary>
        public string Name { get; }

        /// <summary>Gets whether this is "User Arrow...", which asks for a block rather than naming an arrowhead.</summary>
        public bool IsUserArrowEntry => Name == LeaderSettingsViewModel.UserArrowEntry;

        /// <summary>
        /// Gets the picture of the arrowhead, 22 by 12 pixels, drawn the first time it is asked: a leader of it pointing left,
        /// as a leader draws it; null for "User Arrow..." and where it cannot be drawn.
        /// </summary>
        public BitmapSource Picture
        {
            get
            {
                if (!_drawn && !IsUserArrowEntry)
                {
                    _drawn = true;
                    _picture = _draw(Name);
                }

                return _picture;
            }
        }

        /// <inheritdoc/>
        public override string ToString() => Name;
    }

    /// <summary>
    /// AutoCAD 2021's Leader Settings of QLEADER over the request its command hands the window: Annotation (the annotation
    /// type, the MText options, Annotation Reuse), Leader Line &amp; Arrow (straight or spline, the arrowhead, the number of
    /// points, the angle constraints) and Attachment (where the leader meets text on either side, Underline bottom line). The
    /// Attachment tab and the MText options are greyed unless the type is MText, the ten round buttons with Underline, Maximum
    /// with No Limit. Tolerance shows the Geometric Tolerance dialog after QLEADER's points. The arrowheads are listed with
    /// their pictures, then "User Arrow...", which opens AutoCAD's Select Custom Arrow Block over the dialog to choose a block
    /// of the drawing (<see cref="IsChoosingUserArrow"/>). OK writes the settings and the arrowhead into the request; Cancel
    /// leaves it.
    /// </summary>
    public sealed class LeaderSettingsViewModel : ObservableObject
    {
        /// <summary>The Annotation tab.</summary>
        public const string AnnotationTab = "Annotation";

        /// <summary>The Leader Line &amp; Arrow tab.</summary>
        public const string LineTab = "Leader Line & Arrow";

        /// <summary>The Attachment tab.</summary>
        public const string AttachmentTab = "Attachment";

        /// <summary>The last entry of the arrowhead list, which asks for a block of the drawing as the arrow.</summary>
        public const string UserArrowEntry = "User Arrow...";

        /// <summary>The line of Select Custom Arrow Block in a drawing of no block, as board QLeaderUserArrowNoBlocks says it.</summary>
        public const string NoBlocks =
            "The drawing has no block to use as an arrow. Make one with BLOCK first, or Cancel.";

        private static readonly string[] AngleNames = { "Any angle", "Horizontal", "90°", "45°", "30°", "15°" };

        private readonly CadQLeaderSettings _settings;
        private readonly List<LeaderArrowChoiceViewModel> _choices = new List<LeaderArrowChoiceViewModel>();
        private string _tab;
        private string _maxPoints;
        private string _arrow;
        private string _notice = string.Empty;
        private bool _choosingUserArrow;
        private string _userArrowBlock = string.Empty;
        private string _userArrowMessage = string.Empty;
        private bool _userArrowNotFound;
        private DxfDatabase _copy;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of QLEADER's Settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public LeaderSettingsViewModel(CadQLeaderRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _settings = request.Settings.Clone();

            // The tab kept from last time; Attachment only while the type is MText.
            _tab = Tabs[Math.Max(0, Math.Min(2, _settings.Tab))];

            if (_tab == AttachmentTab && !IsMText)
            {
                _tab = AnnotationTab;
            }

            _maxPoints = _settings.MaxPoints.ToString(CultureInfo.InvariantCulture);
            _arrow = request.Arrow;

            foreach (string name in request.Arrows.Concat(new[] { UserArrowEntry }))
            {
                _choices.Add(new LeaderArrowChoiceViewModel(name, PictureOf));
            }

            AttachmentRows = new[]
            {
                new LeaderAttachmentRowViewModel(this, CadQLeaderAttachment.TopOfTopLine, "Top of top line"),
                new LeaderAttachmentRowViewModel(this, CadQLeaderAttachment.MiddleOfTopLine, "Middle of top line"),
                new LeaderAttachmentRowViewModel(this, CadQLeaderAttachment.MiddleOfText, "Middle of multi-line text"),
                new LeaderAttachmentRowViewModel(
                    this,
                    CadQLeaderAttachment.MiddleOfBottomLine,
                    "Middle of bottom line"
                ),
                new LeaderAttachmentRowViewModel(
                    this,
                    CadQLeaderAttachment.BottomOfBottomLine,
                    "Bottom of bottom line"
                ),
            };
        }

        /// <summary>Gets the request.</summary>
        public CadQLeaderRequest Request { get; }

        /// <summary>Gets the names of the tabs, in AutoCAD's order.</summary>
        public IReadOnlyList<string> Tabs { get; } = new[] { AnnotationTab, LineTab, AttachmentTab };

        /// <summary>Gets or sets the tab shown; the Attachment tab only while the type is MText.</summary>
        public string Tab
        {
            get => _tab;
            set
            {
                if (!Tabs.Contains(value) || (value == AttachmentTab && !IsMText) || !Set(ref _tab, value))
                {
                    return;
                }

                Raise(nameof(IsAnnotationTab));
                Raise(nameof(IsLineTab));
                Raise(nameof(IsAttachmentTab));
            }
        }

        /// <summary>Gets or sets whether the Annotation tab is shown; set true, it is.</summary>
        public bool IsAnnotationTab
        {
            get => _tab == AnnotationTab;
            set => Tab = value ? AnnotationTab : _tab;
        }

        /// <summary>Gets or sets whether the Leader Line &amp; Arrow tab is shown; set true, it is.</summary>
        public bool IsLineTab
        {
            get => _tab == LineTab;
            set => Tab = value ? LineTab : _tab;
        }

        /// <summary>Gets or sets whether the Attachment tab is shown; set true, it is.</summary>
        public bool IsAttachmentTab
        {
            get => _tab == AttachmentTab;
            set => Tab = value ? AttachmentTab : _tab;
        }

        /// <summary>Gets the annotation type.</summary>
        public CadQLeaderAnnotation Annotation => _settings.Annotation;

        /// <summary>Gets or sets whether the type is MText.</summary>
        public bool IsMText
        {
            get => _settings.Annotation == CadQLeaderAnnotation.MText;
            set => SetAnnotation(value, CadQLeaderAnnotation.MText);
        }

        /// <summary>Gets or sets whether the type is Copy an Object.</summary>
        public bool IsCopyObject
        {
            get => _settings.Annotation == CadQLeaderAnnotation.CopyObject;
            set => SetAnnotation(value, CadQLeaderAnnotation.CopyObject);
        }

        /// <summary>Gets or sets whether the type is Tolerance: QLEADER then shows the Geometric Tolerance dialog after the points.</summary>
        public bool IsTolerance
        {
            get => _settings.Annotation == CadQLeaderAnnotation.Tolerance;
            set => SetAnnotation(value, CadQLeaderAnnotation.Tolerance);
        }

        /// <summary>Gets or sets whether the type is Block Reference.</summary>
        public bool IsBlockReference
        {
            get => _settings.Annotation == CadQLeaderAnnotation.BlockReference;
            set => SetAnnotation(value, CadQLeaderAnnotation.BlockReference);
        }

        /// <summary>Gets or sets whether the type is None.</summary>
        public bool IsNoAnnotation
        {
            get => _settings.Annotation == CadQLeaderAnnotation.None;
            set => SetAnnotation(value, CadQLeaderAnnotation.None);
        }

        /// <summary>Gets or sets "Prompt for width".</summary>
        public bool PromptForWidth
        {
            get => _settings.PromptForWidth;
            set => SetSetting(_settings.PromptForWidth, value, v => _settings.PromptForWidth = v);
        }

        /// <summary>Gets or sets "Always left justify".</summary>
        public bool AlwaysLeftJustify
        {
            get => _settings.AlwaysLeftJustify;
            set => SetSetting(_settings.AlwaysLeftJustify, value, v => _settings.AlwaysLeftJustify = v);
        }

        /// <summary>Gets or sets "Frame text".</summary>
        public bool FrameText
        {
            get => _settings.FrameText;
            set => SetSetting(_settings.FrameText, value, v => _settings.FrameText = v);
        }

        /// <summary>Gets or sets whether Annotation Reuse is None.</summary>
        public bool IsReuseNone
        {
            get => _settings.Reuse == CadQLeaderReuse.None;
            set => SetReuse(value, CadQLeaderReuse.None);
        }

        /// <summary>Gets or sets whether Annotation Reuse is Reuse Next.</summary>
        public bool IsReuseNext
        {
            get => _settings.Reuse == CadQLeaderReuse.ReuseNext;
            set => SetReuse(value, CadQLeaderReuse.ReuseNext);
        }

        /// <summary>Gets or sets whether Annotation Reuse is Reuse Current.</summary>
        public bool IsReuseCurrent
        {
            get => _settings.Reuse == CadQLeaderReuse.ReuseCurrent;
            set => SetReuse(value, CadQLeaderReuse.ReuseCurrent);
        }

        /// <summary>Gets or sets whether the leader is a spline; false is Straight.</summary>
        public bool IsSpline
        {
            get => _settings.Spline;
            set
            {
                if (_settings.Spline != value)
                {
                    _settings.Spline = value;
                    Raise(nameof(IsSpline));
                    Raise(nameof(IsStraight));
                }
            }
        }

        /// <summary>Gets or sets whether the leader is straight, the other choice of <see cref="IsSpline"/>.</summary>
        public bool IsStraight
        {
            get => !_settings.Spline;
            set => IsSpline = !value;
        }

        /// <summary>
        /// Gets the names of the arrowhead list, in the order of AutoCAD's: Closed filled ... None, a user arrow the drawing
        /// or the dialog has chosen, then <see cref="UserArrowEntry"/>.
        /// </summary>
        public IReadOnlyList<string> Arrows => _choices.Select(c => c.Name).ToList();

        /// <summary>Gets the entries of the arrowhead list with their pictures, as <see cref="Arrows"/> names them.</summary>
        public IReadOnlyList<LeaderArrowChoiceViewModel> ArrowChoices => _choices;

        /// <summary>
        /// Gets or sets the arrowhead chosen, DIMLDRBLK's when the dialog opens. <see cref="UserArrowEntry"/> set asks for a
        /// block (<see cref="IsChoosingUserArrow"/>) and leaves the arrowhead as it is until one is kept.
        /// </summary>
        public string Arrow
        {
            get => _arrow;
            set
            {
                if (value == UserArrowEntry)
                {
                    AskUserArrow();
                }
                else if (value != null && value != UserArrowEntry && Arrows.Contains(value) && Set(ref _arrow, value))
                {
                    Raise(nameof(SelectedArrowChoice));
                }
            }
        }

        /// <summary>Gets or sets the entry of the list chosen, as <see cref="Arrow"/> takes its name.</summary>
        public LeaderArrowChoiceViewModel SelectedArrowChoice
        {
            get => _choices.FirstOrDefault(c => c.Name == _arrow);
            set
            {
                if (value != null)
                {
                    Arrow = value.Name;
                }
            }
        }

        /// <summary>
        /// Gets whether AutoCAD's Select Custom Arrow Block is open over the dialog, the drawing's blocks asked for a user arrow;
        /// the window shows it as a dialog of its own while this is true.
        /// </summary>
        public bool IsChoosingUserArrow
        {
            get => _choosingUserArrow;
            private set => Set(ref _choosingUserArrow, value);
        }

        /// <summary>Gets the blocks of the drawing a user arrow can be, by name.</summary>
        public IReadOnlyList<string> UserArrowBlocks => Request.Blocks;

        /// <summary>Gets whether the drawing has a block to use as an arrow: with none, the list and its button are greyed.</summary>
        public bool HasUserArrowBlocks => Request.Blocks.Count > 0;

        /// <summary>Gets or sets the name of the block chosen or typed for the user arrow.</summary>
        public string UserArrowBlock
        {
            get => _userArrowBlock;
            set
            {
                if (Set(ref _userArrowBlock, value ?? string.Empty))
                {
                    Raise(nameof(CanKeepUserArrow));
                }
            }
        }

        /// <summary>Gets whether OK of Select Custom Arrow Block can be pressed: not while its box is empty.</summary>
        public bool CanKeepUserArrow => (_userArrowBlock ?? string.Empty).Trim().Length > 0;

        /// <summary>
        /// Gets the line under the box of Select Custom Arrow Block: why OK did not take the name typed, or that the drawing
        /// has no block to choose; empty otherwise.
        /// </summary>
        public string UserArrowMessage
        {
            get => _userArrowMessage;
            private set => Set(ref _userArrowMessage, value ?? string.Empty);
        }

        /// <summary>Gets whether the name typed was not found: the box's border and the line under it are then orange.</summary>
        public bool IsUserArrowNotFound
        {
            get => _userArrowNotFound;
            private set => Set(ref _userArrowNotFound, value);
        }

        /// <summary>
        /// OK of Select Custom Arrow Block: the block named, letters of either case the same, made the arrowhead, added to the
        /// list before <see cref="UserArrowEntry"/> where it is not in it, and kept as the settings' user arrow (group 3).
        /// </summary>
        /// <returns>Whether it was kept, the dialog closed: false where the drawing has no block of the name, said in
        /// <see cref="UserArrowMessage"/> ("The drawing has no block TAGX."), the dialog left open.</returns>
        public bool KeepUserArrow()
        {
            string typed = (_userArrowBlock ?? string.Empty).Trim();
            string name = Request.Blocks.FirstOrDefault(b =>
                string.Equals(b, typed, StringComparison.OrdinalIgnoreCase)
            );

            if (name == null)
            {
                UserArrowMessage = HasUserArrowBlocks ? "The drawing has no block " + typed + "." : NoBlocks;
                IsUserArrowNotFound = HasUserArrowBlocks;
                return false;
            }

            if (!_choices.Any(c => c.Name == name))
            {
                _choices.Insert(_choices.Count - 1, new LeaderArrowChoiceViewModel(name, PictureOf));
                Raise(nameof(Arrows));
            }

            _settings.UserArrow = name;
            _arrow = name;
            UserArrowMessage = string.Empty;
            IsUserArrowNotFound = false;
            IsChoosingUserArrow = false;
            Raise(nameof(Arrow));
            Raise(nameof(SelectedArrowChoice));
            return true;
        }

        /// <summary>Cancel, Esc or the close button of Select Custom Arrow Block: the dialog closed, the arrowhead left as it was.</summary>
        public void DropUserArrow()
        {
            UserArrowMessage = string.Empty;
            IsUserArrowNotFound = false;
            IsChoosingUserArrow = false;
            Raise(nameof(SelectedArrowChoice));
        }

        /// <summary>Gets or sets "No Limit": points are asked until Enter, Maximum greyed.</summary>
        public bool NoLimit
        {
            get => _settings.NoLimit;
            set
            {
                if (_settings.NoLimit != value)
                {
                    _settings.NoLimit = value;
                    Raise(nameof(NoLimit));
                    Raise(nameof(IsMaximumEnabled));
                }
            }
        }

        /// <summary>Gets whether Maximum can be typed: not with No Limit.</summary>
        public bool IsMaximumEnabled => !_settings.NoLimit;

        /// <summary>Gets or sets the Maximum number of points, as typed: a whole number from 2 up.</summary>
        public string MaxPoints
        {
            get => _maxPoints;
            set => Set(ref _maxPoints, value ?? string.Empty);
        }

        /// <summary>Gets the angles a segment can be held to, as AutoCAD's lists give them: Any angle, Horizontal, 90°, ... 15°.</summary>
        public IReadOnlyList<string> AngleChoices => AngleNames;

        /// <summary>Gets or sets the angle the first segment is held to, by its name in <see cref="AngleChoices"/>.</summary>
        public string FirstSegment
        {
            get => AngleNames[(int)_settings.FirstSegment];
            set => SetAngle(value, true);
        }

        /// <summary>Gets or sets the angle the second segment is held to, by its name in <see cref="AngleChoices"/>.</summary>
        public string SecondSegment
        {
            get => AngleNames[(int)_settings.SecondSegment];
            set => SetAngle(value, false);
        }

        /// <summary>Gets the five rows of the Attachment tab, top of top line first.</summary>
        public IReadOnlyList<LeaderAttachmentRowViewModel> AttachmentRows { get; }

        /// <summary>Gets or sets where the leader meets text on its left.</summary>
        public CadQLeaderAttachment LeftSide
        {
            get => _settings.LeftSide;
            set
            {
                _settings.LeftSide = value;
                RefreshRows();
            }
        }

        /// <summary>Gets or sets where the leader meets text on its right.</summary>
        public CadQLeaderAttachment RightSide
        {
            get => _settings.RightSide;
            set
            {
                _settings.RightSide = value;
                RefreshRows();
            }
        }

        /// <summary>Gets or sets "Underline bottom line", which greys the rows.</summary>
        public bool Underline
        {
            get => _settings.Underline;
            set
            {
                if (_settings.Underline != value)
                {
                    _settings.Underline = value;
                    Raise(nameof(Underline));
                    Raise(nameof(AreRowsEnabled));
                }
            }
        }

        /// <summary>Gets whether the round buttons of the Attachment tab can be picked: not with Underline bottom line.</summary>
        public bool AreRowsEnabled => !_settings.Underline;

        /// <summary>Gets a line at the foot: why OK did not take the settings; empty when it did.</summary>
        public string Notice
        {
            get => _notice;
            private set => Set(ref _notice, value ?? string.Empty);
        }

        /// <summary>One more point: Maximum up by one.</summary>
        public void MorePoints() => StepPoints(1);

        /// <summary>One point fewer: Maximum down by one, two at least.</summary>
        public void FewerPoints() => StepPoints(-1);

        /// <summary>OK: the settings and the arrowhead written into the request, the tab shown kept for next time.</summary>
        /// <returns>Whether they were taken: false where Maximum is not a whole number from 2 up, said in <see cref="Notice"/>.</returns>
        public bool Ok()
        {
            if (!TryPoints(_maxPoints, out int points))
            {
                Notice = "The maximum number of points must be a whole number, 2 at least.";
                return false;
            }

            _settings.MaxPoints = points;
            _settings.Tab = Tabs.ToList().IndexOf(_tab);
            CadQLeaderSettings target = Request.Settings;
            target.Annotation = _settings.Annotation;
            target.Reuse = _settings.Reuse;
            target.ReusedHandle = _settings.Reuse == CadQLeaderReuse.ReuseCurrent ? _settings.ReusedHandle : null;
            target.PromptForWidth = _settings.PromptForWidth;
            target.AlwaysLeftJustify = _settings.AlwaysLeftJustify;
            target.FrameText = _settings.FrameText;
            target.Spline = _settings.Spline;
            target.NoLimit = _settings.NoLimit;
            target.MaxPoints = _settings.MaxPoints;
            target.FirstSegment = _settings.FirstSegment;
            target.SecondSegment = _settings.SecondSegment;
            target.LeftSide = _settings.LeftSide;
            target.RightSide = _settings.RightSide;
            target.Underline = _settings.Underline;
            target.Tab = _settings.Tab;
            target.UserArrow = _settings.UserArrow;
            Request.Arrow = _arrow;
            Request.Action = CadDialogAction.Ok;
            Notice = string.Empty;
            return true;
        }

        /// <summary>Cancel: nothing written.</summary>
        public void Cancel() => Request.Action = CadDialogAction.Cancel;

        private void SetAnnotation(bool on, CadQLeaderAnnotation type)
        {
            if (!on || _settings.Annotation == type)
            {
                return;
            }

            _settings.Annotation = type;
            Raise(nameof(Annotation));
            Raise(nameof(IsMText));
            Raise(nameof(IsCopyObject));
            Raise(nameof(IsTolerance));
            Raise(nameof(IsBlockReference));
            Raise(nameof(IsNoAnnotation));
        }

        private void SetReuse(bool on, CadQLeaderReuse reuse)
        {
            if (!on || _settings.Reuse == reuse)
            {
                return;
            }

            _settings.Reuse = reuse;
            Raise(nameof(IsReuseNone));
            Raise(nameof(IsReuseNext));
            Raise(nameof(IsReuseCurrent));
        }

        private void SetSetting(
            bool was,
            bool value,
            Action<bool> set,
            [System.Runtime.CompilerServices.CallerMemberName] string name = null
        )
        {
            if (was != value)
            {
                set(value);
                Raise(name);
            }
        }

        private void SetAngle(string name, bool first)
        {
            int index = Array.IndexOf(AngleNames, name);

            if (index < 0)
            {
                return;
            }

            if (first)
            {
                _settings.FirstSegment = (CadQLeaderAngle)index;
                Raise(nameof(FirstSegment));
            }
            else
            {
                _settings.SecondSegment = (CadQLeaderAngle)index;
                Raise(nameof(SecondSegment));
            }
        }

        /// <summary>
        /// "User Arrow...": Select Custom Arrow Block asked, the block in use as the arrow chosen, else the user arrow kept, else
        /// the first block; with no block in the drawing, its box empty and a line saying so.
        /// </summary>
        private void AskUserArrow()
        {
            UserArrowBlock =
                Request.Blocks.Count == 0 ? string.Empty
                : Request.Blocks.Contains(_arrow) ? _arrow
                : Request.Blocks.Contains(_settings.UserArrow) ? _settings.UserArrow
                : Request.Blocks[0];
            UserArrowMessage = Request.Blocks.Count == 0 ? NoBlocks : string.Empty;
            IsUserArrowNotFound = false;
            IsChoosingUserArrow = true;
            Raise(nameof(SelectedArrowChoice));
        }

        /// <summary>The picture of an arrowhead of the list, drawn as a leader draws it; a user arrow from a copy of the drawing, made once.</summary>
        private BitmapSource PictureOf(string name) =>
            CadStylePreview.ArrowheadEntry(
                name,
                () => _copy = _copy ?? DxfDatabase.Parse(Request.Document.Database.ToText()),
                "LeaderSettings"
            );

        private void StepPoints(int step)
        {
            int points = TryPoints(_maxPoints, out int read) ? read : _settings.MaxPoints;
            MaxPoints = Math.Max(2, Math.Min(999, points + step)).ToString(CultureInfo.InvariantCulture);
        }

        private void RefreshRows()
        {
            foreach (LeaderAttachmentRowViewModel row in AttachmentRows)
            {
                row.Refresh();
            }
        }

        private static bool TryPoints(string text, out int points) =>
            int.TryParse((text ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out points)
            && points >= 2
            && points <= 999;
    }
}
