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
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetIndexedBuildConfigurationsForPathAsync(string path, IWorkspace workspace, CancellationToken cancellationToken)
        {
            // Appears to be no sane way to reliably compare paths...
            bool isProject = false; // workspace.MakeRooted(path) == workspace.Location;
            bool isMultiPackageProjectLevelRequest = isProject && Workspace.Build2Workspace.IsMultiPackageProject(workspace);
            var entityPath = isMultiPackageProjectLevelRequest ?
                Path.Combine(workspace.Location, Build2Constants.PackageListManifestFilename) :
                Path.Combine(path, Build2Constants.PackageManifestFilename);
            var indexService = workspace.GetIndexWorkspaceService();
            var buildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(entityPath, PackageIds.Build2ConfigDataValueTypeGuid, cancellationToken: cancellationToken);
            return FilterBuildConfigurationsBySettings(buildConfigValues.Select(entry => entry.Value), workspace);
        }

        /// <summary>
        /// Returns a list of known build configurations associated with path, using build2 invocations directly.
        /// </summary>
        /// <param name="path">Assumed to identify either a package, or the top level project.</param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetBuildConfigurationsForPathOnDemandAsync(string path, IWorkspace workspace, CancellationToken cancellationToken)
        {
            // Appears to be no sane way to reliably compare paths...
            bool isProject = false;// workspace.MakeRooted(path) == workspace.Location;
            bool isMultiPackageProjectLevelRequest = isProject && Workspace.Build2Workspace.IsMultiPackageProject(workspace);
            // @todo: receive optional cancellation token
            var configs = isMultiPackageProjectLevelRequest ?
                await Build2Configs.EnumerateBuildConfigsForProjectPathAsync(path, cancellationToken)
                : await Build2Configs.EnumerateBuildConfigsForPackagePathAsync(path, cancellationToken);
            return FilterBuildConfigurationsBySettings(configs, workspace);
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
