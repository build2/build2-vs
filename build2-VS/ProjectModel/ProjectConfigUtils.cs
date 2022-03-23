using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using B2VS.Toolchain;
using B2VS.VSPackage;

namespace B2VS.ProjectModel
{
    /// <summary>
    /// Model of the build2 project opened in the VS workspace.
    /// </summary>
    internal static class ProjectConfigUtils
    {
        /// <summary>
        /// Returns a list of known build configurations associated with path.
        /// </summary>
        /// <param name="path">Assumed to identify either a package, or the top level project.</param>
        /// <param name="workspace"></param>
        /// <returns></returns>
        public static async Task<IEnumerable<Build2BuildConfiguration>> GetIndexedBuildConfigurationsForPathAsync(string path, IWorkspace workspace)
        {
            var indexService = workspace.GetIndexWorkspaceService();
            // Appears to be no sane way to reliably compare paths...
            bool isProject = workspace.MakeRooted(path) == workspace.Location;
            var entityPath = isProject ?
                Path.Combine(workspace.Location, Build2Constants.PackageListManifestFilename) :
                Path.Combine(path, Build2Constants.PackageManifestFilename);
            var buildConfigValues = await indexService.GetFileDataValuesAsync<Build2BuildConfiguration>(entityPath, PackageIds.Build2ConfigDataValueTypeGuid);
            return buildConfigValues.Select(entry => entry.Value);
        }
    }
}
