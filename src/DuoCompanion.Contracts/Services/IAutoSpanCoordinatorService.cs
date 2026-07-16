using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public sealed class SpanCandidateEventArgs : EventArgs
{
    public SpanTarget Target { get; }
    public SpanCandidateEventArgs(SpanTarget target) => Target = target;
}

public interface IAutoSpanCoordinatorService
{
    event EventHandler<SpanCandidateEventArgs>? SpanCandidateEntered;
    event EventHandler? SpanCandidateExited;
    void Start(IntPtr hostHwnd);
    void Stop();
}
