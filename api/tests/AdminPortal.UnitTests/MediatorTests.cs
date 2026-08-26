using AdminPortal.Application.Common.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace AdminPortal.UnitTests;

public sealed class MediatorTests
{
    [Fact]
    public async Task SendDispatchesRequestToRegisteredHandler()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAppMediator, DefaultAppMediator>();
        services.AddScoped<IAppRequestHandler<EchoQuery, string>, EchoQueryHandler>();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IAppMediator>();

        var result = await mediator.Send(new EchoQuery("hello"), CancellationToken.None);

        Assert.Equal("handled: hello", result);
    }

    [Fact]
    public async Task SendThrowsWhenHandlerIsMissing()
    {
        var services = new ServiceCollection();
        services.AddScoped<IAppMediator, DefaultAppMediator>();
        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IAppMediator>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => mediator.Send(new EchoQuery("hello"), CancellationToken.None));

        Assert.Contains(nameof(EchoQuery), exception.Message, StringComparison.Ordinal);
    }

    private sealed record EchoQuery(string Text) : IAppQuery<string>;

    private sealed class EchoQueryHandler : IAppRequestHandler<EchoQuery, string>
    {
        public Task<string> Handle(EchoQuery request, CancellationToken cancellationToken) =>
            Task.FromResult($"handled: {request.Text}");
    }
}
