using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using B2VS.Language.Manifest;
using B2VS.Toolchain;
using B2VS.VSPackage;

namespace B2VS.Workspace
{
    internal static class Build2Workspace
    {
        // @todo: not sure best approach for this, maybe should be cached as a value attached to the root buildfile?
        public static bool IsMultiPackageProject(IWorkspace workspace)
        {
            return File.Exists(Path.Combine(workspace.Location, Build2Constants.PackageListManifestFilename));
        }

        // Returns workspace-relative package paths (to the package folder)
        public static async Task<IEnumerable<string>> EnumeratePackageLocationsAsync(IWorkspace workspace)
        {
            if (IsMultiPackageProject(workspace))
            {
                var indexService = workspace.GetIndexWorkspaceService();
                var packageListValues = await indexService.GetFileDataValuesAsync<Build2Manifest>(
                    Build2Constants.PackageListManifestFilename,
                    VSPackage.PackageIds.PackageListManifestEntryDataValueTypeGuid);
                return packageListValues.Select(entry => entry.Value.Entries["location"]);
            }
            else
            {
                return new string[] { "" }; // "." };
            }
        }

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

            //var indexService = workspaceContext.GetIndexWorkspaceService();
            //if (IsMultiPackageProject(workspaceContext))
            {
                //var packageListValues = await indexService.GetFileDataValuesAsync<Build2Manifest>(
                //    Build2Constants.PackageListManifestFilename,
                //    VSPackage.PackageIds.PackageListManifestEntryDataValueTypeGuid);
                var packageLocations = await EnumeratePackageLocationsAsync(workspaceContext);
                // Since build2 does not allow nested packages, we look for the first package whose base path contains the given path.
                //foreach (var manifestData in packageListValues)
                foreach (var relPackageLocation in packageLocations)
                {
                    //var manifest = manifestData.Value;
                    //var relPackageLocation = manifest.Entries["location"];
                    var absPackageLocation = Path.Combine(workspaceContext.Location, relPackageLocation);
                    var relative = PathUtils.GetRelativePath(absPackageLocation, filePath);
                    if (relative != filePath && !relative.StartsWith(".."))
                    {
                        return absPackageLocation;
                    }
                }
            }
            //else
            //{
            //    // Probably should return project path, since it is in fact a package too?
            //}
            return null;
        }
    }
}
