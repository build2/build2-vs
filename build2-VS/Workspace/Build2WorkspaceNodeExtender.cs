using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using B2VS.Toolchain;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Imaging.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Workspace;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace B2VS.Workspace
{
    [ExportNodeExtender(KnownViews.PhysicalTree)]
    internal class Build2WorkspaceNodeExtender : INodeExtender
    {
        protected SVsServiceProvider _provider;
        private IWorkspaceCommandHandler _handler = new PackageBuildCommandHandler();

        [ImportingConstructor]
        public Build2WorkspaceNodeExtender([Import] SVsServiceProvider serviceProvider)
        {
            _provider = serviceProvider;
        }

        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            if (parentNode == parentNode.Root)
            {
                return new Build2RootChildrenSource(this, parentNode);
            }
            else if (parentNode is Build2PackagesRootNode)
            {
                return new PackagesChildrenSource(this, parentNode);
            }
            else if (parentNode is IFileNode)
            {
                var parentFileNode = parentNode as IFileNode;
                if (parentFileNode.FileName == Build2Constants.BuildfileFilename)
                {
                    return new BuildfileTargetsChildrenSource(this, parentNode);
                }
            }
            return null;
        }

        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
            => parentNode is Build2PackageNode ? _handler : null;

        public class Build2PackagesRootNode : WorkspaceVisualNodeBase
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

        class Build2RootChildrenSource : IChildrenSource
        {
            private INodeExtender _source;
            private WorkspaceVisualNodeBase _parentNode;
            private IWorkspace _workspace;

            public Build2RootChildrenSource(INodeExtender extender, WorkspaceVisualNodeBase parentNode)
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

            public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
            {
                List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();

                children.Add(new Build2PackagesRootNode(
                    (Extender as Build2WorkspaceNodeExtender)._provider,
                    _parentNode
                    ));

                return children;
            }
        }

        public class Build2PackageNode : WorkspaceVisualNodeBase, IFileNode
        {
            private readonly SVsServiceProvider _provider;
            private string _fileName;
            private string _filePath;
            private ImageMoniker _moniker;

            public string FileName => _fileName;
            public string FullPath => _filePath;

            public Build2PackageNode(SVsServiceProvider provider, WorkspaceVisualNodeBase parent, string fileName, string filePath /*, ImageMoniker moniker*/) : base(parent)
            {
                this._provider = provider;
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

        class PackagesChildrenSource : IChildrenSource
        {
            private INodeExtender _source;
            private WorkspaceVisualNodeBase _parentNode;
            private IWorkspace _workspace;

            public PackagesChildrenSource(INodeExtender extender, WorkspaceVisualNodeBase parentNode)
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

            public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
            {
                List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();
                
                foreach (var pkgPath in await Build2Workspace.EnumeratePackageLocationsAsync(_workspace, verify: false))
                {
                    var pkgName = Path.GetFileName(pkgPath); // @todo: pkg name from manifest/index;
                    var node = new Build2PackageNode(
                        (Extender as Build2WorkspaceNodeExtender)._provider,
                        _parentNode, 
                        pkgName, 
                        Path.Combine(pkgPath, Build2Constants.PackageManifestFilename));
                    children.Add(node);
                }

                return children;
            }
        }

        public class Build2BuildTargetNode : WorkspaceVisualNodeBase
        {
            private readonly SVsServiceProvider _provider;
            private Toolchain.Json.B.DumpLoad.BuildLoadStatus.Target _target;
            private ImageMoniker _moniker;

            public Toolchain.Json.B.DumpLoad.BuildLoadStatus.Target Target => _target;

            public Build2BuildTargetNode(SVsServiceProvider provider, WorkspaceVisualNodeBase parent, Toolchain.Json.B.DumpLoad.BuildLoadStatus.Target target /*, string filePath*/ /*, ImageMoniker moniker*/) : base(parent)
            {
                this._provider = provider;
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

        class BuildfileTargetsChildrenSource : IChildrenSource
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

            public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
            {
                List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();

                var buildfileNode = _parentNode as IFileNode;
                foreach (var tgt in await BuildTargets.EnumerateBuildfileTargetsAsync(buildfileNode.FullPath, CancellationToken.None))
                {
                    var node = new Build2BuildTargetNode(
                        (Extender as Build2WorkspaceNodeExtender)._provider,
                        _parentNode,
                        tgt);
                    children.Add(node);
                }

                return children;
            }
        }
    }
}
