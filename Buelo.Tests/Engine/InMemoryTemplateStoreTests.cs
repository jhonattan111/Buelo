using Buelo.Contracts;
using Buelo.Engine;

namespace Buelo.Tests.Engine;

public class InMemoryTemplateStoreTests
{
    [Fact]
    public async Task SaveAsync_WithEmptyId_ShouldAssignIdAndPersistTemplate()
    {
        var store = new InMemoryTemplateStore();
        var template = new TemplateRecord
        {
            Id = Guid.Empty,
            Name = "Invoice",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"ok\"))).GeneratePdf()"
        };

        var saved = await store.SaveAsync(template);
        var loaded = await store.GetAsync(saved.Id);

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Invoice", loaded!.Name);
    }

    [Fact]
    public async Task SaveAsync_OnUpdate_ShouldRefreshUpdatedAt()
    {
        var store = new InMemoryTemplateStore();
        var template = await store.SaveAsync(new TemplateRecord
        {
            Id = Guid.Empty,
            Name = "Invoice",
            Template = "Document.Create(c => c.Page(p => p.Content().Text(\"ok\"))).GeneratePdf()"
        });

        var firstUpdatedAt = template.UpdatedAt;
        await Task.Delay(10);

        template.Description = "Updated";
        var updated = await store.SaveAsync(template);

        Assert.True(updated.UpdatedAt >= firstUpdatedAt);
        Assert.Equal("Updated", updated.Description);
    }

    [Fact]
    public async Task DeleteAsync_WhenTemplateDoesNotExist_ShouldReturnFalse()
    {
        var store = new InMemoryTemplateStore();

        var deleted = await store.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }
}
