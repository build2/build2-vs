using Microsoft.VisualStudio.Workspace.Extensions.VS;
using Microsoft.VisualStudio.Workspace;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace B2VS.Contexts
{
    internal class BasicContextAction : IFileContextAction, IVsCommandItem
    {
        internal BasicContextAction(
            FileContext fileContext,
            Tuple<Guid, uint> command,
            string displayName,
            Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task<bool>> executeAction)
        {
            this.CommandGroup = command.Item1;
            this.CommandId = command.Item2;
            this.DisplayName = displayName;
            this.Source = fileContext;
            this.executeAction = executeAction;
        }

        // IVsCommandItem interface
        public Guid CommandGroup { get; }
        public uint CommandId { get; }
        // End IVsCommandItem interface

        // IFileContextAction interface
        public string DisplayName { get; }
        public FileContext Source { get; }

        public async Task<IFileContextActionResult> ExecuteAsync(IProgress<IFileContextActionProgressUpdate> progress, CancellationToken cancellationToken)
        {
            bool result = await this.executeAction(this.Source, progress, cancellationToken);
            return new FileContextActionResult(result);
        }
        // End IFileContextAction interface

        private Func<FileContext, IProgress<IFileContextActionProgressUpdate>, CancellationToken, Task<bool>> executeAction;
    }
}
