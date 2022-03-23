using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Indexing;
using B2VS.VSPackage;
using B2VS.Toolchain;

namespace B2VS.Contexts
{
    /// <summary>
    /// File scanner provider factory, for scanning buildfiles.
    /// Currently this is used to generate build configuration contexts - not clear that buildfiles are the right place for this, but
    /// using for now just to get something working.
    /// </summary>
    [ExportFileScanner(
        ProviderType,
        "build2 package manifest",
        Build2Constants.PackageManifestFilename,
        typeof(IReadOnlyCollection<FileDataValue>))]
    class PackageManifestScannerFactory : IWorkspaceProviderFactory<IFileScanner>
    {
        // Unique Guid for PackageManifestScanner.
        public const string ProviderType = "{E32063F8-6E33-42E7-9487-9E3E69BB9603}";

        public IFileScanner CreateProvider(IWorkspace workspaceContext)
        {
            return new PackageManifestScanner(workspaceContext);
        }

        private class PackageManifestScanner : IFileScanner
        {
            private IWorkspace workspaceContext;

            internal PackageManifestScanner(IWorkspace workspaceContext)
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

                // Since the file filter in the attribute doesn't do what it says it does (despite no leading period, 'manifest' still leads to
                // this being invoked for 'x.manifest', we check here anyway.
                if (Path.GetFileName(filePath) != Build2Constants.PackageManifestFilename)
                {
                    return (T)(IReadOnlyCollection<FileDataValue>)new List<FileDataValue>();
                }

                // @todo: verify here that filePath is a valid package manifest, and that the package is valid within the bdep project.

                var relativePath = workspaceContext.MakeRelative(filePath);
                OutputUtils.OutputWindowPaneAsync(string.Format("Package manifest scanner invoked for: {0}", relativePath));

                var results = new List<FileDataValue>();

                // @todo: index the actual manifest contents here.

                // Ask bdep for the configurations this package is initialized in, and index them.
                var packagePath = Path.GetDirectoryName(filePath);
                var configs = await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(workspaceContext.Location, packagePath, cancellationToken);
                results.AddRange(configs.Select(cfg => new FileDataValue(
                    PackageIds.Build2ConfigDataValueTypeGuid,
                    PackageIds.Build2ConfigDataValueName,
                    cfg
                    )));

                OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} configs for package '{1}'", configs.Count(), relativePath));

                OutputUtils.OutputWindowPaneAsync(string.Format("Package manifest scanner completed for: {0}", relativePath));

                return (T)(IReadOnlyCollection<FileDataValue>)results;
            }
        }
    }
}
