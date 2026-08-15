using System;
using System.Collections.Generic;
using System.Linq;

namespace EliasFive.LECS
{
    class Entity
    {
        public bool hasDirtyComponents => _dirtyComponents.Count > 0;

        readonly TypedContainer<IComponent> _components = new();
        readonly HashSet<IComponent> _dirtyComponents = new();

        public Entity()
        {
            _components = new TypedContainer<IComponent>();
            _dirtyComponents = new HashSet<IComponent>();
        }

        public Entity(Dictionary<Type, IComponent> entriesMap) 
        {
            _components = new TypedContainer<IComponent>(entriesMap);
            _dirtyComponents = new HashSet<IComponent>();
        }

        public IComponent[] GetComponents()
        {
            return _components.GetEntriesMap().Values.ToArray();
        }
        public Type[] GetComponentTypes()
        {
            return _components.GetEntriesMap().Keys.ToArray();
        }

        public void ResetDirty()
        {
            foreach (IComponent dataController in _dirtyComponents)
            {
                dataController.ResetDirty();
            }

            _dirtyComponents.Clear();
        }

        public void RemoveData<TC>()
            where TC : struct
        {
            _components.RemoveInstance<IComponent<TC>>();
        }

        public TC PopData<TC>()
            where TC : struct
        {
            TC data = _components.Resolve<IComponent<TC>>().data;

            RemoveData<TC>();

            return data;
        }

        public void AddNewComponent<TC>(in TC instance) where TC : struct
        {
            var dataComponent = new Component<TC>(instance);
            _components.RegisterInstance((IComponent<TC>)dataComponent);
            _dirtyComponents.Add(dataComponent);
        }

        public T1 GetComponentData<T1>()
            where T1 : struct
        {
            return _components.Resolve<IComponent<T1>>().data;
        }

        public bool HasComponent<T1>()
                    where T1 : struct
        {
            return _components.HasInstance<IComponent<T1>>();
        }

        public void SetComponentData<T1>(in T1 value)
                    where T1 : struct
        {
            _dirtyComponents.Add(
                _components
                    .Resolve<IComponent<T1>>()
                    .Set(value)
            );
        }

        public bool IsDirtyData<T1>() where T1 : struct
        {
            return _components
                     .Resolve<IComponent<T1>>()
                     .isDirty;
        }
    }
}
