window.agentRp = {
    applyStoredTheme() {
        const saved = localStorage.getItem("agentRpTheme") || "system";
        document.documentElement.dataset.theme = saved === "system" ? "" : saved;
        return saved;
    },
    setTheme(theme) {
        localStorage.setItem("agentRpTheme", theme);
        document.documentElement.dataset.theme = theme === "system" ? "" : theme;
    },
    downloadText(filename, content, type) {
        const blob = new Blob([content], { type: type || "text/plain;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement("a");
        anchor.href = url;
        anchor.download = filename;
        anchor.click();
        URL.revokeObjectURL(url);
    },
    importText(accept) {
        return new Promise((resolve) => {
            const input = document.createElement("input");
            input.type = "file";
            input.accept = accept || ".json,.txt";
            input.onchange = () => {
                const file = input.files && input.files[0];
                if (!file) {
                    resolve("");
                    return;
                }

                const reader = new FileReader();
                reader.onload = () => resolve(reader.result || "");
                reader.onerror = () => resolve("");
                reader.readAsText(file);
            };
            input.click();
        });
    },
    scrollToBottom(element) {
        if (!element) {
            return;
        }

        element.scrollTop = element.scrollHeight;
    },
    registerInlineFooterScrollSnap(scrollContainer, footer) {
        if (!scrollContainer || !footer) {
            return {
                dispose() {
                }
            };
        }

        const bottomBufferPixels = 4;
        const idleDelayMilliseconds = 2000;
        const buttonOffsetPixels = 12;
        const host = scrollContainer.closest(".inline-footer-scroll-region-host") || scrollContainer.parentElement;
        const originalHostPosition = host?.style.position ?? "";
        const shouldRestoreHostPosition = host && !originalHostPosition && window.getComputedStyle(host).position === "static";
        let snapTimeout = 0;
        let animationFrame = 0;
        let visibilityFrame = 0;

        if (shouldRestoreHostPosition) {
            host.style.position = "relative";
        }

        const scrollButton = document.createElement("button");
        scrollButton.type = "button";
        scrollButton.className = "transcript-scroll-bottom btn btn-secondary btn-sm is-hidden";
        scrollButton.title = "Scroll to bottom";
        scrollButton.setAttribute("aria-label", "Scroll to bottom");
        scrollButton.innerHTML = '<i class="fa-regular fa-chevron-down" aria-hidden="true"></i>';
        scrollButton.style.bottom = `${footer.offsetHeight + buttonOffsetPixels}px`;

        host?.appendChild(scrollButton);

        const getDistanceFromBottom = () =>
            scrollContainer.scrollHeight - scrollContainer.clientHeight - scrollContainer.scrollTop;

        const updateButtonVisibility = () => {
            visibilityFrame = 0;
            scrollButton.style.bottom = `${footer.offsetHeight + buttonOffsetPixels}px`;
            scrollButton.classList.toggle(
                "is-hidden",
                scrollContainer.scrollHeight <= scrollContainer.clientHeight + bottomBufferPixels
                    || getDistanceFromBottom() <= bottomBufferPixels);
        };

        const scheduleButtonVisibilityUpdate = () => {
            if (visibilityFrame) {
                return;
            }

            visibilityFrame = window.requestAnimationFrame(updateButtonVisibility);
        };

        const snapIfNeeded = () => {
            animationFrame = 0;

            if (getDistanceFromBottom() <= bottomBufferPixels) {
                return;
            }

            const footerHeight = footer.offsetHeight;
            if (footerHeight <= 0) {
                return;
            }

            const containerRect = scrollContainer.getBoundingClientRect();
            const footerRect = footer.getBoundingClientRect();
            const visibleTop = Math.max(footerRect.top, containerRect.top);
            const visibleBottom = Math.min(footerRect.bottom, containerRect.bottom);
            const visibleHeight = Math.max(0, visibleBottom - visibleTop);

            if (visibleHeight > footerHeight / 2) {
                scrollContainer.scrollTo({
                    top: scrollContainer.scrollHeight,
                    behavior: "smooth"
                });
            }
        };

        const scheduleSnapCheck = () => {
            if (snapTimeout) {
                window.clearTimeout(snapTimeout);
            }

            snapTimeout = window.setTimeout(() => {
                snapTimeout = 0;

                if (animationFrame) {
                    return;
                }

                animationFrame = window.requestAnimationFrame(snapIfNeeded);
            }, idleDelayMilliseconds);
        };

        scrollContainer.addEventListener("scroll", scheduleSnapCheck, { passive: true });
        scrollContainer.addEventListener("scroll", scheduleButtonVisibilityUpdate, { passive: true });
        window.addEventListener("resize", scheduleSnapCheck);
        window.addEventListener("resize", scheduleButtonVisibilityUpdate);
        scrollButton.addEventListener("click", () => {
            scrollContainer.scrollTo({
                top: scrollContainer.scrollHeight,
                behavior: "smooth"
            });
        });

        const resizeObserver = new ResizeObserver(scheduleButtonVisibilityUpdate);
        resizeObserver.observe(scrollContainer);
        resizeObserver.observe(footer);

        const mutationObserver = new MutationObserver(scheduleButtonVisibilityUpdate);
        mutationObserver.observe(scrollContainer, {
            childList: true,
            subtree: true
        });

        scheduleSnapCheck();
        scheduleButtonVisibilityUpdate();

        return {
            dispose() {
                scrollContainer.removeEventListener("scroll", scheduleSnapCheck);
                scrollContainer.removeEventListener("scroll", scheduleButtonVisibilityUpdate);
                window.removeEventListener("resize", scheduleSnapCheck);
                window.removeEventListener("resize", scheduleButtonVisibilityUpdate);
                resizeObserver.disconnect();
                mutationObserver.disconnect();
                scrollButton.remove();

                if (shouldRestoreHostPosition) {
                    host.style.position = originalHostPosition;
                }

                if (snapTimeout) {
                    window.clearTimeout(snapTimeout);
                    snapTimeout = 0;
                }

                if (animationFrame) {
                    window.cancelAnimationFrame(animationFrame);
                    animationFrame = 0;
                }

                if (visibilityFrame) {
                    window.cancelAnimationFrame(visibilityFrame);
                    visibilityFrame = 0;
                }
            }
        };
    },
    textInputs: (() => {
        const modes = {
            none: "None",
            empty: "Empty",
            change: "Change",
            live: "Live"
        };

        function normalizeOptions(options) {
            return {
                mode: options?.mode || modes.none,
                value: options?.value || "",
                isEmpty: !!options?.isEmpty,
                emptyDebounceMilliseconds: Number(options?.emptyDebounceMilliseconds) || 100,
                changeDebounceMilliseconds: Number(options?.changeDebounceMilliseconds) || 2000,
                liveDebounceMilliseconds: Number(options?.liveDebounceMilliseconds) || 500
            };
        }

        function textValue(element) {
            return element?.value ?? "";
        }

        function isEmptyValue(value) {
            return !value || value.trim().length === 0;
        }

        function track(element, dotNet, options) {
            if (!element || !dotNet) {
                return {
                    update() {
                    },
                    dispose() {
                    }
                };
            }

            let current = normalizeOptions(options);
            let disposed = false;
            let valueTimer = 0;
            let emptyTimer = 0;
            let lastReportedValue = textValue(element);
            let lastReportedEmpty = isEmptyValue(lastReportedValue);

            const clearValueTimer = () => {
                if (!valueTimer) {
                    return;
                }

                window.clearTimeout(valueTimer);
                valueTimer = 0;
            };

            const clearEmptyTimer = () => {
                if (!emptyTimer) {
                    return;
                }

                window.clearTimeout(emptyTimer);
                emptyTimer = 0;
            };

            const clearTimers = () => {
                clearValueTimer();
                clearEmptyTimer();
            };

            const invokeDotNet = (method, ...args) => {
                dotNet.invokeMethodAsync(method, ...args).catch(() => {
                    // The Blazor circuit may be gone during teardown. Disposal is best-effort.
                });
            };

            const notifyValueChanged = () => {
                const next = textValue(element);
                if (next === lastReportedValue) {
                    return;
                }

                lastReportedValue = next;
                lastReportedEmpty = isEmptyValue(next);
                invokeDotNet("NotifyTextValueChanged", next);
            };

            const notifyEmptyChanged = () => {
                const next = isEmptyValue(textValue(element));
                if (next === lastReportedEmpty) {
                    return;
                }

                lastReportedEmpty = next;
                invokeDotNet("NotifyTextEmptyChanged", next);
            };

            const debounceValue = (delay, callback) => {
                clearValueTimer();
                valueTimer = window.setTimeout(() => {
                    valueTimer = 0;

                    if (!disposed) {
                        callback();
                    }
                }, delay);
            };

            const debounceEmpty = (delay, callback) => {
                clearEmptyTimer();
                emptyTimer = window.setTimeout(() => {
                    emptyTimer = 0;

                    if (!disposed) {
                        callback();
                    }
                }, delay);
            };

            const handleInput = () => {
                if (current.mode === modes.empty) {
                    debounceEmpty(current.emptyDebounceMilliseconds, notifyEmptyChanged);
                    debounceValue(current.changeDebounceMilliseconds, notifyValueChanged);
                } else if (current.mode === modes.change) {
                    debounceValue(current.changeDebounceMilliseconds, notifyValueChanged);
                } else if (current.mode === modes.live) {
                    debounceValue(current.liveDebounceMilliseconds, notifyValueChanged);
                }
            };

            const handleChange = () => {
                clearTimers();

                if (current.mode === modes.empty || current.mode === modes.change || current.mode === modes.live) {
                    notifyValueChanged();
                    notifyEmptyChanged();
                }
            };

            element.addEventListener("input", handleInput);
            element.addEventListener("change", handleChange);

            return {
                update(nextOptions) {
                    current = normalizeOptions(nextOptions);

                    if (textValue(element) === current.value) {
                        lastReportedValue = current.value;
                    }

                    lastReportedEmpty = current.isEmpty;
                },
                dispose() {
                    disposed = true;
                    clearTimers();
                    element.removeEventListener("input", handleInput);
                    element.removeEventListener("change", handleChange);
                }
            };
        }

        return { track };
    })(),
    textCommands: (() => {
        const values = new Map();
        let observing = false;
        let updateFrame = 0;

        function cssEscape(value) {
            if (window.CSS && typeof window.CSS.escape === "function") {
                return window.CSS.escape(value);
            }

            return String(value).replace(/["\\]/g, "\\$&");
        }

        function groupSelector(groupId, suffix) {
            return `[data-text-command-group="${cssEscape(groupId)}"]${suffix}`;
        }

        function inputFor(groupId) {
            if (!groupId) {
                return null;
            }

            return document.querySelector(groupSelector(groupId, "[data-text-command-input]"));
        }

        function buttonsFor(groupId) {
            if (!groupId) {
                return [];
            }

            return Array.from(document.querySelectorAll(groupSelector(groupId, "[data-text-command-action]")));
        }

        function groupFrom(element) {
            return element?.dataset?.textCommandGroup || "";
        }

        function isTextInput(element) {
            return !!element?.matches?.("[data-text-command-input][data-text-command-group]");
        }

        function textValue(element) {
            return element?.value ?? "";
        }

        function isEmpty(value) {
            return !value || value.trim().length === 0;
        }

        function isTrue(value) {
            return String(value || "").toLowerCase() === "true";
        }

        function rememberInput(input) {
            const groupId = groupFrom(input);
            if (!groupId) {
                return;
            }

            values.set(groupId, textValue(input));
        }

        function restoreInput(input) {
            const groupId = groupFrom(input);
            if (!groupId) {
                return;
            }

            if (!values.has(groupId)) {
                values.set(groupId, textValue(input));
                return;
            }

            const cachedValue = values.get(groupId) || "";
            if (textValue(input) !== cachedValue) {
                input.value = cachedValue;
            }
        }

        function updateGroup(groupId) {
            const input = inputFor(groupId);
            if (input) {
                restoreInput(input);
            }

            const currentValue = input ? textValue(input) : values.get(groupId) || "";
            const empty = isEmpty(currentValue);
            for (const button of buttonsFor(groupId)) {
                const appDisabled = isTrue(button.dataset.textCommandAppDisabled);
                const componentDisabled = isTrue(button.dataset.textCommandComponentDisabled);
                const requiresText = !button.dataset.textCommandRequiresText
                    || isTrue(button.dataset.textCommandRequiresText);
                button.disabled = componentDisabled || appDisabled || (requiresText && empty);
            }
        }

        function updateAll() {
            const groups = new Set();
            for (const element of document.querySelectorAll("[data-text-command-group]")) {
                const groupId = groupFrom(element);
                if (groupId) {
                    groups.add(groupId);
                }
            }

            for (const groupId of groups) {
                updateGroup(groupId);
            }
        }

        function scheduleUpdateAll() {
            if (updateFrame) {
                return;
            }

            updateFrame = window.requestAnimationFrame(() => {
                updateFrame = 0;
                updateAll();
            });
        }

        function triggerAction(groupId, action) {
            if (!groupId || !action) {
                return false;
            }

            updateGroup(groupId);
            const button = document.querySelector(`${groupSelector(groupId, "[data-text-command-action]")}[data-text-command-action="${cssEscape(action)}"]`);
            if (!button || button.disabled) {
                return false;
            }

            button.click();
            return true;
        }

        function handleInput(event) {
            if (!isTextInput(event.target)) {
                return;
            }

            rememberInput(event.target);
            updateGroup(groupFrom(event.target));
        }

        function handleFocus(event) {
            if (!isTextInput(event.target)) {
                return;
            }

            updateGroup(groupFrom(event.target));
        }

        function handleKeyDown(event) {
            if (!isTextInput(event.target) || event.defaultPrevented || event.isComposing) {
                return;
            }

            if (event.key !== "Enter") {
                return;
            }

            const groupId = groupFrom(event.target);
            const isTextArea = event.target.tagName === "TEXTAREA";
            const ctrlEnter = event.ctrlKey || event.metaKey;
            const action = ctrlEnter
                ? event.target.dataset.textCommandCtrlEnter
                : event.shiftKey && isTextArea
                    ? ""
                    : event.target.dataset.textCommandEnter;

            if (!action) {
                return;
            }

            event.preventDefault();
            triggerAction(groupId, action);
        }

        function value(groupId) {
            const input = inputFor(groupId);
            if (input) {
                rememberInput(input);
                return textValue(input);
            }

            return values.get(groupId) || "";
        }

        function clear(groupId) {
            values.set(groupId, "");
            const input = inputFor(groupId);
            if (input) {
                input.value = "";
            }

            updateGroup(groupId);
        }

        function ensureStarted() {
            if (observing) {
                return;
            }

            observing = true;
            document.addEventListener("input", handleInput, true);
            document.addEventListener("change", handleInput, true);
            document.addEventListener("focusin", handleFocus, true);
            document.addEventListener("keydown", handleKeyDown, true);

            const observer = new MutationObserver(scheduleUpdateAll);
            observer.observe(document.documentElement, {
                attributes: true,
                childList: true,
                subtree: true,
                attributeFilter: [
                    "data-text-command-group",
                    "data-text-command-input",
                    "data-text-command-action",
                    "data-text-command-app-disabled",
                    "data-text-command-component-disabled",
                    "data-text-command-requires-text",
                    "disabled",
                    "value"
                ]
            });

            if (document.readyState === "loading") {
                document.addEventListener("DOMContentLoaded", updateAll, { once: true });
            } else {
                scheduleUpdateAll();
            }
        }

        ensureStarted();

        return { value, clear, update: updateGroup };
    })(),
    numberInputs: (() => {
        function step(element, direction) {
            if (!element || element.disabled) {
                return;
            }

            const previous = element.value;

            try {
                if (direction < 0) {
                    element.stepDown();
                } else {
                    element.stepUp();
                }
            } catch {
                return;
            }

            if (element.value === previous) {
                return;
            }

            element.focus({ preventScroll: true });
            element.dispatchEvent(new Event("input", { bubbles: true }));
            element.dispatchEvent(new Event("change", { bubbles: true }));
        }

        return { step };
    })(),
    audio: (() => {
        let current = null;

        function ensureAudio() {
            if (!current) {
                current = new Audio();
            }

            return current;
        }

        function clearHandlers(audio) {
            audio.onended = null;
            audio.onerror = null;
        }

        async function playUrl(key, url, dotNet) {
            const audio = ensureAudio();
            audio.pause();
            clearHandlers(audio);
            audio.src = url;
            audio.onended = () => dotNet.invokeMethodAsync("NotifyAudioStopped", key);
            audio.onerror = () => dotNet.invokeMethodAsync("NotifyAudioFailed", key, describeAudioError(audio));
            await audio.play();
        }

        function describeAudioError(audio) {
            const code = audio.error && audio.error.code;
            if (code === 1) {
                return "Audio playback was interrupted.";
            }

            if (code === 2) {
                return "The audio stream stopped because of a network error.";
            }

            if (code === 3) {
                return "The generated audio could not be decoded.";
            }

            if (code === 4) {
                return "The audio source could not be played.";
            }

            return "Audio playback failed.";
        }

        function stop() {
            if (!current) {
                return;
            }

            current.pause();
            current.currentTime = 0;
        }

        function createObjectUrl(bytes, contentType) {
            const blob = new Blob([bytes], { type: contentType || "audio/mpeg" });
            return URL.createObjectURL(blob);
        }

        function revokeObjectUrl(url) {
            if (url && url.startsWith("blob:")) {
                URL.revokeObjectURL(url);
            }
        }

        return { playUrl, stop, createObjectUrl, revokeObjectUrl };
    })(),
    modal: (() => {
        const modals = new Map();
        let listening = false;

        function ensureListener() {
            if (listening) {
                return;
            }

            document.addEventListener("keydown", closeOnEscape);
            listening = true;
        }

        function removeListener() {
            if (!listening || modals.size > 0) {
                return;
            }

            document.removeEventListener("keydown", closeOnEscape);
            listening = false;
        }

        function closeOnEscape(event) {
            if (event.key !== "Escape" || event.defaultPrevented || modals.size === 0) {
                return;
            }

            event.preventDefault();
            [...modals.values()].at(-1).dotNet.invokeMethodAsync("CloseFromBrowser");
        }

        function track(id, dotNet) {
            modals.set(id, { id, dotNet });
            ensureListener();
        }

        function untrack(id) {
            modals.delete(id);
            removeListener();
        }

        return { track, untrack };
    })(),
    overlay: (() => {
        const overlays = new Map();
        let listening = false;

        const margin = 8;
        const gap = 6;

        function ensureListeners() {
            if (listening) {
                return;
            }

            window.addEventListener("resize", repositionAll);
            window.addEventListener("scroll", repositionAll, true);
            document.addEventListener("pointerdown", closeOutside, true);
            document.addEventListener("keydown", closeOnEscape, true);
            listening = true;
        }

        function removeListeners() {
            if (!listening || overlays.size > 0) {
                return;
            }

            window.removeEventListener("resize", repositionAll);
            window.removeEventListener("scroll", repositionAll, true);
            document.removeEventListener("pointerdown", closeOutside, true);
            document.removeEventListener("keydown", closeOnEscape, true);
            listening = false;
        }

        function closeOutside(event) {
            for (const overlay of [...overlays.values()].reverse()) {
                if (overlay.popover.contains(event.target) || overlay.anchor.contains(event.target)) {
                    return;
                }

                overlay.dotNet.invokeMethodAsync("CloseFromBrowser");
            }
        }

        function closeOnEscape(event) {
            if (event.key !== "Escape" || overlays.size === 0) {
                return;
            }

            event.preventDefault();
            [...overlays.values()].at(-1).dotNet.invokeMethodAsync("CloseFromBrowser");
        }

        function repositionAll() {
            for (const overlay of overlays.values()) {
                position(overlay);
            }
        }

        function track(id, anchor, popover, placement, dotNet) {
            console.debug("[AgentRp overlay] track begin", {
                id,
                hasAnchor: !!anchor,
                hasPopover: !!popover,
                placement
            });
            if (!anchor || !popover) {
                console.debug("[AgentRp overlay] track skipped: missing anchor or popover", { id });
                return;
            }

            const existing = overlays.get(id);
            if (existing) {
                existing.resizeObserver.disconnect();
            }

            const overlay = {
                id,
                anchor,
                popover,
                placement,
                dotNet,
                resizeObserver: new ResizeObserver(() => position(overlay))
            };

            overlay.resizeObserver.observe(anchor);
            overlay.resizeObserver.observe(popover);
            overlays.set(id, overlay);
            ensureListeners();
            requestAnimationFrame(() => position(overlay));
            console.debug("[AgentRp overlay] track registered", { id, placement });
        }

        function untrack(id) {
            console.debug("[AgentRp overlay] untrack begin", { id });
            const overlay = overlays.get(id);
            if (!overlay) {
                console.debug("[AgentRp overlay] untrack skipped: not found", { id });
                return;
            }

            overlay.resizeObserver.disconnect();
            overlays.delete(id);
            removeListeners();
            console.debug("[AgentRp overlay] untrack complete", { id });
        }

        function position(overlay) {
            if (!document.body.contains(overlay.anchor) || !document.body.contains(overlay.popover)) {
                overlay.dotNet.invokeMethodAsync("CloseFromBrowser");
                return;
            }

            const anchorRect = overlay.anchor.getBoundingClientRect();
            const popover = overlay.popover;
            const placement = overlay.placement || "BottomStart";
            const startsTop = placement.startsWith("Top");
            const preferredWidth = popover.offsetWidth;
            const availableBelow = window.innerHeight - anchorRect.bottom - margin - gap;
            const availableAbove = anchorRect.top - margin - gap;
            const shouldFlipDown = startsTop && availableAbove < Math.min(popover.offsetHeight, 180) && availableBelow > availableAbove;
            const shouldFlipUp = !startsTop && availableBelow < Math.min(popover.offsetHeight, 180) && availableAbove > availableBelow;
            const openAbove = shouldFlipUp || (startsTop && !shouldFlipDown);
            const availableHeight = openAbove ? availableAbove : availableBelow;
            const maxHeight = Math.max(120, Math.min(420, availableHeight));
            const measuredHeight = Math.min(popover.offsetHeight, maxHeight);
            const left = resolveLeft(anchorRect, preferredWidth, placement);
            const top = openAbove
                ? Math.max(margin, anchorRect.top - measuredHeight - gap)
                : Math.min(anchorRect.bottom + gap, window.innerHeight - margin - measuredHeight);

            popover.style.left = `${left}px`;
            popover.style.top = `${top}px`;
            popover.style.maxHeight = `${maxHeight}px`;
        }

        function resolveLeft(anchorRect, preferredWidth, placement) {
            let left = anchorRect.left;

            if (placement.endsWith("End")) {
                left = anchorRect.right - preferredWidth;
            } else if (placement.endsWith("Center")) {
                left = anchorRect.left + anchorRect.width / 2 - preferredWidth / 2;
            }

            return Math.min(Math.max(left, margin), window.innerWidth - preferredWidth - margin);
        }

        return { track, untrack };
    })()
};

window.agentRp.applyStoredTheme();
