using System;

namespace PixUI;

public abstract class UIApplication
{
    protected UIWindow MainWindow = null!; //目前仅支持单一Window

    public static UIApplication Current { get; protected set; } = null!;

    public virtual bool IsWasm => false;

    /// <summary>
    /// Post invalidate event to main loop, maybe called by none UI thread
    /// </summary>
    public abstract void PostInvalidateEvent();

    /// <summary>
    /// Post action on UI thread
    /// </summary>
    public abstract void BeginInvoke(Action action);

    /// <summary>
    /// 处理main loop内收到的InvalidateEvent
    /// </summary>
    protected void OnInvalidateRequest()
    {
        var window = MainWindow; //TODO:根据事件判断哪个UIWindow
        var widgetsCanvas = window.GetOffscreenCanvas();
        var overlayCanvas = window.GetOnscreenCanvas();

        var ctx = PaintContext.Default;
        ctx.Window = window;
        var beginTime = DateTime.UtcNow;

        //先绘制WidgetsCanvas
        if (!window.WidgetsInvalidQueue.IsEmpty)
        {
            ctx.Canvas = widgetsCanvas;
            window.WidgetsInvalidQueue.RenderFrame(ctx);
            if (IsWasm)
                widgetsCanvas.Surface!.Flush();
#if __WEB__
            window.FlushOffscreenSurface();
#endif
        }

        //重新布局OverlayCanvas
        if (!window.OverlayInvalidQueue.IsEmpty)
        {
            ctx.Canvas = overlayCanvas;
            window.OverlayInvalidQueue.RelayoutAll();
        }

#if !__WEB__
        widgetsCanvas.Surface!.Draw(overlayCanvas, 0, 0, null);
        if (window.ScaleFactor != 1)
            overlayCanvas.Scale(window.ScaleFactor, window.ScaleFactor);
        window.Overlay.Paint(overlayCanvas); //always repaint
        if (window.ScaleFactor != 1)
            overlayCanvas.ResetMatrix();
#else
            window.DrawOffscreenSurface();
            if (window.ScaleFactor != 1)
            {
                overlayCanvas.Save();
                overlayCanvas.Scale(window.ScaleFactor, window.ScaleFactor);
            }
            window.Overlay.Paint(overlayCanvas); //always repaint
            if (window.ScaleFactor != 1)
                overlayCanvas.Restore();
#endif

        window.HasPostInvalidateEvent = false;

        var duration = DateTime.UtcNow - beginTime;
        Console.WriteLine($"Draw frame: {duration.TotalMilliseconds}ms");

        window.Present();
    }
}

public sealed class PaintContext //TODO: remove this
{
    internal static readonly PaintContext Default = new();

    private PaintContext() { }

    public UIWindow Window { get; internal set; } = null!;

    public Canvas Canvas { get; internal set; } = null!;
}