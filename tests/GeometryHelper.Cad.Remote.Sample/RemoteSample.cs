using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dxf;
using GeometryHelper.Geometry;

namespace GeometryHelper.Cad.Remote.Sample
{
    /// <summary>
    /// What the sample program does with a connected app, apart from finding the app: it lists the drawings, draws a small
    /// framed title on a layer of its own, asks the user to pick a point and draws a circle there. The program's
    /// <c>Main</c> calls it; the remote tests call it with a headless app.
    /// </summary>
    public static class RemoteSample
    {
        /// <summary>The layer the sample draws on.</summary>
        public const string LayerName = "SAMPLE";

        /// <summary>The radius of the circle drawn where the user picks, in drawing units.</summary>
        public const double CircleRadius = 25.0;

        /// <summary>
        /// Draws the title block and the circle in the drawing the user works in, or in a new drawing where the app has none
        /// open, and says what it does on the output.
        /// </summary>
        /// <param name="app">The app, connected.</param>
        /// <param name="output">Where the sample says what it does.</param>
        /// <returns>0 when the circle was drawn, 1 when the user cancelled the pick.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the app or the output is null.</exception>
        /// <exception cref="CadRemoteException">Thrown when the app refuses a request, as the pick of another program is waiting.</exception>
        public static int Run(CadApplication app, TextWriter output)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            // 1. The drawings the app has open.
            foreach (CadRemoteDocument open in app.Documents)
            {
                output.WriteLine("Open: " + open + (open.IsModified ? " (changed)" : string.Empty));
            }

            CadRemoteDocument drawing = app.ActiveDocument ?? app.New();
            output.WriteLine("Drawing on " + drawing.Name + ".");

            // 2. A layer of its own and a framed title on it, all put in the drawing as one undo step.
            drawing.AddLayer(
                new CadRemoteLayer(LayerName) { Color = DxfColor.FromColorIndex(DxfColorMethod.ByAci, 3) }
            );
            drawing.Insert(Frame(0, 0, 200, 60));
            drawing.Insert(new DxfLine(new GeoPoint3(10, 35, 0), new GeoPoint3(190, 35, 0)) { Layer = LayerName });
            drawing.Insert(
                new DxfMText
                {
                    Contents = "GeometryHelper CAD\\Pdriven from another program",
                    Location = new GeoPoint3(10, 50, 0),
                    TextHeight = 8.0,
                    Layer = LayerName,
                }
            );
            IReadOnlyList<string> handles = drawing.CommitChanges();
            output.WriteLine(
                "Drew " + handles.Count + " objects on the layer " + LayerName + ": " + string.Join(", ", handles)
            );

            // 3. The user picks a point on the canvas, the circle goes there.
            try
            {
                GeoPoint3 centre = drawing.PickPoint("Pick the centre of the circle");
                drawing.Insert(new DxfCircle(centre, new GeoVector3(0, 0, 1), CircleRadius) { Layer = LayerName });
                string circle = drawing.CommitChanges()[0];
                output.WriteLine("Drew the circle " + circle + " at " + centre.X + ", " + centre.Y + ".");
                return 0;
            }
            catch (CadRemoteException ex) when (ex.IsCancelled)
            {
                output.WriteLine("The pick was cancelled; no circle drawn.");
                return 1;
            }
        }

        private static DxfPolyline Frame(double x1, double y1, double x2, double y2)
        {
            var frame = new DxfPolyline { Layer = LayerName };
            frame.AddVertexAt(0, new GeoPoint2(x1, y1), 0, 0, 0);
            frame.AddVertexAt(1, new GeoPoint2(x2, y1), 0, 0, 0);
            frame.AddVertexAt(2, new GeoPoint2(x2, y2), 0, 0, 0);
            frame.AddVertexAt(3, new GeoPoint2(x1, y2), 0, 0, 0);
            frame.Closed = true;
            return frame;
        }
    }
}
