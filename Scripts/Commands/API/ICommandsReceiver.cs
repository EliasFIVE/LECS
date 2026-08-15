namespace EliasFive.LECS
{
    public interface ICommandsReceiver
    {
        void PushCommand<T>(T command)
            where T : ICommand;
    }
}
