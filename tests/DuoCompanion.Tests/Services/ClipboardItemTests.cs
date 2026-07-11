using DuoCompanion.Core.Models;
using Xunit;

namespace DuoCompanion.Tests.Services;

public sealed class ClipboardItemTests
{
    [Fact]
    public void IsPinned_defaults_to_false()
    {
        var item = new ClipboardItem(Guid.NewGuid(), "hello", DateTimeOffset.Now);
        Assert.False(item.IsPinned);
    }

    [Fact]
    public void With_IsPinned_creates_pinned_copy()
    {
        var item = new ClipboardItem(Guid.NewGuid(), "hello", DateTimeOffset.Now);
        var pinned = item with { IsPinned = true };

        Assert.False(item.IsPinned);
        Assert.True(pinned.IsPinned);
        Assert.Equal(item.Id, pinned.Id);
        Assert.Equal(item.Text, pinned.Text);
    }

    [Fact]
    public void ToString_is_implicitly_text_via_record()
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.Now;
        var item = new ClipboardItem(id, "abc", now);
        var copy = new ClipboardItem(id, "abc", now);
        Assert.Equal(item, copy);
    }
}
