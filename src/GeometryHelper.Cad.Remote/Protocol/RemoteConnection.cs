using System;
using System.IO;
using System.IO.Pipes;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// A client's connection to one app: its pipe, opened on the first request and again after it broke, one request at a
    /// time. A request that the app refuses is a <see cref="CadRemoteException"/> with the app's words.
    /// </summary>
    internal sealed class RemoteConnection : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _pipe;
        private readonly int _processId;
        private NamedPipeClientStream _stream;
        private long _nextId;

        /// <summary>The time a connection waits for the app to take it, in milliseconds.</summary>
        public int ConnectTimeout { get; set; } = 5000;

        public RemoteConnection(RemoteInstance instance)
        {
            _pipe = instance.Pipe;
            _processId = instance.ProcessId;
        }

        /// <summary>Sends a request and reads its answer, the app's refusal thrown as a <see cref="CadRemoteException"/>.</summary>
        public JsonObject Request(JsonObject request)
        {
            lock (_gate)
            {
                EnsureConnected();
                request.Set("id", ++_nextId);

                try
                {
                    RemoteFrames.Write(_stream, request);
                    JsonObject answer = RemoteFrames.Read(_stream)
                        ?? throw new IOException("The app closed the channel.");

                    if (answer.GetLong("id") != _nextId)
                    {
                        throw new IOException("The app answered another request.");
                    }

                    if (!answer.GetBool("ok"))
                    {
                        throw new CadRemoteException(answer.GetString("error") ?? "The app refused the request.");
                    }

                    return answer;
                }
                catch (IOException ex)
                {
                    Close();
                    throw new CadRemoteException(
                        "The connection to GeometryHelper CAD (process " + _processId + ") was lost: " + ex.Message,
                        ex
                    );
                }
            }
        }

        private void EnsureConnected()
        {
            if (_stream != null && _stream.IsConnected)
            {
                return;
            }

            Close();
            var stream = new NamedPipeClientStream(".", _pipe, PipeDirection.InOut, PipeOptions.None);

            try
            {
                stream.Connect(ConnectTimeout);
            }
            catch (Exception ex) when (ex is TimeoutException || ex is IOException || ex is UnauthorizedAccessException)
            {
                stream.Dispose();
                throw new CadRemoteException(
                    "GeometryHelper CAD (process " + _processId + ") did not take the connection: " + ex.Message,
                    ex
                );
            }

            _stream = stream;
            JsonObject hello;

            try
            {
                RemoteFrames.Write(
                    _stream,
                    new JsonObject().Set("id", 0L).Set("op", RemoteOps.Hello).Set("protocol", (long)RemoteOps.Version)
                );
                hello = RemoteFrames.Read(_stream) ?? throw new IOException("The app closed the channel.");
            }
            catch (IOException ex)
            {
                Close();
                throw new CadRemoteException(
                    "GeometryHelper CAD (process " + _processId + ") did not answer: " + ex.Message,
                    ex
                );
            }

            if (!hello.GetBool("ok") || hello.GetLong("protocol") != RemoteOps.Version)
            {
                Close();
                throw new CadRemoteException(
                    hello.GetBool("ok")
                        ? "GeometryHelper CAD speaks version "
                            + hello.GetLong("protocol")
                            + " of the remote protocol and this client version "
                            + RemoteOps.Version
                            + "."
                        : hello.GetString("error") ?? "The app refused the connection."
                );
            }
        }

        private void Close()
        {
            _stream?.Dispose();
            _stream = null;
        }

        /// <summary>Closes the pipe.</summary>
        public void Dispose()
        {
            lock (_gate)
            {
                Close();
            }
        }
    }
}
