using B2VS.Toolchain;
using B2VS.VSPackage;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.IO;

namespace B2VS
{
    internal class PackageBuildCommandHandler : CommandHandlerBase
    {
        protected override CommandID Id { get; } = new CommandID(PackageIds.BuildCommandGroupGuid, 0x1000);

        protected override bool QueryStatus(List<WorkspaceVisualNodeBase> selection)
            => selection.Count == 1 && selection[0] is Workspace.Build2WorkspaceNodeExtender.Build2PackageNode;

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
                var pkgNode = selection[0] as Workspace.Build2WorkspaceNodeExtender.Build2PackageNode;
                var buildfilePath = Path.Combine(Path.GetDirectoryName(pkgNode.FullPath), Build2Constants.BuildfileFilename);
                // Use the active config associated with the launch target, for consistency with right-click build on the package's buildfile item.
                var cfgCtx = new BuildConfigurationContext(configSvc.GetActiveProjectBuildConfiguration(new ProjectTargetFileContext(buildfilePath)));
                if (cfgCtx.BuildConfiguration != null)
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        // Currently assuming the node path is the path to the package manifest file
                        var result = await buildSvc.BuildAsync(
                            buildfilePath,
                            null, null, null,
                            cfgCtx,
                            BuildType.Build,
                            true, null, cancellationToken: default);
                    });
                }
            }

            return false;
        }
    }
}
