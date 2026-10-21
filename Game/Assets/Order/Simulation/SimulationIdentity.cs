using Unity.Entities;

namespace Order.Simulation
{
    // Simulation-local mapping. External commands never store an Entity handle.
    public struct SimulationIdentity : IComponentData
    {
        public ulong UnitId;
        public int OwnerId;
    }
}
