using System;
using System.IO;
using System.Text;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// The frames of the channel: a message is its JSON in UTF-8 behind its length as four bytes, least significant first.
    /// </summary>
    internal static class RemoteFrames
    {
        /// <summary>The longest message either side takes, 512 MiB: a drawing's worth of entities, and no more.</summary>
        public const int MaxLength = 512 * 1024 * 1024;

        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Writes a message and flushes it.</summary>
        /// <exception cref="IOException">Thrown when the stream is closed.</exception>
        public static void Write(Stream stream, JsonObject message)
        {
            byte[] body = Utf8.GetBytes(RemoteJson.Write(message));

            if (body.Length > MaxLength)
            {
                throw new CadRemoteException(
                    "The message is " + body.Length + " bytes long, over the " + MaxLength + " the channel takes."
                );
            }

            byte[] head =
            {
                (byte)body.Length,
                (byte)(body.Length >> 8),
                (byte)(body.Length >> 16),
                (byte)(body.Length >> 24),
            };
            stream.Write(head, 0, 4);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        /// <summary>Reads a message; null where the other side closed the stream before the next one began.</summary>
        /// <exception cref="IOException">Thrown when the stream ends inside a message or it is longer than the channel takes.</exception>
        public static JsonObject Read(Stream stream)
        {
            byte[] head = new byte[4];

            if (!ReadAll(stream, head, true))
            {
                return null;
            }

            int length = head[0] | (head[1] << 8) | (head[2] << 16) | (head[3] << 24);

            if (length < 0 || length > MaxLength)
            {
                throw new IOException("A message of " + (uint)length + " bytes is over what the channel takes.");
            }

            byte[] body = new byte[length];
            ReadAll(stream, body, false);

            try
            {
                return RemoteJson.Parse(Utf8.GetString(body)) as JsonObject
                    ?? throw new IOException("A message is not a JSON object.");
            }
            catch (FormatException ex)
            {
                throw new IOException("A message is not JSON: " + ex.Message, ex);
            }
        }

        private static bool ReadAll(Stream stream, byte[] buffer, bool endAllowedAtStart)
        {
            int read = 0;

            while (read < buffer.Length)
            {
                int n = stream.Read(buffer, read, buffer.Length - read);

                if (n <= 0)
                {
                    if (read == 0 && endAllowedAtStart)
                    {
                        return false;
                    }

                    throw new IOException("The channel closed inside a message.");
                }

                read += n;
            }

            return true;
        }
    }
}
