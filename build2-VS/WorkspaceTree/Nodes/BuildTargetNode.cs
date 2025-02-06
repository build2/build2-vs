using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using B2VS.Toolchain.Json.B.DumpLoad;
using B2VS.VSPackage;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace B2VS.Workspace.Nodes
{
    class Build2BuildTargetNode : WorkspaceVisualNodeBase
    {
        private BuildLoadStatus.Target _target;
        private ImageMoniker _moniker;

        public BuildLoadStatus.Target Target => _target;

        public Build2BuildTargetNode(WorkspaceVisualNodeBase parent, BuildLoadStatus.Target target /*, string filePath*/ /*, ImageMoniker moniker*/) : base(parent)
        {
            this._target = target;
            this.NodeMoniker = target.name;
            this._moniker = KnownMonikers.TargetFile;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();
            UINode.Text = Target.displayName;
            SetIcon(_moniker.Guid, _moniker.Id);
        }

        public override int Compare(WorkspaceVisualNodeBase right)
        {
            var node = right as Build2BuildTargetNode;
            if (node == null)
                return -1;
            return Target.name.CompareTo(node.Target.name);
        }
    }

    class BuildfileTargetsChildrenSource : IChildrenSource2
    {
        private INodeExtender _source;
        private WorkspaceVisualNodeBase _parentNode;
        private IWorkspace _workspace;

        public BuildfileTargetsChildrenSource(INodeExtender extender, WorkspaceVisualNodeBase parentNode)
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
            var dataSvc = await _workspace.GetIndexWorkspaceDataServiceAsync();
            var wsData = dataSvc.CreateIndexWorkspaceData();
            var buildfileNode = _parentNode as IFileNode;
            var targets = await wsData.GetFileDataValuesAsync<BuildLoadStatus.Target>(
                buildfileNode.FullPath,
                PackageIds.Build2BuildTargetDataValueTypeGuid,
                cancellationToken: cancellationToken);
            List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();
            foreach (var tgt in targets)
            {
                Debug.Assert(tgt.Name == PackageIds.Build2BuildTargetDataValueName);

                var node = new Build2BuildTargetNode(
                    _parentNode,
                    tgt.Value);
                children.Add(node);
            }

            return children;
        }

        public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
        {
            return await GetCollectionAsync(CancellationToken.None);
        }
    }
}
