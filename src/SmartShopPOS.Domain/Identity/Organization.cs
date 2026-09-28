using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class Organization : Entity
{
    private static readonly Regex ValidCode = new("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    private Organization()
    {
    }

    public Organization(string name, string code)
    {
        Name = Required(name, nameof(name), 200);
        Code = NormalizeCode(code);
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string NormalizeCode(string code)
    {
        var normalized = Required(code, nameof(code), 63).ToLowerInvariant();
        if (!ValidCode.IsMatch(normalized))
        {
            throw new DomainException("Organization code must contain lowercase letters, numbers, and single hyphens only.");
        }

        return normalized;
    }

    internal static string Required(string value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.Length > maxLength)
        {
            throw new DomainException($"{parameterName} is required and must not exceed {maxLength} characters.");
        }

        return trimmed;
    }
}