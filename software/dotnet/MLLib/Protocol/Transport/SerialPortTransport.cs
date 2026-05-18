using System.IO.Ports;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace MLLib.Protocol.Transport;

/// <summary>
/// <see cref="System.IO.Ports.SerialPort"/> を <see cref="ISerialTransport"/> でラップする実装。
/// USB-CDC や XBee USB アダプタなどシリアル経由の transport で使う。
/// コンストラクタで Open まで実行する。失敗したら例外。
/// </summary>
public sealed class SerialPortTransport : ISerialTransport
{
    private readonly SerialPort _port;
    private readonly Subject<ReadOnlyMemory<byte>> _received = new();
    private readonly Subject<bool> _connectionState = new();
    private bool _disposed;

    public SerialPortTransport(string portName, int baudRate = 115200)
    {
        _port = new SerialPort(portName, baudRate)
        {
            ReadTimeout = 500,
            WriteTimeout = 500,
        };

        try
        {
            _port.DataReceived += OnDataReceived;
            _port.Open();
            _connectionState.OnNext(true);
        }
        catch
        {
            _port.DataReceived -= OnDataReceived;
            _port.Dispose();
            _received.Dispose();
            _connectionState.Dispose();
            throw;
        }
    }

    public bool IsConnected => _port.IsOpen;
    public IObservable<bool> ConnectionState => _connectionState.AsObservable();
    public IObservable<ReadOnlyMemory<byte>> Received => _received.AsObservable();

    public Task SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_port.IsOpen) throw new InvalidOperationException("port not open");
        // SerialPort.Write は同期 API なので Task でラップ
        _port.Write(data.ToArray(), 0, data.Length);
        return Task.CompletedTask;
    }

    private void OnDataReceived(object? sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            int available = _port.BytesToRead;
            if (available <= 0) return;
            var buf = new byte[available];
            int read = _port.Read(buf, 0, available);
            if (read > 0) _received.OnNext(buf.AsMemory(0, read));
        }
        catch
        {
            // Port が並列に閉じられた場合などに例外を握りつぶす
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            _port.DataReceived -= OnDataReceived;
            if (_port.IsOpen)
            {
                _port.Close();
                _connectionState.OnNext(false);
            }
            _port.Dispose();
        }
        catch { /* close errors ignored */ }

        _received.OnCompleted();
        _received.Dispose();
        _connectionState.OnCompleted();
        _connectionState.Dispose();
    }
}
