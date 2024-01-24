using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Workspace;
using B2VS.Toolchain;
using System.Diagnostics;
using System.Threading;

namespace B2VS.Workspace
{
    internal static class Build2Workspace
    {
        // Returns workspace-relative package paths (to the package folder)
        public static async Task<IEnumerable<string>> EnumeratePackageLocationsAsync(IWorkspace workspace, bool verify = true, CancellationToken cancellationToken = default)
        {
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
            var packageLocations = await EnumeratePackageLocationsAsync(workspaceContext, verify: false, cancellationToken: cancellationToken);
            // Since build2 does not allow nested packages, we look for the first package whose base path contains the given path.
            foreach (var relPackageLocation in packageLocations)
            {
                var absPackageLocation = Path.Combine(workspaceContext.Location, relPackageLocation);
                var relative = PathUtils.GetRelativePath(absPackageLocation + '/', filePath);
                if (relative != filePath && !relative.StartsWith(".."))
                {
                    return absPackageLocation;
                }
            }
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
    }
}
