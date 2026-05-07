using AgentRp.Models;

namespace AgentRp.Services;

public static class AiProviderModelSelectionRules
{
    public static IReadOnlyList<AiModelRole> ProviderRoles { get; } = [AiModelRole.Chat, AiModelRole.Image, AiModelRole.Voice];

    public static bool HasChatRole(AiProviderModel model) => model.Capabilities.CanGenerateText;

    public static bool HasImageRole(AiProviderModel model) => model.Capabilities.CanGenerateImage;

    public static bool HasVoiceRole(AiProviderModel model) => model.Capabilities.CanGenerateSpeech;

    public static AiModelRole ProviderRoleFor(AiModelRole role) => role switch
    {
        AiModelRole.Reasoning => AiModelRole.Chat,
        _ => role
    };

    public static bool HasRole(AiProviderModel model, AiModelRole role) => ProviderRoleFor(role) switch
    {
        AiModelRole.Chat => HasChatRole(model),
        AiModelRole.Image => HasImageRole(model),
        AiModelRole.Voice => HasVoiceRole(model),
        _ => false
    };

    public static bool HasAnyRole(AiProviderModel model) => ProviderRoles.Any(role => HasRole(model, role));

    public static bool IsSelectedForChat(AiProviderModel model) =>
        IsSelectedForRole(model, AiModelRole.Chat);

    public static bool IsSelectedForImage(AiProviderModel model) =>
        IsSelectedForRole(model, AiModelRole.Image);

    public static bool IsSelectedForVoice(AiProviderModel model) =>
        IsSelectedForRole(model, AiModelRole.Voice);

    public static bool IsSelectedForRole(AiProviderModel model, AiModelRole role) =>
        model.Enabled && model.Roles.Contains(ProviderRoleFor(role)) && HasRole(model, role);

    public static bool IsSelectedForAnyRole(AiProviderModel model) =>
        ProviderRoles.Any(role => IsSelectedForRole(model, role));

    public static void SetChatSelected(AiProviderModel model, bool selected)
    {
        SetRoleSelected(model, AiModelRole.Chat, selected);
    }

    public static void SetImageSelected(AiProviderModel model, bool selected)
    {
        SetRoleSelected(model, AiModelRole.Image, selected);
    }

    public static void SetVoiceSelected(AiProviderModel model, bool selected)
    {
        SetRoleSelected(model, AiModelRole.Voice, selected);
    }

    public static void SetRoleSelected(AiProviderModel model, AiModelRole role, bool selected)
    {
        var providerRole = ProviderRoleFor(role);
        if (selected && HasRole(model, role))
            model.Roles.Add(providerRole);
        else
            model.Roles.Remove(providerRole);

        SynchronizeEnabled(model);
    }

    public static void ClearSelectedRoles(AiProviderModel model)
    {
        model.Roles.Clear();
        SynchronizeEnabled(model);
    }

    public static void SelectAvailableRoles(AiProviderModel model)
    {
        model.Roles = ProviderRoles
            .Where(role => HasRole(model, role))
            .ToHashSet();
        SynchronizeEnabled(model);
    }

    public static void SynchronizeEnabled(AiProviderModel model)
    {
        model.Roles.RemoveWhere(role => role == AiModelRole.Reasoning || !HasRole(model, role));
        model.Enabled = model.Roles.Count > 0;
    }

    public static string Label(AiModelRole role) => role switch
    {
        AiModelRole.Chat => "Chat",
        AiModelRole.Reasoning => "Reasoning",
        AiModelRole.Image => "Image Gen",
        AiModelRole.Voice => "Voice",
        _ => role.ToString()
    };
}
