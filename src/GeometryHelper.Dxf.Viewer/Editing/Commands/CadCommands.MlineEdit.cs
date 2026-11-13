using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLEDIT typed, as AutoCAD 2021's console asks it ("Enter mline editing option [CC/OC/MC/CT/OT/MT/CJ/AV/DV/CS/CA/WA]:"; its
    /// window shows a dialog of the same twelve tools): the closed, open and merged crosses and tees, the corner joint, the
    /// vertex added and deleted, a cut of one element or of all, and the weld that closes the cuts between two picks.
    /// </summary>
    internal static partial class CadCommands
    {
        private static readonly string[] MlineEditOptions =
        {
            "CC",
            "OC",
            "MC",
            "CT",
            "OT",
            "MT",
            "CJ",
            "AV",
            "DV",
            "CS",
            "CA",
            "WA",
        };

        /// <summary>
        /// MLEDIT: an option, then "Select first mline:" and "Select second mline:" (the crosses, tees and corner joint) or
        /// "Select mline:" (the others, with "Select second point:" for the cuts and the weld), asked again as "Select first
        /// mline or [Undo]:" once one pair was tried. A pick that is not a multiline is "Please pick mlines only."; two that do
        /// not meet "These multilines do not intersect."; the second point of a cut on another multiline "Please pick the same
        /// mline.". Undo takes back the last edit of the command. Enter ends it.
        /// </summary>
        private static IEnumerable<CadPrompt> MlineEdit(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword("Enter mline editing option", MlineEditOptions);

            if (c.Input.Kind != CadInputKind.Keyword)
            {
                yield break;
            }

            string option = c.Input.Text;
            bool joins = Array.IndexOf(MlineEditOptions, option) < 7;
            bool cuts = option == "CS" || option == "CA" || option == "WA";
            var steps = new List<List<KeyValuePair<string, DxfMlineBuilder>>>();
            bool tried = false;

            while (true)
            {
                DxfUnknownEntity first = null;
                GeoPoint2 firstAt = default;
                bool undone = false;

                while (first == null && !undone)
                {
                    yield return CadPrompt.ForEntity(
                        joins ? "Select first mline" : "Select mline",
                        tried ? new[] { "Undo" } : null
                    );

                    if (c.Input.Is("Undo"))
                    {
                        MlineUndo(c, steps);
                        undone = true;
                    }
                    else if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }
                    else if (MlinePicked(c) is DxfUnknownEntity picked)
                    {
                        first = picked;
                        firstAt = c.Input.Point;
                    }
                    else
                    {
                        c.Write("Please pick mlines only.");
                    }
                }

                if (undone)
                {
                    continue;
                }

                DxfUnknownEntity second = null;
                GeoPoint2 secondAt = default;

                while ((joins || cuts) && second == null)
                {
                    yield return CadPrompt.ForEntity(joins ? "Select second mline" : "Select second point");

                    if (c.Input.Kind != CadInputKind.Selection)
                    {
                        yield break;
                    }

                    if (MlinePicked(c) is DxfUnknownEntity picked)
                    {
                        if (cuts && !string.Equals(picked.Handle, first.Handle, StringComparison.OrdinalIgnoreCase))
                        {
                            c.Write("Please pick the same mline.");
                            continue;
                        }

                        second = picked;
                        secondAt = c.Input.Point;
                    }
                    else
                    {
                        c.Write(cuts ? "Please pick the same mline." : "Please pick mlines only.");
                    }
                }

                tried = true;

                if (joins)
                {
                    if (
                        string.Equals(first.Handle, second.Handle, StringComparison.OrdinalIgnoreCase)
                        || !MlineJoin(c, option, first, firstAt, second, secondAt, steps)
                    )
                    {
                        c.Write("These multilines do not intersect.");
                    }
                }
                else
                {
                    MlineEditOne(c, option, first, firstAt, secondAt, steps);
                }
            }
        }

        /// <summary>The multiline the answer to an Entity prompt picked; null where it is another entity.</summary>
        private static DxfEntity MlinePicked(CadCommandContext c)
        {
            DxfEntity entity = c.Input.Handles.Count > 0 ? c.Document.FindEntity(c.Input.Handles[0]) : null;
            return entity != null && CadMlineEdit.IsMline(entity) ? entity : null;
        }

        /// <summary>The crossing, tee or corner of two multilines picked, written into their records; false where the two do not meet as the option needs.</summary>
        private static bool MlineJoin(
            CadCommandContext c,
            string option,
            DxfUnknownEntity first,
            GeoPoint2 firstAt,
            DxfUnknownEntity second,
            GeoPoint2 secondAt,
            List<List<KeyValuePair<string, DxfMlineBuilder>>> steps
        )
        {
            var before = new List<KeyValuePair<string, DxfMlineBuilder>>
            {
                new KeyValuePair<string, DxfMlineBuilder>(first.Handle, new DxfMlineBuilder(first)),
                new KeyValuePair<string, DxfMlineBuilder>(second.Handle, new DxfMlineBuilder(second)),
            };
            var a = new DxfMlineBuilder(first);
            var b = new DxfMlineBuilder(second);
            GeoPoint3 pickFirst = P3(firstAt);
            GeoPoint3 pickSecond = P3(secondAt);
            CadMlineEdit.Nearest(a, pickFirst, out int segmentA, out int _);
            CadMlineEdit.Nearest(b, pickSecond, out int segmentB, out int _);
            CadMlineEdit.Joint joint =
                option[0] == 'C' ? CadMlineEdit.Joint.Closed
                : option[0] == 'O' ? CadMlineEdit.Joint.Open
                : CadMlineEdit.Joint.Merged;
            bool changesSecond = joint != CadMlineEdit.Joint.Closed || option == "CJ";
            bool ok;

            if (option == "CC" || option == "OC" || option == "MC")
            {
                ok = CadMlineEdit.Cross(a, segmentA, b, segmentB, joint);
                changesSecond = joint != CadMlineEdit.Joint.Closed;
            }
            else if (option == "CJ")
            {
                ok = CadMlineEdit.Corner(a, segmentA, pickFirst, b, segmentB, pickSecond);
            }
            else
            {
                ok = CadMlineEdit.Tee(a, segmentA, pickFirst, b, segmentB, joint);
            }

            if (!ok)
            {
                return false;
            }

            var after = new List<KeyValuePair<string, DxfMlineBuilder>>
            {
                new KeyValuePair<string, DxfMlineBuilder>(first.Handle, a),
            };

            if (changesSecond)
            {
                after.Add(new KeyValuePair<string, DxfMlineBuilder>(second.Handle, b));
            }

            MlineWrite(c, after);
            steps.Add(before.GetRange(0, changesSecond ? 2 : 1));
            return true;
        }

        /// <summary>The vertex, cut or weld of an option that works on one multiline, written into its record.</summary>
        private static void MlineEditOne(
            CadCommandContext c,
            string option,
            DxfUnknownEntity entity,
            GeoPoint2 firstAt,
            GeoPoint2 secondAt,
            List<List<KeyValuePair<string, DxfMlineBuilder>>> steps
        )
        {
            var before = new KeyValuePair<string, DxfMlineBuilder>(entity.Handle, new DxfMlineBuilder(entity));
            var builder = new DxfMlineBuilder(entity);
            GeoPoint3 pick = P3(firstAt);

            if (option == "AV")
            {
                if (!CadMlineEdit.AddVertex(builder, pick))
                {
                    return;
                }
            }
            else if (option == "DV")
            {
                if (!CadMlineEdit.DeleteVertex(builder, pick))
                {
                    return;
                }
            }
            else
            {
                CadMlineEdit.Nearest(builder, pick, out int segment, out int element);
                CadMlineEdit.Cut(builder, segment, element, option != "CS", pick, P3(secondAt), option == "WA");
            }

            MlineWrite(
                c,
                new List<KeyValuePair<string, DxfMlineBuilder>>
                {
                    new KeyValuePair<string, DxfMlineBuilder>(entity.Handle, builder),
                }
            );
            steps.Add(new List<KeyValuePair<string, DxfMlineBuilder>> { before });
        }

        /// <summary>Writes the records of some multilines from their builders, one edit.</summary>
        private static void MlineWrite(CadCommandContext c, List<KeyValuePair<string, DxfMlineBuilder>> builders)
        {
            var planned = new List<CadEditRecord>();

            foreach (KeyValuePair<string, DxfMlineBuilder> each in builders)
            {
                planned.Add(new CadEditRecord(CadEditRecordKind.Reshaped, each.Key));
            }

            c.Apply(planned, s =>
            {
                foreach (KeyValuePair<string, DxfMlineBuilder> each in builders)
                {
                    each.Value.Rebuild((DxfUnknownEntity)s.Document.FindEntity(each.Key));
                }
            });
        }

        /// <summary>Takes back the last edit of the command: the multilines it changed written again from what they were.</summary>
        private static void MlineUndo(CadCommandContext c, List<List<KeyValuePair<string, DxfMlineBuilder>>> steps)
        {
            if (steps.Count == 0)
            {
                return;
            }

            List<KeyValuePair<string, DxfMlineBuilder>> last = steps[steps.Count - 1];
            steps.RemoveAt(steps.Count - 1);
            MlineWrite(c, last);
        }
    }
}
