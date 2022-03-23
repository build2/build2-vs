using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;
using B2VS.Toolchain;
using B2VS.Workspace;
using B2VS.ProjectModel;

namespace B2VS.Contexts
{
    /// <summary>
    /// File scanner provider factory, for scanning buildfiles.
    /// Currently this is used to generate build configuration contexts - not clear that buildfiles are the right place for this, but
    /// using for now just to get something working.
    /// </summary>
    [ExportFileScanner(
        ProviderType,
        "build2 buildfile",
        new String[] { Build2Constants.BuildfileFilename },
        new Type[] { typeof(IReadOnlyCollection<FileDataValue>) })]
    class BuildfileScannerFactory : IWorkspaceProviderFactory<IFileScanner>
    {
        // Unique Guid for BuildfileScanner.
        public const string ProviderType = "{474D1559-6CBA-4EB7-A380-97ACF82451EF}";

        public IFileScanner CreateProvider(IWorkspace workspaceContext)
        {
            return new BuildfileScanner(workspaceContext);
        }

        private class BuildfileScanner : IFileScanner
        {
            private IWorkspace workspaceContext;

            internal BuildfileScanner(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;

                // No idea if this is a good place for this, or if there's some more intended way to register a dependency so we get auto-refreshed
                // whenever some other file context is updated.
                var indexService = workspaceContext.GetIndexWorkspaceService();
                indexService.OnFileScannerCompleted += async (object sender, FileScannerEventArgs args) =>
                {
                    if (args.WorkspaceFilePath == Build2Constants.PackageListManifestFilename)
                    {
                        //OutputUtils.OutputWindowPaneAsync("BuildfileScanner: Purging scanned data due to change to scan completion of packages.manifest.");
                        /* but why await? */ indexService.PurgeFileScannerDataForProvider(new Guid(ProviderType));
                        //todo: nothing seems to work.
                        //maybe indexService.RefreshElementAsync would, but then we'd need to store all file paths processed, which seems wrong...
                    }
                };
            }

            public async Task<T> ScanContentAsync<T>(string filePath, CancellationToken cancellationToken)
            where T : class
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (typeof(T) != FileScannerTypeConstants.FileDataValuesType)
                {
                    throw new NotImplementedException();
                }

                var relativePath = workspaceContext.MakeRelative(filePath);
                OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner invoked for: {0}", relativePath));

                var indexService = workspaceContext.GetIndexWorkspaceService();

                //using (StreamReader rdr = new StreamReader(filePath))
                //{
                //}

                // @todo: Unsure on this. Seems to work (we are blocking on the indexing of our package manifest completing first, so we can access the
                // cached build config info), but not sure if this is good/safe, or if instead should just return whatever's available now (maybe nothing)
                // and register an event handler for when the manifest indexing is updated, to refresh ourselves at that point.
                Func<string, Task> createDataValuesRefreshTask = async (string entityPath) =>
                {
                    var state = await indexService.GetFileScannerState(entityPath, FileScannerType.FileData);
                    if (!state.HasValue)
                    {
                        await indexService.RefreshElementAsync(entityPath, IndexElement.FileDataValueScanning | IndexElement.InvalidateCache, cancellationToken);
                    }
                };

                var results = new List<FileDataValue>();

                // Determine containing package

                // See note above, unsure if right approach. Idea is to ensure this is indexed, since following call to GetContainingPackagePathAsync relies on it.
                await createDataValuesRefreshTask(Build2Constants.PackageListManifestFilename);

                var packagePath = await Build2Workspace.GetContainingPackagePathAsync(workspaceContext, filePath);
                if (packagePath != null)
                {
                    // Grab cached build configurations for our package
                    var packageManifestPath = Path.Combine(packagePath, Build2Constants.PackageManifestFilename);

                    // See note above, unsure if right approach. Done so that GetIndexedBuild... call below will be sure to have data available.
                    await createDataValuesRefreshTask(packageManifestPath);

                    var buildConfigs = await ProjectConfigUtils.GetIndexedBuildConfigurationsForPathAsync(packagePath, workspaceContext);
                    results.AddRange(buildConfigs.Select(cfg => new FileDataValue(
                        BuildConfigurationContext.ContextTypeGuid,
                        BuildConfigurationContext.DataValueName,
                        null,
                        context: cfg.BuildConfiguration
                        )));

                    OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} configs for '{1}'", buildConfigs.Count(), relativePath));
                }
                else
                {
                    // @todo: for now assuming this means not in a package (at project level), rather than 'indexed data not available'.
                    // Also, for the moment no attempt to build subtrees, just build the whole project. Should probably generate a list of
                    // package paths to pass to bdep, including every package located below us in the folder structure.

                    // Grab cached build configurations for project
                    var buildConfigs = await ProjectConfigUtils.GetIndexedBuildConfigurationsForPathAsync(workspaceContext.Location, workspaceContext);
                    results.AddRange(buildConfigs.Select(cfg => new FileDataValue(
                        BuildConfigurationContext.ContextTypeGuid,
                        BuildConfigurationContext.DataValueName,
                        null,
                        context: cfg.BuildConfiguration
                        )));

                    OutputUtils.OutputWindowPaneAsync(string.Format("Using project-level configs ({0}) for '{1}'", buildConfigs.Count(), relativePath));
                }

                OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner completed for: {0}", relativePath));

                return (T)(IReadOnlyCollection<FileDataValue>)results;
            }
        }
    }
}
