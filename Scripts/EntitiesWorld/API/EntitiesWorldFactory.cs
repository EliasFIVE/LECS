namespace EliasFive.LECS
{
    public static class EntitiesWorldFactory
    {
        public static IEntitiesWorld Create()
        {
            return new EntitiesWorld();
        }

        public static IEntitiesWorld Create(EntitiesWorldSnapshot entitiesWorldSnapshot)
        {
            return new EntitiesWorld(entitiesWorldSnapshot);
        }
    }
}