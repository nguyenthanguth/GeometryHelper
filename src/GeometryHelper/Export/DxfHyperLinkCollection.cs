using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace GeometryHelper.Export
{
    /// <summary>
    /// The hyperlinks of an entity or a block, as AutoCAD's <c>HyperLinkCollection</c>: read from the extended data of the
    /// application <c>PE_URL</c> of its object each time they are asked for, and written back into it by every change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>line.Hyperlinks.Add(new DxfHyperLink { Name = "https://example.com/B-12.pdf", Description = "Plate B-12" })</c> gives
    /// the line the extended data <c>1001 PE_URL, 1000 https://example.com/B-12.pdf, 1002 {, 1000 Plate B-12, 1002 }</c>, as
    /// AutoCAD and ezdxf write it, and the table of applications the application PE_URL where it lacks it. The last hyperlink
    /// taken out takes the application's data with it.
    /// </para>
    /// <para>
    /// A hyperlink read from the collection is a copy: one changed is written by setting it back, <c>links[0] = link</c>.
    /// The extended data a file gives in braces beyond the description and the sub-location, as AutoCAD's own flags, is read
    /// past and not written again.
    /// </para>
    /// </remarks>
    public sealed class DxfHyperLinkCollection : IEnumerable<DxfHyperLink>
    {
        private const string Application = "PE_URL";

        private readonly DxfDBObject _owner;

        internal DxfHyperLinkCollection(DxfDBObject owner)
        {
            _owner = owner;
        }

        /// <summary>Gets how many hyperlinks there are, as AutoCAD's <c>Count</c>.</summary>
        public int Count => Read().Count;

        /// <summary>
        /// Gets or sets a hyperlink, as AutoCAD's indexer: a copy of the one at an index; set, that one replaced and written.
        /// </summary>
        /// <param name="index">The index, from nought.</param>
        /// <returns>A copy of the hyperlink.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a hyperlink.</exception>
        /// <exception cref="ArgumentNullException">Thrown when the hyperlink set is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public DxfHyperLink this[int index]
        {
            get
            {
                List<DxfHyperLink> links = Read();
                Check(index, links.Count);
                return links[index];
            }

            set
            {
                Checked(value);
                List<DxfHyperLink> links = Read();
                Check(index, links.Count);

                // The hyperlink it has changes nothing, whatever else the file keeps in its braces.
                if (DxfHyperLink.Same(links[index], value))
                {
                    return;
                }

                links[index] = value;
                Write(links);
            }
        }

        /// <summary>Adds a hyperlink last, as AutoCAD's <c>Add</c>.</summary>
        /// <param name="value">The hyperlink.</param>
        /// <returns>Its index.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the hyperlink is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public int Add(DxfHyperLink value)
        {
            Checked(value);
            List<DxfHyperLink> links = Read();
            links.Add(value);
            Write(links);
            return links.Count - 1;
        }

        /// <summary>Puts a hyperlink in at an index, as AutoCAD's <c>Insert</c>.</summary>
        /// <param name="index">The index it is to have, from nought to <see cref="Count"/>.</param>
        /// <param name="value">The hyperlink.</param>
        /// <exception cref="ArgumentNullException">Thrown when the hyperlink is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is below nought or above <see cref="Count"/>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void Insert(int index, DxfHyperLink value)
        {
            Checked(value);
            List<DxfHyperLink> links = Read();
            Check(index, links.Count + 1);
            links.Insert(index, value);
            Write(links);
        }

        /// <summary>Takes out every hyperlink, the extended data of PE_URL with them, as AutoCAD's <c>Clear</c>.</summary>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void Clear() => Write(new List<DxfHyperLink>());

        /// <summary>Gets whether there is a hyperlink of the same address, description and sub-location, as AutoCAD's <c>Contains</c>.</summary>
        /// <param name="value">The hyperlink.</param>
        /// <returns>Whether there is one.</returns>
        public bool Contains(DxfHyperLink value) => IndexOf(value) >= 0;

        /// <summary>Gets the index of the first hyperlink of the same address, description and sub-location, as AutoCAD's <c>IndexOf</c>.</summary>
        /// <param name="value">The hyperlink.</param>
        /// <returns>Its index; -1 where there is none.</returns>
        public int IndexOf(DxfHyperLink value) => Read().FindIndex(link => DxfHyperLink.Same(link, value));

        /// <summary>Takes out the first hyperlink of the same address, description and sub-location, as AutoCAD's <c>Remove</c>; nothing where there is none.</summary>
        /// <param name="value">The hyperlink.</param>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void Remove(DxfHyperLink value)
        {
            List<DxfHyperLink> links = Read();
            int at = links.FindIndex(link => DxfHyperLink.Same(link, value));

            if (at >= 0)
            {
                links.RemoveAt(at);
                Write(links);
            }
        }

        /// <summary>Takes out the hyperlink at an index, as AutoCAD's <c>RemoveAt</c>.</summary>
        /// <param name="index">The index.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the index is not that of a hyperlink.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the object is erased.</exception>
        public void RemoveAt(int index)
        {
            List<DxfHyperLink> links = Read();
            Check(index, links.Count);
            links.RemoveAt(index);
            Write(links);
        }

        /// <summary>Copies the hyperlinks into an array from an index of it, as AutoCAD's <c>CopyTo</c>.</summary>
        /// <param name="array">The array.</param>
        /// <param name="index">The index of the array the first goes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when the array is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the array has not room for them from the index.</exception>
        public void CopyTo(DxfHyperLink[] array, int index) => Read().CopyTo(array, index);

        /// <summary>Gets an enumerator over copies of the hyperlinks, in their order.</summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<DxfHyperLink> GetEnumerator() => Read().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// The hyperlinks of the extended data of PE_URL: each a 1000, its address, then, in the braces (1002) after it where
        /// there are some, its description and sub-location, the first two 1000 there; braces in those read past.
        /// </summary>
        private List<DxfHyperLink> Read()
        {
            var links = new List<DxfHyperLink>();
            IReadOnlyList<DxfTypedValue> data = _owner.GetXDataForApplication(Application);

            if (data == null)
            {
                return links;
            }

            DxfHyperLink last = null;
            int depth = 0;
            int texts = 0;

            for (int i = 1; i < data.Count; i++)
            {
                DxfTypedValue group = data[i];

                if (group.TypeCode == 1002)
                {
                    string brace = group.Value.Trim();
                    depth = brace == "{" ? depth + 1 : brace == "}" ? Math.Max(0, depth - 1) : depth;

                    if (depth == 1 && brace == "{")
                    {
                        texts = 0;
                    }

                    continue;
                }

                if (group.TypeCode != 1000)
                {
                    continue;
                }

                if (depth == 0)
                {
                    last = new DxfHyperLink { Name = group.Value };
                    links.Add(last);
                }
                else if (depth == 1 && last != null)
                {
                    if (texts == 0)
                    {
                        last.Description = group.Value;
                    }
                    else if (texts == 1)
                    {
                        last.SubLocation = group.Value;
                    }

                    texts++;
                }
            }

            return links;
        }

        /// <summary>Writes the hyperlinks as the extended data of PE_URL, as AutoCAD writes them; none takes the application's data out.</summary>
        private void Write(List<DxfHyperLink> links)
        {
            var data = new List<DxfTypedValue> { new DxfTypedValue(1001, Application) };

            foreach (DxfHyperLink link in links)
            {
                data.Add(new DxfTypedValue(1000, link.Name));

                if (link.Description.Length == 0 && link.SubLocation.Length == 0)
                {
                    continue;
                }

                data.Add(new DxfTypedValue(1002, "{"));
                data.Add(new DxfTypedValue(1000, link.Description));

                if (link.SubLocation.Length > 0)
                {
                    data.Add(new DxfTypedValue(1000, link.SubLocation));
                }

                data.Add(new DxfTypedValue(1002, "}"));
            }

            // Data the same as the object has changes nothing, which the extended data's setter sees to.
            IReadOnlyList<DxfTypedValue> now = _owner.GetXDataForApplication(Application);

            if (links.Count == 0 && now == null)
            {
                return;
            }

            _owner.XData = data;
        }

        private static void Checked(DxfHyperLink value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
        }

        private static void Check(int index, int count)
        {
            if (index < 0 || index >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "The hyperlinks are 0 to " + (count - 1).ToString(CultureInfo.InvariantCulture) + ".");
            }
        }
    }
}
