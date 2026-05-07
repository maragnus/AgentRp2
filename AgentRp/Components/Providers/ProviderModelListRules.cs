using AgentRp.Models;
using AgentRp.Services;

namespace AgentRp.Components.Providers;

internal static class ProviderModelListRules
{
    public static IEnumerable<AiProviderModel> SortModels(IEnumerable<AiProviderModel> models) =>
        models
            .OrderBy(model => IsSelectedForAnyRole(model) ? 0 : 1)
            .ThenBy(model => IsReady(model) ? 0 : 1)
            .ThenByDescending(model => model.CreatedUnix ?? 0)
            .ThenBy(model => model.Id, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<AiProviderModel> VisibleModels(IEnumerable<AiProviderModel> models, bool showNeedsSetup)
    {
        var sorted = SortModels(models).ToList();
        return showNeedsSetup || sorted.All(NeedsSetup)
            ? sorted
            : sorted.Where(IsReady);
    }

    public static int ReadyCount(IEnumerable<AiProviderModel> models) => models.Count(IsReady);

    public static int NeedsSetupCount(IEnumerable<AiProviderModel> models) => models.Count(NeedsSetup);

    public static bool IsReady(AiProviderModel model) => !NeedsSetup(model);

    public static bool NeedsSetup(AiProviderModel model) => !HasAnyRole(model);

    public static bool HasAnyRole(AiProviderModel model) => AiProviderModelSelectionRules.HasAnyRole(model);

    public static void SetChatSelected(AiProviderModel model, bool selected) =>
        AiProviderModelSelectionRules.SetChatSelected(model, selected);

    public static void SetImageSelected(AiProviderModel model, bool selected) =>
        AiProviderModelSelectionRules.SetImageSelected(model, selected);

    public static void SetVoiceSelected(AiProviderModel model, bool selected) =>
        AiProviderModelSelectionRules.SetVoiceSelected(model, selected);

    public static void ClearSelectedRoles(AiProviderModel model) =>
        AiProviderModelSelectionRules.ClearSelectedRoles(model);

    public static void SynchronizeSelectedRoles(AiProviderModel model) =>
        AiProviderModelSelectionRules.SynchronizeEnabled(model);

    public static bool IsSelectedForAnyRole(AiProviderModel model) =>
        AiProviderModelSelectionRules.IsSelectedForAnyRole(model);
}
