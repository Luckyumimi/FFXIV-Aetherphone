using System.Globalization;
using Aetherphone.Core;
using Aetherphone.Core.Aethernet;
using Aetherphone.Core.Apps;
using Aetherphone.Core.Confirm;
using Aetherphone.Core.Localization;
using Aetherphone.Core.Platform;
using Aetherphone.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;

namespace Aetherphone.Apps.Settings.Pages;

internal sealed class ImportAccountPage : ISettingsPage
{
    public string Title => Loc.T(L.Account.ImportTitle);
    public string Summary => string.Empty;
    public FontAwesomeIcon Icon => FontAwesomeIcon.FileImport;
    public Vector4 Tint => new(0.36f, 0.72f, 0.62f, 1f);

    private readonly Configuration configuration;
    private readonly ConfirmService confirm;
    private List<ImportedAccount>? accounts;
    private readonly HashSet<ulong> selected = new();
    private AccountImportError failure;

    public ImportAccountPage(Configuration configuration, ConfirmService confirm)
    {
        this.configuration = configuration;
        this.confirm = confirm;
    }

    public void Draw(in PhoneContext context, Rect body)
    {
        var theme = context.Theme;
        var scale = UiScale.Current;
        using (AppSurface.Begin(body))
        {
            SettingsSection.Header(Loc.T(L.Account.ImportTitle), theme);
            var picker = GroupCard.Begin(theme, 1);
            if (SettingsRow.Action(picker.NextRow(), Loc.T(L.Account.ImportPickFile), theme.Accent, theme))
            {
                FilePicker.PickJson(Loc.T(L.Account.ImportPickFile), PickFile);
            }

            picker.End();
            SettingsSection.Hint(Loc.T(L.Account.ImportFileHint), theme);
            if (failure != AccountImportError.None)
            {
                SettingsSection.Hint(Loc.T(FailureText(failure)), theme);
            }

            if (accounts is { Count: > 0 })
            {
                ImGui.Dummy(new Vector2(0f, 6f * scale));
                SettingsSection.Header(Loc.T(L.Account.ImportAccountsHeading), theme);
                var list = GroupCard.Begin(theme, accounts.Count);
                for (var index = 0; index < accounts.Count; index++)
                {
                    var account = accounts[index];
                    var isSelected = selected.Contains(account.ContentId);
                    if (SettingsRow.Selectable(list.NextRow(), RowLabel(account), isSelected, theme))
                    {
                        if (isSelected)
                        {
                            selected.Remove(account.ContentId);
                        }
                        else
                        {
                            selected.Add(account.ContentId);
                        }
                    }
                }

                list.End();
                if (selected.Count > 0)
                {
                    ImGui.Dummy(new Vector2(0f, 6f * scale));
                    var actions = GroupCard.Begin(theme, 1);
                    if (SettingsRow.Action(actions.NextRow(), Loc.T(L.Account.ImportAction, selected.Count),
                            theme.Accent, theme))
                    {
                        ConfirmImport();
                    }

                    actions.End();
                }
            }
        }
    }

    private void PickFile(string path)
    {
        failure = AccountImportError.None;
        selected.Clear();
        if (!AccountImport.TryParse(path, out var parsed, out var error))
        {
            failure = error;
            accounts = null;
            return;
        }

        accounts = parsed;
    }

    private void ConfirmImport()
    {
        var count = selected.Count;
        confirm.Ask(new ConfirmRequest
        {
            Title = Loc.T(L.Account.ImportConfirmTitle),
            Message = Loc.T(L.Account.ImportConfirmBody, count),
            ConfirmLabel = Loc.T(L.Account.ImportAction, count),
            CancelLabel = Loc.T(L.Common.Cancel),
            Danger = false,
            Confirm = ImportSelected,
        });
    }

    private void ImportSelected()
    {
        if (accounts is null)
        {
            return;
        }

        var imported = 0;
        foreach (var account in accounts)
        {
            if (!selected.Contains(account.ContentId))
            {
                continue;
            }

            configuration.CharacterSessions[account.ContentId] = new CharacterSession
            {
                Token = account.Token,
                EncryptionKeyCache = account.EncryptionKeyCache,
                EncryptionKeyCacheUserId = account.EncryptionKeyCacheUserId,
                AccountId = account.AccountId,
                Handle = account.Handle,
                DisplayName = account.DisplayName,
                CharacterName = account.CharacterName,
                World = account.World,
                AvatarUrl = account.AvatarUrl,
            };
            imported++;
        }

        if (imported > 0)
        {
            configuration.Save();
        }

        accounts = null;
        selected.Clear();
        confirm.Alert(Loc.T(L.Account.ImportDoneTitle), Loc.T(L.Account.ImportDoneBody, imported),
            Loc.T(L.Common.Close));
    }

    private static string RowLabel(ImportedAccount account)
    {
        var name = account.CharacterName.Length > 0
            ? account.CharacterName
            : account.Handle.Length > 0
                ? account.Handle
                : account.ContentId.ToString(CultureInfo.InvariantCulture);
        return account.World.Length > 0 ? $"{name} @ {account.World}" : name;
    }

    private static LocString FailureText(AccountImportError error) =>
        error == AccountImportError.Unreadable ? L.Account.ImportUnreadable : L.Account.ImportBadFile;
}
