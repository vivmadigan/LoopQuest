using System.Reflection;
using FluentValidation;
using LoopQuest.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace LoopQuest.Application;

/// <summary>
/// Composition root for the Application layer. The API calls <see cref="AddApplication"/> to register
/// MediatR, the validators, and the cross-cutting pipeline behaviours in one place.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();

        // Discover every IRequestHandler / INotificationHandler in this assembly.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));

        // Discover every AbstractValidator in this assembly.
        services.AddValidatorsFromAssembly(applicationAssembly);

        // Behaviours wrap each request in registration order:
        //   Logging( Validation( Handler ) )
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
