using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using B2VS.Toolchain.Json;
using B2VS.Utilities;

namespace B2VS.Toolchain
{
    internal static class BuildTargets
    {
        public static async Task<IEnumerable<Json.B.DumpLoad.BuildLoadStatus.Target>> EnumerateBuildfileTargetsAsync(string buildfilePath, CancellationToken cancellationToken)
        {
            // @todo: think we can invoke on the buildfile itself, but unsure of syntax so for now using the containing directory target (should do same thing)
            var targetPath = Path.GetDirectoryName(buildfilePath) + '/';
            var jsonDumpStr = "";
            var stdErr = "";
            Action<string> outputHandler = (string line) => jsonDumpStr += line;
            Action<string> errorHandler = (string line) => stdErr += line;
            var exitCode = await Build2Toolchain.B.InvokeQueuedAsync(
                // @NOTE: --dump-scope=x, if x is relative it appears to be interpreted relative to cwd, not path of the given target
                new string[] { "--load-only", "--dump=load", string.Format("--dump-scope={0}", targetPath), "--dump-format=json-v0.1", targetPath },
                cancellationToken,
                outputHandler,
                errorHandler);
            if (exitCode != 0)
            {
                throw new Exception(string.Format("'b --dump' failed: {0}", stdErr));
            }

            try
            {
                var json = JsonUtils.StrictDeserialize<Json.B.DumpLoad.BuildLoadStatus>(jsonDumpStr);
                return json.targets;
            }
            catch (Exception ex)
            {
                OutputUtils.OutputWindowPaneAsync("'b --dump' json output not parseable");
                throw;
            }
        }
    }
}
