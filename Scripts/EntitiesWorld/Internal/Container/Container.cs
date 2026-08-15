using System;
using System.Collections.Generic;

namespace EliasFive.LECS
{
    class TypedContainer<T> : ITypedContainer<T>
    {
        readonly Dictionary<Type, T> _entriesMap;

        public TypedContainer(int capacity = 0)
        {
            _entriesMap = new Dictionary<Type,T>(capacity);
        }

        public TypedContainer(Dictionary<Type, T> entriesMap)
        {
            _entriesMap = entriesMap;
        }

        public IReadOnlyDictionary<Type, T> GetEntriesMap()
        {
            return _entriesMap;
        }

        public bool HasInstance<T1>() where T1 : T
        {
            return _entriesMap.ContainsKey(typeof(T1));
        }

        public T1 Resolve<T1>() where T1 : T
        {
            return ResolveInternal<T1>();
        }

        public void RemoveInstance<T1>() where T1 : T
        {
            RemoveInstanceInternal<T1>();
        }

        void RemoveInstanceInternal<T1>() where T1 : T
        {
            Type key = typeof(T1);

            _entriesMap.Remove(key);
        }

        T1 ResolveInternal<T1>() where T1 : T
        {
            Type key = typeof(T1);

            if (_entriesMap.TryGetValue(key, out T instance))
            {
                return (T1)instance;
            }

            throw new InvalidOperationException(
                $"Could not find entry of type {key.FullName}.");
        }

        public void RegisterInstance<T1>(T1 instance) where T1 : T
        {
            RegisterInstanceInternal(instance);
        }

        void RegisterInstanceInternal<T1>(T1 instance) where T1 : T
        {
            Type key = typeof(T1);

            if (!_entriesMap.TryAdd(key, instance))
            {
                throw new InvalidOperationException(
                    $"Entry of type {key.FullName} is already registered.");
            }
        }

    }
}
