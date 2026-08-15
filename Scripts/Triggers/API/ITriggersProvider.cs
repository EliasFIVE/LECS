namespace EliasFive.LECS
{
    public interface ITriggersProvider
    {
        ITriggerProvider<TC> GetTrigger<T, TC>()
            where T : BaseTrigger<TC>, new()
            where TC : struct;
    }
}
