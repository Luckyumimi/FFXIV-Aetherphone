using System.Globalization;
using System.Text.Json;

namespace Aetherphone.Core.Aethernet;

internal enum AccountImportError
{
    None,
    Unreadable,
    NoAccounts,
}

internal sealed record ImportedAccount(ulong ContentId, string Token, string EncryptionKeyCache,
    string EncryptionKeyCacheUserId, string AccountId, string Handle, string DisplayName, string CharacterName,
    string World, string AvatarUrl);

internal static class AccountImport
{
    public static bool TryParse(string path, out List<ImportedAccount> accounts, out AccountImportError error)
    {
        accounts = new List<ImportedAccount>();
        error = AccountImportError.None;
        JsonDocument document;
        try
        {
            var text = File.ReadAllText(path);
            document = JsonDocument.Parse(text);
        }
        catch (Exception)
        {
            error = AccountImportError.Unreadable;
            return false;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("CharacterSessions", out var sessions) ||
                sessions.ValueKind != JsonValueKind.Object)
            {
                error = AccountImportError.NoAccounts;
                return false;
            }

            foreach (var property in sessions.EnumerateObject())
            {
                if (string.Equals(property.Name, "$type", StringComparison.Ordinal) ||
                    !ulong.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out var contentId))
                {
                    continue;
                }

                var entry = property.Value;
                var token = Text(entry, "Token");
                if (token.Length == 0)
                {
                    continue;
                }

                accounts.Add(new ImportedAccount(contentId, token, Text(entry, "EncryptionKeyCache"),
                    Text(entry, "EncryptionKeyCacheUserId"), Text(entry, "AccountId"), Text(entry, "Handle"),
                    Text(entry, "DisplayName"), Text(entry, "CharacterName"), Text(entry, "World"),
                    Text(entry, "AvatarUrl")));
            }
        }

        if (accounts.Count == 0)
        {
            error = AccountImportError.NoAccounts;
            return false;
        }

        return true;
    }

    private static string Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
