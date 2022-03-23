using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Task = System.Threading.Tasks.Task;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;
using B2VS.Workspace;

namespace B2VS.Contexts
{
    /// <summary>
    /// File context provider for build2 package manifests.
    /// </summary>
    [ExportFileContextProvider(
        ProviderType,
        BuildContextTypes.BuildContextType)]
    internal class PackageManifestContextProviderFactory : IWorkspaceProviderFactory<IFileContextProvider>
    {
        // Unique Guid for PackageManifestContextProvider.
        private const string ProviderType = "{D49FCC8D-C1CA-42D5-BE8E-173D0BDA9481}";
        private static readonly Guid ProviderTypeGuid = new Guid(ProviderType);

        /// <inheritdoc/>
        public IFileContextProvider CreateProvider(IWorkspace workspaceContext)
        {
            return new PackageManifestContextProvider(workspaceContext);
        }

        private class PackageManifestContextProvider : IFileContextProvider
        {
            private IWorkspace workspaceContext;

            internal PackageManifestContextProvider(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            /// <inheritdoc />
            public async Task<IReadOnlyCollection<FileContext>> GetContextsForFileAsync(string filePath, CancellationToken cancellationToken)
            {
                var fileContexts = new List<FileContext>();

                var filename = System.IO.Path.GetFileName(filePath);
                if (filename.Equals(Build2Constants.PackageManifestFilename))
                {
                    //var testConfigs = new String[] { "cfgA", "cfgB" };

                    //fileContexts.AddRange(testConfigs.Select(cfg => new FileContext(
                    //    ProviderTypeGuid,
                    //    BuildContextTypes.BuildContextTypeGuid,
                    //    new BuildConfigurationContext(cfg),
                    //    new[] { filePath })));
                }

                return await Task.FromResult(fileContexts.ToArray());
            }
        }
    }
}
