using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Common
{
    /// <summary>
    /// Reads the measuring questions every shape type answers, by reflection, so that the plane and space
    /// guards in the two ApiSymmetryTests can say which shape answers a question about another that the
    /// other cannot answer back, and which pair has a distance but no shortest line.
    /// </summary>
    /// <remarks>
    /// A pair is one-sided when <c>A.Query(B ...)</c> exists, in any overload, and no <c>B.Query(A ...)</c>
    /// does, or when only one of the two has the form taking a <see cref="Tolerance"/>. Two kinds of gap are written down as wanted and left out: a question taking an ellipse, which is
    /// asked from the ellipse, and the point's <c>CollidesWith</c>, <c>GetIntersections</c> and
    /// <c>TryIntersectWith</c>, which a point answers with <c>IsPointOn</c> and <c>Contains</c>.
    /// </remarks>
    internal static class QuerySymmetry
    {
        /// <summary>The questions read from either side.</summary>
        internal static readonly string[] Queries = { "DistanceTo", "CollidesWith", "GetIntersections", "TryIntersectWith", "GetShortestLineTo" };

        /// <summary>The point's three questions it answers some other way.</summary>
        private static readonly string[] NotAskedOfAPoint = { "CollidesWith", "GetIntersections", "TryIntersectWith" };

        /// <summary>The Geo types of the namespace that are not shapes: a direction, a motion and a frame.</summary>
        private static readonly string[] NotShapes = { "Vector", "Transform", "CoordinateSystem" };

        /// <summary>
        /// Gets the public shape types of one dimension, <c>'2'</c> or <c>'3'</c>, in name order.
        /// </summary>
        internal static Type[] ShapeTypes(char dimension)
        {
            return typeof(GeoPoint2).Assembly
                .GetTypes()
                .Where(t => t.IsPublic && t.Namespace == "GeometryHelper.Geometry" && t.Name.StartsWith("Geo", StringComparison.Ordinal))
                .Where(t => t.Name[t.Name.Length - 1] == dimension)
                .Where(t => !NotShapes.Contains(t.Name.Substring(3, t.Name.Length - 4)))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Gets whether a type answers a question whose first argument is another type, in any overload.
        /// </summary>
        internal static bool Answers(Type asked, string query, Type argument)
        {
            return asked
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.Name == query && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == argument);
        }

        /// <summary>
        /// Gets whether a type answers a question whose first argument is another type in a form that takes a tolerance.
        /// </summary>
        internal static bool AnswersWithTolerance(Type asked, string query, Type argument)
        {
            return asked
                .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Any(m => m.Name == query
                    && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == argument
                    && m.GetParameters().Any(p => p.ParameterType == typeof(Tolerance)));
        }

        /// <summary>
        /// Gets every one-sided pair of one dimension as the member missing, <c>B.Query(A)</c>, with the
        /// side that has it, in ordinal order.
        /// </summary>
        internal static string[] OneSidedPairs(char dimension)
        {
            Type[] shapes = ShapeTypes(dimension);
            var missing = new List<string>();

            foreach (string query in Queries)
            {
                foreach (Type a in shapes)
                {
                    foreach (Type b in shapes)
                    {
                        if (a == b || Excepted(b, query, a) || !Answers(a, query, b))
                        {
                            continue;
                        }

                        if (!Answers(b, query, a))
                        {
                            missing.Add(b.Name + "." + query + "(" + a.Name + ")  <- " + a.Name + "." + query + "(" + b.Name + ")");
                        }
                        else if (AnswersWithTolerance(a, query, b) && !AnswersWithTolerance(b, query, a))
                        {
                            missing.Add(b.Name + "." + query + "(" + a.Name + ", Tolerance)  <- " + a.Name + "." + query + "(" + b.Name + ", Tolerance)");
                        }
                    }
                }
            }

            return missing.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Gets every shortest line missing in one dimension where the two shapes already have a distance,
        /// as the member missing, in ordinal order.
        /// </summary>
        /// <remarks>
        /// Wherever <c>A.DistanceTo(B)</c> or <c>B.DistanceTo(A)</c> exists, in any overload, <c>A</c> must
        /// give <c>GetShortestLineTo(B)</c> both without and with a <see cref="Tolerance"/>, returning the
        /// line of its dimension. A form missing is named with its arguments; both missing are written
        /// <c>(B[, Tolerance])</c>. The written exceptions are in <see cref="WithoutShortestLine"/>.
        /// </remarks>
        internal static string[] MissingShortestLines(char dimension)
        {
            Type[] shapes = ShapeTypes(dimension);
            Type line = dimension == '2' ? typeof(GeoLine2) : typeof(GeoLine3);
            var missing = new List<string>();

            foreach (Type a in shapes)
            {
                foreach (Type b in shapes)
                {
                    if (WithoutShortestLine(a, b) || (!Answers(a, "DistanceTo", b) && !Answers(b, "DistanceTo", a)))
                    {
                        continue;
                    }

                    bool plain = a.GetMethod("GetShortestLineTo", new[] { b })?.ReturnType == line;
                    bool within = a.GetMethod("GetShortestLineTo", new[] { b, typeof(Tolerance) })?.ReturnType == line;

                    if (!plain && !within)
                    {
                        missing.Add(a.Name + ".GetShortestLineTo(" + b.Name + "[, Tolerance])");
                    }
                    else if (!plain)
                    {
                        missing.Add(a.Name + ".GetShortestLineTo(" + b.Name + ")");
                    }
                    else if (!within)
                    {
                        missing.Add(a.Name + ".GetShortestLineTo(" + b.Name + ", Tolerance)");
                    }
                }
            }

            return missing.OrderBy(s => s, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// Gets whether a pair with a distance is written down as having no shortest line.
        /// </summary>
        /// <remarks>
        /// A box's shortest line goes through <c>ToSolid()</c>, by an earlier decision; two planes have no one
        /// segment, joined at every point when parallel and along a line when they cross; the segment between
        /// two points is <c>new GeoLine3(a, b)</c>; and an ellipse is asked from the ellipse.
        /// </remarks>
        private static bool WithoutShortestLine(Type asked, Type argument)
        {
            return argument.Name.StartsWith("GeoEllipse", StringComparison.Ordinal)
                || asked == typeof(GeoAabb3) || asked == typeof(GeoObb3)
                || argument == typeof(GeoAabb3) || argument == typeof(GeoObb3)
                || (asked == argument && (asked == typeof(GeoPlane3) || asked == typeof(GeoPoint3) || asked == typeof(GeoPoint2)));
        }

        /// <summary>
        /// Gets whether a missing member is one of the written exceptions.
        /// </summary>
        private static bool Excepted(Type asked, string query, Type argument)
        {
            if (argument.Name.StartsWith("GeoEllipse", StringComparison.Ordinal))
            {
                // An ellipse is asked from the ellipse; both ellipse specs decided it.
                return true;
            }

            return asked.Name.StartsWith("GeoPoint", StringComparison.Ordinal) && NotAskedOfAPoint.Contains(query);
        }
    }
}
