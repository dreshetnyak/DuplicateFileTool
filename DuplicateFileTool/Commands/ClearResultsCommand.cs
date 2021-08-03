using System;

namespace DuplicateFileTool.Commands
{
    internal class ClearResultsCommand : CommandBase
    {
        public Action ClearResults { get; }

        public ClearResultsCommand(Action clearResults)
        {
            Enabled = false;
            ClearResults = clearResults;
        }

        public override void Execute(object parameter)
        {
            ClearResults();
        }
    }
}
