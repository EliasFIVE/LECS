namespace EliasFive.LECS
{
    public interface IComponent
    {
        void ResetDirty();
        bool isDirty { get; }
    }

    public interface IComponent<T> : IComponent where T : struct {
        IComponent Set(T value);
        T data { get; }
    }
}
