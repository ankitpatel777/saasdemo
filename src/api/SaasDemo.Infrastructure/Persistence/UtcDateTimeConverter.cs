using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SaasDemo.Infrastructure.Persistence;

/// <summary>Values are written as-is (always UTC in this app) and read back with Kind = Utc.</summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
