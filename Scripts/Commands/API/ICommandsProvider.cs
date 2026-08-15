using System;

namespace EliasFive.LECS
{
    public interface ICommandsProvider
    {
        bool hasCommands { get; }
        (ICommand, Type) PopCommand();
    }
}
