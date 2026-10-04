(function () {
    function initTabs(root) {
        const tablist = root.querySelector(":scope > [role='tablist']");
        if (!tablist) {
            return;
        }

        const tabs = Array.from(tablist.querySelectorAll(":scope > [role='tab']"));
        const panels = tabs.map(function (tab) {
            return document.getElementById(tab.getAttribute("aria-controls"));
        });

        function activate(index, focusTab) {
            tabs.forEach(function (tab, i) {
                const selected = i === index;
                tab.setAttribute("aria-selected", selected ? "true" : "false");
                tab.tabIndex = selected ? 0 : -1;
                const panel = panels[i];
                if (panel) {
                    panel.hidden = !selected;
                }
            });
            if (focusTab && tabs[index]) {
                tabs[index].focus();
            }
        }

        tabs.forEach(function (tab, index) {
            tab.addEventListener("click", function () {
                activate(index, false);
            });
            tab.addEventListener("keydown", function (event) {
                const last = tabs.length - 1;
                let next = null;
                if (event.key === "ArrowRight") {
                    next = index === last ? 0 : index + 1;
                } else if (event.key === "ArrowLeft") {
                    next = index === 0 ? last : index - 1;
                } else if (event.key === "Home") {
                    next = 0;
                } else if (event.key === "End") {
                    next = last;
                }
                if (next === null) {
                    return;
                }
                event.preventDefault();
                activate(next, true);
            });
        });

        return {
            activate: activate,
            tabs: tabs
        };
    }

    document.querySelectorAll("[data-tabs]").forEach(initTabs);

    document.querySelectorAll("[data-picker]").forEach(function (picker) {
        const options = Array.from(picker.querySelectorAll("[data-pick]"));
        function select(option) {
            options.forEach(function (item) {
                const on = item === option;
                item.setAttribute("aria-pressed", on ? "true" : "false");
                const detail = document.getElementById(item.getAttribute("aria-controls"));
                if (detail) {
                    detail.hidden = !on;
                }
            });
        }

        options.forEach(function (option) {
            option.addEventListener("click", function () {
                select(option);
            });
        });
    });

    const summary = document.getElementById("form-alert");
    const fieldLink = summary && summary.querySelector("a[href^='#']");
    if (fieldLink) {
        const id = fieldLink.getAttribute("href").slice(1);
        const field = document.getElementById(id);
        if (field) {
            const details = field.closest("details");
            if (details) {
                details.open = true;
            }
            const panel = field.closest("[role='tabpanel']");
            if (panel && panel.id) {
                const tab = document.querySelector("[aria-controls='" + panel.id + "']");
                if (tab) {
                    tab.click();
                }
            }
            field.scrollIntoView({ block: "nearest", inline: "nearest" });
        }
    }

    const viewSwitch = document.getElementById("view-switch");
    const workspaceGrid = document.querySelector(".workspace-grid");
    if (viewSwitch && workspaceGrid) {
        const buttons = Array.from(viewSwitch.querySelectorAll("[data-view]"));
        const narrow = window.matchMedia("(max-width: 899px)");
        let chosen = false;

        function show(view) {
            const inventory = view === "inventory";
            workspaceGrid.classList.toggle("show-inventory", inventory);
            buttons.forEach(function (button) {
                const on = button.getAttribute("data-view") === view;
                button.setAttribute("aria-pressed", on ? "true" : "false");
                button.tabIndex = on ? 0 : -1;
            });
        }

        function apply() {
            viewSwitch.hidden = !narrow.matches;
            if (!narrow.matches) {
                workspaceGrid.classList.remove("show-inventory");
                return;
            }
            if (!chosen) {
                chosen = true;
                const hasTables = Boolean(workspaceGrid.querySelector(".inventory-table"));
                show(hasTables ? "inventory" : "setup");
            }
        }

        buttons.forEach(function (button) {
            button.addEventListener("click", function () {
                show(button.getAttribute("data-view"));
            });
        });

        apply();
        if (narrow.addEventListener) {
            narrow.addEventListener("change", apply);
        }
    }
})();
