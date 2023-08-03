using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PixUI.Dynamic.Design;

public sealed class DesignController
{
    /// <summary>
    /// 设计画布缩放百分比
    /// </summary>
    public readonly State<int> Zoom = 100;

    private readonly List<DesignElement> _selection = new();

    public DesignElement? FirstSelected => _selection.Count > 0 ? _selection[0] : null;

    internal void Select(DesignElement element)
    {
        _selection.ForEach(o => o.IsSelected = false);
        _selection.Clear();

        _selection.Add(element);
        element.IsSelected = true;
    }

    public void Load(byte[] json)
    {
        var reader = new Utf8JsonReader(json);
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var propName = reader.GetString();
            switch (propName)
            {
                case "View":
                    ReadView(ref reader);
                    break;
            }
        }
    }

    private DesignElement ReadView(ref Utf8JsonReader reader)
    {
        var element = new DesignElement(this);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var propName = reader.GetString()!;
            switch (propName)
            {
                case nameof(DynamicWidgetData.Type):
                    reader.Read();
                    var meta = DynamicWidgetManager.GetByName(reader.GetString()!);
                    element.ChangeMeta(meta, false);
                    break;
                case nameof(DynamicWidgetData.CtorArgs):
                    ReadCtorArgs(element, ref reader);
                    break;
                case nameof(DynamicWidgetData.Properties):
                    ReadProperties(element, ref reader);
                    break;
                case "Child":
                    ReadChild(element, ref reader);
                    break;
                case "Children":
                    throw new NotImplementedException();
                    break;
            }
        }

        return element;
    }

    private static void ReadCtorArgs(DesignElement element, ref Utf8JsonReader reader)
    {
        var meta = element.Meta!;
        var data = element.Data;
        if (meta.CtorArgs == null || meta.CtorArgs.Length == 0) throw new InvalidOperationException();

        var args = new ValueSource[meta.CtorArgs.Length];
        reader.Read(); //[
        for (var i = 0; i < args.Length; i++)
        {
            args[i].Read(ref reader, meta.CtorArgs[i].Value);
        }

        reader.Read(); //]

        data.CtorArgs = args;
        element.ChangeTarget(null, meta.MakeInstance(data.CtorArgs!));
    }

    private static void ReadProperties(DesignElement element, ref Utf8JsonReader reader)
    {
        var meta = element.Meta!;
        var data = element.Data;
        
        if (element.Target == null)
            element.ChangeTarget(null, meta.MakeDefaultInstance());

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject) break;
            if (reader.TokenType != JsonTokenType.PropertyName) continue;

            var prop = new PropertyValue { Name = reader.GetString()! };
            var propMeta = meta.GetPropertyMeta(prop.Name);
            prop.Value.Read(ref reader, propMeta.Value);
            
            data.AddPropertyValue(prop);
            element.SetPropertyValue(prop);
        }
    }

    private void ReadChild(DesignElement element, ref Utf8JsonReader reader)
    {
        if (element.Target == null)
            element.ChangeTarget(null, element.Meta!.MakeDefaultInstance());
        
        var childElement = ReadView(ref reader);
        element.AddChild(childElement);
    }
}