using System;
using System.Collections.Generic;
using System.Linq;

namespace EliasFive.LECS
{
    class EntitiesWorld : IEntitiesWorld
    {
        readonly Dictionary<int, Entity> _entities;
        readonly HashSet<int> _dirtyEntityIds;
        readonly HashSet<int> _toRemoveEntityIds;
        readonly int _singletonEntityId;
        readonly Dictionary<Type, HashSet<int>> _tags;
        
        int _lastGeneratedId;
        readonly Queue<int> _releasedIds;
        
        public EntitiesWorld()
        {
            _lastGeneratedId = 0;
            _releasedIds = new Queue<int>();
            _entities = new Dictionary<int, Entity>();
            _dirtyEntityIds = new HashSet<int>();
            _toRemoveEntityIds = new HashSet<int>();
            _tags = new Dictionary<Type, HashSet<int>>();

            _singletonEntityId = AddEntity();
        }

        public EntitiesWorld(EntitiesWorldSnapshot entitiesWorldSnapshot)
        {
            _lastGeneratedId = entitiesWorldSnapshot.lastUsedId;
            _singletonEntityId = entitiesWorldSnapshot.singletonEntityId;        
            _dirtyEntityIds = new HashSet<int>();
            _toRemoveEntityIds = new HashSet<int>();
            _releasedIds = new Queue<int>(entitiesWorldSnapshot.releasedIds);

            _entities = new Dictionary<int, Entity>(entitiesWorldSnapshot.entitySnapshots.Length);

            foreach (var entitySnapshot in entitiesWorldSnapshot.entitySnapshots)
            {
                var entriesMap = new Dictionary<Type, IComponent>(entitySnapshot.componentTypes.Length);
                for (int i = 0; i < entitySnapshot.componentTypes.Length; i++)
                {
                    entriesMap.Add(entitySnapshot.componentTypes[i], entitySnapshot.components[i]);
                }

                _entities[entitySnapshot.id] = new Entity(entriesMap);
            }

            _tags = new Dictionary<Type, HashSet<int>>(entitiesWorldSnapshot.tagSnapshots.Length);

            foreach (var tagSnapshot in entitiesWorldSnapshot.tagSnapshots)
            {
                _tags[tagSnapshot.type] = tagSnapshot.ids.ToHashSet();
            }
        }
        
        public void Refresh()
        {
            foreach (int id in _toRemoveEntityIds)
            {
                _entities.Remove(id);
                _releasedIds.Enqueue(id);
            }
            
            _toRemoveEntityIds.Clear();
            RefreshTagsLinks();
            
            ResetDirties();
        }

        public EntitiesWorldSnapshot GetSnapshot()
        {
            var tagSnapshots = new List<EntitiesWorldSnapshot.TagSnapshot>();
            var entitySnapshots = new List<EntitiesWorldSnapshot.EntitySnapshot>();

            foreach (KeyValuePair<Type, HashSet<int>> tag in _tags)
            {
                tagSnapshots.Add(new EntitiesWorldSnapshot.TagSnapshot()
                {
                    type = tag.Key,
                    ids = tag.Value.ToArray()
                });
            }

            foreach (KeyValuePair<int, Entity> entity in _entities)
            {
                entitySnapshots.Add(new EntitiesWorldSnapshot.EntitySnapshot()
                {
                    id = entity.Key,
                    components = entity.Value.GetComponents(),
                    componentTypes = entity.Value.GetComponentTypes()
                });
            }

            var snapshot = new EntitiesWorldSnapshot
            {
                singletonEntityId = _singletonEntityId,
                lastUsedId = _lastGeneratedId,
                releasedIds = _releasedIds.ToArray(),
                tagSnapshots = tagSnapshots.ToArray(),
                entitySnapshots = entitySnapshots.ToArray()
            };

            return snapshot;
        }

        public void AddTag<T>(int id) where T : IEntityTag
        {
            Type tag = typeof(T);

            if (!_tags.ContainsKey(tag))
            {
                _tags.Add(tag, new HashSet<int>());
            }

            _tags[tag].Add(id);
        }

        public void RemoveTag<T>(int id) where T : IEntityTag
        {
            Type tag = typeof(T);

            if (!_tags.TryGetValue(tag, out HashSet<int> set))
            {
                return;
            }

            set.Remove(id);

            if (set.Count == 0)
            {
                _tags.Remove(tag);
            }
        }


        public int AddEntity<T>() where T : IEntityTag
        {
            int id = AddEntity();
            AddTag<T>(id);
            return id;
        }

        public int AddEntity()
        {
            int newEntityId = GetNewEntityId();
            _entities.Add(newEntityId, new Entity());
            _dirtyEntityIds.Add(newEntityId);
            return newEntityId;
        }
        
        public void RemoveEntity(int id)
        {
            _toRemoveEntityIds.Add(id);
        }

        public void RemoveComponentDataAsSingle<T>() where T : struct
        {
            RemoveComponentData<T>(_singletonEntityId);
        }

        public void RemoveComponentData<T>(int id) where T : struct
        {
            _entities[id].RemoveData<T>();
            _dirtyEntityIds.Add(id);
        }

        public T PopComponentDataAsSingle<T>() where T : struct
        {
            return PopComponentData<T>(_singletonEntityId);
        }

        public T PopComponentData<T>(int id) where T : struct
        {   
            _dirtyEntityIds.Add(id);
            return _entities[id].PopData<T>();
        }

        public T GetComponentAsSingle<T>() where T : struct
        {
            return GetComponent<T>(_singletonEntityId);
        }

        public bool IsDirtyComponentDataAsSingle<T>() where T : struct
        {
            return IsDirtyComponentData<T>(_singletonEntityId);
        }

        public bool IsDirtyComponentData<T>(int id) where T : struct
        {
            return _entities[id].IsDirtyData<T>();
        }

        public void SetComponentDataAsSingle<T>(T data) where T : struct
        {
            SetComponentData(_singletonEntityId, data);
        }

        public void AddNewComponentAsSingle<T>(T data) where T : struct
        {
            AddNewComponent(_singletonEntityId, data);
        }

        public bool HasComponentAsSingle<T>()
            where T : struct
        {
            return HasComponent<T>(_singletonEntityId);
        }

        public bool HasComponent<T>(int id)
            where T : struct
        {
            return _entities[id].HasComponent<T>();
        }

        public T GetComponent<T>(int id) where T : struct
        {
            return _entities[id].GetComponentData<T>();
        }

        public void SetComponentData<T>(int id, T data) where T : struct
        {
            _entities[id].SetComponentData(data);
            _dirtyEntityIds.Add(id);
        }

        public void AddNewComponent<T>(int id, T data) where T : struct
        {
            _entities[id].AddNewComponent(data);
            _dirtyEntityIds.Add(id);
        }
        
        public IReadOnlyCollection<int> GetEntitiesByTag<T>() where T : IEntityTag
        {
            Type tag = typeof(T);
            
            if (!_tags.TryGetValue(tag, out HashSet<int> set))
            {
                return Array.Empty<int>();
            }

            foreach (int toRemoveEntityId in _toRemoveEntityIds)
            {
                if (set.Contains(toRemoveEntityId))
                {
                    set.Remove(toRemoveEntityId);
                }
            }
   
            return set;
        }

        public IReadOnlyCollection<int> GetEntityIds()
        {
            if (_toRemoveEntityIds.Count == 0)
            {
                return _entities.Keys.ToArray();
            }

            var result = new List<int>(_entities.Count);

            foreach (int id in _entities.Keys)
            {
                if (!_toRemoveEntityIds.Contains(id))
                {
                    result.Add(id);
                }
            }

            return result;
        }

        public bool HasTag<T>(int id) where T : IEntityTag
        {
            if (_toRemoveEntityIds.Contains(id))
            {
                return false;
            }
            
            Type tag = typeof(T);
            
            return _entities.ContainsKey(id) && _tags.ContainsKey(tag) && _tags[tag].Contains(id);
        }
        
        int GetNewEntityId()
        {
            if (_releasedIds.Count > 0)
            {
                return _releasedIds.Dequeue();
            }
            
            _lastGeneratedId++;
            return _lastGeneratedId;
        }
        
        void ResetDirties()
        {
            foreach (int id in _dirtyEntityIds)
            {
                _entities[id].ResetDirty();
            }

            _dirtyEntityIds.Clear();
        }

        void RefreshTagsLinks()
        {
            foreach (var tagPair in _tags)
            {
                RefreshTagLinks(tagPair.Key);
            }
        }

        void RefreshTagLinks(Type tag)
        {
            var ids = _tags[tag].ToList();

            bool changed = false;
            for (int i = ids.Count - 1; i >= 0; i--)
            {
                if (!_entities.ContainsKey(ids[i]))
                {
                    ids.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
            {
                if (ids.Count == 0)
                {
                    _tags.Remove(tag);
                }
                else
                {
                    _tags[tag] = ids.ToHashSet();
                }
            }
        }
    }
}
