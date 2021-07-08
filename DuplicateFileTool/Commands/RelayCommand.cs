using System;
using DuplicateFileTool.Annotations;

namespace DuplicateFileTool.Commands
{
    internal class RelayCommand : CommandBase
    {
        public Action<object> Command { get; }

        public override void Execute(object parameter)
        {
            try
            {
                Enabled = false;
                Command(parameter);
            }
            finally
            {
                Enabled = true;
            }
        }

        public RelayCommand([NotNull] Action<object> command, bool enabled = true) : base(enabled)
        {
            Command = command;
        }
    }
}
