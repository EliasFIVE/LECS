using System;

namespace EliasFive.LECS
{
    public interface ITriggerProvider<out T> where T : struct
    {
        event Action<T> onFire;
    }
}
