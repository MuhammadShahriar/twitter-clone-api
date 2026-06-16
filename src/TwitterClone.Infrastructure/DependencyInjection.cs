using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TwitterClone.Application.Common.Interfaces;
using TwitterClone.Infrastructure.Persistence;
using TwitterClone.Infrastructure.Persistence.Repositories;

namespace TwitterClone.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = ConnectionStringResolver.Resolve(configuration);

        // AddDbContext registers the context as Scoped, so the repositories and the
        // unit of work below all share the SAME context instance per request — which
        // is what lets the UoW commit the changes the repositories staged.
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ITweetRepository, TweetRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
