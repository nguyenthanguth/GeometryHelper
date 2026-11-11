using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
    /// AutoCAD 2021's Leader Settings of QLEADER over the request its command hands the window: Annotation (the annotation
    /// type, the MText options, Annotation Reuse), Leader Line &amp; Arrow (straight or spline, the arrowhead, the number of
    /// points, the angle constraints) and Attachment (where the leader meets text on either side, Underline bottom line). The
    /// Attachment tab and the MText options are greyed unless the type is MText, the ten round buttons with Underline, Maximum
    /// with No Limit; Tolerance cannot be picked, as the presenter has no Geometric Tolerance dialog. OK writes the settings
    /// and the arrowhead into the request; Cancel leaves it.
    /// </summary>
    public sealed class LeaderSettingsViewModel : ObservableObject
    {
        /// <summary>The Annotation tab.</summary>
        public const string AnnotationTab = "Annotation";

        /// <summary>The Leader Line &amp; Arrow tab.</summary>
        public const string LineTab = "Leader Line & Arrow";

        /// <summary>The Attachment tab.</summary>
        public const string AttachmentTab = "Attachment";

        private static readonly string[] AngleNames = { "Any angle", "Horizontal", "90°", "45°", "30°", "15°" };

        private readonly CadQLeaderSettings _settings;
        private string _tab;
        private string _maxPoints;
        private string _arrow;
        private string _notice = string.Empty;

        /// <summary>Makes the dialog over a request.</summary>
        /// <param name="request">The request of QLEADER's Settings.</param>
        /// <exception cref="ArgumentNullException">Thrown when the request is null.</exception>
        public LeaderSettingsViewModel(CadQLeaderRequest request)
        {
            Request = request ?? throw new ArgumentNullException(nameof(request));
            _settings = request.Settings.Clone();

            // Tolerance cannot be picked here: a drawing that has it opens on MText's options greyed as AutoCAD's would not.
            _tab = Tabs[Math.Max(0, Math.Min(2, _settings.Tab))];

            if (_tab == AttachmentTab && !IsMText)
            {
                _tab = AnnotationTab;
            }

            _maxPoints = _settings.MaxPoints.ToString(CultureInfo.InvariantCulture);
            _arrow = request.Arrow;
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

        /// <summary>Gets whether the type is Tolerance, which only a drawing can hold: the dialog cannot pick it.</summary>
        public bool IsTolerance => _settings.Annotation == CadQLeaderAnnotation.Tolerance;

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

        /// <summary>Gets the tooltip of Tolerance, greyed: why it cannot be picked.</summary>
        public string ToleranceToolTip =>
            "Tolerance needs the Geometric Tolerance dialog. " + MainViewModel.NotBuiltYet;

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

        /// <summary>Gets the arrowheads offered, in the order of AutoCAD's list.</summary>
        public IReadOnlyList<string> Arrows => Request.Arrows;

        /// <summary>Gets or sets the arrowhead chosen, DIMLDRBLK's when the dialog opens.</summary>
        public string Arrow
        {
            get => _arrow;
            set
            {
                if (value != null && Arrows.Contains(value))
                {
                    Set(ref _arrow, value);
                }
            }
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
