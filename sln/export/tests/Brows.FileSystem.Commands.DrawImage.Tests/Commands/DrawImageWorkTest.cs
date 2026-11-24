using Brows.Operations;
using Brows.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Brows.Commands;

[TestFixture]
internal sealed class DrawImageWorkTest {
    private static DrawImageWork Work(DrawImage.Parameter parameter) {
        var outputs = new List<KeyValuePair<FileSystemInfo, string>> {
            new(new FileInfo(Path.Combine(Path.GetTempPath(), "image.png")), "image-out.png")
        };
        var threadPool = new Mock<ISTAThreadPool>(MockBehavior.Strict);
        return new DrawImageWork(parameter, outputs, pathsAreEquivalent: null, threadPool.Object);
    }

    [TestCase(45)]
    [TestCase(100)]
    [TestCase(-30)]
    public void Run_RotateNotMultipleOf90_ThrowsBeforeStartingChildren(int rotate) {
        var progress = new Mock<IOperationProgress>(MockBehavior.Strict);
        var work = Work(new DrawImage.Parameter { Rotate = rotate });
        Assert.That(
            async () => await work.Run(progress.Object, CancellationToken.None),
            Throws.InstanceOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo("Rotate"));
    }

    [TestCase(0, null, "Width")]
    [TestCase(-1, null, "Width")]
    [TestCase(null, 0, "Height")]
    [TestCase(10, -5, "Height")]
    public void Run_SizeNotPositive_ThrowsBeforeStartingChildren(int? width, int? height, string name) {
        var progress = new Mock<IOperationProgress>(MockBehavior.Strict);
        var work = Work(new DrawImage.Parameter { Width = width, Height = height });
        Assert.That(
            async () => await work.Run(progress.Object, CancellationToken.None),
            Throws.InstanceOf<ArgumentOutOfRangeException>().With.Property("ParamName").EqualTo(name));
    }

    [TestCase(-270)]
    [TestCase(0)]
    [TestCase(90)]
    [TestCase(360)]
    public void Validate_RotateMultipleOf90_DoesNotThrow(int rotate) {
        var parameter = new DrawImage.Parameter { Rotate = rotate, Width = 1, Height = 1 };
        Assert.That(parameter.Validate, Throws.Nothing);
    }
}
