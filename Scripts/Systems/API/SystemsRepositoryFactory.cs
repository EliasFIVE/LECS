namespace EliasFive.LECS
{
    public static class SystemsRepositoryFactory
    {
        public static ISystemsRepository Create(ISystem[] systems)
        {
            return new SystemsRepository(systems);
        }
    }
}