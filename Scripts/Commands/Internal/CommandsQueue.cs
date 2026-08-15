using System;
using System.Collections.Generic;

namespace EliasFive.LECS
{
    class CommandsQueue : ICommandsQueue
    {
        public bool hasCommands => _container.Count != 0;

        readonly Queue<(ICommand, Type)> _container = new();

        public void PushCommand<T>(T command) where T : ICommand
        {
            _container.Enqueue((command, typeof(T)));
        }

        public (ICommand, Type) PopCommand()
        {
            return _container.Dequeue();
        }
    }
}
