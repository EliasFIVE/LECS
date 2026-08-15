namespace EliasFive.LECS
{
    public interface ITriggersInvoker
    {
        public void FireTrigger<T, TC>(TC context)
            where T : BaseTrigger<TC>, new()
            where TC : struct;
    }
}
