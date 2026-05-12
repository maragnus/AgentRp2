# Stateful Forms

Read this before creating or changing a saveable editor, especially modal editors. `StatefulForm` is the app-owned pattern for draft state, dirty indicators, save/cancel behavior, and unsaved-change protection.

## Purpose

Stateful forms keep editing predictable:

- Edits happen against a draft, not directly against saved state unless the parent intentionally owns that draft.
- Dirty state means "different from the saved baseline now", not "the user touched this once".
- Save buttons, cancel buttons, tab indicators, section markers, and input outlines all use the same dirty source.
- Exit paths route through the form guard so tab switches, row switches, modal close, and navigation do not silently drop work.

## Core Pattern

Wrap the editor with `StatefulForm` and pass a mutable model plus a baseline snapshot.

```razor
<StatefulForm Model="@Draft"
              Baseline="@Baseline"
              OnSave="Save"
              OnClose="OnClose"
              ContextChanged="SetForm">
    <ChildContent Context="form">
        <ModalShell Title="Editor" OnClose="form.RequestCloseAsync">
            <ModalStackLayout BodyClass="modal-form-stack">
                <ChildContent>
                    <FormSection Title="Details" DirtyPaths="@DetailsPaths">
                        <FieldRow Label="Name" DirtyPath="Name">
                            <AppInput @bind-Value="Draft.Name" DirtyPath="Name" />
                        </FieldRow>
                    </FormSection>
                </ChildContent>
                <Footer>
                    <AppButton Variant="secondary" OnClick="@(_ => form.RequestCloseAsync())">Cancel</AppButton>
                    <BusyButton Disabled="@(!form.CanSave)" OnClick="form.SaveAsync">Save</BusyButton>
                </Footer>
            </ModalStackLayout>
        </ModalShell>
    </ChildContent>
</StatefulForm>
```

```csharp
EditorDraft Draft = new();
EditorDraft Baseline = new();
StatefulFormContext<EditorDraft>? Form;
static readonly string[] DetailsPaths = [nameof(EditorDraft.Name), nameof(EditorDraft.Summary)];

void SetForm(StatefulFormContext<EditorDraft> form) => Form = form;

async Task Save()
{
    await Store.SaveAsync(Draft);
    Baseline = StatefulFormSnapshot.Clone(Draft);
}
```

Use `StatefulFormSnapshot.Clone(...)` for baselines unless a domain service already provides a complete normalized clone. If a store changes externally, reload only when `Form?.HasChanges != true`.

## Dirty State

Supported components:

- `Section`, `FormSection`
- `FieldRow`, `AppField`
- `ModalTab`
- `AppInput`, `AppTextarea`, `AppNumberInput`, `AppSelect`, `KeyInput`
- `RangeSlider`, `ToggleSwitch`
- `OptionGrid`, `CharacterOptionGrid`, `NumberSetting`

They support:

- `Dirty`: explicit boolean for domain-specific comparisons.
- `DirtyPath`: a root-relative path in the `StatefulForm` model.
- `DirtyPaths`: a group of root-relative paths.
- `DirtyScopeId`: a registered custom scope.

Prefer `DirtyPath`/`DirtyPaths` for simple draft fields:

```razor
<ModalTab Id="world" Title="World" DirtyPaths="@WorldPaths" />

<FormSection Title="World" DirtyPaths="@WorldPaths">
    <FieldRow Label="Setting" DirtyPath="Setting">
        <AppTextarea @bind-Value="Draft.Setting" DirtyPath="Setting" />
    </FieldRow>
</FormSection>
```

Use explicit `Dirty` for dictionaries, generated rows, filtered child collections, or comparisons that are clearer in domain terms:

```razor
<FormSection Title="@character.Name" Dirty="@AppearanceChanged(character.Id)">
    <AppTextarea Value="@GetAppearance(character.Id)"
                 ValueChanged="value => SetAppearance(character.Id, value)"
                 Dirty="@AppearanceChanged(character.Id)" />
</FormSection>
```

Use `StatefulFormScope` for nested editors so child components can use local paths:

```razor
<StatefulFormScope PathPrefix="Location">
    <LocationEditor Location="Draft.Location" />
</StatefulFormScope>
```

Inside `LocationEditor`, use `DirtyPath="Name"` instead of `DirtyPath="Location.Name"`.

Scoped paths may pass through nullable draft branches. If `Location` is temporarily `null`, `Location.Name` compares as `null` instead of throwing; missing properties or dictionary keys still throw because those are implementation errors.

For complex child components that mutate lists or dictionaries without a bound input, call the cascaded form context after changing the draft:

```csharp
[CascadingParameter] public IStatefulFormContext? StatefulForm { get; set; }

void ToggleTag(string id)
{
    Toggle(Draft.Tags, id);
    StatefulForm?.NotifyChanged();
}
```

## Save And Exit Behavior

Every exit path that could discard edits must go through the form guard:

- Modal close: `OnClose="form.RequestCloseAsync"`.
- Cancel button: `form.RequestCloseAsync`.
- Row/tab/entity switches: `Form?.GuardAsync(() => SelectDirectAsync(id))`.
- Browser/internal navigation: leave `DisableNavigationGuard` off unless a parent form already guards the surface.

Use `BusyButton` for save actions. Do not hand-roll save busy UI. Keep successful saves local: close the modal, refresh content, clear dirty state, or show a compact inline status only when completion is otherwise invisible.

## Layout Rules

- Use the shared modal primitives from [Modal Layouts](ModalLayouts.md).
- Put fixed save/cancel actions in `ModalStackLayout.Footer`, `EntityListEditor.ItemFooterTemplate`, or `ModalShell.Footer` depending on the layout.
- Keep generic form layout in shared components and utility classes. Feature CSS should not redefine modal footers, scroll regions, section spacing, or field mechanics.
- Do not over-label fields. If a `Section` title already labels the only control, skip an extra `FieldRow` unless accessibility or clarity needs it.

## Done Checklist

Before calling a form complete:

- Draft and baseline are separate objects, or the parent intentionally owns the draft.
- `Save` persists the draft and refreshes/resets the baseline.
- Save uses `BusyButton` and is disabled through `form.CanSave` or a clear validation gate.
- Cancel, modal close, row switches, tab switches, and navigation are guarded.
- Store refreshes do not overwrite local unsaved work.
- Dirty indicators appear on relevant tabs, sections, fields, and inputs.
- Dirty indicators clear when the user reverts a value to the baseline.
- Simple fields use `DirtyPath`/`DirtyPaths`; custom predicates are reserved for complex domain groups.
- Text fields use `AppInput` or `AppTextarea`, not raw text inputs.
- Repeated components/elements use stable `@key` values.
- Async failures are logged and shown through the approved user-facing error pattern.
- The modal/editor uses shared layout primitives and has a consistent footer.
- `dotnet build` passes; add or update tests when the dirty/save behavior is non-trivial.
