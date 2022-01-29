using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace B2VS.Language.Buildfile
{
    #region Format definition
    /// <summary>
    /// Defines the editor format for the buildfile keyword type.
    /// </summary>
    
    // seems like there's already a definition registered for 'keyword'

/*    [Export(typeof(EditorFormatDefinition))]
    [ClassificationType(ClassificationTypeNames = "keyword")]
    [Name("keyword")]
    [UserVisible(false)]
    [Order(Before = Priority.Default)]
    internal sealed class KeywordFmt : ClassificationFormatDefinition
    {
        public KeywordFmt()
        {
            DisplayName = "keyword";
            ForegroundColor = Colors.Blue;
        }
    }
*/
    #endregion //Format definition
}
