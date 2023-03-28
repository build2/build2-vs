using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2VS.VSPackage
{
    internal static class PackageIds
    {
        public static readonly Guid BuildCommandGroupGuid = new Guid("16537f6e-cb14-44da-b087-d1387ce3bf57");

        // Guids from VSCT file.
        public static readonly Guid Build2GeneralCmdSet = new Guid("{AE05FE3E-FF47-42B2-B5B2-6BE612927573}");

        // Guids to associate file context action factories.
        public const string BuildfileContextType = "F08A8F02-FF58-4DAD-B904-9257337B2BE2";
        public static readonly Guid BuildfileContextTypeGuid = new Guid(BuildfileContextType);

        // Data values stored for packages.manifest, one value per package entry.
        public const string PackageListManifestEntryDataValueTypeStr = "{0C103BF1-0B17-4A9D-889F-4751D1E8B976}";
        public static readonly Guid PackageListManifestEntryDataValueTypeGuid = new Guid(PackageListManifestEntryDataValueTypeStr);
        public const string PackageListManifestEntryDataValueName = nameof(PackageListManifestEntryDataValueName);

        // Data values stored for manifest files (packages.manifest is used to represent project, manifest used for packages),
        // one value per build configuration that the project/package is initialized in.
        public const string Build2ConfigDataValueTypeStr = "{38CFA85B-D849-462E-93BF-FF6D2BDE1FE6}";
        public static readonly Guid Build2ConfigDataValueTypeGuid = new Guid(Build2ConfigDataValueTypeStr);
        public const string Build2ConfigDataValueName = nameof(Build2ConfigDataValueName);
    }
}
