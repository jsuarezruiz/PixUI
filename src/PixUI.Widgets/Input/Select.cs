using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace PixUI;

/// <summary>
/// 弹出选择列表，仅支持单选
/// </summary>
public abstract class SelectBase<T> : InputBase<Widget>
{
    protected SelectBase()
    {
        SuffixWidget = new ExpandIcon(new FloatTween(0, 0.5f).Animate(_expandAnimation));
    }

    [SetsRequiredMembers]
    protected SelectBase(State<T?> value) : this()
    {
        Value = value;
    }

    private readonly State<T?> _selectedValue = null!;
    private readonly ListPopupItemBuilder<T>? _optionBuilder;
    private readonly OptionalAnimationController _expandAnimation = new();
    private ListPopup<T>? _listPopup;
    private bool _showing;
    private Func<T, string>? _labelGetter;

    public bool Filterable { get; init; }

    public required State<T?> Value
    {
        init
        {
            _selectedValue = value;
            Editor = Filterable
                ? new EditableText(value.ToStateOfString())
                : new SelectText(value.ToStateOfString());
            if (Editor is IMouseRegion mouseRegion)
                mouseRegion.MouseRegion.PointerTap += OnEditorTap;
            if (Editor is IFocusable focusable)
                focusable.FocusNode.FocusChanged += OnFocusChanged;
        }
    }

    public T[] Options { get; set; } = Array.Empty<T>();

    public Task<T[]> OptionsAsyncGetter
    {
        set => GetOptionsAsync(value);
    }

    public Func<T, string> LabelGetter
    {
        set => _labelGetter = value;
    }

    public override State<bool>? Readonly
    {
        get
        {
            if (Editor is EditableText editableText) return editableText.Readonly;
            return ((SelectText)Editor).Readonly;
        }
        set
        {
            if (Editor is EditableText editableText) editableText.Readonly = value;
            else ((SelectText)Editor).Readonly = value;
        }
    }

    private void OnFocusChanged(FocusChangedEvent e)
    {
        if (!e.IsFocused)
            HidePopup();
    }

    private void OnEditorTap(PointerEvent e)
    {
        if (_showing) HidePopup();
        else ShowPopup();
    }

    private void ShowPopup()
    {
        if (_showing || Options.Length == 0) return;
        _showing = true;

        var optionBuilder =
            _optionBuilder ??
            ((data, index, isHover, isSelected) =>
            {
                var color = RxComputed<Color>.Make(isSelected, v => v ? Colors.White : Colors.Black);
                return new Text(_labelGetter != null ? _labelGetter(data) : data?.ToString() ?? "")
                    { TextColor = color };
            });
        _listPopup = new ListPopup<T>(Overlay!, optionBuilder, W + 8, Theme.DefaultFontSize + 8);
        _listPopup.DataSource = new List<T>(Options);
        //初始化选中的
        if (_selectedValue.Value != null)
            _listPopup.InitSelect(_selectedValue.Value!);
        _listPopup.OnSelectionChanged = OnSelectionChanged;
        _listPopup.Show(this, new Offset(-4, 0), Popup.DefaultTransitionBuilder);
        _expandAnimation.Parent = _listPopup.AnimationController;
    }

    private void HidePopup()
    {
        if (!_showing) return;
        _showing = false;

        _listPopup?.Hide();
        _listPopup = null;
    }

    private async void GetOptionsAsync(Task<T[]> builder)
    {
        Options = await builder;
    }

    private void OnSelectionChanged(T? data)
    {
        _showing = false;
        _listPopup = null;
        _selectedValue.Value = data;
    }
}

public sealed class Select<T> : SelectBase<T>
{
    public Select() { }

    [SetsRequiredMembers]
    public Select(State<T?> value) : base(value) { }
}

public sealed class EnumSelect<T> : SelectBase<T> where T : struct, Enum
{
    public EnumSelect() { }

    [SetsRequiredMembers]
    public EnumSelect(State<T> value) : base(value)
    {
        //TODO:use DisplayNameAttribute to build DisplayText
        Options = Enum.GetValues<T>();
    }
}

internal sealed class SelectText : TextBase, IMouseRegion, IFocusable
{
    public SelectText(State<string> text) : base(text)
    {
        MouseRegion = new MouseRegion();
        FocusNode = new FocusNode();
    }

    public MouseRegion MouseRegion { get; }
    public FocusNode FocusNode { get; }

    private State<bool>? _readonly;

    public State<bool>? Readonly
    {
        get => _readonly;
        set => _readonly = Bind(_readonly, value, RepaintOnStateChanged);
    }

    protected override bool ForceHeight => true;

    public override void Layout(float availableWidth, float availableHeight)
    {
        var maxSize = CacheAndGetMaxSize(availableWidth, availableHeight);

        BuildParagraph(Text.Value, maxSize.Width);

        var fontHeight = (FontSize?.Value ?? Theme.DefaultFontSize) + 4;
        SetSize(maxSize.Width, Math.Min(maxSize.Height, fontHeight));
    }

    public override void Paint(Canvas canvas, IDirtyArea? area = null)
    {
        if (Text.Value.Length == 0) return;
        canvas.DrawParagraph(CachedParagraph!, 0, 2 /*offset*/);
    }
}