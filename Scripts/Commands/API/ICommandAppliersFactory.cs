using System;
using System.Collections.Generic;

namespace EliasFive.LECS
{
    public interface ICommandAppliersFactory
    {
        IReadOnlyDictionary<Type, ICommandApplier> Get();
    }
}
