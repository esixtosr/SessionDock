using System.Security;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SessionDock.App.ViewModels;
using SessionDock.Core;

namespace SessionDock.App.Views;

internal sealed class CredentialEditor : Window
{
    public Guid? SavedCredentialId { get; private set; }
    public CredentialEditor(Window owner, LibraryViewModel vm, CredentialProfile? profile)
    {
        Owner = owner; Title = profile is null ? "Add credentials" : "Edit credentials"; Width = 520; Height = 545; MinHeight = 440; WindowStartupLocation = WindowStartupLocation.CenterOwner; Ui.FitToWorkArea(this);
        var draft = profile is null ? new CredentialProfile() : profile with { }; var stack = Ui.Stack(20);
        var label = Ui.Input("Friendly label", draft.Label, stack); var username = Ui.Input("Username (user, DOMAIN\\user, or user@domain)", draft.Username, stack); var domain = Ui.Input("Domain (optional; leave blank when included in username)", draft.Domain, stack); stack.Children.Add(Ui.Text("Password")); var password = new PasswordBox(); AutomationProperties.SetName(password, "Password"); stack.Children.Add(password);
        Ui.Note(stack, "Passwords are stored only in Windows Credential Manager for this Windows account. A blank password preserves an existing secret; a missing secret prompts at connection time."); var clear = Ui.Check("Deliberately clear the existing saved password", false, stack, profile is not null); Ui.Note(stack, "Editing this shared profile affects future sessions for every PC and gateway that references it.");
        Ui.Footer(stack, () => Ui.Safe(this, () =>
        {
            if (string.IsNullOrWhiteSpace(label.Text) || string.IsNullOrWhiteSpace(username.Text)) { MessageBox.Show(this, "Enter a label and username."); return; }
            using var secret = password.SecurePassword;
            if (clear.IsChecked == true && secret.Length > 0) { MessageBox.Show(this, "Choose either a new password or clear the existing password."); return; }
            draft.Label = label.Text.Trim(); draft.Username = username.Text.Trim(); draft.Domain = domain.Text.Trim();
            // SQLite and the Windows vault are separate stores; restore the prior secret if metadata saving fails.
            bool changesSecret = clear.IsChecked == true || secret.Length > 0;
            using var previousSecret = changesSecret ? vm.Vault.Read(draft.Id) : null;
            bool vaultChanged = false;
            try
            {
                if (clear.IsChecked == true) { vm.Vault.Delete(draft.Id); vaultChanged = true; }
                else if (secret.Length > 0) { vm.Vault.Save(draft.Id, draft.Username, secret); vaultChanged = true; }
                vm.Store.SaveCredential(draft);
            }
            catch (Exception saveError)
            {
                if (vaultChanged)
                {
                    try { if (previousSecret is null) vm.Vault.Delete(draft.Id); else vm.Vault.Save(draft.Id, profile?.Username ?? draft.Username, previousSecret); }
                    catch (Exception) { throw new InvalidOperationException("Credential metadata could not be saved, and restoring the earlier vault entry also failed. Review this saved credential before reconnecting.", saveError); }
                }
                throw;
            }
            vm.Reload(); password.Clear(); SavedCredentialId = draft.Id; DialogResult = true;
        }), () => DialogResult = false); Closed += (_, _) => password.Clear(); Content = Ui.Scroll(stack);
    }
}

internal sealed class CredentialPrompt : Window
{
    public SessionCredentials? Credentials { get; private set; }
    public CredentialPrompt(Window owner, string title, CredentialProfile? metadata)
    {
        Owner = owner; Title = title; Width = 500; Height = 425; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; Ui.FitToWorkArea(this);
        var stack = Ui.Stack(20); Ui.Note(stack, "Used for this connection only. This prompt does not save a password."); var user = Ui.Input("Username", metadata?.Username, stack); var domain = Ui.Input("Domain (optional)", metadata?.Domain, stack); stack.Children.Add(Ui.Text("Password")); var password = new PasswordBox(); AutomationProperties.SetName(password, "Password"); stack.Children.Add(password);
        var actions = Ui.Footer(stack, () => { if (string.IsNullOrWhiteSpace(user.Text)) { MessageBox.Show(this, "Enter a username."); return; } using var secret = password.SecurePassword; Credentials = new SessionCredentials(user.Text.Trim(), secret, domain.Text.Trim()); password.Clear(); DialogResult = true; }, () => DialogResult = false);
        ((Button)actions.Children[1]).Content = "Connect"; AutomationProperties.SetName(actions.Children[1], "Connect");
        Closed += (_, _) => password.Clear(); Content = Ui.Scroll(stack); Loaded += (_, _) => { if (metadata is null) user.Focus(); else password.Focus(); };
    }
}

internal sealed class GatewayEditor : Window
{
    public GatewayEditor(Window owner, LibraryViewModel vm, GatewayProfile? profile)
    {
        Owner = owner; Title = profile is null ? "Add RD Gateway" : "Edit RD Gateway"; Width = 530; Height = 445; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner; Ui.FitToWorkArea(this);
        var draft = profile is null ? new GatewayProfile() : profile with { }; var stack = Ui.Stack(20); var label = Ui.Input("Friendly name", draft.Label, stack); var host = Ui.Input("RD Gateway server (DNS name or address)", draft.Host, stack);
        var choices = new List<Choice> { new(null, "Ask separately for gateway credentials") }; choices.AddRange(vm.Snapshot.Credentials.Select(c => new Choice(c.Id, c.Label))); var credentials = Ui.Combo("Gateway credentials", choices, choices.FirstOrDefault(c => c.Id == draft.CredentialId) ?? choices.First(), stack);
        Ui.Note(stack, "Gateway credentials are separate from destination-PC credentials. This must be a Remote Desktop Gateway service, not a default router or VPN endpoint. HTTPS uses the engine's supported gateway behavior; custom ports and explicit HTTP proxy configuration are unavailable.");
        Ui.Footer(stack, () => Ui.Safe(this, () =>
        {
            if (string.IsNullOrWhiteSpace(label.Text) || !EndpointParser.TryParse(host.Text, out var endpoint, out var error) || endpoint is null) { MessageBox.Show(this, "Enter a friendly name and a valid RD Gateway server."); return; }
            if (endpoint.Port != 3389 || host.Text.Contains(":")) { MessageBox.Show(this, "This prototype accepts a gateway DNS name or IPv4 address without a port."); return; }
            draft.Label = label.Text.Trim(); draft.Host = endpoint.Host; draft.CredentialId = (credentials.SelectedItem as Choice)?.Id; vm.Store.SaveGateway(draft); vm.Reload(); DialogResult = true;
        }), () => DialogResult = false); Content = Ui.Scroll(stack);
    }
}
