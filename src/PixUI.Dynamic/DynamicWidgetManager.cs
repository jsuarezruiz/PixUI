using System;
using System.Collections.Generic;
using System.Linq;

namespace PixUI.Dynamic;

/// <summary>
/// 管理设计时与运行时的DynamicWidget
/// </summary>
public static partial class DynamicWidgetManager
{
    static DynamicWidgetManager()
    {
        Register(MakeButtonMeta());
        Register(MakeStackMeta());
        Register(MakePositionedMeta());
        Register(MakeCenterMeta());
    }

    private static readonly Dictionary<string, DynamicWidgetMeta> _dynamicWidgets = new();

    public static void Register(DynamicWidgetMeta dynamicWidgetMeta, bool replaceExists = false)
    {
        if (_dynamicWidgets.ContainsKey(dynamicWidgetMeta.Name) && !replaceExists)
            throw new Exception("Already exists.");

        _dynamicWidgets[dynamicWidgetMeta.Name] = dynamicWidgetMeta;
    }

    public static void Register<T>(IconData icon,
        string? catalog = null,
        string? name = null,
        DynamicPropertyMeta[]? properties = null,
        DynamicEventMeta[]? events = null,
        ContainerSlot[]? slots = null, bool replaceExists = false)
        where T : Widget, new()
    {
        Register(DynamicWidgetMeta.Make<T>(icon, catalog, name, properties, events, slots), replaceExists);
    }

    public static IList<DynamicWidgetMeta> GetAll() => _dynamicWidgets.Values.ToList();

    public static DynamicWidgetMeta GetByName(string name) => _dynamicWidgets[name]!;
}