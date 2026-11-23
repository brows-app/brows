using Brows.Composition;
using Brows.Entries;
using Brows.Operations;
using Brows.Panels;
using Brows.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Brows.Commands;

/// <summary>
/// Tests the command that exports the messages of a mailbox to the provider of a panel.
/// </summary>
[TestFixture]
internal sealed class ExportTest {
    private MBoxTestDirectory _Directory;
    private MBoxProvider _Provider;
    private TestPanel _Panel;
    private TestCommandContext _Context;
    private IReadOnlyList<MBoxEntry> _Entries;

    /*
     * Receives the files of the export the way that a provider receives copied streams.
     */
    private sealed class CopyService : ICopyProvidedIO {
        internal Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, long> Lengths { get; } = new(StringComparer.Ordinal);
        internal List<string> Temporary { get; } = [];
        internal bool Result { get; set; } = true;

        public async Task<bool> Work(IEnumerable<IProvidedIO> io,
                                     IProvider target,
                                     IOperationProgress progress,
                                     CancellationToken token) {
            foreach (var provided in io) {
                foreach (var set in provided.StreamSets) {
                    try {
                        await set.Consume(
                            progress: progress,
                            token: token,
                            consuming: async (source, stream, progress, token) => {
                                if (stream is FileStream file) {
                                    Temporary.Add(file.Name);
                                }
                                using var memory = new MemoryStream();
                                await stream.CopyToAsync(memory, token);
                                Files[source.RelativePath] = memory.ToArray();
                                Lengths[source.RelativePath] = source.StreamLength;
                            });
                    }
                    catch (InvalidDataException) {
                        return false;
                    }
                }
            }
            return Result;
        }
    }

    private static string Record(string subject, params (string Headers, string Base64)[] attachments) {
        var text = new StringBuilder();
        text.Append(MBoxTestFixture.Envelope + "\n");
        text.Append($"Subject: {subject}\n");
        text.Append("Content-Type: multipart/mixed; boundary=\"mixed\"\n");
        text.Append('\n');
        text.Append("--mixed\n");
        text.Append("Content-Type: text/plain; charset=utf-8\n");
        text.Append('\n');
        text.Append($"{subject} body.\n");
        foreach (var attachment in attachments) {
            text.Append("--mixed\n");
            text.Append(attachment.Headers);
            text.Append("Content-Transfer-Encoding: base64\n");
            text.Append('\n');
            text.Append(attachment.Base64 + "\n");
        }
        text.Append("--mixed--\n");
        return text.ToString();
    }

    private static (string, string) Named(string fileName, string contents) {
        return (
            "Content-Type: application/octet-stream\n" +
            $"Content-Disposition: attachment; filename=\"{fileName}\"\n",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(contents)));
    }

    private static string Many(int count) {
        var attachments = Enumerable
            .Range(0, count)
            .Select(index => Named($"file-{index}.txt", "x"))
            .ToArray();
        return Record("Many", attachments);
    }

    private static string Deep(int depth) {
        var text = new StringBuilder();
        text.Append(MBoxTestFixture.Envelope + "\n");
        text.Append("Subject: Deep\n");
        text.Append("Content-Type: multipart/mixed; boundary=\"d0\"\n");
        text.Append('\n');
        var (headers, base64) = Named("top.txt", "top");
        text.Append($"--d0\n{headers}Content-Transfer-Encoding: base64\n\n{base64}\n");
        for (var level = 1; level < depth; level++) {
            text.Append($"--d{level - 1}\n");
            text.Append($"Content-Type: multipart/mixed; boundary=\"d{level}\"\n\n");
        }
        (headers, base64) = Named("deep.txt", "deep");
        text.Append($"--d{depth - 1}\n{headers}Content-Transfer-Encoding: base64\n\n{base64}\n");
        for (var level = depth - 1; level >= 0; level--) {
            text.Append($"--d{level}--\n");
        }
        return text.ToString();
    }

    private static string PartHeaders(int count) {
        var fields = string.Concat(Enumerable.Range(0, count).Select(index => $"X-Field-{index}: value\n"));
        var (headers, base64) = Named("hidden.txt", "hidden");
        return Record("Part headers", Named("seen.txt", "seen"), (fields + headers, base64));
    }

    private static string MessageHeaders(int count) {
        var fields = string.Concat(Enumerable.Range(0, count).Select(index => $"X-Field-{index}: value\n"));
        var record = Record("Message headers", Named("hidden.txt", "hidden"));
        return record.Replace("Subject: Message headers\n", "Subject: Message headers\n" + fields);
    }

    private static Dictionary<string, string> Unzip(byte[] bytes) {
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        return archive.Entries.ToDictionary(
            entry => entry.FullName,
            entry => {
                using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
                return reader.ReadToEnd();
            });
    }

    private string MessageName(int index) {
        return MBoxPath.MessageName(_Entries[index].Record.Offset);
    }

    private string ArchiveName(int index) {
        return MBoxPath.ArchiveName(_Entries[index].Record.Offset);
    }

    private void Select(params MBoxEntry[] entries) {
        foreach (var entry in entries) {
            entry.Select = true;
        }
    }

    private bool Work(string parameter) {
        _Context.Line = new TestCommandLine { Parameter = parameter };
        Export command = new() {
            CommandCliProvider = new CommandCliProvider(),
        };
        return ((ICommand)command).TriggeredWork(_Context);
    }

    private CopyService Target(int column = 1) {
        var service = new CopyService();
        _Context.Panels.Panels.Add(new TestPanel {
            Column = column,
            Provider = Mock.Of<IProvider>(),
            Services = { service },
        });
        return service;
    }

    [SetUp]
    public async Task SetUp() {
        _Directory = new MBoxTestDirectory();
        var mailbox =
            Record("First", Named("report.pdf", "%PDF first"), Named("photo.png", "PNG bytes")) + "\n" +
            Record("Second",
                   Named("report.pdf", "%PDF second"),
                   ("Content-Type: application/octet-stream\nContent-Disposition: attachment\n",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("unnamed")))) + "\n" +
            MBoxTestFixture.Mailbox("Only text.\n", new[] { "Subject: Plain" }) + "\n" +
            Record("Unsafe",
                   Named(@"..\..\evil.txt", "evil"),
                   Named("sub/dir.txt", "dir"),
                   Named("CON.txt", "device"),
                   Named("trail. ", "trail"),
                   Named("a:b?.txt", "colon")) + "\n" +
            Record("Repeated", Named("report.pdf", "%PDF one"), Named("report.pdf", "%PDF two")) + "\n" +
            Many(1025) + "\n" +
            Deep(18) + "\n" +
            PartHeaders(4097) + "\n" +
            MessageHeaders(4097);
        var path = MBoxTestFixture.WriteFile(_Directory.Directory, "inbox.mbox", mailbox);
        _Provider = await MBoxTestProvider.Create(path, new MBoxConfig());
        await MBoxTestProvider.Refresh(_Provider, CancellationToken.None);
        _Entries = _Provider.Entries.OrderBy(entry => entry.Record.Offset).ToList();
        _Panel = new TestPanel {
            Column = 0,
            Provider = _Provider,
        };
        _Context = new TestCommandContext {
            Panels = { Panels = { _Panel } },
            /*
             * The palette shows the observation of the provider as the source of the command,
             * so the command is reachable when the observation is the source.
             */
            Source = new TestCommandSource { Items = { ((IEntryProvider)_Provider).Observation } },
        };
    }

    [TearDown]
    public void TearDown() {
        ((IProvider)_Provider).End();
        _Provider.Dispose();
        _Directory.Dispose();
    }

    [TestCase("auto eml")]
    [TestCase("n eml")]
    [TestCase("auto mode=eml")]
    [TestCase("mode=eml")]
    public void Export_TriggeredWork_CopiesAMessagePerSelectedMessageInEmlMode(string parameter) {
        var target = Target();
        Select(_Entries[0], _Entries[1]);
        Assert.That(Work(parameter), Is.True);
        Assert.That(target.Files.Keys, Is.EquivalentTo(new[] { MessageName(0), MessageName(1) }));
    }

    [Test]
    public void Export_TriggeredWork_CopiesTheMessageAsAnInternetMessage() {
        var target = Target();
        Select(_Entries[0]);
        Work("auto eml");
        var text = Encoding.UTF8.GetString(target.Files[MessageName(0)]);
        using (Assert.EnterMultipleScope()) {
            Assert.That(text, Does.Contain("Subject: First"));
            Assert.That(text, Does.Contain("First body."));
            Assert.That(text, Does.Contain(Convert.ToBase64String(Encoding.UTF8.GetBytes("%PDF first"))));
            Assert.That(text, Does.Not.Contain("Second body."));
        }
    }

    [TestCase("")]
    [TestCase("auto")]
    [TestCase("auto archive")]
    [TestCase("auto mode=archive")]
    public void Export_TriggeredWork_CopiesAnArchivePerSelectedMessageByDefault(string parameter) {
        var target = Target();
        Select(_Entries[0], _Entries[1]);
        Assert.That(Work(parameter), Is.True);
        Assert.That(target.Files.Keys, Is.EquivalentTo(new[] { ArchiveName(0), ArchiveName(1) }));
    }

    [Test]
    public void Export_TriggeredWork_ArchivesTheMessageAndItsDecodedAttachments() {
        var target = Target();
        Select(_Entries[0]);
        Work("auto mode=archive");
        var files = Unzip(target.Files[ArchiveName(0)]);
        using (Assert.EnterMultipleScope()) {
            Assert.That(files.Keys, Is.EquivalentTo(new[] { MessageName(0), "report.pdf", "photo.png" }));
            Assert.That(files[MessageName(0)], Does.Contain("First body."));
            Assert.That(files["report.pdf"], Is.EqualTo("%PDF first"));
            Assert.That(files["photo.png"], Is.EqualTo("PNG bytes"));
        }
    }

    [Test]
    public void Export_TriggeredWork_NamesAnUnnamedAttachment() {
        var target = Target();
        Select(_Entries[1]);
        Work("auto mode=archive");
        var files = Unzip(target.Files[ArchiveName(1)]);
        using (Assert.EnterMultipleScope()) {
            Assert.That(files.Keys, Is.EquivalentTo(new[] { MessageName(1), "report.pdf", "attachment" }));
            Assert.That(files["attachment"], Is.EqualTo("unnamed"));
        }
    }

    [Test]
    public void Export_TriggeredWork_GivesARepeatedAttachmentNameANameOfItsOwn() {
        var target = Target();
        Select(_Entries[4]);
        Work("auto mode=archive");
        var files = Unzip(target.Files[ArchiveName(4)]);
        using (Assert.EnterMultipleScope()) {
            Assert.That(files.Keys, Is.EquivalentTo(new[] { MessageName(4), "report.pdf", "report (2).pdf" }));
            Assert.That(files["report.pdf"], Is.EqualTo("%PDF one"));
            Assert.That(files["report (2).pdf"], Is.EqualTo("%PDF two"));
        }
    }

    [Test]
    public void Export_TriggeredWork_GivesEveryAttachmentASafeName() {
        var target = Target();
        Select(_Entries[3]);
        Work("auto mode=archive");
        var files = Unzip(target.Files[ArchiveName(3)]);
        using (Assert.EnterMultipleScope()) {
            Assert.That(files.Keys, Is.EquivalentTo(new[] {
                MessageName(3), "_CON.txt", "a_b_.txt", "dir.txt", "evil.txt", "trail",
            }));
            Assert.That(files["evil.txt"], Is.EqualTo("evil"));
        }
    }

    [Test]
    public void Export_TriggeredWork_ArchivesAMessageWithoutAttachments() {
        var target = Target();
        Select(_Entries[2]);
        Assert.That(Work("auto mode=archive"), Is.True);
        Assert.That(Unzip(target.Files[ArchiveName(2)]).Keys, Is.EquivalentTo(new[] { MessageName(2) }));
    }

    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void Export_TriggeredWork_FailsWhenALimitStopsTheAttachmentsBeforeTheirEnd(int index) {
        var target = Target();
        Select(_Entries[index]);
        Assert.That(Work("auto archive"), Is.False);
        Assert.That(target.Files.Keys, Does.Not.Contain(ArchiveName(index)));
    }

    [TestCase(5)]
    [TestCase(6)]
    [TestCase(7)]
    [TestCase(8)]
    public void Export_TriggeredWork_ExportsTheMessageWhoseAttachmentsExceedALimit(int index) {
        var target = Target();
        Select(_Entries[index]);
        Assert.That(Work("auto eml"), Is.True);
        Assert.That(target.Files.Keys, Is.EquivalentTo(new[] { MessageName(index) }));
    }

    [TestCase("auto eml")]
    [TestCase("auto archive")]
    public void Export_TriggeredWork_ReportsTheLengthOfEachFile(string parameter) {
        var target = Target();
        Select(_Entries[0], _Entries[1]);
        Work(parameter);
        var lengths = target.Files.ToDictionary(pair => pair.Key, pair => (long)pair.Value.Length);
        Assert.That(target.Lengths, Is.EqualTo(lengths));
    }

    [TestCase("auto eml")]
    [TestCase("auto archive")]
    public void Export_TriggeredWork_DeletesItsTemporaryFiles(string parameter) {
        var target = Target();
        Select(_Entries[0], _Entries[1]);
        Work(parameter);
        using (Assert.EnterMultipleScope()) {
            Assert.That(target.Temporary, Has.Count.EqualTo(2));
            Assert.That(target.Temporary.Where(File.Exists), Is.Empty);
        }
    }

    [TestCase("next", "next")]
    [TestCase("n", "next")]
    [TestCase("previous", "previous")]
    [TestCase("p", "previous")]
    public void Export_TriggeredWork_CopiesToThePanelChosenByWhere(string where, string expected) {
        _Panel.Column = 1;
        var previous = Target(column: 0);
        var next = Target(column: 2);
        Select(_Entries[0]);
        Assert.That(Work(where), Is.True);
        var copied = new[] { ArchiveName(0) };
        using (Assert.EnterMultipleScope()) {
            Assert.That(previous.Files.Keys, expected == "previous" ? Is.EquivalentTo(copied) : Is.Empty);
            Assert.That(next.Files.Keys, expected == "next" ? Is.EquivalentTo(copied) : Is.Empty);
        }
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWithoutThePanelChosenByWhere() {
        var target = Target();
        Select(_Entries[0]);
        Assert.That(Work("previous"), Is.False);
        Assert.That(target.Files, Is.Empty);
    }

    [Test]
    public void Export_TriggeredWork_CopiesToTheOtherPanelWithoutWhere() {
        var target = Target();
        Select(_Entries[0]);
        Assert.That(Work(""), Is.True);
        Assert.That(target.Files.Keys, Is.EquivalentTo(new[] { ArchiveName(0) }));
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenThePanelCannotReceiveFiles() {
        _Context.Panels.Panels.Add(new TestPanel {
            Column = 1,
            Provider = Mock.Of<IProvider>(),
        });
        Select(_Entries[0]);
        Assert.That(Work("auto"), Is.False);
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenThePanelIsTheMailbox() {
        Target();
        var before = File.ReadAllText(_Provider.Path);
        Select(_Entries[0]);
        Assert.That(Work("here"), Is.False);
        Assert.That(File.ReadAllText(_Provider.Path), Is.EqualTo(before));
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWithoutSelection() {
        var target = Target();
        Assert.That(Work("auto"), Is.False);
        Assert.That(target.Files, Is.Empty);
    }

    [Test]
    public void Export_TriggeredWork_ReturnsFalseWhenTheCopyFails() {
        var target = Target();
        target.Result = false;
        Select(_Entries[0]);
        Assert.That(Work("auto"), Is.False);
    }
}
