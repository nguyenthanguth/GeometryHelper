using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using GeometryHelper.PlaneGeometry.Core;
using GeometryHelper.PlaneGeometry.Geometry;
using GeometryHelper.CadConvert;

namespace GeometryHelper.ArrangeAlgorithms.CadTest
{
    public class SplitAutoTestRunner
    {
        public static void RunSplitAutoTest()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            ed.WriteMessage("\n--- BẮT ĐẦU CHẠY AUTO TEST CẮT HÌNH HỌC (SPLIT AUTO TEST) ---");

            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    BlockTableRecord modelSpace = (BlockTableRecord)tr.GetObject(
                        SymbolUtilityServices.GetBlockModelSpaceId(db), OpenMode.ForWrite);

                    // 1. Dọn dẹp các đối tượng AutoTest cũ trên bản vẽ
                    int erasedCount = 0;
                    foreach (ObjectId objId in modelSpace)
                    {
                        Entity ent = tr.GetObject(objId, OpenMode.ForRead) as Entity;
                        if (ent != null && ent.Layer.StartsWith("AutoTest_", StringComparison.OrdinalIgnoreCase))
                        {
                            ent.UpgradeOpen();
                            ent.Erase();
                            erasedCount++;
                        }
                    }
                    if (erasedCount > 0)
                    {
                        ed.WriteMessage($"\n-> Đã dọn dẹp {erasedCount} đối tượng AutoTest cũ trên bản vẽ.");
                    }

                    // Khởi tạo các Layer kiểm thử
                    ObjectId layerCutter = SplitTestRunner.EnsureLayer(db, tr, "AutoTest_Cutter");
                    ObjectId layerPart1 = SplitTestRunner.EnsureLayer(db, tr, "AutoTest_Part1");
                    ObjectId layerPart2 = SplitTestRunner.EnsureLayer(db, tr, "AutoTest_Part2");
                    ObjectId layerText = SplitTestRunner.EnsureLayer(db, tr, "AutoTest_Text");

                    // Set màu cho các Layer
                    void SetLayerColor(ObjectId lId, short colorIndex)
                    {
                        var lRec = (LayerTableRecord)tr.GetObject(lId, OpenMode.ForWrite);
                        lRec.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByColor, colorIndex);
                    }
                    SetLayerColor(layerCutter, 9); // Light Gray
                    SetLayerColor(layerPart1, 1);  // Red
                    SetLayerColor(layerPart2, 3);  // Green
                    SetLayerColor(layerText, 2);   // Yellow

                    // Helper vẽ chú thích text
                    void AddComment(string msg, double x, double y)
                    {
                        DBText dbText = new DBText();
                        dbText.Position = new Point3d(x, y, 0);
                        dbText.TextString = msg;
                        dbText.Height = 0.6;
                        dbText.LayerId = layerText;
                        modelSpace.AppendEntity(dbText);
                        tr.AddNewlyCreatedDBObject(dbText, true);
                    }

                    // Helper vẽ các mảnh kết quả
                    void AddResultLines(GeoLine2[] pieces, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        for (int i = 0; i < pieces.Length; i++)
                        {
                            Line acadLine = pieces[i].ToAcadLine();
                            acadLine.LayerId = (i % 2 == 0) ? layerPart1 : layerPart2;
                            blockTableRecord.AppendEntity(acadLine);
                            transaction.AddNewlyCreatedDBObject(acadLine, true);
                        }
                    }

                    void AddResultPolylines(GeoPolyline2[] pieces, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        for (int i = 0; i < pieces.Length; i++)
                        {
                            Polyline acadPoly = pieces[i].ToAcadPolyline();
                            acadPoly.LayerId = (i % 2 == 0) ? layerPart1 : layerPart2;
                            blockTableRecord.AppendEntity(acadPoly);
                            transaction.AddNewlyCreatedDBObject(acadPoly, true);
                        }
                    }

                    void AddResultLinePair(GeoLine2 first, GeoLine2 second, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        Line acadFirst = first.ToAcadLine();
                        acadFirst.LayerId = layerPart1;
                        blockTableRecord.AppendEntity(acadFirst);
                        transaction.AddNewlyCreatedDBObject(acadFirst, true);

                        Line acadSecond = second.ToAcadLine();
                        acadSecond.LayerId = layerPart2;
                        blockTableRecord.AppendEntity(acadSecond);
                        transaction.AddNewlyCreatedDBObject(acadSecond, true);
                    }

                    void AddResultPolylinePair(GeoPolyline2 first, GeoPolyline2 second, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        Polyline acadFirst = first.ToAcadPolyline();
                        acadFirst.LayerId = layerPart1;
                        blockTableRecord.AppendEntity(acadFirst);
                        transaction.AddNewlyCreatedDBObject(acadFirst, true);

                        Polyline acadSecond = second.ToAcadPolyline();
                        acadSecond.LayerId = layerPart2;
                        blockTableRecord.AppendEntity(acadSecond);
                        transaction.AddNewlyCreatedDBObject(acadSecond, true);
                    }

                    void AddResultInsideOutsideLines(GeoLine2[] inside, GeoLine2[] outside, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        foreach (var piece in inside)
                        {
                            Line acadLine = piece.ToAcadLine();
                            acadLine.LayerId = layerPart2; // Green
                            acadLine.ColorIndex = 3;
                            blockTableRecord.AppendEntity(acadLine);
                            transaction.AddNewlyCreatedDBObject(acadLine, true);
                        }
                        foreach (var piece in outside)
                        {
                            Line acadLine = piece.ToAcadLine();
                            acadLine.LayerId = layerPart1; // Red
                            acadLine.ColorIndex = 1;
                            blockTableRecord.AppendEntity(acadLine);
                            transaction.AddNewlyCreatedDBObject(acadLine, true);
                        }
                    }

                    void AddResultInsideOutsidePolylines(GeoPolyline2[] inside, GeoPolyline2[] outside, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        foreach (var piece in inside)
                        {
                            Polyline acadPoly = piece.ToAcadPolyline();
                            acadPoly.LayerId = layerPart2; // Green
                            acadPoly.ColorIndex = 3;
                            blockTableRecord.AppendEntity(acadPoly);
                            transaction.AddNewlyCreatedDBObject(acadPoly, true);
                        }
                        foreach (var piece in outside)
                        {
                            Polyline acadPoly = piece.ToAcadPolyline();
                            acadPoly.LayerId = layerPart1; // Red
                            acadPoly.ColorIndex = 1;
                            blockTableRecord.AppendEntity(acadPoly);
                            transaction.AddNewlyCreatedDBObject(acadPoly, true);
                        }
                    }

                    void AddCutterLine(GeoLine2 cutter, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        Line acadLine = cutter.ToAcadLine();
                        acadLine.LayerId = layerCutter;
                        blockTableRecord.AppendEntity(acadLine);
                        transaction.AddNewlyCreatedDBObject(acadLine, true);
                    }

                    void AddCutterPolyline(GeoPolyline2 cutter, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        Polyline acadPoly = cutter.ToAcadPolyline();
                        acadPoly.LayerId = layerCutter;
                        blockTableRecord.AppendEntity(acadPoly);
                        transaction.AddNewlyCreatedDBObject(acadPoly, true);
                    }

                    void AddCutterPolygon(GeoPolygon2 cutter, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        // Convert GeoPolygon2 to Closed Polyline
                        Polyline acadPoly = new Polyline();
                        for (int i = 0; i < cutter.VertexCount; i++)
                        {
                            acadPoly.AddVertexAt(i, new Point2d(cutter[i].X, cutter[i].Y), 0, 0, 0);
                        }
                        acadPoly.Closed = true;
                        acadPoly.LayerId = layerCutter;
                        blockTableRecord.AppendEntity(acadPoly);
                        transaction.AddNewlyCreatedDBObject(acadPoly, true);
                    }

                    void AddCutterPoint(GeoPoint2 point, Transaction transaction, BlockTableRecord blockTableRecord)
                    {
                        DBPoint dbPt = new DBPoint(point.ToAcadPoint3());
                        dbPt.LayerId = layerCutter;
                        blockTableRecord.AppendEntity(dbPt);
                        transaction.AddNewlyCreatedDBObject(dbPt, true);
                    }

                    // --- BẮT ĐẦU VẼ CÁC KỊCH BẢN TEST ---

                    // 1. Line_MultipleLines (Offset Y = 0)
                    {
                        double y = 0.0;
                        AddComment("TC1: Line split by MLines (GeoLine2[])", 0, y + 1.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var cutters = new[]
                        {
                            new GeoLine2(new GeoPoint2(3, y - 2), new GeoPoint2(3, y + 2)),
                            new GeoLine2(new GeoPoint2(7, y - 2), new GeoPoint2(7, y + 2))
                        };
                        foreach (var c in cutters) AddCutterLine(c, tr, modelSpace);
                        if (subject.TrySplitBy(cutters, out GeoLine2[] pieces))
                        {
                            AddResultLines(pieces, tr, modelSpace);
                        }
                    }

                    // 2. Line_Polyline (Offset Y = 10)
                    {
                        double y = 10.0;
                        AddComment("TC2: Line split by Polyline (GeoPolyline2)", 0, y + 2.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var cutter = new GeoPolyline2(new GeoPoint2(2, y - 2), new GeoPoint2(5, y + 2), new GeoPoint2(8, y - 2));
                        AddCutterPolyline(cutter, tr, modelSpace);
                        if (subject.TrySplitBy(cutter, out GeoLine2[] pieces))
                        {
                            AddResultLines(pieces, tr, modelSpace);
                        }
                    }

                    // 3. Line_MultiplePoints (Offset Y = 20)
                    {
                        double y = 20.0;
                        AddComment("TC3: Line split by MPoints (GeoPoint2[])", 0, y + 1.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var points = new[] { new GeoPoint2(3, y), new GeoPoint2(6, y), new GeoPoint2(12, y) };
                        foreach (var pt in points) AddCutterPoint(pt, tr, modelSpace);
                        if (subject.TrySplitBy(points, out GeoLine2[] pieces))
                        {
                            AddResultLines(pieces, tr, modelSpace);
                        }
                    }

                    // 4. Line_MultiplePolygons (Offset Y = 30)
                    {
                        double y = 30.0;
                        AddComment("TC4: Line split by MPolygons (GeoPolygon2[])", 0, y + 2.5);
                        var subject = new GeoLine2(new GeoPoint2(-5, y), new GeoPoint2(15, y));
                        var poly1 = new GeoPolygon2(new GeoPoint2(0, y - 1), new GeoPoint2(2, y - 1), new GeoPoint2(2, y + 1), new GeoPoint2(0, y + 1));
                        var poly2 = new GeoPolygon2(new GeoPoint2(8, y - 1), new GeoPoint2(10, y - 1), new GeoPoint2(10, y + 1), new GeoPoint2(8, y + 1));
                        AddCutterPolygon(poly1, tr, modelSpace);
                        AddCutterPolygon(poly2, tr, modelSpace);
                        if (subject.TrySplitBy(new[] { poly1, poly2 }, out GeoLine2[] inside, out GeoLine2[] outside))
                        {
                            AddResultInsideOutsideLines(inside, outside, tr, modelSpace);
                        }
                    }

                    // 5. Line_MultiplePolylines (Offset Y = 40)
                    {
                        double y = 40.0;
                        AddComment("TC5: Line split by MPolylines (GeoPolyline2[])", 0, y + 2.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var polyline1 = new GeoPolyline2(new GeoPoint2(3, y - 2), new GeoPoint2(3, y + 2));
                        var polyline2 = new GeoPolyline2(new GeoPoint2(7, y - 2), new GeoPoint2(7, y + 2));
                        AddCutterPolyline(polyline1, tr, modelSpace);
                        AddCutterPolyline(polyline2, tr, modelSpace);
                        if (subject.TrySplitBy(new[] { polyline1, polyline2 }, out GeoLine2[] pieces))
                        {
                            AddResultLines(pieces, tr, modelSpace);
                        }
                    }

                    // 6. Polyline_MultiplePoints (Offset Y = 50)
                    {
                        double y = 50.0;
                        AddComment("TC6: Polyline split by MPoints (GeoPoint2[])", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(5, y), new GeoPoint2(5, y + 5));
                        var points = new[] { new GeoPoint2(2, y), new GeoPoint2(5, y + 3) };
                        foreach (var pt in points) AddCutterPoint(pt, tr, modelSpace);
                        if (subject.TrySplitBy(points, out GeoPolyline2[] pieces))
                        {
                            AddResultPolylines(pieces, tr, modelSpace);
                        }
                    }

                    // 7. Polyline_MultipleLines (Offset Y = 60)
                    {
                        double y = 60.0;
                        AddComment("TC7: Polyline split by MLines (GeoLine2[])", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(5, y), new GeoPoint2(5, y + 5));
                        var cutters = new[]
                        {
                            new GeoLine2(new GeoPoint2(2, y - 1), new GeoPoint2(2, y + 1)),
                            new GeoLine2(new GeoPoint2(4, y + 3), new GeoPoint2(6, y + 3))
                        };
                        foreach (var c in cutters) AddCutterLine(c, tr, modelSpace);
                        if (subject.TrySplitBy(cutters, out GeoPolyline2[] pieces))
                        {
                            AddResultPolylines(pieces, tr, modelSpace);
                        }
                    }

                    // 8. Polyline_MultiplePolylines (Offset Y = 70)
                    {
                        double y = 70.0;
                        AddComment("TC8: Polyline split by MPolylines (GeoPolyline2[])", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var cutters = new[]
                        {
                            new GeoPolyline2(new GeoPoint2(3, y - 2), new GeoPoint2(3, y + 2)),
                            new GeoPolyline2(new GeoPoint2(7, y - 2), new GeoPoint2(7, y + 2))
                        };
                        foreach (var c in cutters) AddCutterPolyline(c, tr, modelSpace);
                        if (subject.TrySplitBy(cutters, out GeoPolyline2[] pieces))
                        {
                            AddResultPolylines(pieces, tr, modelSpace);
                        }
                    }

                    // 9. Polyline_MultiplePolygons (Offset Y = 80)
                    {
                        double y = 80.0;
                        AddComment("TC9: Polyline split by MPolygons (GeoPolygon2[])", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(-5, y), new GeoPoint2(15, y));
                        var poly1 = new GeoPolygon2(new GeoPoint2(0, y - 1), new GeoPoint2(2, y - 1), new GeoPoint2(2, y + 1), new GeoPoint2(0, y + 1));
                        var poly2 = new GeoPolygon2(new GeoPoint2(8, y - 1), new GeoPoint2(10, y - 1), new GeoPoint2(10, y + 1), new GeoPoint2(8, y + 1));
                        AddCutterPolygon(poly1, tr, modelSpace);
                        AddCutterPolygon(poly2, tr, modelSpace);
                        if (subject.TrySplitBy(new[] { poly1, poly2 }, out GeoPolyline2[] inside, out GeoPolyline2[] outside))
                        {
                            AddResultInsideOutsidePolylines(inside, outside, tr, modelSpace);
                        }
                    }

                    // 10. Line_Point (Offset Y = 90)
                    {
                        double y = 90.0;
                        AddComment("TC10: Line split by Point (GeoPoint2)", 0, y + 1.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var pt = new GeoPoint2(4, y);
                        AddCutterPoint(pt, tr, modelSpace);
                        if (Splition2.TrySplitBy(subject, pt, out GeoLine2 first, out GeoLine2 second))
                        {
                            AddResultLinePair(first, second, tr, modelSpace);
                        }
                    }

                    // 11. Polyline_Point (Offset Y = 100)
                    {
                        double y = 100.0;
                        AddComment("TC11: Polyline split by Point (GeoPoint2)", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(5, y), new GeoPoint2(5, y + 5));
                        var pt = new GeoPoint2(3, y);
                        AddCutterPoint(pt, tr, modelSpace);
                        if (Splition2.TrySplitBy(subject, pt, out GeoPolyline2 first, out GeoPolyline2 second))
                        {
                            AddResultPolylinePair(first, second, tr, modelSpace);
                        }
                    }

                    // 12. Line_Line (Offset Y = 110)
                    {
                        double y = 110.0;
                        AddComment("TC12: Line split by Line (GeoLine2)", 0, y + 1.5);
                        var subject = new GeoLine2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var cutter = new GeoLine2(new GeoPoint2(5, y - 2), new GeoPoint2(5, y + 2));
                        AddCutterLine(cutter, tr, modelSpace);
                        if (Splition2.TrySplitBy(subject, cutter, out GeoLine2 first, out GeoLine2 second))
                        {
                            AddResultLinePair(first, second, tr, modelSpace);
                        }
                    }

                    // 13. Polyline_Line (Offset Y = 120)
                    {
                        double y = 120.0;
                        AddComment("TC13: Polyline split by Line (GeoLine2)", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(5, y), new GeoPoint2(5, y + 5));
                        var cutter = new GeoLine2(new GeoPoint2(3, y - 2), new GeoPoint2(3, y + 2));
                        AddCutterLine(cutter, tr, modelSpace);
                        if (subject.TrySplitBy(cutter, out GeoPolyline2[] pieces))
                        {
                            AddResultPolylines(pieces, tr, modelSpace);
                        }
                    }

                    // 14. Line_Polygon (Offset Y = 130)
                    {
                        double y = 130.0;
                        AddComment("TC14: Line split by Polygon (GeoPolygon2)", 0, y + 2.5);
                        var subject = new GeoLine2(new GeoPoint2(-5, y), new GeoPoint2(15, y));
                        var poly = new GeoPolygon2(new GeoPoint2(0, y - 1), new GeoPoint2(4, y - 1), new GeoPoint2(4, y + 1), new GeoPoint2(0, y + 1));
                        AddCutterPolygon(poly, tr, modelSpace);
                        if (Splition2.TrySplitBy(subject, poly, out GeoLine2[] inside, out GeoLine2[] outside))
                        {
                            AddResultInsideOutsideLines(inside, outside, tr, modelSpace);
                        }
                    }

                    // 15. Polyline_Polygon (Offset Y = 140)
                    {
                        double y = 140.0;
                        AddComment("TC15: Polyline split by Polygon (GeoPolygon2)", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(-5, y), new GeoPoint2(15, y));
                        var poly = new GeoPolygon2(new GeoPoint2(0, y - 1), new GeoPoint2(4, y - 1), new GeoPoint2(4, y + 1), new GeoPoint2(0, y + 1));
                        AddCutterPolygon(poly, tr, modelSpace);
                        if (Splition2.TrySplitBy(subject, poly, out GeoPolyline2[] inside, out GeoPolyline2[] outside))
                        {
                            AddResultInsideOutsidePolylines(inside, outside, tr, modelSpace);
                        }
                    }

                    // 16. Polyline_Polyline (Offset Y = 150)
                    {
                        double y = 150.0;
                        AddComment("TC16: Polyline split by Polyline (GeoPolyline2)", 0, y + 2.5);
                        var subject = new GeoPolyline2(new GeoPoint2(0, y), new GeoPoint2(10, y));
                        var cutter = new GeoPolyline2(new GeoPoint2(5, y - 2), new GeoPoint2(5, y + 2));
                        AddCutterPolyline(cutter, tr, modelSpace);
                        if (subject.TrySplitBy(new[] { cutter }, out GeoPolyline2[] pieces))
                        {
                            AddResultPolylines(pieces, tr, modelSpace);
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage("\n-> Giao dịch auto test hoàn tất! Bản vẽ đã được cập nhật.");
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n[LỖI] Lỗi trong quá trình chạy auto test: {ex.Message}\n{ex.StackTrace}");
                    tr.Abort();
                }
            }

            ed.WriteMessage("\n--- KẾT THÚC AUTO TEST ---");
        }
    }
}
