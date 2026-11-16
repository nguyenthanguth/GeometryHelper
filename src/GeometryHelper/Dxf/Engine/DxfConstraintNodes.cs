using System;
using System.Collections.Generic;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The classes of the nodes of a constraint group (ACDBASSOC2DCONSTRAINTGROUP) that are known, and the groups each holds
    /// in its DXF past its byte, which the layout of AutoCAD 2018 (the version 2 of the group) and that of AutoCAD 2010 and
    /// 2007 (the version 1) share. The converter takes a group down from the one to the other and the DWG writer writes both.
    /// </summary>
    internal static class DxfConstraintNodes
    {
        private static readonly HashSet<string> Known = new HashSet<string>(StringComparer.Ordinal)
        {
            "AcConstrainedImplicitPoint",
            "AcConstrainedBoundedLine",
            "AcConstrainedArc",
            "AcConstrainedCircle",
            "AcConstrainedEllipse",
            "AcConstrainedBoundedEllipse",
            "AcConstrainedDatumLine",
            "AcConstrained2PointsConstructionLine",
            "AcPointCurveConstraint",
            "AcPointCoincidenceConstraint",
            "AcMidPointConstraint",
            "AcCenterPointConstraint",
            "AcConcentricConstraint",
            "AcColinearConstraint",
            "AcParallelConstraint",
            "AcPerpendicularConstraint",
            "AcTangentConstraint",
            "AcEqualLengthConstraint",
            "AcEqualRadiusConstraint",
            "AcFixedConstraint",
            "AcHorizontalConstraint",
            "AcDistanceConstraint",
            "AcAngleConstraint",
            "Ac3PointAngleConstraint",
            "AcRadiusDiameterConstraint",
        };

        /// <summary>Whether <paramref name="kind"/> is the class of a node the library knows the groups of.</summary>
        internal static bool IsKnown(string kind) => Known.Contains(kind);

        /// <summary>
        /// The codes of the groups a node of <paramref name="kind"/> holds past its byte, in the order of DXF; the circles and
        /// arcs of <paramref name="version2"/> have a fourth real after their three.
        /// </summary>
        internal static List<int> FieldCodes(string kind, bool version2)
        {
            var codes = new List<int>();

            void AddPoint(int code)
            {
                codes.AddRange(new[] { code, code + 10, code + 20 });
            }

            switch (kind)
            {
                case "AcConstrainedImplicitPoint":
                    codes.AddRange(new[] { 330, 90, 280, 90, 90 });
                    break;
                case "AcConstrainedBoundedLine":
                    codes.AddRange(new[] { 330, 90 });
                    AddPoint(10);
                    AddPoint(10);
                    codes.Add(290);
                    AddPoint(10);
                    AddPoint(11);
                    break;
                case "AcConstrainedArc":
                case "AcConstrainedCircle":
                    codes.AddRange(new[] { 330, 90 });

                    for (int i = 0; i < 3; i++)
                    {
                        AddPoint(10);
                    }

                    for (int i = 0; i < (version2 ? 4 : 3); i++)
                    {
                        codes.Add(40);
                    }

                    if (kind == "AcConstrainedArc")
                    {
                        AddPoint(10);
                        AddPoint(11);
                    }

                    break;
                case "AcConstrainedEllipse":
                case "AcConstrainedBoundedEllipse":
                    // The centre, the major axis and the ratio, the same in both layouts; a bounded one adds its two end points.
                    codes.AddRange(new[] { 330, 90 });
                    AddPoint(10);
                    AddPoint(11);
                    codes.Add(40);

                    if (kind == "AcConstrainedBoundedEllipse")
                    {
                        AddPoint(10);
                        AddPoint(11);
                    }

                    break;
                case "AcConstrainedDatumLine":
                case "AcConstrained2PointsConstructionLine":
                    codes.AddRange(new[] { 330, 90 });
                    AddPoint(10);
                    AddPoint(10);
                    break;
                default:
                    codes.AddRange(new[] { 90, 290, 290 });

                    if (kind == "AcHorizontalConstraint")
                    {
                        codes.Add(90);
                    }
                    else if (
                        kind == "AcDistanceConstraint"
                        || kind == "AcAngleConstraint"
                        || kind == "Ac3PointAngleConstraint"
                        || kind == "AcRadiusDiameterConstraint"
                    )
                    {
                        codes.AddRange(new[] { 340, 340, 280 });

                        if (kind == "AcDistanceConstraint")
                        {
                            AddPoint(10);
                        }
                    }

                    break;
            }

            return codes;
        }
    }
}
