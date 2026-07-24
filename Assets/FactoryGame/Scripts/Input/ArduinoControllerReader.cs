using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace LastShift.Input
{
    /// <summary>
    /// Opens the controller's serial port and reads it on a background thread,
    /// publishing complete lines to the main thread through a lock-free queue —
    /// Unity's Update never blocks on serial I/O. Connection pattern adapted from
    /// for_claude_example_unity_grabber/UnityGrabber/ArduinoSensorsGrabber.cs:
    /// candidate USB-serial ports are scanned (or a single configured port used),
    /// a port counts as the controller once it sends a JOY/POT/BTN line, silent
    /// ports are released after a timeout, and lost connections are retried on a
    /// rate-limited schedule. Never touches UI, machines or scenes — it only
    /// hands lines to whoever calls Pump().
    /// </summary>
    public sealed class ArduinoControllerReader : MonoBehaviour
    {
        SerialConnectionSettings settings;

        readonly Dictionary<string, PortSession> sessions =
            new Dictionary<string, PortSession>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, float> retryAfter =
            new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> lastLoggedError =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        readonly ConcurrentQueue<SerialMessage> messages = new ConcurrentQueue<SerialMessage>();

        float nextPortScanAt;
        bool shuttingDown;

        /// <summary>True while a port that speaks the controller protocol is open.</summary>
        public bool IsConnected => !string.IsNullOrEmpty(ConnectedPort);
        public string ConnectedPort { get; private set; } = "";
        public int BaudRate => settings != null ? settings.baudRate : 0;
        public string LastError { get; private set; } = "";

        public void Initialize(SerialConnectionSettings connectionSettings)
        {
            settings = connectionSettings;
            nextPortScanAt = 0f;
        }

        /// <summary>
        /// Called once per frame by the input bridge (before menus poll input):
        /// keeps the port scan alive and forwards this frame's lines from the
        /// identified controller port to <paramref name="lineHandler"/>.
        /// </summary>
        public void Pump(Action<string> lineHandler)
        {
            if (settings == null || shuttingDown) return;

            if (Time.unscaledTime >= nextPortScanAt)
            {
                nextPortScanAt = Time.unscaledTime + settings.portScanInterval;
                ScanPorts();
            }

            int processed = 0;
            while (processed < settings.maxMessagesPerFrame && messages.TryDequeue(out SerialMessage message))
            {
                processed++;
                HandleSerialMessage(message, lineHandler);
            }
        }

        void OnDisable() => StopAllSessions();
        void OnApplicationQuit() => StopAllSessions();
        void OnDestroy() => StopAllSessions();

        void StopAllSessions()
        {
            if (shuttingDown) return;
            shuttingDown = true;

            foreach (PortSession session in sessions.Values)
                session.Stop();
            sessions.Clear();
            retryAfter.Clear();
            while (messages.TryDequeue(out _)) { }

            if (IsConnected && settings != null && settings.logConnections)
                Debug.Log("[Arduino] Serial closed (" + ConnectedPort + ").");
            ConnectedPort = "";
        }

        void ScanPorts()
        {
            // While the controller is identified, keep only its session alive.
            if (IsConnected)
            {
                if (sessions.TryGetValue(ConnectedPort, out PortSession live) && live.IsRunning)
                    return;
                // Session died (USB unplugged, port error): fall through and rescan.
            }

            HashSet<string> available =
                new HashSet<string>(GetCandidatePortNames(), StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, PortSession> pair in sessions.ToArray())
            {
                if (available.Contains(pair.Key) && pair.Value.IsRunning)
                    continue;

                pair.Value.Stop();
                sessions.Remove(pair.Key);
                retryAfter[pair.Key] = Time.unscaledTime + settings.reconnectDelay;
            }

            foreach (string portName in available)
            {
                if (sessions.ContainsKey(portName))
                    continue;
                if (retryAfter.TryGetValue(portName, out float retryTime) && Time.unscaledTime < retryTime)
                    continue;

                var session = new PortSession(portName, settings.baudRate, settings.identificationTimeout, messages);
                sessions.Add(portName, session);
                retryAfter.Remove(portName);
                session.Start();
            }
        }

        IEnumerable<string> GetCandidatePortNames()
        {
            // A configured port always wins over auto-detection.
            if (!string.IsNullOrWhiteSpace(settings.portName))
                return new[] { NormalizePortName(settings.portName.Trim()) };

            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (string portName in NativeSerialPort.GetPortNames())
                    result.Add(NormalizePortName(portName));
            }
            catch (Exception exception)
            {
                if (settings.logConnections)
                    Debug.LogWarning("[Arduino] Could not enumerate serial ports: " + exception.Message, this);
            }

            if (Application.platform == RuntimePlatform.OSXEditor ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                AddUnixDeviceMatches(result, "/dev", "cu.*");
                if (!result.Any(IsLikelyUsbSerialPort))
                    AddUnixDeviceMatches(result, "/dev", "tty.*");
            }
            else if (Application.platform == RuntimePlatform.LinuxEditor ||
                     Application.platform == RuntimePlatform.LinuxPlayer)
            {
                AddUnixDeviceMatches(result, "/dev", "ttyACM*");
                AddUnixDeviceMatches(result, "/dev", "ttyUSB*");
            }

            // macOS exposes the same adapter as tty.* and cu.*; cu.* is the right
            // endpoint for an app initiating the connection, so drop duplicates.
            foreach (string ttyPort in result.Where(p => p.StartsWith("/dev/tty.", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                string calloutPort = "/dev/cu." + ttyPort.Substring("/dev/tty.".Length);
                if (result.Contains(calloutPort))
                    result.Remove(ttyPort);
            }

            return result
                .Where(IsLikelyUsbSerialPort)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        static void AddUnixDeviceMatches(HashSet<string> target, string directory, string pattern)
        {
            try
            {
                foreach (string path in Directory.GetFiles(directory, pattern))
                    target.Add(path);
            }
            catch (Exception)
            {
                // A platform may not expose /dev or may deny enumeration.
            }
        }

        static string NormalizePortName(string portName)
        {
            if (portName.StartsWith("cu.", StringComparison.OrdinalIgnoreCase) ||
                portName.StartsWith("tty.", StringComparison.OrdinalIgnoreCase))
                return "/dev/" + portName;
            return portName;
        }

        static bool IsLikelyUsbSerialPort(string portName)
        {
            if (string.IsNullOrWhiteSpace(portName))
                return false;

            string value = portName.ToLowerInvariant();
            if (value.StartsWith("com"))
                return true;

            return value.Contains("usbmodem") ||
                   value.Contains("usbserial") ||
                   value.Contains("wchusb") ||
                   value.Contains("ch34") ||
                   value.Contains("slab_usbtouart") ||
                   value.StartsWith("/dev/ttyacm") ||
                   value.StartsWith("/dev/ttyusb");
        }

        /// <summary>
        /// Lines that identify our controller board; anything else never claims a
        /// port. "POT" is not part of the current protocol — it is recognized only
        /// as a legacy signature of an outdated sketch so the board still connects,
        /// and the parser ignores those lines with a single warning.
        /// </summary>
        internal static bool IsControllerLine(string line)
        {
            int comma = line.IndexOf(',');
            string tag = (comma >= 0 ? line.Substring(0, comma) : line).Trim().ToUpperInvariant();
            return tag == "JOY" || tag == "BTN" || tag == "POT";
        }

        void HandleSerialMessage(SerialMessage message, Action<string> lineHandler)
        {
            switch (message.type)
            {
                case SerialMessageType.Opened:
                    if (settings.logConnections)
                        Debug.Log("[Arduino] Probing " + message.portName + " @ " + settings.baudRate + " baud", this);
                    break;

                case SerialMessageType.Closed:
                    if (string.Equals(ConnectedPort, message.portName, StringComparison.OrdinalIgnoreCase))
                    {
                        ConnectedPort = "";
                        if (settings.logConnections)
                            Debug.Log("[Arduino] Controller disconnected (" + message.portName + "). Keyboard remains active.", this);
                    }
                    break;

                case SerialMessageType.Error:
                    LastError = message.portName + ": " + message.payload;
                    // Only log a given failure once per port until it changes,
                    // so a busy/foreign port cannot spam the console on retries.
                    if (settings.logConnections &&
                        (!lastLoggedError.TryGetValue(message.portName, out string previous) || previous != message.payload))
                    {
                        lastLoggedError[message.portName] = message.payload;
                        Debug.LogWarning("[Arduino] " + message.portName + ": " + message.payload, this);
                    }
                    break;

                case SerialMessageType.Line:
                    ProcessLine(message.portName, message.payload, lineHandler);
                    break;
            }
        }

        void ProcessLine(string portName, string line, Action<string> lineHandler)
        {
            line = line.Trim();
            if (line.Length == 0 || !IsControllerLine(line))
                return;

            if (!IsConnected)
            {
                ConnectedPort = portName;
                lastLoggedError.Remove(portName);
                if (settings.logConnections)
                    Debug.Log("[Arduino] Controller connected on " + portName + " @ " + settings.baudRate + " baud", this);

                // One controller, one port: release the other probes.
                foreach (KeyValuePair<string, PortSession> pair in sessions.ToArray())
                {
                    if (string.Equals(pair.Key, portName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    pair.Value.Stop();
                    sessions.Remove(pair.Key);
                }
            }

            if (string.Equals(portName, ConnectedPort, StringComparison.OrdinalIgnoreCase))
                lineHandler?.Invoke(line);
        }

        enum SerialMessageType { Opened, Line, Closed, Error }

        readonly struct SerialMessage
        {
            public readonly SerialMessageType type;
            public readonly string portName;
            public readonly string payload;

            public SerialMessage(SerialMessageType type, string portName, string payload = "")
            {
                this.type = type;
                this.portName = portName;
                this.payload = payload;
            }
        }

        sealed class PortSession
        {
            readonly string portName;
            readonly int baudRate;
            readonly float identificationTimeout;
            readonly ConcurrentQueue<SerialMessage> messages;
            readonly object portLock = new object();

            Thread thread;
            NativeSerialPort port;
            volatile bool stopRequested;
            volatile bool isRunning;

            public bool IsRunning => isRunning;

            public PortSession(
                string portName,
                int baudRate,
                float identificationTimeout,
                ConcurrentQueue<SerialMessage> messages)
            {
                this.portName = portName;
                this.baudRate = baudRate;
                this.identificationTimeout = identificationTimeout;
                this.messages = messages;
            }

            public void Start()
            {
                stopRequested = false;
                isRunning = true;
                thread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "Arduino serial " + portName
                };
                thread.Start();
            }

            public void Stop()
            {
                stopRequested = true;
                isRunning = false;

                lock (portLock)
                {
                    if (port == null)
                        return;
                    try { port.Close(); }
                    catch (Exception) { }
                }
            }

            void ReadLoop()
            {
                DateTime openedAt = DateTime.UtcNow;
                bool identified = false;

                try
                {
                    NativeSerialPort serialPort = new NativeSerialPort(portName, baudRate, 250);

                    lock (portLock)
                        port = serialPort;

                    serialPort.Open();
                    openedAt = DateTime.UtcNow;
                    messages.Enqueue(new SerialMessage(SerialMessageType.Opened, portName));

                    while (!stopRequested && serialPort.IsOpen)
                    {
                        try
                        {
                            string line = serialPort.ReadLine();
                            if (string.IsNullOrWhiteSpace(line))
                                continue;

                            if (IsControllerLine(line))
                                identified = true;

                            messages.Enqueue(new SerialMessage(SerialMessageType.Line, portName, line));
                        }
                        catch (TimeoutException)
                        {
                            if (!identified && (DateTime.UtcNow - openedAt).TotalSeconds >= identificationTimeout)
                            {
                                messages.Enqueue(new SerialMessage(
                                    SerialMessageType.Error,
                                    portName,
                                    "No controller messages received; releasing this port."));
                                break;
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (!stopRequested)
                        messages.Enqueue(new SerialMessage(SerialMessageType.Error, portName, exception.Message));
                }
                finally
                {
                    lock (portLock)
                    {
                        if (port != null)
                        {
                            try { port.Close(); }
                            catch (Exception) { }
                            port.Dispose();
                            port = null;
                        }
                    }

                    isRunning = false;
                    if (!stopRequested)
                        messages.Enqueue(new SerialMessage(SerialMessageType.Closed, portName));
                }
            }
        }

        /// <summary>
        /// Minimal desktop serial implementation (read-only), taken from the
        /// workshop grabber example: Unity 6 does not expose System.IO.Ports to
        /// regular .NET Standard assemblies, so the native APIs that back a
        /// serial connection on Windows and macOS are used directly.
        /// </summary>
        sealed class NativeSerialPort : IDisposable
        {
            const int MaxLineLength = 8192;
            static readonly IntPtr InvalidWindowsHandle = new IntPtr(-1);

            readonly string portName;
            readonly int baudRate;
            readonly int readTimeoutMilliseconds;
            readonly StringBuilder lineBuffer = new StringBuilder(128);
            readonly byte[] singleByteBuffer = new byte[1];

            IntPtr windowsHandle = InvalidWindowsHandle;
            int unixFileDescriptor = -1;

            public bool IsOpen
            {
                get
                {
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        return Interlocked.CompareExchange(
                            ref windowsHandle,
                            InvalidWindowsHandle,
                            InvalidWindowsHandle) != InvalidWindowsHandle;

                    return Volatile.Read(ref unixFileDescriptor) >= 0;
                }
            }

            public NativeSerialPort(string portName, int baudRate, int readTimeoutMilliseconds)
            {
                this.portName = portName;
                this.baudRate = baudRate;
                this.readTimeoutMilliseconds = readTimeoutMilliseconds;
            }

            public static string[] GetPortNames()
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return Array.Empty<string>();

                char[] names = new char[65536];
                uint length = WindowsNative.QueryDosDevice(null, names, names.Length);
                if (length == 0)
                    return Array.Empty<string>();

                List<string> ports = new List<string>();
                int start = 0;
                for (int i = 0; i < length; i++)
                {
                    if (names[i] != '\0')
                        continue;

                    if (i > start)
                    {
                        string name = new string(names, start, i - start);
                        if (name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(name.Substring(3), out _))
                            ports.Add(name);
                    }

                    start = i + 1;
                }

                return ports.ToArray();
            }

            public void Open()
            {
                if (IsOpen)
                    return;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    OpenWindows();
                    return;
                }

                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    OpenMac();
                    return;
                }

                throw new PlatformNotSupportedException(
                    "Arduino serial input is implemented for Windows and macOS desktop players.");
            }

            public string ReadLine()
            {
                DateTime deadline = DateTime.UtcNow.AddMilliseconds(readTimeoutMilliseconds);

                while (IsOpen)
                {
                    int count = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? ReadWindowsByte()
                        : ReadMacByte();

                    if (count > 0)
                    {
                        char character = (char)singleByteBuffer[0];
                        if (character == '\n')
                        {
                            string line = lineBuffer.ToString();
                            lineBuffer.Length = 0;
                            return line;
                        }

                        if (character != '\r')
                        {
                            lineBuffer.Append(character);
                            if (lineBuffer.Length > MaxLineLength)
                            {
                                lineBuffer.Length = 0;
                                throw new IOException("Serial line exceeded " + MaxLineLength + " bytes.");
                            }
                        }

                        continue;
                    }

                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException();

                    Thread.Sleep(1);
                }

                throw new IOException("Serial port was closed.");
            }

            public void Close()
            {
                IntPtr handle = Interlocked.Exchange(ref windowsHandle, InvalidWindowsHandle);
                if (handle != InvalidWindowsHandle)
                    WindowsNative.CloseHandle(handle);

                int descriptor = Interlocked.Exchange(ref unixFileDescriptor, -1);
                if (descriptor >= 0)
                    MacNative.close(descriptor);
            }

            public void Dispose() => Close();

            void OpenWindows()
            {
                string devicePath = portName.StartsWith("\\\\.\\", StringComparison.Ordinal)
                    ? portName
                    : "\\\\.\\" + portName;

                IntPtr handle = WindowsNative.CreateFile(
                    devicePath,
                    WindowsNative.GenericRead | WindowsNative.GenericWrite,
                    0,
                    IntPtr.Zero,
                    WindowsNative.OpenExisting,
                    0,
                    IntPtr.Zero);

                if (handle == InvalidWindowsHandle)
                    throw WindowsIOException("Could not open " + portName);

                try
                {
                    WindowsNative.Dcb dcb = new WindowsNative.Dcb
                    {
                        length = (uint)Marshal.SizeOf(typeof(WindowsNative.Dcb))
                    };
                    if (!WindowsNative.GetCommState(handle, ref dcb))
                        throw WindowsIOException("Could not read serial settings for " + portName);

                    dcb.baudRate = (uint)baudRate;
                    dcb.flags = WindowsNative.BinaryMode |
                                WindowsNative.DtrControlEnable |
                                WindowsNative.RtsControlEnable;
                    dcb.byteSize = 8;
                    dcb.parity = 0;
                    dcb.stopBits = 0;

                    if (!WindowsNative.SetCommState(handle, ref dcb))
                        throw WindowsIOException("Could not configure " + portName);

                    WindowsNative.CommTimeouts timeouts = new WindowsNative.CommTimeouts
                    {
                        readIntervalTimeout = uint.MaxValue,
                        readTotalTimeoutMultiplier = 0,
                        readTotalTimeoutConstant = (uint)readTimeoutMilliseconds,
                        writeTotalTimeoutMultiplier = 0,
                        writeTotalTimeoutConstant = 250
                    };
                    if (!WindowsNative.SetCommTimeouts(handle, ref timeouts))
                        throw WindowsIOException("Could not set timeouts for " + portName);

                    WindowsNative.SetupComm(handle, 4096, 4096);
                    WindowsNative.PurgeComm(
                        handle,
                        WindowsNative.PurgeRxClear | WindowsNative.PurgeTxClear);
                    WindowsNative.EscapeCommFunction(handle, WindowsNative.SetDtr);
                    WindowsNative.EscapeCommFunction(handle, WindowsNative.SetRts);

                    windowsHandle = handle;
                }
                catch
                {
                    WindowsNative.CloseHandle(handle);
                    throw;
                }
            }

            void OpenMac()
            {
                int descriptor = MacNative.open(
                    portName,
                    MacNative.OpenReadWrite | MacNative.OpenNoControllingTerminal | MacNative.OpenNonBlocking);
                if (descriptor < 0)
                    throw MacIOException("Could not open " + portName);

                try
                {
                    MacNative.Termios settings = new MacNative.Termios
                    {
                        controlCharacters = new byte[MacNative.ControlCharacterCount]
                    };

                    if (MacNative.tcgetattr(descriptor, ref settings) != 0)
                        throw MacIOException("Could not read serial settings for " + portName);

                    MacNative.cfmakeraw(ref settings);
                    settings.controlFlags |= MacNative.EnableReceiver | MacNative.IgnoreModemControlLines;
                    settings.controlFlags &= ~(MacNative.EnableParity |
                                               MacNative.TwoStopBits |
                                               MacNative.HardwareFlowControl);

                    ulong speed = (ulong)baudRate;
                    if (MacNative.cfsetispeed(ref settings, speed) != 0 ||
                        MacNative.cfsetospeed(ref settings, speed) != 0 ||
                        MacNative.tcsetattr(descriptor, MacNative.ApplyNow, ref settings) != 0)
                        throw MacIOException("Could not configure " + portName);

                    MacNative.tcflush(descriptor, MacNative.FlushInputAndOutput);
                    unixFileDescriptor = descriptor;
                }
                catch
                {
                    MacNative.close(descriptor);
                    throw;
                }
            }

            int ReadWindowsByte()
            {
                IntPtr handle = Interlocked.CompareExchange(
                    ref windowsHandle,
                    InvalidWindowsHandle,
                    InvalidWindowsHandle);
                if (!WindowsNative.ReadFile(handle, singleByteBuffer, 1, out uint read, IntPtr.Zero))
                    throw WindowsIOException("Could not read from " + portName);
                return (int)read;
            }

            int ReadMacByte()
            {
                int descriptor = Volatile.Read(ref unixFileDescriptor);
                long result = MacNative.read(descriptor, singleByteBuffer, (UIntPtr)1);
                if (result >= 0)
                    return (int)result;

                int error = Marshal.GetLastWin32Error();
                if (error == MacNative.Interrupted || error == MacNative.TryAgain)
                    return 0;

                throw new IOException("Could not read from " + portName + " (errno " + error + ").");
            }

            static IOException WindowsIOException(string message)
            {
                return new IOException(message + " (Win32 error " + Marshal.GetLastWin32Error() + ").");
            }

            static IOException MacIOException(string message)
            {
                return new IOException(message + " (errno " + Marshal.GetLastWin32Error() + ").");
            }

            static class WindowsNative
            {
                internal const uint GenericRead = 0x80000000;
                internal const uint GenericWrite = 0x40000000;
                internal const uint OpenExisting = 3;
                internal const uint BinaryMode = 0x00000001;
                internal const uint DtrControlEnable = 0x00000010;
                internal const uint RtsControlEnable = 0x00001000;
                internal const uint PurgeTxClear = 0x0004;
                internal const uint PurgeRxClear = 0x0008;
                internal const uint SetRts = 3;
                internal const uint SetDtr = 5;

                [StructLayout(LayoutKind.Sequential)]
                internal struct Dcb
                {
                    internal uint length;
                    internal uint baudRate;
                    internal uint flags;
                    internal ushort reserved;
                    internal ushort xonLimit;
                    internal ushort xoffLimit;
                    internal byte byteSize;
                    internal byte parity;
                    internal byte stopBits;
                    internal sbyte xonCharacter;
                    internal sbyte xoffCharacter;
                    internal sbyte errorCharacter;
                    internal sbyte eofCharacter;
                    internal sbyte eventCharacter;
                    internal ushort reservedOne;
                }

                [StructLayout(LayoutKind.Sequential)]
                internal struct CommTimeouts
                {
                    internal uint readIntervalTimeout;
                    internal uint readTotalTimeoutMultiplier;
                    internal uint readTotalTimeoutConstant;
                    internal uint writeTotalTimeoutMultiplier;
                    internal uint writeTotalTimeoutConstant;
                }

                [DllImport(
                    "kernel32.dll",
                    EntryPoint = "CreateFileW",
                    CharSet = CharSet.Unicode,
                    ExactSpelling = true,
                    SetLastError = true)]
                internal static extern IntPtr CreateFile(
                    string fileName,
                    uint desiredAccess,
                    uint shareMode,
                    IntPtr securityAttributes,
                    uint creationDisposition,
                    uint flagsAndAttributes,
                    IntPtr templateFile);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool CloseHandle(IntPtr handle);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool GetCommState(IntPtr handle, ref Dcb dcb);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool SetCommState(IntPtr handle, ref Dcb dcb);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool SetCommTimeouts(IntPtr handle, ref CommTimeouts timeouts);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool SetupComm(IntPtr handle, uint inputQueueSize, uint outputQueueSize);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool PurgeComm(IntPtr handle, uint flags);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool EscapeCommFunction(IntPtr handle, uint function);

                [DllImport("kernel32.dll", SetLastError = true)]
                [return: MarshalAs(UnmanagedType.Bool)]
                internal static extern bool ReadFile(
                    IntPtr handle,
                    [Out] byte[] buffer,
                    uint bytesToRead,
                    out uint bytesRead,
                    IntPtr overlapped);

                [DllImport(
                    "kernel32.dll",
                    EntryPoint = "QueryDosDeviceW",
                    CharSet = CharSet.Unicode,
                    ExactSpelling = true,
                    SetLastError = true)]
                internal static extern uint QueryDosDevice(
                    string deviceName,
                    [Out] char[] targetPath,
                    int maximumLength);
            }

            static class MacNative
            {
                const string LibSystem = "libSystem.B.dylib";

                internal const int ControlCharacterCount = 20;
                internal const int OpenReadWrite = 0x0002;
                internal const int OpenNonBlocking = 0x0004;
                internal const int OpenNoControllingTerminal = 0x20000;
                internal const int ApplyNow = 0;
                internal const int FlushInputAndOutput = 3;
                internal const int Interrupted = 4;
                internal const int TryAgain = 35;
                internal const ulong EnableReceiver = 0x00000800;
                internal const ulong EnableParity = 0x00001000;
                internal const ulong TwoStopBits = 0x00000400;
                internal const ulong IgnoreModemControlLines = 0x00008000;
                internal const ulong HardwareFlowControl = 0x00030000;

                [StructLayout(LayoutKind.Sequential)]
                internal struct Termios
                {
                    internal ulong inputFlags;
                    internal ulong outputFlags;
                    internal ulong controlFlags;
                    internal ulong localFlags;

                    [MarshalAs(UnmanagedType.ByValArray, SizeConst = ControlCharacterCount)]
                    internal byte[] controlCharacters;

                    internal ulong inputSpeed;
                    internal ulong outputSpeed;
                }

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int open(string path, int flags);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int close(int fileDescriptor);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern long read(int fileDescriptor, [Out] byte[] buffer, UIntPtr count);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int tcgetattr(int fileDescriptor, ref Termios settings);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int tcsetattr(int fileDescriptor, int optionalActions, ref Termios settings);

                [DllImport(LibSystem)]
                internal static extern void cfmakeraw(ref Termios settings);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int cfsetispeed(ref Termios settings, ulong speed);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int cfsetospeed(ref Termios settings, ulong speed);

                [DllImport(LibSystem, SetLastError = true)]
                internal static extern int tcflush(int fileDescriptor, int queueSelector);
            }
        }
    }
}
