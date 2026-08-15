namespace EliasFive.LECS
{
    public class RefreshWorldSystem : ISystem
    {
        readonly IEntitiesWorld _entitiesWorld;

        public RefreshWorldSystem(IEntitiesWorld entitiesWorld)
        {
            _entitiesWorld = entitiesWorld;
        }

        public void Tick()
        {
            _entitiesWorld.Refresh();
        }
    }
}