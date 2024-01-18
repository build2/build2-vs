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
using B2VS.ProjectModel;
using B2VS.Toolchain;
using Microsoft.VisualStudio.Workspace.Debug;

namespace B2VS.Contexts
{
    /// <summary>
    /// File context provider for build2 buildfiles.
    /// </summary>
    [ExportFileContextProvider(
        ProviderType,
        PackageIds.BuildfileContextType,
        BuildContextTypes.BuildContextType,
        BuildContextTypes.RebuildContextType,
        BuildContextTypes.CleanContextType,
        DebugLaunchActionContext.ContextType)] 
    internal class BuildfileContextProviderFactory : IWorkspaceProviderFactory<IFileContextProvider>
    {
        // Unique Guid for BuildfileContextProvider.
        private const string ProviderType = "{2CA5FBE7-6E60-4EFF-9AD9-9EA10A85BCB0}";
        private static readonly Guid ProviderTypeGuid = new Guid(ProviderType);

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
                if (filename.Equals(Build2Constants.BuildfileFilename))
                {
                    fileContexts.Add(new FileContext(
                        ProviderTypeGuid,
                        new Guid(PackageIds.BuildfileContextType),
                        filePath,
                        new[] { filePath }));

                    // todo: need to understand if the buildfile scanner that produces configs is intended to be producing project-wide configs (in which case we
                    // shouldn't be doing it per buildfile), or scoped ones. suspect this may relate to weird behaviour being seen.

                    // @todo: needs attention.
                    // we may need to explicitly check if the packages list is up to date, since we need to distinguish between 'not up to date so can't generate
                    // configs' and 'up to date but there was no package containing this path, so we should just generate project level configs instead'.
                    // currently, the latter case is just not dealt with.
                    var basePath = await Build2Workspace.GetContainingPackagePathNoIndexAsync(workspaceContext, filePath);
                    if (basePath == null)
                    {
                        // Assume project
                        basePath = workspaceContext.Location;
                    }
                    //if (basePath != null)
                    {
                        // @note : disabled, after vs update the nested calls to the index service provider never yield.
                        // at a guess, it's no longer permitted to use the index service from within a context provider/file scanner callback??
                        var buildConfigs = await //ProjectConfigUtils.GetBuildConfigurationsForPathAsync(basePath, workspaceContext);
                            ProjectConfigUtils.GetBuildConfigurationsForPathOnDemandAsync(basePath, workspaceContext);

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
                    //else
                    //{
                    //    // @todo: will happen if the packages.manifest isn't yet indexed.
                    //    // will need to make sure somehow to retrigger this...
                    //}
                }

                return await Task.FromResult(fileContexts.ToArray());
            }
        }
    }
}
