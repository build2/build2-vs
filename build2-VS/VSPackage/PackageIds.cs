using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace B2VS.VSPackage
{
    internal static class PackageIds
    {
        // Guids from VSCT file.
        public static readonly Guid Build2GeneralCmdSet = new Guid("{AE05FE3E-FF47-42B2-B5B2-6BE612927573}");
        public const int TestCmdId = 0x0101; // ToDo, look into why Command ID 0x100 creates an extra entry in the context menu based on the display name of the command.
        //public const int ToggleWordCountCmdId = 0x0102;

        // Guids to associate file context action factories.
        public const string BuildfileContextType = "F08A8F02-FF58-4DAD-B904-9257337B2BE2";
    }
}
