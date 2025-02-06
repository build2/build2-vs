using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using B2VS.Workspace.Nodes;
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
                return new Build2PackagesChildrenSource(this, parentNode);
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

        class Build2RootChildrenSource : IChildrenSource2
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

            public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync(CancellationToken cancellationToken)
            {
                List<WorkspaceVisualNodeBase> children = new List<WorkspaceVisualNodeBase>();

                children.Add(new Build2PackagesRootNode(
                    (Extender as Build2WorkspaceNodeExtender)._provider,
                    _parentNode
                    ));

                return children;
            }

            public async Task<IReadOnlyCollection<WorkspaceVisualNodeBase>> GetCollectionAsync()
            {
                return await GetCollectionAsync(CancellationToken.None);
            }
        }
    }
}
