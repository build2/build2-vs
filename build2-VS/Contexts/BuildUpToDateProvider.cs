using B2VS.Toolchain;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace B2VS.Contexts
{
    [ExportBuildUpToDateCheck(BuildUpToDateCheckProviderOptions.None, ProviderType, new string[] { })]
    internal class BuildUpToDateProviderFactory : IWorkspaceProviderFactory<IBuildUpToDateCheckProvider>
    {
        // Unique Guid for this provider.
        private const string ProviderType = "{E95B4EB8-5483-4923-AD7D-42A4EA3FA0EF}";

        static BuildUpToDateProviderFactory()
        {
            Build2Toolchain.DebugHandler?.Invoke("Up Static Ctr");
        }

        public BuildUpToDateProviderFactory()
        {
            Build2Toolchain.DebugHandler?.Invoke("Up Instance Ctr");
        }

        public IBuildUpToDateCheckProvider CreateProvider(IWorkspace workspaceContext)
        {
            return new BuildUpToDateProvider(workspaceContext);
        }

        internal class BuildUpToDateProvider : IBuildUpToDateCheckProvider
        {
            IWorkspace workspace;

            public BuildUpToDateProvider(IWorkspace workspaceContext)
            {
                workspace = workspaceContext;
            }

            public Task<bool> IsUpToDateAsync(string projectFile, string projectFileTarget, IBuildConfigurationContext buildConfigurationContext, string buildConfiguration, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
    }
}
