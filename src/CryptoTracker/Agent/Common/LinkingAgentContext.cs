using CryptoTracker.Agent.Services;
using CryptoTracker.Shared;

namespace CryptoTracker.Agent.Common;

public sealed class LinkingAgentContext
{
    public LinkingAgentContext(
        InteractiveLinkingSession session,
        Func<LinkingEventDTO, Task> sendEventAsync,
        bool allowMemorySave,
        bool allowQuestions)
    {
        Session = session;
        SendEventAsync = sendEventAsync;
        AllowMemorySave = allowMemorySave;
        AllowQuestions = allowQuestions;
    }

    public InteractiveLinkingSession Session { get; }
    public Func<LinkingEventDTO, Task> SendEventAsync { get; }
    public bool AllowMemorySave { get; }
    public bool AllowQuestions { get; }
}

public interface ILinkingAgentContextAccessor
{
    LinkingAgentContext? Current { get; set; }
    IDisposable Use(LinkingAgentContext context);
}

public sealed class LinkingAgentContextAccessor : ILinkingAgentContextAccessor
{
    private readonly AsyncLocal<LinkingAgentContext?> _current = new();

    public LinkingAgentContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public IDisposable Use(LinkingAgentContext context)
    {
        var previous = Current;
        Current = context;
        return new RestoreDisposable(() => Current = previous);
    }

    private sealed class RestoreDisposable : IDisposable
    {
        private readonly Action _restore;
        private bool _disposed;

        public RestoreDisposable(Action restore)
        {
            _restore = restore;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _restore();
        }
    }
}
