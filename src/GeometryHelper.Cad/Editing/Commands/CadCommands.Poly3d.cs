using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// 3DPOLY as AutoCAD 2021's console asks it: "Specify start point of polyline:", then "Specify endpoint of line or
    /// [Undo]:" for the first two endpoints and "Specify endpoint of line or [Close/Undo]:" from the third, Enter ending it. The
    /// result is one 3D polyline, a POLYLINE of flag 8 whose vertices are of flag 32. The presenter's points are those of a
    /// plane, so a Z typed (<c>10,0,5</c>) is read and left out: every vertex is on Z 0.
    /// </summary>
    internal static partial class CadCommands
    {
        private static IEnumerable<CadPrompt> Polyline3d(CadCommandContext c)
        {
            yield return CadPrompt.ForPoint("Specify start point of polyline");

            if (!c.Input.IsPoint)
            {
                yield break;
            }

            var points = new List<GeoPoint2> { c.Input.Point };
            string handle = null;

            while (true)
            {
                GeoPoint2 last = points[points.Count - 1];
                yield return CadPrompt.ForPoint(
                    "Specify endpoint of line",
                    last,
                    points.Count >= 3 ? new[] { "Close", "Undo" } : new[] { "Undo" }
                );

                if (c.Input.Is("Undo"))
                {
                    if (points.Count == 1)
                    {
                        c.Write("All segments already undone.");
                        continue;
                    }

                    if (points.Count == 2)
                    {
                        EraseAll(c, new[] { handle });
                        handle = null;
                    }
                    else
                    {
                        string growing = handle;
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, growing) }, s =>
                            ((DxfPolyline3d)s.Document.FindEntity(growing)).Last().Erase()
                        );
                    }

                    points.RemoveAt(points.Count - 1);
                }
                else if (c.Input.Is("Close"))
                {
                    string growing = handle;
                    c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, growing) }, s =>
                        ((DxfPolyline3d)s.Document.FindEntity(growing)).Closed = true
                    );
                    yield break;
                }
                else if (c.Input.IsPoint)
                {
                    GeoPoint2 next = c.Input.Point;

                    if (handle == null)
                    {
                        GeoPoint2 first = last;
                        handle = AddedHandle(
                            c.Apply(null, s =>
                                s.Append(
                                    c.Space,
                                    Current(
                                        c,
                                        new DxfPolyline3d(
                                            DxfPoly3dType.SimplePoly,
                                            new[] { P3(first), P3(next) },
                                            false
                                        )
                                    )
                                )
                            )
                        );

                        if (handle == null)
                        {
                            yield break;
                        }
                    }
                    else
                    {
                        string growing = handle;
                        c.Apply(new[] { new CadEditRecord(CadEditRecordKind.Reshaped, growing) }, s =>
                            ((DxfPolyline3d)s.Document.FindEntity(growing)).AppendVertex(
                                new DxfPolylineVertex3d(P3(next))
                            )
                        );
                    }

                    points.Add(next);
                }
                else
                {
                    yield break;
                }
            }
        }
    }
}
