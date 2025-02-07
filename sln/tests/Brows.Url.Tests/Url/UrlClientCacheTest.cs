using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace Brows.Url {
    [TestFixture]
    public sealed class UrlClientCacheTest {
        private sealed class MockClient : UrlClient {
        }

        private sealed class MockClientCache : UrlClientCache<MockClient> {
        }

        private MockClientCache Subject {
            get => _Subject ??= new();
            set => _Subject = value;
        }
        private MockClientCache _Subject;

        [SetUp]
        public void SetUp() {
            Subject = null;
        }

        public static object[] Urls => new object[] {
            "http://hello.world",
            "https://goodbye.earth"
        };

        [TestCaseSource(nameof(Urls))]
        public void Get_ReturnsAnInstance(string url) {
            var obj = Subject.Get(new Uri(url), Array.Empty<MockClient>());
            Assert.That(obj, Is.Not.Null);
        }

        [TestCaseSource(nameof(Urls))]
        public void Get_ReturnsTheSameInstance(string url) {
            var obj1 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
            var obj2 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
            Assert.That(obj1, Is.SameAs(obj2));
        }

        [TestCaseSource(nameof(Urls))]
        public void Get_ReturnsAnotherInstance(string url) {
            var obj1 = Subject.Get(new Uri(url), Array.Empty<MockClient>());
            var obj2 = Subject.Get(new Uri(url + "/more"), Array.Empty<MockClient>());
            Assert.That(obj1, Is.Not.SameAs(obj2));
        }

        public static object[] Extends = [
            new object[] { "ftp://some.server/path1", "ftp://some.server/path2" },
            new object[] { "ftp://some.server/path1", "ftp://some.server/path1/path2" }
        ];

        [TestCaseSource(nameof(Extends))]
        public void Get_UriIsCorrectAfterExtendingAnotherInstance(string url1, string url2) {
            var obj1 = Subject.Get(new Uri(url1));
            var obj2 = Subject.Get(new Uri(url2));
            Assert.That(obj2.Uri, Is.EqualTo(new Uri(url2)));
        }

        [TestCaseSource(nameof(Extends))]
        public void Get_CopiesUsernameWhileExtending(string url1, string url2) {
            var obj1 = Subject.Get(new Uri(url1));
            obj1.Credential.Username = "cool_dude1";
            var obj2 = Subject.Get(new Uri(url2));
            Assert.That(obj2.Credential.Username, Is.EqualTo("cool_dude1"));
        }

        [TestCaseSource(nameof(Extends))]
        public async Task Get_CopiesPasswordWhileExtending(string url1, string url2) {
            var obj1 = Subject.Get(new Uri(url1));
            await obj1.Credential.ProtectPassword("p@ssword1", SecurityDataKind.PlainText, default);
            var obj2 = Subject.Get(new Uri(url2));
            Assert.That(obj2.Credential.Password, Is.EqualTo("p@ssword1"));
        }
    }
}
