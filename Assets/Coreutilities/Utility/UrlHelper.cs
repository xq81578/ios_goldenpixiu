using System.Linq;

public static class UrlHelper
{
    public static string Combine(params string[] parts)
    {
        if (parts == null || parts.Length == 0) return string.Empty;

        return string.Join("/", parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim('/')));
    }
}