using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.LanguageServer.Client;

namespace B2VS.Language
{
    internal sealed class ContentTypes
    {
        // Unclear if distinction is needed, but for now 'buildfile-like' represents any file using the buildfile language,
        // such as 'buildfile', 'root.build', etc.
        // The 'buildfile' type matches only those files named exactly 'buildfile'.
        [Export]
        [Name("buildfile-like")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition buildfileLikeContentType = null;

        [Export]
        [Name("buildfile")]
        [BaseDefinition("buildfile-like")]
        internal static ContentTypeDefinition buildfileContentType = null;

        [Export]
        [FileExtension(".build")]
        [ContentType("buildfile-like")]
        internal static FileExtensionToContentTypeDefinition buildfileLikeExtensionDefinition = null;

        [Export]
        [FileName(Build2Constants.BuildfileFilename)] // Matches only 'buildfile' exactly
        [ContentType("buildfile")]
        internal static FileExtensionToContentTypeDefinition buildfileExtensionDefinition = null;


        [Export]
        [Name("manifest")]
        [BaseDefinition(CodeRemoteContentDefinition.CodeRemoteContentTypeName)]
        internal static ContentTypeDefinition manifestContentType = null;

        [Export]
        [FileName(Build2Constants.PackageManifestFilename)]
        [ContentType("manifest")]
        internal static FileExtensionToContentTypeDefinition packageManifestFileDefinition = null;

        [Export]
        [FileName(Build2Constants.PackageListManifestFilename)]
        [ContentType("manifest")]
        internal static FileExtensionToContentTypeDefinition packageListManifestFileDefinition = null;

        [Export]
        [FileName(Build2Constants.RepositoriesManifestFilename)]
        [ContentType("manifest")]
        internal static FileExtensionToContentTypeDefinition repositoriesManifestFileDefinition = null;
    }
}
