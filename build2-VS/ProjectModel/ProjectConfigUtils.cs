using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Workspace;
using B2VS.Toolchain;
using B2VS.VSPackage;
using System.IO.Packaging;
using System.Threading;
using B2VS.Workspace;

namespace B2VS.ProjectModel
{
    /// <summary>
    /// Model of the build2 project opened in the VS workspace.
    /// </summary>
    internal static class ProjectConfigUtils
    {
        /// <summary>
        /// Returns a list of known build configurations associated with path, searching the index service only.
        /// </summary>
        /// <param name="path">Assumed to identify either a package, or the top level project.</param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetIndexedBuildConfigurationsForPathAsync(string path, IWorkspace workspace)
        {
            var indexService = workspace.GetIndexWorkspaceService();
            // Appears to be no sane way to reliably compare paths...
            bool isProject = workspace.MakeRooted(path) == workspace.Location;
            var entityPath = isProject && Workspace.Build2Workspace.IsMultiPackageProject(workspace) ?
                Path.Combine(workspace.Location, Build2Constants.PackageListManifestFilename) :
                Path.Combine(path, Build2Constants.PackageManifestFilename);
            var buildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(entityPath, PackageIds.Build2ConfigDataValueTypeGuid);
            return FilterBuildConfigurationsBySettings(buildConfigValues.Select(entry => entry.Value), workspace);
        }

        /// <summary>
        /// Returns a list of known build configurations associated with path, using build2 invocations directly.
        /// </summary>
        /// <param name="path">Assumed to identify either a package, or the top level project.</param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetBuildConfigurationsForPathOnDemandAsync(string path, IWorkspace workspace)
        {
            // Appears to be no sane way to reliably compare paths...
            bool isProject = workspace.MakeRooted(path) == workspace.Location;
            bool isMultiPackageProjectLevelRequest = isProject && Workspace.Build2Workspace.IsMultiPackageProject(workspace);
            // @todo: receive optional cancellation token
            var configs = isMultiPackageProjectLevelRequest ?
                await Build2Configs.EnumerateBuildConfigsForProjectPathAsync(path, CancellationToken.None)
                : await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(path, CancellationToken.None);
            return FilterBuildConfigurationsBySettings(configs, workspace);
        }

        /// <summary>
        /// Returns a list of known build configurations associated with path, trying the index service first, and falling back
        /// onto build2 invocations if there is no index data available.
        /// </summary>
        /// <param name="path">Assumed to identify either a package, or the top level project.</param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetBuildConfigurationsForPathAsync(string path, IWorkspace workspace)
        {
            var configs = await GetIndexedBuildConfigurationsForPathAsync(path, workspace);
            if (configs.Count() == 0)
            {
                configs = await GetBuildConfigurationsForPathOnDemandAsync(path, workspace);
            }
            return configs;
        }

        private static IEnumerable<Build2BuildConfiguration> FilterBuildConfigurationsBySettings(IEnumerable<Build2BuildConfiguration> configs, IWorkspace workspace)
        {
            // @todo: need to better differentiate name vs path. think unnamed configs will use full path for BuildConfiguration,
            // but intent of this setting was to match config names only...
            if (Build2Settings.get(workspace).GetProperty("ignore_build_context_patterns", out string[] ignoreConfigPatterns)
                == Microsoft.VisualStudio.Workspace.Settings.WorkspaceSettingsResult.Success)
            {
                return configs.Where(cfg => !ignoreConfigPatterns.Any(pattern => Regex.IsMatch(cfg.BuildConfiguration, pattern)));
            }
            else
            {
                return configs;
            }
        }
    }
}
