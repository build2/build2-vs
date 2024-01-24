using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Extensions.VS;
using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;
using B2VS.Toolchain;
using B2VS.Language.Manifest;

namespace B2VS.Contexts
{
    /// <summary>
    /// Action provider for build2 package manifests.
    /// </summary>
    [ExportFileContextActionProvider(
        (FileContextActionProviderOptions)VsCommandActionProviderOptions.SupportVsCommands,
        ProviderType, 
        ProviderPriority.Normal,
        BuildContextTypes.BuildContextType,
        BuildContextTypes.RebuildContextType,
        BuildContextTypes.CleanContextType)]
    internal class PackageManifestActionProviderFactory : IWorkspaceProviderFactory<IFileContextActionProvider>, IVsCommandActionProvider
    {
        // Unique Guid for this provider.
        private const string ProviderType = "{9DC27E72-0F45-4F49-B6A3-AF70641845BA}";

        private static readonly Guid ProviderCommandGroup = PackageIds.Build2GeneralCmdSet;
        private static readonly IReadOnlyList<CommandID> SupportedCommands = new List<CommandID>
            {
            };

        public IFileContextActionProvider CreateProvider(IWorkspace workspaceContext)
        {
            return new PackageManifestActionProvider(workspaceContext);
        }

        public IReadOnlyCollection<CommandID> GetSupportedVsCommands()
        {
            return SupportedCommands;
        }

        internal class PackageManifestActionProvider : IFileContextActionProvider
        {
            private IWorkspace workspaceContext;

            internal PackageManifestActionProvider(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            public async Task<IReadOnlyList<IFileContextAction>> GetActionsAsync(string filePath, FileContext fileContext, CancellationToken cancellationToken)
            {
                var actions = new List<IFileContextAction>();

                if (Path.GetFileName(filePath) == Build2Constants.PackageManifestFilename)
                {
                    Build2Toolchain.DebugHandler?.Invoke(string.Format("Generating actions requested for {0}...", filePath));

                    if (fileContext.Context is ContextualBuildConfiguration)
                    {
                        var buildAction = BuildActions.CreateActionFromFileContext(workspaceContext, fileContext);
                        if (buildAction != null)
                        {
                            actions.Add(buildAction);
                        }
                    }
                }

                return await Task.FromResult<IReadOnlyList<IFileContextAction>>(actions);
            }
        }
    }
}
