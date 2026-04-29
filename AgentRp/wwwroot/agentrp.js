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
    }
};

window.agentRp.applyStoredTheme();
