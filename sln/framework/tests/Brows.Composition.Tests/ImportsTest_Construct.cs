using NUnit.Framework;
using Brows.Composition;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ImportsTest_Construct {
    class Pet : IExport { }
    class Dog : Pet { }

    class DogLover_WithArray {
        public Dog[] Dogs { get; }

        public DogLover_WithArray(Dog[] dogs) {
            Dogs = dogs;
        }
    }

    [Test]
    public async Task Construct_ConstructsWithArray() {
        var dog1 = new Dog();
        var dog2 = new Dog();
        var dogLover = default(DogLover_WithArray);
        await ImportSandbox.Enter(inject: [dog1, dog2], action: () => {
            dogLover = Imports.Current.Construct<DogLover_WithArray>();
        });
        var expected = new[] { dog1, dog2 };
        var actual = dogLover.Dogs;
        Assert.That(actual, Is.EqualTo(expected));
    }

    class DogLover<T>(T dogs) {
        public T Dogs { get; } = dogs;
    }

    class DogLover_WithList(List<Dog> dogs) : DogLover<List<Dog>>(dogs) { }
    class DogLover_WithIEnumerable(IEnumerable<Dog> dogs) : DogLover<IEnumerable<Dog>>(dogs) { }
    class DogLover_WithIList(IList<Dog> dogs) : DogLover<IList<Dog>>(dogs) { }
    class DogLover_WithICollection(ICollection<Dog> dogs) : DogLover<ICollection<Dog>>(dogs) { }
    class DogLover_WithIReadOnlyCollection(IReadOnlyCollection<Dog> dogs) : DogLover<IReadOnlyCollection<Dog>>(dogs) { }
    class DogLover_WithIReadOnlyList(IReadOnlyList<Dog> dogs) : DogLover<IReadOnlyList<Dog>>(dogs) { }

    private async Task<T> ConstructDogLover<T>(IEnumerable<Dog> dogs) {
        var dogLover = default(T);
        await ImportSandbox.Enter(inject: [.. dogs], action: () => {
            dogLover = Imports.Current.Construct<T>();
        });
        return dogLover;
    }

    [Test]
    public async Task Construct_CreatesInstanceWithList() {
        var expected = new Dog[] { new(), new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithList>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_CreatesInstanceWithIEnumerable() {
        var expected = new Dog[] { new(), new(), new(), new(), new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithIEnumerable>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_CreatesInstanceWithIList() {
        var expected = new Dog[] { new(), new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithIList>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_CreatesInstanceWithICollection() {
        var expected = new Dog[] { new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithICollection>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_CreatesInstanceWithIReadOnlyCollection() {
        var expected = new Dog[] { new(), new(), new(), new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithIReadOnlyCollection>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_CreatesInstanceWithIReadOnlyList() {
        var expected = new Dog[] { new(), new(), new(), new() };
        var dogLover = await ConstructDogLover<DogLover_WithIReadOnlyList>(expected);
        Assert.That(dogLover.Dogs, Is.EqualTo(expected));
    }

    class Cat : Pet { }

    class PetLover(Pet[] pets) {
        public Pet[] Pets { get; } = pets;
    }

    [Test]
    public async Task Construct_ConstructsPetLover() {
        var expected = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat() };
        var petLover = default(PetLover);
        await ImportSandbox.Enter(inject: expected, action: () => {
            petLover = Imports.Current.Construct<PetLover>();
        });
        Assert.That(petLover.Pets, Is.EqualTo(expected));
    }

    class PetLover2(IEnumerable<Dog> dogs, List<Cat> cats) {
        public IEnumerable<Dog> Dogs { get; } = dogs;
        public List<Cat> Cats { get; } = cats;
    }

    [Test]
    public async Task Construct_ConstructsPetLover2Dogs() {
        var inject = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat(), new Dog() };
        var petLover = default(PetLover2);
        await ImportSandbox.Enter(inject: inject, action: () => {
            petLover = Imports.Current.Construct<PetLover2>();
        });
        Assert.That(petLover.Dogs, Is.EqualTo(inject.OfType<Dog>()));
    }

    [Test]
    public async Task Construct_ConstructsPetLover2Cats() {
        var inject = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat(), new Dog() };
        var petLover = default(PetLover2);
        await ImportSandbox.Enter(inject: inject, action: () => {
            petLover = Imports.Current.Construct<PetLover2>();
        });
        Assert.That(petLover.Cats, Is.EqualTo(inject.OfType<Cat>()));
    }

    class PetLover3 {
        public IEnumerable<Pet> Pets { get; }
        public IEnumerable<Dog> Dogs { get; }
        public IEnumerable<Cat> Cats { get; }

        public PetLover3(IEnumerable<Dog> dogs, IEnumerable<Cat> cats) {
            Dogs = dogs;
            Cats = cats;
        }

        public PetLover3(IEnumerable<Pet> pets) {
            Pets = pets;
        }
    }

    private async Task<PetLover3> ConstructPetLover3(IEnumerable<IExport> inject) {
        var petLover = default(PetLover3);
        await ImportSandbox.Enter(inject: [.. inject], action: () => {
            petLover = Imports.Current.Construct<PetLover3>();
        });
        return petLover;
    }

    [Test]
    public async Task Construct_PicksMoreSpecificConstructor_Parameter2() {
        var inject = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat(), new Dog() };
        var petLover = await ConstructPetLover3(inject);
        Assert.That(petLover.Cats, Is.EqualTo(inject.OfType<Cat>()));
    }

    [Test]
    public async Task Construct_PicksMoreSpecificConstructor_Parameter1() {
        var inject = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat(), new Dog() };
        var petLover = await ConstructPetLover3(inject);
        Assert.That(petLover.Dogs, Is.EqualTo(inject.OfType<Dog>()));
    }

    [Test]
    public async Task Construct_DoesNotPickLessSpecificConstructor() {
        var inject = new Pet[] { new Dog(), new Cat(), new Dog(), new Cat(), new Dog() };
        var petLover = await ConstructPetLover3(inject);
        Assert.That(petLover.Pets, Is.Null);
    }

    interface IPest : IExport { }
    class Mouse : IPest { }

    class Exterminator(IPest pest) {
        public IPest Pest { get; } = pest;
    }

    [Test]
    public async Task Construct_CreatesExterminator() {
        var expected = new Mouse();
        var exterminator = default(Exterminator);
        await ImportSandbox.Enter(inject: [expected], action: () => {
            exterminator = Imports.Current.Construct<Exterminator>();
        });
        Assert.That(exterminator.Pest, Is.SameAs(expected));
    }

    class Exterminator2(IPest pest, Cat[] cats) {
        public IPest Pest { get; } = pest;
        public Cat[] Cats { get; } = cats;

        public Dog Dog { get; set; }
    }

    [Test]
    public async Task Construct_CreatesExterminator2_Pest() {
        var expected = new Mouse();
        var exterminator = default(Exterminator2);
        await ImportSandbox.Enter(inject: [expected, new Cat()], action: () => {
            exterminator = Imports.Current.Construct<Exterminator2>();
        });
        Assert.That(exterminator.Pest, Is.SameAs(expected));
    }

    [Test]
    public async Task Construct_CreatesExterminator2_Cats() {
        var expected = new Cat[] { new() };
        var exterminator = default(Exterminator2);
        await ImportSandbox.Enter(inject: [new Mouse(), .. expected], action: () => {
            exterminator = Imports.Current.Construct<Exterminator2>();
        });
        Assert.That(exterminator.Cats, Is.EqualTo(expected));
    }

    [Test]
    public async Task Construct_PopulatesExterminator2() {
        var expected = new Dog();
        var exterminator = default(Exterminator2);
        await ImportSandbox.Enter(inject: [new Mouse(), new Cat(), expected], action: () => {
            exterminator = Imports.Current.Construct<Exterminator2>();
        });
        Assert.That(exterminator.Dog, Is.SameAs(expected));
    }
}
