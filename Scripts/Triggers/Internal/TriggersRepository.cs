namespace EliasFive.LECS
{
    class TriggersRepository: ITriggersRepository
    {
        readonly ITypedContainer<ITrigger> _triggers = new TypedContainer<ITrigger>();

        public ITriggerProvider<TC> GetTrigger<T,TC>()
            where T : BaseTrigger<TC>, new()
            where TC : struct
        {
            if (!_triggers.HasInstance<T>())
            {
                _triggers.RegisterInstance(new T());
            }

            return _triggers.Resolve<T>();
        }

        public void FireTrigger<T, TC>(TC context)
            where T : BaseTrigger<TC>, new()
            where TC : struct
        {
            if (!_triggers.HasInstance<T>())
            {
                _triggers.RegisterInstance(new T());
            }

            _triggers.Resolve<T>().Fire(context);
        }
    }
}
