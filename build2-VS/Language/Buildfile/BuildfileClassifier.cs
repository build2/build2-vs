using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace B2VS.Language.Buildfile
{
    /// <summary>
    /// Placeholder starting point for syntax highlighting of buildfile language.
    /// Performs naive whitespace-only delimited parsing, looks for a couple of keywords.
    /// See https://github.com/microsoft/VSSDK-Extensibility-Samples/tree/master/Ook_Language_Integration for example of how to do this in 
    /// a multi-layered approach (parsing to generalized token stream, them transforming those tokens to classifiers), which may make sense for better reuse.
    /// </summary>

#if false
    [Export(typeof(ITaggerProvider))]
    [ContentType("buildfile-like")]
    [TagType(typeof(ClassificationTag))]
    internal sealed class BuildfileElementClassifierProvider : ITaggerProvider
    {
        [Import]
        internal IClassificationTypeRegistryService classificationTypeRegistry = null;

        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            return new ElementClassifier(buffer, classificationTypeRegistry) as ITagger<T>;
        }
    }

    internal sealed class ElementClassifier : ITagger<ClassificationTag>
    {
        ITextBuffer buffer;
        IDictionary<string, IClassificationType> elementTypes;

        /// <summary>
        /// Construct the classifier and define search tokens
        /// </summary>
        internal ElementClassifier(ITextBuffer buffer, IClassificationTypeRegistryService typeService)
        {
            this.buffer = buffer;
            elementTypes = new Dictionary<string, IClassificationType>();
            elementTypes["import"] = typeService.GetClassificationType("keyword");
            elementTypes["export"] = typeService.GetClassificationType("keyword");
            elementTypes["using"] = typeService.GetClassificationType("keyword");
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged
        {
            add { }
            remove { }
        }

        /// <summary>
        /// Search the given span for any instances of classified tags
        /// </summary>
        public IEnumerable<ITagSpan<ClassificationTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            foreach (SnapshotSpan curSpan in spans)
            {
                ITextSnapshotLine containingLine = curSpan.Start.GetContainingLine();
                int curLoc = containingLine.Start.Position;
                string[] tokens = containingLine.GetText().ToLower().Split(' ');

                foreach (string tok in tokens)
                {
                    if (elementTypes.ContainsKey(tok))
                    {
                        var tokenSpan = new SnapshotSpan(curSpan.Snapshot, new Span(curLoc, tok.Length));
                        if (tokenSpan.IntersectsWith(curSpan))
                        {
                            yield return new TagSpan<ClassificationTag>(tokenSpan, new ClassificationTag(elementTypes[tok]));
                        }
                    }

                    // Add an extra char location because of the space
                    curLoc += tok.Length + 1;
                }
            }
        }
    }
#endif
}
