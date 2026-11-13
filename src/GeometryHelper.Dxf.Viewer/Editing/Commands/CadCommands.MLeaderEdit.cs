using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// MLEADEREDIT (MLE), as AutoCAD 2021's (its prompts probed in its console): "Select a multileader:", then "Specify leader
    /// arrowhead location or [Remove leaders]:", each point given adding a leader line whose arrowhead stands there (as
    /// <see cref="DxfMLeader.AddLeaderLine(GeoPoint3)"/> adds one), and under Remove leaders "Select leader to remove or [Add
    /// leaders]:", the leader line picked taken away; Enter ends it, each change one edit of the command's one undo step. An
    /// annotative multileader has each change made in the context of every scale it is shown at, not in its default's alone: a
    /// line added for an arrowhead on the right of words lands against their right end at each scale, as AutoCAD 2021 lands it.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> MLeaderEdit(CadCommandContext c)
        {
            yield return CadPrompt.ForEntity("Select a multileader");

            if (c.Input.Kind != CadInputKind.Selection)
            {
                yield break;
            }

            string handle = c.Input.Handles[0];
            DxfEntity entity = c.Document.FindEntity(handle);

            if (!(entity is DxfMLeader))
            {
                c.Write("Object selected is not a multileader.");
                yield break;
            }

            if (CadLayerEditor.IsLocked(c.Database, entity.Layer))
            {
                c.Write("The multileader is on a locked layer.");
                yield break;
            }

            bool adding = true;

            while (true)
            {
                yield return adding
                    ? CadPrompt.ForPoint("Specify leader arrowhead location", null, new[] { "Remove leaders" })
                    : CadPrompt.ForPoint("Select leader to remove", null, new[] { "Add leaders" });

                if (c.Input.Is("Remove leaders") || c.Input.Is("Add leaders"))
                {
                    adding = c.Input.Is("Add leaders");
                    continue;
                }

                if (!c.Input.IsPoint)
                {
                    yield break;
                }

                GeoPoint2 point = c.Input.Point;
                var mleader = (DxfMLeader)c.Document.FindEntity(handle);

                if (adding)
                {
                    ChangeLeaderLines(c, handle, "Leader line added", point, m => m.AddLeaderLine(P3(point)));
                    continue;
                }

                if (mleader.LeaderLineCount <= 1)
                {
                    c.Write("A multileader keeps at least one leader line.");
                    continue;
                }

                int line = NearestLeaderLine(mleader, point, c.Session.PickAperture);

                if (line < 0)
                {
                    c.Write("No leader line there.");
                    continue;
                }

                ChangeLeaderLines(c, handle, "Leader line removed", point, m => m.RemoveLeaderLine(line));
            }
        }

        /// <summary>Changes the leader lines of a multileader as one edit of the command, the point it was made at kept in the record.</summary>
        private static void ChangeLeaderLines(
            CadCommandContext c,
            string handle,
            string property,
            GeoPoint2 point,
            Action<DxfMLeader> change
        )
        {
            var planned = new[]
            {
                new CadEditRecord(CadEditRecordKind.PropertyChanged, handle, property, null, Values(point.X, point.Y)),
            };
            CadEditResult result = c.Apply(planned, s =>
            {
                if (!s.IsHandled(handle))
                {
                    // An annotative multileader has the change made in the context of each scale as well as in its record, as
                    // AutoCAD 2021's MLEADEREDIT makes it.
                    var mleader = (DxfMLeader)s.Document.FindEntity(handle);
                    mleader.EditMLeaderAtEachScale(() => change(mleader));
                }
            });

            if (!result.Succeeded)
            {
                c.Write(result.Message);
            }
        }

        /// <summary>
        /// The index of the leader line of a multileader a point is nearest, within the pick aperture: the distance to the
        /// line through its vertices from its arrowhead; -1 where no line is within it.
        /// </summary>
        private static int NearestLeaderLine(DxfMLeader mleader, GeoPoint2 point, double aperture)
        {
            int found = -1;
            double nearest = double.PositiveInfinity;

            foreach (int leader in mleader.GetLeaderIndexes())
            {
                foreach (int line in mleader.GetLeaderLineIndexes(leader).OrderBy(i => i))
                {
                    int count = mleader.VerticesCount(line);
                    double distance = double.PositiveInfinity;

                    for (int i = 0; i < count; i++)
                    {
                        GeoPoint3 a = mleader.GetVertex(line, i);
                        GeoPoint3 b = i + 1 < count ? mleader.GetVertex(line, i + 1) : a;
                        distance = Math.Min(
                            distance,
                            DistanceToSegment(point, new GeoPoint2(a.X, a.Y), new GeoPoint2(b.X, b.Y))
                        );
                    }

                    if (distance <= aperture && distance < nearest)
                    {
                        nearest = distance;
                        found = line;
                    }
                }
            }

            return found;
        }

        /// <summary>The distance from a point to the segment between two others.</summary>
        private static double DistanceToSegment(GeoPoint2 p, GeoPoint2 a, GeoPoint2 b)
        {
            GeoVector2 along = b - a;
            double length2 = along.X * along.X + along.Y * along.Y;
            double t = length2 <= 0.0 ? 0.0 : ((p.X - a.X) * along.X + (p.Y - a.Y) * along.Y) / length2;
            t = Math.Max(0.0, Math.Min(1.0, t));
            return (p - new GeoPoint2(a.X + along.X * t, a.Y + along.Y * t)).Length;
        }
    }
}
