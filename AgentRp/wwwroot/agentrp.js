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
            if (!anchor || !popover) {
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
        }

        function untrack(id) {
            const overlay = overlays.get(id);
            if (!overlay) {
                return;
            }

            overlay.resizeObserver.disconnect();
            overlays.delete(id);
            removeListeners();
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
