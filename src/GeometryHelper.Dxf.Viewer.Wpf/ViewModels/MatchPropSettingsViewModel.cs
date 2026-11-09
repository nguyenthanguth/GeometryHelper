using System.Collections.Generic;
using System.Linq;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>One check box of the Property Settings dialog: its words, whether it is checked, and whether it can be.</summary>
    public sealed class MatchPropOptionViewModel : ObservableObject
    {
        private bool _isChecked;

        internal MatchPropOptionViewModel(string name, CadMatchProperties property, bool isChecked)
        {
            Name = name;
            Property = property;
            _isChecked = isChecked && property != CadMatchProperties.None;
        }

        /// <summary>Gets the words beside the box: "Linetype Scale".</summary>
        public string Name { get; }

        /// <summary>Gets the property it stands for; <see cref="CadMatchProperties.None"/> for one greyed, which MATCHPROP does not copy.</summary>
        public CadMatchProperties Property { get; }

        /// <summary>Gets whether it can be checked: false for Plot style, Material, Shadow display and Center object.</summary>
        public bool IsEnabled => Property != CadMatchProperties.None;

        /// <summary>Gets or sets whether it is checked; a greyed one stays clear.</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set => Set(ref _isChecked, value && IsEnabled);
        }
    }

    /// <summary>
    /// The Property Settings dialog of MATCHPROP, as AutoCAD 2021's: the Basic Properties (Color, Layer, Linetype, Linetype
    /// Scale, Lineweight, Transparency, Thickness, Plot Style) and the Special Properties (Dimension, Text, Hatch, Polyline,
    /// Viewport, Table, Material, Shadow display, Multileader, Center object), each checked as the command's settings have it.
    /// Plot style, Material, Shadow display and Center object are greyed: the library has no setter of an object's plot style,
    /// and the other three are of 3D and of centre marks, which are not built. OK gives the settings checked
    /// (<see cref="Result"/>).
    /// </summary>
    public sealed class MatchPropSettingsViewModel
    {
        /// <summary>Makes the dialog over the settings MATCHPROP has.</summary>
        /// <param name="settings">The settings: a box is checked where its property is among them.</param>
        public MatchPropSettingsViewModel(CadMatchProperties settings)
        {
            MatchPropOptionViewModel Box(string name, CadMatchProperties property) => new MatchPropOptionViewModel(name, property, (settings & property) != 0);

            Basic = new[]
            {
                Box("Color", CadMatchProperties.Color),
                Box("Layer", CadMatchProperties.Layer),
                Box("Linetype", CadMatchProperties.Linetype),
                Box("Linetype Scale", CadMatchProperties.LinetypeScale),
                Box("Lineweight", CadMatchProperties.Lineweight),
                Box("Transparency", CadMatchProperties.Transparency),
                Box("Thickness", CadMatchProperties.Thickness),
                Box("Plot Style", CadMatchProperties.None),
            };

            Special = new[]
            {
                Box("Dimension", CadMatchProperties.Dimension),
                Box("Text", CadMatchProperties.Text),
                Box("Hatch", CadMatchProperties.Hatch),
                Box("Polyline", CadMatchProperties.Polyline),
                Box("Viewport", CadMatchProperties.Viewport),
                Box("Table", CadMatchProperties.Table),
                Box("Material", CadMatchProperties.None),
                Box("Shadow display", CadMatchProperties.None),
                Box("Multileader", CadMatchProperties.Multileader),
                Box("Center object", CadMatchProperties.None),
            };
        }

        /// <summary>Gets the boxes of Basic Properties, in AutoCAD's order.</summary>
        public IReadOnlyList<MatchPropOptionViewModel> Basic { get; }

        /// <summary>Gets the boxes of Special Properties, in AutoCAD's order.</summary>
        public IReadOnlyList<MatchPropOptionViewModel> Special { get; }

        /// <summary>Gets the settings the boxes checked give: Layer alone where only Layer is checked.</summary>
        public CadMatchProperties Result
        {
            get
            {
                CadMatchProperties result = CadMatchProperties.None;

                foreach (MatchPropOptionViewModel box in Basic.Concat(Special))
                {
                    if (box.IsChecked)
                    {
                        result |= box.Property;
                    }
                }

                return result;
            }
        }
    }
}
