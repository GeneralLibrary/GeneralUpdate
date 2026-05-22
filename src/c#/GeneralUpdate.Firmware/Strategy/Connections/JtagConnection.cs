using System;
using System.Threading;
using System.Threading.Tasks;
using GeneralUpdate.Firmware.Models;
using GeneralUpdate.Firmware.Trace;

namespace GeneralUpdate.Firmware.Strategy.Connections
{
    /// <summary>
    /// Stub connection for JTAG/SWD debug probe firmware flashing.
    /// Will use Process("openocd") subprocess for bare-metal flashing.
    /// </summary>
    internal class JtagConnection : IConnection
    {
        private readonly DeviceConnection _config;

        public JtagConnection(DeviceConnection config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            FirmwareTrace.Info(
                "JTAG connection: config={0}",
                _config.JtagConfig);

            FirmwareTrace.Warn(
                "JTAG/SWD support is a framework stub. " +
                "Full OpenOCD integration will be provided in a follow-up PR.");

            // In the full implementation:
            //   Process.Start("openocd", "-f interface/stlink.cfg -f target/stm32f4x.cfg")
            //   Telnet to OpenOCD → halt → flash write_image → reset

            return Task.CompletedTask;
        }

        public Task WriteAsync(byte[] data, CancellationToken cancellationToken)
        {
            throw new NotImplementedException(
                "JTAG firmware flashing is a framework stub. " +
                "Full implementation will support OpenOCD integration " +
                "for ARM Cortex-M, RISC-V, and other debug targets.");
        }

        public Task CloseAsync()
        {
            FirmwareTrace.Debug("JTAG connection closed.");
            return Task.CompletedTask;
        }
    }
}
