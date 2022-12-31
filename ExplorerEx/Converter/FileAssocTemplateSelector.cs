using System.Windows;
using System.Windows.Controls;
using ExplorerEx.Models;

namespace ExplorerEx.Converter;

internal class FileAssocTemplateSelector : StyleSelector
{
    public Style DefaultStyle { get; set; } = null!;

    public Style CustomStyle { get; set; } = null!;

    public override Style SelectStyle(object item, DependencyObject container)
    {
        return item is FileAssocItem ? CustomStyle : DefaultStyle;
    }
}