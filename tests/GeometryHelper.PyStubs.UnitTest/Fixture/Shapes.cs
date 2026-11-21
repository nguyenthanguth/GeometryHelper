using System;
using System.Collections;
using System.Collections.Generic;

namespace GeometryHelper.Fixture
{
    /// <summary>A shape with a name.</summary>
    public interface IShape
    {
        /// <summary>Gets the name of the shape.</summary>
        string Name { get; }
    }

    public interface INamedShape : IShape { }

    public interface ILabelled { }

    /// <summary>The base of the shapes.</summary>
    public class Shape : IShape
    {
        /// <inheritdoc/>
        public string Name => "shape";
    }

    /// <inheritdoc/>
    public class Circle : Shape, INamedShape, ILabelled
    {
        /// <summary>Gets the radius of the circle.</summary>
        public double Radius { get; set; }
    }

    public class Plain { }

    public class Implementor : IShape
    {
        public string Name => "implementor";
    }

    public class Failure : Exception { }

    /// <summary>A point of the plane, a struct of two doubles.</summary>
    public readonly struct Spot
    {
        /// <summary>Makes a spot.</summary>
        public Spot(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double X { get; }
        public double Y { get; }

        /// <summary>Scales the spot by <paramref name="factor"/>.</summary>
        public Spot Scale(double factor) => new Spot(X * factor, Y * factor);

        public static Spot operator +(Spot a, Spot b) => new Spot(a.X + b.X, a.Y + b.Y);

        public static Spot operator -(Spot a, Spot b) => new Spot(a.X - b.X, a.Y - b.Y);

        public static Spot operator -(Spot a) => new Spot(-a.X, -a.Y);

        public static Spot operator *(Spot a, double factor) => a.Scale(factor);

        public static Spot operator *(double factor, Spot a) => a.Scale(factor);

        public static bool operator <(Spot a, Spot b) => a.X < b.X;

        public static bool operator >(Spot a, Spot b) => a.X > b.X;

        public static bool operator ==(Spot a, Spot b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(Spot a, Spot b) => !(a == b);

        public override bool Equals(object obj) => obj is Spot s && this == s;

        public override int GetHashCode() => X.GetHashCode() ^ Y.GetHashCode();
    }

    /// <summary>Tests a spot.</summary>
    public delegate bool SpotTest(Spot spot, int index);

    public struct Bare
    {
        public int Value;
    }

    /// <summary>The kinds of something.</summary>
    public enum Kind
    {
        /// <summary>Nothing at all.</summary>
        None,

        /// <summary>A round one.</summary>
        Round,
        Square,
    }

    [Flags]
    public enum Mode
    {
        Off = 0,
        On = 1,
        Both = 3,
    }

    public class DataArgs : EventArgs { }

    /// <summary>A bag of spots, which can be walked through.</summary>
    public class Bag : IEnumerable<Spot>
    {
        public IEnumerator<Spot> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>A bag with a name, which is still a bag.</summary>
    public class NamedBag : Bag { }

    /// <summary>A bag that counts its spots, so it is a collection.</summary>
    public class Roster : IReadOnlyCollection<Spot>
    {
        public int Count => 0;

        public IEnumerator<Spot> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    public class SmallRoster : Roster { }

    public class Chain
    {
        /// <summary>Makes a chain through the spots.</summary>
        public Chain(params Spot[] spots) { }

        public int Combine(int first, params Spot[] spots) => first;
    }

    public static class Extras
    {
        /// <summary>Doubles a number.</summary>
        public static int Doubled(this int value) => value * 2;

        public static IEnumerable<T> Twice<T>(this IEnumerable<T> items) => items;
    }
}
