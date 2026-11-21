using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;

namespace GeometryHelper.Cad.Remote
{
    /// <summary>
    /// The servers of this process, by pipe, that can say whether the calling thread is the one they run their requests on. A
    /// client on that thread waits for the answer of a thread it holds: it is refused before it sends anything.
    /// </summary>
    internal static class RemoteSelf
    {
        private static readonly ConcurrentDictionary<string, Func<bool>> Servers = new ConcurrentDictionary<
            string,
            Func<bool>
        >(StringComparer.Ordinal);

        /// <summary>Gives a server's answer to whether the calling thread is its own.</summary>
        internal static void Register(string pipe, Func<bool> onOwnerThread) => Servers[pipe] = onOwnerThread;

        /// <summary>Takes a server out, as it stops.</summary>
        internal static void Unregister(string pipe) => Servers.TryRemove(pipe, out _);

        /// <summary>Gets whether the pipe is a server of this process and the calling thread is the one it runs its requests on.</summary>
        internal static bool IsOwnerThread(string pipe) => Servers.TryGetValue(pipe, out Func<bool> own) && own();
    }

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
            if (RemoteSelf.IsOwnerThread(_pipe))
            {
                throw new CadRemoteException(
                    "A script inside GeometryHelper CAD cannot drive the same app through CadApplication: use geometryhelper.cad, which reaches the drawing directly."
                );
            }

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
                        throw new CadRemoteException(
                            answer.GetString("error") ?? "The app refused the request.",
                            answer.GetBool("cancelled")
                        );
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
