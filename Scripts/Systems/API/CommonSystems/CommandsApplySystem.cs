using System;
using System.Collections.Generic;

namespace EliasFive.LECS
{
    public class CommandsApplySystem : ISystem
    {
        readonly ICommandsProvider _commandsProvider;
        readonly IReadOnlyDictionary<Type, ICommandApplier> _commandAppliers;

        public CommandsApplySystem(ICommandsProvider commandsProvider,
            ICommandAppliersFactory commandAppliersFactory)
        {
            _commandsProvider = commandsProvider;
            _commandAppliers = commandAppliersFactory.Get();
        }

        public void Tick()
        {
            while (_commandsProvider.hasCommands)
            {
                (ICommand command, Type commandType) = _commandsProvider.PopCommand();

                if (!_commandAppliers.TryGetValue(commandType, out ICommandApplier applier))
                {
                    throw new InvalidOperationException(
                        $"No ICommandApplier registered for command type '{commandType.FullName}'.");
                }

                applier.Apply(command);
            }
        }

    }
}
