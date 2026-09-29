namespace SmartShopPOS.Domain.Identity;

internal static class CatalogValidation
{
    public static string? OptionalValue(string? value, string parameterName, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{parameterName} must not exceed {maxLength} characters.");
        }

        return trimmed;
    }

    public static string? OptionalDescription(string? description)
    {
        return OptionalValue(description, nameof(description), 1000);
    }
}