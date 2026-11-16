using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// FIELD and UPDATEFIELD, and the variables FIELDEVAL and FIELDDISPLAY, as AutoCAD 2021's. FIELD shows the Field dialog
    /// (Select object asks "Select an object:" and shows it again) and, once OK, asks "Specify start point of multiline text" and
    /// makes an MTEXT of the field there; the Insert Field of the text editor gives the same field to the words being edited.
    /// UPDATEFIELD asks "Select objects:" and evaluates the fields of the texts selected again, saying "2 field(s) found. 2
    /// field(s) updated.". FIELDEVAL is the bits that update fields on their own (1 open, 2 save, 4 plot, 8 eTransmit, 16
    /// regen; 31 at the start) and FIELDDISPLAY 1 draws a field on a grey ground that is not plotted.
    /// </summary>
    internal static partial class CadCommands
    {
        private static void AddFieldsTo(CadCommandRegistry registry)
        {
            registry.Add(new CadCommand("FIELD", Field));
            registry.Add(new CadCommand("UPDATEFIELD", UpdateField));
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "FIELDEVAL",
                    true,
                    0,
                    31,
                    s => CadFields.Eval(s.Document.Database),
                    (c, v) => CadFields.SetEval(c.Database, v)
                )
            );
            registry.AddVariable(
                CadSystemVariable.Whole(
                    "FIELDDISPLAY",
                    false,
                    0,
                    1,
                    s => s.Document.FieldDisplay ? 1 : 0,
                    (c, v) =>
                    {
                        c.Document.FieldDisplay = v != 0;
                        c.Document.Regen();
                    }
                )
            );
        }

        /// <summary>The context fields are evaluated in for a command: the drawing, now, and the session's system variables.</summary>
        internal static CadFieldContext FieldContext(CadCommandSession session, int reason) =>
            new CadFieldContext(session.Document, reason) { Variable = name => SessionVariable(session, name) };

        /// <summary>The value of a system variable as a field shows it: a text without its quotes, a real to six places, a point as x, y, z; null where there is none of the name.</summary>
        private static string SessionVariable(CadCommandSession session, string name)
        {
            CadSystemVariable variable = session.Registry.FindVariable(name);

            if (variable == null || !string.Equals(variable.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string shown = variable.GetValue(session);

            switch (variable.Kind)
            {
                case CadVariableKind.Text:
                    return shown.Length >= 2 && shown[0] == '"' ? shown.Substring(1, shown.Length - 2) : shown;
                case CadVariableKind.Real:
                    return double.TryParse(shown, NumberStyles.Float, CultureInfo.InvariantCulture, out double real)
                        ? real.ToString("F6", CultureInfo.InvariantCulture)
                        : shown;
                case CadVariableKind.Point:
                    string[] parts = shown.Split(',');
                    var points = new List<string>();

                    foreach (string part in parts)
                    {
                        points.Add(
                            double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                                ? value.ToString("F6", CultureInfo.InvariantCulture)
                                : part.Trim()
                        );
                    }

                    return string.Join(", ", points);
                default:
                    return shown == "ON" ? "1"
                        : shown == "OFF" ? "0"
                        : shown;
            }
        }

        /// <summary>
        /// Shows the Field dialog on a request until it ends with OK or Cancel, Select object asking for an object between: the
        /// words of the dialog are the field in its markers; null where it was cancelled or the window shows no dialog.
        /// </summary>
        internal static IEnumerable<CadPrompt> AskField(
            CadCommandContext c,
            CadFieldRequest request,
            Action<string> done
        )
        {
            while (true)
            {
                request.Action = CadFieldAction.Cancel;

                if (!c.Session.Request("FIELD", c.Document, request))
                {
                    c.Write("The Field dialog is shown by the window: no field is made without it.");
                    done(null);
                    yield break;
                }

                if (request.Action == CadFieldAction.SelectObject)
                {
                    yield return CadPrompt.ForEntity("Select an object");

                    if (c.Input.Kind == CadInputKind.Selection && c.Input.Handles.Count > 0)
                    {
                        request.SetObject(c.Input.Handles[0]);
                    }

                    continue;
                }

                done(request.Action == CadFieldAction.Ok && request.Code.Length > 0 ? request.Code : null);
                yield break;
            }
        }

        private static IEnumerable<CadPrompt> Field(CadCommandContext c)
        {
            var request = new CadFieldRequest(FieldContext(c.Session, CadFieldContexts.Demand));
            string code = null;

            foreach (CadPrompt prompt in AskField(c, request, text => code = text))
            {
                yield return prompt;
            }

            if (code == null)
            {
                yield break;
            }

            double height = c.Remembered("text.height", TextSize(c));
            string attach = "TL";

            while (true)
            {
                yield return CadPrompt.ForPoint(
                    "Specify start point of multiline text",
                    null,
                    new[] { "Height", "Justify" }
                );

                if (c.Input.Is("Height"))
                {
                    yield return CadPrompt.ForDistance("Specify height", null, null, Length(height));

                    if (c.Input.Kind == CadInputKind.Number && c.Input.Number > 0.0)
                    {
                        height = c.Input.Number;
                    }

                    continue;
                }

                if (c.Input.Is("Justify"))
                {
                    yield return CadPrompt.ForKeyword("Enter justification", MTextJustifications, attach);

                    if (c.Input.Kind == CadInputKind.Keyword)
                    {
                        attach = c.Input.Text;
                    }

                    continue;
                }

                break;
            }

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            GeoPoint2 at = c.Input.Point;
            CadUcs ucs = c.Session.Ucs;
            int place = Math.Max(0, Array.IndexOf(MTextJustifications, attach));
            string style = TextStyle(c);
            c.Remember("text.height", height);
            var context = FieldContext(c.Session, CadFieldContexts.Demand);
            bool held = true;

            c.Apply(null, s =>
            {
                var text = new DxfMText
                {
                    Location = P3(at),
                    TextHeight = height,
                    Width = 0.0,
                    Attachment = (DxfAttachmentPoint)(place + 1),
                    Contents = string.Empty,
                };

                if (!ucs.IsWorld)
                {
                    text.Rotation = ucs.AngleToWorld(0.0);
                }

                if (style != null)
                {
                    text.TextStyleName = style;
                }

                s.Append(c.Space, Current(c, text));
                held = CadFields.Set(text, code, context);
            });

            if (!held)
            {
                c.Write("A drawing older than AutoCAD 2004 holds no field: the words the field comes to are written.");
            }
        }

        private static IEnumerable<CadPrompt> UpdateField(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select objects");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            var handles = new List<string>();

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity != null && CadFields.Has(entity))
                {
                    handles.Add(handle);
                }
            }

            int found = 0;
            int updated = 0;
            var planned = new List<CadEditRecord>();

            foreach (string handle in handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);
                planned.Add(
                    new CadEditRecord(
                        CadEditRecordKind.PropertyChanged,
                        handle,
                        "Contents",
                        CadFields.Words(entity),
                        null
                    )
                );
            }

            if (handles.Count > 0)
            {
                CadFieldContext context = FieldContext(c.Session, CadFieldContexts.Demand);
                CadEditResult result = c.Apply(planned, s =>
                {
                    foreach (string handle in handles)
                    {
                        if (s.IsHandled(handle))
                        {
                            continue;
                        }

                        CadFields.Update(s.Document.FindEntity(handle), context, out int count, out int done);
                        found += count;
                        updated += done;
                    }
                });

                if (!result.Succeeded)
                {
                    yield break;
                }
            }

            c.Write(
                found.ToString(CultureInfo.InvariantCulture)
                    + " field(s) found. "
                    + updated.ToString(CultureInfo.InvariantCulture)
                    + " field(s) updated."
            );
        }
    }
}
