using CryptoTracker.Agent.Services;
using CryptoTracker.Shared;

namespace CryptoTracker.Agent.Common;

public sealed class LotLinkingAgentContext
{
    public LotLinkingAgentContext(
        InteractiveLotLinkingSession session,
        Func<LotLinkingEventDTO, Task> sendEventAsync,
        bool allowMemorySave,
        bool allowQuestions)
    {
        Session = session;
        SendEventAsync = sendEventAsync;
        AllowMemorySave = allowMemorySave;
        AllowQuestions = allowQuestions;
    }

    public InteractiveLotLinkingSession Session { get; }
    public Func<LotLinkingEventDTO, Task> SendEventAsync { get; }
    public bool AllowMemorySave { get; }
    public bool AllowQuestions { get; }
}

public interface ILotLinkingAgentContextAccessor
{
    LotLinkingAgentContext? Current { get; set; }
    IDisposable Use(LotLinkingAgentContext context);
}

public sealed class LotLinkingAgentContextAccessor : ILotLinkingAgentContextAccessor
{
    private readonly AsyncLocal<LotLinkingAgentContext?> _current = new();

    public LotLinkingAgentContext? Current
    {
        get => _current.Value;
        set => _current.Value = value;
    }

    public IDisposable Use(LotLinkingAgentContext context)
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
