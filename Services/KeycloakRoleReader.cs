using System.Security.Claims;
using System.Text.Json;

namespace Platform.ProductCoverUpload.Function.Services;

public static class KeycloakRoleReader
{
    private const string RealmAccessClaimType = "realm_access";
    private const string ResourceAccessClaimType = "resource_access";
    private const string RolesPropertyName = "roles";

    public static IEnumerable<string> ReadRoles(ClaimsPrincipal principal)
    {
        foreach (var role in principal.FindAll(ClaimTypes.Role).Select(x => x.Value))
        {
            yield return role;
        }

        foreach (var role in principal.FindAll(RolesPropertyName).Select(x => x.Value))
        {
            yield return role;
        }

        foreach (var role in ReadRealmRoles(principal))
        {
            yield return role;
        }

        foreach (var role in ReadClientRoles(principal))
        {
            yield return role;
        }
    }

    private static IEnumerable<string> ReadRealmRoles(ClaimsPrincipal principal)
    {
        var realmAccess = principal.FindFirst(RealmAccessClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
            yield break;

        using var document = JsonDocument.Parse(realmAccess);
        if (!document.RootElement.TryGetProperty(RolesPropertyName, out var rolesElement) ||
            rolesElement.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var role in ReadRolesFromJsonArray(rolesElement))
        {
            yield return role;
        }
    }

    private static IEnumerable<string> ReadClientRoles(ClaimsPrincipal principal)
    {
        var resourceAccess = principal.FindFirst(ResourceAccessClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(resourceAccess))
            yield break;

        using var document = JsonDocument.Parse(resourceAccess);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var client in document.RootElement.EnumerateObject())
        {
            if (!client.Value.TryGetProperty(RolesPropertyName, out var rolesElement) ||
                rolesElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var role in ReadRolesFromJsonArray(rolesElement))
            {
                yield return role;
            }
        }
    }

    private static IEnumerable<string> ReadRolesFromJsonArray(JsonElement rolesElement)
    {
        foreach (var role in rolesElement.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(role.GetString()))
                yield return role.GetString()!;
        }
    }
}
