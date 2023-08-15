using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using BindingFlags = System.Reflection.BindingFlags;

namespace PixUI.Dynamic.Design;

/// <summary>
/// 包装具体的属性值编辑器，提供绑定状态及重设(删除)按钮
/// </summary>
public sealed class PropertyEditor : Widget
{
    static PropertyEditor()
    {
        RegisterClassValueEditor<string, TextEditor>(true);
        RegisterStructValueEditor<Color, ColorEditor>(true);
    }

    /// <summary>
    /// 新建属性编辑器
    /// </summary>
    public PropertyEditor(DesignElement element, DynamicPropertyMeta propertyMeta)
    {
        var valueType = GetValueType(propertyMeta.Value);
        _targetEditor = GetPropertyValueEditor(valueType, element, propertyMeta, out var editingValue);
        _targetEditor.Parent = this;

        if (propertyMeta.Value.IsState)
            _bindButton = BuildBindButton();
        if (propertyMeta.AllowNull)
        {
            _deleteButton = BuildDeleteButton();
            _deleteButton.OnTap = _ => DeletePropertyValue(element, propertyMeta, editingValue);
        }
    }

    /// <summary>
    /// 新建构造参数编辑器
    /// </summary>
    public PropertyEditor(DesignElement element, DynamicCtorArgMeta ctorArgMeta)
    {
        var valueType = GetValueType(ctorArgMeta.Value);
        _targetEditor = GetCtorArgValueEditor(valueType, element, ctorArgMeta, out var editingValue);
        _targetEditor.Parent = this;

        if (ctorArgMeta.Value.IsState)
            _bindButton = BuildBindButton();
        if (ctorArgMeta.AllowNull)
        {
            _deleteButton = BuildDeleteButton();
            _deleteButton.OnTap = _ => DeleteCtorArgValue(element, ctorArgMeta, editingValue);
        }
    }

    private readonly Widget _targetEditor;
    private readonly Button? _deleteButton;
    private readonly Button? _bindButton;

    private static readonly State<float> _buttonSize = 20f;

    private Button BuildBindButton()
    {
        var button = new Button(icon: MaterialIcons.Link)
        {
            Style = ButtonStyle.Transparent,
            Width = _buttonSize,
            Height = _buttonSize
        };
        button.Parent = this;
        return button;
    }

    private Button BuildDeleteButton()
    {
        var button = new Button(icon: MaterialIcons.Clear)
        {
            Style = ButtonStyle.Transparent,
            Width = _buttonSize,
            Height = _buttonSize,
        };
        button.Parent = this;
        return button;
    }


    #region ====ValueEditors====

    private static readonly List<ValueEditorInfo> _valueEditors = new();

    private static Type GetValueType(DynamicValueMeta valueMeta)
    {
        var valueType = valueMeta.ValueType;
        if (valueType is { IsValueType: true, IsGenericType: true } &&
            valueType.GetGenericTypeDefinition() == typeof(Nullable<>))
            valueType = valueType.GenericTypeArguments[0];
        return valueType;
    }

    private static Func<State, Widget> CreateEditorMaker(Type valueType, Type editorType)
    {
        var stateType = typeof(State<>).MakeGenericType(valueType);
        var ctorArg = Expression.Parameter(typeof(State));
        var ctorInfo = editorType.GetConstructor(new[] { stateType });
        if (ctorInfo == null)
            throw new Exception("编辑器无指定状态的构造");
        return Expression.Lambda<Func<State, Widget>>(
            Expression.New(ctorInfo, Expression.Convert(ctorArg, stateType)), ctorArg
        ).Compile();
    }

    public static void RegisterStructValueEditor<TValue, TEditor>(bool isDefault)
        where TValue : struct
        where TEditor : Widget
    {
        var valueType = typeof(TValue);
        valueType = typeof(Nullable<>).MakeGenericType(valueType);

        var editor = new ValueEditorInfo(
            typeof(TEditor).Name, isDefault, typeof(TValue),
            CreateEditorMaker(valueType, typeof(TEditor)),
            (element, ctorArgMeta, index) => new RxProperty<TValue?>(
                () => (TValue?)GetCtorArgValue(element, ctorArgMeta, index),
                newValue => SetCtorArgValue(element, ctorArgMeta, index, newValue)
            ),
            (element, propertyMeta) => new RxProperty<TValue?>(
                () => (TValue?)GetPropertyValue(element, propertyMeta),
                newValue => SetPropertyValue(element, propertyMeta, newValue)
            )
        );

        _valueEditors.Add(editor);
    }

    public static void RegisterClassValueEditor<TValue, TEditor>(bool isDefault)
        where TValue : class
        where TEditor : Widget
    {
        var editor = new ValueEditorInfo(
            typeof(TEditor).Name, isDefault, typeof(TValue),
            CreateEditorMaker(typeof(TValue), typeof(TEditor)),
            (element, ctorArgMeta, index) => new RxProperty<TValue?>(
                () => (TValue?)GetCtorArgValue(element, ctorArgMeta, index),
                newValue => SetCtorArgValue(element, ctorArgMeta, index, newValue)
            ),
            (element, propertyMeta) => new RxProperty<TValue?>(
                () => (TValue?)GetPropertyValue(element, propertyMeta),
                newValue => SetPropertyValue(element, propertyMeta, newValue)
            )
        );

        _valueEditors.Add(editor);
    }

    /// <summary>
    /// 根据值类型获取默认的编辑器
    /// </summary>
    private static ValueEditorInfo? TryGetValueEditorByValueType(Type valueType) =>
        _valueEditors.FirstOrDefault(e => e.IsDefault && e.ValueType == valueType);

    #endregion

    #region ====CtorArgValue====

    private static Widget GetCtorArgValueEditor(Type valueType, DesignElement element, DynamicCtorArgMeta ctorArgMeta,
        out State? editingValue)
    {
        //TODO:先判断是否指定编辑器

        //获取注册的编辑器信息
        var editorInfo = TryGetValueEditorByValueType(valueType);
        if (editorInfo == null)
        {
            //TODO:
            editingValue = null;
            return new Input("None");
        }

        var index = Array.IndexOf(element.Meta!.CtorArgs!, ctorArgMeta);
        editingValue = editorInfo.CtorArgStateMaker(element, ctorArgMeta, index);
        return editorInfo.EditorMaker(editingValue);
    }

    private static object? GetCtorArgValue(DesignElement element, DynamicCtorArgMeta ctorArgMeta, int index)
    {
        var exists = element.Data.TryGetCtorArgValue(index);
        if (exists != null)
        {
            if (exists.Value.From != ValueSource.Const) throw new NotImplementedException();
            return exists.Value.Value;
        }

        return ctorArgMeta.Value.DefaultValue?.Value;
    }

    private static void SetCtorArgValue(DesignElement element, DynamicCtorArgMeta ctorArgMeta, int index,
        object? newValue)
    {
        var dynamicValue = new DynamicValue { From = ValueSource.Const, Value = newValue };
        element.Data.SetCtorArgValue(dynamicValue, index, element.Meta!.CtorArgs!.Length);
        element.OnCtorArgValueChanged();
    }

    private static void DeleteCtorArgValue(DesignElement element, DynamicCtorArgMeta ctorArgMeta, State? editingValue)
    {
        SetCtorArgValue(element, ctorArgMeta, Array.IndexOf(element.Meta!.CtorArgs!, ctorArgMeta), null);
        //TODO: 考虑恢复默认值
        editingValue?.NotifyValueChanged();
    }

    #endregion

    #region ====PropertyValue====

    private static Widget GetPropertyValueEditor(Type valueType, DesignElement element,
        DynamicPropertyMeta propertyMeta, out State? editingValue)
    {
        //TODO:先判断是否指定编辑器

        //获取注册的编辑器信息
        var editorInfo = TryGetValueEditorByValueType(valueType);
        if (editorInfo == null)
        {
            //TODO:
            editingValue = null;
            return new Input("None");
        }

        editingValue = editorInfo.PropertyStateMaker(element, propertyMeta);
        return editorInfo.EditorMaker(editingValue);
    }

    private static object? GetPropertyValue(DesignElement element, DynamicPropertyMeta propertyMeta)
    {
        var exists = element.Data.TryGetPropertyValue(propertyMeta.Name, out var currentValue);
        if (exists)
        {
            if (currentValue!.Value.From != ValueSource.Const) throw new NotImplementedException();
            return currentValue.Value.Value;
        }

        return propertyMeta.Value.DefaultValue?.Value;
    }

    private static void SetPropertyValue(DesignElement element, DynamicPropertyMeta propertyMeta, object? newValue)
    {
        //属性编辑器设置的值不可能为null
        var dynamicValue = new DynamicValue { From = ValueSource.Const, Value = newValue };
        var propertyValue = element.Data.SetPropertyValue(propertyMeta.Name, dynamicValue);
        element.SetPropertyValue(propertyValue);
    }

    private static void DeletePropertyValue(DesignElement element, DynamicPropertyMeta propertyMeta,
        State? editingValue)
    {
        element.Data.RemovePropertyValue(propertyMeta.Name);
        element.RemovePropertyValue(propertyMeta.Name);

        editingValue?.NotifyValueChanged();
    }

    #endregion

    #region ====Widget Overrides====

    public override void VisitChildren(Func<Widget, bool> action)
    {
        if (action(_targetEditor)) return;
        if (_deleteButton != null && action(_deleteButton)) return;
        if (_bindButton != null) action(_bindButton);
    }

    public override void Layout(float availableWidth, float availableHeight)
    {
        var width = CacheAndCheckAssignWidth(availableWidth);
        var height = CacheAndCheckAssignHeight(availableHeight);

        var editorWidth = width - (_deleteButton == null ? 0 : _buttonSize.Value) -
                          (_bindButton == null ? 0 : _buttonSize.Value);
        _targetEditor.Layout(editorWidth, height);
        _targetEditor.SetPosition(0, 0);
        SetSize(width, _targetEditor.H);


        var right = width;
        if (_deleteButton != null)
        {
            _deleteButton.Layout(_buttonSize.Value, _buttonSize.Value);
            right -= _deleteButton.W;
            _deleteButton.SetPosition(right, (H - _deleteButton.H) / 2f);
        }

        if (_bindButton != null)
        {
            _bindButton.Layout(_buttonSize.Value, _buttonSize.Value);
            right -= _bindButton.W;
            _bindButton.SetPosition(right, (H - _bindButton.H) / 2f);
        }
    }

    #endregion
}