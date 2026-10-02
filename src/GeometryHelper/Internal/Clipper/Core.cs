using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GeometryHelper.Clipper
{
  /// <summary>
  /// A point of integer coordinates, the kind the engine clips: paths of doubles are rounded to these first.
  /// </summary>
  internal struct Point64
  {
    public long X;
    public long Y;

    /// <summary>
    /// A copy of the point.
    /// </summary>
    public Point64(Point64 pt)
    {
      X = pt.X;
      Y = pt.Y;
    }

    /// <summary>
    /// The point multiplied by the scale, each coordinate rounded half away from nought.
    /// </summary>
    public Point64(Point64 pt, double scale)
    {
      X = (long) Math.Round(pt.X * scale, MidpointRounding.AwayFromZero);
      Y = (long) Math.Round(pt.Y * scale, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The point at x and y.
    /// </summary>
    public Point64(long x, long y)
    {
      X = x;
      Y = y;
    }

    /// <summary>
    /// The point at x and y, each rounded half away from nought.
    /// </summary>
    public Point64(double x, double y)
    {
      X = (long) Math.Round(x, MidpointRounding.AwayFromZero);
      Y = (long) Math.Round(y, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The point of doubles, each coordinate rounded half away from nought.
    /// </summary>
    public Point64(PointD pt)
    {
      X = (long) Math.Round(pt.x, MidpointRounding.AwayFromZero);
      Y = (long) Math.Round(pt.y, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The point of doubles multiplied by the scale, each coordinate rounded half away from nought.
    /// </summary>
    public Point64(PointD pt, double scale)
    {
      X = (long) Math.Round(pt.x * scale, MidpointRounding.AwayFromZero);
      Y = (long) Math.Round(pt.y * scale, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether the two points have the same coordinates.
    /// </summary>
    public static bool operator ==(Point64 lhs, Point64 rhs)
    {
      return lhs.X == rhs.X && lhs.Y == rhs.Y;
    }

    /// <summary>
    /// Whether the two points differ in either coordinate.
    /// </summary>
    public static bool operator !=(Point64 lhs, Point64 rhs)
    {
      return lhs.X != rhs.X || lhs.Y != rhs.Y;
    }

    /// <summary>
    /// The two points added coordinate by coordinate.
    /// </summary>
    public static Point64 operator +(Point64 lhs, Point64 rhs)
    {
      return new Point64(lhs.X + rhs.X, lhs.Y + rhs.Y);
    }

    /// <summary>
    /// The second point taken from the first, coordinate by coordinate.
    /// </summary>
    public static Point64 operator -(Point64 lhs, Point64 rhs)
    {
      return new Point64(lhs.X - rhs.X, lhs.Y - rhs.Y);
    }

    /// <summary>
    /// The point as text: x, a comma, y, and a space after it.
    /// </summary>
    public readonly override string ToString()
    {
      return $"{X},{Y} ";
    }

    /// <summary>
    /// Whether the object is a point with the same coordinates.
    /// </summary>
    public readonly override bool Equals(object obj)
    {
      if (obj != null && obj is Point64 p)
        return this == p;
      return false;
    }

    /// <summary>
    /// A hash code made of the two coordinates.
    /// </summary>
    public readonly override int GetHashCode()
    {
      unchecked { return (X.GetHashCode() * 397) ^ Y.GetHashCode(); }
    }

  }

  /// <summary>
  /// A point of double coordinates. Two points are equal when they lie within 1E-12 of each other along each axis.
  /// </summary>
  internal struct PointD
  {
    public double x;
    public double y;

    /// <summary>
    /// A copy of the point.
    /// </summary>
    public PointD(PointD pt)
    {
      x = pt.x;
      y = pt.y;
    }

    /// <summary>
    /// The integer point as a point of doubles.
    /// </summary>
    public PointD(Point64 pt)
    {
      x = pt.X;
      y = pt.Y;
    }

    /// <summary>
    /// The integer point multiplied by the scale.
    /// </summary>
    public PointD(Point64 pt, double scale)
    {
      x = pt.X * scale;
      y = pt.Y * scale;
    }

    /// <summary>
    /// The point multiplied by the scale.
    /// </summary>
    public PointD(PointD pt, double scale)
    {
      x = pt.x * scale;
      y = pt.y * scale;
    }

    /// <summary>
    /// The point at x and y.
    /// </summary>
    public PointD(long x, long y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>
    /// The point at x and y.
    /// </summary>
    public PointD(double x, double y)
    {
      this.x = x;
      this.y = y;
    }

    /// <summary>
    /// The point as text, x and y with <c>precision</c> decimal places, two unless given, written in the current
    /// culture and joined by a comma.
    /// </summary>
    public readonly string ToString(int precision = 2)
    {
      return string.Format($"{{0:F{precision}}},{{1:F{precision}}}", x,y);
    }

    /// <summary>
    /// Whether the two points lie within 1E-12 of each other along X and along Y.
    /// </summary>
    public static bool operator ==(PointD lhs, PointD rhs)
    {
      return InternalClipper.IsAlmostZero(lhs.x - rhs.x) &&
        InternalClipper.IsAlmostZero(lhs.y - rhs.y);
    }

    /// <summary>
    /// Whether the two points lie further than 1E-12 apart along X or along Y.
    /// </summary>
    public static bool operator !=(PointD lhs, PointD rhs)
    {
      return !InternalClipper.IsAlmostZero(lhs.x - rhs.x) ||
        !InternalClipper.IsAlmostZero(lhs.y - rhs.y);
    }

    /// <summary>
    /// Whether the object is a point within 1E-12 of this one along each axis, as == says.
    /// </summary>
    public readonly override bool Equals(object obj)
    {
      if (obj != null && obj is PointD p)
        return this == p;
      return false;
    }

    /// <summary>
    /// Turns the point round the origin: both coordinates change sign.
    /// </summary>
    public void Negate() { x = -x; y = -y; }

    /// <summary>
    /// A hash code made of the two coordinates as they are: points that == calls equal, less than 1E-12 apart, may
    /// still hash differently.
    /// </summary>
    public readonly override int GetHashCode()
    {
      unchecked { return (x.GetHashCode() * 397) ^ y.GetHashCode(); }
    }

  }

  /// <summary>
  /// An axis-aligned rectangle of integers. Its top is its smallest Y and its bottom its largest, the engine's Y
  /// growing downwards.
  /// </summary>
  internal struct Rect64
  {
    public long left;
    public long top;
    public long right;
    public long bottom;

    /// <summary>
    /// The rectangle between the left, top, right and bottom edges.
    /// </summary>
    public Rect64(long l, long t, long r, long b)
    {
      left = l;
      top = t;
      right = r;
      bottom = b;
    }

    /// <summary>
    /// With isValid, the rectangle of no size at the origin. Without, the rectangle around nothing, its left and top at
    /// long.MaxValue and its right and bottom at long.MinValue, ready to grow into the bounds of the points it is
    /// given.
    /// </summary>
    public Rect64(bool isValid)
    {
      if (isValid)
      {
        left = 0; top = 0; right = 0; bottom = 0;
      }
      else
      {
        left = long.MaxValue; top = long.MaxValue;
        right = long.MinValue; bottom = long.MinValue;
      }
    }

    /// <summary>
    /// A copy of the rectangle.
    /// </summary>
    public Rect64(Rect64 rec)
    {
      left = rec.left;
      top = rec.top;
      right = rec.right;
      bottom = rec.bottom;
    }

    /// <summary>
    /// Its width, right less left; setting it moves the right edge.
    /// </summary>
    public long Width
    { readonly get => right - left;
      set => right = left + value;
    }

    /// <summary>
    /// Its height, bottom less top; setting it moves the bottom edge.
    /// </summary>
    public long Height
    { readonly get => bottom - top;
      set => bottom = top + value;
    }

    /// <summary>
    /// Whether it encloses nothing: its width or its height is nought or less.
    /// </summary>
    public readonly bool IsEmpty()
    {
      return bottom <= top || right <= left;
    }

    /// <summary>
    /// Whether it has been given bounds: its left is below long.MaxValue, where the rectangle around nothing has it.
    /// </summary>
    public readonly bool IsValid()
    {
      return left < long.MaxValue;
    }

    /// <summary>
    /// Its centre, the coordinates halved by integer division, toward nought.
    /// </summary>
    public readonly Point64 MidPoint()
    {
      return new Point64((left + right) /2, (top + bottom)/2);
    }

    /// <summary>
    /// Whether the point lies strictly inside it, not on an edge.
    /// </summary>
    public readonly bool Contains(Point64 pt)
    {
      return pt.X > left && pt.X < right &&
        pt.Y > top && pt.Y < bottom;
    }

    /// <summary>
    /// Whether the other rectangle lies inside it, the edges allowed to touch.
    /// </summary>
    public readonly bool Contains(Rect64 rec)
    {
      return rec.left >= left && rec.right <= right &&
        rec.top >= top && rec.bottom <= bottom;
    }

    /// <summary>
    /// Whether the two rectangles overlap or touch: an edge or a corner in common counts.
    /// </summary>
    public readonly bool Intersects(Rect64 rec)
    {
      return (Math.Max(left, rec.left) <= Math.Min(right, rec.right)) &&
        (Math.Max(top, rec.top) <= Math.Min(bottom, rec.bottom));
    }

    /// <summary>
    /// Its corners as a path: left top, right top, right bottom, left bottom, counter-clockwise with Y up.
    /// </summary>
    public readonly Path64 AsPath()
    {
      Path64 result = new Path64(4)
      {
        new Point64(left, top),
        new Point64(right, top),
        new Point64(right, bottom),
        new Point64(left, bottom)
      };
      return result;
    }

  }

  /// <summary>
  /// An axis-aligned rectangle of doubles. Its top is its smallest Y and its bottom its largest, as in Rect64.
  /// </summary>
  internal struct RectD
  {
    public double left;
    public double top;
    public double right;
    public double bottom;

    /// <summary>
    /// The rectangle between the left, top, right and bottom edges.
    /// </summary>
    public RectD(double l, double t, double r, double b)
    {
      left = l;
      top = t;
      right = r;
      bottom = b;
    }

    /// <summary>
    /// A copy of the rectangle.
    /// </summary>
    public RectD(RectD rec)
    {
      left = rec.left;
      top = rec.top;
      right = rec.right;
      bottom = rec.bottom;
    }

    /// <summary>
    /// With isValid, the rectangle of no size at the origin. Without, the rectangle around nothing, its left and top at
    /// double.MaxValue and its right and bottom at -double.MaxValue, ready to grow into the bounds of the points it is
    /// given.
    /// </summary>
    public RectD(bool isValid)
    {
      if (isValid)
      {
        left = 0; top = 0; right = 0; bottom = 0;
      }
      else
      {
        left = double.MaxValue; top = double.MaxValue;
        right = -double.MaxValue; bottom = -double.MaxValue;
      }
    }

    /// <summary>
    /// Its width, right less left; setting it moves the right edge.
    /// </summary>
    public double Width
    { readonly get => right - left;
      set => right = left + value;
    }

    /// <summary>
    /// Its height, bottom less top; setting it moves the bottom edge.
    /// </summary>
    public double Height
    { readonly get => bottom - top;
      set => bottom = top + value;
    }

    /// <summary>
    /// Whether it encloses nothing: its width or its height is nought or less.
    /// </summary>
    public readonly bool IsEmpty()
    {
      return bottom <= top || right <= left;
    }

    /// <summary>
    /// Its centre.
    /// </summary>
    public readonly PointD MidPoint()
    {
      return new PointD((left + right) / 2, (top + bottom) / 2);
    }

    /// <summary>
    /// Whether the point lies strictly inside it, not on an edge.
    /// </summary>
    public readonly bool Contains(PointD pt)
    {
      return pt.x > left && pt.x < right &&
        pt.y > top && pt.y < bottom;
    }

    /// <summary>
    /// Whether the other rectangle lies inside it, the edges allowed to touch.
    /// </summary>
    public readonly bool Contains(RectD rec)
    {
      return rec.left >= left && rec.right <= right &&
        rec.top >= top && rec.bottom <= bottom;
    }

    /// <summary>
    /// Whether the two rectangles overlap over some area. Unlike Rect64's, an edge or a corner in common does not
    /// count.
    /// </summary>
    public readonly bool Intersects(RectD rec)
    {
      return (Math.Max(left, rec.left) < Math.Min(right, rec.right)) &&
        (Math.Max(top, rec.top) < Math.Min(bottom, rec.bottom));
    }

    /// <summary>
    /// Its corners as a path: left top, right top, right bottom, left bottom, counter-clockwise with Y up.
    /// </summary>
    public readonly PathD AsPath()
    {
      PathD result = new PathD(4)
      {
        new PointD(left, top),
        new PointD(right, top),
        new PointD(right, bottom),
        new PointD(left, bottom)
      };
      return result;
    }

  }

  /// <summary>
  /// A path of integer points, read as a polygon when it is closed and as a line when it is open.
  /// </summary>
  internal class Path64 : List<Point64>
  {
    /// <summary>
    /// An empty path.
    /// </summary>
    public Path64() : base() { }

    /// <summary>
    /// An empty path with room for <c>capacity</c> points.
    /// </summary>
    public Path64(int capacity = 0) : base(capacity) { }

    /// <summary>
    /// A path of the points, copied.
    /// </summary>
    public Path64(IEnumerable<Point64> path) : base(path) { }

    /// <summary>
    /// The points as text, joined by a comma and a space.
    /// </summary>
    public override string ToString()
    {
      return string.Join(", ", this);
    }
  }

  /// <summary>
  /// Paths of integer points: the subjects, the clips or the answer of an operation.
  /// </summary>
  internal class Paths64 : List<Path64>
  {
    /// <summary>
    /// No paths.
    /// </summary>
    public Paths64() : base() { }

    /// <summary>
    /// No paths, with room for <c>capacity</c> of them.
    /// </summary>
    public Paths64(int capacity = 0) : base(capacity) { }

    /// <summary>
    /// A list of the paths themselves, not of copies of them.
    /// </summary>
    public Paths64(IEnumerable<Path64> paths) : base(paths) { }

    /// <summary>
    /// The paths as text, a line for each.
    /// </summary>
    public override string ToString()
    {
      return string.Join(Environment.NewLine, this);
    }
  }

  /// <summary>
  /// A path of points of doubles, read as a polygon when it is closed and as a line when it is open.
  /// </summary>
  internal class PathD : List<PointD>
  {
    /// <summary>
    /// An empty path.
    /// </summary>
    public PathD() : base() { }

    /// <summary>
    /// An empty path with room for <c>capacity</c> points.
    /// </summary>
    public PathD(int capacity = 0) : base(capacity) { }

    /// <summary>
    /// A path of the points, copied.
    /// </summary>
    public PathD(IEnumerable<PointD> path) : base(path) { }

    /// <summary>
    /// The points as text with <c>precision</c> decimal places, two unless given, joined by a comma and a space.
    /// </summary>
    public string ToString(int precision = 2)
    {
      return string.Join(", ", ConvertAll(x => x.ToString(precision)));
    }
  }

  /// <summary>
  /// Paths of points of doubles: the subjects, the clips or the answer of an operation.
  /// </summary>
  internal class PathsD : List<PathD>
  {
    /// <summary>
    /// No paths.
    /// </summary>
    public PathsD() : base() { }

    /// <summary>
    /// No paths, with room for <c>capacity</c> of them.
    /// </summary>
    public PathsD(int capacity = 0) : base(capacity) { }

    /// <summary>
    /// A list of the paths themselves, not of copies of them.
    /// </summary>
    public PathsD(IEnumerable<PathD> paths) : base(paths) { }

    /// <summary>
    /// The paths as text with <c>precision</c> decimal places, two unless given, a line for each.
    /// </summary>
    public string ToString(int precision = 2)
    {
      return string.Join(Environment.NewLine, ConvertAll(x => x.ToString(precision)));
    }
  }

  /// <summary>
  /// The operation: none, intersection, union, difference or exclusive or. All but Difference give the same answer with
  /// the subject and the clip swapped.
  /// </summary>
  internal enum ClipType
  {
    NoClip,
    Intersection,
    Union,
    Difference,
    Xor
  }

  /// <summary>
  /// Whether a path is a subject or a clip.
  /// </summary>
  internal enum PathType
  {
    Subject,
    Clip
  }

  /// <summary>
  /// Which parts the paths enclose, by the winding number of each point, the number of times the paths wind round it:
  /// EvenOdd fills where it is odd, NonZero where it is not nought, Positive where it is above nought and Negative
  /// where it is below. EvenOdd and NonZero are by far the most used, also called Alternate and Winding.
  /// </summary>
  internal enum FillRule
  {
    EvenOdd,
    NonZero,
    Positive,
    Negative
  }

  /// <summary>
  /// The arithmetic the engine stands on: turns and products of integer points, some of them exact, crossings of lines
  /// and segments, bounds, and where a point lies against a polygon.
  /// </summary>
  internal static class InternalClipper
  {
    internal const long MaxInt64 = 9223372036854775807;
    internal const long MaxCoord = MaxInt64 / 4;
    internal const double max_coord = MaxCoord;
    internal const double min_coord = -MaxCoord;
    internal const long Invalid64 = MaxInt64;

    internal const double floatingPointTolerance = 1E-12;
    internal const double defaultMinimumEdgeLength = 0.1;

    private static readonly string
      precision_range_error = "Error: Precision is out of range.";

    /// <summary>
    /// The cross product of the edge from pt1 to pt2 and the edge from pt2 to pt3, in doubles so that it cannot
    /// overflow: positive where the path turns left with Y up, negative where it turns right, nought where the points
    /// are collinear, as far as doubles tell. CrossProductSign gives its sign exactly.
    /// </summary>
    public static double CrossProduct(Point64 pt1, Point64 pt2, Point64 pt3)
    {
      // typecast to double to avoid potential int overflow
      return ((double) (pt2.X - pt1.X) * (pt3.Y - pt2.Y) -
              (double) (pt2.Y - pt1.Y) * (pt3.X - pt2.X));
    }

    /// <summary>
    /// The sign of CrossProduct, 1, -1 or 0, worked out exactly with products of 128 bits, so that it is right however
    /// near the points are to collinear.
    /// </summary>
    public static int CrossProductSign(Point64 pt1, Point64 pt2, Point64 pt3)
    {
      long a = pt2.X - pt1.X;
      long b = pt3.Y - pt2.Y;
      long c = pt2.Y - pt1.Y;
      long d = pt3.X - pt2.X;
      UInt128Struct ab = MultiplyUInt64((ulong) Math.Abs(a), (ulong) Math.Abs(b));
      UInt128Struct cd = MultiplyUInt64((ulong) Math.Abs(c), (ulong) Math.Abs(d));
      int signAB = TriSign(a) * TriSign(b);
      int signCD = TriSign(c) * TriSign(d);

      if (signAB == signCD)
      {
        int result;
        if (ab.hi64 == cd.hi64)
        {
          if (ab.lo64 == cd.lo64) return 0;
          result = (ab.lo64 > cd.lo64) ? 1 : -1;
        }
        else result = (ab.hi64 > cd.hi64) ? 1 : -1;
        return (signAB > 0) ? result : -result;
      }
      return (signAB > signCD) ? 1 : -1;
    }

    /// <summary>
    /// Throws for a precision outside -8 to 8, a plain Exception rather than the ClipperLibException of ClipperD's
    /// constructor.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CheckPrecision(int precision)
    {
      if (precision < -8 || precision > 8)
        throw new Exception(precision_range_error);
    }

    /// <summary>
    /// Whether the value lies within 1E-12 of nought.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsAlmostZero(double value)
    {
      return (Math.Abs(value) <= floatingPointTolerance);
    }

    /// <summary>
    /// The sign of the value: -1, 0 or 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int TriSign(long x)
    {
      return (x < 0) ? -1 : (x > 0) ? 1 : 0;
    }

    /// <summary>
    /// An unsigned number of 128 bits, as its low and its high 64 bits.
    /// </summary>
    public struct UInt128Struct
    {
      public ulong lo64;
      public ulong hi64;
    }

    /// <summary>
    /// The exact product of two unsigned numbers of 64 bits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UInt128Struct MultiplyUInt64(ulong a, ulong b) // #834,#835
    {
      ulong x1 = (a & 0xFFFFFFFF) * (b & 0xFFFFFFFF);
      ulong x2 = (a >> 32) * (b & 0xFFFFFFFF) + (x1 >> 32);
      ulong x3 = (a & 0xFFFFFFFF) * (b >> 32) + (x2 & 0xFFFFFFFF);
      UInt128Struct result;
      result.lo64 = (x3 & 0xFFFFFFFF) << 32 | (x1 & 0xFFFFFFFF);
      result.hi64 = (a >> 32) * (b >> 32) + (x2 >> 32) + (x3 >> 32);
      return result;
    }

    /// <summary>
    /// Whether a times b equals c times d, exactly: their magnitudes are multiplied to 128 bits and their signs
    /// compared apart.
    /// </summary>
    internal static bool ProductsAreEqual(long a, long b, long c, long d)
    {
      // nb: the magnitudes are multiplied unsigned, by MultiplyUInt64, and the signs compared apart
      ulong absA = (ulong) Math.Abs(a);
      ulong absB = (ulong) Math.Abs(b);
      ulong absC = (ulong) Math.Abs(c);
      ulong absD = (ulong) Math.Abs(d);

      UInt128Struct mul_ab = MultiplyUInt64(absA, absB);
      UInt128Struct mul_cd = MultiplyUInt64(absC, absD);

      // nb: it's important to differentiate 0 values here from other values
      int sign_ab = TriSign(a) * TriSign(b);
      int sign_cd = TriSign(c) * TriSign(d);

      return mul_ab.lo64 == mul_cd.lo64 && mul_ab.hi64 == mul_cd.hi64 && sign_ab == sign_cd;
    }

    /// <summary>
    /// Whether the three points lie on one line, tested exactly: the edges from pt1 to sharedPt and from sharedPt to
    /// pt2 have a cross product of nought. A path turning straight back at sharedPt counts as collinear.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsCollinear(Point64 pt1, Point64 sharedPt, Point64 pt2)
    {
      long a = sharedPt.X - pt1.X;
      long b = pt2.Y - sharedPt.Y;
      long c = sharedPt.Y - pt1.Y;
      long d = pt2.X - sharedPt.X;
      // When checking for collinearity with very large coordinate values
      // then ProductsAreEqual is more accurate than using CrossProduct.
      return ProductsAreEqual(a, b, c, d);
    }

    /// <summary>
    /// The dot product of the edge from pt1 to pt2 and the edge from pt2 to pt3, in doubles so that it cannot overflow:
    /// negative where the path turns back by more than a right angle.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double DotProduct(Point64 pt1, Point64 pt2, Point64 pt3)
    {
      // typecast to double to avoid potential int overflow
      return ((double) (pt2.X - pt1.X) * (pt3.X - pt2.X) +
              (double) (pt2.Y - pt1.Y) * (pt3.Y - pt2.Y));
    }

    /// <summary>
    /// The cross product of two vectors taken the other way round, <c>vec1.y * vec2.x - vec2.y * vec1.x</c>: positive
    /// when vec1 lies counter-clockwise of vec2 with Y up, the opposite sign to the cross product of three points.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double CrossProduct(PointD vec1, PointD vec2)
    {
      return (vec1.y * vec2.x - vec2.y * vec1.x);
    }

    /// <summary>
    /// The dot product of the two vectors.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static double DotProduct(PointD vec1, PointD vec2)
    {
      return (vec1.x * vec2.x + vec1.y * vec2.y);
    }

    /// <summary>
    /// The value rounded half away from nought to an integer, or Invalid64 when it lies at or beyond MaxCoord either
    /// side of nought.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static long CheckCastInt64(double val)
    {
      if ((val >= max_coord) || (val <= min_coord)) return Invalid64;
      return (long)Math.Round(val, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Where the line through ln1a and ln1b crosses the line through ln2a and ln2b; false, with ip at the origin, when
    /// they are parallel or either has no length. The point is kept on the first segment, an end of it taken when the
    /// crossing lies beyond, but it may lie outside the second segment. Between the ends it is cut down to integers,
    /// toward nought, not rounded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetLineIntersectPt(Point64 ln1a,
      Point64 ln1b, Point64 ln2a, Point64 ln2b, out Point64 ip)
    {
      double dy1 = (ln1b.Y - ln1a.Y);
      double dx1 = (ln1b.X - ln1a.X);
      double dy2 = (ln2b.Y - ln2a.Y);
      double dx2 = (ln2b.X - ln2a.X);
      double det = dy1 * dx2 - dy2 * dx1;
      if (det == 0.0)
      {
        ip = new Point64();
        return false;
      }

      double t = ((ln1a.X - ln2a.X) * dy2 - (ln1a.Y - ln2a.Y) * dx2) / det;
      if (t <= 0.0) ip = ln1a;
      else if (t >= 1.0) ip = ln1b;
      else
      {
        // avoid using constructor (and rounding too) as they affect performance //664
        ip.X = (long) (ln1a.X + t * dx1);
        ip.Y = (long) (ln1a.Y + t * dy1);
      }
      return true;
    }

    /// <summary>
    /// Where the line through ln1a and ln1b crosses the line through ln2a and ln2b; false, with ip at the origin, when
    /// they are parallel or either has no length. The point is kept on the first segment, an end of it taken when the
    /// crossing lies beyond, but it may lie outside the second segment.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetLineIntersectPt(PointD ln1a,
      PointD ln1b, PointD ln2a, PointD ln2b, out PointD ip)
    {
      double dy1 = (ln1b.y - ln1a.y);
      double dx1 = (ln1b.x - ln1a.x);
      double dy2 = (ln2b.y - ln2a.y);
      double dx2 = (ln2b.x - ln2a.x);
      double det = dy1 * dx2 - dy2 * dx1;
      if (det == 0.0)
      {
        ip = new PointD();
        return false;
      }

      double t = ((ln1a.x - ln2a.x) * dy2 - (ln1a.y - ln2a.y) * dx2) / det;
      if (t <= 0.0) ip = ln1a;
      else if (t >= 1.0) ip = ln1b;
      else
      {
        // avoid using constructor (and rounding too) as they affect performance //664
        ip.x = (ln1a.x + t * dx1);
        ip.y = (ln1a.y + t * dy1);
      }
      return true;
    }

    /// <summary>
    /// Whether the two segments cross. Parallel segments never do, those overlapping along one line included. Without
    /// inclusive, the only way the engine asks, segments that only touch, an end of one on the other, do not count;
    /// with inclusive they do. Clipper2 2.0.0, its C++ as well, counted with inclusive seg1a lying anywhere on the line
    /// through the second segment, even beyond its ends; that is mended here.
    /// </summary>
    internal static bool SegsIntersect(Point64 seg1a,
      Point64 seg1b, Point64 seg2a, Point64 seg2b, bool inclusive = false)
    {
      double dy1 = (seg1b.Y - seg1a.Y);
      double dx1 = (seg1b.X - seg1a.X);
      double dy2 = (seg2b.Y - seg2a.Y);
      double dx2 = (seg2b.X - seg2a.X);
      double cp = dy1 * dx2 - dy2 * dx1;
      if (cp == 0) return false; // ie parallel segments

      if (inclusive)
      {
        //result **includes** segments that touch at an end point
        double t = ((seg1a.X - seg2a.X) * dy2 - (seg1a.Y - seg2a.Y) * dx2);
        // t == 0 only puts seg1a on the line through seg2; the test along seg2 below says whether it is on seg2
        if (t > 0)
        {
          if (cp < 0 || t > cp) return false;
        }
        else if (t < 0 && (cp > 0 || t < cp)) return false; // false when t more neg. than cp

        t = ((seg1a.X - seg2a.X) * dy1 - (seg1a.Y - seg2a.Y) * dx1);
        if (t == 0) return true;
        if (t > 0) return (cp > 0 && t <= cp);
        else return (cp < 0 && t >= cp);        // true when t less neg. than cp
      }
      else
      {
        //result **excludes** segments that touch at an end point
        double t = ((seg1a.X - seg2a.X) * dy2 - (seg1a.Y - seg2a.Y) * dx2);
        if (t == 0) return false;
        if (t > 0)
        {
          if (cp < 0 || t >= cp) return false;
        }
        else if (cp > 0 || t <= cp) return false; // false when t more neg. than cp

        t = ((seg1a.X - seg2a.X) * dy1 - (seg1a.Y - seg2a.Y) * dx1);
        if (t == 0) return false;
        if (t > 0) return (cp > 0 && t < cp);
        else return (cp < 0 && t > cp); // true when t less neg. than cp
      }
    }

    /// <summary>
    /// The smallest axis-aligned rectangle holding every point of the path; the rectangle of no size at the origin for
    /// a path with no points.
    /// </summary>
    public static Rect64 GetBounds(Path64 path)
    {
      if (path.Count == 0) return new Rect64();
      Rect64 result = Clipper2.InvalidRect64;
      foreach (Point64 pt in path)
      {
        if (pt.X < result.left) result.left = pt.X;
        if (pt.X > result.right) result.right = pt.X;
        if (pt.Y < result.top) result.top = pt.Y;
        if (pt.Y > result.bottom) result.bottom = pt.Y;
      }
      return result;
    }

    /// <summary>
    /// The point of the segment from seg1 to seg2 nearest offPt, rounded half to even as nearbyint rounds in Clipper2's
    /// C++; seg1 when the segment has no length.
    /// </summary>
    public static Point64 GetClosestPtOnSegment(Point64 offPt,
      Point64 seg1, Point64 seg2)
    {
      if (seg1.X == seg2.X && seg1.Y == seg2.Y) return seg1;
      double dx = (seg2.X - seg1.X);
      double dy = (seg2.Y - seg1.Y);
      double q = ((offPt.X - seg1.X) * dx +
        (offPt.Y - seg1.Y) * dy) / ((dx*dx) + (dy*dy));
      if (q < 0) q = 0; else if (q > 1) q = 1;
      return new Point64(
        // use MidpointRounding.ToEven in order to explicitly match the nearbyint behaviour on the C++ side
        seg1.X + Math.Round(q * dx, MidpointRounding.ToEven),
        seg1.Y + Math.Round(q * dy, MidpointRounding.ToEven)
      );
    }

    /// <summary>
    /// Where the point lies against the polygon, read by the even-odd rule: on its boundary, tested exactly, inside or
    /// outside. A polygon of fewer than three points, or one with every point at the Y of pt, has nothing inside.
    /// </summary>
    public static PointInPolygonResult PointInPolygon(Point64 pt, Path64 polygon)
    {
      int len = polygon.Count, start = 0;
      if (len < 3) return PointInPolygonResult.IsOutside;

      while (start < len && polygon[start].Y == pt.Y) start++;
      if (start == len) return PointInPolygonResult.IsOutside;

      bool isAbove = polygon[start].Y < pt.Y, startingAbove = isAbove;
      int val = 0, i = start + 1, end = len;
      while (true)
      {
        if (i == end)
        {
          if (end == 0 || start == 0) break;
          end = start;
          i = 0;
        }

        if (isAbove)
        {
          while (i < end && polygon[i].Y < pt.Y) i++;
        }
        else
        {
          while (i < end && polygon[i].Y > pt.Y) i++;
        }

        if (i == end) continue;

        Point64 curr = polygon[i], prev;
        if (i > 0) prev = polygon[i - 1];
        else prev = polygon[len - 1];

        if (curr.Y == pt.Y)
        {
          if (curr.X == pt.X || (curr.Y == prev.Y &&
            ((pt.X < prev.X) != (pt.X < curr.X))))
            return PointInPolygonResult.IsOn;
          i++;
          if (i == start) break;
          continue;
        }

        if (pt.X < curr.X && pt.X < prev.X)
        {
          // we're only interested in edges crossing on the left
        }
        else if (pt.X > prev.X && pt.X > curr.X)
        {
          val = 1 - val; // toggle val
        }
        else
        {
          int cps2 = CrossProductSign(prev, curr, pt);
          if (cps2 == 0) return PointInPolygonResult.IsOn;
          if ((cps2 < 0) == isAbove) val = 1 - val;
        }
        isAbove = !isAbove;
        i++;
      }

      if (isAbove == startingAbove) return val == 0 ? PointInPolygonResult.IsOutside : PointInPolygonResult.IsInside;
      if (i == len) i = 0;
      int cps = (i == 0) ?
        CrossProductSign(polygon[len - 1], polygon[0], pt) :
        CrossProductSign(polygon[i - 1], polygon[i], pt);

      if (cps == 0) return PointInPolygonResult.IsOn;
      if ((cps < 0) == isAbove) val = 1 - val;
      return val == 0 ? PointInPolygonResult.IsOutside : PointInPolygonResult.IsInside;
    }

    /// <summary>
    /// Whether path1 lies inside path2. Two vertices of path1 in a row on the same side settle it, those on the
    /// boundary of path2 passed over, so that one misjudged through rounding does not; when that never happens, the
    /// centre of the bounds of path1 decides, on the boundary counting as inside.
    /// </summary>
    public static bool Path2ContainsPath1(Path64 path1, Path64 path2)
    {
      // we need to make some accommodation for rounding errors
      // so we won't jump if the first vertex is found outside
      PointInPolygonResult pip = PointInPolygonResult.IsOn;
      foreach (Point64 pt in path1)
      {
        switch (PointInPolygon(pt, path2))
        {
          case PointInPolygonResult.IsOutside:
            if (pip == PointInPolygonResult.IsOutside) return false;
            pip = PointInPolygonResult.IsOutside;
            break;
          case PointInPolygonResult.IsInside:
            if (pip == PointInPolygonResult.IsInside) return true;
            pip = PointInPolygonResult.IsInside;
            break;
          default: break;
        }
      }
      // since path1's location is still equivocal, check its midpoint
      Point64 mp = GetBounds(path1).MidPoint();
      return InternalClipper.PointInPolygon(mp, path2) != PointInPolygonResult.IsOutside;
    }

  } // InternalClipper

} // namespace
