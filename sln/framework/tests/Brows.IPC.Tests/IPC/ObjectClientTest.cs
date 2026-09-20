using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows.IPC;

[TestFixture]
internal class ObjectClientTest {
    class Pet(string color) {
        public string Color { get; } = color;
        public int Age { get; set; }
    }

    class Dog(DogBreed breed, string color) : Pet(color) {
        public DogBreed Breed { get; } = breed;
    }

    enum DogBreed {
        Pointer,
        Retriever
    }

    private readonly IObjectTypeResolver TypeResolver =
        ObjectTypeResolver.Lookup(new Dictionary<ObjectTypeInfo, Type>
        {
            { ObjectTypeInfo.From(typeof(Pet)), typeof(Pet) },
            { ObjectTypeInfo.From(typeof(Dog)), typeof(Dog) },
        });

    [TestCase(nameof(Dog.Color), "white")]
    [TestCase(nameof(Dog.Breed), DogBreed.Pointer)]
    public async Task ReadObject_ReadsObjectProperty(string propertyName, object expected) {
        var json = @"{""v"":1,""j"":{""t"":""Dog"",""s"":""Brows.IPC.Tests"",""v"":""67.78.89.90"",""o"":{ ""Color"": ""white"", ""breed"": 0 }}}";
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        using (stream) {
            var subject = ObjectClient.From(stream, TypeResolver);
            using (subject) {
                var @object = await subject.Read();
                var actual = @object.GetType().GetProperty(propertyName).GetValue(@object);
                Assert.That(actual, Is.EqualTo(expected));
            }
        }
    }

    [Test]
    public async Task WriteObject_SerializesObject() {
        var stream = new MemoryStream(capacity: 2048);
        using (stream) {
            var subject = ObjectClient.From(stream, TypeResolver);
            using (subject) {
                await subject.Write(new Dog(DogBreed.Retriever, "Black") { Age = 2 }, default);
            }
            var json = Encoding.UTF8.GetString(stream.ToArray());
            var expected = @"{""v"":1,""j"":{""t"":""Dog"",""s"":""Brows.IPC.Tests"",""v"":""67.78.89.90"",""o"":{""Breed"":1,""Color"":""Black"",""Age"":2}}}" + '\n';
            Assert.That(json, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task ReadObject_ReturnsObjectWritten() {
        var expected = new Dog(DogBreed.Pointer, "copper") { Age = 11 };
        using var write = new MemoryStream(4096);
        using var writer = ObjectClient.From(write, TypeResolver);
        await writer.Write(expected, default);
        using var read = new MemoryStream(write.ToArray());
        using var reader = ObjectClient.From(read, TypeResolver);
        var actual = await reader.Read();
        var properties = expected.GetType().GetProperties();
        var expectedValues = properties.Select(p => p.GetValue(expected)).ToList();
        var actualValues = properties.Select(p => p.GetValue(actual)).ToList();
        Assert.That(actualValues, Is.EqualTo(expectedValues));
    }

    [Test]
    public async Task ReadObject_ThrowsException() {
        using var write = new MemoryStream(4096);
        using var writer = ObjectClient.From(write, TypeResolver);
        await writer.Write(new Pet("green"), default);
        using var read = new MemoryStream(write.ToArray());
        using var reader = ObjectClient.From(read, TypeResolver);
        Assert.That(() => reader.Read<Dog>(default), Throws.InstanceOf<ObjectTypeUnexpectedException>());
    }

    [Test]
    public async Task ReadObject_ThrowsExceptionWithTypeInfo() {
        using var write = new MemoryStream(4096);
        using var writer = ObjectClient.From(write, TypeResolver);
        await writer.Write(new Pet("green"), default);
        using var read = new MemoryStream(write.ToArray());
        using var reader = ObjectClient.From(read, TypeResolver);
        Assert.That(() => reader.Read<Dog>(default), Throws
            .Exception
            .With
            .Property(nameof(ObjectTypeUnexpectedException.ExpectedType))
            .EqualTo(typeof(Dog))
            .And
            .Property(nameof(ObjectTypeUnexpectedException.ActualType))
            .EqualTo(typeof(Pet)));
    }
}
