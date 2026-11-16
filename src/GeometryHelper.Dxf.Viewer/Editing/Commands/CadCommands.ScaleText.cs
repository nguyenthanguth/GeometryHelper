using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    internal static partial class CadCommands
    {
        /// <summary>A text SCALETEXT scales: its handle, the point it is scaled about and the factor.</summary>
        private sealed class TextScaling
        {
            public string Handle { get; set; }

            public GeoPoint3 Base { get; set; }

            public double Factor { get; set; }
        }

        /// <summary>
        /// SCALETEXT: "Select objects:", "Enter a base point option for scaling [Existing/Left/Center/Middle/Right/TL/TC/TR/ML/MC/MR/BL/BC/BR]
        /// &lt;Existing&gt;:", then "Specify new model height or [Paper height/Match object/Scale factor] &lt;2.5000&gt;:". Each
        /// text is scaled about its own base point to the height asked, to the height of the text picked by Match object, or
        /// by the factor of Scale factor (with Reference); Paper height asks the height on the sheet of an annotative text and
        /// leaves the others alone, as "n non-annotative objects ignored". The base point and the height are kept for the
        /// next run, as AutoCAD keeps them. Ends with "n objects changed", all of it one undo step.
        /// </summary>
        private static IEnumerable<CadPrompt> ScaleText(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection();

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            List<string> texts = TextsOf(c, c.Input.Handles);

            if (texts.Count == 0)
            {
                yield break;
            }

            yield return CadPrompt.ForKeyword(
                "Enter a base point option for scaling",
                ScaleTextBases,
                ScaleTextBases[(int)c.Remembered("scaletext.base", 0.0)]
            );

            int chosen = Array.IndexOf(ScaleTextBases, c.Input.Text);

            if (chosen < 0)
            {
                yield break;
            }

            c.Remember("scaletext.base", chosen);
            string option = ScaleTextBases[chosen];

            // How the new size is given: a model height (1), a paper height (2), the height of a text picked (3) or a factor (4).
            int how;
            double value;

            while (true)
            {
                yield return CadPrompt.ForDistance(
                    "Specify new model height",
                    null,
                    new[] { "Paper height", "Match object", "Scale factor" },
                    Length(c.Remembered("scaletext.height", TextSize(c)))
                );

                if (c.Input.Is("Paper height"))
                {
                    yield return CadPrompt.ForDistance(
                        "Specify new paper height",
                        null,
                        null,
                        Length(c.Remembered("scaletext.paper", TextSize(c)))
                    );

                    how = 2;
                    value =
                        c.Input.Kind == CadInputKind.Number
                            ? c.Input.Number
                            : c.Remembered("scaletext.paper", TextSize(c));
                    c.Remember("scaletext.paper", value);
                }
                else if (c.Input.Is("Match object"))
                {
                    yield return CadPrompt.ForEntity("Select a text object with the desired height");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    DxfEntity picked = c.Document.FindEntity(c.Input.Handles[0]);

                    if (!IsText(picked))
                    {
                        c.Write("Selected object is not a text object.");
                        continue;
                    }

                    how = 3;
                    value = picked is DxfMText words ? words.TextHeight : ((DxfDBText)picked).Height;
                    c.Remember("scaletext.height", value);
                }
                else if (c.Input.Is("Scale factor"))
                {
                    foreach (CadPrompt prompt in AskTextFactor(c))
                    {
                        yield return prompt;
                    }

                    value = c.Remembered("scaletext.factor", 1.0);
                    how = 4;
                }
                else
                {
                    how = 1;
                    value =
                        c.Input.Kind == CadInputKind.Number
                            ? c.Input.Number
                            : c.Remembered("scaletext.height", TextSize(c));
                    c.Remember("scaletext.height", value);
                }

                if (value > 0.0)
                {
                    break;
                }

                c.Write("Requires numeric distance, two points, or option keyword.");
            }

            DxfAnnotationScale scale = null;
            var jobs = new List<TextScaling>();
            int ignored = 0;

            foreach (string handle in texts)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                double height = entity is DxfMText mtext ? mtext.TextHeight : ((DxfDBText)entity).Height;

                if (!(height > 0.0))
                {
                    continue;
                }

                if (how == 2)
                {
                    // The drawing's list of scales is looked at only when an annotative text asks for it.
                    scale = scale
                        ?? (
                            entity.Annotative == DxfAnnotativeStates.True
                                ? CurrentScale(c.Database, c.Space.IsModel)
                                : null
                        );

                    if (scale == null || entity.Annotative != DxfAnnotativeStates.True)
                    {
                        ignored++;
                        continue;
                    }
                }

                GeoPoint3? held = ScaleBase(c, entity, option);

                if (!held.HasValue)
                {
                    continue;
                }

                jobs.Add(
                    new TextScaling
                    {
                        Handle = handle,
                        Base = held.Value,
                        Factor =
                            how == 4 ? value
                            : how == 2 ? value / scale.Scale / height
                            : value / height,
                    }
                );
            }

            if (ignored > 0)
            {
                c.Write(ignored.ToString(CultureInfo.InvariantCulture) + " non-annotative objects ignored");
            }

            if (jobs.Count == 0)
            {
                yield break;
            }

            var planned = jobs.Select(j => new CadEditRecord(
                    CadEditRecordKind.Transformed,
                    j.Handle,
                    "Scale",
                    null,
                    Values(j.Base.X, j.Base.Y, j.Factor)
                ))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (TextScaling job in jobs)
                {
                    if (!s.IsHandled(job.Handle))
                    {
                        s.Document.FindEntity(job.Handle).TransformBy(GeoTransform3.Scaling(job.Base, job.Factor));
                    }
                }
            });

            c.Write(
                result.Succeeded
                    ? jobs.Count.ToString(CultureInfo.InvariantCulture) + " objects changed"
                    : result.Message
            );
        }

        /// <summary>
        /// "Specify scale factor or [Reference]:" with the factor kept as the next default, a reference length and a new
        /// length giving it as their ratio; the factor is left in the session's memory (scaletext.factor), nought where it
        /// was refused.
        /// </summary>
        private static IEnumerable<CadPrompt> AskTextFactor(CadCommandContext c)
        {
            yield return CadPrompt.ForDistance(
                "Specify scale factor",
                null,
                new[] { "Reference" },
                Length(c.Remembered("scaletext.factor", 1.0))
            );

            if (c.Input.Is("Reference"))
            {
                yield return CadPrompt.ForDistance("Specify the reference length", null, null, Length(1.0));
                double reference = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                yield return CadPrompt.ForDistance("Specify new length", null, null, Length(1.0));
                double length = c.Input.Kind == CadInputKind.Number ? c.Input.Number : 1.0;
                c.Remember("scaletext.factor", reference > c.Session.Tolerance.EqualPoint ? length / reference : 0.0);
                yield break;
            }

            c.Remember(
                "scaletext.factor",
                c.Input.Kind == CadInputKind.Number ? c.Input.Number : c.Remembered("scaletext.factor", 1.0)
            );
        }

        /// <summary>
        /// The point a text is scaled about: where it is held for Existing (its first point, its second where it is
        /// justified, an MTEXT's attachment point), else the point of its box the option names; null for a text drawn nowhere.
        /// </summary>
        private static GeoPoint3? ScaleBase(CadCommandContext c, DxfEntity entity, string option)
        {
            if (option == "Existing")
            {
                if (entity is DxfMText mtext)
                {
                    return mtext.Location;
                }

                var text = (DxfDBText)entity;
                DxfTextHorizontalMode mode = text.HorizontalMode;
                return
                    text.IsDefaultAlignment
                    || (
                        text.VerticalMode == DxfTextVerticalMode.TextBase
                        && (mode == DxfTextHorizontalMode.TextAlign || mode == DxfTextHorizontalMode.TextFit)
                    )
                    ? text.Position
                    : text.AlignmentPoint;
            }

            TextFrame frame = FrameOf(c, entity);

            if (frame == null)
            {
                return null;
            }

            TextHold(option, out int across, out int level);
            GeoPoint2 at = frame.At(across, level);
            double z = entity is DxfMText words ? words.Location.Z : ((DxfDBText)entity).Position.Z;
            return new GeoPoint3(at.X, at.Y, z);
        }
    }
}
