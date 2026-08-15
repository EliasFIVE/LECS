namespace EliasFive.LECS
{
    class Component<T>:
            IComponent<T>
        where T : struct
    {

        public bool isDirty => _isDirty;
        public T data => _internalData;

        // Public for JSON save/load (serializer only writes public instance fields).
        public T _internalData;
        bool _isDirty;

        public Component(T internalData)
        {
            _internalData = internalData;
            _isDirty = true;
        }

        public IComponent Set(T value)
        {
            _internalData = value;
            _isDirty = true;

            return this;
        }

        public void ResetDirty()
        {
            _isDirty = false;
        }
    }
}
