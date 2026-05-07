# AppButton

`AppButton` is the shared primitive for simple buttons, icon actions, compact popup triggers, and short command buttons.

`IconButton` has been removed. Use `AppButton` for icon actions.

## Refactor Plan

1. Make `AppButton` own all simple button sizing.
2. Replace the ambiguous `Title` parameter with `TooltipText`.
3. Add convenience parameters for common button content:
   - `Icon` renders a leading `FaIcon`.
   - `Text` renders visible button text.
   - `TrailingIcon` renders a trailing `FaIcon`, usually for popup triggers.
   - `ChildContent` remains available for custom content and takes precedence when supplied.
4. Keep `Size` as scale only: `xs`, `sm`, `md`, and `lg`.
5. Remove `Size="icon"` as a size concept. Use `xs`, `sm`, `md`, or `lg` for scale.
6. Use an `lh`-based CSS contract so each size derives height from its font metrics instead of hard-coded pixel heights.
7. Remove `IconButton` call sites instead of keeping a compatibility wrapper.

## Size Contract

Button height must be consistent for every simple button with the same `Size`, whether it contains only an icon, text, an icon plus text, or a popup chevron.

The intended CSS shape is:

```css
.btn {
    --btn-block-padding: 4px;
    line-height: 1.5;
    height: calc(1lh + (var(--btn-block-padding) * 2));
    min-height: calc(1lh + (var(--btn-block-padding) * 2));
    padding-block: var(--btn-block-padding);
}

.btn-sm {
    font-size: 12px;
    padding-inline: 9px;
}

```

Inner icons, provider badges, spinners, and other glyph-like content must not inflate button height. Constrain them inside the button instead of changing the button's size locally.

## Usage

Use `Text` for visible button text. Use `TooltipText` for hover text and icon-only accessible naming. Avoid `Title` for new button APIs because it is ambiguous with visible labels.

Icon-only action:

```razor
<AppButton Icon="edit"
           TooltipText="Edit message"
           OnClick="@(_ => OpenEditBody())" />
```

Text command:

```razor
<AppButton Variant="primary"
           Text="Save"
           Icon="check"
           OnClick="@(_ => Save())" />
```

Popup trigger:

```razor
<AppButton Variant="secondary"
           Text="@SelectedVoiceName"
           TrailingIcon="chevron-down"
           TooltipText="@VoicePickerTitle"
           Active="@picker.IsOpen"
           OnClick="@(_ => picker.Toggle())" />
```

Custom content is still allowed for uncommon layouts:

```razor
<AppButton Variant="secondary" TooltipText="Change active voice model">
    <ProviderBadge Type="@provider.Type" />
    <FaIcon Name="chevron-down" />
</AppButton>
```

## Voice Selector Target

The compact voice selector should use `AppButton Size="sm"` for all three controls:

- provider/model popup trigger,
- voice popup trigger,
- TTS sample play button.

All three must calculate to the same height because they share the same `AppButton` size.

Feature CSS for `VoicePicker` may set widths, gaps, truncation, and provider-badge constraints. It must not set independent button heights or padding-block for these controls.
