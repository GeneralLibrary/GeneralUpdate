using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// USB Device Firmware Upgrade (DFU) connection implementation.
    /// Implements the full USB DFU 1.1 protocol for firmware transfer over USB.
    ///
    /// <para><b>Platform backends:</b></para>
    /// <list type="bullet">
    ///   <item><description>Linux — libusb-1.0 via P/Invoke</description></item>
    ///   <item><description>Windows — WinUSB + SetupAPI via P/Invoke</description></item>
    /// </list>
    ///
    /// <para><b>Supported DFU commands:</b></para>
    /// <list type="bullet">
    ///   <item><description>DETACH — Detach from app mode to DFU mode</description></item>
    ///   <item><description>DNLOAD — Download firmware block</description></item>
    ///   <item><description>UPLOAD — Upload firmware (for backup/verify)</description></item>
    ///   <item><description>GETSTATUS — Poll device status</description></item>
    ///   <item><description>CLRSTATUS — Clear error status</description></item>
    ///   <item><description>GETSTATE — Query current DFU state</description></item>
    ///   <item><description>ABORT — Abort current operation</description></item>
    /// </list>
    ///
    /// <para><b>Common devices:</b> STM32 DFU bootloader, RP2040 (Pico), ATmega16U2, nRF52 DFU</para>
    /// </summary>
    internal class UsbDfuConnection : IConnection
    {
        private readonly DeviceConnection _config;
        private IDfuUsbBackend _backend;
        private int _dfuInterface;
        private int _transferSize;
        private int _blockNum;

        // DFU protocol constants (USB DFU 1.1 specification)
        private const byte DFU_DETACH    = 0;
        private const byte DFU_DNLOAD    = 1;
        private const byte DFU_UPLOAD    = 2;
        private const byte DFU_GETSTATUS = 3;
        private const byte DFU_CLRSTATUS = 4;
        private const byte DFU_GETSTATE  = 5;
        private const byte DFU_ABORT     = 6;

        // DFU states
        private const byte STATE_APP_IDLE              = 0;
        private const byte STATE_APP_DETACH            = 1;
        private const byte STATE_DFU_IDLE              = 2;
        private const byte STATE_DFU_DNLOAD_SYNC       = 3;
        private const byte STATE_DFU_DNBUSY            = 4;
        private const byte STATE_DFU_DNLOAD_IDLE       = 5;
        private const byte STATE_DFU_MANIFEST_SYNC     = 6;
        private const byte STATE_DFU_MANIFEST          = 7;
        private const byte STATE_DFU_MANIFEST_WAIT_RESET = 8;
        private const byte STATE_DFU_UPLOAD_IDLE       = 9;
        private const byte STATE_DFU_ERROR             = 10;

        // USB control transfer constants
        private const byte USB_TYPE_CLASS    = 0x20;
        private const byte USB_RECIP_INTERFACE = 0x01;
        private const byte USB_DIR_OUT       = 0x00;
        private const byte USB_DIR_IN        = 0x80;
        private const byte USB_REQ_TYPE_OUT  = USB_TYPE_CLASS | USB_RECIP_INTERFACE | USB_DIR_OUT;
        private const byte USB_REQ_TYPE_IN   = USB_TYPE_CLASS | USB_RECIP_INTERFACE | USB_DIR_IN;

        // Default transfer size (STM32 standard is 2048 bytes)
        private const int DEFAULT_TRANSFER_SIZE = 2048;

        // Status poll timeout and interval
        private static readonly TimeSpan STATUS_POLL_TIMEOUT = TimeSpan.FromSeconds(30);
        private const int STATUS_POLL_INTERVAL_MS = 50;

        public UsbDfuConnection(DeviceConnection config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _transferSize = DEFAULT_TRANSFER_SIZE;
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            FirmwareTrace.Info(
                "USB DFU: opening device VID=0x{0:X4}, PID=0x{1:X4}",
                _config.VendorId, _config.ProductId);

            _backend = CreateBackend();

            // Open the USB device
            _backend.Open(_config.VendorId, _config.ProductId);

            // Claim the DFU interface (typically interface 0)
            _dfuInterface = 0;
            _backend.ClaimInterface(_dfuInterface);

            // Read the DFU functional descriptor to get transfer size
            // Most devices use 2048 bytes; STM32 uses 2048, RP2040 uses 256
            // If the backend supports descriptor reading, use it; otherwise use default
            int? detectedTransferSize = _backend.GetDfuTransferSize(_dfuInterface);
            if (detectedTransferSize.HasValue && detectedTransferSize.Value > 0)
            {
                _transferSize = detectedTransferSize.Value;
            }

            FirmwareTrace.Info(
                "USB DFU: device opened, interface={0}, transferSize={1} bytes",
                _dfuInterface, _transferSize);

            // Ensure the device is in DFU mode
            // If the device is in app mode (STATE_APP_IDLE), send DETACH
            byte state = GetState();
            FirmwareTrace.Info("USB DFU: initial device state = {0} ({1})", state, StateName(state));

            if (state == STATE_APP_IDLE)
            {
                FirmwareTrace.Info("USB DFU: device in app mode, sending DETACH...");
                try
                {
                    Detach(1000);
                    // After DETACH, the device may re-enumerate. Wait a bit.
                    Thread.Sleep(1500);

                    // Re-open the device (it may have re-enumerated)
                    _backend.Dispose();
                    _backend = CreateBackend();
                    _backend.Open(_config.VendorId, _config.ProductId);
                    _backend.ClaimInterface(_dfuInterface);
                }
                catch (Exception ex)
                {
                    FirmwareTrace.Warn("USB DFU: DETACH failed or device did not re-enumerate ({0}). " +
                        "The device may already be in DFU mode or require manual DFU mode entry.", ex.Message);
                }
            }

            // If device is in error state, clear it
            if (state == STATE_DFU_ERROR)
            {
                FirmwareTrace.Info("USB DFU: device in error state, clearing...");
                ClearStatus();
                state = GetState();
                FirmwareTrace.Info("USB DFU: state after clear = {0} ({1})", state, StateName(state));
            }

            // Abort any pending operation to reach idle
            if (state != STATE_DFU_IDLE)
            {
                FirmwareTrace.Info("USB DFU: aborting pending operation to reach idle...");
                Abort();
                PollUntilState(STATE_DFU_IDLE, cancellationToken);
            }

            FirmwareTrace.Info("USB DFU: device ready for firmware transfer.");
            return Task.CompletedTask;
        }

        public async Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_backend == null)
                throw new InvalidOperationException("USB DFU connection is not open. Call OpenAsync first.");

            int totalBytes = data.Length;
            int totalBlocks = (totalBytes + _transferSize - 1) / _transferSize;
            _blockNum = 0;

            FirmwareTrace.Info(
                "USB DFU: starting download — {0} bytes in {1} blocks (block size: {2})",
                totalBytes, totalBlocks, _transferSize);

            var sw = Stopwatch.StartNew();

            for (int offset = 0; offset < totalBytes; offset += _transferSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int remaining = totalBytes - offset;
                int blockSize = Math.Min(_transferSize, remaining);
                byte[] block = new byte[blockSize];
                Array.Copy(data, offset, block, 0, blockSize);

                FirmwareTrace.Debug(
                    "USB DFU: DNLOAD block {0}/{1} ({2} bytes)",
                    _blockNum, totalBlocks, blockSize);

                // Send DNLOAD
                Dnload(_blockNum, block);

                // Poll until device is ready for next block (STATE_DFU_DNLOAD_IDLE)
                PollUntilState(STATE_DFU_DNLOAD_IDLE, cancellationToken);

                _blockNum++;
            }

            // Final zero-length DNLOAD to signal end of transfer and enter MANIFEST
            FirmwareTrace.Info("USB DFU: sending final zero-length DNLOAD to enter MANIFEST phase...");
            Dnload(_blockNum, Array.Empty<byte>());

            // Poll for MANIFEST completion
            // After manifest, device may go to STATE_DFU_MANIFEST_WAIT_RESET then reset
            // or back to STATE_DFU_IDLE
            byte finalState = PollUntilState(
                new[] { STATE_DFU_IDLE, STATE_DFU_MANIFEST_WAIT_RESET },
                cancellationToken);

            sw.Stop();

            FirmwareTrace.Info(
                "USB DFU: download complete — {0} bytes, {1} blocks, {2:F1}s. Final state: {3}",
                totalBytes, _blockNum, sw.Elapsed.TotalSeconds, StateName(finalState));
        }

        public Task CloseAsync()
        {
            if (_backend != null)
            {
                try
                {
                    _backend.ReleaseInterface(_dfuInterface);
                }
                catch (Exception ex)
                {
                    FirmwareTrace.Warn("USB DFU: error releasing interface: {0}", ex.Message);
                }

                try
                {
                    _backend.Dispose();
                }
                catch (Exception ex)
                {
                    FirmwareTrace.Warn("USB DFU: error closing device: {0}", ex.Message);
                }

                _backend = null;
                FirmwareTrace.Info("USB DFU: connection closed.");
            }
            return Task.CompletedTask;
        }

        // ── DFU Protocol Operations ─────────────────────────────────────

        private void Detach(int timeoutMs)
        {
            _backend.ControlTransfer(USB_REQ_TYPE_OUT, DFU_DETACH, timeoutMs, _dfuInterface, null, 0);
        }

        private void Dnload(int blockNum, byte[] data)
        {
            _backend.ControlTransfer(USB_REQ_TYPE_OUT, DFU_DNLOAD, blockNum, _dfuInterface, data, data.Length);
        }

        private byte[] Upload(int blockNum, int length)
        {
            return _backend.ControlTransferIn(USB_REQ_TYPE_IN, DFU_UPLOAD, blockNum, _dfuInterface, length);
        }

        private DfuStatus GetStatus()
        {
            byte[] response = _backend.ControlTransferIn(USB_REQ_TYPE_IN, DFU_GETSTATUS, 0, _dfuInterface, 6);
            if (response == null || response.Length < 6)
                throw new InvalidOperationException("USB DFU: invalid GETSTATUS response.");

            return new DfuStatus
            {
                Status  = response[0],
                PollTimeout = (uint)(response[1] | (response[2] << 8) | (response[3] << 16)),
                State   = response[4],
                StringIndex = response[5]
            };
        }

        private void ClearStatus()
        {
            _backend.ControlTransfer(USB_REQ_TYPE_OUT, DFU_CLRSTATUS, 0, _dfuInterface, null, 0);
        }

        private byte GetState()
        {
            byte[] response = _backend.ControlTransferIn(USB_REQ_TYPE_IN, DFU_GETSTATE, 0, _dfuInterface, 1);
            if (response == null || response.Length < 1)
                throw new InvalidOperationException("USB DFU: invalid GETSTATE response.");
            return response[0];
        }

        private void Abort()
        {
            _backend.ControlTransfer(USB_REQ_TYPE_OUT, DFU_ABORT, 0, _dfuInterface, null, 0);
        }

        private void PollUntilState(byte expectedState, CancellationToken cancellationToken)
        {
            PollUntilState(new[] { expectedState }, cancellationToken);
        }

        private byte PollUntilState(byte[] expectedStates, CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + STATUS_POLL_TIMEOUT;
            int pollCount = 0;

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                DfuStatus status;
                try
                {
                    status = GetStatus();
                }
                catch (Exception ex)
                {
                    FirmwareTrace.Warn("USB DFU: status poll error (attempt {0}): {1}", pollCount, ex.Message);
                    Thread.Sleep(STATUS_POLL_INTERVAL_MS);
                    pollCount++;
                    continue;
                }

                pollCount++;

                // Check if the state matches any expected state
                for (int i = 0; i < expectedStates.Length; i++)
                {
                    if (status.State == expectedStates[i])
                    {
                        FirmwareTrace.Debug(
                            "USB DFU: poll reached state {0} after {1} attempts",
                            StateName(status.State), pollCount);
                        return status.State;
                    }
                }

                // Check for error state
                if (status.State == STATE_DFU_ERROR)
                {
                    FirmwareTrace.Error(
                        "USB DFU: device entered error state (status={0}, pollTimeout={1}ms)",
                        status.Status, status.PollTimeout);
                    throw new DfuException(
                        string.Format("DFU device reported error: status={0}, state=dfuERROR", status.Status),
                        status);
                }

                // Use device-reported poll timeout, capped at 500ms
                int delayMs = Math.Min(status.PollTimeout > 0 ? (int)status.PollTimeout : STATUS_POLL_INTERVAL_MS, 500);
                if (delayMs < STATUS_POLL_INTERVAL_MS) delayMs = STATUS_POLL_INTERVAL_MS;

                Thread.Sleep(delayMs);
            }

            byte currentState = GetState();
            throw new TimeoutException(
                string.Format(
                    "USB DFU: timed out polling for state(s) after {0} attempts. Current state: {1} ({2}).",
                    pollCount, currentState, StateName(currentState)));
        }

        private static string StateName(byte state)
        {
            switch (state)
            {
                case STATE_APP_IDLE:              return "appIDLE";
                case STATE_APP_DETACH:            return "appDETACH";
                case STATE_DFU_IDLE:              return "dfuIDLE";
                case STATE_DFU_DNLOAD_SYNC:       return "dfuDNLOAD-SYNC";
                case STATE_DFU_DNBUSY:            return "dfuDNBUSY";
                case STATE_DFU_DNLOAD_IDLE:       return "dfuDNLOAD-IDLE";
                case STATE_DFU_MANIFEST_SYNC:     return "dfuMANIFEST-SYNC";
                case STATE_DFU_MANIFEST:          return "dfuMANIFEST";
                case STATE_DFU_MANIFEST_WAIT_RESET: return "dfuMANIFEST-WAIT-RESET";
                case STATE_DFU_UPLOAD_IDLE:       return "dfuUPLOAD-IDLE";
                case STATE_DFU_ERROR:             return "dfuERROR";
                default:                          return string.Format("unknown(0x{0:X2})", state);
            }
        }

        // ── Backend Factory ──────────────────────────────────────────

        private static IDfuUsbBackend CreateBackend()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                FirmwareTrace.Debug("USB DFU: using libusb-1.0 backend (Linux).");
                return new LibUsbBackend();
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                FirmwareTrace.Debug("USB DFU: using WinUSB backend (Windows).");
                return new WinUsbBackend();
            }
            throw new PlatformNotSupportedException(
                "USB DFU is only supported on Linux (libusb-1.0) and Windows (WinUSB).");
        }
    }

    // ── Data Types ─────────────────────────────────────────────────

    /// <summary>
    /// Represents a DFU GETSTATUS response (6 bytes per DFU 1.1 spec).
    /// </summary>
    internal struct DfuStatus
    {
        /// <summary>bStatus — OK=0x00, errTARGET=0x01, errFILE=0x02, etc.</summary>
        public byte Status;
        /// <summary>bwPollTimeout — milliseconds between status polls (24-bit LE).</summary>
        public uint PollTimeout;
        /// <summary>bState — current DFU device state.</summary>
        public byte State;
        /// <summary>iString — string descriptor index for status message.</summary>
        public byte StringIndex;
    }

    /// <summary>
    /// Exception thrown for DFU protocol-level errors.
    /// </summary>
    internal class DfuException : Exception
    {
        public DfuStatus Status { get; }

        public DfuException(string message, DfuStatus status)
            : base(message)
        {
            Status = status;
        }
    }

    // ── USB Backend Interface ──────────────────────────────────────

    /// <summary>
    /// Platform abstraction for USB device communication.
    /// Each platform (Linux libusb, Windows WinUSB) provides its own implementation.
    /// </summary>
    internal interface IDfuUsbBackend : IDisposable
    {
        void Open(ushort vendorId, ushort productId);
        void ClaimInterface(int interfaceNum);
        void ReleaseInterface(int interfaceNum);
        void ControlTransfer(byte requestType, byte request, int value, int index, byte[] data, int length);
        byte[] ControlTransferIn(byte requestType, byte request, int value, int index, int length);
        int? GetDfuTransferSize(int interfaceNum);
    }

    // ── Linux Backend: libusb-1.0 P/Invoke ────────────────────────

    internal class LibUsbBackend : IDfuUsbBackend
    {
        private IntPtr _ctx;
        private IntPtr _deviceHandle;
        private bool _disposed;

        private static class Native
        {
            public const string LibName = "libusb-1.0.so.0";

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libusb_init(out IntPtr ctx);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libusb_exit(IntPtr ctx);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr libusb_open_device_with_vid_pid(IntPtr ctx, ushort vendorId, ushort productId);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libusb_claim_interface(IntPtr handle, int interfaceNum);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libusb_release_interface(IntPtr handle, int interfaceNum);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int libusb_control_transfer(
                IntPtr handle, byte requestType, byte request,
                ushort value, ushort index, byte[] data, ushort length, uint timeout);

            [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
            public static extern void libusb_close(IntPtr handle);
        }

        public void Open(ushort vendorId, ushort productId)
        {
            int rc = Native.libusb_init(out _ctx);
            if (rc != 0)
                throw new InvalidOperationException(
                    string.Format("libusb_init failed with error {0}. Is libusb-1.0 installed?", rc));

            _deviceHandle = Native.libusb_open_device_with_vid_pid(_ctx, vendorId, productId);
            if (_deviceHandle == IntPtr.Zero)
            {
                Native.libusb_exit(_ctx);
                _ctx = IntPtr.Zero;
                throw new InvalidOperationException(
                    string.Format("USB DFU device not found: VID=0x{0:X4}, PID=0x{1:X4}. " +
                        "Check device connection and permissions (udev rules may be required).",
                        vendorId, productId));
            }

            FirmwareTrace.Info("libusb: device opened VID=0x{0:X4} PID=0x{1:X4}", vendorId, productId);
        }

        public void ClaimInterface(int interfaceNum)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device not opened.");

            int rc = Native.libusb_claim_interface(_deviceHandle, interfaceNum);
            if (rc != 0)
            {
                throw new InvalidOperationException(
                    string.Format("libusb_claim_interface({0}) failed with error {1}. " +
                        "The interface may be in use by a kernel driver.", interfaceNum, rc));
            }
        }

        public void ReleaseInterface(int interfaceNum)
        {
            if (_deviceHandle != IntPtr.Zero)
            {
                Native.libusb_release_interface(_deviceHandle, interfaceNum);
            }
        }

        public void ControlTransfer(byte requestType, byte request, int value, int index, byte[] data, int length)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device not opened.");

            byte[] buffer = data ?? Array.Empty<byte>();
            int rc = Native.libusb_control_transfer(
                _deviceHandle, requestType, request,
                (ushort)value, (ushort)index, buffer, (ushort)length, 5000);

            if (rc < 0)
            {
                throw new InvalidOperationException(
                    string.Format("libusb_control_transfer failed: error {0} (request=0x{1:X2}, value={2})",
                        rc, request, value));
            }
        }

        public byte[] ControlTransferIn(byte requestType, byte request, int value, int index, int length)
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device not opened.");

            byte[] buffer = new byte[length];
            int rc = Native.libusb_control_transfer(
                _deviceHandle, requestType, request,
                (ushort)value, (ushort)index, buffer, (ushort)length, 5000);

            if (rc < 0)
            {
                throw new InvalidOperationException(
                    string.Format("libusb_control_transfer (in) failed: error {0} (request=0x{1:X2})",
                        rc, request));
            }

            // Trim to actual received bytes
            if (rc < length)
            {
                byte[] trimmed = new byte[rc];
                Array.Copy(buffer, trimmed, rc);
                return trimmed;
            }

            return buffer;
        }

        public int? GetDfuTransferSize(int interfaceNum)
        {
            // libusb doesn't expose DFU descriptor directly via control transfers.
            // The transfer size is part of the DFU functional descriptor.
            // We could parse it from the configuration descriptor, but for simplicity
            // we return null and let the default transfer size be used.
            // Full implementation would use libusb_get_config_descriptor to find
            // the DFU functional descriptor and parse wTransferSize.
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_deviceHandle != IntPtr.Zero)
                {
                    Native.libusb_close(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
                if (_ctx != IntPtr.Zero)
                {
                    Native.libusb_exit(_ctx);
                    _ctx = IntPtr.Zero;
                }
            }
        }
    }

    // ── Windows Backend: WinUSB + SetupAPI P/Invoke ───────────────

    internal class WinUsbBackend : IDfuUsbBackend
    {
        private IntPtr _deviceHandle = IntPtr.Zero;
        private IntPtr _winUsbHandle = IntPtr.Zero;
        private bool _disposed;

        private static class Native
        {
            // ── SetupAPI ──────────────────────────────────────
            public const string SetupApi = "setupapi.dll";

            public const uint DIGCF_PRESENT      = 0x02;
            public const uint DIGCF_DEVICEINTERFACE = 0x10;

            [DllImport(SetupApi, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr SetupDiGetClassDevs(
                ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

            [DllImport(SetupApi, SetLastError = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiEnumDeviceInterfaces(
                IntPtr deviceInfoSet, IntPtr deviceInfoData,
                ref Guid interfaceClassGuid, uint memberIndex,
                ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

            [DllImport(SetupApi, SetLastError = true, CharSet = CharSet.Unicode)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiGetDeviceInterfaceDetail(
                IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
                IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize,
                out uint requiredSize, IntPtr deviceInfoData);

            [DllImport(SetupApi, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

            // ── Kernel32 ──────────────────────────────────────
            public const string Kernel32 = "kernel32.dll";

            public const uint GENERIC_READ  = 0x80000000;
            public const uint GENERIC_WRITE = 0x40000000;
            public const uint FILE_SHARE_READ  = 0x00000001;
            public const uint FILE_SHARE_WRITE = 0x00000002;
            public const uint OPEN_EXISTING = 3;
            public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

            [DllImport(Kernel32, SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateFile(
                string fileName, uint desiredAccess, uint shareMode,
                IntPtr securityAttributes, uint creationDisposition,
                uint flagsAndAttributes, IntPtr templateFile);

            [DllImport(Kernel32, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr handle);

            // ── WinUSB ────────────────────────────────────────
            public const string WinUsb = "winusb.dll";

            [DllImport(WinUsb, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WinUsb_Initialize(IntPtr deviceHandle, out IntPtr interfaceHandle);

            [DllImport(WinUsb, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WinUsb_Free(IntPtr interfaceHandle);

            [DllImport(WinUsb, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool WinUsb_ControlTransfer(
                IntPtr interfaceHandle,
                ref WINUSB_SETUP_PACKET setupPacket,
                byte[] buffer, uint bufferLength,
                out uint lengthTransferred,
                IntPtr overlapped);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct WINUSB_SETUP_PACKET
        {
            public byte RequestType;
            public byte Request;
            public ushort Value;
            public ushort Index;
            public ushort Length;
        }

        // WinUSB device interface GUID for DFU
        // Standard GUID_DEVINTERFACE_USB_DEVICE: {A5DCBF10-6530-11D2-901F-00C04FB951ED}
        private static readonly Guid UsbDeviceGuid = new Guid("{A5DCBF10-6530-11D2-901F-00C04FB951ED}");

        public void Open(ushort vendorId, ushort productId)
        {
            // Discover device path via SetupAPI
            string devicePath = FindDevicePath(vendorId, productId);
            if (devicePath == null)
            {
                throw new InvalidOperationException(
                    string.Format("USB DFU device not found: VID=0x{0:X4}, PID=0x{1:X4}. " +
                        "Check device connection and driver (WinUSB driver required).",
                        vendorId, productId));
            }

            FirmwareTrace.Debug("WinUSB: device path = {0}", devicePath);

            // Open the device
            _deviceHandle = Native.CreateFile(
                devicePath,
                Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero,
                Native.OPEN_EXISTING,
                Native.FILE_FLAG_OVERLAPPED,
                IntPtr.Zero);

            if (_deviceHandle == IntPtr.Zero || _deviceHandle == new IntPtr(-1))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("Failed to open USB device (error {0}). Administrator privileges may be required.", error));
            }

            // Initialize WinUSB
            if (!Native.WinUsb_Initialize(_deviceHandle, out _winUsbHandle))
            {
                int error = Marshal.GetLastWin32Error();
                Native.CloseHandle(_deviceHandle);
                _deviceHandle = IntPtr.Zero;
                throw new InvalidOperationException(
                    string.Format("WinUsb_Initialize failed (error {0}). Verify WinUSB driver is installed for this device.", error));
            }

            FirmwareTrace.Info("WinUSB: device opened VID=0x{0:X4} PID=0x{1:X4}", vendorId, productId);
        }

        public void ClaimInterface(int interfaceNum)
        {
            // WinUSB doesn't have explicit interface claiming —
            // the WinUsb_Initialize call has already associated the handle with the device.
            // Interface setting is implicit for single-interface DFU devices.
            FirmwareTrace.Debug("WinUSB: interface {0} assumed (WinUSB auto-claims)", interfaceNum);
        }

        public void ReleaseInterface(int interfaceNum)
        {
            // No-op: WinUSB doesn't require explicit interface release.
        }

        public void ControlTransfer(byte requestType, byte request, int value, int index, byte[] data, int length)
        {
            if (_winUsbHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device not opened.");

            var setup = new WINUSB_SETUP_PACKET
            {
                RequestType = requestType,
                Request = request,
                Value = (ushort)value,
                Index = (ushort)index,
                Length = (ushort)length
            };

            byte[] buffer = data ?? Array.Empty<byte>();
            if (!Native.WinUsb_ControlTransfer(_winUsbHandle, ref setup, buffer, (uint)length, out uint transferred, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("WinUsb_ControlTransfer failed: error {0} (request=0x{1:X2}, value={2})",
                        error, request, value));
            }
        }

        public byte[] ControlTransferIn(byte requestType, byte request, int value, int index, int length)
        {
            if (_winUsbHandle == IntPtr.Zero)
                throw new InvalidOperationException("Device not opened.");

            var setup = new WINUSB_SETUP_PACKET
            {
                RequestType = requestType,
                Request = request,
                Value = (ushort)value,
                Index = (ushort)index,
                Length = (ushort)length
            };

            byte[] buffer = new byte[length];
            if (!Native.WinUsb_ControlTransfer(_winUsbHandle, ref setup, buffer, (uint)length, out uint transferred, IntPtr.Zero))
            {
                int error = Marshal.GetLastWin32Error();
                throw new InvalidOperationException(
                    string.Format("WinUsb_ControlTransfer (in) failed: error {0} (request=0x{1:X2})",
                        error, request));
            }

            if (transferred < length)
            {
                byte[] trimmed = new byte[transferred];
                Array.Copy(buffer, trimmed, transferred);
                return trimmed;
            }

            return buffer;
        }

        public int? GetDfuTransferSize(int interfaceNum)
        {
            // WinUSB doesn't directly expose the DFU functional descriptor.
            // Full implementation would use WinUsb_GetDescriptor to read the
            // configuration descriptor and parse the DFU functional descriptor.
            return null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                if (_winUsbHandle != IntPtr.Zero)
                {
                    Native.WinUsb_Free(_winUsbHandle);
                    _winUsbHandle = IntPtr.Zero;
                }
                if (_deviceHandle != IntPtr.Zero && _deviceHandle != new IntPtr(-1))
                {
                    Native.CloseHandle(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Finds a USB device by VID/PID using SetupAPI and returns its device path.
        /// </summary>
        private static string FindDevicePath(ushort vendorId, ushort productId)
        {
            IntPtr deviceInfoSet = IntPtr.Zero;
            try
            {
                Guid usbGuid = UsbDeviceGuid;
                deviceInfoSet = Native.SetupDiGetClassDevs(
                    ref usbGuid, null, IntPtr.Zero,
                    Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);

                if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
                    return null;

                uint memberIndex = 0;
                while (true)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA();
                    interfaceData.cbSize = (uint)Marshal.SizeOf(interfaceData);

                    if (!Native.SetupDiEnumDeviceInterfaces(
                        deviceInfoSet, IntPtr.Zero, ref usbGuid,
                        memberIndex, ref interfaceData))
                    {
                        break; // No more devices
                    }

                    // Get required buffer size
                    Native.SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet, ref interfaceData, IntPtr.Zero, 0,
                        out uint requiredSize, IntPtr.Zero);

                    IntPtr detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                    try
                    {
                        // Windows struct: first 4 or 8 bytes are cbSize (pointer-sized on 64-bit)
                        Marshal.WriteInt32(detailBuffer, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 8 : 6);

                        if (Native.SetupDiGetDeviceInterfaceDetail(
                            deviceInfoSet, ref interfaceData, detailBuffer,
                            requiredSize, out requiredSize, IntPtr.Zero))
                        {
                            // Device path is at offset 4 (32-bit) or 8 (64-bit) after cbSize
                            int pathOffset = IntPtr.Size == 8 ? 8 : 4;
                            string devicePath = Marshal.PtrToStringUni(
                                detailBuffer + pathOffset);

                            // Check if the hardware ID matches our VID/PID
                            if (DevicePathMatchesVidPid(devicePath, vendorId, productId))
                            {
                                return devicePath;
                            }
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailBuffer);
                    }

                    memberIndex++;
                }

                return null;
            }
            finally
            {
                if (deviceInfoSet != IntPtr.Zero && deviceInfoSet != new IntPtr(-1))
                {
                    Native.SetupDiDestroyDeviceInfoList(deviceInfoSet);
                }
            }
        }

        /// <summary>
        /// Checks whether a device path string contains the expected VID and PID.
        /// Example path: \\?\USB#VID_0483&PID_DF11#...
        /// </summary>
        private static bool DevicePathMatchesVidPid(string devicePath, ushort vendorId, ushort productId)
        {
            if (string.IsNullOrWhiteSpace(devicePath)) return false;

            string expectedVid = string.Format("VID_{0:X4}", vendorId);
            string expectedPid = string.Format("PID_{0:X4}", productId);

            string upperPath = devicePath.ToUpperInvariant();
            return upperPath.Contains(expectedVid.ToUpperInvariant())
                && upperPath.Contains(expectedPid.ToUpperInvariant());
        }
    }
}
