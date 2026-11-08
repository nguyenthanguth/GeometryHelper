using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace GeometryHelper.Dxf.Viewer
{
    /// <summary>
    /// The mark of an owned entity: the name of its owner, the id of the object it draws and, where the owner wants one,
    /// a second id, kept in the entity's extended data under the application <see cref="ApplicationName"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A line of the front view of a model drawing drawing the beam B-12 is tagged
    /// <c>new CadOwnerTag("GeometryHelper.Drawing", "B-12", "V-1").WriteTo(line)</c>, and its extended data reads
    /// <c>1001 GH_OWNER</c>, <c>1000 GeometryHelper.Drawing</c>, <c>1000 B-12</c>, <c>1000 V-1</c>; with no second id the last
    /// group is left out. The application is registered in the drawing's table of applications when the tag is written.
    /// AutoCAD keeps the data and ignores it, so a tagged drawing opens there as any other.
    /// </para>
    /// <para>
    /// Each text is written so that it reads back exactly as it was given: a per cent sign, a backslash, a line break or
    /// another control character is written as <c>%</c> and its two hexadecimal digits (<c>%25</c>, <c>%5C</c>, <c>%0A</c>),
    /// and half of a surrogate pair standing alone as <c>%u</c> and four (<c>%uD800</c>); every other letter, accented
    /// letters among them, is written as it is. Without that, a backslash followed by <c>U+0041</c> would read back as an
    /// <c>A</c>. Each text, so written, is at most 255 characters, as extended data holds one.
    /// </para>
    /// </remarks>
    public sealed class CadOwnerTag
    {
        /// <summary>The name of the application whose extended data holds the tag: GH_OWNER.</summary>
        public const string ApplicationName = "GH_OWNER";

        /// <summary>The most characters one text of the tag is written with, as extended data holds a string (group 1000).</summary>
        private const int LongestText = 255;

        /// <summary>Makes a tag.</summary>
        /// <param name="owner">The owner's name, as its <see cref="ICadEntityOwner.Name"/> gives it.</param>
        /// <param name="objectId">The id of the object the entity draws, in the owner's terms.</param>
        /// <param name="extraId">A second id, such as the view the entity is in; null for none.</param>
        /// <exception cref="ArgumentNullException">Thrown when the owner or the object id is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the owner or the object id is empty, or a text, written as the
        /// remarks say, is longer than 255 characters.</exception>
        public CadOwnerTag(string owner, string objectId, string extraId = null)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (objectId == null)
            {
                throw new ArgumentNullException(nameof(objectId));
            }

            if (owner.Length == 0)
            {
                throw new ArgumentException("A tag names its owner.", nameof(owner));
            }

            if (objectId.Length == 0)
            {
                throw new ArgumentException("A tag names the id of its object.", nameof(objectId));
            }

            Fits(owner, nameof(owner));
            Fits(objectId, nameof(objectId));

            if (extraId != null)
            {
                Fits(extraId, nameof(extraId));
            }

            Owner = owner;
            ObjectId = objectId;
            ExtraId = extraId;
        }

        /// <summary>Gets the owner's name.</summary>
        public string Owner { get; }

        /// <summary>Gets the id of the object the entity draws.</summary>
        public string ObjectId { get; }

        /// <summary>Gets the second id; null where the tag has none.</summary>
        public string ExtraId { get; }

        /// <summary>
        /// Reads the tag of an entity: its extended data of <see cref="ApplicationName"/>, two or three strings (1000), the
        /// owner and the object id not empty. Strings after the third are left unread.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <param name="tag">The tag; null where the entity has none.</param>
        /// <returns>Whether the entity has a tag: false for one with no data of the application, and for one whose data is
        /// not a tag, as a string that does not read back or a group of another code.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public static bool TryRead(DxfEntity entity, out CadOwnerTag tag)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            tag = null;
            IReadOnlyList<DxfTypedValue> data = entity.GetXDataForApplication(ApplicationName);

            // The 1001 first, then the owner, the object id and perhaps the second id.
            if (data == null || data.Count < 3)
            {
                return false;
            }

            var texts = new List<string>(3);

            for (int i = 1; i < data.Count && texts.Count < 3; i++)
            {
                if (data[i].TypeCode != 1000 || !TryUnescape(data[i].Value, out string text))
                {
                    return false;
                }

                texts.Add(text);
            }

            if (texts.Count < 2 || texts[0].Length == 0 || texts[1].Length == 0)
            {
                return false;
            }

            tag = new CadOwnerTag(texts[0], texts[1], texts.Count > 2 ? texts[2] : null);
            return true;
        }

        /// <summary>
        /// Takes the tag out of an entity's extended data, the data of other applications kept: for a copy of an owned
        /// entity, which is the presenter's own and no longer its original owner's.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <returns>Whether the entity had data of <see cref="ApplicationName"/> to take out.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        public static bool Remove(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            if (entity.GetXDataForApplication(ApplicationName) == null)
            {
                return false;
            }

            entity.XData = new[] { new DxfTypedValue(1001, ApplicationName) };
            return true;
        }

        /// <summary>
        /// Writes the tag into an entity's extended data, in place of a tag it has; the data of other applications is kept.
        /// An entity in no drawing yet takes it when it is appended.
        /// </summary>
        /// <param name="entity">The entity.</param>
        /// <exception cref="ArgumentNullException">Thrown when the entity is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the entity is erased.</exception>
        public void WriteTo(DxfEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity));
            }

            var data = new List<DxfTypedValue>(4)
            {
                new DxfTypedValue(1001, ApplicationName),
                new DxfTypedValue(1000, Escape(Owner)),
                new DxfTypedValue(1000, Escape(ObjectId)),
            };

            if (ExtraId != null)
            {
                data.Add(new DxfTypedValue(1000, Escape(ExtraId)));
            }

            entity.XData = data;
        }

        /// <summary>Gives the tag as a line for debugging.</summary>
        /// <returns>The line, such as <c>GeometryHelper.Drawing B-12 V-1</c>.</returns>
        public override string ToString() => Owner + " " + ObjectId + (ExtraId == null ? string.Empty : " " + ExtraId);

        /// <summary>Refuses a text too long for a string of extended data once it is escaped.</summary>
        private static void Fits(string text, string parameter)
        {
            if (Escape(text).Length > LongestText)
            {
                throw new ArgumentException("A text of a tag is at most " + LongestText.ToString(CultureInfo.InvariantCulture) + " characters as written; this one is longer.", parameter);
            }
        }

        /// <summary>A text as the tag writes it: a per cent sign, a backslash and control characters as %XX, a lone surrogate as %uXXXX.</summary>
        private static string Escape(string text)
        {
            StringBuilder written = null;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                string escape = null;

                if (c == '%' || c == '\\' || c < ' ' || c == '\u007F')
                {
                    escape = "%" + ((int)c).ToString("X2", CultureInfo.InvariantCulture);
                }
                else if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    written?.Append(c).Append(text[i + 1]);
                    i++;
                    continue;
                }
                else if (char.IsSurrogate(c))
                {
                    escape = "%u" + ((int)c).ToString("X4", CultureInfo.InvariantCulture);
                }

                if (escape != null && written == null)
                {
                    written = new StringBuilder(text, 0, i, text.Length + 16);
                }

                if (escape != null)
                {
                    written.Append(escape);
                }
                else
                {
                    written?.Append(c);
                }
            }

            return written?.ToString() ?? text;
        }

        /// <summary>A text as it was before <see cref="Escape"/>; false where a per cent sign begins no escape.</summary>
        private static bool TryUnescape(string written, out string text)
        {
            text = null;

            if (written.IndexOf('%') < 0)
            {
                text = written;
                return true;
            }

            var read = new StringBuilder(written.Length);

            for (int i = 0; i < written.Length; i++)
            {
                if (written[i] != '%')
                {
                    read.Append(written[i]);
                    continue;
                }

                bool wide = i + 1 < written.Length && written[i + 1] == 'u';
                int start = wide ? i + 2 : i + 1;
                int digits = wide ? 4 : 2;

                if (start + digits > written.Length
                    || !int.TryParse(written.Substring(start, digits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                {
                    return false;
                }

                read.Append((char)code);
                i = start + digits - 1;
            }

            text = read.ToString();
            return true;
        }
    }
}
