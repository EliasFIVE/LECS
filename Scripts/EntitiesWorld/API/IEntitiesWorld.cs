namespace EliasFive.LECS
{
    public interface IEntitiesWorld : IEntitiesWorldDataProvider
    {
        void Refresh();
        int AddEntity<T>() where T : IEntityTag;
        int AddEntity();
        void RemoveEntity(int id);
        void RemoveComponentData<T>(int id) where T : struct;
        void RemoveComponentDataAsSingle<T>() where T : struct;
        T PopComponentDataAsSingle<T>() where T : struct;
        T PopComponentData<T>(int id) where T : struct;
        void SetComponentDataAsSingle<T>(T data) where T : struct;
        void AddNewComponentAsSingle<T>(T data) where T : struct;
        void SetComponentData<T>(int id, T data) where T : struct;
        void AddNewComponent<T>(int id, T data) where T : struct;
        void AddTag<T>(int id) where T : IEntityTag;
        void RemoveTag<T>(int id) where T : IEntityTag;

        bool IsDirtyComponentDataAsSingle<T>() where T : struct;
        bool IsDirtyComponentData<T>(int id) where T : struct;
    }
}
