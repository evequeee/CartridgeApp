using CartridgeApp.Converters;
using CartridgeApp.Models;
using CartridgeApp.Services;

namespace CartridgeApp.Tests;

public class ConverterAndBackupTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("game", true)]
    public void EmptyConverterHandlesText(string? value, bool expected)
    {
        var converter = new IsNotNullOrEmptyConverter();

        Assert.Equal(expected, converter.Convert(value, typeof(bool), null, null!));
    }

    [Theory]
    [InlineData(GameStatus.Completed, 1)]
    [InlineData(GameStatus.Backlog, 1)]
    public void StatusConverterReturnsAColor(GameStatus status, int expected)
    {
        var converter = new StatusColorConverter();

        object result = converter.Convert(status, typeof(Color), null, null!);

        Assert.IsType<Color>(result);
    }

    [Fact]
    public async Task JsonBackupExportsAndImportsWithoutDuplicates()
    {
        var repository = new MockGameService();
        var backup = new JsonBackupService(repository);

        string json = await backup.ExportAsync();

        Assert.Contains("Persona 4 Golden", json);
        Assert.Equal(0, await backup.ImportAsync(json));
    }
}
