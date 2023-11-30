using System;
using System.Collections.Generic;
using System.Linq;

namespace PixUI;

internal sealed class DataGridFooter<T> : Widget
{
    public DataGridFooter(DataGridController<T> controller)
    {
        _controller = controller;
    }

    private readonly DataGridController<T> _controller;

    public DataGridFooterCell[] Cells { get; set; } = null!;

    public override void Layout(float availableWidth, float availableHeight)
    {
        SetSize(availableWidth, _controller.Theme.RowHeight /*TODO:暂设为行高*/);
    }

    public override void Paint(Canvas canvas, IDirtyArea? area = null)
    {
        //TODO:判断定义列范围重叠或超出范围或简单捕获异常

        var visibleColumns = _controller.CachedVisibleColumns;
        var leafColumns = _controller.CachedLeafColumns;
        var rowHeight = _controller.Theme.RowHeight;
        var defaultCellStyle = _controller.Theme.DefaultHeaderCellStyle; /*TODO:暂使用默认的HeaderStyle*/

        var cellRect = new Rect(0, 0, 0, rowHeight);
        for (var i = 0; i < visibleColumns.Count; i++)
        {
            var visibleColumn = visibleColumns[i];
            var visibleColumnIndex = leafColumns.IndexOf(visibleColumn);

            //判断可见列是否在定义的单元格范围内
            cellRect.Left = visibleColumn.CachedVisibleLeft;
            var inCell = Cells.FirstOrDefault(cell => cell.Contains(visibleColumnIndex));
            CellStyle cellStyle;
            string? content = null;
            if (inCell == null)
            {
                cellRect.Right = visibleColumn.CachedVisibleRight;
                cellStyle = defaultCellStyle;
            }
            else
            {
                var span = inCell.EndColumnIndex - visibleColumnIndex;
                var endVisibleColumn = visibleColumn;
                var sep = 0;
                for (var j = 1; j <= span; j++)
                {
                    var leaf = leafColumns[visibleColumnIndex + j];
                    var visible = visibleColumns[i + j];
                    if (ReferenceEquals(leaf, visible))
                    {
                        endVisibleColumn = leaf;
                        sep++;
                    }
                    else break;
                }

                cellRect.Right = endVisibleColumn.CachedVisibleRight;
                cellStyle = inCell.CellStyle ?? defaultCellStyle;
                content = GetCellContent(inCell);
                i += sep;
            }

            //画单元格
            PaintCell(canvas, cellRect, cellStyle, content);
        }
    }

    private string? GetCellContent(DataGridFooterCell cell)
    {
        if (cell.Content == DataGridFooterCell.ContentType.Text)
            return cell.Text;

        if (cell.Content == DataGridFooterCell.ContentType.Custom)
            return cell.CustomBuilder?.Invoke();

        // if (cell.Content is DataGridFooterCell.ContentType.Sum or DataGridFooterCell.ContentType.Avg)
        // {
        //     
        // }
        throw new NotImplementedException();
    }

    private void PaintCell(Canvas canvas, in Rect cellRect, CellStyle style, string? content)
    {
        if (style.BackgroundColor.HasValue)
        {
            var paint = PaintUtils.Shared(style.BackgroundColor.Value);
            canvas.DrawRect(cellRect, paint);
        }

        _controller.PaintCellBorder(canvas, cellRect);

        if (string.IsNullOrEmpty(content)) return;

        canvas.Save();
        canvas.ClipRect(cellRect, ClipOp.Intersect, false);
        //TODO:暂使用可见框计算位置
        using var ph = TextPainter.BuildParagraph(content, cellRect.Width, style.FontSize, style.Color ?? Colors.Black);
        var ox = style.HorizontalAlignment switch
        {
            HorizontalAlignment.Center => (cellRect.Width - ph.MaxIntrinsicWidth) / 2f,
            HorizontalAlignment.Right => cellRect.Width - ph.MaxIntrinsicWidth - CellStyle.CellPadding,
            _ => CellStyle.CellPadding
        };
        var oy = style.VerticalAlignment switch
        {
            VerticalAlignment.Middle => (cellRect.Height - ph.Height) / 2f,
            VerticalAlignment.Bottom => cellRect.Height - ph.Height - CellStyle.CellPadding,
            _ => CellStyle.CellPadding
        };
        canvas.DrawParagraph(ph, ox + cellRect.Left, oy);
        canvas.Restore();
    }
}