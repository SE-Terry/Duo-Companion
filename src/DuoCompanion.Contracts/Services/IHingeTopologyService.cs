using DuoCompanion.Core.Models;

namespace DuoCompanion.Contracts.Services;

public interface IHingeTopologyService
{
    DuoDisplayTopology? CurrentTopology { get; }
    HingeZone? CurrentHinge => CurrentTopology?.Hinge;
    event EventHandler? TopologyChanged;
    void Start();
    void Stop();
}
