using System;
using System.Collections.Generic;

namespace PixUI;

/// <summary>
/// Widget重新布局后向上所影响的Widget及区域
/// </summary>
public sealed class AffectsByRelayout
{
    internal static readonly AffectsByRelayout Default = new();

    public Widget Widget = null!;
    public float OldX = 0;
    public float OldY = 0;
    public float OldW = 0;
    public float OldH = 0;

    private AffectsByRelayout() { }

    /// <summary>
    /// 计算受影响的Widget的dirty area(新旧的union), 注意相对于上级
    /// </summary>
    public IDirtyArea GetDirtyArea()
    {
        //TODO: 考虑Root返回null或现有Bounds

        var cx = 0f;
        var cy = 0f;

        if (Widget.Parent is IScrollable scrollable) //判断上级是否IScrollable,是则处理偏移量
        {
            cx = scrollable.ScrollOffsetX;
            cy = scrollable.ScrollOffsetY;
        }

        return new RepaintArea(
            new Rect(Math.Min(OldX, Widget.X) - cx,
                Math.Min(OldY, Widget.Y) - cy,
                Math.Max(OldX + OldW, Widget.X + Widget.W),
                Math.Max(OldY + OldH, Widget.Y + Widget.H))
        );
    }
}

public enum InvalidAction : byte
{
    Repaint,
    Relayout,
}

internal sealed class InvalidWidget
{
    internal Widget Widget = null!;
    internal InvalidAction Action;
    internal int Level;
    internal bool RelayoutOnly = false;

    /// <summary>
    /// 用于局部重绘的对象,null表示全部重绘
    /// </summary>
    internal IDirtyArea? Area;

    internal InvalidWidget() { } //Need for web now, TODO:use TSRecordAttribute
}

/// <summary>
/// Dirty widget queue, One UIWindow has two queue.
/// </summary>
internal sealed class InvalidQueue
{
    #region ====Static====

    /// <summary>
    /// Add invalid widget to queue
    /// 只允许UI thread 添加，动画控制器只向ui thread递交修改状态请求
    /// </summary>
    /// <returns>false=widget is not mounted and can't add to queue</returns>
    internal static bool Add(Widget widget, InvalidAction action, IDirtyArea? item)
    {
        //暂在这里判断Widget是否已挂载
        if (!widget.IsMounted) return false;

        //根据Widget所在的画布加入相应的队列
        var root = widget.Root!;
        if (root is Overlay)
        {
            //When used for overlay, only Relayout invalid add to queue.
            if (action == InvalidAction.Relayout)
                root.Window.OverlayInvalidQueue.AddInternal(widget, action, item);
        }
        else
        {
            root.Window.WidgetsInvalidQueue.AddInternal(widget, action, item);
        }

        if (!root.Window.HasPostInvalidateEvent)
        {
            root.Window.HasPostInvalidateEvent = true;
            UIApplication.Current.PostInvalidateEvent();
        }

        return true;
    }

    #endregion

    private readonly List<InvalidWidget> _queue = new List<InvalidWidget>(32);

    internal bool IsEmpty => _queue.Count == 0;

    /// <summary>
    /// Add dirty widget to queue.
    /// </summary>
    /// <returns>true=the first item added to queue</returns>
    private void AddInternal(Widget widget, InvalidAction action, IDirtyArea? item)
    {
        //先尝试合并入现有项
        var level = GetLevelToTop(widget);
        var insertPos = 0; // -1 mean has merged to exist.
        var relayoutOnly = false;

        foreach (var exist in _queue)
        {
            if (exist.Level > level)
            {
                //TODO:判断新项是否现存项的任意上级，是则尝试合并
                break;
            }

            // check is same widget
            if (ReferenceEquals(exist.Widget, widget))
            {
                if (exist.Action < action)
                    exist.Action = action;
                if (exist.Action == InvalidAction.Repaint && action == InvalidAction.Repaint)
                {
                    if (item == null)
                        exist.Area = null;
                    exist.Area?.Merge(item);
                }

                insertPos = -1;
                break;
            }

            // check is any parent of current
            if (exist.Widget.IsAnyParentOf(widget))
            {
                if (exist.Action == InvalidAction.Relayout ||
                    (exist.Action == InvalidAction.Repaint && action == InvalidAction.Repaint))
                {
                    insertPos = -1;
                    break;
                }

                //上级要求重绘，子级要求重新布局的情况，尽可能标记当前项为RelayoutOnly
                relayoutOnly = true;
                exist.Area = null; //TODO:合并脏区域
            }

            insertPos++;
        }

        if (insertPos < 0) return;

        //在同一上级的子级内排序,eg: 同一Stack内的两个widget同时需要刷新，但要控制重绘顺序
        if (widget.Parent != null)
        {
            for (var i = insertPos - 1; i >= 0; i--)
            {
                var exist = _queue[i];
                if (exist.Level < level) break;
                //same level now, check parent is same
                if (!ReferenceEquals(exist.Widget.Parent, widget.Parent)) continue;
                //compare index of same parent
                var existIndex = widget.Parent.IndexOfChild(exist.Widget);
                var curIndex = widget.Parent.IndexOfChild(widget);
                if (curIndex > existIndex) break;
                insertPos = i;
            }
        }

        // insert to invalid queue.
        //TODO:use object pool for InvalidWidget
        var target = new InvalidWidget
        {
            Widget = widget,
            Action = action,
            Level = level,
            Area = item,
            RelayoutOnly = relayoutOnly
        };
        _queue.Insert(insertPos, target);
    }

    private static int GetLevelToTop(Widget widget)
    {
        var level = 0;
        Widget cur = widget;
        while (cur.Parent != null)
        {
            level++;
            cur = cur.Parent;
        }

        return level;
    }

    /// <summary>
    /// Only for Widgets Tree
    /// </summary>
    internal void RenderFrame(PaintContext context)
    {
        var hasRelayout = false;

        foreach (var item in _queue)
        {
            //Maybe removed from widget tree after added to InvalidQueue, so check again
            if (!item.Widget.IsMounted) continue;

            if (item.Action == InvalidAction.Relayout)
            {
                hasRelayout = true;
                var affects = AffectsByRelayout.Default;
                RelayoutWidget(item.Widget, affects);
                if (!item.RelayoutOnly)
                {
                    //注意: 以下重绘的是受影响Widget的上级，除非本身是根节点
                    RepaintWidget(context, affects.Widget.Parent ?? affects.Widget, affects.GetDirtyArea());
                }
            }
            else
            {
                RepaintWidget(context, item.Widget, item.Area);
            }
        }

        // clear items
        _queue.Clear();

        // 通知重新进行HitTest TODO:确认布局影响，eg:Input重布局没有改变大小，则不需要重新HitTest
        if (hasRelayout)
            context.Window.RunNewHitTest();
    }

    /// <summary>
    /// Only for overlay
    /// </summary>
    internal void RelayoutAll()
    {
        foreach (var item in _queue)
        {
            if (item.Action == InvalidAction.Relayout)
            {
                var affects = AffectsByRelayout.Default;
                RelayoutWidget(item.Widget, affects);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        // clear items
        _queue.Clear();
    }

    private static void RelayoutWidget(Widget widget, AffectsByRelayout affects)
    {
        //先初始化受影响的Widget(必须因为可能重新布局后没有改变大小)
        affects.Widget = widget;
        affects.OldX = widget.X;
        affects.OldY = widget.Y;
        affects.OldW = widget.W;
        affects.OldH = widget.H;

        //再重新布局并尝试通知上级
        widget.Layout(widget.CachedAvailableWidth, widget.CachedAvailableHeight);
        widget.TryNotifyParentIfSizeChanged(affects.OldW, affects.OldH, affects);
    }

    private static void RepaintWidget(PaintContext ctx, Widget widget, IDirtyArea? dirtyArea)
    {
        var canvas = ctx.Canvas;
        //向上循环至Root，并找到需要开始绘制的Opaque组件
        var widgetToRoot = new List<Widget>();
        Widget? opaque = null;
        Widget temp = widget;
        while (true)
        {
            widgetToRoot.Add(temp);

            //查找开始绘制的Widget
            if (opaque == null)
            {
                if (temp.IsOpaque)
                    opaque = temp;
            }
            else
            {
                //TODO: 判断temp是否Transform or Opacity or ImageFilter等，是则重设opaque
            }

            if (temp.Parent == null)
                break;
            temp = temp.Parent;
        }

        opaque ??= temp; //没找到指向RootWidget
        Log.Debug($"from:{opaque} to:{widget} dirty={dirtyArea}");

        //转换坐标并裁剪绘制区域
        var saveCount = canvas.Save();

        for (var i = widgetToRoot.Count - 1; i >= 0; i--)
        {
            temp = widgetToRoot[i];
            if (i == 0)
            {
                var dirtyRect = dirtyArea?.GetRect() ?? Rect.FromLTWH(0, 0, temp.W, temp.H);
                temp.BeforePaint(canvas, false, dirtyRect);
                if (canvas.IsClipEmpty)
                {
                    Log.Debug("重绘裁剪区域为空");
                    canvas.RestoreToCount(saveCount);
                    return;
                }
            }
            else
            {
                temp.BeforePaint(canvas);
            }
        }

        //恢复坐标转换开始绘制
        var factor = widget.Root!.Window.ScaleFactor;
        var matrix = Matrix4.CreateScale(factor, factor, 1);
        canvas.SetMatrix(matrix);

        for (var i = widgetToRoot.Count - 1; i >= 0; i--)
        {
            temp = widgetToRoot[i];
            temp.BeforePaint(canvas, true);
            if (ReferenceEquals(temp, opaque))
            {
                opaque.Paint(canvas, ReferenceEquals(opaque, widget)
                    ? dirtyArea
                    : new RepaintChild(opaque, widget, dirtyArea));
                break;
            }
        }

        canvas.RestoreToCount(saveCount);
    }
}