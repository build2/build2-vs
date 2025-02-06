using B2VS.VSPackage;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;
using System.Collections.Generic;
using System.ComponentModel.Design;

namespace B2VS.Workspace
{
    internal class PackageBuildCommandHandler : CommandHandlerBase
    {
        protected override CommandID Id { get; } = new CommandID(PackageIds.BuildCommandGroupGuid, 0x1000);

        protected override bool QueryStatus(List<WorkspaceVisualNodeBase> selection)
            => selection.Count == 1 && selection[0] is Workspace.Nodes.Build2PackageNode;

        protected override bool Execute(List<WorkspaceVisualNodeBase> selection)
        {
            // @TODO: Questionable as to whether it makes sense to use the build service at all, since it's just routing back to build actions
            // implemented within this extension anyway, so perhaps should just invoke directly?
            var workspace = selection[0].Workspace;
            var buildSvc = workspace.GetBuildService();
            var configSvc = ThreadHelper.JoinableTaskFactory.Run(async delegate
            {
                return await workspace.GetProjectConfigurationServiceAsync();
            });
            if (buildSvc != null && configSvc != null)
            {
                var pkgNode = selection[0] as Workspace.Nodes.Build2PackageNode;
                var projectTargetFilePath = pkgNode.FullPath;
                // Use the active config associated with the launch target, for consistency with right-click build on the package's buildfile item.
                var cfgCtx = new BuildConfigurationContext(configSvc.GetActiveProjectBuildConfiguration(new ProjectTargetFileContext(projectTargetFilePath)));
                if (cfgCtx.BuildConfiguration != null)
                {
                    // @NOTE: Seems dodgy. This Execute call comes in on main thread and if we use JoinableTaskFactory.Run() then we block the UI during the build.
                    // So using RunAsync, but also feels wrong since we can't await it, and this also means we just have to immediately return success without waiting
                    // for the build result (maybe this is fine though, not sure of exact semantics of base class Execute return value anyway).
                    var result = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                    {
                        // Currently assuming the node path is the path to the package manifest file
                        return await buildSvc.BuildAsync(
                            projectTargetFilePath,
                            null, null, null,
                            cfgCtx,
                            BuildType.Build,
                            true, null, cancellationToken: default);
                    });
                    return true; // result.IsSuccess;
                }
            }

            return false;
        }
    }
}
