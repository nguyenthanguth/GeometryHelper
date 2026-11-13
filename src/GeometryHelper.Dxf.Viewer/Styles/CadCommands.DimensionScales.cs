using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// Annotative dimensions: a dimension drawn by an annotative style, or made annotative from its Properties, holds a picture
    /// block for each scale it is shown at (a context of its own, as AutoCAD 2021 writes it), laid out again at that scale by
    /// the builders that draw a dimension afresh, and drawn at the annotation scale of the space.
    /// </summary>
    internal static partial class CadCommands
    {
        // The variables AutoCAD 2021 gives a dimension made annotative from its Properties, in the order it writes them: each
        // size, which is paper units under an annotative dimension, set to the size it was drawn at over the scale.
        private static readonly string[] AnnotativeSizes =
        {
            "Dimgap",
            "Dimcen",
            "Dimexo",
            "Dimexe",
            "Dimasz",
            "Dimtxt",
        };

        /// <summary>
        /// The DIMSCALE a dimension is drawn by under an annotative current dimension style: one over the annotation scale of
        /// the space (50 at 1:50 in model space, 1 in a layout), whatever the header's DIMSCALE says, as AutoCAD 2021 does;
        /// null where the current style is not annotative or the drawing has no such scale.
        /// </summary>
        private static double? AnnotationDimscale(CadCommandContext c)
        {
            DxfDimStyleTableRecord style = NamedDimStyle(c);

            if (style == null || style.Annotative != DxfAnnotativeStates.True)
            {
                return null;
            }

            DxfAnnotationScale scale = CurrentScale(c.Database, c.Space.IsModel);
            double ratio = scale == null ? 0.0 : 1.0 / scale.Scale;
            return ratio > 0.0 && !double.IsInfinity(ratio) ? ratio : (double?)null;
        }

        /// <summary>
        /// Makes a dimension just drawn by an annotative style annotative at the scale it was drawn at, as AutoCAD 2021 does for
        /// DIMLINEAR, DIMALIGNED, DIMRADIUS, DIMDIAMETER, DIMANGULAR and DIMORDINATE: its extended data and its default context,
        /// whose block is the picture drawn. A dimension of another kind keeps the picture and is not annotative here.
        /// </summary>
        private static void MakeAnnotativeAsStyled(
            CadCommandContext c,
            DxfDimension dimension,
            DxfDimStyleTableRecord style
        )
        {
            if (style == null || style.Annotative != DxfAnnotativeStates.True || !dimension.CanHoldDimensionContexts())
            {
                return;
            }

            DxfAnnotationScale scale = CurrentScale(c.Database, c.Space.IsModel);

            if (scale != null)
            {
                dimension.MakeDimensionAnnotative(scale);
            }
        }

        /// <summary>
        /// Makes a dimension annotative at the scale of its space from its Properties, as AutoCAD 2021 does (probe GHAN4: a
        /// DIMLINEAR of the style Standard, 0.18 high, at 1:50): its DIMSCALE written as an override of 0 and each size (the gap
        /// 147, the centre mark 141, the extension offset 42 and extension 44, the arrowhead 41 and the text 140) as an override
        /// of the size it was drawn at over the scale (0.0036 for 0.18 at 1:50), so that its picture is as it was; its default
        /// context is the scale, holding its picture.
        /// </summary>
        internal static string MakeDimensionAnnotative(DxfDatabase database, DxfDimension dimension, bool isModel)
        {
            if (!dimension.CanHoldDimensionContexts())
            {
                return "A "
                    + CadDocument.KindName(dimension)
                    + " is not made annotative here: its words are moved or turned, or it is of a kind the presenter has no annotative picture of yet.";
            }

            DxfAnnotationScale scale = CurrentScale(database, isModel);

            if (scale == null)
            {
                return "The drawing has no annotation scale to make it annotative at.";
            }

            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            double drawn = data.Dimscale > 0.0 ? data.Dimscale : 1.0;
            double ratio = 1.0 / scale.Scale;

            foreach (string name in AnnotativeSizes)
            {
                DxfDimVariable variable = DxfDimVariable.Named(name);
                DxfDimStyleTableRecord one = dimension.GetDimstyleData();
                one.Keep(variable, (double)data.Get(variable) * drawn / ratio);
                dimension.SetDimstyleData(one);
            }

            DxfDimStyleTableRecord zero = dimension.GetDimstyleData();
            zero.Keep(DxfDimVariable.Named("Dimscale"), 0.0);
            dimension.SetDimstyleData(zero);
            dimension.MakeDimensionAnnotative(scale);
            return null;
        }

        /// <summary>
        /// Shows an annotative dimension at another scale, as AutoCAD 2021's OBJECTSCALE does: a picture block of its own laid
        /// out at the scale by the builders that draw it afresh (the arrowheads, words, gaps and extension lines of that
        /// scale, the words at their default place there), and a context that names it and the middle of its words. A
        /// dimension of the scale already is left.
        /// </summary>
        internal static void AddDimensionScale(DxfDatabase database, DxfDimension dimension, DxfAnnotationScale scale)
        {
            if (dimension.HasContext(scale))
            {
                return;
            }

            if (!dimension.CanHoldDimensionContexts())
            {
                throw new NotSupportedException(
                    "A "
                        + CadDocument.KindName(dimension)
                        + " is not shown at a second scale here: its words are moved or turned, or it is of a kind the presenter lays out at one scale only."
                );
            }

            List<DxfEntity> picture = PictureAtScale(dimension, scale);

            if (picture == null)
            {
                throw new NotSupportedException(
                    "A " + CadDocument.KindName(dimension) + " is not laid out at a second scale here."
                );
            }

            var block = new DxfBlockTableRecord { Name = "*D" };
            database.BlockTable.Add(block);
            Fill(block, picture);
            dimension.AddDimensionContext(scale, block, RecordedPoint(picture) ?? dimension.TextPosition);
        }

        /// <summary>
        /// Draws every picture of an annotative dimension anew, each block at the scale of its context, as <see cref="Redraw(DxfDimension)"/>
        /// draws a dimension of one: the middle of the words of each context written, and the record's words and measurement
        /// those of the default.
        /// </summary>
        private static bool RedrawAnnotative(DxfDimension dimension)
        {
            var drawn = new List<KeyValuePair<DxfDBObject.DimensionPicture, List<DxfEntity>>>();

            foreach (DxfDBObject.DimensionPicture held in dimension.DimensionPictures())
            {
                List<DxfEntity> picture = PictureAtScale(dimension, held.Scale);

                if (picture == null)
                {
                    return false;
                }

                drawn.Add(new KeyValuePair<DxfDBObject.DimensionPicture, List<DxfEntity>>(held, picture));
            }

            foreach (KeyValuePair<DxfDBObject.DimensionPicture, List<DxfEntity>> pair in drawn)
            {
                foreach (DxfEntity old in pair.Key.Block.ToList())
                {
                    old.Erase();
                }

                Fill(pair.Key.Block, pair.Value);
                GeoPoint3? words = RecordedPoint(pair.Value);

                if (words.HasValue)
                {
                    dimension.SetDimensionWords(pair.Key.Key, words.Value);
                }

                if (pair.Key.IsDefault)
                {
                    BreakAgain(dimension);
                    Recorded(dimension, pair.Value);
                }
            }

            return true;
        }

        /// <summary>The picture of a dimension at a scale of the drawing's list, its variables read as <see cref="Redraw(DxfDimension)"/> reads them, DIMSCALE one over the scale.</summary>
        private static List<DxfEntity> PictureAtScale(DxfDimension dimension, DxfAnnotationScale scale)
        {
            DxfDimStyleTableRecord data = dimension.GetDimstyleData();
            data.Dimscale = 1.0 / scale.Scale;
            return Picture(dimension, data, data.Dimtxt);
        }

        /// <summary>
        /// Gives the copy of an annotative dimension a picture of its own for each scale, as COPY gives it one for the default: the
        /// block of each context copied and named by the copy's context, the default's context naming the block the copy's record
        /// does. Without it a copy and its original share the pictures of the other scales, and a move of the one moves the other's.
        /// </summary>
        internal static void CopyContextPictures(DxfDatabase database, DxfDimension copy, DxfBlockTableRecord own)
        {
            if (copy.Annotative != DxfAnnotativeStates.True)
            {
                return;
            }

            foreach (DxfDBObject.DimensionPicture picture in copy.DimensionPictures())
            {
                if (picture.IsDefault)
                {
                    copy.SetContextBlock(picture.Key, own);
                    continue;
                }

                var extra = new DxfBlockTableRecord { Name = "*D" };
                database.BlockTable.Add(extra);

                foreach (DxfEntity part in picture.Block)
                {
                    if (!part.IsErased)
                    {
                        extra.AppendEntity(part.Clone());
                    }
                }

                copy.SetContextBlock(picture.Key, extra);
            }
        }

        /// <summary>Appends a picture's entities to a block, each on layer 0 where it names none.</summary>
        private static void Fill(DxfBlockTableRecord block, List<DxfEntity> picture)
        {
            foreach (DxfEntity entity in picture)
            {
                if (entity.Layer == null || entity.Layer.Length == 0)
                {
                    entity.Layer = "0";
                }

                block.AppendEntity(entity);
            }
        }
    }
}
