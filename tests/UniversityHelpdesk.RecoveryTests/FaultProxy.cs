using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace UniversityHelpdesk.RecoveryTests;

/// <summary>
/// A TCP forwarder placed between a component and a real service to simulate an outage without
/// stopping the shared service. <see cref="Cut"/> resets every open connection and drops new ones
/// until <see cref="Restore"/> is called.
/// </summary>
public sealed class FaultProxy : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly ConcurrentDictionary<Guid, (TcpClient Client, TcpClient Server)> _links = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly string _targetHost;
    private readonly int _targetPort;
    private readonly Task _acceptLoop;
    private volatile bool _open = true;
    private int _rejected;

    public FaultProxy(string targetHost, int targetPort)
    {
        _targetHost = targetHost;
        _targetPort = targetPort;
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptLoopAsync();
    }

    public int Port { get; }

    /// <summary>Connections dropped while the proxy was cut.</summary>
    public int RejectedConnections => Volatile.Read(ref _rejected);

    public void Cut()
    {
        _open = false;
        foreach (var (id, link) in _links)
        {
            Reset(link.Client);
            Reset(link.Server);
            _links.TryRemove(id, out _);
        }
    }

    public void Restore() => _open = true;

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token);
            }
            catch (Exception) when (_stop.IsCancellationRequested)
            {
                return;
            }

            if (!_open)
            {
                Interlocked.Increment(ref _rejected);
                Reset(client);
                continue;
            }

            _ = ForwardAsync(client);
        }
    }

    private async Task ForwardAsync(TcpClient client)
    {
        var server = new TcpClient();
        var id = Guid.NewGuid();
        try
        {
            await server.ConnectAsync(_targetHost, _targetPort, _stop.Token);
            _links[id] = (client, server);
            await Task.WhenAny(
                client.GetStream().CopyToAsync(server.GetStream(), _stop.Token),
                server.GetStream().CopyToAsync(client.GetStream(), _stop.Token));
        }
        catch (Exception)
        {
            // Either side went away; both are closed below.
        }
        finally
        {
            _links.TryRemove(id, out _);
            Reset(client);
            Reset(server);
        }
    }

    /// <summary>Closes with a TCP reset, as if the peer had crashed.</summary>
    private static void Reset(TcpClient connection)
    {
        try
        {
            connection.LingerState = new LingerOption(true, 0);
            connection.Close();
        }
        catch (Exception)
        {
            // Already closed.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _listener.Stop();
        Cut();
        await _acceptLoop;
        _stop.Dispose();
    }
}
