using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Task = System.Threading.Tasks.Task;
using BuildContextTypes = Microsoft.VisualStudio.Workspace.Build.BuildContextTypes;
using B2VS.VSPackage;
using B2VS.Workspace;
using B2VS.ProjectModel;
using B2VS.Toolchain;

namespace B2VS.Contexts
{
    /// <summary>
    /// File context provider for build2 package manifests.
    /// </summary>
    [ExportFileContextProvider(
        ProviderType,
        BuildContextTypes.BuildContextType,
        BuildContextTypes.RebuildContextType,
        BuildContextTypes.CleanContextType)]
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
                    var basePath = Path.GetDirectoryName(filePath);
                    if (basePath != null)
                    {
                        var buildConfigs = await ProjectConfigUtils.GetIndexedBuildConfigurationsForPathAsync(basePath, workspaceContext, cancellationToken);

                        // @todo: Unclear if should be creating a full build config here; could instead just pass through minimal info and then use that to 
                        // retrieve the full config info from somewhere centralized when invoking an action on this context.
                        // @todo: Also no idea why project root buildfile fails to yield a 'Build' menu option in the case that the project is opened
                        // and indexed for the first time (subsequent openings of the project folder work as expected, as do other buildfiles even on
                        // first opening). Scanner invocation ordering and generated configs all look correct.
                        fileContexts.AddRange(buildConfigs.Select(cfg => new FileContext(
                            ProviderTypeGuid,
                            BuildContextTypes.BuildContextTypeGuid,
                            new ContextualBuildConfiguration(cfg, basePath),
                            new[] { filePath })));

                        fileContexts.AddRange(buildConfigs.Select(cfg => new FileContext(
                            ProviderTypeGuid,
                            BuildContextTypes.RebuildContextTypeGuid,
                            new ContextualBuildConfiguration(cfg, basePath),
                            new[] { filePath })));

                        fileContexts.AddRange(buildConfigs.Select(cfg => new FileContext(
                            ProviderTypeGuid,
                            BuildContextTypes.CleanContextTypeGuid,
                            new ContextualBuildConfiguration(cfg, basePath),
                            new[] { filePath })));

                        //fileContexts.Add(new FileContext(
                        //    ProviderTypeGuid,
                        //    BuildContextTypes.BuildAllContextTypeGuid,
                        //    new Build2BuildConfiguration(),
                        //    Array.Empty<string>()));
                    }
                }

                return await Task.FromResult(fileContexts.ToArray());
            }
        }
    }
}
