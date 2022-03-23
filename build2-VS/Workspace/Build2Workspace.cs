using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using B2VS.Language.Manifest;

namespace B2VS.Workspace
{
    internal static class Build2Workspace
    {
        /// <summary>
        /// Maps a path to the path of the build2 package containing it.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static async Task<string> GetContainingPackagePathAsync(IWorkspace workspaceContext, string filePath)
        {
            //var dataService = workspaceContext.GetIndexWorkspaceDataService();
            //var data = dataService.CreateIndexWorkspaceData();
            //data.

            var indexService = workspaceContext.GetIndexWorkspaceService();
            var packageListValues = await indexService.GetFileDataValuesAsync<Build2Manifest>(
                Build2Constants.PackageListManifestFilename,
                VSPackage.PackageIds.PackageListManifestEntryDataValueTypeGuid);
            // Since build2 does not allow nested packages, we look for the first package whose base path contains the given path.
            foreach (var manifestData in packageListValues)
            {
                var manifest = manifestData.Value;
                var relPackageLocation = manifest.Entries["location"];
                var absPackageLocation = Path.Combine(workspaceContext.Location, relPackageLocation);
                var relative = PathUtils.GetRelativePath(absPackageLocation, filePath);
                if (relative != filePath && !relative.StartsWith(".."))
                {
                    return absPackageLocation;
                }
            }
            return null;
        }
    }
}
