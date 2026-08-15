namespace EliasFive.LECS
{
    interface ITriggerInvoker<in T> where T : struct
    {
        void Fire(T context);
    }
}
