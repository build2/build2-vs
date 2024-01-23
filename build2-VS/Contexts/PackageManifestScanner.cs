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
using B2VS.ProjectModel;
using Microsoft.VisualStudio.Workspace.Build;

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

                var relativePath = workspaceContext.MakeRelative(filePath);
                Build2Toolchain.DebugHandler?.Invoke(string.Format("Package manifest scanner invoked for: {0}", relativePath));

                // @todo: verify here that filePath is a valid package manifest, and that the package is valid within the bdep project.

                var results = new List<FileDataValue>();

                using (StreamReader rdr = new StreamReader(filePath))
                {
                    var manifest = await Parsing.ManifestParsing.ParseSingleManifestAsync(rdr, cancellationToken);
                    results.AddRange(manifest.Entries.Select(kv => new FileDataValue(
                        PackageIds.PackageManifestEntryDataValueTypeGuid,
                        kv.Key, // data value name
                        kv.Value // value
                        )));
                }

                // Ask bdep for the configurations this package is initialized in, and index them.
                // @TODO: Need to somehow retrigger this scanner in response to configuration changes (it really doesn't make much sense to me that
                // the build config stuff is implemented through data values, since what configurations are relevant for the file is not defined 
                // purely by the contents of the file, and this would seem to be the case for any build system).
                var packagePath = Path.GetDirectoryName(filePath);
                var configs = await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(packagePath, cancellationToken);
                // @NOTE: Choosing to index here regardless of settings filter, since this scanner is really just meant to index the contents of
                // this particular file. 
                results.AddRange(configs.Select(cfg => new FileDataValue(
                    PackageIds.Build2ConfigDataValueTypeGuid,
                    PackageIds.Build2ConfigDataValueName,
                    cfg
                    )));

                results.AddRange(configs.Select(cfg => new FileDataValue(
                    BuildConfigurationContext.ContextTypeGuid,
                    BuildConfigurationContext.DataValueName,
                    value: null,
                    target: null,
                    context: cfg.BuildConfiguration
                    )));

                OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} configs for package '{1}'", configs.Count(), relativePath));

                Build2Toolchain.DebugHandler?.Invoke(string.Format("Package manifest scanner completed for: {0}", relativePath));

                return (T)(IReadOnlyCollection<FileDataValue>)results;
            }
        }
    }
}
