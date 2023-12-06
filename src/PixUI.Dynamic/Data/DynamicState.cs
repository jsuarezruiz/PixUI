using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PixUI.Dynamic;

public interface IDynamicState
{
    void WriteTo(Utf8JsonWriter writer);
}

public interface IDynamicValueState : IDynamicState
{
    void ReadFrom(ref Utf8JsonReader reader, DynamicState state);

    State GetRuntimeValue(DynamicState state);
}

public interface IDynamicDataSetState : IDynamicState
{
    void ReadFrom(ref Utf8JsonReader reader);

    ValueTask<object?> GetRuntimeDataSet();
}

public enum DynamicStateType
{
    DataSet,
    Int,
    String,
    DateTime,
}

public sealed class DynamicState
{
    public string Name { get; set; } = null!;
    public DynamicStateType Type { get; set; }
    public IDynamicState? Value { get; set; }
    public bool AllowNull { get; set; }

    /// <summary>
    /// 根据状态类型(DynamicStateType)获取相应的反射值类型(Type)
    /// </summary>
    public Type GetValueStateValueType()
    {
        if (Type == DynamicStateType.DataSet)
            throw new NotSupportedException("Only for value state");

        var valueType = Type switch
        {
            DynamicStateType.Int => typeof(int),
            DynamicStateType.String => typeof(string),
            DynamicStateType.DateTime => typeof(DateTime),
            _ => throw new NotImplementedException()
        };
        if (AllowNull && Type != DynamicStateType.String)
            valueType = typeof(Nullable<>).MakeGenericType(valueType);
        return valueType;
    }

    /// <summary>
    /// 根据属性值类型获取相应的DynamicStateType
    /// </summary>
    public static DynamicStateType GetStateTypeByValueType(DynamicPropertyMeta propertyMeta, out bool allowNull)
    {
        var valueType = propertyMeta.ValueType;
        allowNull = false;
        if (propertyMeta.IsNullableValueType)
        {
            valueType = valueType.GenericTypeArguments[0];
            allowNull = true;
        }

        DynamicStateType stateType;
        if (valueType == typeof(string))
            stateType = DynamicStateType.String;
        else if (valueType == typeof(int))
            stateType = DynamicStateType.Int;
        else if (valueType == typeof(DateTime))
            stateType = DynamicStateType.DateTime;
        else
            throw new NotImplementedException();

        return stateType;
    }
}