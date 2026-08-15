namespace EliasFive.LECS
{
    public abstract class BaseCommandApplier<T> : ICommandApplier where T : ICommand
    {
        public void Apply(ICommand command)
        {
            Apply((T)command);
        }

        protected abstract void Apply(T command);
    }
}
