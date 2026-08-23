using System;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using PlaneGeometry.Geometry;

namespace ArrangeAlgorithms.CadTest
{
    public class RectangleCombineTestRunner
    {
        public void RunRectangleCombineTest()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            // 1. Pick a Rectangle (Polyline)
            var rectOpts = new PromptEntityOptions("\nSelect a rectangle (Polyline):");
            rectOpts.SetRejectMessage("\nMust select a Polyline representing a rectangle.");
            rectOpts.AddAllowedClass(typeof(Polyline), exactMatch: true);

            PromptEntityResult rectRes = ed.GetEntity(rectOpts);
            if (rectRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nRectangle selection cancelled.");
                return;
            }

            // 2. Pick a Circle
            var circleOpts = new PromptEntityOptions("\nSelect a circle:");
            circleOpts.SetRejectMessage("\nMust select a Circle.");
            circleOpts.AddAllowedClass(typeof(Circle), exactMatch: true);

            PromptEntityResult circleRes = ed.GetEntity(circleOpts);
            if (circleRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\nCircle selection cancelled.");
                return;
            }

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    // Read selected polyline and check if it is a rectangle
                    Polyline poly = tr.GetObject(rectRes.ObjectId, OpenMode.ForRead) as Polyline;
                    if (poly == null || !poly.TryToGeoRectangle(out GeoRectangle2 rect))
                    {
                        ed.WriteMessage("\nThe selected Polyline does not form a valid rectangle.");
                        tr.Abort();
                        return;
                    }

                    // Read selected circle
                    Circle circle = tr.GetObject(circleRes.ObjectId, OpenMode.ForRead) as Circle;
                    if (circle == null)
                    {
                        ed.WriteMessage("\nInvalid circle selected.");
                        tr.Abort();
                        return;
                    }
                    GeoCircle2 geoCircle = circle.ToGeoCircle();

                    // Convert circle to bounding rectangle oriented with the rectangle's angle
                    GeoRectangle2 circleRect = geoCircle.ToRectangle(rect.AngleRad);

                    // Combine the two rectangles
                    GeoRectangle2 combined = rect.Combine(circleRect);

                    // Create AutoCAD Polyline for the oriented circle boundary
                    Polyline acadCircleRect = circleRect.ToPolyline().ToAcadPolyline();
                    acadCircleRect.LayerId = SplitTestRunner.EnsureLayer(db, tr, "RectangleCombine_CircleBound");
                    acadCircleRect.ColorIndex = 2; // Yellow
                    acadCircleRect.Closed = true;

                    // Create AutoCAD Polyline for the combined rectangle
                    Polyline acadCombined = combined.ToPolyline().ToAcadPolyline();
                    acadCombined.LayerId = SplitTestRunner.EnsureLayer(db, tr, "RectangleCombine_Result");
                    acadCombined.ColorIndex = 3; // Green
                    acadCombined.Closed = true;

                    // Append to model space
                    modelSpace.AppendEntity(acadCircleRect);
                    tr.AddNewlyCreatedDBObject(acadCircleRect, true);

                    modelSpace.AppendEntity(acadCombined);
                    tr.AddNewlyCreatedDBObject(acadCombined, true);

                    ed.WriteMessage("\n[Rectangle Combine] Successfully combined the rectangle and the oriented bounding box of the circle.");
                    ed.WriteMessage($"\n[Rectangle Combine] Original Rect: Center({rect.Center.X:F2},{rect.Center.Y:F2}), Dim({rect.Width:F2}x{rect.Height:F2}), Angle({rect.AngleRad:F2} rad)");
                    ed.WriteMessage($"\n[Rectangle Combine] Circle: Center({geoCircle.Center.X:F2},{geoCircle.Center.Y:F2}), Radius({geoCircle.Radius:F2})");
                    ed.WriteMessage($"\n[Rectangle Combine] Combined Rect: Center({combined.Center.X:F2},{combined.Center.Y:F2}), Dim({combined.Width:F2}x{combined.Height:F2})");

                    tr.Commit();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\nError in Rectangle Combine: {ex.Message}");
                    tr.Abort();
                }
            }
        }
    }
}
