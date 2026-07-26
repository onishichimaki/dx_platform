namespace DxPlatform.Application.Validation;

public interface IUrlSafetyValidator
{
    bool IsSafeHttpUrl(string? value, bool allowEmpty = true);
}

public sealed class UrlSafetyValidator : IUrlSafetyValidator
{
    public bool IsSafeHttpUrl(string? value, bool allowEmpty = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return allowEmpty;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(uri.UserInfo)
            && !string.IsNullOrWhiteSpace(uri.Host);
    }
}

