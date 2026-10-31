using System;
using System.Collections.Generic;
using System.Globalization;
using GeometryHelper.Export;
using GeometryHelper.Geometry;

namespace GeometryHelper.Dxf
{
    /// <summary>
    /// Writes the parameters and actions of a dynamic block made in code, in the form AutoCAD writes them and AutoCAD 2021 read
    /// back as dynamic (plugin v17): the BLOCK_RECORD's extended data (AcDbDynamicBlockTrueName, AcDbDynamicBlockGUID,
    /// AcDbBlockRepETag) and extension dictionary (ACAD_ENHANCEDBLOCK naming the ACAD_EVALUATION_GRAPH,
    /// AcDbDynamicBlockRoundTripPurgePreventer), a tag of AcDbBlockRepETag on each entity of the block, the nodes of the graph
    /// with their edges, and the classes in the CLASSES section.
    /// </summary>
    internal static class DxfDynamicBlockWriter
    {
        // The version of each node's object (98 and 99 of AcDbEvalExpr and AcDbBlockElement), as AutoCAD's files of R2010 hold it.
        private const string Major = "33";
        private const string Minor = "197";

        // The value of a grip location component of an end grip before AutoCAD evaluates it, as AutoCAD writes it.
        private const string Unset = "1.797693134862314E+99";

        /// <summary>
        /// Writes a linear parameter with its two grips, each with its two location components: seven nodes, and the six edges
        /// AutoCAD writes for them (the parameter to each component, each grip to the parameter).
        /// </summary>
        internal static DxfBlockLinearParameter AddLinear(DxfBlockTableRecord block, string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint, double labelOffset, DxfBlockParameterValueSet valueSet)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Linear");
            GeoVector3 along = basePoint.GetVectorTo(endPoint);

            DxfItem parameter = file.AddObject("BLOCKLINEARPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, basePoint);
                Point(b, 1011, endPoint);
                b.Group(170, "4");
                b.Group(91, Int(p + 4));
                b.Group(91, Int(p + 1));
                b.Group(91, "0");
                b.Group(91, "0");
                Connection(b, 171, 92, p + 4, "DisplacementX");
                Connection(b, 172, 93, p + 4, "DisplacementY");
                Connection(b, 173, 94, p + 1, "DisplacementX");
                Connection(b, 174, 95, p + 1, "DisplacementY");
                b.Group(177, "0");
                b.Group(100, "AcDbBlockLinearParameter");
                b.Group(305, name);
                b.Group(306, description);
                b.Group(140, Real(labelOffset));
                b.Group(307, string.Empty);
                Values(b, valueSet);
            });

            // The label stands off the middle of the line to its left, looking from the base to the end, by the offset.
            double flat = Math.Sqrt(along.X * along.X + along.Y * along.Y);
            GeoPoint3 middle = basePoint.GetMiddlePoint(endPoint);
            GeoPoint3 label = flat > 0.0 ? new GeoPoint3(middle.X - along.Y / flat * labelOffset, middle.Y + along.X / flat * labelOffset, middle.Z) : middle;
            DxfDBObject.OfObject(parameter).XData = new[]
            {
                new DxfTypedValue(1001, "ACAUTHENVIRON"), new DxfTypedValue(1010, Real(label.X)), new DxfTypedValue(1020, Real(label.Y)), new DxfTypedValue(1030, Real(label.Z)),
            };

            graph.Add(p, parameter.Handle);
            Grip(graph, p, p + 1, "End Grip", endPoint, along, Unset, "UpdatedEnd");
            Grip(graph, p, p + 4, "Base Grip", basePoint, new GeoVector3(-along.X, -along.Y, -along.Z), "0.0", "UpdatedBase");
            graph.Link(p, p + 2, 1);
            graph.Link(p, p + 3, 1);
            graph.Link(p + 1, p, 2);
            graph.Link(p, p + 5, 1);
            graph.Link(p, p + 6, 1);
            graph.Link(p + 4, p, 2);
            graph.Write();

            file.AddClass("BLOCKLINEARPARAMETER", "AcDbBlockLinearParameter", 1153);
            file.AddClass("BLOCKLINEARGRIP", "AcDbBlockLinearGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockLinearParameter(block, p, parameter.Handle, name, description, basePoint, endPoint, labelOffset, valueSet);
        }

        /// <summary>
        /// Writes a point parameter with its grip and the grip's two location components: four nodes, and the three edges
        /// AutoCAD writes for them (the parameter to each component, the grip to the parameter), as AutoCAD saved the point
        /// parameter of ACadSharp's sample; the label at the point.
        /// </summary>
        internal static DxfBlockPointParameter AddPoint(DxfBlockTableRecord block, string name, string description, GeoPoint3 location)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Point");

            DxfItem parameter = file.AddObject("BLOCKPOINTPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock1PtParameter");
                Point(b, 1010, location);
                b.Group(93, Int(p + 1));
                b.Group(170, "1");
                b.Group(91, Int(p + 1));
                b.Group(301, "DisplacementX");
                b.Group(171, "1");
                b.Group(92, Int(p + 1));
                b.Group(302, "DisplacementY");
                b.Group(100, "AcDbBlockPointParameter");
                b.Group(303, name);
                b.Group(304, description);
                Point(b, 1011, location);
            });

            Environ(parameter, location);
            graph.Add(p, parameter.Handle);
            GripOf(graph, "BLOCKXYGRIP", "AcDbBlockXYGrip", p + 1, "Grip", location, "1", null);
            Components(graph, p, p + 1, "0.0", "Updated");
            graph.Link(p, p + 2, 1);
            graph.Link(p, p + 3, 1);
            graph.Link(p + 1, p, 2);
            graph.Write();

            file.AddClass("BLOCKPOINTPARAMETER", "AcDbBlockPointParameter", 1153);
            file.AddClass("BLOCKXYGRIP", "AcDbBlockXYGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockPointParameter(block, p, parameter.Handle, name, description, location);
        }

        /// <summary>
        /// Writes a rotation parameter with its grip at the end point and the grip's two location components: four nodes, and
        /// the three edges AutoCAD writes for them, as AutoCAD saved the rotation parameter of ACadSharp's sample: the end point
        /// at the radius and the angle from the base point, the base angle point at the radius along the block's x axis, the
        /// angle's set of none (flags 3, nought to a whole turn).
        /// </summary>
        internal static DxfBlockRotationParameter AddRotation(DxfBlockTableRecord block, string name, string description, GeoPoint3 basePoint, double radius, double angle)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Rotation");
            var endPoint = new GeoPoint3(basePoint.X + radius * Math.Cos(angle), basePoint.Y + radius * Math.Sin(angle), basePoint.Z);
            var baseAngle = new GeoPoint3(basePoint.X + radius, basePoint.Y, basePoint.Z);

            DxfItem parameter = file.AddObject("BLOCKROTATIONPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, basePoint);
                Point(b, 1011, endPoint);
                b.Group(170, "4");
                b.Group(91, "0");
                b.Group(91, Int(p + 1));
                b.Group(91, "0");
                b.Group(91, "0");
                b.Group(171, "0");
                b.Group(172, "0");
                Connection(b, 173, 94, p + 1, "DisplacementX");
                Connection(b, 174, 95, p + 1, "DisplacementY");
                b.Group(177, "0");
                b.Group(100, "AcDbBlockRotationParameter");
                b.Group(305, name);
                b.Group(306, description);
                Point(b, 1011, baseAngle);
                b.Group(140, "0.0");
                b.Group(307, string.Empty);
                b.Group(96, "3");
                b.Group(141, "0.0");
                b.Group(142, Real(2.0 * Math.PI));
                b.Group(143, "0.0");
                b.Group(175, "0");
            });

            Environ(parameter, baseAngle);
            graph.Add(p, parameter.Handle);
            GripOf(graph, "BLOCKROTATIONGRIP", "AcDbBlockRotationGrip", p + 1, "Grip", endPoint, "0", null);
            Components(graph, p, p + 1, Unset, "UpdatedEnd");
            graph.Link(p, p + 2, 1);
            graph.Link(p, p + 3, 1);
            graph.Link(p + 1, p, 2);
            graph.Write();

            file.AddClass("BLOCKROTATIONPARAMETER", "AcDbBlockRotationParameter", 1153);
            file.AddClass("BLOCKROTATIONGRIP", "AcDbBlockRotationGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockRotationParameter(block, p, parameter.Handle, name, description, basePoint, radius, angle);
        }

        /// <summary>
        /// Writes a flip parameter with its grip and the grip's three components (the state, the base point's x and y): five
        /// nodes, and the four edges AutoCAD writes for them, as AutoCAD saved the flip parameter of ACadSharp's sample: the
        /// reflection line from the base point to the end point, the states labelled Not flipped and Flipped, the label and
        /// the grip at the line's end point.
        /// </summary>
        internal static DxfBlockFlipParameter AddFlip(DxfBlockTableRecord block, string name, string description, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Flip");
            GeoVector3 along = basePoint.GetVectorTo(endPoint);

            DxfItem parameter = file.AddObject("BLOCKFLIPPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, basePoint);
                Point(b, 1011, endPoint);
                b.Group(170, "4");
                b.Group(91, Int(p + 1));
                b.Group(91, "0");
                b.Group(91, "0");
                b.Group(91, "0");
                b.Group(171, "0");
                b.Group(172, "0");
                b.Group(173, "0");
                b.Group(174, "0");
                b.Group(177, "0");
                b.Group(100, "AcDbBlockFlipParameter");
                b.Group(305, name);
                b.Group(306, description);
                b.Group(307, "Not flipped");
                b.Group(308, "Flipped");
                Point(b, 1012, endPoint);
                b.Group(309, "UpdatedFlip");
                b.Group(96, "2");
            });

            Environ(parameter, endPoint);
            graph.Add(p, parameter.Handle);
            GripOf(graph, "BLOCKFLIPGRIP", "AcDbBlockFlipGrip", p + 1, "Grip", endPoint, "0", b =>
            {
                b.Group(140, Real(along.X));
                b.Group(141, Real(along.Y));
                b.Group(142, Real(along.Z));
                b.Group(93, Int(p + 2));
            }, p + 3, p + 4);

            DxfItem state = file.AddObject("BLOCKGRIPLOCATIONCOMPONENT", null, b =>
            {
                Head(b, graph.Handle, p + 2, null);
                b.Group(1, string.Empty);
                b.Group(70, "70");
                b.Group(70, "0");
                b.Group(100, "AcDbBlockGripExpr");
                b.Group(91, Int(p));
                b.Group(300, "UpdatedFlip");
            });

            graph.Add(p + 2, state.Handle);
            Component(graph, p, p + 3, "0.0", "UpdatedBaseX");
            Component(graph, p, p + 4, "0.0", "UpdatedBaseY");
            graph.Link(p, p + 3, 1);
            graph.Link(p, p + 4, 1);
            graph.Link(p, p + 2, 1);
            graph.Link(p + 1, p, 1);
            graph.Write();

            file.AddClass("BLOCKFLIPPARAMETER", "AcDbBlockFlipParameter", 1153);
            file.AddClass("BLOCKFLIPGRIP", "AcDbBlockFlipGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockFlipParameter(block, p, parameter.Handle, name, description, basePoint, endPoint);
        }

        /// <summary>
        /// Writes an XY parameter with its four grips (the end, the base, base x with end y, end x with base y), each with its
        /// two location components: thirteen nodes, and the twelve edges AutoCAD writes for them, as AutoCAD saved the XY
        /// parameter of ACadSharp's sample: the labels of y and x at 305 and 306, their descriptions at 307 and 308, the label
        /// offsets nought, the sets of none.
        /// </summary>
        internal static DxfBlockXYParameter AddXY(DxfBlockTableRecord block, string xName, string xDescription, string yName, string yDescription, GeoPoint3 basePoint, GeoPoint3 endPoint)
        {
            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("XY");

            DxfItem parameter = file.AddObject("BLOCKXYPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock2PtParameter");
                Point(b, 1010, basePoint);
                Point(b, 1011, endPoint);
                b.Group(170, "4");
                b.Group(91, Int(p + 4));
                b.Group(91, Int(p + 1));
                b.Group(91, Int(p + 7));
                b.Group(91, Int(p + 10));
                Connections(b, 171, 92, p + 4, p + 7, "DisplacementX");
                Connections(b, 172, 93, p + 4, p + 10, "DisplacementY");
                Connections(b, 173, 94, p + 1, p + 10, "DisplacementX");
                Connections(b, 174, 95, p + 1, p + 7, "DisplacementY");
                b.Group(177, "0");
                b.Group(100, "AcDbBlockXYParameter");
                b.Group(305, yName);
                b.Group(306, xName);
                b.Group(307, yDescription);
                b.Group(308, xDescription);
                b.Group(141, "0.0");
                b.Group(140, "0.0");
                b.Group(309, string.Empty);
                b.Group(97, "1");
                b.Group(146, "0.0");
                b.Group(147, "0.0");
                b.Group(148, "0.0");
                b.Group(176, "0");
                b.Group(410, string.Empty);
                b.Group(96, "1");
                b.Group(142, "0.0");
                b.Group(143, "0.0");
                b.Group(144, "0.0");
                b.Group(175, "0");
            });

            Environ(parameter, endPoint);
            graph.Add(p, parameter.Handle);
            GripOf(graph, "BLOCKXYGRIP", "AcDbBlockXYGrip", p + 1, "End Grip", endPoint, "1", null);
            Components(graph, p, p + 1, Unset, "UpdatedEnd");
            GripOf(graph, "BLOCKXYGRIP", "AcDbBlockXYGrip", p + 4, "Base Grip", basePoint, "1", null);
            Components(graph, p, p + 4, "0.0", "UpdatedBase");
            GripOf(graph, "BLOCKXYGRIP", "AcDbBlockXYGrip", p + 7, "BaseX-EndY Grip", new GeoPoint3(basePoint.X, endPoint.Y, basePoint.Z), "1", null);
            Component(graph, p, p + 8, "0.0", "UpdatedBaseX");
            Component(graph, p, p + 9, Unset, "UpdatedEndY");
            GripOf(graph, "BLOCKXYGRIP", "AcDbBlockXYGrip", p + 10, "BaseY-EndX Grip", new GeoPoint3(endPoint.X, basePoint.Y, basePoint.Z), "1", null);
            Component(graph, p, p + 11, Unset, "UpdatedEndX");
            Component(graph, p, p + 12, "0.0", "UpdatedBaseY");

            for (int grip = p + 1; grip <= p + 10; grip += 3)
            {
                graph.Link(p, grip + 1, 1);
                graph.Link(p, grip + 2, 1);
                graph.Link(grip, p, 2);
            }

            graph.Write();
            file.AddClass("BLOCKXYPARAMETER", "AcDbBlockXYParameter", 1153);
            file.AddClass("BLOCKXYGRIP", "AcDbBlockXYGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
            return new DxfBlockXYParameter(block, p, parameter.Handle, xName, xDescription, yName, yDescription, basePoint, endPoint);
        }

        /// <summary>
        /// Writes a stretch action driven by the end point of a linear or an XY parameter (EndXDelta, EndYDelta): its frame of
        /// two corners, and each entity with the indices of its points inside the frame; one node, and the edge from the
        /// parameter.
        /// </summary>
        internal static void AddStretch(DxfBlockTableRecord block, int node, string name, GeoPoint2 low, GeoPoint2 high, List<KeyValuePair<DxfEntity, List<int>>> held)
        {
            Graph graph = Graph.Of(block);
            Known(graph, block, node, name);
            DxfFile file = graph.File;
            int s = graph.NextId;
            string element = graph.FreeName("Stretch");
            DxfItem action = file.AddObject("BLOCKSTRETCHACTION", null, b =>
            {
                Head(b, graph.Handle, s, element);
                b.Group(100, "AcDbBlockAction");
                b.Group(70, "0");
                b.Group(71, Int(held.Count));

                foreach (KeyValuePair<DxfEntity, List<int>> entity in held)
                {
                    b.Group(330, entity.Key.Handle);
                }

                Point(b, 1010, new GeoPoint3((low.X + high.X) / 2.0, (low.Y + high.Y) / 2.0, 0.0));
                b.Group(100, "AcDbBlockStretchAction");
                b.Group(92, Int(node));
                b.Group(301, "EndXDelta");
                b.Group(93, Int(node));
                b.Group(302, "EndYDelta");
                b.Group(72, "2");
                b.Group(1011, Real(low.X));
                b.Group(1021, Real(low.Y));
                b.Group(1011, Real(high.X));
                b.Group(1021, Real(high.Y));
                b.Group(73, Int(held.Count));

                foreach (KeyValuePair<DxfEntity, List<int>> entity in held)
                {
                    b.Group(331, entity.Key.Handle);
                    b.Group(74, Int(entity.Value.Count));
                    entity.Value.ForEach(i => b.Group(94, Int(i)));
                }

                b.Group(75, "0");
                b.Group(140, "1.0");
                b.Group(141, "0.0");
                b.Group(280, "0");
            });

            graph.Add(s, action.Handle);
            graph.Link(node, s, 2);
            graph.Write();
            file.AddClass("BLOCKSTRETCHACTION", "AcDbBlockStretchAction", 1153);
        }

        /// <summary>
        /// Writes a move action driven by a parameter's connections (EndXDelta and EndYDelta of a linear or an XY parameter,
        /// XDelta and YDelta of a point parameter), as AutoCAD saved the move of ACadSharp's point sample: the entities it
        /// moves, its distance multiplier 1 and angle offset nought; one node, and the edge from the parameter.
        /// </summary>
        internal static void AddMove(DxfBlockTableRecord block, int node, string name, string x, string y, GeoPoint3 at, List<DxfEntity> entities)
        {
            Graph graph = Graph.Of(block);
            Known(graph, block, node, name);
            int s = graph.NextId;
            string element = graph.FreeName("Move");
            DxfItem action = graph.File.AddObject("BLOCKMOVEACTION", null, b =>
            {
                ActionHead(b, graph, s, element, entities, at);
                b.Group(100, "AcDbBlockMoveAction");
                b.Group(92, Int(node));
                b.Group(301, x);
                b.Group(93, Int(node));
                b.Group(302, y);
                b.Group(140, "1.0");
                b.Group(141, "0.0");
                b.Group(280, "0");
            });

            graph.Add(s, action.Handle);
            graph.Link(node, s, 2);
            graph.Write();
            graph.File.AddClass("BLOCKMOVEACTION", "AcDbBlockMoveAction", 1153);
        }

        /// <summary>
        /// Writes a scale action of a linear parameter, as AutoCAD saved the scale of ACadSharp's linear sample: its base the
        /// parameter's base point (dependent, of no offset), its connections Scale, XScale and YScale; one node, and the edge
        /// from the parameter (of five connections).
        /// </summary>
        internal static void AddScale(DxfBlockLinearParameter parameter, List<DxfEntity> entities)
        {
            Graph graph = Graph.Of(parameter.Block);
            Known(graph, parameter.Block, parameter.Node, parameter.Name);
            int s = graph.NextId;
            string element = graph.FreeName("Scale");
            int p = parameter.Node;
            DxfItem action = graph.File.AddObject("BLOCKSCALEACTION", null, b =>
            {
                ActionHead(b, graph, s, element, entities, parameter.EndPoint);
                WithBase(b, p);
                b.Group(100, "AcDbBlockScaleAction");
                b.Group(94, Int(p));
                b.Group(95, Int(p));
                b.Group(96, Int(p));
                b.Group(303, "Scale");
                b.Group(304, "XScale");
                b.Group(305, "YScale");
            });

            graph.Add(s, action.Handle);
            graph.Link(p, s, 5);
            graph.Write();
            graph.File.AddClass("BLOCKSCALEACTION", "AcDbBlockScaleAction", 1153);
        }

        /// <summary>
        /// Writes a rotate action of a rotation parameter, as AutoCAD saved the rotate of ACadSharp's rotation sample: its base
        /// the parameter's base point (dependent, of no offset), its connection AngleDelta; one node, and the edge from the
        /// parameter (of three connections).
        /// </summary>
        internal static void AddRotate(DxfBlockRotationParameter parameter, List<DxfEntity> entities)
        {
            Graph graph = Graph.Of(parameter.Block);
            Known(graph, parameter.Block, parameter.Node, parameter.Name);
            int s = graph.NextId;
            string element = graph.FreeName("Rotate");
            int p = parameter.Node;
            DxfItem action = graph.File.AddObject("BLOCKROTATEACTION", null, b =>
            {
                ActionHead(b, graph, s, element, entities, parameter.BasePoint);
                WithBase(b, p);
                b.Group(100, "AcDbBlockRotationAction");
                b.Group(94, Int(p));
                b.Group(303, "AngleDelta");
            });

            graph.Add(s, action.Handle);
            graph.Link(p, s, 3);
            graph.Write();
            graph.File.AddClass("BLOCKROTATEACTION", "AcDbBlockRotateAction", 1153);
        }

        /// <summary>
        /// Writes a flip action of a flip parameter, as AutoCAD saved the flip of ACadSharp's sample: its connections Flip,
        /// UpdatedFlip, UpdatedBase and UpdatedEnd; one node, and the edge from the parameter (of four connections).
        /// </summary>
        internal static void AddFlipAction(DxfBlockFlipParameter parameter, List<DxfEntity> entities)
        {
            Graph graph = Graph.Of(parameter.Block);
            Known(graph, parameter.Block, parameter.Node, parameter.Name);
            int s = graph.NextId;
            string element = graph.FreeName("Flip");
            int p = parameter.Node;
            DxfItem action = graph.File.AddObject("BLOCKFLIPACTION", null, b =>
            {
                ActionHead(b, graph, s, element, entities, parameter.BasePoint.GetMiddlePoint(parameter.EndPoint));
                b.Group(100, "AcDbBlockFlipAction");
                b.Group(92, Int(p));
                b.Group(93, Int(p));
                b.Group(94, Int(p));
                b.Group(95, Int(p));
                b.Group(301, "Flip");
                b.Group(302, "UpdatedFlip");
                b.Group(303, "UpdatedBase");
                b.Group(304, "UpdatedEnd");
            });

            graph.Add(s, action.Handle);
            graph.Link(p, s, 4);
            graph.Write();
            graph.File.AddClass("BLOCKFLIPACTION", "AcDbBlockFlipAction", 1153);
        }

        /// <summary>Refuses a parameter whose node is no longer in the block's evaluation graph.</summary>
        private static void Known(Graph graph, DxfBlockTableRecord block, int node, string name)
        {
            if (!graph.HasNode(node))
            {
                throw new ArgumentException("The parameter " + name + " is no longer in the evaluation graph of the block " + block.Name + ".", "parameter");
            }
        }

        /// <summary>The head of an action: its node's head, then the entities it works on (330) and where it is shown (1010).</summary>
        private static void ActionHead(DxfFile.Builder b, Graph graph, int id, string element, List<DxfEntity> entities, GeoPoint3 at)
        {
            Head(b, graph.Handle, id, element);
            b.Group(100, "AcDbBlockAction");
            b.Group(70, "0");
            b.Group(71, Int(entities.Count));
            entities.ForEach(e => b.Group(330, e.Handle));
            Point(b, 1010, at);
        }

        /// <summary>
        /// The base of a scale or a rotate action: the parameter's base point (UpdatedBaseX, UpdatedBaseY), of no offset (1011),
        /// dependent (280 of 1), and 1012 nought, as AutoCAD's samples write it: AutoCAD 2021 adds 1012 to the parameter's base
        /// point, and turned an arrow about (100, 50) where 1012 was the base point (50, 25) itself (plugin v20, 24d-f).
        /// </summary>
        private static void WithBase(DxfFile.Builder b, int parameter)
        {
            b.Group(100, "AcDbBlockActionWithBasePt");
            b.Group(92, Int(parameter));
            b.Group(93, Int(parameter));
            b.Group(301, "UpdatedBaseX");
            b.Group(302, "UpdatedBaseY");
            Point(b, 1011, new GeoPoint3(0, 0, 0));
            b.Group(280, "1");
            Point(b, 1012, new GeoPoint3(0, 0, 0));
        }

        /// <summary>The extended data AutoCAD ends a parameter with: ACAUTHENVIRON and the point its label or action bar is shown at.</summary>
        private static void Environ(DxfItem parameter, GeoPoint3 at)
        {
            DxfDBObject.OfObject(parameter).XData = new[]
            {
                new DxfTypedValue(1001, "ACAUTHENVIRON"), new DxfTypedValue(1010, Real(at.X)), new DxfTypedValue(1020, Real(at.Y)), new DxfTypedValue(1030, Real(at.Z)),
            };
        }

        /// <summary>
        /// Writes a visibility parameter with its grip and the grip's two location components: four nodes, and the three edges
        /// AutoCAD writes for them (the grip to the parameter, the parameter to each component), as AutoCAD 2021 saved the
        /// visibility parameter of ACadSharp's sample. The entities the states name are governed, in the order of the block;
        /// each of them not shown in the first state is made invisible, as AutoCAD keeps the block in that state.
        /// </summary>
        internal static void AddVisibility(DxfBlockTableRecord block, string name, string description, GeoPoint3 location, List<DxfBlockVisibilityState> states)
        {
            if (Has(block, "BLOCKVISIBILITYPARAMETER"))
            {
                throw new InvalidOperationException("The block " + block.Name + " has a visibility parameter already, and AutoCAD's Block Editor gives a block one.");
            }

            var named = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            states.ForEach(s => named.UnionWith(Handles(s)));
            var governed = new List<DxfEntity>();

            foreach (DxfEntity entity in block)
            {
                if (named.Contains(entity.Handle))
                {
                    governed.Add(entity);
                }
            }

            Graph graph = Graph.Of(block);
            DxfFile file = graph.File;
            int p = graph.NextId;
            string element = graph.FreeName("Visibility State");
            HashSet<string> first = Handles(states[0]);
            governed.ForEach(e => e.Visible = first.Contains(e.Handle));

            DxfItem parameter = file.AddObject("BLOCKVISIBILITYPARAMETER", null, b =>
            {
                Head(b, graph.Handle, p, element);
                b.Group(100, "AcDbBlockParameter");
                b.Group(280, "1");
                b.Group(281, "0");
                b.Group(100, "AcDbBlock1PtParameter");
                Point(b, 1010, location);
                b.Group(93, Int(p + 1));
                b.Group(170, "0");
                b.Group(171, "0");
                b.Group(100, "AcDbBlockVisibilityParameter");
                b.Group(281, "1");
                b.Group(301, name);
                b.Group(302, description);
                b.Group(91, "0");
                b.Group(93, Int(governed.Count));
                governed.ForEach(e => b.Group(331, e.Handle));
                b.Group(92, Int(states.Count));

                foreach (DxfBlockVisibilityState state in states)
                {
                    HashSet<string> shown = Handles(state);
                    List<DxfEntity> visible = governed.FindAll(e => shown.Contains(e.Handle));
                    b.Group(303, state.Name);
                    b.Group(94, Int(visible.Count));
                    visible.ForEach(e => b.Group(332, e.Handle));
                    b.Group(95, "0");
                }
            });

            graph.Add(p, parameter.Handle);
            DxfItem grip = file.AddObject("BLOCKVISIBILITYGRIP", null, b =>
            {
                Head(b, graph.Handle, p + 1, "Grip");
                b.Group(100, "AcDbBlockGrip");
                b.Group(91, Int(p + 2));
                b.Group(92, Int(p + 3));
                Point(b, 1010, location);
                b.Group(280, "0");
                b.Group(93, "-1");
                b.Group(100, "AcDbBlockVisibilityGrip");
            });

            graph.Add(p + 1, grip.Handle);
            Components(graph, p, p + 1, "0.0", "Updated");
            graph.Link(p + 1, p, 1);
            graph.Link(p, p + 2, 1);
            graph.Link(p, p + 3, 1);
            graph.Write();

            file.AddClass("BLOCKVISIBILITYPARAMETER", "AcDbBlockVisibilityParameter", 1153);
            file.AddClass("BLOCKVISIBILITYGRIP", "AcDbBlockVisibilityGrip", 1153);
            file.AddClass("BLOCKGRIPLOCATIONCOMPONENT", "AcDbBlockGripExpr", 1153);
        }

        /// <summary>The handles of the entities a state shows.</summary>
        private static HashSet<string> Handles(DxfBlockVisibilityState state)
        {
            var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (DxfEntity entity in state.Entities)
            {
                handles.Add(entity.Handle);
            }

            return handles;
        }

        /// <summary>
        /// The handles of the entities the actions of a block's evaluation graph work on (the 330s of each AcDbBlockAction): of
        /// its flip actions, or of its other actions; read without writing anything, none where the block has no graph.
        /// </summary>
        internal static HashSet<string> ActedOn(DxfBlockTableRecord block, bool flips)
        {
            var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            DxfDBDictionary dictionary = block.ExtensionDictionary;
            DxfItem item = dictionary != null && dictionary.Contains("ACAD_ENHANCEDBLOCK") ? dictionary.GetAt("ACAD_ENHANCEDBLOCK")?.Item : null;

            if (item == null || item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return handles;
            }

            foreach (DxfTypedValue group in item.Groups)
            {
                DxfItem node = group.TypeCode == 360 ? item.File.ObjectItem(group.Value) : null;

                if (node != null && node.Type.EndsWith("ACTION", StringComparison.Ordinal) && (node.Type == "BLOCKFLIPACTION") == flips)
                {
                    foreach (DxfTypedValue acted in DxfDynamicProperties.Section(node.Groups, "AcDbBlockAction"))
                    {
                        if (acted.TypeCode == 330)
                        {
                            handles.Add(acted.Value?.Trim());
                        }
                    }
                }
            }

            return handles;
        }

        /// <summary>Whether the evaluation graph of a block holds a node of a type, read without writing anything.</summary>
        private static bool Has(DxfBlockTableRecord block, string type)
        {
            DxfDBDictionary dictionary = block.ExtensionDictionary;
            DxfItem item = dictionary != null && dictionary.Contains("ACAD_ENHANCEDBLOCK") ? dictionary.GetAt("ACAD_ENHANCEDBLOCK")?.Item : null;

            if (item == null || item.Type != "ACAD_EVALUATION_GRAPH")
            {
                return false;
            }

            foreach (DxfTypedValue group in item.Groups)
            {
                if (group.TypeCode == 360 && item.File.ObjectItem(group.Value)?.Type == type)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A grip of a linear parameter and its two location components, the nodes after it.</summary>
        private static void Grip(Graph graph, int parameter, int id, string name, GeoPoint3 at, GeoVector3 direction, string value, string connection)
        {
            DxfItem grip = graph.File.AddObject("BLOCKLINEARGRIP", null, b =>
            {
                Head(b, graph.Handle, id, name);
                b.Group(100, "AcDbBlockGrip");
                b.Group(91, Int(id + 1));
                b.Group(92, Int(id + 2));
                Point(b, 1010, at);
                b.Group(280, "1");
                b.Group(93, "-1");
                b.Group(100, "AcDbBlockLinearGrip");
                b.Group(140, Real(direction.X));
                b.Group(141, Real(direction.Y));
                b.Group(142, Real(direction.Z));
            });

            graph.Add(id, grip.Handle);
            Components(graph, parameter, id, value, connection);
        }

        /// <summary>
        /// A grip of a type other than linear: its two location components (by default the nodes after it), its location, its
        /// cycling flag (280) and what its own subclass holds.
        /// </summary>
        private static void GripOf(Graph graph, string type, string subclass, int id, string name, GeoPoint3 at, string cycling, Action<DxfFile.Builder> tail, int x = -1, int y = -1)
        {
            DxfItem grip = graph.File.AddObject(type, null, b =>
            {
                Head(b, graph.Handle, id, name);
                b.Group(100, "AcDbBlockGrip");
                b.Group(91, Int(x < 0 ? id + 1 : x));
                b.Group(92, Int(y < 0 ? id + 2 : y));
                Point(b, 1010, at);
                b.Group(280, cycling);
                b.Group(93, "-1");
                b.Group(100, subclass);
                tail?.Invoke(b);
            });

            graph.Add(id, grip.Handle);
        }

        /// <summary>The two location components of a grip, X and Y, the nodes after it: each of a value and of a connection with X or Y after it.</summary>
        private static void Components(Graph graph, int parameter, int id, string value, string connection)
        {
            Component(graph, parameter, id + 1, value, connection + "X");
            Component(graph, parameter, id + 2, value, connection + "Y");
        }

        /// <summary>A location component of a grip: its value, and the parameter's connection it follows.</summary>
        private static void Component(Graph graph, int parameter, int id, string value, string connection)
        {
            DxfItem component = graph.File.AddObject("BLOCKGRIPLOCATIONCOMPONENT", null, b =>
            {
                Head(b, graph.Handle, id, null);
                b.Group(1, string.Empty);
                b.Group(70, "40");
                b.Group(140, value);
                b.Group(100, "AcDbBlockGripExpr");
                b.Group(91, Int(parameter));
                b.Group(300, connection);
            });

            graph.Add(id, component.Handle);
        }

        /// <summary>Two connections of a parameter's point to grips: their count (2), then each grip's id and what it gives.</summary>
        private static void Connections(DxfFile.Builder b, int count, int node, int first, int second, string connection)
        {
            b.Group(count, "2");
            b.Group(node, Int(first));
            b.Group(count + 130, connection);
            b.Group(node, Int(second));
            b.Group(count + 130, connection);
        }

        /// <summary>The head of a node's object: its owner, the graph, its id, and its name where it is a named element.</summary>
        private static void Head(DxfFile.Builder b, string graph, int id, string element)
        {
            b.Group(330, graph);
            b.Group(100, "AcDbEvalExpr");
            b.Group(90, Int(id));
            b.Group(98, Major);
            b.Group(99, Minor);

            if (element != null)
            {
                b.Group(100, "AcDbBlockElement");
                b.Group(300, element);
                b.Group(98, Major);
                b.Group(99, Minor);
                b.Group(1071, "0");
            }
        }

        /// <summary>A connection of a parameter's point to a grip: its count (1), the grip's id and what it gives.</summary>
        private static void Connection(DxfFile.Builder b, int count, int node, int grip, string connection)
        {
            b.Group(count, "1");
            b.Group(node, Int(grip));
            b.Group(count + 130, connection);
        }

        /// <summary>
        /// The value set of a distance: its flags (96), minimum, maximum and increment (141 to 143), and the list counted by 175;
        /// the flags 1 and noughts where there is none, as AutoCAD writes a set of None.
        /// </summary>
        private static void Values(DxfFile.Builder b, DxfBlockParameterValueSet set)
        {
            b.Group(96, set == null ? "1" : set.IsList ? "8" : "7");
            b.Group(141, Real(set?.Minimum ?? 0.0));
            b.Group(142, Real(set?.Maximum ?? 0.0));
            b.Group(143, Real(set?.Increment ?? 0.0));
            IReadOnlyList<double> list = set?.Values ?? new double[0];
            b.Group(175, Int(list.Count));

            foreach (double value in list)
            {
                b.Group(144, Real(value));
            }
        }

        private static void Point(DxfFile.Builder b, int code, GeoPoint3 point)
        {
            b.Group(code, Real(point.X));
            b.Group(code + 10, Real(point.Y));
            b.Group(code + 20, Real(point.Z));
        }

        private static string Real(double value) => Numbers.Exact(value + 0.0);

        private static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// The evaluation graph of a dynamic block, its nodes and edges as its record holds them, to which nodes and edges are
        /// added and the record written anew: the four links of each node (its first and last edge in, its first and last edge
        /// out) and the five of each edge (the edges before and after it into the same node and out of the same node, then -1)
        /// worked out from the order of the edges, as AutoCAD writes them.
        /// </summary>
        private sealed class Graph
        {
            private readonly DxfItem _item;
            private readonly List<int[]> _nodes = new List<int[]>();          // flags, id
            private readonly List<string> _handles = new List<string>();
            private readonly List<int[]> _edges = new List<int[]>();          // flags, kind, from index, to index
            private readonly HashSet<string> _names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private int _last;

            private Graph(DxfFile file, DxfItem item)
            {
                File = file;
                _item = item;
            }

            internal DxfFile File { get; }

            internal string Handle => _item.Handle;

            /// <summary>The id the next node is given: one above the last the graph gave (96) and every id of its nodes.</summary>
            internal int NextId
            {
                get
                {
                    int last = _last;
                    _nodes.ForEach(n => last = Math.Max(last, n[1]));
                    return last + 1;
                }
            }

            /// <summary>
            /// The graph of a block, made where it has none: the BLOCK_RECORD given its extension dictionary with the graph and
            /// the purge preventer, and its extended data of the dynamic block (its true name and a new GUID) where it has none;
            /// each entity of the block without a tag given the next, and the record the tag after the last.
            /// </summary>
            internal static Graph Of(DxfBlockTableRecord block)
            {
                DxfFile file = block.File;
                block.CreateExtensionDictionary();
                DxfDBDictionary dictionary = block.ExtensionDictionary;
                DxfItem item = dictionary.Contains("ACAD_ENHANCEDBLOCK") ? dictionary.GetAt("ACAD_ENHANCEDBLOCK")?.Item : null;

                if (item == null || item.Type != "ACAD_EVALUATION_GRAPH")
                {
                    item = dictionary.Add("ACAD_ENHANCEDBLOCK", "ACAD_EVALUATION_GRAPH", b =>
                    {
                        b.Group(100, "AcDbEvalGraph");
                        b.Group(96, "0");
                        b.Group(97, "0");
                    });

                    if (!dictionary.Contains("AcDbDynamicBlockRoundTripPurgePreventer"))
                    {
                        dictionary.Add("AcDbDynamicBlockRoundTripPurgePreventer", "ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION", b =>
                        {
                            b.Group(100, "AcDbDynamicBlockPurgePreventer");
                            b.Group(70, "1");
                        });
                    }

                    file.AddClass("ACAD_EVALUATION_GRAPH", "AcDbEvalGraph", 1153);
                    file.AddClass("ACDB_DYNAMICBLOCKPURGEPREVENTER_VERSION", "AcDbDynamicBlockPurgePreventer", 1153);
                }

                Tag(block);
                var graph = new Graph(file, item);
                graph.Read();
                return graph;
            }

            /// <summary>
            /// The extended data of the dynamic block on its BLOCK_RECORD, and a tag on each of its entities that has none: the
            /// application AcDbBlockRepETag with 1070 1, the tag (1071) and the entity's own handle (1005), as AutoCAD tags them;
            /// the record's 1071 the tag after the last.
            /// </summary>
            private static void Tag(DxfBlockTableRecord block)
            {
                int next = Tagged(block.GetXDataForApplication("AcDbBlockRepETag"));
                var untagged = new List<DxfEntity>();

                foreach (DxfEntity entity in block)
                {
                    IReadOnlyList<DxfTypedValue> tag = entity.GetXDataForApplication("AcDbBlockRepETag");

                    if (tag == null)
                    {
                        untagged.Add(entity);
                    }
                    else
                    {
                        next = Math.Max(next, Tagged(tag) + 1);
                    }
                }

                foreach (DxfEntity entity in untagged)
                {
                    entity.XData = new[] { new DxfTypedValue(1001, "AcDbBlockRepETag"), new DxfTypedValue(1070, "1"), new DxfTypedValue(1071, Int(next++)), new DxfTypedValue(1005, entity.Handle) };
                }

                var data = new List<DxfTypedValue>();

                if (block.GetXDataForApplication("AcDbDynamicBlockTrueName") == null)
                {
                    data.Add(new DxfTypedValue(1001, "AcDbDynamicBlockTrueName"));
                    data.Add(new DxfTypedValue(1000, block.Name));
                }

                if (block.GetXDataForApplication("AcDbDynamicBlockGUID") == null)
                {
                    data.Add(new DxfTypedValue(1001, "AcDbDynamicBlockGUID"));
                    data.Add(new DxfTypedValue(1000, Guid.NewGuid().ToString("B", CultureInfo.InvariantCulture).ToUpperInvariant()));
                }

                data.Add(new DxfTypedValue(1001, "AcDbBlockRepETag"));
                data.Add(new DxfTypedValue(1070, "1"));
                data.Add(new DxfTypedValue(1071, Int(next)));
                block.XData = data;
            }

            /// <summary>The tag of an application's data of AcDbBlockRepETag (its 1071); nought where it has none.</summary>
            private static int Tagged(IReadOnlyList<DxfTypedValue> data)
            {
                foreach (DxfTypedValue group in data ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0])
                {
                    if (group.TypeCode == 1071 && int.TryParse(group.Value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int tag))
                    {
                        return tag;
                    }
                }

                return 0;
            }

            /// <summary>Whether a node of the id is in the graph.</summary>
            internal bool HasNode(int id) => _nodes.Exists(n => n[1] == id);

            /// <summary>The first of a name, then of the name with 1, 2 and on after it, that no element of the graph has, as AutoCAD names its parameters and actions.</summary>
            internal string FreeName(string name)
            {
                string free = name;

                for (int i = 1; _names.Contains(free); i++)
                {
                    free = name + i.ToString(CultureInfo.InvariantCulture);
                }

                _names.Add(free);
                return free;
            }

            internal void Add(int id, string handle)
            {
                _nodes.Add(new[] { 32, id });
                _handles.Add(handle);
            }

            /// <summary>An edge from one node to another, by their ids, of a kind (94): 1 to a grip's component, 2 to a parameter or an action.</summary>
            internal void Link(int from, int to, int kind) => _edges.Add(new[] { 0, kind, _nodes.FindIndex(n => n[1] == from), _nodes.FindIndex(n => n[1] == to) });

            /// <summary>Reads the nodes and edges the record holds, and the names of the elements of its nodes.</summary>
            private void Read()
            {
                List<DxfTypedValue> groups = File.TailOf(_item, 0);
                int at = groups.FindIndex(g => g.TypeCode == 100 && g.Value?.Trim() == "AcDbEvalGraph") + 1;

                if (at < groups.Count && groups[at].TypeCode == 96)
                {
                    _last = Number(groups[at].Value);
                }

                while (at < groups.Count && (groups[at].TypeCode == 96 || groups[at].TypeCode == 97))
                {
                    at++;
                }

                while (at + 7 < groups.Count && groups[at].TypeCode == 91)
                {
                    _nodes.Add(new[] { Number(groups[at + 1].Value), Number(groups[at + 2].Value) });
                    _handles.Add(groups[at + 3].Value?.Trim());
                    at += 8;
                }

                while (at + 9 < groups.Count && groups[at].TypeCode == 92)
                {
                    _edges.Add(new[] { Number(groups[at + 1].Value), Number(groups[at + 2].Value), Number(groups[at + 3].Value), Number(groups[at + 4].Value) });
                    at += 10;
                }

                foreach (string handle in _handles)
                {
                    DxfItem node = File.ObjectItem(handle);
                    IReadOnlyList<DxfTypedValue> own = node?.Groups ?? (IReadOnlyList<DxfTypedValue>)new DxfTypedValue[0];
                    int marker = -1;

                    for (int i = 0; i < own.Count; i++)
                    {
                        if (own[i].TypeCode == 100)
                        {
                            marker = own[i].Value?.Trim() == "AcDbBlockElement" ? i : -1;
                        }
                        else if (marker >= 0 && own[i].TypeCode == 300)
                        {
                            _names.Add(DxfReader.NameOf(own[i].Value ?? string.Empty));
                            break;
                        }
                    }
                }
            }

            /// <summary>Writes the record anew after its subclass marker: the last id given, twice, then the nodes and the edges.</summary>
            internal void Write()
            {
                List<DxfTypedValue> groups = File.TailOf(_item, 0);
                int from = groups.FindIndex(g => g.TypeCode == 100 && g.Value?.Trim() == "AcDbEvalGraph") + 1;
                var written = new List<KeyValuePair<int, string>>();
                string last = Int(NextId - 1);
                written.Add(Pair(96, last));
                written.Add(Pair(97, last));

                for (int i = 0; i < _nodes.Count; i++)
                {
                    List<int> into = Edges(3, i);
                    List<int> outOf = Edges(2, i);
                    written.Add(Pair(91, Int(i)));
                    written.Add(Pair(93, Int(_nodes[i][0])));
                    written.Add(Pair(95, Int(_nodes[i][1])));
                    written.Add(Pair(360, _handles[i]));
                    written.Add(Pair(92, Int(into.Count > 0 ? into[0] : -1)));
                    written.Add(Pair(92, Int(into.Count > 0 ? into[into.Count - 1] : -1)));
                    written.Add(Pair(92, Int(outOf.Count > 0 ? outOf[0] : -1)));
                    written.Add(Pair(92, Int(outOf.Count > 0 ? outOf[outOf.Count - 1] : -1)));
                }

                for (int k = 0; k < _edges.Count; k++)
                {
                    int[] edge = _edges[k];
                    List<int> into = Edges(3, edge[3]);
                    List<int> outOf = Edges(2, edge[2]);
                    int i = into.IndexOf(k);
                    int o = outOf.IndexOf(k);
                    written.Add(Pair(92, Int(k)));
                    written.Add(Pair(93, Int(edge[0])));
                    written.Add(Pair(94, Int(edge[1])));
                    written.Add(Pair(91, Int(edge[2])));
                    written.Add(Pair(91, Int(edge[3])));
                    written.Add(Pair(92, Int(i > 0 ? into[i - 1] : -1)));
                    written.Add(Pair(92, Int(i + 1 < into.Count ? into[i + 1] : -1)));
                    written.Add(Pair(92, Int(o > 0 ? outOf[o - 1] : -1)));
                    written.Add(Pair(92, Int(o + 1 < outOf.Count ? outOf[o + 1] : -1)));
                    written.Add(Pair(92, "-1"));
                }

                File.ReplaceTail(_item, from, written);
            }

            /// <summary>The indices of the edges whose end of a place (2 from, 3 to) is a node, in the order of the edges.</summary>
            private List<int> Edges(int end, int node)
            {
                var found = new List<int>();

                for (int k = 0; k < _edges.Count; k++)
                {
                    if (_edges[k][end] == node)
                    {
                        found.Add(k);
                    }
                }

                return found;
            }

            private static KeyValuePair<int, string> Pair(int code, string value) => new KeyValuePair<int, string>(code, value);

            private static int Number(string text) => int.TryParse(text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : 0;
        }
    }
}
