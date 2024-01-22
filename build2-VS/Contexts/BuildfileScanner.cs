using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Text.Json;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;
using B2VS.Toolchain;
using B2VS.Workspace;
using B2VS.ProjectModel;
using Microsoft.VisualStudio.Workspace.Debug;
using System.Windows.Documents;
using B2VS.VSPackage;

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
        new Type[] { typeof(IReadOnlyCollection<FileDataValue>) } //, typeof(IReadOnlyCollection<FileReferenceInfo>) }
        )]
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
            }

            public async Task<T> ScanContentAsync<T>(string filePath, CancellationToken cancellationToken)
            where T : class
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = workspaceContext.MakeRelative(filePath);
                OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner [{0}] invoked for: {1}", typeof(T), relativePath));

                if (typeof(T) == FileScannerTypeConstants.FileDataValuesType)
                {
                    var indexService = workspaceContext.GetIndexWorkspaceService();

                    //using (StreamReader rdr = new StreamReader(filePath))
                    //{
                    //}

                    var results = new List<FileDataValue>();

                    void AddStartupItem(string name)
                    {
                        IPropertySettings launchSettings = new PropertySettings
                        {
                            [LaunchConfigurationConstants.NameKey] = name,
                            //[LaunchConfigurationConstants.DebugTypeKey] = LaunchConfigurationConstants.NativeOptionKey,
                            //[LaunchConfigurationConstants.ProgramKey] = binTarget,
                        };
                        results.Add(new FileDataValue(
                            DebugLaunchActionContext.ContextTypeGuid,
                            DebugLaunchActionContext.IsDefaultStartupProjectEntry,
                            value: launchSettings,
                            target: null/*outFile*/));
                    }

                    // Determine containing package

                    //var packagePath = await Build2Workspace.GetContainingPackagePathNoIndexAsync(workspaceContext, filePath, verify: true, cancellationToken: cancellationToken);
                    //if (packagePath != null)
                    //{
                    //    // Grab cached build configurations for our package
                    //    var packageManifestPath = Path.Combine(packagePath, Build2Constants.PackageManifestFilename);

                    //    var buildConfigs = await ProjectConfigUtils.GetBuildConfigurationsForPathOnDemandAsync(packagePath, workspaceContext, cancellationToken);
                    //    results.AddRange(buildConfigs.Select(cfg => new FileDataValue(
                    //        BuildConfigurationContext.ContextTypeGuid,
                    //        BuildConfigurationContext.DataValueName,
                    //        value: null,
                    //        target: null,
                    //        context: cfg.BuildConfiguration
                    //    )));

                    //    // @todo: enumerate exe targets within the buildfile

                    //    var buildfileDir = new DirectoryInfo(Path.GetDirectoryName(filePath));
                    //    string tempTargetName = buildfileDir.Name;
                    //    // @todo: pull pkg name from index
                    //    var packageDir = new DirectoryInfo(packagePath);

                    //    // Just restricting startup items (which can also be built from the top menu) to packages for now.
                    //    if (string.Equals(PathUtils.NormalizePath(buildfileDir.FullName), PathUtils.NormalizePath(packageDir.FullName)))
                    //    {
                    //        string pkgName = packageDir.Name;
                    //        string name = $"{tempTargetName} [{pkgName}]";
                    //        AddStartupItem(name);
                    //    }

                    //    OutputUtils.OutputWindowPaneAsync(string.Format("Found {0} configs for '{1}'", buildConfigs.Count(), relativePath));
                    //}

                    // Target enumeration
                    {
                        var targets = BuildTargets.EnumerateBuildfileTargetsAsync(filePath, cancellationToken);

                        // @pending
                    }

                    OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner completed for: {0}", relativePath));
                    return (T)(IReadOnlyCollection<FileDataValue>)results;
                }
                //else if (typeof(T) == FileScannerTypeConstants.FileReferenceInfoType)
                //{                    
                //    var results = new List<FileReferenceInfo>();

                //    var buildConfigs = await ProjectConfigUtils.GetIndexedBuildConfigurationsForPathAsync(workspaceContext.Location, workspaceContext);

                //    // @TODO: VS will only show the 'Set as Startup Item' context menu option if a file ref
                //    // is given pointing at something ending in .exe (doesn't need to exist).
                //    // Ideally we should enumerate exe targets in the buildfile and use those.
                //    // Still don't understand why the ability to change build configs appears to be tied to debug launch and not just
                //    // anything that's buildable.
                //    string tgtPath = Path.Combine(Path.GetDirectoryName(filePath), "hack.exe");
                //    results.AddRange(buildConfigs.Select(cfg => new FileReferenceInfo(
                //        relativePath: tgtPath,
                //        target: null, // no idea how this is used
                //        //context: cfg.BuildConfiguration, - appears unneeded, though rust project passes something similar
                //        referenceType: (int)FileReferenceInfoType.Output
                //        )));

                //    OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner completed for: {0}", relativePath));
                //    return (T)(IReadOnlyCollection<FileReferenceInfo>)results;
                //}
                else
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
