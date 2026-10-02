using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper;
using GeometryHelper.Enums;
using GeometryHelper.Geometry;
using GeometryHelper.Meshing;
using Xunit;

namespace GeometryHelper.UnitTest.Meshing
{
    /// <summary>
    /// What every grid of cells promises, held against the body it was cut from.
    /// </summary>
    internal static class CellAssert
    {
        /// <summary>
        /// Holds a grid to its promises: the cells in order, each a closed body of the volume it says, within its whole cell
        /// but for the snap distance, its box the grid's at its index; the volume expected; and at points spread through the
        /// body, a point of the body in one cell, or in at most one where joints leave some out, and a point outside in none.
        /// </summary>
        /// <param name="grid">The grid.</param>
        /// <param name="body">Where a point stands against the body.</param>
        /// <param name="volume">The volume the cells should hold together.</param>
        /// <param name="snap">How far a cut may have been moved off the grid's lines.</param>
        /// <param name="tolerance">The tolerance the cells were cut within.</param>
        /// <param name="covers">Whether every point of the body lies in a cell: false with joints.</param>
        /// <param name="samples">How many points to try.</param>
        public static void IsSound(GeoCellGrid3 grid, Func<GeoPoint3, PointLocation> body, double volume, double snap, Tolerance tolerance, bool covers = true, int samples = 600)
        {
            Assert.True(grid.Frame.IsValid, $"the frame is no frame: {grid.Frame}");
            Assert.True(Math.Abs(grid.Volume - volume) <= 1E-9 * Math.Max(1.0, Math.Abs(volume)), $"the cells hold {grid.Volume:R}, not {volume:R}");
            Assert.True(Math.Abs(grid.Cells.Sum(c => c.Volume) - grid.Volume) <= 1E-9 * Math.Max(1.0, grid.Volume), "Volume is not the cells'");

            GeoCell3 previous = null;

            foreach (GeoCell3 cell in grid.Cells)
            {
                if (previous != null)
                {
                    int order = cell.K != previous.K ? cell.K.CompareTo(previous.K) : cell.J != previous.J ? cell.J.CompareTo(previous.J) : cell.I != previous.I ? cell.I.CompareTo(previous.I) : cell.Piece.CompareTo(previous.Piece);
                    Assert.True(order > 0, $"{cell} comes after {previous}");
                }

                previous = cell;
                Assert.True(cell.I >= 0 && cell.I < grid.CountX && cell.J >= 0 && cell.J < grid.CountY && cell.K >= 0 && cell.K < grid.CountZ, $"{cell} stands outside the grid");
                Assert.True(cell.Volume > 0.0, $"{cell} holds nothing");

                GeoSolid3 solid = cell.Solid;
                Assert.True(solid.IsClosed(tolerance), $"{cell} is not closed");
                Assert.True(Math.Abs(solid.Volume - cell.Volume) <= 1E-9 * Math.Max(1.0, cell.Volume), $"{cell} says {cell.Volume:R}, its body holds {solid.Volume:R}");

                GeoObb3 box = grid.GetBox(cell.I, cell.J, cell.K);
                Assert.True(box.IsEqualTo(cell.Box, new Tolerance(1E-9, 1E-9)), $"{cell} stands in {cell.Box}, the grid lays {box} there");

                double reach = snap + 2.0 * tolerance.EqualPoint;

                foreach (GeoFace3 face in solid.Faces)
                {
                    foreach (GeoPoint3 corner in face.Boundary.Vertices)
                    {
                        GeoPoint3 local = box.CoordinateSystem.ToLocal(corner);
                        Assert.True(Math.Abs(local.X) <= box.ExtentX + reach && Math.Abs(local.Y) <= box.ExtentY + reach && Math.Abs(local.Z) <= box.ExtentZ + reach, $"{cell} reaches out of its cell to {corner}");
                    }
                }

                if (cell.IsWhole)
                {
                    Assert.True(Math.Abs(cell.Volume - box.Volume) <= tolerance.EqualPoint * box.SurfaceArea, $"{cell} is whole but holds {cell.Volume:R} of {box.Volume:R}");
                }
            }

            Covers(grid, body, tolerance, covers, samples);
        }

        /// <summary>
        /// At points spread through the body's box: a point of the body in one cell, or at most one with joints, a point
        /// outside it in none.
        /// </summary>
        private static void Covers(GeoCellGrid3 grid, Func<GeoPoint3, PointLocation> body, Tolerance tolerance, bool covers, int samples)
        {
            if (grid.CellCount == 0)
            {
                return;
            }

            // The points are spread through the cells' box in the grid's own frame, which a turned body fills.
            GeoAabb3 bounds = GeoAabb3.Empty;

            foreach (GeoCell3 cell in grid.Cells)
            {
                foreach (GeoFace3 face in cell.Solid.Faces)
                {
                    foreach (GeoPoint3 corner in face.Boundary.Vertices)
                    {
                        bounds = bounds.Union(grid.Frame.ToLocal(corner));
                    }
                }
            }

            bounds = bounds.Expand(0.1 * Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ)));
            GeoSolid3[] solids = grid.GetSolids();
            GeoAabb3[] boxes = solids.Select(s => s.GetAabb()).ToArray();
            var random = new Random(11);
            int inside = 0;

            for (int n = 0; n < samples; n++)
            {
                GeoPoint3 point = grid.Frame.ToGlobal(new GeoPoint3(
                    bounds.Min.X + random.NextDouble() * bounds.SizeX,
                    bounds.Min.Y + random.NextDouble() * bounds.SizeY,
                    bounds.Min.Z + random.NextDouble() * bounds.SizeZ));
                PointLocation where = body(point);

                if (where == PointLocation.OnSide)
                {
                    continue;
                }

                int count = 0;
                bool onSide = false;

                for (int c = 0; c < solids.Length; c++)
                {
                    if (!boxes[c].Contains(point, tolerance))
                    {
                        continue;
                    }

                    PointLocation inCell = solids[c].Locate(point, tolerance);
                    onSide |= inCell == PointLocation.OnSide;
                    count += inCell == PointLocation.Inside ? 1 : 0;
                }

                if (onSide)
                {
                    continue;
                }

                if (where == PointLocation.Inside)
                {
                    inside++;
                    Assert.True(covers ? count == 1 : count <= 1, $"{point} of the body lies in {count} cells");
                }
                else
                {
                    Assert.True(count == 0, $"{point} outside the body lies in {count} cells");
                }
            }

            Assert.True(inside >= 10, $"only {inside} points fell in the body");
        }
    }
}
