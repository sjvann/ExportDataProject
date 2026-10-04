(function () {
    const profiles = {
        Sqlite: {
            sqlite: true,
            sample: "Data Source=c:\\temp\\database.db;"
        },
        SqlServer: {
            server: true,
            sqlServer: true,
            listDatabases: true,
            hostLabel: "伺服器",
            hostPlaceholder: "localhost 或 localhost\\SQLEXPRESS",
            port: 1433,
            databaseLabel: "資料庫",
            databasePlaceholder: "database",
            userPlaceholder: "sa",
            sample: "Data Source=localhost;Initial Catalog=database;User Id=sa;Password=password;TrustServerCertificate=True"
        },
        MySql: {
            server: true,
            listDatabases: true,
            hostLabel: "伺服器",
            hostPlaceholder: "localhost",
            port: 3306,
            databaseLabel: "資料庫",
            databasePlaceholder: "database",
            userPlaceholder: "root",
            sample: "Server=localhost;Port=3306;Database=database;Uid=root;Pwd=password"
        },
        Oracle: {
            server: true,
            oracle: true,
            hostLabel: "主機",
            hostPlaceholder: "localhost",
            port: 1521,
            databaseLabel: "服務名稱",
            databasePlaceholder: "ORCL",
            userPlaceholder: "system",
            sample: "Data Source=localhost:1521/ORCL;User Id=system;Password=password"
        },
        PostgreSql: {
            server: true,
            listDatabases: true,
            hostLabel: "主機",
            hostPlaceholder: "localhost",
            port: 5432,
            databaseLabel: "資料庫",
            databasePlaceholder: "postgres",
            userPlaceholder: "postgres",
            sample: "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=password"
        }
    };

    const form = document.getElementById("export-form");
    const card = document.getElementById("db-connection-card");
    if (!form || !card) {
        return;
    }

    const typeSelect = document.getElementById("DbConfig_DbType");
    const catalog = readCatalog();
    let previewController = null;
    let previewTimer = 0;
    let listController = null;
    let listTimer = 0;

    function readCatalog() {
        const node = document.getElementById("driver-catalog");
        if (!node) {
            return [];
        }

        try {
            return JSON.parse(node.textContent || "[]");
        } catch (error) {
            return [];
        }
    }

    function rawMode() {
        const selected = document.querySelector('input[name="Connection.UseRawConnectionString"]:checked');
        return selected ? selected.value === "true" : false;
    }

    function setHidden(id, hidden) {
        const element = document.getElementById(id);
        if (element) {
            element.hidden = hidden;
        }
    }

    function renderDriver(dbType) {
        const box = document.getElementById("driver-status");
        if (!box) {
            return;
        }

        const driver = catalog.find(function (item) { return item.dbType === dbType; });
        if (!driver) {
            box.hidden = true;
            box.replaceChildren();
            return;
        }

        const meta = document.createElement("div");
        meta.className = "driver-meta";
        const title = document.createElement("div");
        title.className = "fw-semibold";
        title.textContent = driver.installed
            ? driver.displayName + " 驅動程式已內建"
            : "尚未載入 " + driver.displayName + " 驅動程式";
        const detail = document.createElement("div");
        detail.className = "small";
        detail.textContent = driver.installed
            ? [driver.packageId, driver.version].filter(Boolean).join(" ")
            : "下載安裝後請重新啟動此工具。";
        meta.append(title, detail);

        const link = document.createElement("a");
        link.href = driver.downloadUrl;
        link.target = "_blank";
        link.rel = "noopener noreferrer";
        link.className = driver.installed ? "btn btn-sm btn-outline-secondary" : "btn btn-sm btn-outline-danger";
        link.textContent = driver.installed ? "官方下載" : "下載驅動程式";

        box.classList.remove("alert-success", "alert-warning");
        box.classList.add(driver.installed ? "alert-success" : "alert-warning");
        box.replaceChildren(meta, link);
        box.hidden = false;
    }

    function sync(options) {
        const dbType = typeSelect ? typeSelect.value : "";
        const profile = profiles[dbType];
        const hasType = Boolean(profile);
        const raw = hasType && rawMode();
        const structured = hasType && !raw;
        const windowsAuth = document.getElementById("Connection_IntegratedSecurity");
        const useWindows = Boolean(windowsAuth && windowsAuth.checked && profile && profile.sqlServer);

        setHidden("connection-empty", hasType);
        setHidden("connection-mode", !hasType);
        setHidden("connection-server", !(structured && profile && profile.server));
        setHidden("connection-sqlite", !(structured && profile && profile.sqlite));
        setHidden("connection-account", !(structured && profile && profile.server && !useWindows));
        setHidden("connection-sqlserver", !(structured && profile && profile.sqlServer));
        setHidden("connection-oracle", !(structured && profile && profile.oracle));
        setHidden("connection-database", !(structured && profile && profile.listDatabases));
        setHidden("connection-raw", !raw);
        setHidden("connection-preview-wrap", !structured);
        setHidden("connection-test", !hasType);
        const advanced = document.getElementById("disclose-advanced");
        if (advanced && raw) {
            advanced.open = true;
        }
        const databaseSelect = document.getElementById("Connection_Database");
        const serviceInput = document.getElementById("Connection_Service");
        if (databaseSelect) databaseSelect.disabled = !(structured && profile && profile.listDatabases);
        if (serviceInput) serviceInput.disabled = !(structured && profile && profile.oracle);
        renderDriver(dbType);

        if (profile && profile.server) {
            const hostLabel = document.getElementById("label-host");
            const databaseLabel = document.getElementById("label-database");
            const host = document.getElementById("Connection_Host");
            const username = document.getElementById("Connection_Username");
            const port = document.getElementById("Connection_Port");
            const service = document.getElementById("Connection_Service");
            if (hostLabel) hostLabel.textContent = profile.hostLabel;
            if (databaseLabel && profile.listDatabases) databaseLabel.textContent = profile.databaseLabel;
            if (host) host.placeholder = profile.hostPlaceholder;
            if (service && profile.oracle) service.placeholder = profile.databasePlaceholder;
            if (username) username.placeholder = profile.userPlaceholder;
            if (port) {
                port.placeholder = String(profile.port);
                if (options && options.resetPort) {
                    port.value = String(profile.port);
                }
            }
        }

        const rawInput = document.getElementById("DbConfig_ConnectionString");
        if (rawInput && profile && profile.sample) {
            rawInput.placeholder = profile.sample;
        }

        schedulePreview();
    }

    function schedulePreview() {
        window.clearTimeout(previewTimer);
        previewTimer = window.setTimeout(updatePreview, 350);
    }

    async function updatePreview() {
        const wrap = document.getElementById("connection-preview-wrap");
        const preview = document.getElementById("connection-preview");
        if (!wrap || !preview || wrap.hidden) {
            return;
        }

        if (previewController) {
            previewController.abort();
        }

        previewController = new AbortController();
        const url = new URL(form.getAttribute("action") || window.location.pathname, window.location.origin);
        url.searchParams.set("handler", "Preview");

        try {
            const response = await fetch(url, {
                method: "POST",
                body: new FormData(form),
                signal: previewController.signal,
                headers: { "X-Requested-With": "fetch" }
            });
            if (!response.ok) {
                preview.className = "form-text text-secondary mb-0";
                preview.textContent = "無法產生連線字串預覽";
                return;
            }

            const payload = await response.json();
            if (payload.ok) {
                preview.className = "connection-preview";
                preview.textContent = payload.preview || "";
            } else {
                preview.className = "form-text text-secondary mb-0";
                preview.textContent = payload.message || "";
            }
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            preview.className = "form-text text-secondary mb-0";
            preview.textContent = "暫時無法產生連線字串預覽";
        }
    }

    function isListable() {
        const profile = profiles[typeSelect ? typeSelect.value : ""];
        return Boolean(profile && profile.listDatabases && !rawMode());
    }

    function canList() {
        if (!isListable()) {
            return false;
        }

        const profile = profiles[typeSelect.value];
        const windows = document.getElementById("Connection_IntegratedSecurity");
        if (profile.sqlServer && windows && windows.checked) {
            return true;
        }

        const user = document.getElementById("Connection_Username");
        return Boolean(user && user.value.trim());
    }

    function passwordReady() {
        const profile = profiles[typeSelect ? typeSelect.value : ""];
        const windows = document.getElementById("Connection_IntegratedSecurity");
        if (profile && profile.sqlServer && windows && windows.checked) {
            return true;
        }

        const password = document.getElementById("Connection_Password");
        if (password && password.value.length > 0) {
            return true;
        }

        const state = document.getElementById("PasswordState");
        return Boolean(state && state.value === "kept");
    }

    function resetDatabaseOptions() {
        const select = document.getElementById("Connection_Database");
        if (!select) {
            return;
        }

        const blank = document.createElement("option");
        blank.value = "";
        blank.textContent = "請選擇資料庫";
        select.replaceChildren(blank);
    }

    function scheduleDatabaseLoad() {
        window.clearTimeout(listTimer);
        listTimer = window.setTimeout(function () { loadDatabases(false); }, 400);
    }

    function setListButtonBusy(busy) {
        const button = document.getElementById("reload-databases");
        if (!button) {
            return;
        }

        button.disabled = busy;
        button.textContent = busy ? "載入中…" : "載入";
    }

    async function loadDatabases(force) {
        const status = document.getElementById("database-list-status");
        const select = document.getElementById("Connection_Database");
        if (!status || !select) {
            return;
        }

        if (!isListable()) {
            return;
        }

        if (!canList()) {
            status.className = "form-text";
            status.textContent = "填好使用者名稱後，這裡會列出伺服器上的資料庫。";
            return;
        }

        if (!force && !passwordReady()) {
            status.className = "form-text";
            status.textContent = "輸入密碼後會列出資料庫。若此伺服器不需要密碼，請按載入。";
            return;
        }

        if (listController) {
            listController.abort();
        }

        listController = new AbortController();
        const current = select.value;
        status.className = "form-text";
        status.textContent = "正在讀取資料庫…";
        setListButtonBusy(true);
        const url = new URL(form.getAttribute("action") || window.location.pathname, window.location.origin);
        url.searchParams.set("handler", "Databases");

        try {
            const response = await fetch(url, {
                method: "POST",
                body: new FormData(form),
                signal: listController.signal,
                headers: { "X-Requested-With": "fetch" }
            });
            const payload = await response.json();
            if (!payload.ok) {
                status.className = "form-text text-danger";
                status.textContent = payload.message || "無法讀取資料庫清單";
                setListButtonBusy(false);
                return;
            }

            const names = payload.databases || [];
            select.replaceChildren();
            const blank = document.createElement("option");
            blank.value = "";
            blank.textContent = names.length ? "請選擇資料庫" : "沒有可選的資料庫";
            select.append(blank);
            names.forEach(function (name) {
                const option = document.createElement("option");
                option.value = name;
                option.textContent = name;
                if (name === current) {
                    option.selected = true;
                }
                select.append(option);
            });
            status.className = "form-text";
            status.textContent = "已列出 " + names.length + " 個資料庫";
            setListButtonBusy(false);
            schedulePreview();
        } catch (error) {
            if (error && error.name === "AbortError") {
                return;
            }

            status.className = "form-text text-danger";
            status.textContent = "暫時無法讀取資料庫清單";
            setListButtonBusy(false);
        }
    }

    card.addEventListener("change", function (event) {
        const target = event.target;
        const id = target && target.id;
        if (target && target.classList) {
            target.classList.remove("is-invalid");
            target.removeAttribute("aria-invalid");
        }

        if (id) {
            const inlineError = document.getElementById(id + "-error");
            if (inlineError) {
                inlineError.hidden = true;
            }
        }

        if (id === "DbConfig_DbType") {
            resetDatabaseOptions();
        }

        sync({ resetPort: id === "DbConfig_DbType" });
        if (id === "DbConfig_DbType" || id === "Connection_Host" || id === "Connection_Port" || id === "Connection_Username" || id === "Connection_Password" || id === "Connection_IntegratedSecurity" || id === "mode-form" || id === "mode-raw") {
            scheduleDatabaseLoad();
        }
    });

    card.addEventListener("input", function (event) {
        const target = event.target;
        if (!target || target.id === "DbConfig_DbType") {
            return;
        }

        if (target.classList) {
            target.classList.remove("is-invalid");
            target.removeAttribute("aria-invalid");
        }

        const inlineError = document.getElementById(target.id + "-error");
        if (inlineError) {
            inlineError.hidden = true;
        }

        schedulePreview();
    });

    const passwordToggle = document.getElementById("toggle-password");
    const passwordInput = document.getElementById("Connection_Password");
    if (passwordToggle && passwordInput) {
        passwordToggle.addEventListener("click", function () {
            const showing = passwordInput.type === "text";
            passwordInput.type = showing ? "password" : "text";
            passwordToggle.textContent = showing ? "顯示" : "隱藏";
            passwordToggle.setAttribute("aria-pressed", showing ? "false" : "true");
        });
    }

    const passwordState = document.getElementById("PasswordState");
    if (passwordInput && passwordState) {
        passwordInput.addEventListener("input", function () {
            passwordState.value = passwordInput.value.length > 0 ? "kept" : "cleared";
        });
    }

    form.addEventListener("submit", function (event) {
        const button = event.submitter;
        if (!button) {
            return;
        }

        button.setAttribute("aria-busy", "true");
        button.textContent = "處理中…";
    });

    const summary = document.getElementById("form-alert");
    if (summary && summary.getAttribute("role") === "alert") {
        summary.focus();
    }

    const reloadDatabases = document.getElementById("reload-databases");
    if (reloadDatabases) {
        reloadDatabases.addEventListener("click", function () {
            loadDatabases(true);
        });
    }

    sync({ resetPort: false });
    scheduleDatabaseLoad();
})();
