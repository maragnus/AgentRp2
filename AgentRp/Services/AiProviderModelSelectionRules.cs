using AgentRp.Models;

namespace AgentRp.Services;

public static class AiProviderModelSelectionRules
{
    public static bool HasChatRole(AiProviderModel model) => model.Capabilities.CanGenerateText;

    public static bool HasImageRole(AiProviderModel model) => model.Capabilities.CanGenerateImage;

    public static bool HasAnyRole(AiProviderModel model) => HasChatRole(model) || HasImageRole(model);

    public static bool IsSelectedForChat(AiProviderModel model) =>
        model.Enabled && model.Text && HasChatRole(model);

    public static bool IsSelectedForImage(AiProviderModel model) =>
        model.Enabled && model.Image && HasImageRole(model);

    public static bool IsSelectedForAnyRole(AiProviderModel model) =>
        IsSelectedForChat(model) || IsSelectedForImage(model);

    public static void SetChatSelected(AiProviderModel model, bool selected)
    {
        model.Text = selected && HasChatRole(model);
        SynchronizeEnabled(model);
    }

    public static void SetImageSelected(AiProviderModel model, bool selected)
    {
        model.Image = selected && HasImageRole(model);
        SynchronizeEnabled(model);
    }

    public static void ClearSelectedRoles(AiProviderModel model)
    {
        model.Enabled = false;
        model.Text = false;
        model.Image = false;
        model.ActiveText = false;
    }

    public static void SelectAvailableRoles(AiProviderModel model)
    {
        model.Text = HasChatRole(model);
        model.Image = HasImageRole(model);
        SynchronizeEnabled(model);
    }

    public static void SynchronizeEnabled(AiProviderModel model)
    {
        model.Text = model.Text && HasChatRole(model);
        model.Image = model.Image && HasImageRole(model);
        model.Enabled = model.Text || model.Image;
        if (!IsSelectedForChat(model))
            model.ActiveText = false;
    }
}
