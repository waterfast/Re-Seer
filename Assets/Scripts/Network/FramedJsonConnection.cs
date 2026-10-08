using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReSeer.Network
{
    /// <summary>Length-prefixed UTF-8 transport. It never touches Unity objects.</summary>
    public sealed class FramedJsonConnection : IDisposable
    {
        public const int MaximumFrameBytes = 64 * 1024;
        private readonly TcpClient client = new TcpClient();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private readonly ConcurrentQueue<string> failures = new ConcurrentQueue<string>();
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private NetworkStream stream;
        private volatile bool disposed;

        public bool TryReceive(out string message) => incoming.TryDequeue(out message);
        public bool TryGetFailure(out string message) => failures.TryDequeue(out message);

        public async Task ConnectAsync(string host, int port)
        {
            Task connect = client.ConnectAsync(host, port);
            if (await Task.WhenAny(connect, Task.Delay(5000)).ConfigureAwait(false) != connect)
            {
                Dispose();
                // Observe a late failure after disposal as well as the timeout.
                _ = connect.ContinueWith(task => { var ignored = task.Exception; },
                    TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("连接服务器超时。");
            }
            await connect.ConfigureAwait(false);
            if (disposed) throw new ObjectDisposedException(nameof(FramedJsonConnection));
            client.NoDelay = true;
            stream = client.GetStream();
            _ = ReceiveLoopAsync();
        }

        public async Task SendAsync(string json)
        {
            byte[] body = Utf8.GetBytes(json);
            if (body.Length == 0 || body.Length > MaximumFrameBytes)
                throw new InvalidDataException("消息长度不合法。");
            byte[] header = { (byte)(body.Length >> 24), (byte)(body.Length >> 16),
                (byte)(body.Length >> 8), (byte)body.Length };
            await sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (disposed || stream == null) throw new IOException("连接已关闭。");
                await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
                await stream.WriteAsync(body, 0, body.Length).ConfigureAwait(false);
            }
            finally { sendLock.Release(); }
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                var header = new byte[4];
                while (!disposed)
                {
                    await ReadExactlyAsync(header).ConfigureAwait(false);
                    uint size = ((uint)header[0] << 24) | ((uint)header[1] << 16) |
                        ((uint)header[2] << 8) | header[3];
                    if (size == 0 || size > MaximumFrameBytes)
                        throw new InvalidDataException("服务器消息长度不合法。");
                    var body = new byte[(int)size];
                    await ReadExactlyAsync(body).ConfigureAwait(false);
                    if (incoming.Count >= 256) throw new IOException("服务器消息积压过多。");
                    incoming.Enqueue(Utf8.GetString(body));
                }
            }
            catch (Exception error)
            {
                if (!disposed) failures.Enqueue(error.Message);
                Dispose();
            }
        }

        private async Task ReadExactlyAsync(byte[] bytes)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                int read = await stream.ReadAsync(bytes, offset, bytes.Length - offset).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("服务器断开了连接。");
                offset += read;
            }
        }

        public void Dispose()
        {
            disposed = true;
            client.Close();
        }
    }
}
