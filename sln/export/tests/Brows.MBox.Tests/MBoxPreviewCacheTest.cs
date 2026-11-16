using Brows.MBox.Format;
using System.Collections.Generic;

namespace Brows;

[TestFixture]
internal sealed class MBoxPreviewCacheTest {
    private static MBoxPreviewResult Preview(string text) {
        return new MBoxPreviewResult {
            Text = text,
            State = MBoxBodyState.Complete
        };
    }

    [Test]
    public void Add_RetainsThePreview() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 8);
        var preview = Preview("body");
        Assert.That(cache.Add("first", preview), Is.True);
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.SameAs(preview));
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Bytes, Is.EqualTo(8));
        }
    }

    [Test]
    public void Get_ReturnsNothingForAPreviewThatIsNotCached() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 8);
        Assert.That(cache.Get("first"), Is.Null);
    }

    [Test]
    public void Add_KeepsNoPreviewThatIsLargerThanTheCache() {
        var cache = new MBoxPreviewCache(maxBytes: 4, maxCount: 8);
        Assert.That(cache.Add("first", Preview("too long")), Is.False);
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Null);
            Assert.That(cache.Count, Is.EqualTo(0));
            Assert.That(cache.Bytes, Is.EqualTo(0));
        }
    }

    [Test]
    public void Add_EvictsTheOldestPreviewWhenTheCacheIsFullOfBytes() {
        var cache = new MBoxPreviewCache(maxBytes: 20, maxCount: 8);
        cache.Add("first", Preview("123456"));
        cache.Add("second", Preview("123456"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Null);
            Assert.That(cache.Get("second"), Is.Not.Null);
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Bytes, Is.EqualTo(12));
        }
    }

    [Test]
    public void Add_EvictsTheOldestPreviewWhenTheCacheIsFullOfPreviews() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 2);
        cache.Add("first", Preview("1"));
        cache.Add("second", Preview("2"));
        cache.Add("third", Preview("3"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Null);
            Assert.That(cache.Get("second"), Is.Not.Null);
            Assert.That(cache.Get("third"), Is.Not.Null);
            Assert.That(cache.Count, Is.EqualTo(2));
        }
    }

    [Test]
    public void Get_MakesThePreviewTheOneThatIsEvictedLast() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 2);
        cache.Add("first", Preview("1"));
        cache.Add("second", Preview("2"));
        _ = cache.Get("first");
        cache.Add("third", Preview("3"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Not.Null);
            Assert.That(cache.Get("second"), Is.Null);
            Assert.That(cache.Get("third"), Is.Not.Null);
        }
    }

    [Test]
    public void Add_ReplacesThePreviewOfTheSameKey() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 8);
        cache.Add("first", Preview("1"));
        cache.Add("first", Preview("12345"));
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Bytes, Is.EqualTo(10));
        }
    }

    [Test]
    public void Remove_TakesTheGivenPreviewsOutOfTheCache() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 8);
        cache.Add("first", Preview("1"));
        cache.Add("second", Preview("2"));
        cache.Remove(new[] { "first" });
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Null);
            Assert.That(cache.Get("second"), Is.Not.Null);
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.Bytes, Is.EqualTo(2));
        }
    }

    [Test]
    public void Clear_TakesEveryPreviewOutOfTheCache() {
        var cache = new MBoxPreviewCache(maxBytes: 1024, maxCount: 8);
        cache.Add("first", Preview("1"));
        cache.Add("second", Preview("2"));
        cache.Clear();
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Get("first"), Is.Null);
            Assert.That(cache.Count, Is.EqualTo(0));
            Assert.That(cache.Bytes, Is.EqualTo(0));
        }
    }

    [Test]
    public void Size_CountsUtf16BytesOfBothTextAndHtml() {
        var preview = new MBoxPreviewResult {
            Text = "é😀",
            Html = "x",
            State = MBoxBodyState.Complete,
        };
        var cache = new MBoxPreviewCache(maxBytes: 8, maxCount: 8);
        Assert.That(preview.Size, Is.EqualTo(8));
        Assert.That(cache.Add("unicode", preview), Is.True);
        Assert.That(cache.Bytes, Is.EqualTo(8));
    }

    [Test]
    public void Add_DoesNotRetainAUnicodePreviewOverTheByteLimit() {
        var cache = new MBoxPreviewCache(maxBytes: 4, maxCount: 8);
        Assert.That(cache.Add("unicode", Preview("é😀")), Is.False);
        using (Assert.EnterMultipleScope()) {
            Assert.That(cache.Count, Is.EqualTo(0));
            Assert.That(cache.Bytes, Is.EqualTo(0));
        }
    }
}
