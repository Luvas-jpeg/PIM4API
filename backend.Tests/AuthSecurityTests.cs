using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using EquipamentosMedicosApi.Data;
using EquipamentosMedicosApi.Models;
using EquipamentosMedicosApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace backend.Tests;

public sealed class AuthSecurityTests
{
    [Fact]
    public async Task RefreshCredentialCanBeRevokedWithoutAccessTokenAndWithoutAffectingAnotherSession()
    {
        await using var fixture=await TestFixture.CreateAsync();
        await fixture.AddRefreshTokenAsync(Hash("old-access-session"));
        await fixture.AddRefreshTokenAsync(Hash("another-session"));
        await fixture.CreateService().RevokeRefreshAsync("unknown");
        await fixture.CreateService().RevokeRefreshAsync("old-access-session");
        Assert.False((await fixture.CreateService().RefreshAsync("old-access-session")).Success);
        Assert.True((await fixture.CreateService().RefreshAsync("another-session")).Success);
    }

    [Fact]
    public async Task LogoutWithRefreshTokenRevokesOnlyThatSession()
    {
        await using var fixture=await TestFixture.CreateAsync();
        await fixture.AddRefreshTokenAsync(Hash("session-one"));
        await fixture.AddRefreshTokenAsync(Hash("session-two"));
        var userId=await fixture.Context.Users.Select(u=>u.ID).SingleAsync();
        await fixture.CreateService().LogoutAsync(userId,"session-one");
        Assert.False((await fixture.CreateService().RefreshAsync("session-one")).Success);
        Assert.True((await fixture.CreateService().RefreshAsync("session-two")).Success);
    }

    [Fact]
    public async Task RefreshRotatesHashedTokenAndRejectsReuse()
    {
        await using var fixture = await TestFixture.CreateAsync();
        const string rawToken = "refresh-token-known-only-by-the-client";
        await fixture.AddRefreshTokenAsync(Hash(rawToken));
        var service = fixture.CreateService();

        var first = await service.RefreshAsync(rawToken);
        var reused = await service.RefreshAsync(rawToken);

        Assert.True(first.Success);
        Assert.NotNull(first.Data);
        Assert.False(reused.Success);
        Assert.NotEqual(rawToken, first.Data!.RefreshToken);

        fixture.Context.ChangeTracker.Clear();
        var tokens = await fixture.Context.RefreshTokens.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, tokens.Count);
        Assert.Equal(Hash(rawToken), tokens[0].Token);
        Assert.NotNull(tokens[0].RevokedAt);
        Assert.Equal(Hash(first.Data.RefreshToken), tokens[0].ReplacedByToken);
        Assert.Equal(Hash(first.Data.RefreshToken), tokens[1].Token);
        Assert.Null(tokens[1].RevokedAt);
    }

    [Fact]
    public async Task RefreshMigratesLegacyPlainTextTokenToHash()
    {
        await using var fixture = await TestFixture.CreateAsync();
        const string legacyToken = "legacy-refresh-token-stored-as-plain-text";
        await fixture.AddRefreshTokenAsync(legacyToken);

        var result = await fixture.CreateService().RefreshAsync(legacyToken);

        Assert.True(result.Success);
        fixture.Context.ChangeTracker.Clear();
        var oldToken = await fixture.Context.RefreshTokens.OrderBy(item => item.Id).FirstAsync();
        Assert.Equal(Hash(legacyToken), oldToken.Token);
        Assert.NotNull(oldToken.RevokedAt);
    }

    private static string Hash(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private readonly DbConnection _connection;
        public AppDbContext Context { get; }
        private User User { get; }

        private TestFixture(DbConnection connection, AppDbContext context, User user)
        {
            _connection = connection;
            Context = context;
            User = user;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new AppDbContext(options);
            await context.Database.EnsureCreatedAsync();
            var user = new User
            {
                Nome = "Aluno Teste",
                Email = "auth.security@example.com",
                Cpf = "52998224725",
                SenhaHash = "hash",
                Role = "Cliente"
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return new TestFixture(connection, context, user);
        }

        public async Task AddRefreshTokenAsync(string token)
        {
            Context.RefreshTokens.Add(new RefreshToken
            {
                Token = token,
                UserId = User.ID,
                ExpiresAt = DateTime.UtcNow.AddDays(1)
            });
            await Context.SaveChangesAsync();
        }

        public AuthService CreateService()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Secret"] = "test-secret-with-at-least-thirty-two-characters",
                    ["Jwt:Issuer"] = "MediShopAPI.Tests",
                    ["Jwt:Audience"] = "MediShopFrontend.Tests",
                    ["Jwt:ExpirationHours"] = "1"
                })
                .Build();
            return new AuthService(Context, new TokenService(configuration));
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}