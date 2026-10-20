using System;
using System.Collections.Generic;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// The kinds of item a scene holds, and what the <see cref="SceneItem.Data"/> of each is made of. A point is an array of
    /// its coordinates, <c>[x,y]</c> in the plane and <c>[x,y,z]</c> in space; a shape in the plane sits at z = 0 when drawn.
    /// Every number is written as the 17 significant digits that read back as the same double.
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Kind</term><description>Data</description></listheader>
    /// <item><term>GeoPoint2, GeoPoint3</term><description><c>[x,y]</c>, <c>[x,y,z]</c></description></item>
    /// <item><term>GeoLine2, GeoLine3</term><description><c>{a:point, b:point}</c>, the two ends</description></item>
    /// <item><term>GeoRay2, GeoRay3</term><description><c>{a:origin, b:direction}</c>, the direction as the ray holds it</description></item>
    /// <item><term>GeoXLine2, GeoXLine3</term><description><c>{a:origin, b:direction}</c>, the direction as the line holds it; drawn through the box of the scene</description></item>
    /// <item><term>GeoPolyline2, GeoPolyline3, GeoPolygon2, GeoPolygon3</term><description><c>{points:[point,..]}</c>, as the library holds them</description></item>
    /// <item><term>GeoPolylineArc2, GeoPolygonArc2</term><description><c>{points:[..], bulges:[..]}</c>, the bulge of the edge leaving each vertex</description></item>
    /// <item><term>GeoPolylineArc3, GeoPolygonArc3</term><description><c>{points:[..], bulges:[..], normals:[[x,y,z],..]}</c>, the normal each bulge is read about</description></item>
    /// <item><term>GeoArc2</term><description><c>{center, radius, start, sweep}</c>, the angles in radians as the arc holds them</description></item>
    /// <item><term>GeoArc3</term><description><c>{center, normal, radius, start, sweep}</c></description></item>
    /// <item><term>GeoCircle2, GeoCircle3</term><description><c>{center, radius}</c>, <c>{center, normal, radius}</c></description></item>
    /// <item><term>GeoEllipse2, GeoEllipse3</term><description><c>{center, majorAxis, majorRadius, minorRadius}</c>, with <c>normal</c> in space</description></item>
    /// <item><term>GeoRectangle2</term><description><c>{center, width, height, angle}</c>, the angle in radians</description></item>
    /// <item><term>GeoTriangle2, GeoTriangle3</term><description><c>{points:[a,b,c]}</c></description></item>
    /// <item><term>GeoFace2, GeoFace3</term><description><c>{outer:[point,..], holes:[[point,..],..]}</c>, the loops as the face holds them</description></item>
    /// <item><term>GeoSolid3</term><description><c>{faces:[face,..], openings:[solid,..]}</c></description></item>
    /// <item><term>GeoMesh2, GeoMesh3</term><description><c>{kind, points:[..], faces:[[i,j,k,..],..]}</c>, each face the indexes of its corners, counter-clockwise</description></item>
    /// <item><term>GeoAabb3</term><description><c>{min, max}</c>, or <c>{empty:true}</c></description></item>
    /// <item><term>GeoObb3</term><description><c>{center, sizeX, sizeY, sizeZ, axisX, axisY}</c></description></item>
    /// <item><term>GeoSphere3</term><description><c>{center, radius}</c>; drawn as a fine mesh at the chord tolerance of the scene, with three great circles</description></item>
    /// <item><term>GeoPlane3</term><description><c>{origin, normal}</c>; drawn as a square patch sized to the scene</description></item>
    /// <item><term>Text</term><description><c>{at, text, height}</c>, a label; height 0 draws it at a size that suits the screen</description></item>
    /// </list>
    /// </remarks>
    public static class SceneKinds
    {
        /// <summary>
        /// The kind of a label.
        /// </summary>
        public const string Text = "Text";

        private static readonly string[] _all =
        {
            "GeoPoint2", "GeoPoint3",
            "GeoLine2", "GeoLine3",
            "GeoRay2", "GeoRay3",
            "GeoXLine2", "GeoXLine3",
            "GeoPolyline2", "GeoPolyline3", "GeoPolygon2", "GeoPolygon3",
            "GeoPolylineArc2", "GeoPolygonArc2", "GeoPolylineArc3", "GeoPolygonArc3",
            "GeoArc2", "GeoArc3", "GeoCircle2", "GeoCircle3", "GeoEllipse2", "GeoEllipse3",
            "GeoRectangle2", "GeoTriangle2", "GeoTriangle3",
            "GeoFace2", "GeoFace3", "GeoSolid3", "GeoMesh2", "GeoMesh3",
            "GeoAabb3", "GeoObb3", "GeoSphere3", "GeoPlane3",
            Text,
        };

        private static readonly HashSet<string> _known = new HashSet<string>(_all, StringComparer.Ordinal);

        /// <summary>
        /// Gets every kind the viewer draws, in a fixed order.
        /// </summary>
        public static IReadOnlyList<string> All => _all;

        /// <summary>
        /// Gets whether the viewer draws items of a kind.
        /// </summary>
        /// <param name="kind">The kind, as written in a scene.</param>
        public static bool IsKnown(string kind) => kind != null && _known.Contains(kind);
    }
}
