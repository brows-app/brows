using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Brows.IPC;

[TestFixture]
internal sealed class ObjectHostTest {
    class Pet(string color) {
        public string Color { get; } = color;
        public int Age { get; set; }
    }

    class Cat(string color) : Pet(color) {
        public CatTemperament Temperament { get; set; }
    }

    enum CatTemperament {
        Bad,
        Worse
    }

    class PetDelegate : IObjectCallDelegate {
        IObjectTypeResolver IObjectCallDelegate.TypeResolver => TypeResolver;

        Task<object> IObjectCallDelegate.Call(object input, CancellationToken token) {
            return Task.FromResult(input);
        }

        public static IObjectTypeResolver TypeResolver =>
            ObjectTypeResolver.Lookup(new Dictionary<ObjectTypeInfo, Type>
            {
                { new(nameof(Pet)), typeof(Pet) },
                { new(nameof(Cat)), typeof(Cat) }
            });
    }

    class PetCallFactory : IObjectCallFactory {
        string IObjectCallFactory.Name => nameof(PetDelegate);
        IObjectCallDelegate IObjectCallFactory.Create() => new PetDelegate();
    }

    public async Task<object[]> ReadPets() {
        var subject = new ObjectCallHost(new PetDelegate());
        var actual = new List<Pet>();
        var expected = new[]
        {
                new Pet("white") { Age = 15 },
                new Cat("Black") { Age = 3, Temperament = CatTemperament.Worse }
            };
        using (var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10))) {
            var run = default(Task);
            var token = tokenSource.Token;
            var start = subject.Run(token: token, info: new() {
                OnStart = (info, token) => {
                    run = Task.Run(async () => {
                        foreach (var item in expected) {
                            using (var client = ObjectClient.From(info.EndPoint, PetDelegate.TypeResolver)) {
                                await client.Write(item, token: token);
                                var result = await client.Read<Pet>(token: token);
                                actual.Add(result);
                            }
                        }
                        tokenSource.Cancel();
                    }, token);
                    return Task.CompletedTask;
                }
            });
            try {
                SpinWait.SpinUntil(() => token.IsCancellationRequested || run is not null);
                await run;
                await start;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) {
            }
        }
        return actual.ToArray();
    }

    [Test]
    public async Task Read_FirstObjectType() {
        var result = await ReadPets();
        Assert.That(result[0].GetType(), Is.EqualTo(typeof(Pet)));
    }

    [Test]
    public async Task Read_SecondObjectType() {
        var result = await ReadPets();
        Assert.That(result[1].GetType(), Is.EqualTo(typeof(Cat)));
    }

    [Test]
    public async Task Read_FirstPetColor() {
        var result = await ReadPets();
        Assert.That(((Pet)result[0]).Color, Is.EqualTo("white"));
    }

    [Test]
    public async Task Read_SecondPetColor() {
        var result = await ReadPets();
        Assert.That(((Pet)result[1]).Color, Is.EqualTo("Black"));
    }

    [Test]
    public async Task Read_FirstPetAge() {
        var result = await ReadPets();
        Assert.That(((Pet)result[0]).Age, Is.EqualTo(15));
    }

    [Test]
    public async Task Read_SecondPetAge() {
        var result = await ReadPets();
        Assert.That(((Pet)result[1]).Age, Is.EqualTo(3));
    }

    [Test]
    public async Task Read_CatTemperament() {
        var result = await ReadPets();
        Assert.That(((Cat)result[1]).Temperament, Is.EqualTo(CatTemperament.Worse));
    }
}
