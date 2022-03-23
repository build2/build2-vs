using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;
using B2VS.VSPackage;
using B2VS.Toolchain;

namespace B2VS.Contexts
{
    /// <summary>
    /// File scanner provider factory, for scanning packages.manifest files.
    /// </summary>
    [ExportFileScanner(
        ProviderType,
        "build2 package list manifest",
        new String[] { Build2Constants.PackageListManifestFilename },
        new Type[] { typeof(IReadOnlyCollection<FileDataValue>) })]
    class PackageListManifestScannerFactory : IWorkspaceProviderFactory<IFileScanner>
    {
        // Unique Guid for PackageListManifestScanner.
        public const string ProviderType = "{56CFE682-18CA-4CB7-8348-65A76D60EC88}";

        public IFileScanner CreateProvider(IWorkspace workspaceContext)
        {
            return new PackageListManifestScanner(workspaceContext);
        }

        private class PackageListManifestScanner : IFileScanner
        {
            private IWorkspace workspaceContext;

            internal PackageListManifestScanner(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            public async Task<T> ScanContentAsync<T>(string filePath, CancellationToken cancellationToken)
            where T : class
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (typeof(T) != FileScannerTypeConstants.FileDataValuesType)
                {
                    throw new NotImplementedException();
                }

                using (StreamReader rdr = new StreamReader(filePath))
                {
                    var relativePath = workspaceContext.MakeRelative(filePath);
                    OutputUtils.OutputWindowPaneAsync(string.Format("Package List Manifest scanner invoked for: {0}", relativePath));

                    var manifests = await Parsing.ManifestParsing.ParseManifestListAsync(rdr, cancellationToken);

                    var results = new List<FileDataValue>(manifests.Select(m => new FileDataValue(
                        PackageIds.PackageListManifestEntryDataValueTypeGuid,
                        PackageIds.PackageListManifestEntryDataValueName,
                        m // value
                        //context: ?
                        )));

                    OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} packages in package manifest", manifests.Count()));

                    //
                    //{
                    //    var configs = await Build2Configs.EnumerateBuildConfigsForProjectPathAsync(workspaceContext.Location, cancellationToken);
                    //    results.AddRange(configs.Select(cfg => new FileDataValue(
                    //        BuildConfigurationContext.ContextTypeGuid,
                    //        BuildConfigurationContext.DataValueName,
                    //        null, // value
                    //        context: cfg.BuildConfiguration
                    //        )));
                    //}
                    //

                    // Ask bdep for the configurations the project is initialized in, and index them.
                    var configs = await Build2Configs.EnumerateBuildConfigsForProjectPathAsync(workspaceContext.Location, cancellationToken);
                    results.AddRange(configs.Select(cfg => new FileDataValue(
                        PackageIds.Build2ConfigDataValueTypeGuid,
                        PackageIds.Build2ConfigDataValueName,
                        cfg
                        )));
                    OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} configurations for project", configs.Count()));

                    OutputUtils.OutputWindowPaneAsync(string.Format("Package List Manifest scanner completed for: {0}", relativePath));

                    return (T)(IReadOnlyCollection<FileDataValue>)results;
                }
            }
        }
    }
}
