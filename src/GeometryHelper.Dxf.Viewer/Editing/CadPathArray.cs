using System;
using System.Collections.Generic;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>How the items of a path array are turned: with the path as the first item stands, by a direction the user gave, or with the path along each item's Z axis.</summary>
    internal enum CadPathTangent
    {
        /// <summary>The direction of the path at the first item is the direction of the items: the first item is not turned.</summary>
        Path,

        /// <summary>The direction given by two points stands for the path's direction: the first item is turned from it to the path.</summary>
        Vector,

        /// <summary>The Normal option: the path runs along the Z axis of each item, as AutoCAD 2021's ARRAYPATH turns it.</summary>
        Normal,
    }

    /// <summary>
    /// The items of one path array, as AutoCAD 2021's ARRAYPATH places them (probed over its console): the items follow the
    /// path from the end nearest the pick (a closed path from its start), each a step of the Measure distance or an equal
    /// share of the path (Divide) from the one before, each carrying the first item's place with it, so that a first item away
    /// from the path stays that far from it all along. The base point, the first item's point that stands on the path where
    /// the array begins, is the path's start unless the user gave one; the items are turned by how the path turns unless
    /// Align items is No; the rows lie to the left of the way the path runs and the levels above, along Z.
    /// </summary>
    internal sealed class CadPathArray
    {
        /// <summary>The most items an array holds.</summary>
        internal const int MostItems = 32767;

        /// <summary>Makes the array over a path.</summary>
        /// <param name="path">The path curve.</param>
        /// <param name="length">Its length.</param>
        /// <param name="closed">Whether it is closed: its start is the first item's place, and its last item stops short of its start.</param>
        /// <param name="fromEnd">Whether the items begin at the end of the path, as the pick was nearer to it.</param>
        internal CadPathArray(DxfCurve path, double length, bool closed, bool fromEnd)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Length = length;
            Closed = closed;
            FromEnd = fromEnd && !closed;
        }

        /// <summary>Gets the path curve.</summary>
        internal DxfCurve Path { get; }

        /// <summary>Gets the length of the path.</summary>
        internal double Length { get; }

        /// <summary>Gets whether the path is closed.</summary>
        internal bool Closed { get; }

        /// <summary>Gets whether the array begins at the end of the path.</summary>
        internal bool FromEnd { get; }

        /// <summary>Gets or sets Divide, the items equally spread over the path; false is Measure, a distance between them.</summary>
        internal bool Divide { get; set; }

        /// <summary>Gets or sets the distance between items of Measure.</summary>
        internal double Spacing { get; set; } = 1.0;

        /// <summary>Gets or sets the number of items along the path.</summary>
        internal int Items { get; set; } = 1;

        /// <summary>Gets or sets the base point; null is the point of the path the array begins at.</summary>
        internal GeoPoint3? Base { get; set; }

        /// <summary>Gets or sets how the items are turned.</summary>
        internal CadPathTangent Tangent { get; set; }

        /// <summary>Gets or sets the direction of the first item, for <see cref="CadPathTangent.Vector"/>, as an angle in the plan in radians.</summary>
        internal double TangentAngle { get; set; }

        /// <summary>Gets or sets whether the items are turned to follow the path.</summary>
        internal bool Align { get; set; } = true;

        /// <summary>Gets or sets the number of rows.</summary>
        internal int Rows { get; set; } = 1;

        /// <summary>Gets or sets the distance between rows, to the left of the way the path runs.</summary>
        internal double RowSpacing { get; set; } = 1.0;

        /// <summary>Gets or sets how much higher each row is than the one before.</summary>
        internal double Elevation { get; set; }

        /// <summary>Gets or sets the number of levels.</summary>
        internal int Levels { get; set; } = 1;

        /// <summary>Gets or sets the distance between levels, along Z.</summary>
        internal double LevelSpacing { get; set; } = 1.0;

        /// <summary>
        /// The most items a Measure distance holds along the path: a path of 100 measured by 15 holds 7, items at 0, 15 and so
        /// on to 90; a closed path of 314.16 by 100 holds 3, since AutoCAD does not count the one at 300 that would stand
        /// within a step of its start.
        /// </summary>
        internal int MaxItems(double spacing)
        {
            if (!(spacing > 0.0))
            {
                return 1;
            }

            double whole = Math.Floor(Length / spacing + 1E-9);
            double count = Closed ? whole : whole + 1.0;
            return (int)Math.Max(1.0, Math.Min(count, MostItems));
        }

        /// <summary>
        /// The distances of the items from where the array begins: a step of <see cref="Spacing"/> for Measure, with the items
        /// that fit; equal shares of the path for Divide, the last at the end of an open path, and a closed path shared by as
        /// many as there are items.
        /// </summary>
        internal List<double> Distances()
        {
            var distances = new List<double>();
            int count = Math.Max(1, Items);

            if (!Divide)
            {
                count = Math.Min(count, MaxItems(Spacing));

                for (int k = 0; k < count; k++)
                {
                    distances.Add(k * Spacing);
                }

                return distances;
            }

            double share =
                count < 2 ? 0.0
                : Closed ? Length / count
                : Length / (count - 1);

            for (int k = 0; k < count; k++)
            {
                distances.Add(k * share);
            }

            return distances;
        }

        /// <summary>
        /// The transformations of the items in turn, row by row and level by level in each: the first is the first item's own,
        /// which is not a move at all unless a base point away from the path's start or a direction turned it. Each takes the
        /// base point to the item's place on the path and the item's rows and levels from there.
        /// </summary>
        internal List<GeoTransform3> Moves()
        {
            List<double> distances = Distances();
            var moves = new List<GeoTransform3>();
            GeoPoint3 start = default(GeoPoint3);
            double firstAngle = 0.0;
            GeoVector2 last = new GeoVector2(1.0, 0.0);

            for (int k = 0; k < distances.Count; k++)
            {
                double at = FromEnd ? Length - distances[k] : distances[k];
                at = Math.Min(Math.Max(at, 0.0), Length);
                GeoPoint3 point = Path.GetPointAtDist(at);
                GeoVector3 along = Path.GetFirstDerivative(Path.GetParameterAtDistance(at));
                double flat = Math.Sqrt(along.X * along.X + along.Y * along.Y);
                GeoVector2 tangent = flat > 1E-12 ? new GeoVector2(along.X / flat, along.Y / flat) : last;
                tangent = FromEnd ? new GeoVector2(-tangent.X, -tangent.Y) : tangent;
                last = tangent;
                double angle = Math.Atan2(tangent.Y, tangent.X);

                if (k == 0)
                {
                    start = point;
                    firstAngle = angle;
                }

                double[,] turn = Turn(tangent, angle, Tangent == CadPathTangent.Vector ? TangentAngle : firstAngle);
                GeoPoint3 origin = Base ?? start;

                for (int row = 0; row < Math.Max(1, Rows); row++)
                {
                    for (int level = 0; level < Math.Max(1, Levels); level++)
                    {
                        double sideways = row * RowSpacing;
                        double toX = point.X - tangent.Y * sideways;
                        double toY = point.Y + tangent.X * sideways;
                        double toZ = point.Z + row * Elevation + level * LevelSpacing;
                        var matrix = new double[4, 4];

                        for (int r = 0; r < 3; r++)
                        {
                            matrix[r, 0] = turn[r, 0];
                            matrix[r, 1] = turn[r, 1];
                            matrix[r, 2] = turn[r, 2];
                            double target =
                                r == 0 ? toX
                                : r == 1 ? toY
                                : toZ;
                            matrix[r, 3] =
                                target - (turn[r, 0] * origin.X + turn[r, 1] * origin.Y + turn[r, 2] * origin.Z);
                        }

                        matrix[3, 3] = 1.0;
                        moves.Add(new GeoTransform3(matrix));
                    }
                }
            }

            return moves;
        }

        /// <summary>The turn of an item: none where it is not aligned, the plan turned from the direction of the first item to the path's where it is, and for Normal the path along the item's Z axis.</summary>
        private double[,] Turn(GeoVector2 tangent, double angle, double reference)
        {
            var turn = new double[3, 3];

            if (!Align)
            {
                turn[0, 0] = 1.0;
                turn[1, 1] = 1.0;
                turn[2, 2] = 1.0;
                return turn;
            }

            if (Tangent == CadPathTangent.Normal)
            {
                // The columns are where the item's X, Y and Z go: to the left of the path, up, and along the path.
                turn[0, 0] = -tangent.Y;
                turn[1, 0] = tangent.X;
                turn[2, 1] = 1.0;
                turn[0, 2] = tangent.X;
                turn[1, 2] = tangent.Y;
                return turn;
            }

            double by = angle - reference;
            double cos = Math.Cos(by);
            double sin = Math.Sin(by);
            turn[0, 0] = cos;
            turn[0, 1] = -sin;
            turn[1, 0] = sin;
            turn[1, 1] = cos;
            turn[2, 2] = 1.0;
            return turn;
        }
    }
}
