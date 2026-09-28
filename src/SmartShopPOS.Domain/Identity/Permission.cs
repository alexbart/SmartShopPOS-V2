using System.Text.RegularExpressions;

namespace SmartShopPOS.Domain.Identity;

public sealed class Permission : Entity
{
    private static readonly Regex ValidKey = new("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$", RegexOptions.Compiled);

    private Permission()
    {
    }

    public Permission(string key, string name, string? description = null)
    {
        Key = Organization.Required(key, nameof(key), 100).ToLowerInvariant();
        if (!ValidKey.IsMatch(Key))
        {
            throw new DomainException("Permission key must be a stable lowercase identifier using letters, numbers, dots, or hyphens.");
        }

        Name = Organization.Required(name, nameof(name), 120);
        Description = description?.Trim();
        if (Description?.Length > 500)
        {
            throw new DomainException("Permission description must not exceed 500 characters.");
        }
    }

    public string Key { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
}