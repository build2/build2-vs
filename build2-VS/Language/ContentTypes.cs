using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Utilities;
using System.ComponentModel.Composition;

namespace B2VS.Language
{
    internal sealed class ContentTypes
    {
        // Unclear if distinction is needed, but for now 'buildfile-like' represents any file using the buildfile language,
        // such as 'buildfile', 'root.build', etc.
        // The 'buildfile' type matches only those files named exactly 'buildfile'.
        [Export]
        [Name("buildfile-like")]
        [BaseDefinition("code")] // Content types appear to form a graph, and can multiple-inherit. Presumably 'code' is a built-in defined type?
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
    }
}
