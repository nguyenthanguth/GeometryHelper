using System;
using System.Collections.Generic;
using System.Linq;
using GeometryHelper.Geometry;
using Xunit;

namespace GeometryHelper.Viewer.Tests
{
    /// <summary>
    /// A clip plane that the box round an item lies wholly on one side of leaves the item whole or takes it whole without the item being looked at: the result has to be
    /// what looking at it gives, to the bit, and the plane has to meet the few items that it does.
    /// </summary>
    public class ClipBoxShortcutTests
    {
        private static GeoSolid3 Box(double x, double y, double z, double size)
        {
            var floor = new GeoPolygon3(new GeoPoint3(x, y, z), new GeoPoint3(x + size, y, z), new GeoPoint3(x + size, y + size, z), new GeoPoint3(x, y + size, z));
            return GeoSolid3.Extrude(floor, new GeoVector3(0, 0, size));
        }

        private static Scene FieldOfBoxes()
        {
            var writer = new SceneWriter();

            for (int i = 0; i < 6; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    writer.Add(Box(i * 12.0, j * 12.0, 0.0, 10.0), name: "box " + i + " " + j);
                }
            }

            writer.Add(new GeoLine3(new GeoPoint3(-20, 30, 5), new GeoPoint3(100, 30, 5)), name: "line");
            writer.Add(new GeoPoint3(30, 30, 30), name: "point");
            writer.Add(new GeoCircle3(new GeoPoint3(36, 24, 5), new GeoVector3(0, 0, 1), 15.0), name: "circle");
            writer.Add(new GeoRay3(new GeoPoint3(0, 0, 0), new GeoVector3(1, 1, 1)), name: "ray");
            writer.Add(new GeoPlane3(new GeoPoint3(30, 30, 20), new GeoVector3(0, 0, 1)), name: "plane");
            writer.Text(new GeoPoint3(60, 60, 5), "label");
            return writer.Scene;
        }

        private static void AssertSame(ItemPrimitives expected, ItemPrimitives actual)
        {
            Assert.Equal(expected.ItemIndex, actual.ItemIndex);
            Assert.Equal(expected.Points, actual.Points);
            Assert.Equal(expected.Labels.Select(l => l.At), actual.Labels.Select(l => l.At));
            Assert.Equal(expected.Polylines.Count, actual.Polylines.Count);

            for (int i = 0; i < expected.Polylines.Count; i++)
            {
                Assert.Equal(expected.Polylines[i].Closed, actual.Polylines[i].Closed);
                Assert.Equal(expected.Polylines[i].Points, actual.Polylines[i].Points);
            }

            Assert.Equal(expected.Triangles.Count, actual.Triangles.Count);

            for (int i = 0; i < expected.Triangles.Count; i++)
            {
                Assert.Equal(expected.Triangles[i].A, actual.Triangles[i].A);
                Assert.Equal(expected.Triangles[i].B, actual.Triangles[i].B);
                Assert.Equal(expected.Triangles[i].C, actual.Triangles[i].C);
                Assert.Equal(expected.Triangles[i].Normal, actual.Triangles[i].Normal);
            }
        }

        [Fact]
        public void TheResultOfCuttingAFieldOfItems_IsTheSame_WhetherOrNotTheBoxesAreLookedAt_ForManyPlanes()
        {
            Primitives primitives = Primitives.Build(FieldOfBoxes());
            Primitives blind = Primitives.Build(FieldOfBoxes());

            foreach (ItemPrimitives item in blind.Items)
            {
                item.BoundsCoverAll = false;
            }

            var random = new Random(77);
            int whole = 0;
            int cutAway = 0;

            for (int trial = 0; trial < 300; trial++)
            {
                var planes = new List<ClipPlane>();
                int count = 1 + random.Next(3);

                for (int k = 0; k < count; k++)
                {
                    // Planes through the corners of the boxes, to the bit, and planes anywhere.
                    Vec3 through = trial % 2 == 0
                        ? new Vec3(random.Next(0, 8) * 12.0 - (random.Next(2) == 0 ? 2.0 : 0.0), random.Next(0, 6) * 12.0, random.Next(0, 3) * 5.0)
                        : new Vec3(random.NextDouble() * 80.0, random.NextDouble() * 70.0, random.NextDouble() * 15.0);
                    Vec3 normal = trial % 3 == 0 ? new Vec3(k == 0 ? 1.0 : 0.0, k == 1 ? 1.0 : 0.0, k == 2 ? -1.0 : 0.0) : new Vec3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, random.NextDouble() - 0.5);

                    if (normal.Length < 1E-3)
                    {
                        normal = new Vec3(1, 0, 0);
                    }

                    planes.Add(new ClipPlane(through, normal.Normalized(), random.Next(4) == 0 ? random.NextDouble() * 4.0 - 2.0 : 0.0));
                }

                ClipSet clips = new ClipSet(planes).ForScene(primitives.Bounds);

                for (int i = 0; i < primitives.Items.Count; i++)
                {
                    ItemPrimitives fast = clips.Apply(primitives.Items[i]);
                    ItemPrimitives slow = clips.Apply(blind.Items[i]);
                    AssertSame(slow, fast);

                    if (ReferenceEquals(fast, primitives.Items[i]))
                    {
                        whole++;
                    }
                    else if (fast.Triangles.Count == 0 && fast.Polylines.Count == 0 && fast.Points.Count == 0 && fast.Labels.Count == 0)
                    {
                        cutAway++;
                    }
                }

                ClipResult withBoxes = ClipResult.Build(primitives, clips, true);
                ClipResult withoutBoxes = ClipResult.Build(blind, clips, true);

                for (int i = 0; i < primitives.Items.Count; i++)
                {
                    Assert.Equal(withoutBoxes.Sections[i].Count, withBoxes.Sections[i].Count);

                    for (int s = 0; s < withBoxes.Sections[i].Count; s++)
                    {
                        Assert.Equal(withoutBoxes.Sections[i][s].Points, withBoxes.Sections[i][s].Points);
                        Assert.Equal(withoutBoxes.Sections[i][s].Closed, withBoxes.Sections[i][s].Closed);
                    }

                    Assert.Equal(withoutBoxes.Caps[i].Count, withBoxes.Caps[i].Count);
                }
            }

            // The comparison has to have been about items that were kept whole and cut away whole, a good many of each.
            Assert.True(whole > 1000, whole + " items kept whole");
            Assert.True(cutAway > 500, cutAway + " items cut away whole");
        }

        [Fact]
        public void ABigItemCutOnEveryCore_IsTheSameAsItsPartsCutOneAfterAnother_ToTheBit()
        {
            // A cylinder of 20 000 sides has 80 000 triangles, more than are cut in pieces; its two halves are each under that, and are cut one core at a time.
            var scene = new SceneWriter().Add(GeoSolid3.Cylinder(new GeoPoint3(0, 0, 0), new GeoPoint3(0, 0, 100), 5000, 20000), name: "drum").Scene;
            ItemPrimitives whole = Primitives.Build(scene).Items[0];
            Assert.True(whole.Triangles.Count > 50000, whole.Triangles.Count + " triangles");

            var first = new ItemPrimitives { Bounds = whole.Bounds };
            var second = new ItemPrimitives { Bounds = whole.Bounds };
            first.Triangles.AddRange(whole.Triangles.Take(whole.Triangles.Count / 2));
            second.Triangles.AddRange(whole.Triangles.Skip(whole.Triangles.Count / 2));

            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(1234.5, 0, 30), new Vec3(0.3, 0.4, 0.8).Normalized()) }).ForScene(whole.Bounds);
            ItemPrimitives together = clips.Apply(whole);
            ItemPrimitives apart = clips.Apply(first);
            apart.Triangles.AddRange(clips.Apply(second).Triangles);

            Assert.True(together.Triangles.Count > 1000);
            Assert.Equal(apart.Triangles.Count, together.Triangles.Count);

            for (int i = 0; i < apart.Triangles.Count; i++)
            {
                Assert.Equal(apart.Triangles[i].A, together.Triangles[i].A);
                Assert.Equal(apart.Triangles[i].B, together.Triangles[i].B);
                Assert.Equal(apart.Triangles[i].C, together.Triangles[i].C);
                Assert.Equal(apart.Triangles[i].Normal, together.Triangles[i].Normal);
            }
        }

        [Fact]
        public void APlaneThroughOneBoxOfAField_IsGivenTheOtherItemsAsTheyAre_AndOnlyThatBoxIsCopied()
        {
            Primitives primitives = Primitives.Build(FieldOfBoxes());
            var clips = new ClipSet(new[] { new ClipPlane(new Vec3(5, 0, 0), new Vec3(1, 0, 0)) }).ForScene(primitives.Bounds);

            Primitives cut = clips.Apply(primitives);

            // The boxes of the first column, x from 0 to 10, are met by the plane at 5; the boxes beyond it are whole and the line, the circle and the rest are looked at.
            int copied = 0;

            for (int i = 0; i < primitives.Items.Count; i++)
            {
                if (!ReferenceEquals(cut.Items[i], primitives.Items[i]))
                {
                    copied++;
                }
            }

            Assert.True(copied < primitives.Items.Count - 20, copied + " of " + primitives.Items.Count + " items copied");
        }
    }
}
