using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TwitterClone.Domain.Entities;
using TwitterClone.Infrastructure.Identity;

namespace TwitterClone.Infrastructure.Persistence;

/// <summary>
/// EF Core context. Concrete and confined to Infrastructure — the Application layer
/// talks to <c>IRepository</c>/<c>IUnitOfWork</c> abstractions and never sees this type.
///
/// Derives from <see cref="IdentityDbContext{TUser,TRole,TKey}"/> with a <see cref="Guid"/> key so the
/// Identity user/role schema lives in the same database as the domain tables and its keys map to native
/// <c>uuid</c> columns — consistent with <c>Tweet.Id</c>. Identity stays an infrastructure concern.
/// </summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Tweet> Tweets => Set<Tweet>();

    public DbSet<Like> Likes => Set<Like>();

    public DbSet<Retweet> Retweets => Set<Retweet>();

    public DbSet<Follow> Follows => Set<Follow>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's own entity mappings must be applied first (base call),
        // then our configurations (Tweet + ApplicationUser profile fields) layer on top.
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
