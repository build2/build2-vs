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
using static B2VS.Toolchain.Json.B.DumpLoad.BuildLoadStatus;
using System.IO.Packaging;

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
        new Type[] { typeof(IReadOnlyCollection<FileDataValue>), typeof(IReadOnlyCollection<FileReferenceInfo>) }
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
                    OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner [{0}] completed for: {1}", typeof(T), relativePath));
                    return (T)(IReadOnlyCollection<FileDataValue>)await GetFileDataValuesAsync(filePath, cancellationToken);
                }
                else if (typeof(T) == FileScannerTypeConstants.FileReferenceInfoType)
                {
                    OutputUtils.OutputWindowPaneAsync(string.Format("Buildfile scanner [{0}] completed for: {1}", typeof(T), relativePath));
                    return (T)(IReadOnlyCollection<FileReferenceInfo>)await GetFileReferenceInfosAsync(filePath, cancellationToken);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            private async Task<List<FileDataValue>> GetFileDataValuesAsync(string filePath, CancellationToken cancellationToken)
            {
                var results = new List<FileDataValue>();

                void AddStartupItem(string name, string target = null, string context = null)
                {
                    IPropertySettings launchSettings = new PropertySettings
                    {
                        [LaunchConfigurationConstants.TypeKey] = "default",
                        [LaunchConfigurationConstants.NameKey] = name,
                        [LaunchConfigurationConstants.DebugTypeKey] = LaunchConfigurationConstants.NativeOptionKey,
                        //[LaunchConfigurationConstants.ProgramKey] = binTarget,
                        //[LaunchConfigurationConstants.ProjectKey] = workspaceContext.MakeRelative(binTarget),
                        //[LaunchConfigurationConstants.ProjectTargetKey] = name,
                    };
                    results.Add(new FileDataValue(
                        DebugLaunchActionContext.ContextTypeGuid,
                        DebugLaunchActionContext.IsDefaultStartupProjectEntry,
                        value: launchSettings,
                        target: target,
                        context: context
                        ));
                }

                const bool usePerConfigTargets = true;

                // Determine containing package
                var packagePath = await Build2Workspace.GetContainingPackagePathAsync(workspaceContext, filePath, cancellationToken: cancellationToken);
                if (packagePath != null)
                {
                    var pkgName = new DirectoryInfo(packagePath).Name; // @todo: from indexed manifest, however, still issue with using index from scanner??
                    var packageRelativeBuildfilePath = PathUtils.GetRelativePath(packagePath + '/', filePath);
                    var configs = await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(packagePath, cancellationToken);
                    var condensedTargets = new List<Target>();
                    foreach (var cfg in configs)
                    {
                        var outPath = Path.Combine(cfg.ConfigDir, pkgName, packageRelativeBuildfilePath);
                        var targets = await BuildTargets.EnumerateBuildfileTargetsAsync(outPath, cfg, cancellationToken);
                        foreach (var target in targets)
                        {
                            if (target.type == "exe")
                            {
                                var configAgnosticTargetPath = target.name;// Path.Combine(pkgName, packageRelativeBuildfilePath); // @todo: not safe assumption, buildfiles could set out path to anything per config I guess.
                                var binTargetPath = Path.Combine(Path.GetDirectoryName(outPath), target.OutFileTitle) + ".exe";

                                results.Add(new FileDataValue(
                                    BuildConfigurationContext.ContextTypeGuid,
                                    BuildConfigurationContext.DataValueName,
                                    value: null,
                                    target: usePerConfigTargets ? binTargetPath : configAgnosticTargetPath,
                                    context: cfg.BuildConfiguration
                                    ));

                                // @NOTE: One simple approach that works is a startup item for every target-config pait, and allows us to embed the build configuration/output
                                // path info into the launch properties. However it has the drawback that it results in an entry in the startup items dropdown
                                // for every target-configuration pair, which quickly gets out of hand.
                                if (usePerConfigTargets)
                                {
                                    AddStartupItem(
                                        string.Format("{0} [{1}]", target.OutFileTitle, cfg.BuildConfiguration),
                                        target: binTargetPath,
                                        context: cfg.BuildConfiguration // ??
                                        );

                                    results.Add(new FileDataValue(
                                        PackageIds.Build2BuildTargetDataValueTypeGuid,
                                        PackageIds.Build2BuildTargetDataValueName,
                                        value: target,
                                        target: binTargetPath));
                                }
                                // So alternative to avoid that is to merge so that we have just a single startup item per target, but then we can only
                                // encode configuration agnostic info. 
                                // For whatever reason, despite try all reasonable combinations, nothing seems to result in the launch provider LaunchDebugTargetAsync
                                // call receiving a launch context with a valid BuildConfiguration (always null), so this approach is a bit cumbersome not only due to 
                                // this merging, but also needing additional build configuration name -> output path lookup in the launch handler.
                                else
                                {
                                    if (condensedTargets.Find(t => t.name == target.name) == null)
                                    {
                                        condensedTargets.Add(target);
                                    }
                                }
                            }
                        }

                        // @NOTE: This is only if we want to associate the buildfile as a whole (rather than specific contained targets) with a 
                        // build configuration. It seems this is maybe required in order for the launch action on the buildfile itself (which we don't really want)
                        // to automatically trigger a build first, but unable to recreate that behaviour for subtargets...
                        results.Add(new FileDataValue(
                            BuildConfigurationContext.ContextTypeGuid,
                            BuildConfigurationContext.DataValueName,
                            value: null,
                            target: null,
                            context: cfg.BuildConfiguration
                            ));
                    }

                    if (!usePerConfigTargets)
                    {
                        foreach (var target in condensedTargets)
                        {
                            AddStartupItem(
                                target.OutFileTitle,
                                target: target.name //configAgnosticTargetPath
                                );

                            results.Add(new FileDataValue(
                                PackageIds.Build2BuildTargetDataValueTypeGuid,
                                PackageIds.Build2BuildTargetDataValueName,
                                value: target,
                                target: target.name));
                        }
                    }
                }

                // Enumerate contained build targets and index.
                {
                    // @note: disabled for now due to issue that needs further investigation.
                    // kperf is triggering a weird error when running b from within the bdep project folder (rather than in the config, which works).
                    // seems to relate to kperf not being initialized in the forwarded configuration, but it triggers even if run b from /core.
                    // need to try to repro in a simpler project.
                    // also note that for this target enumeration case, we actually don't want to use the forwarding anyway, we want to run in a config
                    // probably (though relates to question of whether we can do this without a config), but unsure how to get the equivalent out dir path
                    // for an arbitrary buildfile.

                    //var targets = await BuildTargets.EnumerateBuildfileTargetsAsync(filePath, cancellationToken);

                    //results.AddRange(targets.Select(tgt => new FileDataValue(
                    //    PackageIds.Build2BuildTargetDataValueTypeGuid,
                    //    PackageIds.Build2BuildTargetDataValueName,
                    //    value: tgt)));
                }

                return results;
            }

            private async Task<List<FileReferenceInfo>> GetFileReferenceInfosAsync(string filePath, CancellationToken cancellationToken)
            {
                var results = new List<FileReferenceInfo>();

                // Old comment:

                // VS will only show the 'Set as Startup Item' context menu option if a file ref
                // is given pointing at something ending in .exe (doesn't need to exist).
                // Ideally we should enumerate exe targets in the buildfile and use those.
                // Still don't understand why the ability to change build configs appears to be tied to debug launch and not just
                // anything that's buildable.

                var packagePath = await Build2Workspace.GetContainingPackagePathAsync(workspaceContext, filePath, cancellationToken: cancellationToken);
                if (packagePath != null)
                {
                    var packageRelativeBuildfilePath = PathUtils.GetRelativePath(packagePath + '/', filePath);
                    var pkgName = new DirectoryInfo(packagePath).Name; // @todo: from indexed manifest, however, still issue with using index from scanner??
                    var configs = await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(packagePath, cancellationToken);
                    foreach (var cfg in configs)
                    {
                        var outPath = Path.Combine(cfg.ConfigDir, pkgName, packageRelativeBuildfilePath);
                        var targets = await BuildTargets.EnumerateBuildfileTargetsAsync(outPath, cfg, cancellationToken);

                        results.AddRange(targets
                            .Where(tgt => tgt.type == "exe")
                            .Select(tgt => new FileReferenceInfo(
                                // @todo: possibly meant to be rel to our own file path (the buildfile)?
                                relativePath: workspaceContext.MakeRelative(Path.Combine(Path.GetDirectoryName(outPath), tgt.OutFileTitle) + ".exe"),
                                target: Path.Combine(Path.GetDirectoryName(outPath), tgt.OutFileTitle) + ".exe", // no idea how this is used
                                //context: cfg.BuildConfiguration, - appears unneeded, though rust project passes something similar
                                referenceType: (int)FileReferenceInfoType.Output
                            )));
                    }
                }

                return results;
            }
        }
    }
}
