using System;
using System.Collections.ObjectModel;

namespace PixUI;

public sealed class DataGridColumns<T> : Collection<DataGridColumn<T>>
{
    internal DataGridColumns(DataGridController<T> controller)
    {
        _controller = controller;
    }

    private readonly DataGridController<T> _controller;

    internal int HeaderRows { get; private set; } = 1;

    protected override void ClearItems()
    {
        base.ClearItems();
        
        CalcHeaderRows();
        _controller.ClearLeafColumns();
        _controller.CheckHasFrozen();
        _controller.OnColumnsChanged();
    }

    protected override void InsertItem(int index, DataGridColumn<T> item)
    {
        base.InsertItem(index, item);

        CalcHeaderRows();
        _controller.GetLeafColumns(item, null);
        _controller.CheckHasFrozen();
        _controller.OnColumnsChanged();
    }

    protected override void RemoveItem(int index)
    {
        var item = this[index];
        base.RemoveItem(index);

        CalcHeaderRows();
        _controller.RemoveLeafColumns(item);
        _controller.CheckHasFrozen();
        _controller.OnColumnsChanged();
    }

    protected override void SetItem(int index, DataGridColumn<T> item)
    {
        var oldItem = this[index];
        base.SetItem(index, item);

        CalcHeaderRows();
        _controller.RemoveLeafColumns(oldItem);
        _controller.GetLeafColumns(item, null);
        _controller.CheckHasFrozen();
        _controller.OnColumnsChanged();
    }

    private void CalcHeaderRows()
    {
        if (Count == 0)
        {
            HeaderRows = 1;
            return;
        }

        foreach (var col in this)
        {
            CalcHearderRowsLoop(col, 1);
        }
    }

    private void CalcHearderRowsLoop(DataGridColumn<T> column, int rows)
    {
        if (column is not DataGridGroupColumn<T> groupColumn) return;
        
        HeaderRows = Math.Max(HeaderRows, rows + 1);
        foreach (var child in groupColumn.Children)
        {
            CalcHearderRowsLoop(child, rows + 1);
        }
    }
}