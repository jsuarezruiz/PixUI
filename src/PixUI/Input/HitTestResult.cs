using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PixUI;

public sealed class HitTestResult
{
    private readonly List<HitTestEntry> _path = new List<HitTestEntry>();

    /// <summary>
    /// 仅用于缓存最后命中的Widget相对于窗体的变换,用于将窗体坐标映射为本地坐标
    /// </summary>
    private Matrix4 _transform = Matrix4.CreateIdentity();

    /// <summary>
    /// 仅用于缓存最后命中的Widget,不一定是MouseRegion
    /// </summary>
    internal Widget? LastHitWidget { get; private set; }

    public bool IsHitAnyMouseRegion => _path.Count > 0;

    public bool IsHitAnyWidget => LastHitWidget != null;

    /// <summary>
    /// 添加命中的Widget
    /// </summary>
    /// <returns>true = Widget is opaque MouseRegion</returns>
    public bool Add(Widget widget)
    {
        if (ReferenceEquals(LastHitWidget, widget))
            return false; //排除在旧区域中重新HitTest引起的重复加入

        LastHitWidget = widget;
        _transform.Translate(-widget.X, -widget.Y);
        if (widget.Parent is IScrollable scrollable && !scrollable.IgnoreScrollOffsetForHitTest)
        {
            _transform.Translate(scrollable.ScrollOffsetX, scrollable.ScrollOffsetY);
        }

        var isOpaqueMouseRegion = false;
        if (widget is IMouseRegion mouseRegion)
        {
            _path.Add(new HitTestEntry(mouseRegion, _transform));
            isOpaqueMouseRegion = mouseRegion.MouseRegion.Opaque;
        }

        return isOpaqueMouseRegion;
    }

    /// <summary>
    /// 仅用于[Transform] Widget命中子级后转换
    /// </summary>
    internal void ConcatLastTransform(in Matrix4 transform)
    {
        _transform.PreConcat(transform);
        if (ReferenceEquals(LastHitWidget, LastWidgetWithMouseRegion))
        {
            _path[_path.Count - 1] = new HitTestEntry(LastEntry!.Value.Widget, _transform);
        }
    }

    /// <summary>
    /// 滚动时判断是否超出已命中的范围，没有则更新Transform
    /// </summary>
    /// <returns>false=已失效，需要重新HitTest</returns>
    internal bool TranslateOnScroll(Widget scrollable, float dx, float dy, float winX, float winY)
    {
        //如果scrollable就是LastHitWidget，不需要处理
        if (ReferenceEquals(LastHitWidget, scrollable))
            return true;

        //滚动时必定没有超出scrollable的范围
        _transform.Translate(dx, dy);
        var transformed = MatrixUtils.TransformPoint(_transform, winX, winY);
        var contains = LastHitWidget!.ContainsPoint(transformed.Dx, transformed.Dy);
        if (contains)
        {
            //Translate路径内所有Scrollable的子级
            for (var i = _path.Count - 1; i >= 0; i--)
            {
                if (!scrollable.IsAnyParentOf((Widget)_path[i].Widget))
                    break;
                _path[i].Transform.Translate(dx, dy);
            }
        }

        return contains;
    }

    /// <summary>
    /// 最后命中的实现了MouseRegion的Widget
    /// </summary>
    public Widget? LastWidgetWithMouseRegion =>
        _path.Count == 0 ? null : (Widget)_path[_path.Count - 1].Widget;

    internal HitTestEntry? LastEntry => _path.Count == 0 ? null : _path[_path.Count - 1];

    /// <summary>
    /// 检测新坐标是否仍旧在最后一个命中的Widget区域内
    /// </summary>
    internal bool StillInLastRegion(float winX, float winY)
    {
        if (LastHitWidget == null) return false;

        var transformed = MatrixUtils.TransformPoint(_transform, winX, winY);
        var contains = LastHitWidget.ContainsPoint(transformed.Dx, transformed.Dy);
        if (!contains) return false;

        //如果上级是IScrollable，还需要判断是否已滚出上级区域
        var scrollableParent = LastHitWidget.Parent?.FindParent(w => w is IScrollable);
        if (scrollableParent == null) return true;

        var scrollableToWin = scrollableParent.LocalToWindow(0, 0);
        return scrollableParent.ContainsPoint(winX - scrollableToWin.X,
            winY - scrollableToWin.Y);
    }

    /// <summary>
    /// 在旧区域内重新HitTest
    /// </summary>
    internal void HitTestInLastRegion(float winX, float winY)
    {
        var transformed = MatrixUtils.TransformPoint(_transform, winX, winY);
        var isOpaqueMouseRegion = false;
        if (LastHitWidget is IMouseRegion mouseRegion)
            isOpaqueMouseRegion = mouseRegion.MouseRegion.Opaque;
        if (!isOpaqueMouseRegion)
            LastHitWidget!.HitTest(transformed.Dx, transformed.Dy, this);
    }

    internal void ExitAll()
    {
        for (var i = _path.Count - 1; i >= 0; i--)
        {
            _path[i].Widget.MouseRegion.RaiseHoverChanged(false);
        }
    }

    /// <summary>
    /// 与新的结果比较，激发旧的HoverChanged(false)事件
    /// </summary>
    internal void ExitOldRegion(HitTestResult newResult)
    {
        if (!IsHitAnyMouseRegion) return;

        var exitTo = -1; //从后往前退出的区域 eg: 1->2->3 变为 1, exitTo=1 
        for (var i = 0; i < _path.Count; i++)
        {
            exitTo = i;
            if (newResult._path.Count == i)
                break;
            if (!ReferenceEquals(_path[i].Widget, newResult._path[i].Widget))
                break;
            if (i == _path.Count - 1) return; //两者相等
        }

        for (var i = _path.Count - 1; i >= exitTo; i--)
        {
            _path[i].Widget.MouseRegion.RaiseHoverChanged(false);
        }

        //退出嵌套的MouseRegion的子级，需要恢复上级的Cursor
        if (exitTo > 0)
            _path[exitTo - 1].Widget.MouseRegion.RestoreHoverCursor();
    }

    /// <summary>
    /// 与旧的结果比较，激发新的HoverChanged(true)事件
    /// </summary>
    internal void EnterNewRegion(HitTestResult oldResult)
    {
        if (!IsHitAnyMouseRegion) return;

        var enterFrom = -1; //从前往后进入的区域 eg: 1 变为 1->2, enterFrom=1
        for (var i = 0; i < _path.Count; i++)
        {
            enterFrom = i;
            if (oldResult._path.Count == i)
                break;
            if (!ReferenceEquals(_path[i].Widget, oldResult._path[i].Widget))
                break;
            if (i == _path.Count - 1) return; //两者相等
        }

        for (var i = enterFrom; i < _path.Count; i++)
        {
            _path[i].Widget.MouseRegion.RaiseHoverChanged(true);
        }
    }

    /// <summary>
    /// 向上(冒泡)传播PointerEvent
    /// </summary>
    internal void PropagatePointerEvent(PointerEvent e, Action<MouseRegion, PointerEvent> handler)
    {
        for (var i = _path.Count - 1; i >= 0; i--)
        {
            var transformed = MatrixUtils.TransformPoint(_path[i].Transform, e.X, e.Y);
            e.SetPoint(transformed.Dx, transformed.Dy);
            handler(_path[i].Widget.MouseRegion, e);
            if (e.IsHandled)
                return; //Stop propagate
        }
    }

    internal void Reset()
    {
        _path.Clear();
        LastHitWidget = null;
        _transform = Matrix4.CreateIdentity();
    }

    internal void CopyFrom(HitTestResult other)
    {
        _path.Clear();
        _path.AddRange(other._path);
        LastHitWidget = other.LastHitWidget;
        _transform = other._transform;
    }
}

internal readonly struct HitTestEntry
{
    internal readonly IMouseRegion Widget;
    internal readonly Matrix4 Transform;

    internal HitTestEntry(IMouseRegion widget, Matrix4 transform)
    {
        Widget = widget;
        Transform = transform;
    }

    internal bool ContainsPoint(float winX, float winY)
    {
        var transformedPosition = MatrixUtils.TransformPoint(Transform, winX, winY);
        return ((Widget)Widget).ContainsPoint(transformedPosition.Dx, transformedPosition.Dy);
    }

#if __WEB__
    public HitTestEntry Clone() => new(Widget, Transform);
#endif
}