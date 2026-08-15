namespace EliasFive.LECS
{
    public interface ICommandApplier
    {
        void Apply(ICommand command);
    }
}
