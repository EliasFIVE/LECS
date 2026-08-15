namespace EliasFive.LECS
{
    interface ITypedContainer<T>
    {
        bool HasInstance<T1>()
            where T1 : T;

        void RegisterInstance<T1>(T1 instance)
            where T1 : T;

        T1 Resolve<T1>()
            where T1 : T;

        void RemoveInstance<T1>() where T1 : T;
    }
}
