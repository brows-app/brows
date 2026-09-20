using NUnit.Framework;

namespace Brows.IPC;

[TestFixture]
public class ObjectTypeInfoTests {
    [Test]
    public void Constructor_SetsPropertiesCorrectly() {
        var info = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(info.TypeName, Is.EqualTo("TypeA"));
    }

    [Test]
    public void Constructor_SetsTypeSourceCorrectly() {
        var info = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(info.TypeSource, Is.EqualTo("AssemblyA"));
    }

    [Test]
    public void Constructor_SetsTypeVersionCorrectly() {
        var info = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(info.TypeVersion, Is.EqualTo("1.0.0.0"));
    }

    [Test]
    public void Equals_ReturnsTrue_ForIdenticalObjects() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_ReturnsFalse_ForDifferentTypeName() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeB", "AssemblyA", "1.0.0.0");
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_ReturnsFalse_ForDifferentTypeSource() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyB", "1.0.0.0");
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_ReturnsFalse_ForDifferentTypeVersion() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyA", "2.0.0.0");
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void Equals_ReturnsFalse_ForNull() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(a.Equals(null), Is.False);
    }

    [Test]
    public void Equals_ObjectOverride_ReturnsTrue_ForIdenticalObjects() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        object b = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_ObjectOverride_ReturnsFalse_ForDifferentType() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        object b = "not an ObjectTypeInfo";
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void GetHashCode_SameForEqualObjects() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void GetHashCode_DifferentForDifferentObjects1() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeB", "AssemblyA", "1.0.0.0");
        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void GetHashCode_DifferentForDifferentObjects2() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyB", "1.0.0.0");
        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void GetHashCode_DifferentForDifferentObjects3() {
        var a = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.0");
        var b = new ObjectTypeInfo("TypeA", "AssemblyA", "1.0.0.1");
        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    class SomeType { }

    [Test]
    public void From_Type_ReturnsExpectedResult() {
        var actual = ObjectTypeInfo.From(typeof(SomeType));
        var expected = new ObjectTypeInfo(
            typeName: typeof(SomeType).Name,
            typeSource: typeof(SomeType).Assembly.GetName().Name,
            typeVersion: typeof(SomeType).Assembly.GetName().Version.ToString());
        Assert.That(actual, Is.EqualTo(expected));
    }
}