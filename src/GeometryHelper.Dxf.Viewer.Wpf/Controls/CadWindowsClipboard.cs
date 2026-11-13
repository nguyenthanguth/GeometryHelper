using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;

namespace GeometryHelper.Dxf.Viewer.Wpf
{
    /// <summary>
    /// The Windows clipboard as the presenter's (<see cref="ICadClipboard"/>): objects go under the private format
    /// <see cref="CadClipboardData.Format"/>, so that any copy of the app reads them, and their words as Unicode text, which
    /// every other app reads; text another app put there comes back as text, and a picture as the bytes of a PNG. Windows may hold the clipboard for a moment for
    /// another app: each call tries a few times before it gives up, and a call that gives up is logged and reads as empty.
    /// </summary>
    public sealed class CadWindowsClipboard : ICadClipboard
    {
        private const int Tries = 5;

        /// <inheritdoc/>
        public void SetData(CadClipboardData data)
        {
            if (data == null)
            {
                return;
            }

            var dataObject = new DataObject();
            string format = data.ToFormat();

            if (format != null)
            {
                dataObject.SetData(CadClipboardData.Format, format);
            }

            if (data.Text != null)
            {
                dataObject.SetData(DataFormats.UnicodeText, data.Text);
            }

            Retry(() => Clipboard.SetDataObject(dataObject, true));
        }

        /// <inheritdoc/>
        public CadClipboardData GetData()
        {
            CadClipboardData read = null;
            Retry(() =>
            {
                IDataObject held = Clipboard.GetDataObject();

                if (held == null)
                {
                    read = null;
                    return;
                }

                string format = held.GetDataPresent(CadClipboardData.Format)
                    ? held.GetData(CadClipboardData.Format) as string
                    : null;
                string text = held.GetDataPresent(DataFormats.UnicodeText)
                    ? held.GetData(DataFormats.UnicodeText) as string
                    : null;
                read = CadClipboardData.FromFormat(format, string.IsNullOrEmpty(text) ? null : text);

                // A picture another app copied, taken as a PNG, which PASTECLIP pastes where there are no objects and no text.
                if (read == null || (!read.HasObjects && read.Text == null))
                {
                    byte[] picture = PictureOf(held);

                    if (picture != null)
                    {
                        read = new CadClipboardData(null, null, picture: picture);
                    }
                }
            });

            return read;
        }

        /// <summary>The bitmap the clipboard holds written as a PNG, with its alpha; null where it holds none or Windows cannot give it.</summary>
        private static byte[] PictureOf(IDataObject held)
        {
            try
            {
                if (
                    !held.GetDataPresent(DataFormats.Bitmap)
                    || !(held.GetData(DataFormats.Bitmap) is BitmapSource image)
                )
                {
                    return null;
                }

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));

                using (var memory = new MemoryStream())
                {
                    encoder.Save(memory);
                    return memory.ToArray();
                }
            }
            catch (Exception exception)
                when (exception is COMException
                    || exception is ArgumentException
                    || exception is NotSupportedException
                    || exception is InvalidOperationException
                    || exception is IOException
                )
            {
                GeometryHelperLog.Debug(
                    "CadWindowsClipboard: the picture on the clipboard could not be read (" + exception.Message + ")."
                );
                return null;
            }
        }

        private static void Retry(Action action)
        {
            for (int i = 1; ; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (COMException exception) when (i < Tries)
                {
                    GeometryHelperLog.Debug(
                        "CadWindowsClipboard: the clipboard was busy (" + exception.Message + "), tried again."
                    );
                    Thread.Sleep(20);
                }
                catch (COMException exception)
                {
                    GeometryHelperLog.Warn("CadWindowsClipboard: the clipboard stayed busy.", exception);
                    return;
                }
            }
        }
    }
}
