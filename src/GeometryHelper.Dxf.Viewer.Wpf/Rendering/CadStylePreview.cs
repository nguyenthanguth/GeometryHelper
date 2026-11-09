using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GeometryHelper.Dxf;
using SkiaSharp;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The previews of the style dialogs: a drawing of their own (a sample text, sample dimensions) drawn by the canvas's own
    /// renderer, in the fonts of the drawing the dialog edits, scaled to fit a picture of a size, as AutoCAD's dialogs show
    /// theirs, so that an SHX font, a slant or an arrowhead looks as the drawing will.
    /// </summary>
    public static class CadStylePreview
    {
        /// <summary>The sample text of the Text Style dialog, as AutoCAD's.</summary>
        public const string SampleText = "AaBbCcD 123";

        /// <summary>
        /// Draws the sample text of a style's settings: a drawing of its own with the style and one TEXT of it, its width,
        /// slant, backwards and upside down as the style says, as TEXT writes it.
        /// </summary>
        /// <param name="settings">The settings of the style.</param>
        /// <param name="fonts">The fonts of the drawing edited.</param>
        /// <param name="width">The width of the picture, in pixels.</param>
        /// <param name="height">The height of the picture, in pixels.</param>
        /// <returns>The picture.</returns>
        public static BitmapSource Text(CadTextStyleSettings settings, CadFonts fonts, int width, int height)
        {
            var database = new DxfDatabase("AC1027");

            try
            {
                database.TextStyleTable.Add(new DxfTextStyleTableRecord { Name = "Preview" });
                DxfTextStyleTableRecord style = database.TextStyleTable["Preview"];
                style.FileName = settings.FileName ?? string.Empty;
                style.BigFontFileName = settings.BigFontFileName ?? string.Empty;
                style.XScale = settings.WidthFactor > 0.0 ? settings.WidthFactor : 1.0;
                style.ObliquingAngle = Math.Max(-85.0, Math.Min(85.0, settings.ObliqueAngle * 180.0 / Math.PI)) * Math.PI / 180.0;
                style.Font = settings.Font;
                var text = new DxfDBText
                {
                    Position = new Geometry.GeoPoint3(0, 0, 0),
                    Height = 10.0,
                    TextString = SampleText,
                    TextStyleName = "Preview",
                    WidthFactor = style.XScale,
                    Oblique = style.ObliquingAngle,
                    IsMirroredInX = settings.Backwards,
                    IsMirroredInY = settings.UpsideDown,
                };
                database.ModelSpace.AppendEntity(text);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug("CadStylePreview: the sample text was not drawn: " + exception.Message);
            }

            // The box of a TrueType text is the canvas's to measure, not the display list's: the sample is fitted to the box its
            // letters take, about three quarters of the height each, mirrored as it is.
            double across = SampleText.Length * 10.0 * 0.75 * (settings.WidthFactor > 0.0 ? settings.WidthFactor : 1.0) + 10.0 * Math.Abs(Math.Tan(Math.Min(1.5, Math.Abs(settings.ObliqueAngle))));
            var fit = new DisplayBox(settings.Backwards ? -across : 0.0, settings.UpsideDown ? -10.0 : -3.0, settings.Backwards ? 0.0 : across, settings.UpsideDown ? 3.0 : 10.0);
            return Draw(database, fonts, width, height, fit);
        }

        /// <summary>Draws the sample dimensions of a dimension style's variables, as <see cref="CadDimStyleEditor.Samples"/> makes them.</summary>
        /// <param name="source">The drawing edited, whose text styles the samples take.</param>
        /// <param name="values">The variables by name.</param>
        /// <param name="fonts">The fonts of the drawing edited.</param>
        /// <param name="width">The width of the picture, in pixels.</param>
        /// <param name="height">The height of the picture, in pixels.</param>
        /// <returns>The picture; of the parts alone where the variables do not make a style.</returns>
        public static BitmapSource Dimensions(DxfDatabase source, System.Collections.Generic.IDictionary<string, object> values, CadFonts fonts, int width, int height)
        {
            DxfDatabase database;

            try
            {
                database = CadDimStyleEditor.Samples(source, values);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                GeometryHelperLog.Debug("CadStylePreview: the sample dimensions were not drawn: " + exception.Message);
                database = new DxfDatabase("AC1027");
            }

            return Draw(database, fonts, width, height, new DisplayBox(0.0, 0.0, 0.0, 0.0));
        }

        /// <summary>
        /// Draws the sample multileader of a multileader style's settings, as <see cref="CadMLeaderStyleEditor.Sample"/> makes
        /// it: the words "Default Text" in the style, its arrow down on the left and its landing up on the right, made as
        /// MLEADER makes one, in the font of the text style the settings name in the drawing edited.
        /// </summary>
        /// <param name="source">The drawing edited, whose text style's font the words take.</param>
        /// <param name="settings">The settings of the style.</param>
        /// <param name="fonts">The fonts of the drawing edited.</param>
        /// <param name="width">The width of the picture, in pixels.</param>
        /// <param name="height">The height of the picture, in pixels.</param>
        /// <returns>The picture; of the parts alone where the settings do not make a style.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the settings are null.</exception>
        public static BitmapSource MLeader(DxfDatabase source, CadMLeaderStyleSettings settings, CadFonts fonts, int width, int height)
        {
            return Draw(CadMLeaderStyleEditor.Sample(source, settings), fonts, width, height, new DisplayBox(0.0, 0.0, 0.0, 0.0));
        }

        /// <summary>
        /// Draws a block of a drawing as the Blocks palette shows it: an INSERT of it at the origin, copied with the block into a
        /// drawing of its own, zoomed to what it draws, white on the black ground of the canvas. The drawing given gets the INSERT
        /// copied (an entity added to its model space), so a copy of the drawing edited is given, not the drawing itself.
        /// </summary>
        /// <param name="scratch">A copy of the drawing the block is in, which the INSERT is added to.</param>
        /// <param name="name">The name of the block.</param>
        /// <param name="fonts">The fonts of the drawing.</param>
        /// <param name="width">The width of the picture, in pixels.</param>
        /// <param name="height">The height of the picture, in pixels.</param>
        /// <returns>The picture.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the drawing or the name is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the drawing has no block of the name.</exception>
        public static BitmapSource Block(DxfDatabase scratch, string name, CadFonts fonts, int width, int height)
        {
            if (scratch == null)
            {
                throw new ArgumentNullException(nameof(scratch));
            }

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (!scratch.BlockTable.Has(name))
            {
                throw new ArgumentException("The drawing has no block " + name + ".", nameof(name));
            }

            var carrier = new DxfBlockReference(new Geometry.GeoPoint3(0, 0, 0), scratch.BlockTable[name]);
            scratch.ModelSpace.AppendEntity(carrier);
            var database = new DxfDatabase("AC1027");
            scratch.WblockCloneObjects(new[] { carrier }, database.ModelSpace);
            return Draw(database, fonts, width, height, new DisplayBox(0.0, 0.0, 0.0, 0.0));
        }

        /// <summary>The picture of model space of a drawing, as the attach dialog and the External References palette preview an external reference.</summary>
        internal static BitmapSource Drawing(DxfDatabase database, CadFonts fonts, int width, int height) => Draw(database, fonts, width, height, DisplayBox.Empty);

        /// <summary>Draws model space of a drawing, zoomed to what it draws and a box given, white on the black ground of the canvas.</summary>
        private static BitmapSource Draw(DxfDatabase database, CadFonts fonts, int width, int height, DisplayBox fit)
        {
            CadDocument document = CadDocument.From(database, "preview.dxf");

            if (fonts != null)
            {
                document.Fonts = fonts;
            }

            var view = new CadView(width, height);
            DisplayBox bounds = document.GetDisplay(document.ModelSpace).Bounds;
            bounds = bounds.IsEmpty ? fit : bounds.Union(fit);

            if (!bounds.IsEmpty)
            {
                view.ZoomTo(bounds, 0.08);
            }

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            var bitmap = new WriteableBitmap(width, height, 96.0, 96.0, PixelFormats.Pbgra32, null);
            bitmap.Lock();

            try
            {
                using (SKSurface surface = SKSurface.Create(info, bitmap.BackBuffer, bitmap.BackBufferStride))
                using (var renderer = new SkiaCanvasRenderer())
                {
                    renderer.Render(surface.Canvas, document, document.ModelSpace, view, new CanvasOptions { Background = 0x000000 });
                    surface.Canvas.Flush();
                }

                bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
            }
            finally
            {
                bitmap.Unlock();
            }

            bitmap.Freeze();
            return bitmap;
        }
    }
}
