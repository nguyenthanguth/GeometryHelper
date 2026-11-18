using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GeometryHelper.Cad
{
    /// <summary>
    /// Makes the pipe the remote server listens on, open to the Windows user who runs the app and to nobody else, and shut
    /// to other computers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// netstandard2.0 has no <c>PipeSecurity</c> (the framework's own lives in System.Core on .NET Framework and in
    /// System.IO.Pipes.AccessControl on .NET 8, neither of which a netstandard2.0 library can name), and a package for it is
    /// out of the question, so the pipe is made with the Win32 call itself: <c>CreateNamedPipe</c> given a security
    /// descriptor from the string <c>D:P(A;;GA;;;&lt;the user's SID&gt;)</c>, one entry that gives all access to the user
    /// and no one else, not the administrators or the system. The handle is wrapped in a <see cref="NamedPipeServerStream"/>
    /// and works the same on .NET Framework and .NET.
    /// </para>
    /// <para>
    /// The first pipe of a name is made with <c>FILE_FLAG_FIRST_PIPE_INSTANCE</c>, so that a process that made the name
    /// before the app cannot sit between the app and its clients: the call fails then, and so does the server.
    /// </para>
    /// <para>
    /// Where the call is not there (a system that is not Windows) the pipe is made the framework's way with its default
    /// security, which on Windows would be the creator, the administrators and the system.
    /// </para>
    /// </remarks>
    internal static class CadRemotePipe
    {
        private const uint PipeAccessDuplex = 0x00000003;
        private const uint FirstPipeInstance = 0x00080000;
        private const uint RejectRemoteClients = 0x00000008;
        private const uint MaxInstances = 255;
        private const uint BufferSize = 65536;

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr Descriptor;
            public int InheritHandle;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(
            string sddl,
            uint revision,
            out IntPtr descriptor,
            out uint size
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafePipeHandle CreateNamedPipeW(
            string name,
            uint openMode,
            uint pipeMode,
            uint maxInstances,
            uint outBufferSize,
            uint inBufferSize,
            uint defaultTimeOut,
            ref SecurityAttributes attributes
        );

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr token,
            int informationClass,
            IntPtr information,
            int length,
            out int returned
        );

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>The SID of the Windows user of the process as text, such as <c>S-1-5-21-...</c>; null where Windows does not say.</summary>
        private static string CurrentUserSid()
        {
            const uint tokenQuery = 0x0008;
            const int tokenUser = 1;

            if (!OpenProcessToken(GetCurrentProcess(), tokenQuery, out IntPtr token))
            {
                return null;
            }

            IntPtr buffer = IntPtr.Zero;

            try
            {
                GetTokenInformation(token, tokenUser, IntPtr.Zero, 0, out int length);

                if (length <= 0)
                {
                    return null;
                }

                buffer = Marshal.AllocHGlobal(length);

                // The buffer starts with a TOKEN_USER: a pointer to the SID, and the attributes.
                if (!GetTokenInformation(token, tokenUser, buffer, length, out _))
                {
                    return null;
                }

                if (!ConvertSidToStringSidW(Marshal.ReadIntPtr(buffer), out IntPtr text))
                {
                    return null;
                }

                try
                {
                    return Marshal.PtrToStringUni(text);
                }
                finally
                {
                    LocalFree(text);
                }
            }
            finally
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                }

                CloseHandle(token);
            }
        }

        /// <summary>Makes one instance of the pipe, waiting for a client.</summary>
        /// <param name="name">The pipe's name, without the <c>\\.\pipe\</c>.</param>
        /// <param name="first">Whether this is the first instance of the name, which must not exist yet.</param>
        /// <returns>The server end.</returns>
        /// <exception cref="System.IO.IOException">Thrown when the pipe cannot be made, as when the name is taken by another process.</exception>
        public static NamedPipeServerStream Create(string name, bool first)
        {
            try
            {
                return CreateOwn(name, first);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                GeometryHelperLog.Warn(
                    "CadRemotePipe: Win32 pipes are not here; the pipe keeps the framework's default access.",
                    ex
                );
                return new NamedPipeServerStream(
                    name,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.None
                );
            }
        }

        private static NamedPipeServerStream CreateOwn(string name, bool first)
        {
            string user = CurrentUserSid();

            if (user == null)
            {
                throw new System.IO.IOException("The Windows user of the process has no SID to restrict the pipe to.");
            }

            if (
                !ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    "D:P(A;;GA;;;" + user + ")",
                    1,
                    out IntPtr descriptor,
                    out _
                )
            )
            {
                throw new System.IO.IOException(
                    "The pipe's access could not be set: Windows error " + Marshal.GetLastWin32Error() + "."
                );
            }

            try
            {
                var attributes = new SecurityAttributes
                {
                    Length = Marshal.SizeOf(typeof(SecurityAttributes)),
                    Descriptor = descriptor,
                    InheritHandle = 0,
                };
                SafePipeHandle handle = CreateNamedPipeW(
                    @"\\.\pipe\" + name,
                    PipeAccessDuplex | (first ? FirstPipeInstance : 0),
                    RejectRemoteClients,
                    MaxInstances,
                    BufferSize,
                    BufferSize,
                    0,
                    ref attributes
                );

                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw new System.IO.IOException(
                        "The pipe " + name + " could not be made: Windows error " + error + "."
                    );
                }

                return new NamedPipeServerStream(PipeDirection.InOut, false, false, handle);
            }
            finally
            {
                LocalFree(descriptor);
            }
        }
    }
}
