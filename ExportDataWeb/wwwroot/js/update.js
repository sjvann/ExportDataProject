(function () {
    const root = document.getElementById("app-update");
    const message = document.getElementById("app-update-message");
    const detail = document.getElementById("app-update-detail");
    const meter = document.getElementById("app-update-meter");
    const button = document.getElementById("app-update-action");
    if (!root || !message || !detail || !meter || !button) {
        return;
    }

    const quiet = { idle: true, checking: true, current: true };
    let timer = 0;

    function render(snapshot) {
        const phase = snapshot && snapshot.phase;
        root.hidden = !phase || quiet[phase] === true;
        message.textContent = snapshot && snapshot.message ? snapshot.message : "";
        if (snapshot && snapshot.detail) {
            detail.hidden = false;
            detail.textContent = snapshot.detail;
        } else {
            detail.hidden = true;
            detail.textContent = "";
        }

        if (phase === "downloading" || phase === "downloaded" || phase === "applying") {
            meter.hidden = false;
            if (typeof snapshot.percent === "number") {
                meter.value = snapshot.percent;
                meter.removeAttribute("aria-valuetext");
            } else {
                meter.removeAttribute("value");
            }
        } else {
            meter.hidden = true;
        }

        if (snapshot && snapshot.action === "download") {
            button.hidden = false;
            button.textContent = "下載並更新";
            button.disabled = false;
        } else if (snapshot && snapshot.action === "retry") {
            button.hidden = false;
            button.textContent = "再檢查一次";
            button.disabled = false;
        } else {
            button.hidden = true;
        }
    }

    async function post(path) {
        const response = await fetch(path, {
            method: "POST",
            headers: { "X-Requested-With": "fetch" }
        });
        if (!response.ok) {
            throw new Error(String(response.status));
        }
        return response.json();
    }

    async function refresh() {
        const response = await fetch("/update", { headers: { "X-Requested-With": "fetch" } });
        if (!response.ok) {
            throw new Error(String(response.status));
        }
        return response.json();
    }

    function watch(snapshot) {
        render(snapshot);
        window.clearTimeout(timer);
        const phase = snapshot && snapshot.phase;
        if (phase === "checking" || phase === "downloading") {
            timer = window.setTimeout(async function () {
                try {
                    watch(await refresh());
                } catch (error) {
                    render({ phase: "failed", message: "無法讀取下載進度。", action: "retry" });
                }
            }, 400);
        }
    }

    button.addEventListener("click", async function () {
        button.disabled = true;
        const retry = button.textContent === "再檢查一次";
        try {
            watch(await post(retry ? "/update/check?force=true" : "/update/download"));
        } catch (error) {
            render({ phase: "failed", message: "無法開始下載。", action: "retry" });
        }
    });

    post("/update/check").then(watch).catch(function () {
        render({ phase: "failed", message: "無法檢查更新。請稍後再試。", action: "retry" });
    });
})();
