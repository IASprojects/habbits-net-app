using System.Text.Json;
using System.Text.Json.Serialization;
using HabitsApp.Domain.Enums;

namespace HabitsApp.Application.Tests;

public class HabitPeriodSerializationTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    [Fact]
    public void Undefined_InvalidString_IsRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DayPeriod?>("\"Midday\"", Options));
    }

    [Fact]
    public void NumericValue_IsRejected()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<DayPeriod?>("999", Options));
    }

    [Fact]
    public void Null_IsAccepted()
    {
        Assert.Null(JsonSerializer.Deserialize<DayPeriod?>("null", Options));
    }

    [Theory]
    [InlineData("\"Any\"", DayPeriod.Any)]
    [InlineData("\"Morning\"", DayPeriod.Morning)]
    [InlineData("\"Afternoon\"", DayPeriod.Afternoon)]
    [InlineData("\"Night\"", DayPeriod.Night)]
    public void ValidPeriods_AreAccepted(string json, DayPeriod expected)
    {
        Assert.Equal(expected, JsonSerializer.Deserialize<DayPeriod?>(json, Options));
    }
}