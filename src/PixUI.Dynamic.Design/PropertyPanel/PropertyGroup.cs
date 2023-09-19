using System.Collections.Generic;

namespace PixUI.Dynamic.Design;

public sealed class PropertyGroup : SingleChildWidget
{
    public PropertyGroup(State<string> title)
    {
        Child = new Collapse
        {
            Title = new Text(title) { FontWeight = FontWeight.Bold },
            Body = new Form { Ref = _formRef, LabelWidth = 108 }
        };
    }

    private readonly WidgetRef<Form> _formRef = new();

    internal void SetItems(IList<FormItem> items)
    {
        _formRef.Widget!.Children = items;
    }
}