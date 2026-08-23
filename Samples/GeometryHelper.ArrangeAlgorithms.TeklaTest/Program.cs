using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.PlaneGeometry.Geometry;
using TSD = Tekla.Structures.Drawing;
using TSG = Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using Tekla.Structures.Datatype;
using Tekla.Structures.Drawing;
using Tekla.Structures.Geometry3d;

namespace GeometryHelper.ArrangeAlgorithms.TeklaTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Run the main test routine for arranging rebar marks
            RebarTest();
        }

        /// <summary>
        /// Test method that connects to Tekla structures, retrieves selected marks and dimensions,
        /// calculates optimal non-overlapping positions for rebar marks, and updates the drawing.
        /// </summary>
        static void RebarTest()
        {
            // Initialize connection to the active Tekla model and drawing handler
            TSM.Model model = new TSM.Model();
            TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
            TSD.Drawing drawing = drawingHandler.GetActiveDrawing();

            // Display connection status to Tekla Model and Drawing Editor
            Console.WriteLine("Model connection status: " + model.GetConnectionStatus());
            Console.WriteLine("Drawing handler connection status: " + drawingHandler.GetConnectionStatus());

            List<TSD.Mark> marks = new List<TSD.Mark>();
            List<DrawingObject> dimensions = new List<DrawingObject>();

            // Retrieve all currently selected objects in the active drawing
            var selects = drawingHandler.GetDrawingObjectSelector().GetSelected();
            while (selects.MoveNext())
            {
                // Classify selected drawing objects into marks and dimensions
                if (selects.Current is TSD.Mark mark)
                {
                    marks.Add(mark);
                }
                else if (selects.Current is TSD.StraightDimensionSet straightDimensionSet)
                {
                    dimensions.Add(straightDimensionSet);
                }
                else if (selects.Current is TSD.StraightDimension straightDimension)
                {
                    dimensions.Add(straightDimension);
                }
            }

            // Retrieve related model reinforcement info and calculate the middle line for each mark
            List<MarkRebarGroup> markGroups = new List<MarkRebarGroup>();

            // Define block regions (polygons) where marks should not be placed, based on selected dimensions
            var blockPolygons = new List<GeoPolygon2>();

            // Define block lines based on the centerlines of the rebars, preventing marks from overlapping them
            var blockLines = new List<GeoLine2>();

            foreach (var mark in marks)
            {
                // Find drawing reinforcement objects associated with the selected mark
                var drawingRebars = mark.GetRelatedObjects([typeof(TSD.ReinforcementBase)]);
                drawingRebars.MoveNext();

                var drawingRebar = drawingRebars.Current as TSD.ReinforcementBase;
                if (drawingRebar == null)
                    continue;

                // Select the corresponding model reinforcement object using the drawing rebar's model identifier
                var modelRebar = model.SelectModelObject(drawingRebar.ModelIdentifier) as TSM.Reinforcement;
                if (modelRebar == null)
                    continue;

                // Get 3D geometries of the rebar and calculate the middle centerline points
                var geometries = modelRebar.GetRebarGeometries(false).Cast<TSM.RebarGeometry>().ToList();
                var middlePoints = GetMiddlePointOfRebar(geometries);

                // Convert the calculated centerline points into individual line segments
                List<LineSegment> segments = middlePoints.ToLineSegments();
                foreach (LineSegment segment in segments)
                {
                    // Add each segment as a block line to prevent other marks from crossing over this rebar
                    blockLines.Add(new GeoLine2(segment.StartPoint.ToGeoPoint(), segment.EndPoint.ToGeoPoint()));
                }

                // Group the mark and rebar data together for positioning calculations
                markGroups.Add(new MarkRebarGroup
                {
                    View = mark.GetView() as TSD.View,
                    Mark = mark,
                    DrawingRebar = drawingRebar,
                    ModelRebar = modelRebar,
                    // Use the longest segment of the centerline as the primary guide line for the mark
                    MiddleLineRebar = segments.GetLongestLength()
                });
            }

            foreach (var dimension in dimensions)
            {
                if (dimension is StraightDimensionSet straightDimensionSet)
                {
                    // Calculate the Axis-Aligned Bounding Box (AABB) of the dimension set
                    var dimBox = GetDimensionBox(straightDimensionSet);
                    GeoPoint2[] vertex =
                    [
                        new GeoPoint2(dimBox.MinPoint.X, dimBox.MinPoint.Y),
                        new GeoPoint2(dimBox.MinPoint.X, dimBox.MaxPoint.Y),
                        new GeoPoint2(dimBox.MaxPoint.X, dimBox.MaxPoint.Y),
                        new GeoPoint2(dimBox.MaxPoint.X, dimBox.MinPoint.Y),
                    ];
                    // Add the bounding polygon to the list of blocked regions
                    blockPolygons.Add(new GeoPolygon2(vertex));
                }
            }

            // Create Arrange objects representing the layout constraints for each mark
            List<Arrange> arranges = new List<Arrange>();
            foreach (var markGroup in markGroups)
            {
                var markBox = markGroup.Mark.GetAxisAlignedBoundingBox();

                arranges.Add(new Arrange
                {
                    // Define the mark's boundary rectangle (center point, width, height, and angle)
                    GeoRectangle2 = new GeoRectangle2(
                        markBox.GetCenterPoint().ToGeoPoint(),
                        markBox.Width,
                        markBox.Height,
                        Angle.FromDegrees(markBox.AngleToAxis).Radians),
                    // Define the target centerline for the mark
                    GeoLine2 = new GeoLine2(markGroup.MiddleLineRebar.StartPoint.ToGeoPoint(), markGroup.MiddleLineRebar.EndPoint.ToGeoPoint()),
                    // Preferred offset distance of the mark from the rebar centerline
                    BaseOffsetFromLine = 50.0,
                    // Keep track of obstacles (dimensions and other rebar lines) to avoid overlaps
                    BlockPolygons = blockPolygons,
                    BlockLines = blockLines,
                });
            }

            // Set up layout options using the Greedy algorithm
            ArrangeOptions arrangeOptions = new ArrangeOptions
            {
                Algorithm = ArrangeAlgorithmType.Greedy,
                MinimumMoveDistance = 10.0
            };

            // Run the label placement optimization algorithm
            Arrange.Run(arranges, arrangeOptions);

            // Apply calculated displacement vectors to move the marks in the drawing
            for (int i = 0; i < arranges.Count; i++)
            {
                // Only move the mark if the calculated displacement exceeds the minimum distance threshold
                if (arranges[i].TranslationVector.Length < arrangeOptions.MinimumMoveDistance)
                    continue;

                TSG.Vector translate = arranges[i].TranslationVector.ToTeklaVector();
                markGroups[i].Mark.MoveObjectRelative(translate);
                markGroups[i].Mark.Modify();
            }

            // Commit changes and save the modified active drawing
            drawing.CommitChanges();
            drawingHandler.SaveActiveDrawing();
        }

        /// <summary>
        /// Calculates the centerline points of a reinforcement bar group.
        /// For a single rebar, it returns its shape points. For a group of rebars,
        /// it interpolates the middle points between the first and last rebar geometries.
        /// </summary>
        static List<TSG.Point> GetMiddlePointOfRebar(List<TSM.RebarGeometry> geometries)
        {
            if (geometries.Count == 0)
            {
                throw new ArgumentException("Rebar geometries list cannot be empty.", nameof(geometries));
            }

            if (geometries.Count == 1)
            {
                // Return points of the single reinforcement bar
                return geometries.First().Shape.Points.Cast<TSG.Point>().ToList();
            }

            var firstPoints = geometries.First().Shape.Points.Cast<TSG.Point>().ToList();
            var lastPoints = geometries.Last().Shape.Points.Cast<TSG.Point>().ToList();

            if (firstPoints.Count != lastPoints.Count)
            {
                throw new InvalidOperationException("Mismatched point count between the first and last rebar geometries in the group.");
            }

            // Interpolate the average/middle points between the first and last rebar of the group
            var middlePoints = new List<TSG.Point>();
            for (int i = 0; i < firstPoints.Count; i++)
            {
                var vector = new TSG.Vector(lastPoints[i] - firstPoints[i]).GetNormal();
                var distance = TSG.Distance.PointToPoint(firstPoints[i], lastPoints[i]) * 0.5;
                middlePoints.Add(firstPoints[i] + vector * distance);
            }

            return middlePoints;
        }

        /// <summary>
        /// Computes the bounding box (AABB) for a set of straight dimensions.
        /// </summary>
        static AABB GetDimensionBox(StraightDimensionSet straightDimensionSet)
        {
            AABB dimBox = new AABB();

            // Iterate over all individual dimension elements in the set and combine their bounding boxes
            var straightDimensions = straightDimensionSet.GetObjects();
            while (straightDimensions.MoveNext())
            {
                if (straightDimensions.Current is StraightDimension straightDimension)
                {
                    dimBox += GetDimensionBox(straightDimension);
                }
            }

            return dimBox;
        }

        /// <summary>
        /// Computes the bounding box (AABB) for a single straight dimension line,
        /// taking into account its baseline, direction, and extension line distance.
        /// </summary>
        static AABB GetDimensionBox(StraightDimension straightDimension)
        {
            AABB dimBox = new AABB();

            // Include the physical start and end points of the dimension line
            dimBox += straightDimension.StartPoint;
            dimBox += straightDimension.EndPoint;

            // Include the offset points based on the dimension's distance and up direction
            dimBox += straightDimension.StartPoint + straightDimension.UpDirection * straightDimension.Distance;
            dimBox += straightDimension.EndPoint + straightDimension.UpDirection * straightDimension.Distance;

            return dimBox;
        }

        /// <summary>
        /// Data structure holding the mapping relationships between a drawing Mark,
        /// its drawing and model Rebar representations, and the calculated centerline.
        /// </summary>
        public class MarkRebarGroup
        {
            public TSD.View View { get; set; }
            public TSD.Mark Mark { get; set; }
            public TSD.ReinforcementBase DrawingRebar { get; set; }
            public TSM.Reinforcement ModelRebar { get; set; }
            public TSG.LineSegment MiddleLineRebar { get; set; }
        }
    }
}
