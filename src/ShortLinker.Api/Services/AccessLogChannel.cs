using System.Threading.Channels;
using ShortLinker.Api.Models;

namespace ShortLinker.Api.Services;

public class AccessLogChannel
{
    private readonly Channel<LinkAccessLog> _channel;

    public AccessLogChannel()
    {
        _channel = Channel.CreateUnbounded<LinkAccessLog>();
    }

    public async ValueTask AddLogAsync(LinkAccessLog log, CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(log, ct);
    }

    public IAsyncEnumerable<LinkAccessLog> ReadAllAsync(CancellationToken ct = default)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}