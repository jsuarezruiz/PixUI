using System;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq.Expressions;
using NUnit.Framework;

namespace PixUI.UnitTests;

public class Tests
{
    [SetUp]
    public void Setup() { }

    [Test]
    public void Test1()
    {
        float a = 1;
        float b = float.PositiveInfinity;
        Assert.True(a < b);
        Assert.True(float.IsPositiveInfinity(b - 1));

        float n = float.NaN;
        n += 1.0f;
        Assert.True(float.IsNaN(n));

        Assert.Pass();
    }

    [Test]
    public void DynamicTest()
    {
        // dynamic obj = new Person();
        // obj.Name = "Rick";
        // //obj["Name"] = "Rick";
        // obj.SayHello();

        // var s = new S<int?>();

        // var v = new V();
        // v.AA += 1;
        // Console.WriteLine(v.AA);

        State<int> a = 2;
        State<int?> b = a.ToNullable();
    }
}

internal class Person //: IDynamicMetaObjectProvider
{
    public string Name { get; set; }

    public void SayHello() => Console.WriteLine($"Hello {Name}");

    public DynamicMetaObject GetMetaObject(Expression parameter)
    {
        throw new NotImplementedException();
    }
}

// internal abstract class S<T> where T : notnull
// {
//     public abstract T? Value { get; set; }
//
//     public bool HasValue => Value != null;
//
//     // public static implicit operator S<T>(T value)
//     // {
//     //     return new S<T>();
//     // }
// }
//
// internal class S1<T> : S<T?> where T : struct
// {
//     //public override T? Value { get; set; }
//     public override T? Value { get; set; }
// }
//
// // internal class S2<T> : S<T> where T : class
// // {
// //     public override T? Value { get; set; }
// // }
