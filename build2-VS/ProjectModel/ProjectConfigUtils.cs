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
using Microsoft.VisualStudio.Workspace.Settings;

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
            var entityPath = Path.Combine(path, Build2Constants.PackageManifestFilename);
            var indexService = workspace.GetIndexWorkspaceService();
            var buildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(entityPath, PackageIds.Build2ConfigDataValueTypeGuid, cancellationToken: cancellationToken);
            return FilterBuildConfigurationsBySettings(buildConfigValues.Select(entry => entry.Value), workspace);
        }

        private static IEnumerable<Build2BuildConfiguration> FilterBuildConfigurationsBySettings(IEnumerable<Build2BuildConfiguration> configs, IWorkspace workspace)
        {
            var settings = Build2Settings.get(workspace);
            // @todo: need to better differentiate name vs path. think unnamed configs will use full path for BuildConfiguration,
            // but intent of this setting was to match config names only...
            bool disableIgnoreConfigPatterns = false;
            settings.GetProperty("disableIgnoreBuildConfigPatterns", out disableIgnoreConfigPatterns);
            if (!disableIgnoreConfigPatterns
                && settings.GetProperty("ignoreBuildConfigPatterns", out string[] ignoreConfigPatterns)
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
