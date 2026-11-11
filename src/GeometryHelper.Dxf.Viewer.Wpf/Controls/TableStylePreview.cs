using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The preview of a table style, as the Table Style dialogs draw it on white: a title, a header and data rows of five
    /// columns, each kind of row in its text height, fill and lines; or, given a cell style, two cells of it alone.
    /// </summary>
    public sealed class TableStylePreview : FrameworkElement
    {
        /// <summary>The settings drawn.</summary>
        public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
            nameof(Settings),
            typeof(CadTableStyleSettings),
            typeof(TableStylePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        /// <summary>A number changed whenever the settings drawn change in place, so that the preview is drawn anew.</summary>
        public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(
            nameof(Version),
            typeof(int),
            typeof(TableStylePreview),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        /// <summary>The cell style drawn alone: Data, Header or Title; null for the whole table.</summary>
        public static readonly DependencyProperty CellStyleProperty = DependencyProperty.Register(
            nameof(CellStyle),
            typeof(string),
            typeof(TableStylePreview),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
        );

        private static readonly Typeface Face = new Typeface("Segoe UI");

        /// <summary>Gets or sets the settings drawn.</summary>
        public CadTableStyleSettings Settings
        {
            get => (CadTableStyleSettings)GetValue(SettingsProperty);
            set => SetValue(SettingsProperty, value);
        }

        /// <summary>Gets or sets the number changed whenever the settings change in place.</summary>
        public int Version
        {
            get => (int)GetValue(VersionProperty);
            set => SetValue(VersionProperty, value);
        }

        /// <summary>Gets or sets the cell style drawn alone; null for the whole table.</summary>
        public string CellStyle
        {
            get => (string)GetValue(CellStyleProperty);
            set => SetValue(CellStyleProperty, value);
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext dc)
        {
            double width = ActualWidth;
            double height = ActualHeight;
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            CadTableStyleSettings settings = Settings;

            if (settings == null || width < 40 || height < 30)
            {
                return;
            }

            if (CellStyle != null)
            {
                CadTableCellStyleSettings cell = settings.Cell(
                    CellStyle == "Title" ? DxfRowType.TitleRow
                    : CellStyle == "Header" ? DxfRowType.HeaderRow
                    : DxfRowType.DataRow
                );
                var box = new Rect(10, 10, width - 20, height - 20);
                DrawRow(dc, cell, box, 2, CellStyle, true, true);
                return;
            }

            // Five columns: the title, the header and three rows of data, each as tall as its text with the margins.
            DxfRowType[] kinds =
            {
                DxfRowType.TitleRow,
                DxfRowType.HeaderRow,
                DxfRowType.DataRow,
                DxfRowType.DataRow,
                DxfRowType.DataRow,
            };
            double[] tall = new double[kinds.Length];
            double total = 0.0;

            for (int r = 0; r < kinds.Length; r++)
            {
                tall[r] = settings.Cell(kinds[r]).TextHeight * 4.0 / 3.0 + 2.0 * settings.VerticalMargin;
                total += tall[r];
            }

            double scale = (height - 20) / Math.Max(total, 1E-9);
            bool up = settings.FlowDirection == DxfFlowDirection.BottomToTop;
            double y = up ? height - 10 : 10;

            for (int r = 0; r < kinds.Length; r++)
            {
                double h = tall[r] * scale;
                var box = new Rect(10, up ? y - h : y, width - 20, h);
                y += up ? -h : h;
                DxfRowType kind = kinds[r];
                string word =
                    kind == DxfRowType.TitleRow ? "Title"
                    : kind == DxfRowType.HeaderRow ? "Header"
                    : "Data";
                DrawRow(
                    dc,
                    settings.Cell(kind),
                    box,
                    kind == DxfRowType.TitleRow ? 1 : 5,
                    word,
                    r == 0 || kinds[r - 1] != kind,
                    r == kinds.Length - 1 || kinds[r + 1] != kind
                );
            }
        }

        /// <summary>A row of cells of a cell style: its fill, its lines, and its word in each cell where its alignment says.</summary>
        private static void DrawRow(
            DrawingContext dc,
            CadTableCellStyleSettings cell,
            Rect box,
            int columns,
            string word,
            bool first,
            bool last
        )
        {
            if (cell.FillColor != null)
            {
                dc.DrawRectangle(
                    new SolidColorBrush(Shown(cell.FillColor, Color.FromRgb(0xD9, 0xDD, 0xE3))),
                    null,
                    box
                );
            }

            double each = box.Width / columns;

            // Top, inside across, bottom, left, inside down, right.
            Line(dc, cell, first ? 0 : 1, box.TopLeft, box.TopRight);
            Line(dc, cell, last ? 2 : 1, box.BottomLeft, box.BottomRight);
            Line(dc, cell, 3, box.TopLeft, box.BottomLeft);
            Line(dc, cell, 5, box.TopRight, box.BottomRight);

            for (int c = 1; c < columns; c++)
            {
                Line(dc, cell, 4, new Point(box.Left + c * each, box.Top), new Point(box.Left + c * each, box.Bottom));
            }

            double size = Math.Max(6.0, Math.Min(box.Height * 0.45, 16.0));
            var brush = new SolidColorBrush(Shown(cell.TextColor, Color.FromRgb(0x1A, 0x1A, 0x1A)));
            int across = ((int)cell.Alignment - 1) % 3;
            int upDown = ((int)cell.Alignment - 1) / 3;

            for (int c = 0; c < columns; c++)
            {
                var text = new FormattedText(
                    word,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    Face,
                    size,
                    brush,
                    1.0
                );
                double left = box.Left + c * each;
                double x =
                    across == 0 ? left + 3
                    : across == 1 ? left + (each - text.Width) / 2.0
                    : left + each - text.Width - 3;
                double y =
                    upDown == 0 ? box.Top + 2
                    : upDown == 1 ? box.Top + (box.Height - text.Height) / 2.0
                    : box.Bottom - text.Height - 2;
                dc.PushClip(new RectangleGeometry(new Rect(left, box.Top, each, box.Height)));
                dc.DrawText(text, new Point(x, y));
                dc.Pop();
            }
        }

        private static void Line(DrawingContext dc, CadTableCellStyleSettings cell, int line, Point from, Point to)
        {
            if (!cell.LinesShown[line])
            {
                return;
            }

            DxfLineWeight weight = cell.LineWeights[line];
            double thickness = (int)weight > 0 ? Math.Max(1.0, (int)weight / 25.0) : 1.0;
            dc.DrawLine(
                new Pen(new SolidColorBrush(Shown(cell.LineColors[line], Color.FromRgb(0x1A, 0x1A, 0x1A))), thickness),
                from,
                to
            );
        }

        /// <summary>A colour as the preview shows it on white: by block, by layer and white drawn dark, as AutoCAD's preview does.</summary>
        private static Color Shown(DxfColor color, Color otherwise)
        {
            if (color == null || color.IsByBlock || color.IsByLayer || color.ColorIndex == 7)
            {
                return otherwise;
            }

            int rgb = color.ColorValue & 0xFFFFFF;
            return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        }
    }
}
