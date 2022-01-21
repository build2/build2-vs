using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using Task = System.Threading.Tasks.Task;
using B2VS.VSPackage;

namespace B2VS.Contexts
{
    /// <summary>
    /// File context provider for build2 buildfiles.
    /// </summary>
    [ExportFileContextProvider(ProviderType, PackageIds.BuildfileContextType)]
    internal class BuildfileContextProviderFactory : IWorkspaceProviderFactory<IFileContextProvider>
    {
        // Unique Guid for BuildfileContextProvider.
        private const string ProviderType = "2CA5FBE7-6E60-4EFF-9AD9-9EA10A85BCB0";

        /// <inheritdoc/>
        public IFileContextProvider CreateProvider(IWorkspace workspaceContext)
        {
            return new BuildfileContextProvider(workspaceContext);
        }

        private class BuildfileContextProvider : IFileContextProvider
        {
            private IWorkspace workspaceContext;

            internal BuildfileContextProvider(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            /// <inheritdoc />
            public async Task<IReadOnlyCollection<FileContext>> GetContextsForFileAsync(string filePath, CancellationToken cancellationToken)
            {
                var fileContexts = new List<FileContext>();

                var filename = System.IO.Path.GetFileName(filePath);
                if (filename.Equals("buildfile"))
                {
                    fileContexts.Add(new FileContext(
                        new Guid(ProviderType),
                        new Guid(PackageIds.BuildfileContextType),
                        filePath,
                        Array.Empty<string>()));
                }

                return await Task.FromResult(fileContexts.ToArray());
            }
        }
    }
}
