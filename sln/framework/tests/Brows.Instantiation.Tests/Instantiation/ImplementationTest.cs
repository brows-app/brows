using NUnit.Framework;
using System.Reflection;

namespace Brows.Instantiation;

[TestFixture(TestOf = typeof(Implementation<>))]
internal sealed class ImplementationTest {
    private abstract class TypeBase { }
    private class TypeA : TypeBase { }
    private class TypeB : TypeBase { }
    private class TypeC : TypeA { }

    [Test]
    public void From_FindsTypesThatAreNotAbstract() {
        var actual = Implementation<TypeBase>.From(Assembly.GetExecutingAssembly());
        var expected = new[] { typeof(TypeA), typeof(TypeB), typeof(TypeC) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void List_IncludesTypesThatAreNotAbstract() {
        var actual = new Implementation<TypeBase>(Assembly.GetExecutingAssembly()).List;
        var expected = new[] { typeof(TypeA), typeof(TypeB), typeof(TypeC) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void From_IncludesBaseType() {
        var actual = Implementation<TypeA>.From(Assembly.GetExecutingAssembly());
        var expected = new[] { typeof(TypeA), typeof(TypeC) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void List_IncludesBaseType() {
        var actual = new Implementation<TypeA>(Assembly.GetExecutingAssembly()).List;
        var expected = new[] { typeof(TypeA), typeof(TypeC) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void From_AppliesPredicate() {
        var actual = Implementation<TypeBase>.From([Assembly.GetExecutingAssembly()], type => type != typeof(TypeC));
        var expected = new[] { typeof(TypeA), typeof(TypeB) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }

    [Test]
    public void List_DoesNotIncludeTypesFilteredByPredicate() {
        var actual = new Implementation<TypeA>([Assembly.GetExecutingAssembly()], type => type != typeof(TypeA)).List;
        var expected = new[] { typeof(TypeC) };
        Assert.That(actual, Is.EquivalentTo(expected));
    }
}
