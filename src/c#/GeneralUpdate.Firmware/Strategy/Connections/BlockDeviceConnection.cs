using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Connection implementation for local block devices (eMMC, SD card, SATA, NVMe).
    /// Wraps FileStream for raw sector read/write operations.
    /// 
    /// <para>
    /// Linux paths: /dev/mmcblk0, /dev/sda
    /// Windows paths: \\.\PhysicalDrive0, \\.\C:
    /// </para>
    /// </summary>
    internal class BlockDeviceConnection : IConnection
    {
        private readonly string _devicePath;
        private FileStream _stream;

        public BlockDeviceConnection(string devicePath)
        {
            _devicePath = devicePath ?? throw new ArgumentNullException(nameof(devicePath));
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            FirmwareTrace.Debug("Opening block device: {0}", _devicePath);

            _stream = new FileStream(
                _devicePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: 1024 * 1024,
                useAsync: true);

            FirmwareTrace.Info("Block device opened: {0} (size={1} bytes)", _devicePath, _stream.Length);
            return Task.CompletedTask;
        }

        public async Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            if (_stream == null)
                throw new InvalidOperationException("Connection is not open. Call OpenAsync first.");

            FirmwareTrace.Info("Writing {0} bytes to block device...", data.Length);

            await _stream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);

            FirmwareTrace.Info("Block device write completed: {0} bytes", data.Length);
        }

        public Task CloseAsync()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
                FirmwareTrace.Debug("Block device connection closed.");
            }
            return Task.CompletedTask;
        }
    }
}
