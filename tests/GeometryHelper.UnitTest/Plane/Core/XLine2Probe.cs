using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper;
using GeometryHelper.Geometry;

namespace GeometryHelper.UnitTest.Plane
{
    /// <summary>
    /// Asks any shape in the plane the five queries from either side by reflection, so that one table of cases can run
    /// against every pair, and so that a member missing shows as a named failure rather than a compile error.
    /// </summary>
    /// <remarks>
    /// <c>a.Query(b)</c> is found by name and by the type of its first argument, with and without the <see cref="Tolerance"/>
    /// form, and a <c>TryIntersectWith</c> by the type of its <c>out</c> argument too, a point for a line, a ray and another
    /// infinite line and an array of points for every shape else.
    /// </remarks>
    internal static class XLine2Probe
    {
        /// <summary>A thousandth for points and 1E-5 for vectors, as the default but a value of its own.</summary>
        internal static readonly Tolerance Strict = new Tolerance(1E-3, 1E-5);

        private static MethodInfo Find(object owner, string name, object arg, bool withTolerance, Type outType = null)
        {
            var types = new List<Type> { arg.GetType() };

            if (outType != null)
            {
                types.Add(outType.MakeByRefType());
            }

            if (withTolerance)
            {
                types.Add(typeof(Tolerance));
            }

            MethodInfo method = owner.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, types.ToArray(), null);

            if (method == null)
            {
                throw new MissingMemberException(owner.GetType().Name + "." + name + "(" + string.Join(", ", types.Select(t => t.Name)) + ")");
            }

            return method;
        }

        /// <summary>Whether <paramref name="owner"/> answers <paramref name="name"/> about <paramref name="arg"/>, in the form with a tolerance.</summary>
        internal static bool Answers(object owner, string name, object arg)
        {
            return owner.GetType().GetMethods().Any(m => m.Name == name && m.GetParameters().Length > 0
                && m.GetParameters()[0].ParameterType == arg.GetType()
                && m.GetParameters().Any(p => p.ParameterType == typeof(Tolerance)));
        }

        internal static bool Collides(object a, object b, Tolerance? tolerance = null)
            => (bool)Unwrap(() => tolerance.HasValue
                ? Find(a, "CollidesWith", b, true).Invoke(a, new[] { b, tolerance.Value })
                : Find(a, "CollidesWith", b, false).Invoke(a, new[] { b }));

        internal static double Distance(object a, object b, Tolerance? tolerance = null)
            => (double)Unwrap(() => tolerance.HasValue
                ? Find(a, "DistanceTo", b, true).Invoke(a, new[] { b, tolerance.Value })
                : Find(a, "DistanceTo", b, false).Invoke(a, new[] { b }));

        internal static GeoPoint2[] Crossings(object a, object b, Tolerance? tolerance = null)
            => (GeoPoint2[])Unwrap(() => tolerance.HasValue
                ? Find(a, "GetIntersections", b, true).Invoke(a, new[] { b, tolerance.Value })
                : Find(a, "GetIntersections", b, false).Invoke(a, new[] { b }));

        internal static GeoLine2 Shortest(object a, object b, Tolerance? tolerance = null)
            => (GeoLine2)Unwrap(() => tolerance.HasValue
                ? Find(a, "GetShortestLineTo", b, true).Invoke(a, new[] { b, tolerance.Value })
                : Find(a, "GetShortestLineTo", b, false).Invoke(a, new[] { b }));

        /// <summary>
        /// Asks <c>TryIntersectWith</c>: whether they meet and where, a single point wrapped as an array.
        /// </summary>
        internal static bool Try(object a, object b, out GeoPoint2[] points, Tolerance? tolerance = null)
        {
            Type outType = a.GetType().GetMethods().Any(m => m.Name == "TryIntersectWith" && m.GetParameters().Length > 1
                && m.GetParameters()[0].ParameterType == b.GetType() && m.GetParameters()[1].ParameterType == typeof(GeoPoint2).MakeByRefType())
                ? typeof(GeoPoint2)
                : typeof(GeoPoint2[]);

            MethodInfo method = Find(a, "TryIntersectWith", b, tolerance.HasValue, outType);
            object[] arguments = tolerance.HasValue ? new object[] { b, null, tolerance.Value } : new object[] { b, null };
            bool hit = (bool)Unwrap(() => method.Invoke(a, arguments));

            points = outType == typeof(GeoPoint2)
                ? (hit ? new[] { (GeoPoint2)arguments[1] } : new GeoPoint2[0])
                : (GeoPoint2[])arguments[1];
            return hit;
        }

        /// <summary>Whether <c>TryIntersectWith</c> exists for the pair, in either shape of its <c>out</c>.</summary>
        internal static bool HasTry(object a, object b)
        {
            return a.GetType().GetMethods().Any(m => m.Name == "TryIntersectWith" && m.GetParameters().Length > 1 && m.GetParameters()[0].ParameterType == b.GetType());
        }

        private static object Unwrap(Func<object> call)
        {
            try
            {
                return call();
            }
            catch (TargetInvocationException e)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }
    }
}
