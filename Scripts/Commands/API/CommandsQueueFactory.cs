namespace EliasFive.LECS
{
    public static class CommandsQueueFactory
    {
        public static ICommandsQueue Create()
        {
            return new CommandsQueue();
        }
    }
}