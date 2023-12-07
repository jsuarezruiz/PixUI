using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PixUI.Dynamic.Design;

public sealed partial class DesignController
{
    public DesignController()
    {
        StatesController.DataSource = new List<DynamicState>();
    }

    /// <summary>
    /// 设计画布缩放百分比
    /// </summary>
    public readonly State<int> Zoom = 100;

    public DesignCanvas DesignCanvas { get; set; } = null!;

    public DesignElement RootElement { get; internal set; } = null!;

    private DynamicBackground? _background;
    private Image? _cachedBgImage;

    public DynamicBackground? Background
    {
        get => _background;
        set
        {
            _background = value;
            _cachedBgImage?.Dispose();
            _cachedBgImage = null;
            if (_background is { ImageData: not null })
            {
                _cachedBgImage = Image.FromEncodedData(_background.ImageData);
            }

            RootElement.Invalidate(InvalidAction.Repaint);
        }
    }

    internal Image? BackgroundImage => _cachedBgImage;

    /// <summary>
    /// 当前工具箱选择的项
    /// </summary>
    public DynamicWidgetMeta? CurrentToolboxItem { get; internal set; }

    /// <summary>
    /// 外部(非属性编辑器)改变布局属性值时通知属性面板附加的布局属性发生了变更
    /// </summary>
    internal Action<string>? NotifyLayoutPropertyChanged;

    /// <summary>
    /// 状态编辑面板改变了状态值时通知属性面板状态值发生了变更
    /// </summary>
    internal Action<DynamicState>? NotifyStateValueChanged;

    /// <summary>
    /// 需要刷新大纲视图的事件
    /// </summary>
    public event Action? OutlineChanged;

    internal void RaiseOutlineChanged() => OutlineChanged?.Invoke();

    /// <summary>
    /// 状态列表控制器
    /// </summary>
    internal readonly DataGridController<DynamicState> StatesController = new();

    public DynamicState? FindState(string name) =>
        StatesController.DataSource!.FirstOrDefault(s => s.Name == name);

    public List<DynamicState> FindStatesByValueType(DynamicStateType type, bool allowNull)
    {
        if (type == DynamicStateType.DataSet) throw new NotSupportedException();

        return StatesController.DataSource!
            .Where(s => s.Type == type && s.AllowNull == allowNull)
            .ToList();
    }

    public IEnumerable<DynamicState> GetAllDataSet()
    {
        if (StatesController.DataSource == null) yield break;
        foreach (var state in StatesController.DataSource)
        {
            if (state.Type == DynamicStateType.DataSet) yield return state;
        }
    }

    #region ====ContextMenu====

    internal void ShowContextMenu()
    {
        var list = new List<MenuItem>();
        list.Add(MenuItem.Item("Select Parent", MaterialIcons.SwipeUp, () => new SelectParentCommand().Run(this)));
        list.Add(MenuItem.Divider());
        list.Add(MenuItem.Item("Move Forward", MaterialIcons.MoveUp,
            () => new MoveChildCommand(MoveChildAction.Forward).Run(this)));
        list.Add(MenuItem.Item("Move Backward", MaterialIcons.MoveDown,
            () => new MoveChildCommand(MoveChildAction.Backward).Run(this)));
        ContextMenu.Show(list.ToArray());
    }

    #endregion

    #region ====DesignElement Selection====

    internal readonly List<DesignElement> Selection = new();

    public event Action? SelectionChanged;

    public DesignElement? FirstSelected => Selection.Count > 0 ? Selection[0] : null;

    internal void Select(DesignElement element)
    {
        if (Selection.Count == 1 && ReferenceEquals(Selection[0], element)) return;

        Selection.ForEach(o => o.IsSelected = false);
        Selection.Clear();

        Selection.Add(element);
        element.IsSelected = true;

        OnSelectionChanged();
    }

    internal void OnSelectionChanged() => SelectionChanged?.Invoke();

    #endregion
}