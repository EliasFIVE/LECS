using System.Collections.Generic;

namespace EliasFive.LECS
{
    public interface IEntitiesWorldDataProvider
    {
        EntitiesWorldSnapshot GetSnapshot();
        T GetComponentAsSingle<T>() where T : struct;
        T GetComponent<T>(int id) where T : struct;
        bool HasComponentAsSingle<T>() where T : struct;
        bool HasComponent<T>(int id)
            where T : struct;
        IReadOnlyCollection<int> GetEntitiesByTag<T>() where T : IEntityTag;
        IReadOnlyCollection<int> GetEntityIds();
        bool HasTag<T>(int id) where T : IEntityTag;
    }
}
