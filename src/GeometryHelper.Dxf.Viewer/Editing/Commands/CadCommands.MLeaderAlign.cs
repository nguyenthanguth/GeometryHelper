using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLEADERALIGN (MLA), as AutoCAD 2021's (its prompts probed in its console): "Select multileaders:", "Current mode: Use
    /// current spacing", "Select multileader to align to or [Options]:", then the direction; the content of each multileader
    /// (its words' attachment point, or its block's position) is moved with its landing (<see cref="DxfMoveType.MoveContentAndDoglegPoints"/>,
    /// the arrowheads staying) onto the line through the content of the one picked along the direction. Use current spacing
    /// keeps each content's place along the line; Specify spacing puts them a gap apart along it, the one picked staying;
    /// Distribute spaces them evenly between two points. The gap is measured between the content points, not their extents.
    /// Make leader segments parallel is not built.
    /// </summary>
    internal static partial class CadCommands
    {
        // Distribute 0, make leader segments Parallel 1, specify Spacing 2, Use current spacing 3 (the default).
        private static readonly string[] MLeaderAlignModes =
        {
            "Distribute",
            "make leader segments Parallel",
            "specify Spacing",
            "Use current spacing",
        };

        private static IEnumerable<CadPrompt> MLeaderAlign(CadCommandContext c)
        {
            yield return CadPrompt.ForSelection("Select multileaders");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            var members = new List<DxfMLeader>();
            int others = 0;
            int locked = 0;

            foreach (string handle in c.Input.Handles)
            {
                DxfEntity entity = c.Document.FindEntity(handle);

                if (entity == null)
                {
                    continue;
                }

                if (!(entity is DxfMLeader mleader) || ContentPoint(mleader) == null)
                {
                    others++;
                }
                else if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
                {
                    locked++;
                }
                else
                {
                    members.Add(mleader);
                }
            }

            if (others > 0)
            {
                c.Write(
                    others.ToString(CultureInfo.InvariantCulture) + (others == 1 ? " was" : " were") + " filtered out."
                );
            }

            if (locked > 0)
            {
                c.Write(
                    locked.ToString(CultureInfo.InvariantCulture)
                        + (locked == 1 ? " was" : " were")
                        + " on a locked layer."
                );
            }

            if (members.Count == 0)
            {
                yield break;
            }

            c.Write(MLeaderAlignMode(c));
            DxfMLeader reference = null;
            GeoVector2 direction = default;
            GeoPoint2 first = default;
            GeoPoint2 second = default;
            bool between = false;

            while (true)
            {
                int mode = (int)c.Remembered("mleaderalign.mode", 3.0);

                if (mode == 0)
                {
                    yield return CadPrompt.ForPoint("Specify first point", null, new[] { "Options" });
                }
                else
                {
                    yield return CadPrompt.ForEntity("Select multileader to align to", new[] { "Options" });
                }

                if (c.Input.Is("Options"))
                {
                    foreach (CadPrompt prompt in MLeaderAlignChooseMode(c))
                    {
                        yield return prompt;
                    }

                    c.Write(MLeaderAlignMode(c));
                    continue;
                }

                if (mode == 0)
                {
                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    first = c.Input.Point;
                    yield return CadPrompt.ForPoint("Specify second point", first);

                    if (!c.Input.IsPoint)
                    {
                        yield break;
                    }

                    second = c.Input.Point;
                    between = true;
                    break;
                }

                if (c.Input.Kind != CadInputKind.Selection)
                {
                    yield break;
                }

                reference = c.Document.FindEntity(c.Input.Handles[0]) as DxfMLeader;
                GeoPoint3? at = reference == null ? (GeoPoint3?)null : ContentPoint(reference);

                if (at == null)
                {
                    c.Write("Object selected is not a multileader with content.");
                    continue;
                }

                yield return CadPrompt.ForPoint("Specify direction", new GeoPoint2(at.Value.X, at.Value.Y));

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                direction = c.Input.Point - new GeoPoint2(at.Value.X, at.Value.Y);

                if (direction.Length < 1E-9)
                {
                    c.Write("The direction needs a second point apart from the first.");
                    yield break;
                }

                break;
            }

            Dictionary<string, GeoVector2> moves = between
                ? MLeaderDistribute(members, first, second)
                : MLeaderAlignMoves(
                    members,
                    reference,
                    direction,
                    (int)c.Remembered("mleaderalign.mode", 3.0) == 2
                        ? c.Remembered("mleaderalign.spacing", 0.0)
                        : (double?)null
                );
            MoveContents(c, members, moves);
        }

        /// <summary>The point the content of a multileader is held by: its words' attachment point or its block's position; null for a multileader with neither.</summary>
        private static GeoPoint3? ContentPoint(DxfMLeader mleader)
        {
            switch (mleader.ContentType)
            {
                case DxfContentType.MTextContent:
                    return mleader.HasContent() ? mleader.TextLocation : (GeoPoint3?)null;
                case DxfContentType.BlockContent:
                    return mleader.HasContent() ? mleader.BlockPosition : (GeoPoint3?)null;
                default:
                    return null;
            }
        }

        private static IEnumerable<CadPrompt> MLeaderAlignChooseMode(CadCommandContext c)
        {
            yield return CadPrompt.ForKeyword(
                "Enter an option",
                MLeaderAlignModes,
                MLeaderAlignModes[(int)c.Remembered("mleaderalign.mode", 3.0)]
            );

            int chosen = Array.IndexOf(MLeaderAlignModes, c.Input.Text);

            if (chosen < 0)
            {
                yield break;
            }

            if (chosen == 1)
            {
                c.Write("Make leader segments parallel is not built here: the mode stays as it was.");
                yield break;
            }

            c.Remember("mleaderalign.mode", chosen);

            if (chosen == 2)
            {
                yield return CadPrompt.ForDistance(
                    "Specify spacing",
                    null,
                    null,
                    Length(c.Remembered("mleaderalign.spacing", 0.0))
                );

                if (c.Input.Kind == CadInputKind.Number)
                {
                    c.Remember("mleaderalign.spacing", c.Input.Number);
                }
            }
        }

        private static string MLeaderAlignMode(CadCommandContext c)
        {
            switch ((int)c.Remembered("mleaderalign.mode", 3.0))
            {
                case 0:
                    return "Current mode: Distribute";
                case 1:
                    return "Current mode: Make leader segments parallel";
                case 2:
                    return "Current mode: Specify spacing = " + Length(c.Remembered("mleaderalign.spacing", 0.0));
                default:
                    return "Current mode: Use current spacing";
            }
        }

        /// <summary>
        /// How far each content moves onto the line through the content of the multileader picked along a direction: across
        /// the line only, keeping its place along it; with a spacing, a gap apart along the line in the order they stand, the
        /// one picked staying where it is (the first of them where it is not among them).
        /// </summary>
        private static Dictionary<string, GeoVector2> MLeaderAlignMoves(
            IReadOnlyList<DxfMLeader> members,
            DxfMLeader reference,
            GeoVector2 direction,
            double? spacing
        )
        {
            var moves = new Dictionary<string, GeoVector2>(StringComparer.OrdinalIgnoreCase);
            GeoVector2 along = direction / direction.Length;
            GeoPoint3 origin3 = ContentPoint(reference).Value;
            var origin = new GeoPoint2(origin3.X, origin3.Y);
            List<DxfMLeader> ordered = members
                .OrderBy(m => Along(m, origin, along))
                .ThenBy(m => m.Handle, StringComparer.OrdinalIgnoreCase)
                .ToList();
            int fixedAt = ordered.FindIndex(m =>
                string.Equals(m.Handle, reference.Handle, StringComparison.OrdinalIgnoreCase)
            );
            var wanted = new double[ordered.Count];

            for (int i = 0; i < ordered.Count; i++)
            {
                wanted[i] = Along(ordered[i], origin, along);
            }

            if (spacing.HasValue)
            {
                int stay = fixedAt < 0 ? 0 : fixedAt;

                for (int i = stay - 1; i >= 0; i--)
                {
                    wanted[i] = wanted[i + 1] - spacing.Value;
                }

                for (int i = stay + 1; i < ordered.Count; i++)
                {
                    wanted[i] = wanted[i - 1] + spacing.Value;
                }
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                GeoPoint3 now = ContentPoint(ordered[i]).Value;
                GeoPoint2 target = origin + along * wanted[i];
                var move = new GeoVector2(target.X - now.X, target.Y - now.Y);

                if (move.Length > 1E-9)
                {
                    moves[ordered[i].Handle] = move;
                }
            }

            return moves;
        }

        private static double Along(DxfMLeader mleader, GeoPoint2 origin, GeoVector2 along)
        {
            GeoPoint3 at = ContentPoint(mleader).Value;
            return (at.X - origin.X) * along.X + (at.Y - origin.Y) * along.Y;
        }

        /// <summary>How far each content moves to stand evenly between two points, the first at the first point and the last at the last, in the order they stand along the line.</summary>
        private static Dictionary<string, GeoVector2> MLeaderDistribute(
            IReadOnlyList<DxfMLeader> members,
            GeoPoint2 first,
            GeoPoint2 second
        )
        {
            var moves = new Dictionary<string, GeoVector2>(StringComparer.OrdinalIgnoreCase);
            GeoVector2 line = second - first;
            double length = line.Length;

            if (length < 1E-9)
            {
                return moves;
            }

            GeoVector2 along = line / length;
            List<DxfMLeader> ordered = members
                .OrderBy(m => Along(m, first, along))
                .ThenBy(m => m.Handle, StringComparer.OrdinalIgnoreCase)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                double fraction = ordered.Count == 1 ? 0.0 : (double)i / (ordered.Count - 1);
                GeoPoint2 target = first + line * fraction;
                GeoPoint3 now = ContentPoint(ordered[i]).Value;
                var move = new GeoVector2(target.X - now.X, target.Y - now.Y);

                if (move.Length > 1E-9)
                {
                    moves[ordered[i].Handle] = move;
                }
            }

            return moves;
        }

        /// <summary>Moves the content of each multileader by its own vector, with its landing, as one edit of the command.</summary>
        private static void MoveContents(
            CadCommandContext c,
            IReadOnlyList<DxfMLeader> members,
            IReadOnlyDictionary<string, GeoVector2> moves
        )
        {
            if (moves.Count == 0)
            {
                return;
            }

            List<string> handles = moves.Keys.OrderBy(h => h, StringComparer.OrdinalIgnoreCase).ToList();
            var planned = handles
                .Select(h => new CadEditRecord(
                    CadEditRecordKind.PropertyChanged,
                    h,
                    "Content position",
                    null,
                    Values(moves[h].X, moves[h].Y)
                ))
                .ToList();
            CadEditResult result = c.Apply(planned, s =>
            {
                foreach (string handle in handles)
                {
                    if (!s.IsHandled(handle))
                    {
                        ((DxfMLeader)s.Document.FindEntity(handle)).MoveMLeader(
                            new GeoVector3(moves[handle].X, moves[handle].Y, 0.0),
                            DxfMoveType.MoveContentAndDoglegPoints
                        );
                    }
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }
    }
}
