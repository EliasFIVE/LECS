using System;

namespace EliasFive.LECS
{
    public class EntitiesWorldSnapshot
    {
        public class EntitySnapshot
        {
            public int id;
            public IComponent[] components;
            public Type[] componentTypes;
        }

        public class TagSnapshot
        {
            public Type type;
            public int[] ids;
        }

        public EntitySnapshot[] entitySnapshots;
        public TagSnapshot[] tagSnapshots;
        public int singletonEntityId;
        public int lastUsedId;
        public int[] releasedIds;
    }
}