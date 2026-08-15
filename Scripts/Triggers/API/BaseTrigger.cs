using System;

namespace EliasFive.LECS
{
    public abstract class BaseTrigger<T> : ITrigger, ITriggerInvoker<T>,
        ITriggerProvider<T> where T : struct
    {
        public event Action<T> onFire;

        public void Fire(T context)
        {
            onFire?.Invoke(context);
        }
    }
}
