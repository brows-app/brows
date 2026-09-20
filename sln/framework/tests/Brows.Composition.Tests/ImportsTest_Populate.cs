using Brows.Composition;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Brows;

[TestFixture]
internal sealed class ImportsTest_Populate {
    private class DaBears {
        public ICoach Coach { get; set; }
        public IEnumerable<IPlayer> Players { get; set; }

        public interface IPlayer : IExport { }
        public interface ICoach : IExport { }

        public class Sweetness : IPlayer { }
        public class TheFridge : IPlayer { }

        public class Ditka : ICoach { }
    }

    [Test]
    public async Task Populate_PopulatesArbitraryObjectList() {
        var team = new DaBears();
        await ImportSandbox.Clean(async () => {
            await
            Imports.Init(null, default);
            Imports.Current.Populate(team);
        });
        Assert.That(team.Players.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Populate_PopulatesArbitraryObjectProperty() {
        var team = new DaBears();
        await ImportSandbox.Clean(async () => {
            await
            Imports.Init(null, default);
            Imports.Current.Populate(team);
        });
        Assert.That(team.Coach, Is.InstanceOf<DaBears.Ditka>());
    }

    private class Bears2025 {
        public IQuarterback QB {
            get => _QB ?? throw new ImportRequiredException();
            set => _QB = value;
        }
        private IQuarterback _QB;
        public interface IQuarterback : IExport { }
    }

    [Test]
    public async Task Populate_ThrowsImportRequiredException() {
        var ex = default(ImportRequiredException);
        var team = new Bears2025();
        await ImportSandbox.Enter([], () => {
            try {
                Imports.Current.Populate(team);
            }
            catch (ImportRequiredException e) {
                ex = e;
            }
        });
        Assert.That(ex.Message, Is.EqualTo("The import 'QB' is required for target 'Bears2025'."));
    }

    private class Bears2026 {
        [ImportRequired]
        public IQuarterback QB { get; set; }
        public interface IQuarterback : IExport { }
    }

    [Test]
    public async Task Populate_ThrowsImportRequiredExceptionIfPropertyHasAttribute() {
        var ex = default(ImportRequiredException);
        var team = new Bears2026();
        await ImportSandbox.Enter([], () => {
            try {
                Imports.Current.Populate(team);
            }
            catch (ImportRequiredException e) {
                ex = e;
            }
        });
        Assert.That(ex,
            Is.Not.Null.And.Property("Message").EqualTo("The import 'QB' is required for target 'Bears2026'."));
    }

    private class Colts2005 {
        [ImportOptional] public IQuarterback QB { get; set; }
        public interface IQuarterback : IExport { }
    }

    [Test]
    public async Task Populate_DoesNotThrowImportRequiredExceptionIfPropertyHasAttribute() {
        var ex = default(ImportRequiredException);
        var team = new Colts2005();
        await ImportSandbox.Enter([], () => {
            try {
                Imports.Current.Populate(team);
            }
            catch (ImportRequiredException e) {
                ex = e;
            }
        });
        Assert.That(ex, Is.Null);
    }

    interface ITeamPlayer : IExport { }
    class TeamPlayer1 : ITeamPlayer { }
    class TeamPlayer2 : ITeamPlayer { }
    class TeamWithEnumerable { internal IEnumerable<ITeamPlayer> Players { get; set; } }
    class TeamWithReadOnlyList { internal IReadOnlyList<ITeamPlayer> Players { get; set; } }
    class TeamWithReadOnlyCollection { internal IReadOnlyCollection<ITeamPlayer> Players { get; set; } }

    private static IEnumerable<Type> ExpectedTeamPlayers => [typeof(TeamPlayer1), typeof(TeamPlayer2)];

    private async Task<T> PopulateTeamPlayers<T>() where T : new() {
        var team = new T();
        await ImportSandbox.Clean(async () => {
            await
            Imports.Init(null, default);
            Imports.Current.Populate(team);
        });
        return team;
    }

    [Test]
    public async Task Populate_PopulatesEnumerable() {
        var team = await PopulateTeamPlayers<TeamWithEnumerable>();
        Assert.That(team.Players.Select(p => p.GetType()), Is.EqualTo(ExpectedTeamPlayers));
    }

    [Test]
    public async Task Populate_PopulatesReadOnlyList() {
        var team = await PopulateTeamPlayers<TeamWithReadOnlyList>();
        Assert.That(team.Players.Select(p => p.GetType()), Is.EqualTo(ExpectedTeamPlayers));
    }

    [Test]
    public async Task Populate_PopulatesReadOnlyCollection() {
        var team = await PopulateTeamPlayers<TeamWithReadOnlyCollection>();
        Assert.That(team.Players.Select(p => p.GetType()), Is.EqualTo(ExpectedTeamPlayers));
    }

    class ThisNeedsAnAgent {
        internal IImportAgent Agent { get; set; }
    }

    [Test]
    public async Task Populate_PopulatesImportAgent() {
        var obj = new ThisNeedsAnAgent();
        await ImportSandbox.Clean(async () => {
            await
            Imports.Init(null, default);
            Imports.Current.Populate(obj);
        });
        Assert.That(obj.Agent, Is.Not.Null);
    }

    [Test]
    public async Task Populate_PopulatesImportAgentThatCanConstruct() {
        var obj = new ThisNeedsAnAgent();
        var team = default(TeamWithEnumerable);
        await ImportSandbox.Clean(async () => {
            await
            Imports.Init(null, default);
            Imports.Current.Populate(obj);
            team = obj.Agent.Construct<TeamWithEnumerable>();
        });
        Assert.That(team.Players.Select(p => p.GetType()), Is.EqualTo(ExpectedTeamPlayers));
    }
}
