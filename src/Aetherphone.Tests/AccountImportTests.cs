using Aetherphone.Core.Aethernet;
using Xunit;

namespace Aetherphone.Tests;

public sealed class AccountImportTests
{
    private const string SampleJson = """
        {
          "OtherField": 1,
          "CharacterSessions": {
            "$type": "System.Collections.Generic.Dictionary`2[[System.UInt64, System.Private.CoreLib],[Aetherphone.Core.Aethernet.CharacterSession, Aetherphone]], System.Private.CoreLib",
            "18014398558011676": {
              "Token": "ABC123",
              "EncryptionKeyCache": "KEY",
              "EncryptionKeyCacheUserId": "01a012713a577aa49b5059f75b817951",
              "AccountId": "01a012713a577aa49b5059f75b817951",
              "Handle": "9d4ed1badf",
              "DisplayName": "Mi Sachi",
              "CharacterName": "Mi Sachi",
              "World": "Chocobo",
              "AvatarUrl": "https://example.com/a.jpg"
            },
            "18014469511243409": {
              "Token": "",
              "CharacterName": "Empty",
              "World": "X"
            }
          }
        }
        """;

    [Fact]
    public void ParsesAccountsAndSkipsEmptyTokens()
    {
        using var temp = TempFile(SampleJson);
        Assert.True(AccountImport.TryParse(temp.Path, out var accounts, out var error));
        Assert.Equal(AccountImportError.None, error);
        var account = Assert.Single(accounts);
        Assert.Equal(18014398558011676ul, account.ContentId);
        Assert.Equal("ABC123", account.Token);
        Assert.Equal("KEY", account.EncryptionKeyCache);
        Assert.Equal("01a012713a577aa49b5059f75b817951", account.EncryptionKeyCacheUserId);
        Assert.Equal("9d4ed1badf", account.Handle);
        Assert.Equal("Mi Sachi", account.DisplayName);
        Assert.Equal("Mi Sachi", account.CharacterName);
        Assert.Equal("Chocobo", account.World);
        Assert.Equal("https://example.com/a.jpg", account.AvatarUrl);
    }

    [Fact]
    public void MissingSessionsIsNotAnErrorButHasNothing()
    {
        using var temp = TempFile("{\"AethernetToken\": \"X\"}");
        Assert.False(AccountImport.TryParse(temp.Path, out var accounts, out var error));
        Assert.Empty(accounts);
        Assert.Equal(AccountImportError.NoAccounts, error);
    }

    [Fact]
    public void OnlyEmptyTokensIsNoAccounts()
    {
        using var temp = TempFile("""
            {
              "CharacterSessions": {
                "18014398558011676": { "Token": "", "CharacterName": "A", "World": "B" }
              }
            }
            """);
        Assert.False(AccountImport.TryParse(temp.Path, out var accounts, out var error));
        Assert.Empty(accounts);
        Assert.Equal(AccountImportError.NoAccounts, error);
    }

    [Fact]
    public void BrokenJsonIsUnreadable()
    {
        using var temp = TempFile("this is not json");
        Assert.False(AccountImport.TryParse(temp.Path, out var accounts, out var error));
        Assert.Empty(accounts);
        Assert.Equal(AccountImportError.Unreadable, error);
    }

    [Fact]
    public void MissingFileIsUnreadable()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        Assert.False(AccountImport.TryParse(path, out var accounts, out var error));
        Assert.Empty(accounts);
        Assert.Equal(AccountImportError.Unreadable, error);
    }

    private static TempFileHandle TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, content);
        return new TempFileHandle(path);
    }

    private sealed class TempFileHandle : IDisposable
    {
        public string Path { get; }

        public TempFileHandle(string path)
        {
            Path = path;
        }

        public void Dispose()
        {
            try
            {
                File.Delete(Path);
            }
            catch (IOException)
            {
            }
        }
    }
}
