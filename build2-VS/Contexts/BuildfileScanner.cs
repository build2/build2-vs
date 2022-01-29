using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.Build;
using Microsoft.VisualStudio.Workspace.Indexing;

namespace B2VS.Contexts
{
    /// <summary>
    /// File scanner provider factory, for scanning buildfiles.
    /// Currently this is used to generate build configuration contexts - not clear that buildfiles are the right place for this, but
    /// using for now just to get something working.
    /// </summary>
    [ExportFileScanner(
        ProviderType,
        "buildfile",
        new String[] { "buildfile" },
        new Type[] { typeof(IReadOnlyCollection<FileDataValue>) })]
    class BuildfileScannerFactory : IWorkspaceProviderFactory<IFileScanner>
    {
        // Unique Guid for BuildfileScanner.
        public const string ProviderType = "474D1559-6CBA-4EB7-A380-97ACF82451EF";

        public IFileScanner CreateProvider(IWorkspace workspaceContext)
        {
            return new BuildfileScanner(workspaceContext);
        }

        private class BuildfileScanner : IFileScanner
        {
            private IWorkspace workspaceContext;

            internal BuildfileScanner(IWorkspace workspaceContext)
            {
                this.workspaceContext = workspaceContext;
            }

            public async Task<T> ScanContentAsync<T>(string filePath, CancellationToken cancellationToken)
            where T : class
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (typeof(T) != FileScannerTypeConstants.FileDataValuesType)
                {
                    throw new NotImplementedException();
                }

                using (StreamReader rdr = new StreamReader(filePath))
                {
                    //string line;
                    //int lineNo = 1;
                    var results = new List<FileDataValue>();
                    //while ((line = await rdr.ReadLineAsync()) != null)
                    //{
                    //    // Extract any line that starts with ` as a symbol and add it to the symbol database for that file.
                    //    if (line.StartsWith("`"))
                    //    {
                    //        results.Add(new SymbolDefinition(line.Substring(1), SymbolKind.None, SymbolAccessibility.None, new TextLocation(lineNo, 1)));
                    //    }
                    //    ++lineNo;
                    //}
                    results.Add(new FileDataValue(
                        BuildConfigurationContext.ContextTypeGuid,
                        BuildConfigurationContext.DataValueName,
                        null, // value
                        context: Build2BuildConfiguration.PlaceholderBuildConfigName
                        ));
                    return (T)(IReadOnlyCollection<FileDataValue>)results;
                }
            }
        }
    }
}
