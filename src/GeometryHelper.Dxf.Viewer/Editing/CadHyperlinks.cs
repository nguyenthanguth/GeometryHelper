using System;
using System.Collections.Generic;
using System.IO;
using GeometryHelper.Dxf;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>What a hyperlink opens.</summary>
    public enum CadHyperlinkTargetKind
    {
        /// <summary>Nothing: the hyperlink has no address and names no view.</summary>
        None,

        /// <summary>A named view of the drawing the hyperlink is in, which opening restores (View of This Drawing).</summary>
        View,

        /// <summary>A web address, <c>https://example.com/plate.pdf</c>.</summary>
        Web,

        /// <summary>An e-mail address, <c>mailto:detailing@example.com?subject=Plate</c>.</summary>
        Mail,

        /// <summary>A file, its full path made from the drawing's hyperlink base.</summary>
        File,
    }

    /// <summary>Where a hyperlink leads, as <see cref="CadHyperlinks.Resolve"/> works it out.</summary>
    public sealed class CadHyperlinkTarget
    {
        internal CadHyperlinkTarget(CadHyperlinkTargetKind kind, string address, string view)
        {
            Kind = kind;
            Address = address;
            View = view;
        }

        /// <summary>Gets what the hyperlink opens.</summary>
        public CadHyperlinkTargetKind Kind { get; }

        /// <summary>Gets the address to open: the web address or mail address as written, the full path of a file; empty for a view or nothing.</summary>
        public string Address { get; }

        /// <summary>Gets the named view of the drawing the hyperlink restores; empty for every other kind.</summary>
        public string View { get; }
    }

    /// <summary>
    /// The hyperlinks of objects, as AutoCAD 2021 keeps and follows them: kept in the extended data of PE_URL
    /// (<see cref="DxfEntity.Hyperlinks"/>), the address, the text it is shown by and the named place in it; a link to a view of
    /// the drawing has no address and the view as its place; an e-mail address is written <c>mailto:</c> with its subject after
    /// <c>?subject=</c>. These helpers say where a hyperlink leads, how its tooltip reads and which objects have one.
    /// </summary>
    public static class CadHyperlinks
    {
        /// <summary>The longest address, text to display or named place AutoCAD takes: 255 characters.</summary>
        public const int MaxLength = 255;

        /// <summary>The words under a hyperlink's text in the tooltip the cursor shows over its object, as AutoCAD writes them.</summary>
        public const string FollowHint = "CTRL + click to follow link";

        /// <summary>
        /// Says where a hyperlink leads. An address with a scheme (<c>https:</c>, <c>ftp:</c>) or starting <c>www.</c> is a web
        /// address, one starting <c>mailto:</c> a mail address; no address with a place named is a view of the drawing; any other
        /// address is a file, a relative path one made from the drawing's hyperlink base (<see cref="DxfDatabase.HyperlinkBase"/>)
        /// or, where the base is empty, the folder of the drawing's file.
        /// </summary>
        /// <param name="document">The document the hyperlink is in.</param>
        /// <param name="link">The hyperlink.</param>
        /// <returns>Where it leads.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the document or the hyperlink is null.</exception>
        public static CadHyperlinkTarget Resolve(CadDocument document, DxfHyperLink link)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (link == null)
            {
                throw new ArgumentNullException(nameof(link));
            }

            string name = link.Name.Trim();

            if (name.Length == 0)
            {
                return link.SubLocation.Length > 0
                    ? new CadHyperlinkTarget(CadHyperlinkTargetKind.View, string.Empty, link.SubLocation)
                    : new CadHyperlinkTarget(CadHyperlinkTargetKind.None, string.Empty, string.Empty);
            }

            if (name.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                return new CadHyperlinkTarget(CadHyperlinkTargetKind.Mail, name, string.Empty);
            }

            if (IsWeb(name))
            {
                return new CadHyperlinkTarget(
                    CadHyperlinkTargetKind.Web,
                    name.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? "http://" + name : name,
                    string.Empty
                );
            }

            return new CadHyperlinkTarget(CadHyperlinkTargetKind.File, FullPath(document, name), string.Empty);
        }

        /// <summary>
        /// Gets the text of the tooltip the cursor shows over an object with a hyperlink: what the first hyperlink is shown by (its
        /// text, else its address), then <see cref="FollowHint"/> on the next line; null for an object with none.
        /// </summary>
        /// <param name="entity">The object.</param>
        /// <returns>The tooltip, such as <c>Plate B-12&#10;CTRL + click to follow link</c>; null for none.</returns>
        public static string Tooltip(DxfEntity entity)
        {
            DxfHyperLink first = First(entity);
            return first == null ? null : first.DisplayString + "\n" + FollowHint;
        }

        /// <summary>Gets the first hyperlink of an object, as Ctrl+click follows it; null for an object with none, or none given.</summary>
        /// <param name="entity">The object.</param>
        /// <returns>The hyperlink, a copy.</returns>
        public static DxfHyperLink First(DxfEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            DxfHyperLinkCollection links = entity.Hyperlinks;
            return links.Count > 0 ? links[0] : null;
        }

        /// <summary>
        /// Makes the hyperlink the dialogs and -HYPERLINK write from what they ask: the address, the named place in it and the text
        /// it is shown by. An empty address with a place is a link to a view of the drawing.
        /// </summary>
        /// <param name="address">The address; empty for the current drawing.</param>
        /// <param name="place">The named place; empty for none.</param>
        /// <param name="text">The text to display; empty for none, the address then showing.</param>
        /// <returns>The hyperlink.</returns>
        public static DxfHyperLink Make(string address, string place, string text) =>
            new DxfHyperLink
            {
                Name = (address ?? string.Empty).Trim(),
                SubLocation = (place ?? string.Empty).Trim(),
                Description = (text ?? string.Empty).Trim(),
            };

        /// <summary>The message AutoCAD gives for an address, text or place longer than <see cref="MaxLength"/>; null where they all fit.</summary>
        internal static string TooLong(DxfHyperLink link)
        {
            if (link.Name.Length > MaxLength)
            {
                return "The URL you entered contains too many characters. URL string is currently limited to 255 characters in length.";
            }

            if (link.Description.Length > MaxLength)
            {
                return "The text you entered in \"Text to display\" field contains too many characters. This text is currently limited to 255 characters in length.";
            }

            return link.SubLocation.Length > MaxLength
                ? "The location you specified contains too many characters. Location string is currently limited to 255 characters in length."
                : null;
        }

        private static bool IsWeb(string name)
        {
            if (name.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            int colon = name.IndexOf(':');

            // "C:\plans" is a drive, not a scheme: a scheme is two letters at least, and the :// follows it.
            return colon > 1 && name.IndexOf("://", StringComparison.Ordinal) == colon;
        }

        private static string FullPath(CadDocument document, string name)
        {
            try
            {
                if (Path.IsPathRooted(name))
                {
                    return Path.GetFullPath(name);
                }

                string root = document.Database.HyperlinkBase;

                if (string.IsNullOrWhiteSpace(root))
                {
                    root = string.IsNullOrEmpty(document.FileName)
                        ? string.Empty
                        : Path.GetDirectoryName(document.FileName) ?? string.Empty;
                }

                if (root.IndexOf("://", StringComparison.Ordinal) > 0)
                {
                    return root.TrimEnd('/') + "/" + name.Replace('\\', '/');
                }

                return root.Length == 0 ? name : Path.GetFullPath(Path.Combine(root, name));
            }
            catch (Exception exception)
                when (exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is PathTooLongException
                )
            {
                return name;
            }
        }

        /// <summary>The objects of a list that have a hyperlink, in the order given.</summary>
        internal static List<DxfEntity> WithLinks(CadDocument document, IReadOnlyList<string> handles)
        {
            var found = new List<DxfEntity>();

            foreach (string handle in handles)
            {
                DxfEntity entity = document.FindEntity(handle);

                if (entity != null && entity.Hyperlinks.Count > 0)
                {
                    found.Add(entity);
                }
            }

            return found;
        }
    }
}
