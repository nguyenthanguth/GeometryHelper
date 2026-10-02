using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace GeometryHelper.Clipper
{

  /// <summary>
  /// The front of the clipping engine in the plane: booleans of regions read by a fill rule, areas, bounds, scaling and
  /// conversion between integer and floating-point paths, simplification and where a point lies. Paths of integers
  /// (Path64) are worked on as they are; paths of doubles (PathD) are rounded to a number of decimal places first,
  /// worked on as integers, and scaled back.
  /// </summary>
  internal static class Clipper2
  {
    private static Rect64 invalidRect64 = new Rect64(false);
    /// <summary>
    /// A rectangle around nothing, its left and top at long.MaxValue and its right and bottom at long.MinValue, so that
    /// the first point it grows by becomes its bounds.
    /// </summary>
    public static Rect64 InvalidRect64 => invalidRect64;

    private static RectD invalidRectD = new RectD(false);
    /// <summary>
    /// A rectangle around nothing, its left and top at double.MaxValue and its right and bottom at -double.MaxValue, so
    /// that the first point it grows by becomes its bounds.
    /// </summary>
    public static RectD InvalidRectD => invalidRectD;

    /// <summary>
    /// The region the subject and the clip both cover, each read by the fill rule.
    /// </summary>
    public static Paths64 Intersect(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Intersection, subject, clip, fillRule);
    }

    /// <summary>
    /// The region the subject and the clip both cover, each read by the fill rule, with the coordinates rounded to
    /// <c>precision</c> decimal places, from -8 to 8, the answer scaled back.
    /// </summary>
    public static PathsD Intersect(PathsD subject, PathsD clip, 
      FillRule fillRule, int precision = 2)
    {
      return BooleanOp(ClipType.Intersection,
        subject, clip, fillRule, precision);
    }

    /// <summary>
    /// The region the paths cover, read by the fill rule, as paths that neither cross nor overlap.
    /// </summary>
    public static Paths64 Union(Paths64 subject, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, subject, null, fillRule);
    }

    /// <summary>
    /// The region the subject or the clip covers, each read by the fill rule.
    /// </summary>
    public static Paths64 Union(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, subject, clip, fillRule);
    }

    /// <summary>
    /// The region the paths cover, read by the fill rule, as paths that neither cross nor overlap, with the coordinates
    /// rounded to two decimal places, the answer scaled back.
    /// </summary>
    public static PathsD Union(PathsD subject, FillRule fillRule)
    {
      return BooleanOp(ClipType.Union, subject, null, fillRule);
    }

    /// <summary>
    /// The region the subject or the clip covers, each read by the fill rule, with the coordinates rounded to
    /// <c>precision</c> decimal places, from -8 to 8, the answer scaled back.
    /// </summary>
    public static PathsD Union(PathsD subject, PathsD clip, 
      FillRule fillRule, int precision = 2)
    {
      return BooleanOp(ClipType.Union,
        subject, clip, fillRule, precision);
    }

    /// <summary>
    /// The part of the subject's region outside the clip's, each read by the fill rule.
    /// </summary>
    public static Paths64 Difference(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Difference, subject, clip, fillRule);
    }

    /// <summary>
    /// The part of the subject's region outside the clip's, each read by the fill rule, with the coordinates rounded to
    /// <c>precision</c> decimal places, from -8 to 8, the answer scaled back.
    /// </summary>
    public static PathsD Difference(PathsD subject, PathsD clip, 
      FillRule fillRule, int precision = 2)
    {
      return BooleanOp(ClipType.Difference,
        subject, clip, fillRule, precision);
    }

    /// <summary>
    /// The parts that only one of the subject and the clip covers, each read by the fill rule.
    /// </summary>
    public static Paths64 Xor(Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      return BooleanOp(ClipType.Xor, subject, clip, fillRule);
    }

    /// <summary>
    /// The parts that only one of the subject and the clip covers, each read by the fill rule, with the coordinates
    /// rounded to <c>precision</c> decimal places, from -8 to 8, the answer scaled back.
    /// </summary>
    public static PathsD Xor(PathsD subject, PathsD clip, 
      FillRule fillRule, int precision = 2)
    {
      return BooleanOp(ClipType.Xor, 
        subject, clip, fillRule, precision);
    }

    /// <summary>
    /// Runs the operation on integer paths and gives the closed paths of the answer, outers counter-clockwise with Y up
    /// and holes clockwise. Nothing comes back without a subject; the clip may be missing.
    /// </summary>
    public static Paths64 BooleanOp(ClipType clipType,
      Paths64 subject, Paths64 clip, FillRule fillRule)
    {
      Paths64 solution = new Paths64();
      if (subject == null) return solution;
      Clipper64 c = new Clipper64();
      c.AddPaths(subject, PathType.Subject);
      if (clip != null)
        c.AddPaths(clip, PathType.Clip);
      c.Execute(clipType, fillRule, solution);
      return solution;
    }

    /// <summary>
    /// Runs the operation on integer paths into a tree, each hole nested in the outer it lies in and each island in its
    /// hole. Nothing is added without a subject; the clip may be missing.
    /// </summary>
    public static void BooleanOp(ClipType clipType,
      Paths64 subject, Paths64 clip, 
      PolyTree64 polytree, FillRule fillRule)
    {
      if (subject == null) return;
      Clipper64 c = new Clipper64();
      c.AddPaths(subject, PathType.Subject);
      if (clip != null)
        c.AddPaths(clip, PathType.Clip);
      c.Execute(clipType, fillRule, polytree);
    }

    /// <summary>
    /// Runs the operation and gives the closed paths of the answer, outers counter-clockwise with Y up and holes
    /// clockwise, with the coordinates rounded to <c>precision</c> decimal places, from -8 to 8, the answer scaled
    /// back. The clip may be missing.
    /// </summary>
    public static PathsD BooleanOp(ClipType clipType, PathsD subject, PathsD clip, 
      FillRule fillRule, int precision = 2)
    {
      PathsD solution = new PathsD();
      ClipperD c = new ClipperD(precision);
      c.AddSubject(subject);
      if (clip != null)
        c.AddClip(clip);
      c.Execute(clipType, fillRule, solution);
      return solution;
    }

    /// <summary>
    /// Runs the operation into a tree, each hole nested in the outer it lies in and each island in its hole, with the
    /// coordinates rounded to <c>precision</c> decimal places, from -8 to 8, the answer scaled back. Nothing is added
    /// without a subject; the clip may be missing.
    /// </summary>
    public static void BooleanOp(ClipType clipType,
      PathsD subject, PathsD clip,
      PolyTreeD polytree, FillRule fillRule, int precision = 2)
    {
      if (subject == null) return;
      ClipperD c = new ClipperD(precision);
      c.AddPaths(subject, PathType.Subject);
      if (clip != null)
        c.AddPaths(clip, PathType.Clip);
      c.Execute(clipType, fillRule, polytree);
    }

    /// <summary>
    /// The signed area of a closed path by the shoelace formula: positive when it runs counter-clockwise with Y up,
    /// negative when clockwise, nought with fewer than three points.
    /// </summary>
    public static double Area(Path64 path)
    {
      // https://en.wikipedia.org/wiki/Shoelace_formula
      double a = 0.0;
      int cnt = path.Count;
      if (cnt < 3) return 0.0;
      Point64 prevPt = path[cnt - 1];
      foreach (Point64 pt in path)
      {
        a += (double) (prevPt.Y + pt.Y) * (prevPt.X - pt.X);
        prevPt = pt;
      }
      return a * 0.5;
    }

    /// <summary>
    /// The sum of the signed areas of the paths, so that holes, wound the other way, count against their outers.
    /// </summary>
    public static double Area(Paths64 paths)
    {
      double a = 0.0;
      foreach (Path64 path in paths)
        a += Area(path);
      return a;
    }

    /// <summary>
    /// The signed area of a closed path by the shoelace formula: positive when it runs counter-clockwise with Y up,
    /// negative when clockwise, nought with fewer than three points.
    /// </summary>
    public static double Area(PathD path)
    {
      double a = 0.0;
      int cnt = path.Count;
      if (cnt < 3) return 0.0;
      PointD prevPt = path[cnt - 1];
      foreach (PointD pt in path)
      {
        a += (prevPt.y + pt.y) * (prevPt.x - pt.x);
        prevPt = pt;
      }
      return a * 0.5;
    }

    /// <summary>
    /// The sum of the signed areas of the paths, so that holes, wound the other way, count against their outers.
    /// </summary>
    public static double Area(PathsD paths)
    {
      double a = 0.0;
      foreach (PathD path in paths)
        a += Area(path);
      return a;
    }

    /// <summary>
    /// Whether the signed area of the path is nought or more: it runs counter-clockwise with Y up, or encloses nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPositive(Path64 poly)
    {
      return Area(poly) >= 0;
    }

    /// <summary>
    /// Whether the signed area of the path is nought or more: it runs counter-clockwise with Y up, or encloses nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsPositive(PathD poly)
    {
      return Area(poly) >= 0;
    }

    /// <summary>
    /// The points of the path as text, each as x,y and a space, and a line break at the end.
    /// </summary>
    public static string Path64ToString(Path64 path)
    {
      string result = "";
      foreach (Point64 pt in path)
        result += pt.ToString();
      return result + '\n';
    }
    /// <summary>
    /// The paths as text, a line for each.
    /// </summary>
    public static string Paths64ToString(Paths64 paths)
    {
      string result = "";
      foreach (Path64 path in paths)
        result += Path64ToString(path);
      return result;
    }
    /// <summary>
    /// The points of the path as text, each as x,y and a space, and a line break at the end.
    /// </summary>
    public static string PathDToString(PathD path)
    {
      string result = "";
      foreach (PointD pt in path)
        result += pt.ToString();
      return result + '\n';
    }
    /// <summary>
    /// The paths as text, a line for each.
    /// </summary>
    public static string PathsDToString(PathsD paths)
    {
      string result = "";
      foreach (PathD path in paths)
        result += PathDToString(path);
      return result;
    }
    /// <summary>
    /// A copy of the path moved by dx along X and dy along Y, as TranslatePath moves it.
    /// </summary>
    public static Path64 OffsetPath(Path64 path, long dx, long dy)
    {
      Path64 result = new Path64(path.Count);
      foreach (Point64 pt in path)
        result.Add(new Point64(pt.X + dx, pt.Y + dy));
      return result;
    }

    /// <summary>
    /// The point with its coordinates multiplied by the scale and rounded half away from nought.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point64 ScalePoint64(Point64 pt, double scale)
    {
      Point64 result = new Point64()
      {
        X = (long) Math.Round(pt.X * scale, MidpointRounding.AwayFromZero),
        Y = (long) Math.Round(pt.Y * scale, MidpointRounding.AwayFromZero),
      };
      return result;
    }

    /// <summary>
    /// The integer point multiplied by the scale, as a point of doubles.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PointD ScalePointD(Point64 pt, double scale)
    {
      PointD result = new PointD()
      {
        x = pt.X * scale,
        y = pt.Y * scale,
      };
      return result;
    }


    /// <summary>
    /// The rectangle multiplied by the scale, its edges cut down to integers toward nought rather than rounded.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Rect64 ScaleRect(RectD rec, double scale)
    {
      Rect64 result = new Rect64()
      {
        left = (long) (rec.left * scale),
        top = (long) (rec.top * scale),
        right = (long) (rec.right * scale),
        bottom = (long) (rec.bottom * scale)
      };
      return result;
    }

    /// <summary>
    /// The path multiplied by the scale, each point rounded half away from nought. A scale of one gives back the same
    /// path, not a copy.
    /// </summary>
    public static Path64 ScalePath(Path64 path, double scale)
    {
      if (InternalClipper.IsAlmostZero(scale - 1)) return path;
      Path64 result = new Path64(path.Count);
      foreach (Point64 pt in path)
        result.Add(new Point64(pt.X * scale, pt.Y * scale));
      return result;
    }

    /// <summary>
    /// Each path multiplied by the scale, each point rounded half away from nought. A scale of one gives back the same
    /// paths, not a copy.
    /// </summary>
    public static Paths64 ScalePaths(Paths64 paths, double scale)
    {
      if (InternalClipper.IsAlmostZero(scale - 1)) return paths;
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(ScalePath(path, scale));
      return result;
    }

    /// <summary>
    /// The path multiplied by the scale. A scale of one gives back the same path, not a copy.
    /// </summary>
    public static PathD ScalePath(PathD path, double scale)
    {
      if (InternalClipper.IsAlmostZero(scale - 1)) return path;
      PathD result = new PathD(path.Count);
      foreach (PointD pt in path)
        result.Add(new PointD(pt, scale));
      return result;
    }

    /// <summary>
    /// Each path multiplied by the scale. A scale of one gives back the same paths, not a copy.
    /// </summary>
    public static PathsD ScalePaths(PathsD paths, double scale)
    {
      if (InternalClipper.IsAlmostZero(scale - 1)) return paths;
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(ScalePath(path, scale));
      return result;
    }

    /// <summary>
    /// A path of doubles made a path of integers: each point multiplied by the scale and rounded half away from nought.
    /// This is the way a path of doubles reaches the engine.
    /// </summary>
    public static Path64 ScalePath64(PathD path, double scale)
    {
      int cnt = path.Count;
      Path64 res = new Path64(cnt);
      foreach (PointD pt in path)
        res.Add(new Point64(pt, scale));
      return res;
    }

    /// <summary>
    /// Paths of doubles made paths of integers: each point multiplied by the scale and rounded half away from nought.
    /// </summary>
    public static Paths64 ScalePaths64(PathsD paths, double scale)
    {
      int cnt = paths.Count;
      Paths64 res = new Paths64(cnt);
      foreach (PathD path in paths)
        res.Add(ScalePath64(path, scale));
      return res;
    }

    /// <summary>
    /// A path of integers made a path of doubles, each point multiplied by the scale. This is the way the engine's
    /// answer comes back as doubles.
    /// </summary>
    public static PathD ScalePathD(Path64 path, double scale)
    {
      int cnt = path.Count;
      PathD res = new PathD(cnt);
      foreach (Point64 pt in path)
        res.Add(new PointD(pt, scale));
      return res;
    }

    /// <summary>
    /// Paths of integers made paths of doubles, each point multiplied by the scale.
    /// </summary>
    public static PathsD ScalePathsD(Paths64 paths, double scale)
    {
      int cnt = paths.Count;
      PathsD res = new PathsD(cnt);
      foreach (Path64 path in paths)
        res.Add(ScalePathD(path, scale));
      return res;
    }

    /// <summary>
    /// A path of doubles made a path of integers without scaling, each coordinate rounded half away from nought.
    /// </summary>
    public static Path64 Path64(PathD path)
    {
      Path64 result = new Path64(path.Count);
      foreach (PointD pt in path)
        result.Add(new Point64(pt));
      return result;
    }

    /// <summary>
    /// Paths of doubles made paths of integers without scaling, each coordinate rounded half away from nought.
    /// </summary>
    public static Paths64 Paths64(PathsD paths)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (PathD path in paths)
        result.Add(Path64(path));
      return result;
    }

    /// <summary>
    /// Paths of integers made paths of doubles without scaling.
    /// </summary>
    public static PathsD PathsD(Paths64 paths)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (Path64 path in paths)
        result.Add(PathD(path));
      return result;
    }

    /// <summary>
    /// A path of integers made a path of doubles without scaling.
    /// </summary>
    public static PathD PathD(Path64 path)
    {
      PathD result = new PathD(path.Count);
      foreach (Point64 pt in path)
        result.Add(new PointD(pt));
      return result;
    }

    /// <summary>
    /// A copy of the path moved by dx along X and dy along Y.
    /// </summary>
    public static Path64 TranslatePath(Path64 path, long dx, long dy)
    {
      Path64 result = new Path64(path.Count);
      foreach (Point64 pt in path)
        result.Add(new Point64(pt.X + dx, pt.Y + dy));
      return result;
    }

    /// <summary>
    /// Copies of the paths, each moved by dx along X and dy along Y.
    /// </summary>
    public static Paths64 TranslatePaths(Paths64 paths, long dx, long dy)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(OffsetPath(path, dx, dy));
      return result;
    }

    /// <summary>
    /// A copy of the path moved by dx along X and dy along Y.
    /// </summary>
    public static PathD TranslatePath(PathD path, double dx, double dy)
    {
      PathD result = new PathD(path.Count);
      foreach (PointD pt in path)
        result.Add(new PointD(pt.x + dx, pt.y + dy));
      return result;
    }

    /// <summary>
    /// Copies of the paths, each moved by dx along X and dy along Y.
    /// </summary>
    public static PathsD TranslatePaths(PathsD paths, double dx, double dy)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(TranslatePath(path, dx, dy));
      return result;
    }

    /// <summary>
    /// A copy of the path with its points in the opposite order, which turns its winding round.
    /// </summary>
    public static Path64 ReversePath(Path64 path)
    {
      Path64 result = new Path64(path);
      result.Reverse();
      return result;
    }

    /// <summary>
    /// A copy of the path with its points in the opposite order, which turns its winding round.
    /// </summary>
    public static PathD ReversePath(PathD path)
    {
      PathD result = new PathD(path);
      result.Reverse();
      return result;
    }

    /// <summary>
    /// Copies of the paths, each with its points in the opposite order.
    /// </summary>
    public static Paths64 ReversePaths(Paths64 paths)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 t in paths)
        result.Add(ReversePath(t));

      return result;
    }

    /// <summary>
    /// Copies of the paths, each with its points in the opposite order.
    /// </summary>
    public static PathsD ReversePaths(PathsD paths)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(ReversePath(path));
      return result;
    }

    /// <summary>
    /// The smallest axis-aligned rectangle holding every point of the path, its top the smallest Y and its bottom the
    /// largest. A path with no points gives the empty rectangle at the origin.
    /// </summary>
    public static Rect64 GetBounds(Path64 path)
    {
      Rect64 result = InvalidRect64;
      foreach (Point64 pt in path)
      {
        if (pt.X < result.left) result.left = pt.X;
        if (pt.X > result.right) result.right = pt.X;
        if (pt.Y < result.top) result.top = pt.Y;
        if (pt.Y > result.bottom) result.bottom = pt.Y;
      }
      return result.left == long.MaxValue ? new Rect64() : result;
    }

    /// <summary>
    /// The smallest axis-aligned rectangle holding every point of the paths, its top the smallest Y and its bottom the
    /// largest. Paths with no points give the empty rectangle at the origin.
    /// </summary>
    public static Rect64 GetBounds(Paths64 paths)
    {
      Rect64 result = InvalidRect64;
      foreach (Path64 path in paths)
        foreach (Point64 pt in path)
        {
          if (pt.X < result.left) result.left = pt.X;
          if (pt.X > result.right) result.right = pt.X;
          if (pt.Y < result.top) result.top = pt.Y;
          if (pt.Y > result.bottom) result.bottom = pt.Y;
        }
      return result.left == long.MaxValue ? new Rect64() : result;
    }

    /// <summary>
    /// The smallest axis-aligned rectangle holding every point of the path, its top the smallest Y and its bottom the
    /// largest. A path with no points gives the empty rectangle at the origin.
    /// </summary>
    public static RectD GetBounds(PathD path)
    {
      RectD result = InvalidRectD;
      foreach (PointD pt in path)
      {
        if (pt.x < result.left) result.left = pt.x;
        if (pt.x > result.right) result.right = pt.x;
        if (pt.y < result.top) result.top = pt.y;
        if (pt.y > result.bottom) result.bottom = pt.y;
      }
      return Math.Abs(result.left - double.MaxValue) < InternalClipper.floatingPointTolerance ? new RectD() : result;
    }

    /// <summary>
    /// The smallest axis-aligned rectangle holding every point of the paths, its top the smallest Y and its bottom the
    /// largest. Paths with no points give the empty rectangle at the origin.
    /// </summary>
    public static RectD GetBounds(PathsD paths)
    {
      RectD result = InvalidRectD;
      foreach (PathD path in paths)
        foreach (PointD pt in path)
        {
          if (pt.x < result.left) result.left = pt.x;
          if (pt.x > result.right) result.right = pt.x;
          if (pt.y < result.top) result.top = pt.y;
          if (pt.y > result.bottom) result.bottom = pt.y;
        }
      return Math.Abs(result.left - double.MaxValue) < InternalClipper.floatingPointTolerance ? new RectD() : result;
    }

    /// <summary>
    /// A path from coordinates given in turn, x and y of the first point, then of the second, and so on. A last value
    /// without its pair is left out.
    /// </summary>
    public static Path64 MakePath(int[] arr)
    {
      int len = arr.Length / 2;
      Path64 p = new Path64(len);
      for (int i = 0; i < len; i++)
        p.Add(new Point64(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }

    /// <summary>
    /// A path from coordinates given in turn, x and y of the first point, then of the second, and so on. A last value
    /// without its pair is left out.
    /// </summary>
    public static Path64 MakePath(long[] arr)
    {
      int len = arr.Length / 2;
      Path64 p = new Path64(len);
      for (int i = 0; i < len; i++)
        p.Add(new Point64(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }

    /// <summary>
    /// A path from coordinates given in turn, x and y of the first point, then of the second, and so on. A last value
    /// without its pair is left out.
    /// </summary>
    public static PathD MakePath(double[] arr)
    {
      int len = arr.Length / 2;
      PathD p = new PathD(len);
      for (int i = 0; i < len; i++)
        p.Add(new PointD(arr[i * 2], arr[i * 2 + 1]));
      return p;
    }


    /// <summary>
    /// The value times itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqr(double val)
    {
      return val * val;
    }

    /// <summary>
    /// The value times itself, worked out in doubles so that it cannot overflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Sqr(long val)
    {
      return (double) val * (double) val;
    }

    /// <summary>
    /// The square of the distance between two points.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DistanceSqr(Point64 pt1, Point64 pt2)
    {
      return Sqr(pt1.X - pt2.X) + Sqr(pt1.Y - pt2.Y);
    }

    /// <summary>
    /// The point halfway between two points, its coordinates halved by integer division, toward nought.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point64 MidPoint(Point64 pt1, Point64 pt2)
    {
      return new Point64((pt1.X + pt2.X) / 2, (pt1.Y + pt2.Y) / 2);
    }

    /// <summary>
    /// The point halfway between two points.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PointD MidPoint(PointD pt1, PointD pt2)
    {
      return new PointD((pt1.x + pt2.x) / 2, (pt1.y + pt2.y) / 2);
    }

    /// <summary>
    /// Grows the rectangle by dx on its left and on its right and by dy on its top and on its bottom; negative values
    /// shrink it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InflateRect(ref Rect64 rec, int dx, int dy)
    {
      rec.left -= dx;
      rec.right += dx;
      rec.top -= dy;
      rec.bottom += dy;
    }

    /// <summary>
    /// Grows the rectangle by dx on its left and on its right and by dy on its top and on its bottom; negative values
    /// shrink it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void InflateRect(ref RectD rec, double dx, double dy)
    {
      rec.left -= dx;
      rec.right += dx;
      rec.top -= dy;
      rec.bottom += dy;
    }

    /// <summary>
    /// Whether two points lie nearer each other than the square root of distanceSqrd.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointsNearEqual(PointD pt1, PointD pt2, double distanceSqrd)
    {
      return Sqr(pt1.x - pt2.x) + Sqr(pt1.y - pt2.y) < distanceSqrd;
    }

    /// <summary>
    /// A copy of the path without the points that lie nearer than the square root of minEdgeLenSqrd to the point kept
    /// before them. Of a closed path, the last point goes too when it lies that near the first.
    /// </summary>
    public static PathD StripNearDuplicates(PathD path,
        double minEdgeLenSqrd, bool isClosedPath)
    {
      int cnt = path.Count;
      PathD result = new PathD(cnt);
      if (cnt == 0) return result;
      PointD lastPt = path[0];
      result.Add(lastPt);
      for (int i = 1; i < cnt; i++)
        if (!PointsNearEqual(lastPt, path[i], minEdgeLenSqrd))
        {
          lastPt = path[i];
          result.Add(lastPt);
        }

      if (isClosedPath && PointsNearEqual(lastPt, result[0], minEdgeLenSqrd))
      {
        result.RemoveAt(result.Count - 1);
      }

      return result;
    }

    /// <summary>
    /// A copy of the path without the points equal to the point before them. Of a closed path, the last point goes too
    /// when it equals the first.
    /// </summary>
    public static Path64 StripDuplicates(Path64 path, bool isClosedPath)
    {
      int cnt = path.Count;
      Path64 result = new Path64(cnt);
      if (cnt == 0) return result;
      Point64 lastPt = path[0];
      result.Add(lastPt);
      for (int i = 1; i < cnt; i++)
        if (lastPt != path[i])
        {
          lastPt = path[i];
          result.Add(lastPt);
        }
      if (isClosedPath && lastPt == result[0])
        result.RemoveAt(result.Count - 1);
      return result;
    }

    /// <summary>
    /// Adds the polygon of a node of the tree, when it has one, and then those of all the nodes below it, to the paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddPolyNodeToPaths(PolyPath64 polyPath, Paths64 paths)
    {
      if (polyPath.Polygon.Count > 0)
        paths.Add(polyPath.Polygon);
      for (int i = 0; i < polyPath.Count; i++)
        AddPolyNodeToPaths((PolyPath64) polyPath._childs[i], paths);
    }

    /// <summary>
    /// The polygons of a tree as a flat list of paths, each outer before the holes in it and each hole before the
    /// islands in it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Paths64 PolyTreeToPaths64(PolyTree64 polyTree)
    {
      Paths64 result = new Paths64();
      for (int i = 0; i < polyTree.Count; i++)
        AddPolyNodeToPaths((PolyPath64) polyTree._childs[i], result);
      return result;
    }

    /// <summary>
    /// Adds the polygon of a node of the tree, when it has one, and then those of all the nodes below it, to the paths.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddPolyNodeToPathsD(PolyPathD polyPath, PathsD paths)
    {
      if (polyPath.Polygon.Count > 0)
        paths.Add(polyPath.Polygon);
      for (int i = 0; i < polyPath.Count; i++)
        AddPolyNodeToPathsD((PolyPathD) polyPath._childs[i], paths);
    }

    /// <summary>
    /// The polygons of a tree as a flat list of paths, each outer before the holes in it and each hole before the
    /// islands in it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PathsD PolyTreeToPathsD(PolyTreeD polyTree)
    {
      PathsD result = new PathsD();
      foreach (PolyPathD polyPathBase in polyTree)
      {
        PolyPathD p = (PolyPathD)polyPathBase;
        AddPolyNodeToPathsD(p, result);
      }

      return result;
    }


    /// <summary>
    /// The square of the distance from a point to the whole line through two others; nought when the two are the same
    /// point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PerpendicDistFromLineSqrd(PointD pt, PointD line1, PointD line2)
    {
      double a = pt.x - line1.x;
      double b = pt.y - line1.y;
      double c = line2.x - line1.x;
      double d = line2.y - line1.y;
      if (c == 0 && d == 0) return 0;
      return Sqr(a * d - c * b) / (c * c + d * d);
    }

    /// <summary>
    /// The square of the distance from a point to the whole line through two others, worked out in doubles; nought when
    /// the two are the same point.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double PerpendicDistFromLineSqrd(Point64 pt, Point64 line1, Point64 line2)
    {
      double a = (double) pt.X - line1.X;
      double b = (double) pt.Y - line1.Y;
      double c = (double) line2.X - line1.X;
      double d = (double) line2.Y - line1.Y;
      if (c == 0 && d == 0) return 0;
      return Sqr(a * d - c * b) / (c * c + d * d);
    }

    /// <summary>
    /// One step of Ramer-Douglas-Peucker between two points kept: when the point between them furthest from the line
    /// through them lies further than the square root of epsSqrd, it is marked kept and both sides of it are worked on
    /// the same way. Points at the end that repeat the point at the beginning are marked dropped.
    /// </summary>
    internal static void RDP(Path64 path, int begin, int end, double epsSqrd, List<bool> flags)
    {
      while (true)
      {
        int idx = 0;
        double max_d = 0;
        while (end > begin && path[begin] == path[end]) flags[end--] = false;
        for (int i = begin + 1; i < end; ++i)
        {
          // PerpendicDistFromLineSqrd - avoids expensive Sqrt()
          double d = PerpendicDistFromLineSqrd(path[i], path[begin], path[end]);
          if (d <= max_d) continue;
          max_d = d;
          idx = i;
        }

        if (max_d <= epsSqrd) return;
        flags[idx] = true;
        if (idx > begin + 1) RDP(path, begin, idx, epsSqrd, flags);
        if (idx < end - 1)
        {
          begin = idx;
          continue;
        }

        break;
      }
    }

    /// <summary>
    /// The path simplified by Ramer-Douglas-Peucker: its first and last points are kept, and every point dropped lies
    /// within epsilon of the line through the points kept either side of it. A path of fewer than five points comes
    /// back as it is.
    /// </summary>
    public static Path64 RamerDouglasPeucker(Path64 path, double epsilon)
    {
      int len = path.Count;
      if (len < 5) return path;
      List<bool> flags = new List<bool>(new bool[len]) { [0] = true, [len - 1] = true };
      RDP(path, 0, len - 1, Sqr(epsilon), flags);
      Path64 result = new Path64(len);
      for (int i = 0; i < len; ++i)
        if (flags[i]) result.Add(path[i]);
      return result;
    }

    /// <summary>
    /// Each path simplified by Ramer-Douglas-Peucker, as for a single path.
    /// </summary>
    public static Paths64 RamerDouglasPeucker(Paths64 paths, double epsilon)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(RamerDouglasPeucker(path, epsilon));
      return result;
    }

    /// <summary>
    /// One step of Ramer-Douglas-Peucker between two points kept: when the point between them furthest from the line
    /// through them lies further than the square root of epsSqrd, it is marked kept and both sides of it are worked on
    /// the same way. Points at the end that repeat the point at the beginning are marked dropped.
    /// </summary>
    internal static void RDP(PathD path, int begin, int end, double epsSqrd, List<bool> flags)
    {
      while (true)
      {
        int idx = 0;
        double max_d = 0;
        while (end > begin && path[begin] == path[end]) flags[end--] = false;
        for (int i = begin + 1; i < end; ++i)
        {
          // PerpendicDistFromLineSqrd - avoids expensive Sqrt()
          double d = PerpendicDistFromLineSqrd(path[i], path[begin], path[end]);
          if (d <= max_d) continue;
          max_d = d;
          idx = i;
        }

        if (max_d <= epsSqrd) return;
        flags[idx] = true;
        if (idx > begin + 1) RDP(path, begin, idx, epsSqrd, flags);
        if (idx < end - 1)
        {
          begin = idx;
          continue;
        }

        break;
      }
    }

    /// <summary>
    /// The path simplified by Ramer-Douglas-Peucker: its first and last points are kept, and every point dropped lies
    /// within epsilon of the line through the points kept either side of it. A path of fewer than five points comes
    /// back as it is.
    /// </summary>
    public static PathD RamerDouglasPeucker(PathD path, double epsilon)
    {
      int len = path.Count;
      if (len < 5) return path;
      List<bool> flags = new List<bool>(new bool[len]) { [0] = true, [len - 1] = true };
      RDP(path, 0, len - 1, Sqr(epsilon), flags);
      PathD result = new PathD(len);
      for (int i = 0; i < len; ++i)
        if (flags[i]) result.Add(path[i]);
      return result;
    }

    /// <summary>
    /// Each path simplified by Ramer-Douglas-Peucker, as for a single path.
    /// </summary>
    public static PathsD RamerDouglasPeucker(PathsD paths, double epsilon)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(RamerDouglasPeucker(path, epsilon));
      return result;
    }


    /// <summary>
    /// The index of the first point after current that is not flagged removed, going round to the start after high.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetNext(int current, int high, ref bool[] flags)
    {
      ++current;
      while (current <= high && flags[current]) ++current;
      if (current <= high) return current;
      current = 0;
      while (flags[current]) ++current;
      return current;
    }

    /// <summary>
    /// The index of the first point before current that is not flagged removed, going round to high before the start.
    /// </summary>
    private static int GetPrior(int current, int high, ref bool[] flags)
    {
      if (current == 0) current = high;
      else --current;
      while (current > 0 && flags[current]) --current;
      if (!flags[current]) return current;
      current = high;
      while (flags[current]) --current;
      return current;
    }

    /// <summary>
    /// The path without the vertices that lie within epsilon of the line through their neighbours, removed one at a
    /// time, of two such neighbours the one nearer its line first, and the neighbours measured again after each. The
    /// ends of an open path stay; a path of fewer than four points comes back as it is.
    /// </summary>
    public static Path64 SimplifyPath(Path64 path,
      double epsilon, bool isClosedPath = true)
    {
      int len = path.Count, high = len - 1;
      double epsSqr = Sqr(epsilon);
      if (len < 4) return path;

      bool[] flags = new bool[len];
      double[] dsq = new double[len];
      int curr = 0;

      if (isClosedPath)
      {
        dsq[0] = PerpendicDistFromLineSqrd(path[0], path[high], path[1]);
        dsq[high] = PerpendicDistFromLineSqrd(path[high], path[0], path[high - 1]);
      }
      else
      {
        dsq[0] = double.MaxValue;
        dsq[high] = double.MaxValue;
      }

      for (int i = 1; i < high; ++i)
        dsq[i] = PerpendicDistFromLineSqrd(path[i], path[i - 1], path[i + 1]);

      for (; ; )
      {
        if (dsq[curr] > epsSqr)
        {
          int start = curr;
          do
          {
            curr = GetNext(curr, high, ref flags);
          } while (curr != start && dsq[curr] > epsSqr);
          if (curr == start) break;
        }

        int prev = GetPrior(curr, high, ref flags);
        int next = GetNext(curr, high, ref flags);
        if (next == prev) break;

        int prior2;
        if (dsq[next] < dsq[curr])
        {
          prior2 = prev;
          prev = curr;
          curr = next;
          next = GetNext(next, high, ref flags);
        }
        else
          prior2 = GetPrior(prev, high, ref flags);

        flags[curr] = true;
        curr = next;
        next = GetNext(next, high, ref flags);
        if (isClosedPath || ((curr != high) && (curr != 0)))
          dsq[curr] = PerpendicDistFromLineSqrd(path[curr], path[prev], path[next]);
        if (isClosedPath || ((prev != 0) && (prev != high)))
          dsq[prev] = PerpendicDistFromLineSqrd(path[prev], path[prior2], path[curr]);
      }
      Path64 result = new Path64(len);
      for (int i = 0; i < len; i++)
        if (!flags[i]) result.Add(path[i]);
      return result;
    }

    /// <summary>
    /// Each path simplified as SimplifyPath simplifies one.
    /// </summary>
    public static Paths64 SimplifyPaths(Paths64 paths,
      double epsilon, bool isClosedPaths = true)
    {
      Paths64 result = new Paths64(paths.Count);
      foreach (Path64 path in paths)
        result.Add(SimplifyPath(path, epsilon, isClosedPaths));
      return result;
    }

    /// <summary>
    /// The path without the vertices that lie within epsilon of the line through their neighbours, removed one at a
    /// time, of two such neighbours the one nearer its line first, and the neighbours measured again after each. The
    /// ends of an open path stay; a path of fewer than four points comes back as it is.
    /// </summary>
    public static PathD SimplifyPath(PathD path,
      double epsilon, bool isClosedPath = true)
    {
      int len = path.Count, high = len - 1;
      double epsSqr = Sqr(epsilon);
      if (len < 4) return path;

      bool[] flags = new bool[len];
      double[] dsq = new double[len];
      int curr = 0;
      if (isClosedPath)
      {
        dsq[0] = PerpendicDistFromLineSqrd(path[0], path[high], path[1]);
        dsq[high] = PerpendicDistFromLineSqrd(path[high], path[0], path[high - 1]);
      }
      else
      {
        dsq[0] = double.MaxValue;
        dsq[high] = double.MaxValue;
      }
      for (int i = 1; i < high; ++i)
        dsq[i] = PerpendicDistFromLineSqrd(path[i], path[i - 1], path[i + 1]);

      for (; ; )
      {
        if (dsq[curr] > epsSqr)
        {
          int start = curr;
          do
          {
            curr = GetNext(curr, high, ref flags);
          } while (curr != start && dsq[curr] > epsSqr);
          if (curr == start) break;
        }

        int prev = GetPrior(curr, high, ref flags);
        int next = GetNext(curr, high, ref flags);
        if (next == prev) break;

        int prior2;
        if (dsq[next] < dsq[curr])
        {
          prior2 = prev;
          prev = curr;
          curr = next;
          next = GetNext(next, high, ref flags);
        }
        else 
          prior2 = GetPrior(prev, high, ref flags);

        flags[curr] = true;
        curr = next;
        next = GetNext(next, high, ref flags);
        if (isClosedPath || ((curr != high) && (curr != 0)))
          dsq[curr] = PerpendicDistFromLineSqrd(path[curr], path[prev], path[next]);
        if (isClosedPath || ((prev != 0) && (prev != high)))
          dsq[prev] = PerpendicDistFromLineSqrd(path[prev], path[prior2], path[curr]);
      }
      PathD result = new PathD(len);
      for (int i = 0; i < len; i++)
        if (!flags[i]) result.Add(path[i]);
      return result;
    }

    /// <summary>
    /// Each path simplified as SimplifyPath simplifies one.
    /// </summary>
    public static PathsD SimplifyPaths(PathsD paths,
      double epsilon, bool isClosedPath = true)
    {
      PathsD result = new PathsD(paths.Count);
      foreach (PathD path in paths)
        result.Add(SimplifyPath(path, epsilon, isClosedPath));
      return result;
    }

    /// <summary>
    /// A copy of the path without the vertices that lie on the line through their neighbours, those where it turns
    /// straight back included. A closed path is trimmed across its start as well, and comes back empty with fewer than
    /// three vertices left; an open path keeps its ends.
    /// </summary>
    public static Path64 TrimCollinear(Path64 path, bool isOpen = false)
    {
      int len = path.Count;
      int i = 0;
      if (!isOpen)
      {
        while (i < len - 1 && 
          InternalClipper.IsCollinear(path[len - 1], path[i], path[i + 1])) i++;
        while (i < len - 1 && InternalClipper.IsCollinear(path[len - 2], path[len - 1], path[i])) len--;
      }

      if (len - i < 3)
      {
        if (!isOpen || len < 2 || path[0] == path[1])
          return new Path64();
        return path;
      }

      Path64 result = new Path64(len - i);
      Point64 last = path[i];
      result.Add(last);
      for (i++; i < len - 1; i++)
      {
        if (InternalClipper.IsCollinear(last, path[i], path[i + 1])) continue;
        last = path[i];
        result.Add(last);
      }

      if (isOpen)
        result.Add(path[len - 1]);
      else if (!InternalClipper.IsCollinear(last, path[len - 1], result[0]))
        result.Add(path[len - 1]);
      else
      {
        while (result.Count > 2 && InternalClipper.IsCollinear(
                 result[result.Count - 1], result[result.Count - 2], result[0]))
        {
          result.RemoveAt(result.Count - 1);
        }
        if (result.Count < 3)
          result.Clear();
      }
      return result;
    }

    /// <summary>
    /// A copy of the path without the vertices that lie on the line through their neighbours, as for a path of
    /// integers, with the coordinates rounded to <c>precision</c> decimal places, from -8 to 8, the answer scaled back.
    /// </summary>
    public static PathD TrimCollinear(PathD path, int precision, bool isOpen = false)
    {
      InternalClipper.CheckPrecision(precision);
      double scale = Math.Pow(10, precision);
      Path64 p = ScalePath64(path, scale);
      p = TrimCollinear(p, isOpen);
      return ScalePathD(p, 1 / scale);
    }

    /// <summary>
    /// Where a point lies against a polygon read by the even-odd rule: inside, outside or on its boundary. A polygon of
    /// fewer than three points has nothing inside.
    /// </summary>
    public static PointInPolygonResult PointInPolygon(Point64 pt, Path64 polygon)
    {
      return InternalClipper.PointInPolygon(pt, polygon);
    }

    /// <summary>
    /// Where a point lies against a polygon read by the even-odd rule, inside, outside or on its boundary, with the
    /// point and the polygon rounded to <c>precision</c> decimal places, from -8 to 8.
    /// </summary>
    public static PointInPolygonResult PointInPolygon(PointD pt, 
      PathD polygon, int precision = 2)
    {
      InternalClipper.CheckPrecision(precision);
      double scale = Math.Pow(10, precision);
      Point64 p = new Point64(pt, scale);
      Path64 path = ScalePath64(polygon, scale);
      return InternalClipper.PointInPolygon(p, path);
    }

    /// <summary>
    /// A polygon of <c>steps</c> points on the ellipse of the radii about the center, counter-clockwise with Y up from
    /// the point at radiusX along X, rounded to integers. A radiusY of nought or less takes radiusX, for a circle;
    /// steps of two or fewer take pi times the square root of the mean radius, rounded up. Nothing for a radiusX of
    /// nought or less.
    /// </summary>
    public static Path64 Ellipse(Point64 center,
      double radiusX, double radiusY = 0, int steps = 0)
    {
      if (radiusX <= 0) return new Path64();
      if (radiusY <= 0) radiusY = radiusX;
      if (steps <= 2)
        steps = (int) Math.Ceiling(Math.PI * Math.Sqrt((radiusX + radiusY) / 2));

      double si = Math.Sin(2 * Math.PI / steps);
      double co = Math.Cos(2 * Math.PI / steps);
      double dx = co, dy = si;
      Path64 result = new Path64(steps) { new Point64(center.X + radiusX, center.Y) };
      for (int i = 1; i < steps; ++i)
      {
        result.Add(new Point64(center.X + radiusX * dx, center.Y + radiusY * dy));
        double x = dx * co - dy * si;
        dy = dy * co + dx * si;
        dx = x;
      }
      return result;
    }

    /// <summary>
    /// A polygon of <c>steps</c> points on the ellipse of the radii about the center, counter-clockwise with Y up from
    /// the point at radiusX along X. A radiusY of nought or less takes radiusX, for a circle; steps of two or fewer
    /// take pi times the square root of the mean radius, rounded up. Nothing for a radiusX of nought or less.
    /// </summary>
    public static PathD Ellipse(PointD center,
      double radiusX, double radiusY = 0, int steps = 0)
    {
      if (radiusX <= 0) return new PathD();
      if (radiusY <= 0) radiusY = radiusX;
      if (steps <= 2)
        steps = (int) Math.Ceiling(Math.PI * Math.Sqrt((radiusX + radiusY) / 2));

      double si = Math.Sin(2 * Math.PI / steps);
      double co = Math.Cos(2 * Math.PI / steps);
      double dx = co, dy = si;
      PathD result = new PathD(steps) { new PointD(center.x + radiusX, center.y) };
      for (int i = 1; i < steps; ++i)
      {
        result.Add(new PointD(center.x + radiusX * dx, center.y + radiusY * dy));
        double x = dx * co - dy * si;
        dy = dy * co + dx * si;
        dx = x;
      }
      return result;
    }

    /// <summary>
    /// Writes a node of the tree to the console, Outer or Hole with the number of its children, indented by its level,
    /// and then its children a level deeper.
    /// </summary>
    private static void ShowPolyPathStructure(PolyPath64 pp, int level)
    {
      string spaces = new string(' ', level * 2);
      string caption = (pp.IsHole ? "Hole " : "Outer ");
      if (pp.Count == 0)
      {
        Console.WriteLine(spaces + caption);
      }
      else
      {
        Console.WriteLine(spaces + caption + $"({pp.Count})");
        foreach (PolyPath64 child in pp) { ShowPolyPathStructure(child, level + 1); }
      }
    }

    /// <summary>
    /// Writes the nesting of a tree to the console, a line for each node indented by its depth, for debugging.
    /// </summary>
    public static void ShowPolyTreeStructure(PolyTree64 polytree)
    {
      Console.WriteLine("Polytree Root");
      foreach (PolyPath64 child in polytree) { ShowPolyPathStructure(child, 1); }
    }

    /// <summary>
    /// Writes a node of the tree to the console, Outer or Hole with the number of its children, indented by its level,
    /// and then its children a level deeper.
    /// </summary>
    private static void ShowPolyPathStructure(PolyPathD pp, int level)
    {
      string spaces = new string(' ', level * 2);
      string caption = (pp.IsHole ? "Hole " : "Outer ");
      if (pp.Count == 0)
      {
        Console.WriteLine(spaces + caption);
      }
      else
      {
        Console.WriteLine(spaces + caption + $"({pp.Count})");
        foreach (PolyPathD child in pp) { ShowPolyPathStructure(child, level + 1); }
      }
    }

    /// <summary>
    /// Writes the nesting of a tree to the console, a line for each node indented by its depth, for debugging.
    /// </summary>
    public static void ShowPolyTreeStructure(PolyTreeD polytree)
    {
      Console.WriteLine("Polytree Root");
      foreach (PolyPathD child in polytree) { ShowPolyPathStructure(child, 1); }
    }


  } // Clipper2
} // namespace