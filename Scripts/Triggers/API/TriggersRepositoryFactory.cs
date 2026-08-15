namespace EliasFive.LECS
{
    public static class TriggersRepositoryFactory
    {
        public static ITriggersRepository Create()
        {
            return new TriggersRepository();
        }
    }
}