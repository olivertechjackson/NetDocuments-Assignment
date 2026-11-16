using GameOfLife.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameOfLife.Tests.Unit;

public sealed class GameSettingsValidationTests
{
    private static IOptions<GameSettings> BuildOptions(Dictionary<string, string?> settings)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();

        services
            .AddOptions<GameSettings>()
            .Bind(configuration.GetSection(GameSettings.SectionName))
            .ValidateDataAnnotations();

        return services.BuildServiceProvider().GetRequiredService<IOptions<GameSettings>>();
    }

    [Fact]
    public void GameSettings_ValidConfiguration_BindsAllValues()
    {
        IOptions<GameSettings> options = BuildOptions(new Dictionary<string, string?>
        {
            ["Game:MaxGridDimension"] = "100",
            ["Game:MaxAllowedSteps"] = "5000",
            ["Game:MaxFinalIterations"] = "250",
        });

        GameSettings settings = options.Value;

        Assert.Equal(100, settings.MaxGridDimension);
        Assert.Equal(5000, settings.MaxAllowedSteps);
        Assert.Equal(250, settings.MaxFinalIterations);
    }

    [Theory]
    [InlineData("Game:MaxGridDimension", "0")]
    [InlineData("Game:MaxAllowedSteps", "-5")]
    [InlineData("Game:MaxFinalIterations", "0")]
    public void GameSettings_OutOfRangeValue_FailsDataAnnotationValidation(string key, string value)
    {
        IOptions<GameSettings> options = BuildOptions(new Dictionary<string, string?> { [key] = value });

        Assert.Throws<OptionsValidationException>(() => options.Value);
    }
}
