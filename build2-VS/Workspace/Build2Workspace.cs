using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using B2VS.Language.Manifest;
using B2VS.Toolchain;
using B2VS.VSPackage;
using System.Diagnostics;
using System.IO.Packaging;
using System.Threading;

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
        public static async Task<IEnumerable<string>> EnumeratePackageLocationsAsync(IWorkspace workspace, bool verify = true, CancellationToken cancellationToken = default)
        {
            //if (IsMultiPackageProject(workspace))
            //{
            //    var indexService = workspace.GetIndexWorkspaceService();
            //    var packageListValues = await indexService.GetFileDataValuesAsync<Build2Manifest>(
            //        Build2Constants.PackageListManifestFilename,
            //        VSPackage.PackageIds.PackageListManifestEntryDataValueTypeGuid);
            //    return packageListValues.Select(entry => entry.Value.Entries["location"]);
            //}
            //else
            //{
            //    return new string[] { "" }; // "." };
            //}

            var indexService = workspace.GetIndexWorkspaceService();
            var filePaths = await indexService.GetFilesAsync(Build2Constants.PackageManifestFilename, cancellationToken);
            // @NOTE: Above returns anything containing the pattern, not only exact matches.
            var manifestFilePaths = filePaths.Where(path => Path.GetFileName(path) == Build2Constants.PackageManifestFilename);
            var manifestFolderPaths = manifestFilePaths.Select(manifestPath => Path.GetDirectoryName(manifestPath));
            var validManifestFolderPaths = new List<string>();
            foreach (var path in manifestFolderPaths)
            {
                if (!verify || await VerifyValidPackageNoIndexAsync(path, cancellationToken))
                {
                    validManifestFolderPaths.Add(path);
                }
            }
            return validManifestFolderPaths;
        }

        /// <summary>
        /// Maps a path to the path of the build2 package containing it.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static async Task<string> GetContainingPackagePathAsync(IWorkspace workspaceContext, string filePath, CancellationToken cancellationToken = default)
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
                var packageLocations = await EnumeratePackageLocationsAsync(workspaceContext, cancellationToken: cancellationToken);
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

        public static async Task<bool> VerifyValidPackageNoIndexAsync(string folderPath, CancellationToken cancellationToken = default)
        {
            // Assumption is that we've found a path with a manifest, but we want to check that it's registered as a package
            // in a containing multi-package project (in packages.manifest), or it is a project itself.
            // @NOTE: Bit weird, but important to specify json output format as it prevents non-zero exit codes when the package is valid
            // but is not initialized in any configuration. See https://cpplang.slack.com/archives/CDJ0Z991S/p1648741346159539?thread_ts=1647000265.335129&cid=CDJ0Z991S
            var args = new string[] { "status", "-d", folderPath, "-a", "--stdout-format", "json" };
            var exitCode = await Build2Toolchain.BDep.InvokeQueuedAsync(args, cancellationToken);
            return exitCode == 0;
        }

        public static async Task<bool> IsPackageRootFolderNoIndexAsync(string folderPath, bool verify, CancellationToken cancellationToken = default)
        {
            // Determine based on existence of manifest file
            // @todo: without verifying that the package also exists in packages.manifest, this is flaky,
            // but doing that without using the index is ridiculous...
            var matches = Directory.EnumerateFiles(folderPath, Build2Constants.PackageManifestFilename);
            if (matches.Count() == 1)
            {
                return !verify || await VerifyValidPackageNoIndexAsync(folderPath, cancellationToken);
            }
            Debug.Assert(matches.Count() == 0); // Assuming above search pattern looks for exact match...?
            return false;
        }

        /// <summary>
        /// Maps a path to the path of the build2 package containing it.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static async Task<string> GetContainingPackagePathNoIndexAsync(IWorkspace workspaceContext, string filePath, bool verify, CancellationToken cancellationToken = default)
        {
            var rootDirName = workspaceContext.Location;
            var dir = new DirectoryInfo(Path.GetDirectoryName(filePath));
            while (dir.Exists)
            {
                if (await IsPackageRootFolderNoIndexAsync(dir.FullName, verify, cancellationToken))
                {
                    return dir.FullName;
                }
                if (dir.FullName == rootDirName)
                {
                    break;
                }
                dir = dir.Parent;
            }

            return null;
        }
    }
}
