using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GeneralUpdate.Firmware.Strategy.Connections.Protocol
{
    /// <summary>
    /// Shared stream helpers for serial transfer protocols.
    /// </summary>
    internal static class StreamHelpers
    {
        // Protocol control characters
        public const byte SOH = 0x01;
        public const byte STX = 0x02;
        public const byte EOT = 0x04;
        public const byte ACK = 0x06;
        public const byte NAK = 0x15;
        public const byte CAN = 0x18;
        public const byte C   = 0x43;

        public const int DefaultTimeoutMs = 10000;
        public const int MaxRetries = 10;

        /// <summary>
        /// Reads a single byte from the stream with a timeout.
        /// Returns -1 if the stream ended.
        /// </summary>
        public static async Task<int> ReadByteWithTimeoutAsync(
            Stream stream, int timeoutMs, CancellationToken cancellationToken)
        {
            using (var cts = new CancellationTokenSource(timeoutMs))
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
            {
                byte[] buffer = new byte[1];
                Task<int> readTask = stream.ReadAsync(buffer, 0, 1, linked.Token);

                try
                {
                    int result = await readTask.ConfigureAwait(false);
                    return result == 0 ? -1 : buffer[0];
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    throw new TimeoutException(string.Format(
                        "Timed out waiting for byte after {0}ms.", timeoutMs));
                }
            }
        }

        /// <summary>
        /// Drains stale data from the stream (up to 4KB).
        /// </summary>
        public static async Task DrainAsync(Stream stream, CancellationToken cancellationToken)
        {
            try
            {
                byte[] buffer = new byte[256];
                int totalDrained = 0;
                while (totalDrained < 4096)
                {
                    using (var cts = new CancellationTokenSource(200))
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken))
                    {
                        try
                        {
                            int read = await stream.ReadAsync(buffer, 0, buffer.Length, linked.Token)
                                .ConfigureAwait(false);
                            if (read == 0) break;
                            totalDrained += read;
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested)
                        {
                            break;
                        }
                    }
                }
            }
            catch (IOException)
            {
                // Stream may not support timeout — ignore
            }
        }

        /// <summary>
        /// Sends EOT (End of Transmission) and waits for ACK or CAN.
        /// </summary>
        public static async Task SendEotAsync(
            Stream stream, CancellationToken cancellationToken)
        {
            for (int retry = 0; retry < MaxRetries; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                stream.WriteByte(EOT);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                int response = await ReadByteWithTimeoutAsync(stream, DefaultTimeoutMs, cancellationToken)
                    .ConfigureAwait(false);

                if (response == ACK || response == CAN) return;

                // NAK or other — retry
            }
        }
    }
}
