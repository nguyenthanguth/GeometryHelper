using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The associative array selected on the canvas and the contextual Array tab over it, as AutoCAD 2021's: the Type, the
    /// Columns (the Items of a polar or path array) with their count, the distance between and the total, the Rows and the
    /// Levels the same, and Properties (Base Point, Rotate Items or Align Items, Direction, Method, Reset Array). Each value
    /// written is one undo step named ARRAYEDIT, the items made again, as the option of the command does; Edit Source and
    /// Replace Item are greyed (the source objects are replaced with ARRAYEDIT's REPlace option).
    /// </summary>
    public sealed class ArrayTabViewModel : ObservableObject
    {
        private readonly MainViewModel _model;
        private CadAssocArray _array;

        /// <summary>Makes the tab over the array of an INSERT.</summary>
        /// <param name="model">The window's view model.</param>
        /// <param name="handle">The INSERT's handle.</param>
        public ArrayTabViewModel(MainViewModel model, string handle)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Handle = handle ?? throw new ArgumentNullException(nameof(handle));
            ResetCommand = new DelegateCommand(_ => Apply(a => { }));
            BasePointCommand = new DelegateCommand(_ => _model.StartCommand("ARRAYEDIT"));
            Refresh();
        }

        /// <summary>Gets the handle of the array's INSERT.</summary>
        public string Handle { get; }

        /// <summary>Gets the command of Reset Array: the items made again from the parameters.</summary>
        public ICommand ResetCommand { get; }

        /// <summary>Gets the command of Base Point: ARRAYEDIT, which asks for the array and its options.</summary>
        public ICommand BasePointCommand { get; }

        /// <summary>Gets the kind of the array as the tab names it: Rectangular, Polar or Path.</summary>
        public string TypeName => _array == null ? string.Empty : _array.Kind.ToString();

        /// <summary>Gets whether the array is rectangular.</summary>
        public bool IsRectangular => _array != null && _array.Kind == CadArrayKind.Rectangular;

        /// <summary>Gets whether the array is polar.</summary>
        public bool IsPolar => _array != null && _array.Kind == CadArrayKind.Polar;

        /// <summary>Gets whether the array runs along a path.</summary>
        public bool IsPath => _array != null && _array.Kind == CadArrayKind.Path;

        /// <summary>Gets the title of the first group: Columns for a rectangular array, Items for the others.</summary>
        public string ItemsTitle => IsRectangular ? "Columns" : "Items";

        /// <summary>Gets or sets the columns of a rectangular array, the items of the others.</summary>
        public int Items
        {
            get =>
                _array == null ? 0
                : IsRectangular ? _array.Columns
                : _array.Items;
            set =>
                Apply(a =>
                {
                    int count = Math.Max(1, Math.Min(value, CadPathArray.MostItems));

                    if (a.Kind == CadArrayKind.Rectangular)
                    {
                        a.Columns = count;
                    }
                    else if (a.Kind == CadArrayKind.Polar)
                    {
                        double fill = a.Fill;
                        a.Items = count;
                        a.Between = Math.Abs(StepOf(count, fill));
                    }
                    else
                    {
                        a.Items = count;
                    }
                });
        }

        /// <summary>Gets or sets the distance between the columns or items; for a polar array the angle between them, in degrees.</summary>
        public double Between
        {
            get =>
                _array == null ? 0.0
                : IsRectangular ? _array.ColumnSpacing
                : IsPolar ? _array.Between * 180.0 / Math.PI
                : _array.Spacing;
            set =>
                Apply(a =>
                {
                    if (a.Kind == CadArrayKind.Rectangular)
                    {
                        a.ColumnSpacing = value;
                    }
                    else if (a.Kind == CadArrayKind.Polar)
                    {
                        a.Between = Math.Abs(value) * Math.PI / 180.0;
                        a.Clockwise = value < 0.0;
                    }
                    else if (value > 0.0)
                    {
                        a.Spacing = value;
                        a.Items = Math.Min(a.Items, a.MostAlongPath(value));
                    }
                });
        }

        /// <summary>Gets or sets the distance from the first column or item to the last; for a polar array the angle it fills, in degrees.</summary>
        public double Total
        {
            get =>
                _array == null ? 0.0
                : IsRectangular ? _array.ColumnSpacing * Math.Max(0, _array.Columns - 1)
                : IsPolar ? _array.Fill * 180.0 / Math.PI
                : _array.Spacing * Math.Max(0, _array.Items - 1);
            set =>
                Apply(a =>
                {
                    if (a.Kind == CadArrayKind.Polar)
                    {
                        double step = StepOf(a.Items, value * Math.PI / 180.0);
                        a.Between = Math.Abs(step);
                        a.Clockwise = step < 0.0;
                    }
                    else
                    {
                        int count = a.Kind == CadArrayKind.Rectangular ? a.Columns : a.Items;
                        double step = count > 1 ? value / (count - 1) : value;

                        if (a.Kind == CadArrayKind.Rectangular)
                        {
                            a.ColumnSpacing = step;
                        }
                        else if (step > 0.0)
                        {
                            a.Spacing = step;
                        }
                    }
                });
        }

        /// <summary>Gets or sets the number of rows.</summary>
        public int Rows
        {
            get => _array?.Rows ?? 0;
            set => Apply(a => a.Rows = Math.Max(1, Math.Min(value, CadPathArray.MostItems)));
        }

        /// <summary>Gets or sets the distance between the rows.</summary>
        public double RowBetween
        {
            get => _array?.RowSpacing ?? 0.0;
            set => Apply(a => a.RowSpacing = value);
        }

        /// <summary>Gets or sets the distance from the first row to the last.</summary>
        public double RowTotal
        {
            get => _array == null ? 0.0 : _array.RowSpacing * Math.Max(0, _array.Rows - 1);
            set => Apply(a => a.RowSpacing = a.Rows > 1 ? value / (a.Rows - 1) : value);
        }

        /// <summary>Gets or sets how much higher each row is than the one before.</summary>
        public double RowElevation
        {
            get => _array?.RowElevation ?? 0.0;
            set => Apply(a => a.RowElevation = value);
        }

        /// <summary>Gets or sets the number of levels.</summary>
        public int Levels
        {
            get => _array?.Levels ?? 0;
            set => Apply(a => a.Levels = Math.Max(1, Math.Min(value, CadPathArray.MostItems)));
        }

        /// <summary>Gets or sets the distance between the levels.</summary>
        public double LevelBetween
        {
            get => _array?.LevelSpacing ?? 0.0;
            set => Apply(a => a.LevelSpacing = value);
        }

        /// <summary>Gets or sets the distance from the first level to the last.</summary>
        public double LevelTotal
        {
            get => _array == null ? 0.0 : _array.LevelSpacing * Math.Max(0, _array.Levels - 1);
            set => Apply(a => a.LevelSpacing = a.Levels > 1 ? value / (a.Levels - 1) : value);
        }

        /// <summary>Gets or sets whether the items of a polar array are turned with it (Rotate Items).</summary>
        public bool RotateItems
        {
            get => _array != null && _array.Rotate;
            set => Apply(a => a.Rotate = value);
        }

        /// <summary>Gets or sets whether the items of a path array are turned to follow the path (Align Items).</summary>
        public bool AlignItems
        {
            get => _array != null && _array.Align;
            set => Apply(a => a.Align = value);
        }

        /// <summary>Gets or sets whether a polar array runs clockwise (Direction).</summary>
        public bool Clockwise
        {
            get => _array != null && _array.Clockwise;
            set => Apply(a => a.Clockwise = value);
        }

        /// <summary>Gets or sets whether a path array shares the path among its items (Divide) rather than setting them a distance apart (Measure).</summary>
        public bool Divide
        {
            get => _array != null && _array.Divide;
            set =>
                Apply(a =>
                {
                    a.Divide = value;
                    a.Items = a.MostAlongPath(a.Spacing);
                });
        }

        /// <summary>Gets whether the INSERT is still an associative array of the drawing shown.</summary>
        /// <param name="document">The drawing.</param>
        internal bool IsValid(CadDocument document) => CadArrays.TryRead(document, Handle) != null;

        /// <summary>Reads the array again from the drawing, after an edit, an undo or a command, and tells the tab to show it.</summary>
        internal void Refresh()
        {
            CadDocument document = _model.Document;
            _array = document == null ? null : CadArrays.TryRead(document, Handle);
            Raise(string.Empty);
        }

        /// <summary>The angle between the items of a polar array filling an angle: a whole turn shared by them all, else from the first to the last.</summary>
        private static double StepOf(int items, double fill)
        {
            bool whole = Math.Abs(Math.Abs(fill) - 2.0 * Math.PI) <= 1E-9;
            return whole || items < 2 ? fill / Math.Max(1, items) : fill / (items - 1);
        }

        /// <summary>Changes the parameters and writes the array again, one undo step; a refusal is said on the command line.</summary>
        private void Apply(Action<CadAssocArray> change)
        {
            CadDocument document = _model.Document;

            if (document == null || _array == null)
            {
                return;
            }

            CadEditResult result = CadArrays.Edit(document, Handle, change);

            if (!result.Succeeded)
            {
                _model.Session?.Write(result.Message);
            }

            Refresh();
        }
    }
}
