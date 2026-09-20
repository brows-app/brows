using NUnit.Framework;
using Brows.Composition;
using Brows.Composition.ImportCollections;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
public sealed class ImportsTest {
    [SetUp]
    public void SetUp() {
    }

    [TearDown]
    public void TearDown() {
    }

    private interface IAmAnExport : IExport { }
    private class AnExport : IAmAnExport { }
    private class AnotherExport : IAmAnExport { }

    [Test]
    public Task List_ThrowsExceptionIfNotReady() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.Current.List<IAmAnExport>(), Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public Task List_ThrowsExceptionIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.List<IAmAnExport>(), Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public Task List_ThrowsImportsNotReadyExceptionIfNotReady() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.Current.List<IAmAnExport>(), Throws.InstanceOf<ImportsNotReadyException>());
        });
    }

    [Test]
    public Task List_ThrowsImportsNotReadyExceptionIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.List<IAmAnExport>(), Throws.InstanceOf<ImportsNotReadyException>());
        });
    }

    [Test]
    public Task List_ReturnsEmptyIfNotReady() {
        return ImportSandbox.Clean(() => {
            Assert.That(Imports.Current.List<IAmAnExport>(throwIfNotReady: false), Is.Empty);
        });
    }

    [Test]
    public Task List_ReturnsEmptyIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(Imports.List<IAmAnExport>(throwIfNotReady: false), Is.Empty);
        });
    }

    [Test]
    public Task Find_ThrowsExceptionIfNotReady() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.Current.Find<IAmAnExport>(), Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public Task Find_ThrowsExceptionIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.Find<IAmAnExport>(), Throws.InstanceOf<InvalidOperationException>());
        });
    }

    [Test]
    public Task Find_ThrowsImportsNotReadyExceptionIfNotReady() {
        return ImportSandbox.Clean(() => {
            var subject = Imports.Current;
            Assert.That(() => subject.Find<IAmAnExport>(),
                Throws.InstanceOf<ImportsNotReadyException>()
                      .With.Property(nameof(ImportsNotReadyException.Agent))
                           .SameAs(subject));
        });
    }

    [Test]
    public Task Find_ThrowsImportsNotReadyExceptionIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(() => Imports.Find<IAmAnExport>(),
                Throws.InstanceOf<ImportsNotReadyException>()
                      .With.Property(nameof(ImportsNotReadyException.Agent))
                           .SameAs(Imports.Current));
        });
    }

    [Test]
    public Task Find_ReturnsDefaultIfNotReady() {
        return ImportSandbox.Clean(() => {
            Assert.That(Imports.Current.Find<IAmAnExport>(throwIfNotReady: false), Is.Null);
        });
    }

    [Test]
    public Task Find_ReturnsDefaultIfNotReady_FromStaticMethod() {
        return ImportSandbox.Clean(() => {
            Assert.That(Imports.Find<IAmAnExport>(throwIfNotReady: false), Is.Null);
        });
    }

    [Test]
    public Task List_ListsSpecifiedTypes() {
        var inject = new IExport[] { new AnExport(), new AnExport(), new AnotherExport() };
        return ImportSandbox.Enter(inject, () => {
            var actual = Imports.Current.List<IAmAnExport>();
            Assert.That(actual, Is.EqualTo(inject));
        });
    }

    [Test]
    public Task List_ListsSpecifiedTypes_FromStaticMethod() {
        var inject = new IExport[] { new AnExport(), new AnExport(), new AnotherExport() };
        return ImportSandbox.Enter(inject, () => {
            var actual = Imports.List<IAmAnExport>();
            Assert.That(actual, Is.EqualTo(inject));
        });
    }

    [Test]
    public Task Find_FindsFirstSpecifiedType() {
        var inject = new IExport[] { new AnExport(), new AnExport(), new AnotherExport() };
        return ImportSandbox.Enter(inject, () => {
            var actual = Imports.Current.Find<IAmAnExport>();
            var expected = inject.First();
            Assert.That(actual, Is.SameAs(expected));
        });
    }

    [Test]
    public Task Find_FindsFirstSpecifiedType_FromStaticMethod() {
        var inject = new IExport[] { new AnExport(), new AnExport(), new AnotherExport() };
        return ImportSandbox.Enter(inject, () => {
            var actual = Imports.Find<IAmAnExport>();
            var expected = inject.First();
            Assert.That(actual, Is.SameAs(expected));
        });
    }

    [Test]
    public Task Find_ReturnsNullIfNotFound() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            var actual = Imports.Current.Find<AnotherExport>();
            Assert.That(actual, Is.Null);
        });
    }

    [Test]
    public Task Find_ReturnsNullIfNotFound_FromStaticMethod() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            var actual = Imports.Find<AnotherExport>();
            Assert.That(actual, Is.Null);
        });
    }

    [Test]
    public Task Find_ThrowsExceptionIfNotFound() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            Assert.That(() => Imports.Current.Find<AnotherExport>(throwIfNotFound: true),
                Throws.TypeOf<ImportNotFoundException>());
        });
    }

    [Test]
    public Task Find_ThrowsExceptionIfNotFound_FromStaticMethod() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            Assert.That(() => Imports.Find<AnotherExport>(throwIfNotFound: true),
                Throws.TypeOf<ImportNotFoundException>());
        });
    }

    [Test]
    public Task Find_ThrowsExceptionWithNotFoundType() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            var err = default(ImportNotFoundException);
            try {
                Imports.Current.Find<AnotherExport>(throwIfNotFound: true);
            }
            catch (ImportNotFoundException ex) {
                err = ex;
            }
            Assert.That(err.TypeNotFound, Is.EqualTo(typeof(AnotherExport)));
        });
    }

    [Test]
    public Task Find_ThrowsExceptionWithNotFoundType_FromStaticMethod() {
        return ImportSandbox.Enter([new AnExport(), new AnExport()], () => {
            var err = default(ImportNotFoundException);
            try {
                Imports.Find<AnotherExport>(throwIfNotFound: true);
            }
            catch (ImportNotFoundException ex) {
                err = ex;
            }
            Assert.That(err.TypeNotFound, Is.EqualTo(typeof(AnotherExport)));
        });
    }

    private class ExportWithPublicImport : IExport {
        public AnExport AnExport { get; set; }
    }

    [Test]
    public async Task Init_PopulatesPublicImport() {
        var obj = default(ExportWithPublicImport);
        var expected = new AnExport();
        await ImportSandbox.Enter([expected, new ExportWithPublicImport()], () => {
            obj = Imports.Current.Find<ExportWithPublicImport>();
        });
        Assert.That(obj.AnExport, Is.SameAs(expected));
    }

    private class ExportWithInternalImport : IExport {
        internal AnExport AnExport { get; set; }
    }

    [Test]
    public async Task Init_PopulatesInternalImport() {
        var obj = default(ExportWithInternalImport);
        var expected = new AnExport();
        await ImportSandbox.Enter([expected, new ExportWithInternalImport()], () => {
            obj = Imports.Current.Find<ExportWithInternalImport>();
        });
        Assert.That(obj.AnExport, Is.SameAs(expected));
    }

    private class ExportWithContractImport : IExport {
        internal IAmAnExport AnExport { get; set; }
    }

    [Test]
    public async Task Init_PopulatesImportByInterface() {
        var obj = default(ExportWithContractImport);
        var expected = new AnExport();
        await ImportSandbox.Enter([expected, new ExportWithContractImport()], () => {
            obj = Imports.Current.Find<ExportWithContractImport>();
        });
        Assert.That(obj.AnExport, Is.SameAs(expected));
    }

    private class ExportWithInternalReadOnlyList : IExport {
        internal IReadOnlyList<AnExport> Imports { get; set; }
    }

    [Test]
    public async Task Init_PopulatesReadOnlyListsOfImports() {
        var obj = default(ExportWithInternalReadOnlyList);
        var expected1 = new AnExport();
        var expected2 = new AnExport();
        await ImportSandbox.Enter([expected1, expected2, new ExportWithInternalReadOnlyList()], () => {
            obj = Imports.Current.Find<ExportWithInternalReadOnlyList>();
        });
        Assert.That(obj.Imports, Is.EqualTo(new[] { expected1, expected2 }));
    }

    private class ExportWithInternalEnumerable : IExport {
        internal IEnumerable<AnExport> Imports { get; set; }
    }

    [Test]
    public async Task Init_PopulatesEnumerableOfImports() {
        var obj = default(ExportWithInternalEnumerable);
        var expected1 = new AnExport();
        var expected2 = new AnExport();
        await ImportSandbox.Enter([expected1, expected2, new ExportWithInternalEnumerable()], () => {
            obj = Imports.Current.Find<ExportWithInternalEnumerable>();
        });
        Assert.That(obj.Imports, Is.EqualTo(new[] { expected1, expected2 }));
    }

    private class ExportWithInternalEnumerableContract : IExport {
        internal IEnumerable<IAmAnExport> Imports { get; set; }
    }

    [Test]
    public async Task Init_PopulatesEnumerableOfContracts() {
        var obj = default(ExportWithInternalEnumerableContract);
        var expected1 = new AnExport();
        var expected2 = new AnotherExport();
        var expected3 = new AnExport();
        await ImportSandbox.Enter([expected1, expected2, expected3, new ExportWithInternalEnumerableContract()], () => {
            obj = Imports.Current.Find<ExportWithInternalEnumerableContract>();
        });
        Assert.That(obj.Imports, Is.EqualTo(new IExport[] { expected1, expected2, expected3 }));
    }

    interface IDoStuff : IExport { }
    interface IDoSomeStuff : IDoStuff { }
    interface IDoMoreStuff : IDoStuff { }
    class DoStuff : IDoSomeStuff, IDoMoreStuff { }

    [Test]
    public async Task Find_FindsSameInstanceForClassThatImplementsMultipleExports() {
        var a = default(IDoSomeStuff);
        var b = default(IDoMoreStuff);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            a = Imports.Current.Find<IDoSomeStuff>();
            b = Imports.Current.Find<IDoMoreStuff>();
        });
        Assert.That(a, Is.SameAs(b));
    }

    [Test]
    public async Task Find_FindsSameInstanceForClassThatImplementsMultipleExports_2() {
        var a = default(IDoSomeStuff);
        var b = default(IDoMoreStuff);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            a = Imports.Current.Find<IDoSomeStuff>();
            b = Imports.Find<IDoMoreStuff>();
        });
        Assert.That(a, Is.SameAs(b));
    }

    [Test]
    public async Task Find_FindsOneInstanceForClassThatImplementsMultipleExports() {
        var list = default(IReadOnlyList<IDoStuff>);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            list = Imports.Current.List<IDoStuff>();
        });
        Assert.That(list, Has.Exactly(1).Items);
    }

    [Test]
    public async Task Find_FindsCorrectInstanceForClassThatImplementsMultipleExports() {
        var list = default(IReadOnlyList<IDoStuff>);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            list = Imports.Current.List<IDoStuff>();
        });
        Assert.That(list[0], Is.TypeOf<DoStuff>());
    }

    class ExportWithPopulatedCallback : IExport {
        public int CallbackCount;

        [ImportsPopulatedCallback]
        private void OnImportsPopulated() {
            Interlocked.Increment(ref CallbackCount);
        }
    }

    [Test]
    public async Task Init_CallsMethodDecoratedWithImportsPopulatedCallbackAttribute() {
        var obj = default(ExportWithPopulatedCallback);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            obj = Imports.Current.Find<ExportWithPopulatedCallback>();
        });
        Assert.That(obj.CallbackCount, Is.EqualTo(1));
    }

    class ExportWithImport : IExport {
        internal ExportWithPopulatedCallback MyImport { get; set; }
    }

    [Test]
    public async Task Init_PopulatesImportOfExport() {
        var obj = default(ExportWithImport);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            obj = Imports.Current.Find<ExportWithImport>();
        });
        Assert.That(obj.MyImport, Is.Not.Null);
    }

    class ExportWithCallbackParent : IExport {
        public List<string> Callbacks { get; } = [];

        [ImportsPopulatedCallback]
        private void Callback() {
            Callbacks.Add("parent");
        }
    }

    class ExportWithCallbackChild : ExportWithCallbackParent {
        [ImportsPopulatedCallback]
        private void Callback() {
            Callbacks.Add("child");
        }
    }

    [Test]
    public async Task Init_InvokesAllCallbacksInHeirarchy() {
        var obj = default(ExportWithCallbackChild);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(null, default);
            obj = Imports.Current.Find<ExportWithCallbackChild>();
        });
        Assert.That(obj.Callbacks, Is.EqualTo(["parent", "child"]));
    }

    class VaryingExport : IExportAndVary {
        public int Called { get; private set; }
        public Task Result { get; set; }
        public IExportVariables Variables { get; private set; }
        public CancellationToken Token { get; private set; }

        Task IExportAndVary.Vary(IExportVariables variables, CancellationToken token) {
            Called++;
            Token = token;
            Variables = variables;
            return Result ?? Task.CompletedTask;
        }

        public class Variable : IExportVariable {
        }
    }

    [Test]
    public async Task Init_DoesNotVaryImportsIfVariablesAreNull() {
        var actual = default(int);
        var export = new VaryingExport();
        var expected = 0;
        await ImportSandbox.Enter(inject: [export], variables: null, action: () => {
            actual = Imports.Current.Find<VaryingExport>().Called;
        });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Init_PassesVariablesToImports() {
        var actual = default(VaryingExport.Variable);
        var export = new VaryingExport();
        var expected = new VaryingExport.Variable();
        await ImportSandbox.Enter(inject: [export], variables: new ImportVariables().Set(expected), action: () => {
            actual = Imports.Current.Find<VaryingExport>().Variables.Get<VaryingExport.Variable>();
        });
        Assert.That(actual, Is.SameAs(expected));
    }

    [Test]
    public async Task Init_PassesVariablesToAllImports() {
        var expected = new VaryingExport.Variable();
        var actual = default(IEnumerable<VaryingExport.Variable>);
        var exports = new IExport[] { new VaryingExport(), new VaryingExport() };
        await ImportSandbox.Enter(
            inject: exports,
            variables: new ImportVariables(expected),
            action: () => {
                actual = Imports
                    .Current
                    .List<VaryingExport>()
                    .Select(import => import.Variables.Get<VaryingExport.Variable>());
            });
        Assert.That(actual, Is.EqualTo([expected, expected]));
    }

    [Test]
    public async Task Init_PassesTokenToImports() {
        using (var source = new CancellationTokenSource()) {
            var actual = default(CancellationToken);
            var export = new VaryingExport();
            var expected = source.Token;
            await ImportSandbox.Clean(async () => {
                await Imports.Init(token: expected, environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(
                        () => new ImportVariables().Set(new VaryingExport.Variable()),
                        () => [export])
                });
                actual = Imports.Current.Find<VaryingExport>().Token;
            });
            Assert.That(actual, Is.EqualTo(expected));
        }
    }

    [Test]
    public async Task Init_ThrowsExceptionIfVaryingImportThrows() {
        var actual = default(Exception);
        var expected = new InvalidOperationException();
        await ImportSandbox.Clean(async () => {
            try {
                await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(
                        list: () => [new VaryingExport { Result = Task.FromException(expected) }],
                        variables: () => new ImportVariables().Set(new VaryingExport.Variable()))
                });
            }
            catch (Exception ex) {
                actual = ex;
            }
        });
        Assert.That(actual, Is.SameAs(expected));
    }

    class VaryWithGeneric : IExportAndVary<VaryWithGeneric.VariableClass> {
        public int Called = 0;
        public Task Result { get; set; }
        public VariableClass Variable { get; private set; }
        public CancellationToken Token { get; private set; }

        public class VariableClass : IExportVariable {
        }

        Task IExportAndVary<VariableClass>.Vary(VariableClass variable, CancellationToken token) {
            Called++;
            Token = token;
            Variable = variable;
            return Result ?? Task.CompletedTask;
        }
    }

    [Test]
    public async Task Init_CallsVaryOnImportsThanImplementItGenerically() {
        var actual = default(VaryWithGeneric.VariableClass);
        var export = new VaryWithGeneric();
        var expected = new VaryWithGeneric.VariableClass();
        await ImportSandbox.Enter(inject: [export], variables: new ImportVariables().Set(expected), action: () => {
            actual = Imports.Current.Find<VaryWithGeneric>().Variable;
        });
        Assert.That(actual, Is.SameAs(expected));
    }

    [Test]
    public async Task Init_DoesNotCallGenericVaryIfVariableIsNull() {
        var actual = default(int);
        var export = new VaryWithGeneric();
        var expected = 0;
        await ImportSandbox.Enter(inject: [export], variables: new ImportVariables().Set(default(VaryWithGeneric.VariableClass)), action: () => {
            actual = Imports.Current.Find<VaryWithGeneric>().Called;
        });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Init_DoesNotCallGenericVaryIfVariableIsNotSet() {
        var actual = default(int);
        var export = new VaryWithGeneric();
        var expected = 0;
        await ImportSandbox.Enter(inject: [export], variables: new ImportVariables(), action: () => {
            actual = Imports.Current.Find<VaryWithGeneric>().Called;
        });
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public async Task Init_ThrowsExceptionIfGenericallyVaryingImportThrows() {
        var actual = default(Exception);
        var expected = new InvalidOperationException();
        await ImportSandbox.Clean(async () => {
            try {
                await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(
                        list: () => [new VaryWithGeneric { Result = Task.FromException(expected) }],
                        variables: () => new(new VaryWithGeneric.VariableClass()))
                });
            }
            catch (Exception ex) {
                actual = ex;
            }
        });
        Assert.That(actual, Is.SameAs(expected));
    }

    [Test]
    public async Task Init_PassesTokenToImportsThatVaryGenerically() {
        using (var source = new CancellationTokenSource()) {
            var actual = default(CancellationToken);
            var export = new VaryWithGeneric();
            var expected = source.Token;
            await ImportSandbox.Clean(async () => {
                await Imports.Init(token: expected, environment: new ImportSandboxEnvironment {
                    ImportInfo = ImportInfo.Listed(
                        list: () => [export],
                        variables: () => new ImportVariables().Set(new VaryWithGeneric.VariableClass()))
                });
                actual = Imports.Current.Find<VaryWithGeneric>().Token;
            });
            Assert.That(actual, Is.EqualTo(expected));
        }
    }

    class VariesALot : IExportAndVary, IExportAndVary<VariesALot.Variable1>, IExportAndVary<VariesALot.Variable2> {
        public int VaryCalled { get; private set; }
        public int Vary1Called { get; private set; }
        public int Vary2Called { get; private set; }

        public class Variable1 : IExportVariable { }
        public class Variable2 : IExportVariable { }

        public Task Vary(IExportVariables variables, CancellationToken token) {
            VaryCalled++;
            return Task.CompletedTask;
        }

        public Task Vary(Variable1 variable, CancellationToken token) {
            Vary1Called++;
            return Task.CompletedTask;
        }

        public Task Vary(Variable2 variable, CancellationToken token) {
            Vary2Called++;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task Init_CallsAllVaryMethodsForImports() {
        var import = default(VariesALot);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                ImportInfo = new ListedImportInfo {
                    List = [new VariesALot()],
                    Variables = new ImportVariables()
                        .Set(new VariesALot.Variable1())
                        .Set(new VariesALot.Variable2())
                }
            });
            import = Imports.Current.Find<VariesALot>();
        });
        var actual = new[] { import.VaryCalled, import.Vary1Called, import.Vary2Called };
        var expected = new[] { 1, 1, 1 };
        Assert.That(actual, Is.EqualTo(expected));
    }

    class VariesALot2 : IExportAndVary, IExportAndVary<VariesALot.Variable1>, IExportAndVary<VariesALot.Variable2> {
        public int VaryCalled { get; private set; }
        public int Vary1Called { get; private set; }
        public int Vary2Called { get; private set; }

        public Task Vary(IExportVariables variables, CancellationToken token) {
            VaryCalled++;
            return Task.CompletedTask;
        }

        public Task Vary(VariesALot.Variable1 variable, CancellationToken token) {
            Vary1Called++;
            return Task.CompletedTask;
        }

        public Task Vary(VariesALot.Variable2 variable, CancellationToken token) {
            Vary2Called++;
            return Task.CompletedTask;
        }
    }

    [Test]
    public async Task Init_CallsAllVaryMethodsForAllImports() {
        var import = default(VariesALot);
        var import2 = default(VariesALot2);
        await ImportSandbox.Clean(async () => {
            await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                ImportInfo = new ListedImportInfo {
                    List = [new VariesALot(), new VariesALot2()],
                    Variables = new ImportVariables()
                    .Set(new VariesALot.Variable1())
                    .Set(new VariesALot.Variable2())
                }
            });
            import = Imports.Current.Find<VariesALot>();
            import2 = Imports.Current.Find<VariesALot2>();
        });
        var actual = new[]
        {
            import.VaryCalled, import.Vary1Called, import.Vary2Called,
            import2.VaryCalled, import2.Vary1Called, import2.Vary2Called
        };
        var expected = new[] { 1, 1, 1, 1, 1, 1 };
        Assert.That(actual, Is.EqualTo(expected));
    }

    class ExportWithReadyCallback : IExport {
        [ImportsReadyCallback]
        internal void ImportsReady() {
            InvocationCount++;
        }

        public int InvocationCount { get; protected set; }
    }

    [Test]
    public async Task ImportsReadyCallbackAttribute_SpecifiesImportsReadyCallback() {
        await ImportSandbox.Clean(async () => {
            await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                ImportInfo = new ListedImportInfo {
                    List = [new ExportWithReadyCallback()]
                }
            });
            var import = Imports.Current.Find<ExportWithReadyCallback>();
            Assert.That(import.InvocationCount, Is.EqualTo(1));
        });
    }

    class AnotherExportWithReadyCallback : ExportWithReadyCallback {
        [ImportsReadyCallback]
        [SuppressMessage("CodeQuality", "IDE0051:Remove unused private members", Justification = "Called implicitly")]
        new private void ImportsReady() {
            InvocationCount++;
        }
    }

    [Test]
    public async Task ImportsReadyCallbackAttribute_SpecifiedCallbackIsCalledOncePerTypeInHierarchy() {
        await ImportSandbox.Clean(async () => {
            await Imports.Init(token: default, environment: new ImportSandboxEnvironment {
                ImportInfo = new ListedImportInfo {
                    List = [new ExportWithReadyCallback(), new AnotherExportWithReadyCallback()]
                }
            });
            var import = Imports.Current.Find<AnotherExportWithReadyCallback>();
            Assert.That(import.InvocationCount, Is.EqualTo(2));
        });
    }
}
