using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GeometryHelper.Geometry;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// What the library says about two items: the distance, and the shortest segment between them.
    /// </summary>
    public sealed class LibraryDistance
    {
        internal LibraryDistance(string firstName, string secondName)
        {
            FirstName = firstName;
            SecondName = secondName;
        }

        /// <summary>
        /// Gets the name of the first item with its kind.
        /// </summary>
        public string FirstName { get; }

        /// <summary>
        /// Gets the name of the second item with its kind.
        /// </summary>
        public string SecondName { get; }

        /// <summary>
        /// Gets what <c>DistanceTo</c> gave, or null where the library has none between the two kinds.
        /// </summary>
        public double? Distance { get; internal set; }

        /// <summary>
        /// Gets where <c>GetShortestLineTo</c> starts, on the first item, or null where the library has none.
        /// </summary>
        public Vec3? From { get; internal set; }

        /// <summary>
        /// Gets where <c>GetShortestLineTo</c> ends, on the second item, or null where the library has none.
        /// </summary>
        public Vec3? To { get; internal set; }

        /// <summary>
        /// Gets what is to be said besides the numbers: that the library has no such call between the two kinds, that it refused, or that
        /// the shapes had to be rebuilt at 1E-12.
        /// </summary>
        public List<string> Notes { get; } = new List<string>();
    }

    /// <summary>
    /// Asks the library for the distance and the shortest segment between two items of a scene, the shapes rebuilt from their data and the
    /// calls made under the tolerance of the scene.
    /// </summary>
    public static class LibraryMeasure
    {
        /// <summary>
        /// Measures between two items with the library's <c>DistanceTo</c> and <c>GetShortestLineTo</c>.
        /// </summary>
        /// <param name="scene">The scene; its tolerance is the one passed to the library.</param>
        /// <param name="first">The index of the first item.</param>
        /// <param name="second">The index of the second item.</param>
        /// <returns>
        /// The answer. Where the library has no call between the two kinds, or refuses the shapes, the numbers are missing and a note says
        /// why; this never throws for that. A call is looked for with the first item as the subject and then with the second, and
        /// the segment of the second is turned round so that it always starts on the first item.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when the scene is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when an index is not an item of the scene.</exception>
        public static LibraryDistance Between(Scene scene, int first, int second)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (first < 0 || first >= scene.Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(first));
            }

            if (second < 0 || second >= scene.Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(second));
            }

            SceneItem a = scene.Items[first];
            SceneItem b = scene.Items[second];
            var result = new LibraryDistance(Describe(a), Describe(b));

            object shapeA;
            object shapeB;
            Tolerance tolerance = scene.Tolerance;

            try
            {
                shapeA = scene.Rebuild(a, out string noteA);
                shapeB = scene.Rebuild(b, out string noteB);

                if (noteA != null || noteB != null)
                {
                    tolerance = Scene.MinimalTolerance;
                    result.Notes.Add("a shape was rebuilt at 1E-12 and measured at it, since the library refused it at the tolerance of the scene");
                }
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException || e is FormatException)
            {
                result.Notes.Add("the library cannot rebuild the shape: " + e.Message);
                return result;
            }

            if (shapeA is Array || shapeB is Array)
            {
                result.Notes.Add("a mesh is a set of polygons and has no one shape to measure; measure its polygons");
                return result;
            }

            using (Tolerance.Use(tolerance))
            {
                object distance = Call("DistanceTo", shapeA, shapeB, tolerance, out _, result.Notes);

                if (distance is double d)
                {
                    result.Distance = d;
                }

                object line = Call("GetShortestLineTo", shapeA, shapeB, tolerance, out bool reversedLine, result.Notes);

                if (line is GeoLine2 l2)
                {
                    SetLine(result, new Vec3(l2.StartPoint.X, l2.StartPoint.Y, 0.0), new Vec3(l2.EndPoint.X, l2.EndPoint.Y, 0.0), reversedLine);
                }
                else if (line is GeoLine3 l3)
                {
                    SetLine(result, new Vec3(l3.StartPoint.X, l3.StartPoint.Y, l3.StartPoint.Z), new Vec3(l3.EndPoint.X, l3.EndPoint.Y, l3.EndPoint.Z), reversedLine);
                }
            }

            return result;
        }

        private static void SetLine(LibraryDistance result, Vec3 start, Vec3 end, bool reversed)
        {
            result.From = reversed ? end : start;
            result.To = reversed ? start : end;
        }

        private static string Describe(SceneItem item) => item.Kind + (string.IsNullOrEmpty(item.Name) ? string.Empty : " '" + item.Name + "'");

        /// <summary>
        /// Calls <c>name(other, tolerance)</c>, or <c>name(other)</c> under a scope of the tolerance where the library offers no overload with
        /// one, on the first shape and, where the library has none for the kinds, on the second with the first as the argument.
        /// </summary>
        private static object Call(string name, object first, object second, Tolerance tolerance, out bool reversed, List<string> notes)
        {
            reversed = false;
            MethodInfo method = Find(first.GetType(), name, second.GetType());
            object subject = first;
            object argument = second;

            if (method == null)
            {
                method = Find(second.GetType(), name, first.GetType());
                subject = second;
                argument = first;
                reversed = true;
            }

            if (method == null)
            {
                notes.Add("the library has no " + name + " between " + first.GetType().Name + " and " + second.GetType().Name);
                return null;
            }

            try
            {
                return method.Invoke(subject, method.GetParameters().Length == 2 ? new[] { argument, tolerance } : new[] { argument });
            }
            catch (TargetInvocationException e) when (e.InnerException != null)
            {
                notes.Add(name + " refused the shapes (" + e.InnerException.GetType().Name + ": " + e.InnerException.Message + ")");
                return null;
            }
        }

        /// <summary>
        /// Finds the public instance method of a type with the name that takes a shape of the other type, with a tolerance after it where
        /// the library has one and without where it has not; the most specific by the type it takes, and the first by its name where the
        /// library offers equals.
        /// </summary>
        private static MethodInfo Find(Type type, string name, Type other)
        {
            IEnumerable<MethodInfo> candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == name && m.GetParameters()[0].ParameterType.IsAssignableFrom(other));

            return candidates
                .Where(m => m.GetParameters().Length == 2 && m.GetParameters()[1].ParameterType == typeof(Tolerance))
                .Concat(candidates.Where(m => m.GetParameters().Length == 1))
                .OrderBy(m => m.GetParameters().Length == 2 ? 0 : 1)
                .ThenByDescending(m => m.GetParameters()[0].ParameterType == other)
                .ThenBy(m => m.GetParameters()[0].ParameterType.FullName, StringComparer.Ordinal)
                .FirstOrDefault();
        }
    }
}