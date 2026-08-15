namespace EliasFive.LECS
{
    class SystemsRepository : ISystemsRepository
    {
        readonly ISystem[] _systems;

        public SystemsRepository(ISystem[] systems)
        {
            _systems = systems;
        }

        public void Tick()
        {
            foreach (ISystem system in _systems)
            {
                system.Tick();
            }
        }
    }
}
