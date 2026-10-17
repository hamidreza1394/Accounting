using Microsoft.Extensions.Logging.Abstractions;
using Server.Infrastructure.DatabaseSetup;
using Xunit;

namespace Template.Tests;

public sealed class DatabaseSetupValidationTests
{
    [Fact]
    public async Task ConfigureAsync_WhenServerIsMissing_ReturnsValidationFailure()
    {
        var store = new DatabaseConnectionSettingsStore(
            NullLogger<DatabaseConnectionSettingsStore>.Instance);
        var service = new DatabaseConnectionSetupService(
            store,
            NullLogger<DatabaseConnectionSetupService>.Instance);

        var result = await service.ConfigureAsync(new DatabaseSetupRequest
        {
            Server = string.Empty,
            Database = "TarazAva_Test"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("نام سرور و نام پایگاه داده الزامی است؛ نام دیتابیس حداکثر ۱۲۸ نویسه است.", result.ErrorMessage);
    }

    [Fact]
    public async Task ConfigureAsync_WhenAuthenticationModeIsInvalid_ReturnsValidationFailure()
    {
        var store = new DatabaseConnectionSettingsStore(
            NullLogger<DatabaseConnectionSettingsStore>.Instance);
        var service = new DatabaseConnectionSetupService(
            store,
            NullLogger<DatabaseConnectionSetupService>.Instance);

        var result = await service.ConfigureAsync(new DatabaseSetupRequest
        {
            Server = "localhost",
            Database = "TarazAva_Test",
            AuthenticationMode = "Unknown"
        });

        Assert.False(result.Succeeded);
        Assert.Equal("نوع احراز هویت انتخاب‌شده معتبر نیست.", result.ErrorMessage);
    }

    [Fact]
    public async Task ConfigureAsync_WhenSqlAuthenticationHasNoPassword_ReturnsValidationFailure()
    {
        var store = new DatabaseConnectionSettingsStore(
            NullLogger<DatabaseConnectionSettingsStore>.Instance);
        var service = new DatabaseConnectionSetupService(
            store,
            NullLogger<DatabaseConnectionSetupService>.Instance);

        var result = await service.ConfigureAsync(new DatabaseSetupRequest
        {
            Server = "localhost",
            Database = "TarazAva_Test",
            AuthenticationMode = "Sql",
            UserName = "test-user",
            Password = string.Empty
        });

        Assert.False(result.Succeeded);
        Assert.Equal("برای ورود SQL Server، نام کاربری و رمز عبور لازم است.", result.ErrorMessage);
    }
}
