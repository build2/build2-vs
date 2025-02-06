using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace B2VS.Workspace.Nodes
{
    class Build2PackageNode : WorkspaceVisualNodeBase, IFileNode
    {
        private string _fileName;
        private string _filePath;
        private ImageMoniker _moniker;

        public string FileName => _fileName;
        public string FullPath => _filePath;

        public Build2PackageNode(WorkspaceVisualNodeBase parent, string fileName, string filePath /*, ImageMoniker moniker*/) : base(parent)
        {
            this._fileName = fileName;
            this._filePath = filePath;
            this.NodeMoniker = fileName;
            this._moniker = KnownMonikers.Package;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            UINode.Text = _fileName;
            SetIcon(_moniker.Guid, _moniker.Id);
        }

        public override int Compare(WorkspaceVisualNodeBase right)
        {
            var node = right as Build2PackageNode;
            if (node == null)
                return -1;
            return _fileName.CompareTo(node._fileName);
        }
    }

    class Build2PackagesChildrenSource : IChildrenSource2
    {
        private INodeExtender _source;
        private WorkspaceVisualNodeBase _parentNode;
        private IWorkspace _workspace;

        public Build2PackagesChildrenSource(INodeExtender extender, WorkspaceVisualNodeBase parentNode)
        {
            this._source = extender;
            this._parentNode = parentNode;
            this._workspace = parentNode.Workspace;
        }

        public INodeExtender Extender => _source;

        public int Order => 1;

        public bool ForceExpanded => false;

        public void Dispose()
        {
        }

        public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync(CancellationToken cancellationToken)
        {
            List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();

            foreach (var pkgPath in await Build2Workspace.EnumeratePackageLocationsAsync(_workspace, verify: false, cancellationToken: cancellationToken))
            {
                var pkgName = Path.GetFileName(pkgPath); // @todo: pkg name from manifest/index;
                var node = new Build2PackageNode(
                    _parentNode,
                    pkgName,
                    Path.Combine(pkgPath, Build2Constants.PackageManifestFilename));
                children.Add(node);
            }

            return children;
        }

        public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
        {
            return await GetCollectionAsync(CancellationToken.None);
        }
    }

    internal class Build2PackagesRootNode : WorkspaceVisualNodeBase
    {
        private readonly SVsServiceProvider _provider;
        private ImageMoniker _moniker;

        public Build2PackagesRootNode(SVsServiceProvider provider, WorkspaceVisualNodeBase parent) : base(parent)
        {
            this._provider = provider;
            this.NodeMoniker = "BUILD2_Packages";
            this._moniker = KnownMonikers.FlatList;
        }

        public override bool SupportsRename => false;

        protected override void OnInitialized()
        {
            base.OnInitialized();
            UINode.Text = "packages";
            SetIcon(_moniker.Guid, _moniker.Id);
            SetExpandedIcon(_moniker.Guid, _moniker.Id);
        }

        public override int Compare(WorkspaceVisualNodeBase right)
        {
            Build2PackagesRootNode node = right as Build2PackagesRootNode;
            if (node == null)
                return -1;
            return 0;
        }
    }
}
