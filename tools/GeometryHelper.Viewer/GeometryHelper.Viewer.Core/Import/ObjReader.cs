using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace GeometryHelper.Viewer
{
    /// <summary>
    /// Reads a Wavefront OBJ file: the vertices (<c>v</c>) and what names them, the faces (<c>f</c>), the lines (<c>l</c>) and the points
    /// (<c>p</c>), each object (<c>o</c>) or group (<c>g</c>) an item of its own. Texture coordinates, normals, materials and smoothing
    /// groups are not geometry and are left out, said once each.
    /// </summary>
    internal static class ObjReader
    {
        private const string DefaultLayer = "obj";

        private sealed class ObjObject
        {
            internal string Name;
            internal string Layer;
            internal readonly List<int[]> Faces = new List<int[]>();
            internal readonly List<int[]> Lines = new List<int[]>();
            internal readonly List<int> Points = new List<int>();

            internal bool IsEmpty => Faces.Count == 0 && Lines.Count == 0 && Points.Count == 0;
        }

        internal static Scene Read(string text, string title, CancellationToken cancel)
        {
            var b = new ImportBuilder(title);
            var vertices = new List<Vec3>();
            var ignored = new Dictionary<string, int>(StringComparer.Ordinal);
            var ignoredOrder = new List<string>();
            var current = new ObjObject { Name = "shape", Layer = DefaultLayer };
            string group = DefaultLayer;
            int objects = 0;
            int lineNumber = 0;
            int colouredVertices = 0;

            using (var reader = new StringReader(text))
            {
                string line;

                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;

                    if ((lineNumber & 4095) == 0)
                    {
                        cancel.ThrowIfCancellationRequested();
                    }

                    // A backslash at the end of a line joins it to the next.
                    while (line.EndsWith("\\", StringComparison.Ordinal))
                    {
                        string next = reader.ReadLine();
                        lineNumber++;
                        line = line.Substring(0, line.Length - 1) + " " + (next ?? string.Empty);
                    }

                    line = line.Trim();

                    if (line.Length == 0 || line[0] == '#')
                    {
                        continue;
                    }

                    string[] words = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    string command = words[0];

                    switch (command)
                    {
                        case "v":
                        {
                            if (words.Length < 4 || !ImportNumber.TryParse(words[1], out double x) || !ImportNumber.TryParse(words[2], out double y) || !ImportNumber.TryParse(words[3], out double z))
                            {
                                b.Warn("OBJ line " + lineNumber.ToString(CultureInfo.InvariantCulture) + ": a vertex without three numbers was read as the origin, so that the numbers of the vertices after it stay right");
                                vertices.Add(default(Vec3));
                                break;
                            }

                            // x y z w, or x y z r g b: the extra numbers are a weight or a colour, not a place.
                            if (words.Length >= 7)
                            {
                                colouredVertices++;
                            }

                            vertices.Add(new Vec3(x, y, z));
                            break;
                        }

                        case "o":
                        case "g":
                        {
                            Finish(b, current, vertices, cancel);
                            string name = string.Join(" ", words.Skip(1));
                            objects++;

                            if (command == "g")
                            {
                                group = name.Length == 0 ? DefaultLayer : name;
                                current = new ObjObject { Name = name.Length == 0 ? "group" + objects.ToString(CultureInfo.InvariantCulture) : name, Layer = group };
                            }
                            else
                            {
                                current = new ObjObject { Name = name.Length == 0 ? "shape" + objects.ToString(CultureInfo.InvariantCulture) : name, Layer = group };
                            }

                            break;
                        }

                        case "f":
                        case "l":
                        case "p":
                        {
                            int[] indexes = Indexes(words, vertices.Count, out string problem);

                            if (problem != null)
                            {
                                b.Warn("OBJ line " + lineNumber.ToString(CultureInfo.InvariantCulture) + ": a '" + command + "' statement was left out: " + problem);
                                break;
                            }

                            if (command == "f")
                            {
                                if (indexes.Length < 3)
                                {
                                    b.Warn("OBJ: a face with fewer than three corners was left out");
                                }
                                else
                                {
                                    current.Faces.Add(indexes);
                                }
                            }
                            else if (command == "l")
                            {
                                if (indexes.Length < 2)
                                {
                                    b.Warn("OBJ: a line with fewer than two vertices was left out");
                                }
                                else
                                {
                                    current.Lines.Add(indexes);
                                }
                            }
                            else
                            {
                                current.Points.AddRange(indexes);
                            }

                            break;
                        }

                        default:
                            if (!ignored.ContainsKey(command))
                            {
                                ignored.Add(command, 0);
                                ignoredOrder.Add(command);
                            }

                            ignored[command]++;
                            break;
                    }
                }
            }

            Finish(b, current, vertices, cancel);

            foreach (string command in ignoredOrder)
            {
                b.Warn("OBJ: " + ignored[command].ToString(CultureInfo.InvariantCulture) + " '" + command + "' statement(s) are not geometry or not drawn and were left out");
            }

            if (colouredVertices > 0)
            {
                b.Warn("OBJ: " + colouredVertices.ToString(CultureInfo.InvariantCulture) + " vertex(es) carry a weight or a colour after the three coordinates, which are left out");
            }

            return b.Finish();
        }

        /// <summary>
        /// Reads the vertex numbers of a statement: 3, 3/1, 3//2 and 3/1/2 all name the third vertex, and a negative number counts back from
        /// the last vertex read so far.
        /// </summary>
        private static int[] Indexes(string[] words, int count, out string problem)
        {
            var indexes = new int[words.Length - 1];
            problem = null;

            for (int i = 1; i < words.Length; i++)
            {
                string word = words[i];
                int slash = word.IndexOf('/');
                string head = slash < 0 ? word : word.Substring(0, slash);

                if (!int.TryParse(head, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int n) || n == 0)
                {
                    problem = "'" + word + "' is not a vertex number";
                    return indexes;
                }

                int at = n > 0 ? n - 1 : count + n;

                if (at < 0 || at >= count)
                {
                    problem = "vertex " + head + " is not there (" + count.ToString(CultureInfo.InvariantCulture) + " read so far)";
                    return indexes;
                }

                indexes[i - 1] = at;
            }

            return indexes;
        }

        /// <summary>
        /// Turns what an object collected into items: its faces one mesh for each body they make, each line a line or a chain, each point a point. The corners of a mesh
        /// are the vertices its faces name, in the order the file has them.
        /// </summary>
        private static void Finish(ImportBuilder b, ObjObject o, List<Vec3> vertices, CancellationToken cancel)
        {
            if (o.IsEmpty)
            {
                return;
            }

            int made = 0;

            string NameFor() => ++made == 1 ? o.Name : o.Name + " " + made.ToString(CultureInfo.InvariantCulture);

            if (o.Faces.Count > 0)
            {
                var used = new List<int>();

                foreach (int[] face in o.Faces)
                {
                    used.AddRange(face);
                }

                used.Sort();
                int[] distinct = used.Distinct().ToArray();
                var points = new List<Vec3>(distinct.Length);

                foreach (int index in distinct)
                {
                    points.Add(vertices[index]);
                }

                var faces = new List<int[]>(o.Faces.Count);

                foreach (int[] face in o.Faces)
                {
                    var local = new int[face.Length];

                    for (int k = 0; k < face.Length; k++)
                    {
                        local[k] = Array.BinarySearch(distinct, face[k]);
                    }

                    faces.Add(local);
                }

                // The faces of one object may be several bodies, or a body with a void in it; they are told apart by their edges, as a DXF's are.
                // Only closed shells are split off: the faces that do not close are one item, at the place of the first of them, so that fifty loose triangles
                // are one item and not fifty.
                var openFaces = new List<int>();
                bool openEmitted = false;
                List<ShellPiece> pieces = MeshShells.Split(points, faces, b.Scene.EqualPoint, cancel);

                foreach (ShellPiece piece in pieces)
                {
                    if (!piece.Closed)
                    {
                        openFaces.AddRange(piece.Faces);
                    }
                }

                openFaces.Sort();

                foreach (ShellPiece piece in pieces)
                {
                    List<int> chosen;

                    if (piece.Closed)
                    {
                        chosen = piece.Faces;
                    }
                    else if (!openEmitted)
                    {
                        openEmitted = true;
                        chosen = openFaces;
                    }
                    else
                    {
                        continue;
                    }

                    MeshShells.Extract(points, faces, chosen, true, out List<Vec3> corners, out List<int[]> taken);
                    b.Add("GeoMesh3", ImportData.Mesh(corners, taken, 3), o.Layer, NameFor(), null, "OBJ f");
                }
            }

            foreach (int[] line in o.Lines)
            {
                Vec3[] chain = line.Select(i => vertices[i]).ToArray();
                bool segment = chain.Length == 2;
                b.Add(segment ? "GeoLine3" : "GeoPolyline3", segment ? ImportData.Line(chain[0], chain[1], 3) : ImportData.Chain(chain, 3), o.Layer, NameFor(), null, "OBJ l");
            }

            foreach (int index in o.Points)
            {
                b.Add("GeoPoint3", ImportData.Point(vertices[index], 3), o.Layer, NameFor(), null, "OBJ p");
            }
        }
    }
}
