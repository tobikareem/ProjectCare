using CarePath.WebApi.ModelBinding;
using CarePath.WebApi.Serialization;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;

namespace CarePath.Application.Tests.WebApi;

public sealed class UtcDateTimeModelBinderTests
{
    [Fact]
    public async Task BindModelAsync_WhenInnerBinderProducesUnspecifiedKind_NormalizesToUtc()
    {
        var unspecified = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var binder = new UtcDateTimeModelBinder(new StubModelBinder(ModelBindingResult.Success(unspecified)));
        var bindingContext = CreateBindingContext();

        await binder.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        var bound = (DateTime)bindingContext.Result.Model!;
        bound.Kind.Should().Be(DateTimeKind.Utc);
        bound.Should().Be(new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task BindModelAsync_WhenInnerBinderFails_LeavesResultUnchanged()
    {
        var binder = new UtcDateTimeModelBinder(new StubModelBinder(ModelBindingResult.Failed()));
        var bindingContext = CreateBindingContext();

        await binder.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeFalse();
    }

    [Fact]
    public async Task BindModelAsync_WhenInnerBinderProducesNullModel_LeavesResultUnchanged()
    {
        var binder = new UtcDateTimeModelBinder(new StubModelBinder(ModelBindingResult.Success(null)));
        var bindingContext = CreateBindingContext(typeof(DateTime?));

        await binder.BindModelAsync(bindingContext);

        bindingContext.Result.IsModelSet.Should().BeTrue();
        bindingContext.Result.Model.Should().BeNull();
    }

    [Theory]
    [InlineData(typeof(DateTime))]
    [InlineData(typeof(DateTime?))]
    public void GetBinder_WhenModelTypeIsDateTime_ReturnsUtcBinder(Type modelType)
    {
        var provider = new UtcDateTimeModelBinderProvider();

        var binder = provider.GetBinder(new TestModelBinderProviderContext(modelType));

        binder.Should().BeOfType<UtcDateTimeModelBinder>();
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(DateTimeOffset))]
    public void GetBinder_WhenModelTypeIsNotDateTime_ReturnsNull(Type modelType)
    {
        var provider = new UtcDateTimeModelBinderProvider();

        var binder = provider.GetBinder(new TestModelBinderProviderContext(modelType));

        binder.Should().BeNull();
    }

    private static DefaultModelBindingContext CreateBindingContext(Type? modelType = null)
    {
        return new DefaultModelBindingContext
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType ?? typeof(DateTime)),
            ModelName = "timestamp",
            ModelState = new ModelStateDictionary(),
        };
    }

    private sealed class StubModelBinder : IModelBinder
    {
        private readonly ModelBindingResult result;

        public StubModelBinder(ModelBindingResult result)
        {
            this.result = result;
        }

        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            bindingContext.Result = result;
            return Task.CompletedTask;
        }
    }

    private sealed class TestModelBinderProviderContext : ModelBinderProviderContext
    {
        private readonly ModelMetadata metadata;
        private readonly IServiceProvider services;

        public TestModelBinderProviderContext(Type modelType)
        {
            metadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType);
            services = new ServiceCollection().AddLogging().BuildServiceProvider();
        }

        public override BindingInfo BindingInfo { get; } = new();

        public override ModelMetadata Metadata => metadata;

        public override IModelMetadataProvider MetadataProvider => new EmptyModelMetadataProvider();

        public override IServiceProvider Services => services;

        public override IModelBinder CreateBinder(ModelMetadata metadata) =>
            throw new NotSupportedException("Not required for these tests.");
    }
}
