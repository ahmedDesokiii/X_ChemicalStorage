// wwwroot/js/biobank.js
(function () {
    "use strict";

    const BB_SWAL_THEME = {
        background: "var(--bb-surface)",
        customClass: {
            popup: "bb-swal-popup",
            confirmButton: "bb-swal-confirm",
            cancelButton: "bb-swal-cancel"
        },
        buttonsStyling: false // let our CSS classes above style the buttons, not Swal's defaults
    };
    // ===================== CONFIG =====================
    const API = {
        freezers: "/biobank/api/freezers",
        freezer: (id) => `/biobank/api/freezers/${id}`,
        shelves: (id) => `/biobank/api/freezers/${id}/shelves`,
        racksByShelf: (id, sIdx) => `/biobank/api/freezers/${id}/shelves/${sIdx}/racks`,
        racksByFreezer: (id) => `/biobank/api/freezers/${id}/racks`,
        drawers: (rackId) => `/biobank/api/racks/${rackId}/drawers`,
        boxesByDrawer: (drawerId) => `/biobank/api/drawers/${drawerId}/boxes`,
        boxesByRack: (rackId) => `/biobank/api/racks/${rackId}/boxes`,
        boxDetails: (boxId) => `/biobank/api/boxes/${boxId}`,
        search: (q) => `/biobank/api/search?q=${encodeURIComponent(q)}`,

        attachments: (sampleId, category) => `/biobank/api/samples/${encodeURIComponent(sampleId)}/attachments?category=${category}`,
        attachmentDownload: (id) => `/biobank/api/attachments/${id}/download`,
        attachmentDelete: (id) => `/biobank/api/attachments/${id}`
    };

    // ===================== STATE =====================
    let currentView = "room";
    let currentPath = [];          // [freezer, shelf?, rack?, drawer?, box?]
    let freezersCache = [];

    // ===================== HELPERS =====================

    function formatLiveTemp(fz) {
        if (fz.currentTemperature == null) return null;
        return Number(fz.currentTemperature).toFixed(1) + "°C";
    }

    function getStatusBadge(fz) {
        if (fz.hasActiveAlarm) {
            return `<span class="bb-status-badge bb-status-alarm">⚠ Alarm</span>`;
        }
        if (fz.status === 2 || fz.status === "Offline") {
            return `<span class="bb-status-badge bb-status-offline">● Offline</span>`;
        }
        if (fz.status === 3 || fz.status === "Maintenance") {
            return `<span class="bb-status-badge bb-status-maint">● Maintenance</span>`;
        }
        return `<span class="bb-status-badge bb-status-online">● Online</span>`;
    }

    async function apiGet(url) {
        console.log("[BioBank] GET", url);
        const res = await fetch(url);
        console.log("[BioBank] Status", res.status, url);

        if (!res.ok) {
            let msg = `Request failed: ${res.status}`;
            try {
                const err = await res.json();
                msg = err.message || msg;
            } catch { }
            throw new Error(msg);
        }
        return res.json();
    }

    // NEW (walks up from an element we KNOW exists, instead of guessing nesting)
    function setModalWidth(size) {
        // size: falsy = default (620px), true/"wide" = 960px (Store/Edit/History), "xl" = larger (Sample Search)
        const modalBody = $("modalBody");
        const modalBox = modalBody ? modalBody.closest(".bb-modal") : null;
        if (!modalBox) return;

        let value;
        if (size === "xl") {
            value = window.innerWidth <= 900 ? "96%" : "1200px";
        } else if (size) {
            value = window.innerWidth <= 900 ? "92%" : "960px";
        } else {
            value = "620px";
        }

        // setProperty with "important" guarantees this wins over any CSS class
        // (e.g. .bb-modal's own max-width:420px) regardless of specificity order.
        modalBox.style.setProperty("max-width", value, "important");
    }
    // "FZ80-01 › S01 › R02 › D03 › BX01 › A05" — mirrors the logic already used
    // in updateBreadcrumb(), just as plain text instead of clickable crumbs.
    function buildLocationPath() {
        const parts = [];
        if (currentPath[0]) parts.push(currentPath[0].id);
        if (currentPath[1] && currentPath[1].shelfIndex !== undefined) {
            parts.push(`S${String(currentPath[1].shelfIndex + 1).padStart(2, "0")}`);
        }
        if (currentPath[2]) {
            parts.push(`R${String((currentPath[2].rackIndex ?? 0) + 1).padStart(2, "0")}`);
        }
        if (currentPath[3] && currentPath[3].drawerIndex !== undefined) {
            parts.push(`D${String(currentPath[3].drawerIndex + 1).padStart(2, "0")}`);
        }
        if (currentPath[4]) parts.push(currentPath[4].boxId || currentPath[4].id || "");
        if (currentTubeContext?.position) parts.push(currentTubeContext.position);
        return parts.filter(Boolean).join(" &rsaquo; ");
    }

    async function prefillNextSampleId() {
        const el = $("smId");
        if (!el) return;
        try {
            const data = await apiGet("/biobank/api/samples/next-id");
            el.value = data.sampleId;
        } catch (e) {
            // Generation failed (offline / server error) — fall back to manual entry
            // instead of blocking the whole form.
            el.readOnly = false;
            el.value = "";
            el.placeholder = "SMP-2026-00001";
        }
        //populateSOPSelect("smSOP", null);
    }

    function bloodTypeLabel(v) {
        const map = { 0: "-", 1: "A+", 2: "A-", 3: "B+", 4: "B-", 5: "AB+", 6: "AB-", 7: "O+", 8: "O-" };
        return map[v] ?? "-";
    }


    let sopsCache = [];

    function testResultInfo(v) {
        const map = {
            0: { label: "Pending", color: "var(--bb-text-muted)" },
            1: { label: "Negative", color: "var(--bb-success)" },
            2: { label: "Positive", color: "#ef4444" }
        };
        return map[v] ?? map[0];
    }

    function testResultOptionsHtml(selected) {
        return `
        <option value="0" ${selected === 0 || selected == null ? "selected" : ""}>Pending</option>
        <option value="1" ${selected === 1 ? "selected" : ""}>Negative</option>
        <option value="2" ${selected === 2 ? "selected" : ""}>Positive</option>
    `;
    }

    async function populateSOPSelect(selectId, selectedId) {
        const el = $(selectId);
        if (!el) return;
        if (!sopsCache.length) {
            try { sopsCache = await apiGet("/biobank/api/sops"); } catch { sopsCache = []; }
        }
        el.innerHTML = `<option value="">— None —</option>` +
            sopsCache.map(s => `<option value="${s.id}" ${s.id === selectedId ? "selected" : ""}>${s.name}</option>`).join("");
    }



    // NEW helper — add near bloodTypeLabel()
    function movementTypeInfo(type) {
        const byNumber = {
            0: "Registered", 1: "Reserved", 2: "Reservation Cancelled", 3: "Stored",
            4: "Withdrawn", 5: "Transferred", 6: "Disposed", 7: "Edited",
            8: "Checked Out", 9: "Checked In"
        };
        const colors = {
            Registered: "var(--bb-text-muted)", Reserved: "var(--bb-warning)",
            "Reservation Cancelled": "var(--bb-text-muted)", Stored: "var(--bb-accent)",
            Withdrawn: "#ef4444", Transferred: "var(--bb-primary)", Disposed: "#ef4444",
            Edited: "var(--bb-cyan)", "Checked Out": "#a78bfa", "Checked In": "var(--bb-success)"
        };
        const icons = {
            Registered: "📝", Reserved: "🔖", "Reservation Cancelled": "✖", Stored: "📥",
            Withdrawn: "📤", Transferred: "🔀", Disposed: "🗑", Edited: "✏️",
            "Checked Out": "🧪", "Checked In": "✅"
        };
        const label = typeof type === "number" ? (byNumber[type] || `Type ${type}`) : (type || "Unknown");
        return { label, color: colors[label] || "var(--bb-text-muted)", icon: icons[label] || "•" };
    }


    function formatNumber(n) {
        return Number(n).toLocaleString();
    }

    function getOccupancyColor(pct) {
        if (pct < 50) return "var(--bb-success)";
        if (pct < 80) return "var(--bb-warning)";
        return "var(--bb-danger)";
    }

    function $(id) {
        return document.getElementById(id);
    }

    // ===================== MODERN ICONS (Lucide-style SVG) =====================
    const ICONS = {
        rack: `<svg class="bb-icon bb-icon-rack" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="3" y="3" width="18" height="18" rx="2"/>
            <path d="M3 9h18M3 15h18"/>
            <path d="M9 3v18"/>
        </svg>`,
        rackDrawer: `<svg class="bb-icon bb-icon-rack" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="2" y="3" width="20" height="18" rx="2"/>
            <path d="M2 9h20M2 15h20"/>
            <circle cx="6" cy="6" r="0.9" fill="currentColor" stroke="none"/>
            <circle cx="6" cy="12" r="0.9" fill="currentColor" stroke="none"/>
            <circle cx="6" cy="18" r="0.9" fill="currentColor" stroke="none"/>
        </svg>`,
        shelf: `<svg class="bb-icon bb-icon-shelf" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M3 4h18M3 10h18M3 16h18M3 22h18"/>
            <path d="M5 4v18M19 4v18"/>
        </svg>`,
        drawer: `<svg class="bb-icon bb-icon-drawer" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="3" y="5" width="18" height="14" rx="2"/>
            <path d="M3 10h18"/>
            <path d="M10 15h4"/>
        </svg>`,
        box: `<svg class="bb-icon bb-icon-box" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M21 8l-9-5-9 5v8l9 5 9-5V8z"/>
            <path d="M3 8l9 5 9-5"/>
            <path d="M12 13v8"/>
        </svg>`,
        freezer: `<svg class="bb-icon bb-icon-freezer" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.75" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <rect x="5" y="2" width="14" height="20" rx="2"/>
            <path d="M9 6h6M9 10h6"/>
            <path d="M12 14v4"/>
        </svg>`
    };

    function iconRack(sizeClass) {
        return `<div class="bb-icon-wrap bb-icon-tone-rack ${sizeClass || ""}">${ICONS.rackDrawer}</div>`;
    }

    function iconShelf(sizeClass) {
        return `<div class="bb-icon-wrap bb-icon-tone-shelf ${sizeClass || ""}">${ICONS.shelf}</div>`;
    }

    function iconDrawer(sizeClass) {
        return `<div class="bb-icon-wrap bb-icon-tone-drawer ${sizeClass || ""}">${ICONS.drawer}</div>`;
    }

    function iconBox(sizeClass) {
        return `<div class="bb-icon-wrap bb-icon-tone-box ${sizeClass || ""}">${ICONS.box}</div>`;
    }

    function clearViewport() {
        const vp = $("bbViewport");
        vp.innerHTML = "";
        vp.className = "bb-viewport";
    }

    function showLoading() {
        $("bbViewport").innerHTML = `
            <div class="bb-loading-screen">
                <div class="bb-spinner"></div>
                <div>Loading...</div>
            </div>`;
    }

    // ===================== NAVIGATION =====================
    function triggerColdFlash() {
        const flash = $("bbColdFlash");
        if (!flash) return;
        flash.classList.remove("flash");
        void flash.offsetWidth;
        flash.classList.add("flash");
    }

    async function navigateTo(view, data = null) {
        currentView = view;
        triggerColdFlash();
        clearViewport();
        showLoading();

        try {
            switch (view) {
                case "room":
                    currentPath = [];
                    await renderRoom();
                    break;
                case "freezer":
                    currentPath = [data];
                    await renderFreezer(data);
                    break;
                case "shelf":
                    currentPath = [data.freezer, data];
                    await renderShelf(data.freezer, data.shelfIndex);
                    break;
                case "rack":
                    currentPath = [data.freezer, { shelfIndex: data.shelfIndex }, data];
                    await renderRack(data);
                    break;
                case "drawer":
                    currentPath = [data.freezer, { shelfIndex: data.shelfIndex }, { rackIndex: data.rackIndex, id: data.rackId }, data];
                    await renderDrawer(data);
                    break;
                case "box":
                    currentPath = [data.freezer, { shelfIndex: data.shelfIndex }, { rackIndex: data.rackIndex }, { drawerIndex: data.drawerIndex }, data];
                    await renderBox(data.boxId);
                    break;
            }
            updateBreadcrumb();
            hideInfoPanel();
        } catch (err) {
            console.error("[BioBank] Error:", err);
            $("bbViewport").innerHTML = `
                <div class="bb-loading-screen">
                    <div style="font-size:2rem">⚠️</div>
                    <div style="max-width:420px;text-align:center;">
                        <strong>Failed to load Bio Bank</strong><br>
                        <span style="font-size:0.85rem;color:var(--bb-text-muted)">${err.message || err}</span>
                    </div>
                    <button class="bb-modal-btn" style="margin-top:16px" onclick="window.BioBank.navigateTo('room')">Back to Room</button>
                </div>`;
        }
    }

    // ===================== BREADCRUMB =====================
    function updateBreadcrumb() {
        const bc = $("bbBreadcrumb");
        let html = `<div class="bb-bcrumb-item ${currentView === "room" ? "active" : ""}" data-nav="room"><span>🏢</span> Bio Bank Room</div>`;

        if (currentView !== "room" && currentPath[0]) {
            const fz = currentPath[0];
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "freezer" ? "active" : ""}" data-nav="freezer" data-freezer-id="${fz.id}"><span>❄️</span> ${fz.id}</div>`;
        }

        if (["shelf", "rack", "drawer", "box"].includes(currentView) && currentPath[1] && currentPath[1].shelfIndex !== undefined) {
            const sIdx = currentPath[1].shelfIndex;
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "shelf" ? "active" : ""}" data-nav="shelf" data-shelf-index="${sIdx}"><span class="bb-bcrumb-icon">${ICONS.shelf}</span> S${String(sIdx + 1).padStart(2, "0")}</div>`;
        }

        if (["rack", "drawer", "box"].includes(currentView) && currentPath[2]) {
            const rIdx = currentPath[2].rackIndex ?? 0;
            const rId = currentPath[2].id || "";
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "rack" ? "active" : ""}" data-nav="rack" data-rack-id="${rId}" data-shelf-index="${currentPath[1]?.shelfIndex ?? 0}" data-rack-index="${rIdx}"><span class="bb-bcrumb-icon">${ICONS.rackDrawer}</span> R${String(rIdx + 1).padStart(2, "0")}</div>`;
        }

        if (["drawer", "box"].includes(currentView) && currentPath[3]) {
            const dIdx = currentPath[3].drawerIndex ?? 0;
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "drawer" ? "active" : ""}" data-nav="drawer" data-drawer-id="${currentPath[3].id || ""}" data-drawer-index="${dIdx}"><span class="bb-bcrumb-icon">${ICONS.drawer}</span> D${String(dIdx + 1).padStart(2, "0")}</div>`;
        }

        if (currentView === "box" && currentPath[4]) {
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item active"><span class="bb-bcrumb-icon">${ICONS.box}</span> ${currentPath[4].boxId || currentPath[4].id || "Box"}</div>`;
        }

        bc.innerHTML = html;
    }

    // ===================== MINIMAP =====================
    async function updateMinimap() {
        const mm = $("minimapContent");
        if (!freezersCache.length) {
            mm.innerHTML = `<div class="bb-loading">No freezers</div>`;
            return;
        }

        let html = "";
        for (const fz of freezersCache) {
            const isActive = currentPath[0] && currentPath[0].id === fz.id;
            html += `<div class="bb-tree-item ${isActive ? "active" : ""}" data-nav="freezer" data-freezer-id="${fz.id}">
                        <span class="bb-tree-icon">❄️</span> ${fz.id}
                     </div>`;

            if (isActive) {
                if (fz.type === 1 || fz.type === "Minus80") { // -80
                    try {
                        const shelves = await apiGet(API.shelves(fz.id));
                        html += `<div class="bb-tree-children">`;
                        for (const s of shelves) {
                            const sActive = currentPath[1] && currentPath[1].shelfIndex === s.shelfIndex;
                            html += `<div class="bb-tree-item ${sActive ? "active" : ""}" data-nav="shelf" data-shelf-index="${s.shelfIndex}">
                                        <span>📚</span> ${s.displayName || ("S" + String(s.shelfIndex + 1).padStart(2, "0"))}
                                     </div>`;
                        }
                        html += `</div>`;
                    } catch { /* ignore */ }
                } else {
                    try {
                        const racks = await apiGet(API.racksByFreezer(fz.id));
                        html += `<div class="bb-tree-children">`;
                        for (const r of racks) {
                            html += `<div class="bb-tree-item" data-nav="rack" data-rack-id="${r.id}" data-shelf-index="0" data-rack-index="${r.rackIndex}">
                                        <span>🧰</span> ${r.displayName || ("R" + String(r.rackIndex + 1).padStart(2, "0"))}
                                     </div>`;
                        }
                        html += `</div>`;
                    } catch { /* ignore */ }
                }
            }
        }
        mm.innerHTML = html;
    }

    // ===================== RENDER: ROOM =====================
    async function renderRoom() {
        freezersCache = await apiGet(API.freezers);
        const container = $("bbViewport");
        container.className = "bb-viewport bb-fade-in";

        if (!freezersCache.length) {
            container.innerHTML = `
                <div class="bb-loading-screen">
                    <div style="font-size:2.5rem">❄️</div>
                    <div>No freezers found</div>
                    <div style="font-size:0.85rem;color:var(--bb-text-muted)">Create freezers from the API or admin panel</div>
                </div>`;
            return;
        }

        const freezers80 = freezersCache.filter(fz => !(fz.type === 2 || fz.type === "Minus150"));
        const freezers150 = freezersCache.filter(fz => fz.type === 2 || fz.type === "Minus150");

        const renderFreezerCard = (fz) => {
            const occColor = getOccupancyColor(fz.occupancyPercent);
            const is150 = fz.type === 2 || fz.type === "Minus150";
            const typeClass = is150 ? "type-150" : "type-80";
            const alarmClass = fz.hasActiveAlarm ? "bb-freezer-alarm" : "";

            // Live temperature إن وُجدت، وإلا القيمة الافتراضية
            const liveTemp = fz.currentTemperature != null
                ? Number(fz.currentTemperature).toFixed(1)
                : (fz.temperature ?? (is150 ? -150 : -80));

            const racksVal = is150
                ? (fz.racksPerShelf ?? fz.racksCount ?? "-")
                : ((fz.shelvesCount || 0) * (fz.racksPerShelf || 0)) || "-";

            // Status badge
            let statusHtml = "";
            if (fz.hasActiveAlarm) {
                statusHtml = `<span class="bb-status-badge bb-status-alarm">⚠ Alarm</span>`;
            } else if (fz.status === 2 || fz.status === "Offline") {
                statusHtml = `<span class="bb-status-badge bb-status-offline">● Offline</span>`;
            } else if (fz.status === 3 || fz.status === "Maintenance") {
                statusHtml = `<span class="bb-status-badge bb-status-maint">● Maintenance</span>`;
            } else {
                statusHtml = `<span class="bb-status-badge bb-status-online">● Online</span>`;
            }

            const visual80 = `
        <div class="bb-fz-visual-80">
            <div class="bb-fz-body-80">
                <div class="bb-fz-logo">Haier</div>
                <div class="bb-fz-panel"></div>
                <div class="bb-fz-handle-80"></div>
                <div class="bb-fz-base-80"></div>
            </div>
        </div>`;

            const visual150 = `
        <div class="bb-fz-visual-150">
            <div class="bb-fz-body-150">
                <div class="bb-fz-lid"></div>
                <div class="bb-fz-side-panel"></div>
                <div class="bb-fz-latch"></div>
                <div class="bb-fz-logo-150">Haier</div>
            </div>
        </div>`;

            const stats = `
        <div class="bb-freezer-stats">
            <div class="bb-stat-row">
                <span class="bb-stat-label">Type</span>
                <span class="bb-stat-value">${fz.typeDisplay || (is150 ? "-150°C" : "-80°C")}</span>
            </div>
            <div class="bb-stat-row">
                <span class="bb-stat-label">Live Temp</span>
                <span class="bb-stat-value ${fz.hasActiveAlarm ? "bb-temp-alarm" : ""}">${liveTemp}°C</span>
            </div>
            <div class="bb-stat-row">
                <span class="bb-stat-label">${is150 ? "Racks" : "Shelves"}</span>
                <span class="bb-stat-value">${is150 ? racksVal : (fz.shelvesCount || "-")}</span>
            </div>
            ${!is150 ? `<div class="bb-stat-row"><span class="bb-stat-label">Racks</span><span class="bb-stat-value">${racksVal}</span></div>` : ""}
            <div class="bb-stat-row">
                <span class="bb-stat-label">Capacity</span>
                <span class="bb-stat-value">${formatNumber(fz.totalCapacity)} tubes</span>
            </div>
            <div class="bb-occupancy-bar">
                <div class="bb-occ-label">
                    <span>Occupancy</span>
                    <span style="color:${occColor}">${fz.occupancyPercent}%</span>
                </div>
                <div class="bb-occ-track">
                    <div class="bb-occ-fill" style="width:${fz.occupancyPercent}%;background:linear-gradient(90deg,${occColor},var(--bb-accent))"></div>
                </div>
            </div>
            <div class="bb-freezer-status">${statusHtml}</div>
        </div>`;

            return `
    <div class="bb-freezer ${typeClass} ${alarmClass}" data-nav="freezer" data-hover="freezer" data-freezer-id="${fz.id}">
        <div class="bb-freezer-header">
            <div class="bb-freezer-id">${fz.id}</div>
            <div class="bb-temp-badge ${is150 ? "cold" : ""} ${fz.hasActiveAlarm ? "bb-temp-alarm" : ""}">
                🌡️ ${liveTemp}°C
            </div>
        </div>
        <div class="bb-freezer-body">
            ${is150 ? visual150 : visual80}
            ${stats}
        </div>
    </div>`;
        };

        let html = `<div class="bb-room bb-stagger">`;

        // Row 1: up to 4 × -80 freezers side by side
        if (freezers80.length) {
            html += `<div class="bb-room-row row-80">`;
            freezers80.slice(0, 4).forEach(fz => { html += renderFreezerCard(fz); });
            html += `</div>`;
            // Extra -80 beyond 4 (if any)
            if (freezers80.length > 4) {
                html += `<div class="bb-room-row row-80">`;
                freezers80.slice(4).forEach(fz => { html += renderFreezerCard(fz); });
                html += `</div>`;
            }
        }

        // Row 2: -150 freezers (2 per row, under every two -80)
        if (freezers150.length) {
            html += `<div class="bb-room-row row-150">`;
            freezers150.forEach(fz => { html += renderFreezerCard(fz); });
            html += `</div>`;
        }

        html += `</div>`;
        container.innerHTML = html;
    }

    // ===================== IoT HELPERS =====================
    async function loadFreezerIoT(freezerId) {
        try {
            const from = new Date(Date.now() - 7 * 24 * 60 * 60 * 1000).toISOString();
            const [alarms, readings] = await Promise.all([
                apiGet(`/biobank/api/freezers/${encodeURIComponent(freezerId)}/alarms`),
                apiGet(`/biobank/api/freezers/${encodeURIComponent(freezerId)}/readings?from=${encodeURIComponent(from)}`)
            ]);
            return { alarms: alarms || [], readings: readings || [] };
        } catch (e) {
            console.warn("[BioBank] IoT data load failed", e);
            return { alarms: [], readings: [] };
        }
    }

    async function acknowledgeAlarm(alarmId) {
        const confirm = await Swal.fire({
            title: "Acknowledge Alarm?",
            text: "This will mark the alarm as cleared.",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Acknowledge",
            ...BB_SWAL_THEME
        });
        if (!confirm.isConfirmed) return false;

        try {
            await apiPost(`/biobank/api/alarms/${alarmId}/acknowledge`, {});
            return true;
        } catch (e) {
            Swal.fire({ icon: "error", title: "Failed", text: e.message, ...BB_SWAL_THEME });
            return false;
        }
    }

    function buildIoTPanel(freezer, iot) {
        const liveTemp = freezer.currentTemperature != null
            ? Number(freezer.currentTemperature).toFixed(1) + "°C"
            : (freezer.temperature ?? "-") + "°C";

        let alarmsHtml = "";
        if (!iot.alarms || iot.alarms.length === 0) {
            alarmsHtml = `<div style="color:var(--bb-text-muted);font-size:0.85rem;">No active alarms</div>`;
        } else {
            alarmsHtml = iot.alarms.map(a => `
            <div class="bb-alarm-row" data-alarm-id="${a.id}">
                <div class="bb-alarm-info">
                    <div class="bb-alarm-type">${a.alarmType} <span class="bb-alarm-level">L${a.level}</span></div>
                    <div class="bb-alarm-msg">${a.message || ""}</div>
                    <div class="bb-alarm-time">${new Date(a.triggeredAt).toLocaleString()}</div>
                </div>
                <button type="button" class="bb-btn-ack" data-ack-alarm="${a.id}">Acknowledge</button>
            </div>
        `).join("");
        }

        const recentReadings = (iot.readings || []).slice(-8).reverse();
        let readingsHtml = recentReadings.length === 0
            ? `<div style="color:var(--bb-text-muted);font-size:0.85rem;">No readings yet</div>`
            : recentReadings.map(r => `
            <div class="bb-reading-row">
                <span>${Number(r.temperature).toFixed(1)}°C</span>
                <span style="color:var(--bb-text-muted)">${new Date(r.timestamp).toLocaleString()}</span>
                <span class="bb-reading-online ${r.isOnline ? "on" : "off"}">${r.isOnline ? "Online" : "Offline"}</span>
            </div>
        `).join("");

        return `
    <div class="bb-iot-panel">
        <div class="bb-iot-header">
    <div class="bb-iot-title">IoT Monitoring</div>
    <div style="display:flex;align-items:center;gap:10px;flex-wrap:wrap;">
        <div class="bb-iot-live ${freezer.hasActiveAlarm ? "alarm" : ""}">
            Live: <strong>${liveTemp}</strong>
            ${freezer.hasActiveAlarm ? '<span class="bb-status-badge bb-status-alarm">⚠ Alarm</span>' : ""}
        </div>
        <button type="button" class="bb-btn-add" style="padding:6px 12px;font-size:0.8rem;"
                data-action="link-iot" data-freezer-id="${freezer.id}">
            🔗 Link Device
        </button>
    </div>
</div>
        <div class="bb-iot-section">
            <div class="bb-iot-section-title">Active Alarms</div>
            <div id="bbAlarmsList">${alarmsHtml}</div>
        </div>
        <div class="bb-iot-section">
            <div class="bb-iot-section-title">Recent Readings</div>
            <div class="bb-readings-list">${readingsHtml}</div>
        </div>
        <div class="bb-iot-section">
            <div class="bb-iot-section-title">Temperature History (7 days)</div>
            <div class="bb-chart-wrap">
                <canvas id="bbTempChart" height="120"></canvas>
            </div>
        </div>
    </div>`;
    }

    function renderTempChart(readings) {
        const canvas = document.getElementById("bbTempChart");
        if (!canvas || typeof Chart === "undefined") return;

        // تدمير أي جراف قديم
        if (window.__bbTempChart) {
            window.__bbTempChart.destroy();
            window.__bbTempChart = null;
        }

        if (!readings || readings.length === 0) {
            canvas.parentElement.innerHTML = `<div style="color:var(--bb-text-muted);font-size:0.85rem;padding:12px 0;">No readings yet</div>`;
            return;
        }

        // ترتيب تصاعدي حسب الوقت
        const sorted = [...readings].sort((a, b) => new Date(a.timestamp) - new Date(b.timestamp));

        const labels = sorted.map(r => {
            const d = new Date(r.timestamp);
            return d.toLocaleString(undefined, { month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
        });
        const temps = sorted.map(r => Number(r.temperature));

        window.__bbTempChart = new Chart(canvas, {
            type: "line",
            data: {
                labels,
                datasets: [{
                    label: "Temperature °C",
                    data: temps,
                    borderColor: "#22d3ee",
                    backgroundColor: "rgba(34, 211, 238, 0.12)",
                    borderWidth: 2,
                    pointRadius: sorted.length > 40 ? 0 : 3,
                    pointHoverRadius: 5,
                    tension: 0.25,
                    fill: true
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        backgroundColor: "#1a2332",
                        titleColor: "#f1f5f9",
                        bodyColor: "#22d3ee",
                        borderColor: "rgba(148,163,184,0.2)",
                        borderWidth: 1
                    }
                },
                scales: {
                    x: {
                        ticks: { color: "#94a3b8", maxTicksLimit: 8, font: { size: 10 } },
                        grid: { color: "rgba(148,163,184,0.08)" }
                    },
                    y: {
                        ticks: { color: "#94a3b8", font: { size: 10 }, callback: v => v + "°" },
                        grid: { color: "rgba(148,163,184,0.08)" }
                    }
                }
            }
        });
    }
    function openLinkIoTModal(freezerId) {
        setModalWidth(false);
        $("modalTitle").textContent = "Link IoT Device";
        $("modalIcon").textContent = "🔗";

        const body = $("modalBody");
        body.innerHTML = `
        <form id="bbLinkIoTForm" class="bb-form" style="text-align:left;">
            <div class="bb-form-row">
                <label>Freezer ID</label>
                <input type="text" id="iotFreezerId" value="${freezerId || ""}" readonly
                       style="opacity:0.85;cursor:not-allowed;">
            </div>
            <div class="bb-form-row">
                <label>Collector Code *</label>
                <input type="text" id="iotCollector" placeholder="e.g. GF5022315032881" required maxlength="100">
            </div>
            <div class="bb-form-row">
                <label>Gateway Serial *</label>
                <input type="text" id="iotSerial" placeholder="e.g. GF50222308030161" required maxlength="100">
            </div>
            <div class="bb-form-row">
                <label>Device Name</label>
                <input type="text" id="iotDeviceName" placeholder="Optional" maxlength="150">
            </div>
            <div id="bbLinkIoTError" class="bb-form-error" style="display:none;"></div>
        </form>
    `;

        const actions = $("modalActions");
        actions.innerHTML = `
        <button type="button" class="bb-btn-cancel" id="bbLinkIoTCancel">Cancel</button>
        <button type="button" class="bb-modal-btn" id="bbLinkIoTSubmit">Link Device</button>
    `;

        $("bbModal").classList.add("show");

        $("bbLinkIoTCancel").onclick = () => $("bbModal").classList.remove("show");
        $("bbLinkIoTSubmit").onclick = () => submitLinkIoT();
    }

    async function submitLinkIoT() {
        const errorEl = $("bbLinkIoTError");
        const btn = $("bbLinkIoTSubmit");
        if (errorEl) errorEl.style.display = "none";

        const payload = {
            freezerId: ($("iotFreezerId")?.value || "").trim(),
            collectorCode: ($("iotCollector")?.value || "").trim(),
            gatewaySerial: ($("iotSerial")?.value || "").trim(),
            deviceName: ($("iotDeviceName")?.value || "").trim() || null
        };

        if (!payload.freezerId || !payload.collectorCode || !payload.gatewaySerial) {
            if (errorEl) {
                errorEl.textContent = "Freezer ID, Collector Code and Gateway Serial are required.";
                errorEl.style.display = "block";
            }
            return;
        }

        if (btn) {
            btn.disabled = true;
            btn.textContent = "Linking...";
        }

        try {
            await apiPost("/biobank/api/iot/link", payload);
            $("bbModal").classList.remove("show");

            await Swal.fire({
                icon: "success",
                title: "Device Linked",
                text: `Collector ${payload.collectorCode} linked to ${payload.freezerId}`,
                ...BB_SWAL_THEME
            });

            // أعد تحميل صفحة الفريزر
            let fz = freezersCache.find(f => f.id === payload.freezerId);
            if (!fz) {
                try { fz = await apiGet(API.freezer(payload.freezerId)); } catch { fz = { id: payload.freezerId }; }
            }
            await navigateTo("freezer", fz);
        } catch (e) {
            if (errorEl) {
                errorEl.textContent = e.message || "Link failed";
                errorEl.style.display = "block";
            }
        } finally {
            if (btn) {
                btn.disabled = false;
                btn.textContent = "Link Device";
            }
        }
    }



    function bindAlarmAckButtons(freezer) {
        document.querySelectorAll("[data-ack-alarm]").forEach(btn => {
            btn.addEventListener("click", async (e) => {
                e.preventDefault();
                e.stopPropagation();
                const id = btn.dataset.ackAlarm;
                const ok = await acknowledgeAlarm(id);
                if (ok) {
                    let refreshed = freezersCache.find(f => f.id === freezer.id);
                    if (!refreshed) {
                        try { refreshed = await apiGet(API.freezer(freezer.id)); } catch { refreshed = freezer; }
                    }
                    await navigateTo("freezer", refreshed || freezer);
                }
            });
        });
    }
    // ===================== RENDER: FREEZER =====================
    async function renderFreezer(fz) {
        const is150 = fz.type === 2 || fz.type === "Minus150";
        const container = $("bbViewport");
        container.className = "bb-viewport bb-stage-enter";

        // تحميل بيانات الـ IoT مرة واحدة
        const iot = await loadFreezerIoT(fz.id);
        const iotPanel = buildIoTPanel(fz, iot);

        if (is150) {
            const racks = await apiGet(API.racksByFreezer(fz.id));
            let interior = "";
            for (const r of racks) {
                interior += `
            <div class="bb-rack" data-nav="rack" data-hover="rack"
                 data-rack-id="${r.id}" data-shelf-index="0" data-rack-index="${r.rackIndex}" data-occ="${r.occupancyPercent}">
                ${iconRack("bb-icon-md")}
                <div style="font-weight:700;font-size:0.82rem;margin-bottom:2px;">${r.displayName || r.id}</div>
                <div style="font-size:0.72rem;color:var(--bb-text-muted);">${r.boxesCount ?? "-"} Box · ${formatNumber(r.totalTubes || 0)} Tubes</div>
                <div style="margin-top:6px;"><div class="bb-occ-track"><div class="bb-occ-fill" style="width:${r.occupancyPercent || 0}%"></div></div></div>
            </div>`;
            }
            container.innerHTML = `
        <div class="bb-open-freezer-150">
            <div class="bb-open-150-title">
                <span>❄️ ${fz.id}</span>
                <span class="bb-temp-badge cold">-150°C</span>
                <span style="font-size:0.85rem;color:var(--bb-text-muted);font-weight:500;">${racks.length} Racks</span>
            </div>
            <div class="bb-150-row">
                <div class="bb-150-iot">${iotPanel}</div>
                <div class="bb-150-main">
                    <div class="bb-open-150-lid-bar" aria-hidden="true"></div>
                    <div class="bb-open-chest-body">${interior}</div>
                </div>
            </div>
        </div>`;
        } else {
            const shelves = await apiGet(API.shelves(fz.id));
            let rows = "";
            for (const s of shelves) {
                let slots = "";
                for (let i = 0; i < (s.racksCount || 4); i++) slots += "<span></span>";
                rows += `
            <div class="bb-open-shelf-row" data-nav="shelf" data-hover="shelf"
                 data-shelf-index="${s.shelfIndex}" data-shelf-id="${s.id}"
                 data-racks="${s.racksCount}" data-drawers="${s.drawersCount}"
                 data-boxes="${s.boxesCount}" data-tubes="${s.totalTubes}" data-occ="${s.occupancyPercent}">
                <span class="bb-open-shelf-icon">${ICONS.shelf}</span>
                <div class="bb-shelf-badge">S${String(s.shelfIndex + 1).padStart(2, "0")}</div>
                <div class="bb-open-shelf-slots">${slots}</div>
                <div class="bb-open-shelf-occ">${s.occupancyPercent}%</div>
            </div>`;
            }
            container.innerHTML = `
        <div class="bb-open-freezer-80">
            <div style="font-weight:700;font-size:1.15rem;color:var(--bb-text);">${fz.id} · -80°C</div>
            <div class="d-flex w-100">
                ${iotPanel}
                <div class="col-2">
                </div>
                <div class="bb-open-door-frame">
                    <div class="bb-open-door">
                        <div class="bb-fz-logo" style="top:4%;font-size:0.55rem;">Haier</div>
                        <div class="bb-fz-panel" style="top:18%;height:22%;"></div>
                        <div class="bb-fz-handle-80" style="left:4%;"></div>
                    </div>
                    <div class="bb-open-interior">${rows}</div>
                </div>
            </div>
        </div>`;
        }

        // ربط أزرار Acknowledge بعد ما يتحط الـ HTML
        bindAlarmAckButtons(fz);
        // رسم الجراف بعد ما الـ DOM يتحط
        renderTempChart(iot.readings);
    }


    // ===================== RENDER: SHELF =====================
    async function renderShelf(fz, shelfIndex) {
        const racks = await apiGet(API.racksByShelf(fz.id, shelfIndex));
        const container = $("bbViewport");
        container.className = "bb-viewport bb-stage-enter";

        let html = `
        <div style="margin-bottom:14px;display:flex;align-items:center;gap:10px;flex-wrap:wrap;">
            <span style="font-weight:700;font-size:1.1rem;">${fz.id}</span>
            <span class="bb-bcrumb-sep">›</span>
            <span class="bb-bcrumb-icon" style="color:var(--bb-cyan)">${ICONS.shelf}</span>
            <span class="bb-shelf-badge" style="font-size:0.85rem;padding:5px 12px;">S${String(shelfIndex + 1).padStart(2, "0")}</span>
            <span style="font-size:0.85rem;color:var(--bb-text-muted);">${racks.length} Racks</span>
        </div>
        <div class="bb-racks-container bb-stagger">`;
        for (const r of racks) {
            html += `
            <div class="bb-rack" data-nav="rack" data-hover="rack"
                 data-rack-id="${r.id}" data-shelf-index="${shelfIndex}" data-rack-index="${r.rackIndex}" data-occ="${r.occupancyPercent}">
                ${iconRack("bb-icon-lg")}
                <div style="font-weight:700;margin-bottom:4px;">${r.displayName || r.id}</div>
                <div style="font-size:0.8rem;color:var(--bb-text-muted);">${r.drawersCount} Drawers · ${r.boxesCount} Boxes</div>
                <div style="margin-top:8px;"><div class="bb-occ-track"><div class="bb-occ-fill" style="width:${r.occupancyPercent}%"></div></div></div>
            </div>`;
        }
        html += `</div>`;
        container.innerHTML = html;
    }

    // ===================== RENDER: RACK =====================
    async function renderRack(data) {
        const fz = data.freezer || currentPath[0];
        const is150 = fz.type === 2 || fz.type === "Minus150";
        const container = $("bbViewport");
        container.className = "bb-viewport bb-stage-enter";

        if (is150) {
            // -150: boxes directly under rack
            const boxes = await apiGet(API.boxesByRack(data.id || data.rackId));
            let html = `<div class="bb-boxes-container bb-stagger">`;
            for (const b of boxes) {
                html += renderBoxCard(b, data);
            }
            html += `</div>`;
            container.innerHTML = html;
        } else {
            const drawers = await apiGet(API.drawers(data.id || data.rackId));
            let html = `<div class="bb-drawers-container bb-stagger">`;
            for (const d of drawers) {
                html += `
                <div class="bb-drawer" data-nav="drawer" data-hover="drawer"
                     data-drawer-id="${d.id}" data-drawer-index="${d.drawerIndex}"
                     data-shelf-index="${data.shelfIndex}" data-rack-index="${data.rackIndex}" data-occ="${d.occupancyPercent}">
                    ${iconDrawer("bb-icon-lg")}
                    <div style="font-weight:700;margin-bottom:4px;">${d.displayName || d.id}</div>
                    <div style="font-size:0.8rem;color:var(--bb-text-muted);">${d.boxesCount} Boxes · ${formatNumber(d.totalTubes)} Tubes</div>
                    <div style="margin-top:8px;"><div class="bb-occ-track"><div class="bb-occ-fill" style="width:${d.occupancyPercent}%"></div></div></div>
                </div>`;
            }
            html += `</div>`;
            container.innerHTML = html;
        }
    }

    // ===================== RENDER: DRAWER =====================
    async function renderDrawer(data) {
        const boxes = await apiGet(API.boxesByDrawer(data.id || data.drawerId));
        const container = $("bbViewport");
        container.className = "bb-viewport bb-stage-enter";

        let html = `<div class="bb-boxes-container bb-stagger">`;
        for (const b of boxes) {
            html += renderBoxCard(b, data);
        }
        html += `</div>`;
        container.innerHTML = html;
    }

    function renderBoxCard(b, parentData) {
        return `
        <div class="bb-box" data-nav="box" data-hover="box"
             data-box-id="${b.id}" data-box-index="${b.boxIndex}"
             data-shelf-index="${parentData.shelfIndex ?? 0}"
             data-rack-index="${parentData.rackIndex ?? 0}"
             data-drawer-index="${parentData.drawerIndex ?? 0}"
             data-occ="${b.occupancyPercent}">
            ${iconBox("bb-icon-lg")}
            <div style="font-weight:700;margin-bottom:4px;font-size:0.9rem;">${b.displayName || b.id}</div>
            <div style="font-size:0.8rem;color:var(--bb-text-muted);">100 Tubes · 10×10</div>
            <div style="margin-top:8px;"><div class="bb-occ-track"><div class="bb-occ-fill" style="width:${b.occupancyPercent || 0}%;background:linear-gradient(90deg,${getOccupancyColor(b.occupancyPercent)},var(--bb-accent))"></div></div></div>
            <div style="margin-top:4px;font-size:0.78rem;">
                <span style="color:${getOccupancyColor(b.occupancyPercent)};font-weight:600">${b.occupancyPercent || 0}%</span> occupied
            </div>
        </div>`;
    }

    // ===================== RENDER: BOX (10x10) =====================
    async function renderBox(boxId) {
        const box = await apiGet(API.boxDetails(boxId));
        const container = $("bbViewport");
        container.className = "bb-viewport bb-stage-enter";

        // Build position map from tubes
        const tubeMap = {};
        (box.bio_Tubes || box.tubes || []).forEach(t => {
            tubeMap[t.position] = t;
        });

        let colHeaders = "";
        for (let c = 0; c < 10; c++) {
            colHeaders += `<div style="text-align:center;font-size:0.7rem;color:var(--bb-text-muted);font-weight:600;">${c + 1}</div>`;
        }

        let rowLabels = "";
        for (let r = 0; r < 10; r++) {
            rowLabels += `<div style="height:32px;display:flex;align-items:center;justify-content:center;font-size:0.7rem;color:var(--bb-text-muted);font-weight:600;">${String.fromCharCode(65 + r)}</div>`;
        }

        let tubes = "";
        for (let row = 0; row < 10; row++) {
            for (let col = 0; col < 10; col++) {
                const pos = String.fromCharCode(65 + row) + String(col + 1).padStart(2, "0");
                const tube = tubeMap[pos];
                let stateClass = "";
                let isOccupied = false;
                let isReserved = false;
                let isCheckedOut = false;

                if (tube) {
                    if (tube.status === 1 || tube.status === "Occupied") {
                        stateClass = "occupied";
                        isOccupied = true;
                    } else if (tube.status === 2 || tube.status === "Reserved") {
                        stateClass = "reserved";
                        isReserved = true;
                    } else if (tube.status === 4 || tube.status === "CheckedOut") {
                        stateClass = "checked-out";
                        isCheckedOut = true;
                    }
                }

                const tubeId = tube ? tube.id : `${boxId}-${pos}`;
                tubes += `<div class="bb-tube ${stateClass}"
                    data-hover="tube" data-action="open-modal"
                    data-tube-id="${tubeId}" data-pos="${pos}"
                    data-occupied="${isOccupied}" data-reserved="${isReserved}" data-checked-out="${isCheckedOut}"
                    data-sample-id="${tube?.sampleId || ""}"
                    data-sample-name="${tube?.sampleName || ""}"
                    data-barcode="${tube?.barcode || ""}"
                    title="${pos}"></div>`;
            }
        }

        const html = `
        <div class="bb-cryobox-container">
            <div class="bb-cryobox">
                <div class="bb-cryobox-header">
                    <div>
                        <div style="font-weight:700;font-size:1.2rem;">${box.id}</div>
                        <div style="font-size:0.8rem;color:var(--bb-text-muted);">Cryobox · 10×10 Grid · 100 Positions</div>
                    </div>
                    <div class="bb-temp-badge" style="font-size:0.75rem;">🌡️ ${box.temperature}°C</div>
                </div>
                <div style="display:grid;grid-template-columns:30px 1fr;gap:8px;align-items:start;">
                    <div style="display:flex;flex-direction:column;gap:6px;padding-top:28px;">${rowLabels}</div>
                    <div>
                        <div style="display:grid;grid-template-columns:repeat(10,1fr);gap:6px;margin-bottom:6px;">${colHeaders}</div>
                        <div class="bb-cryobox-grid">${tubes}</div>
                    </div>
                </div>
               <div style="margin-top:16px;display:flex;gap:16px;justify-content:center;font-size:0.8rem;flex-wrap:wrap;">
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:linear-gradient(135deg,var(--bb-accent),var(--bb-cyan));"></div>
                        <span class="text-light" style="color:var(--bb-text-muted)">Occupied (${box.occupiedCount})</span>
                    </div>
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:linear-gradient(135deg,var(--bb-warning),var(--bb-gold));"></div>
                        <span style="color:var(--bb-text-muted)">Reserved (${box.reservedCount})</span>
                    </div>
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:linear-gradient(135deg,#a78bfa,var(--bb-primary));"></div>
                        <span style="color:var(--bb-text-muted)">Checked Out (${box.checkedOutCount || 0})</span>
                    </div>
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:rgba(255,255,255,0.1);border:1px solid rgba(255,255,255,0.15);"></div>
                        <span style="color:var(--bb-text-muted)">Available (${box.availableCount})</span>
                    </div>
                </div>
            </div>
        </div>`;
        container.innerHTML = html;
    }

    // ===================== INFO PANEL =====================
    function showFreezerInfo(fz) {
        $("infoIcon").textContent = "❄️";
        $("infoTitle").textContent = fz.id;
        $("infoSubtitle").textContent = "Ultra-Low Temperature Freezer";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Temperature</span><span class="value" style="color:var(--bb-cyan)">${fz.temperature}°C</span></div>
            <div class="bb-info-row"><span class="label">Type</span><span class="value">${fz.typeDisplay || fz.type}</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ${fz.status}</span></div>
            <div class="bb-info-row"><span class="label">Total Capacity</span><span class="value">${formatNumber(fz.totalCapacity)}</span></div>
            <div class="bb-info-row"><span class="label">Occupied</span><span class="value">${formatNumber(fz.occupiedCount)}</span></div>
            <div class="bb-info-row"><span class="label">Available</span><span class="value">${formatNumber(fz.availableCount)}</span></div>
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(fz.occupancyPercent)}">${fz.occupancyPercent}%</span></div>
            <div class="bb-info-row"><span class="label">Barcode</span><span class="value" style="font-size:0.75rem">${fz.barcode || "-"}</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showShelfInfo(el) {
        $("infoIcon").innerHTML = ICONS.shelf;
        $("infoTitle").textContent = el.dataset.shelfId || "Shelf";
        $("infoSubtitle").textContent = "Storage Shelf";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Racks</span><span class="value">${el.dataset.racks}</span></div>
            <div class="bb-info-row"><span class="label">Drawers</span><span class="value">${el.dataset.drawers}</span></div>
            <div class="bb-info-row"><span class="label">Boxes</span><span class="value">${el.dataset.boxes}</span></div>
            <div class="bb-info-row"><span class="label">Tube Capacity</span><span class="value">${formatNumber(el.dataset.tubes)}</span></div>
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showRackInfo(el) {
        $("infoIcon").innerHTML = ICONS.rackDrawer;
        $("infoTitle").textContent = el.dataset.rackId || "Rack";
        $("infoSubtitle").textContent = "Storage Rack";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ONLINE</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showDrawerInfo(el) {
        $("infoIcon").innerHTML = ICONS.drawer;
        $("infoTitle").textContent = el.dataset.drawerId || "Drawer";
        $("infoSubtitle").textContent = "Storage Drawer";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ONLINE</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showBoxInfo(el) {
        $("infoIcon").innerHTML = ICONS.box;
        $("infoTitle").textContent = el.dataset.boxId || "Box";
        $("infoSubtitle").textContent = "Cryobox";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Grid</span><span class="value">10 × 10</span></div>
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ACTIVE</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showTubeInfo(el, event) {
        const tt = $("bbTooltip");
        $("ttTitle").textContent = el.dataset.tubeId;
        const isOccupied = el.dataset.occupied === "true";
        const isReserved = el.dataset.reserved === "true";
        const isCheckedOut = el.dataset.checkedOut === "true";
        const status = isOccupied ? "Occupied" : (isCheckedOut ? "Checked Out" : (isReserved ? "Reserved" : "Available"));
        const statusColor = isOccupied ? "var(--bb-accent)" : (isCheckedOut ? "#a78bfa" : (isReserved ? "var(--bb-warning)" : "var(--bb-success)"));

        $("ttContent").innerHTML = `
        <div class="bb-tooltip-row"><span>Position</span><span>${el.dataset.pos}</span></div>
        <div class="bb-tooltip-row"><span>Status</span><span style="color:${statusColor};font-weight:600">${status}</span></div>
        ${el.dataset.barcode ? `<div class="bb-tooltip-row"><span>Barcode</span><span>${el.dataset.barcode}</span></div>` : ""}
        ${el.dataset.sampleId ? `<div class="bb-tooltip-row"><span>Sample</span><span>${el.dataset.sampleId}</span></div>` : ""}
        ${el.dataset.sampleName ? `<div class="bb-tooltip-row"><span>Name</span><span>${el.dataset.sampleName}</span></div>` : ""}`;

        tt.style.left = (event.clientX + 15) + "px";
        tt.style.top = (event.clientY + 15) + "px";
        tt.classList.add("show");
    }

    function hideTooltip() {
        $("bbTooltip").classList.remove("show");
    }

    function hideInfoPanel() {
        $("bbInfoPanel").classList.remove("show");
    }

    // ===================== MODAL =====================
    let currentTubeContext = null; // { boxId, position, tubeId }

    // NEW
    async function openModal(tubeEl) {
        const pos = tubeEl.dataset.pos;
        const boxId = currentPath[4]?.boxId || currentPath[4]?.id;
        if (!boxId || !pos) return;
        await openModalForPosition(boxId, pos);
    }

    // Same logic as openModal, but callable without a DOM tube element —
    // used by "Add Sample" (jumps to an auto-found empty slot) and by
    // "View" in Sample Search (jumps to a specific stored sample's tube).
    async function openModalForPosition(boxId, pos) {
        currentTubeContext = { boxId, position: pos, tubeId: `${boxId}-${pos}` };

        const modal = $("bbModal");
        const body = $("modalBody");
        const actions = $("modalActions");
        const title = $("modalTitle");

        body.innerHTML = `<div class="bb-loading"><div class="bb-spinner"></div></div>`;
        actions.innerHTML = "";
        modal.classList.add("show");

        try {
            const details = await apiGet(`/biobank/api/boxes/${encodeURIComponent(boxId)}/tubes/${encodeURIComponent(pos)}`);
            renderTubeModal(details);
        } catch (err) {
            setModalWidth(false);
            title.textContent = `Position ${pos}`;
            body.innerHTML = `<p style="color:#fca5a5">${err.message}</p>`;
            actions.innerHTML = `<button class="bb-btn-cancel" onclick="document.getElementById('bbModal').classList.remove('show')">Close</button>`;
        }
    }

    function highlightTubeInGrid(position) {
        const el = document.querySelector(`.bb-tube[data-pos="${position}"]`);
        if (!el) return;
        el.classList.add("bb-tube-highlight");
        setTimeout(() => el.classList.remove("bb-tube-highlight"), 2200);
    }

    // Resolves a box's full ancestor chain and navigates the background view to it,
    // so the breadcrumb, minimap and the modal's own "📍 location path" line all
    // stay correct — not just the box grid itself.
    async function navigateToBoxLocation(boxId) {
        const loc = await apiGet(`/biobank/api/boxes/${encodeURIComponent(boxId)}/location`);
        const freezer = await apiGet(API.freezer(loc.freezerId));
        await navigateTo("box", {
            freezer,
            shelfIndex: loc.shelfIndex,
            rackIndex: loc.rackIndex,
            rackId: loc.rackId,
            drawerIndex: loc.drawerIndex,
            drawerId: loc.drawerId,
            boxId: loc.boxId
        });
    }

    async function goToSampleLocation(sample) {
        if (!sample.currentBoxId || !sample.currentPosition) {
            Swal.fire({ icon: "info", title: "Not in storage", text: "This sample has no active storage location.", ...BB_SWAL_THEME });
            return;
        }
        try {
            $("bbModal").classList.remove("show");
            await navigateToBoxLocation(sample.currentBoxId);
            highlightTubeInGrid(sample.currentPosition);
        } catch (e) {
            Swal.fire({ icon: "error", title: "Couldn't locate sample", text: e.message, ...BB_SWAL_THEME });
        }
    }

    // NEW — full replacement
    function renderTubeModal(details) {
        const title = $("modalTitle");
        const body = $("modalBody");
        const actions = $("modalActions");
        const status = details.tubeStatus; // 0 Available, 1 Occupied, 2 Reserved, 4 CheckedOut (or string)

        const isOccupied = status === 1 || status === "Occupied";
        const isReserved = status === 2 || status === "Reserved";
        const isCheckedOut = status === 4 || status === "CheckedOut";
        const sample = details.sample;

        title.textContent = `Position ${details.position}`;
        title.className = "text-light";

        setModalWidth(false); // default; only the Store form widens itself below

        const pathLine = `<div style="font-size:0.75rem;color:var(--bb-text-muted);margin-bottom:14px;padding-bottom:10px;border-bottom:1px solid var(--bb-border);">📍 ${buildLocationPath()}</div>`;

        if (isOccupied && sample) {
            setModalWidth(true); // give the 75/25 split room to breathe

            body.innerHTML = `
        ${pathLine}
        <div class="bb-occ-split">
            <div class="bb-occ-info-col">
                <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-accent)">Occupied</span></div>
                <div class="bb-info-row"><span class="label">Sample ID</span><span class="value text-light">${sample.id}</span></div>
                <div class="bb-info-row"><span class="label">Name</span><span class="value text-light">${sample.name}</span></div>
                <div class="bb-info-row"><span class="label">Tube Barcode</span><span class="value text-light">${details.barcode || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Sample Type</span><span class="value text-light">${sample.sampleType || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Volume</span><span class="value text-light">${sample.remainingVolumeMl ?? sample.volumeMl ?? "-"}${sample.volumeMl ? " ml" : ""}</span></div>
                <div class="bb-info-row"><span class="label">Owner / PI</span><span class="value text-light">${sample.patientRef || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Project</span><span class="value text-light">${sample.projectCode || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Collected By</span><span class="value text-light">${sample.collectedByEmployeeId || "-"}</span></div>
                <div class="bb-info-row"><span class="label">SOP</span><span class="value text-light">${sample.sopId || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Analytics</span><span class="value text-light">${sample.analyticsResults || "-"}</span></div>
                <div class="bb-info-row"><span class="label">Freezer</span><span class="value text-light">${sample.currentFreezerId || "-"}</span></div>
                ${sample.notes ? `<p style="margin-top:10px;font-size:0.85rem;color:var(--bb-text-muted)">${sample.notes}</p>` : ""}
            </div>
            <div class="bb-occ-attach-col">
                <div style="font-size:0.78rem;color:var(--bb-text-muted);font-weight:600;margin-bottom:6px;">FASTA Access Code</div>
                <div id="occFastaList" class="bb-attachment-list"></div>
                <div style="font-size:0.78rem;color:var(--bb-text-muted);font-weight:600;margin:14px 0 6px;">Sample Attachments</div>
                <div id="occAttachmentsList" class="bb-attachment-list"></div>
            </div>
        </div>
    `;
            actions.innerHTML = `
        <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
        <button class="bb-modal-btn" id="btnEditSample">Edit</button>
        <button class="bb-modal-btn" style="background:linear-gradient(135deg,#a78bfa,var(--bb-primary))" id="btnCheckOut">Check Out</button>
        <button class="bb-modal-btn" style="background:linear-gradient(135deg,#f59e0b,#ef4444)" id="btnWithdraw">Withdraw</button>
        <button class="bb-modal-btn" id="btnHistory">History</button>
    `;
            $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
            $("btnEditSample").onclick = () => openSampleEditForm(sample);
            $("btnCheckOut").onclick = () => doCheckOut(sample.id);
            $("btnWithdraw").onclick = () => doWithdraw(sample.id);
            $("btnHistory").onclick = () => showHistory(sample.id);

            loadAttachmentList(sample.id, "Fasta", "occFastaList", false);
            loadAttachmentList(sample.id, "General", "occAttachmentsList", false);
        }
        else if (isCheckedOut && sample) {
            const isOverdue = sample.expectedReturnAt && parseUtcDate(sample.expectedReturnAt) < new Date();
            body.innerHTML = `
        ${pathLine}
        <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:#a78bfa">Checked Out</span></div>
        <div class="bb-info-row"><span class="label">Sample ID</span><span class="value text-light">${sample.id}</span></div>
        <div class="bb-info-row"><span class="label">Name</span><span class="value text-light">${sample.name}</span></div>
        <div class="bb-info-row"><span class="label">Tube Barcode</span><span class="value text-light">${details.barcode || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Sample Type</span><span class="value text-light">${sample.sampleType || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Volume</span><span class="value text-light">${sample.remainingVolumeMl ?? sample.volumeMl ?? "-"}${sample.volumeMl ? " ml" : ""}</span></div>
        <div class="bb-info-row"><span class="label">Owner / PI</span><span class="value text-light">${sample.patientRef || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Project</span><span class="value text-light">${sample.projectCode || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Collected By</span><span class="value text-light">${sample.collectedByEmployeeId || "-"}</span></div>
        
        <div class="bb-info-row"><span class="label">Reason</span><span class="value text-light">${sample.checkOutReason || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Checked Out At</span><span class="value text-light">${sample.checkedOutAt ? parseUtcDate(sample.checkedOutAt).toLocaleString() : "-"}</span></div>
        <div class="bb-info-row"><span class="label">Expected Back</span><span class="value text-light" style="${isOverdue ? "color:var(--bb-danger);font-weight:700" : ""}">${sample.expectedReturnAt ? parseUtcDate(sample.expectedReturnAt).toLocaleDateString() : "-"}${isOverdue ? " ⚠ Overdue" : ""}</span></div>
        <p style="margin-top:12px;font-size:0.85rem;color:var(--bb-text-muted)">This slot stays locked for this sample until it's checked back in.</p>
    `;
            actions.innerHTML = `
        <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
        <button class="bb-modal-btn" style="background:linear-gradient(135deg,var(--bb-success),var(--bb-accent))" id="btnCheckIn">Check In</button>
        <button class="bb-modal-btn" id="btnHistory">History</button>
    `;
            $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
            $("btnCheckIn").onclick = () => doCheckIn(sample.id);
            $("btnHistory").onclick = () => showHistory(sample.id);
        }

        else if (isReserved) {
            body.innerHTML = `
        ${pathLine}
        <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-warning)">Reserved</span></div>
        <div class="bb-info-row"><span class="label">Sample</span><span class="value text-light">${sample?.id || "-"}</span></div>
        <div class="bb-info-row"><span class="label">Name</span><span class="value text-light">${sample?.name || "-"}</span></div>
        <p style="margin-top:12px;font-size:0.85rem;color:var(--bb-text-muted)">Confirm store or cancel reservation.</p>
    `;
            actions.innerHTML = `
        <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
        <button class="bb-btn-cancel" id="btnCancelReserve">Cancel Reserve</button>
        <button class="bb-modal-btn" id="btnConfirmStore">Confirm Store</button>
    `;
            $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
            $("btnCancelReserve").onclick = () => doCancelReserve();
            $("btnConfirmStore").onclick = () => showStoreForm(sample);
        }
        else {
            // Available — blood sample intake form, widened (XL) + 4/4/3 column layout
            setModalWidth(true);
            body.innerHTML = `
  ${pathLine}
<p style="margin-bottom:14px;color:var(--bb-text-muted)">Position is <strong style="color:var(--bb-success)">Available</strong>. Store a new blood sample:</p>

<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>Sample ID</label><input id="smId" readonly value="Generating…" style="opacity:0.75;cursor:not-allowed;"></div>
    <div class="bb-form-row"><label>Sample Name *</label><input id="smName" required placeholder="Plasma Sample"></div>
    <div class="bb-form-row"><label>Tube Barcode</label><input id="tbBarcode" placeholder="Scan or enter barcode"></div>
    <div class="bb-form-row"><label>Volume (ml)</label><input id="smVolume" type="number" step="0.1" placeholder="e.g. 5"></div>
</div>

<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>Sample Type</label><input id="smType" placeholder="Plasma"></div>
    <div class="bb-form-row"><label>Owner / PI</label><input id="smOwner" placeholder="Owner name"></div>
    <div class="bb-form-row"><label>Project</label><input id="smProject" placeholder="PRJ-01"></div>
    <div class="bb-form-row"><label>Collected By</label><input id="smCollectedBy" placeholder="Employee name"></div>
</div>

<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>SOP</label><input id="smSOP" placeholder="SOP reference"></div>
    <div class="bb-form-row"><label>Analytics</label><input id="smAnalytics" placeholder="Lab analysis notes"></div>
    <div class="bb-form-row bb-span-2"><label>Sample Notes</label><input id="smNotes" placeholder="Optional"></div>
</div>

<div class="bb-form-grid">
    <div class="bb-form-row">
        <label>FASTA Access Code (.txt file)</label>
        <input type="file" id="smFasta" accept=".txt">
    </div>
    <div class="bb-form-row">
        <label>Sample Attachments</label>
        <input type="file" id="smAttachments" multiple>
        <div style="font-size:0.72rem;color:var(--bb-text-muted);margin-top:4px;">Not wired to storage yet — files chosen here aren't uploaded.</div>
    </div>
</div>

<div id="smError" class="bb-form-error" style="display:none;"></div>
    `;
            actions.innerHTML = `
        <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
        <button class="bb-btn-cancel" id="btnReserve">Reserve</button>
        <button class="bb-modal-btn" id="btnStore">Store Sample</button>
    `;
            $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
            $("btnStore").onclick = () => doStore();
            $("btnReserve").onclick = () => doReserve();

            prefillNextSampleId(); // fire-and-forget; fills #smId once the server responds
        }
    }


    async function openSampleSearch() {
        $("modalTitle").textContent = "Search Samples";
        $("modalTitle").className = "text-light";
        const body = $("modalBody");
        const actions = $("modalActions");

        body.innerHTML = `
    <div class="bb-form-row">
        <input id="sampleSearchInput" class="bb-search-input-xl" placeholder="Search by Sample ID, Name, Owner, Project, FASTA code, or Tube Barcode…" autofocus>
    </div>
    <div id="sampleSearchResults" style="max-height:50vh;overflow-y:auto;margin-top:12px;">
        <p style="color:var(--bb-text-muted);font-size:0.85rem;">Type at least 2 characters…</p>
    </div>
`;
        actions.innerHTML = `<button class="bb-btn-cancel" id="btnCloseModal">Close</button>`;
        $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
        $("bbModal").classList.add("show");

        setModalWidth("xl");

        // Focus after the modal's open transition so the input is actually interactive.
        setTimeout(() => $("sampleSearchInput")?.focus(), 60);

        let searchDebounce = null;
        $("sampleSearchInput").addEventListener("input", function () {
            clearTimeout(searchDebounce);
            const q = this.value;
            searchDebounce = setTimeout(() => runSampleSearch(q), 300);
        });
    }

    async function runSampleSearch(query) {
        const container = $("sampleSearchResults");
        if (!container) return;

        if (!query || query.length < 2) {
            container.innerHTML = `<p style="color:var(--bb-text-muted);font-size:0.85rem;">Type at least 2 characters…</p>`;
            return;
        }

        container.innerHTML = `<div class="bb-loading"><div class="bb-spinner"></div></div>`;

        try {
            const results = await apiGet(`/biobank/api/samples/search?q=${encodeURIComponent(query)}`);
            if (!results.length) {
                container.innerHTML = `<p style="color:var(--bb-text-muted);font-size:0.85rem;">No samples found.</p>`;
                return;
            }
            container.innerHTML = results.map(renderSearchResultRow).join("");
            results.forEach(s => {
                const viewBtn = $(`viewBtn-${s.id}`);
                const editBtn = $(`editBtn-${s.id}`);
                if (viewBtn) viewBtn.onclick = () => goToSampleLocation(s);
                if (editBtn) editBtn.onclick = () => openSampleEditForm(s);
            });
        } catch (e) {
            container.innerHTML = `<p style="color:#fca5a5;font-size:0.85rem;">${e.message}</p>`;
        }
    }

    function renderSearchResultRow(s) {
        const located = s.currentBoxId && s.currentPosition;
        return `
    <div class="bb-search-result-row">
        <div style="flex:1;min-width:0;">
            <div class="text-light " style="font-weight:700;">${s.id} <span style="font-weight:400;color:var(--bb-text-muted);">· ${s.name}</span></div>
            <div style="font-size:0.78rem;color:var(--bb-text-muted);">
                ${bloodTypeLabel(s.bloodType)} · ${s.sampleType || "-"} · ${located ? `${s.currentBoxId} · ${s.currentPosition}` : "Not currently stored"}
            </div>
        </div>
        <div style="display:flex;gap:6px;flex-shrink:0;">
            ${located ? `<button class="bb-btn-cancel" id="viewBtn-${s.id}" style="padding:6px 12px;">View</button>` : ""}
            <button class="bb-modal-btn" id="editBtn-${s.id}" style="margin-top:0;padding:6px 12px;">Edit</button>
        </div>
    </div>`;
    }

    // Add near the other small helpers (e.g. next to bloodTypeLabel)
    function parseUtcDate(dateStr) {
        if (!dateStr) return null;
        // If the string has no timezone marker, the server almost certainly meant
        // UTC (see note above) — force it, instead of letting the browser assume local.
        const hasTz = /Z$|[+-]\d{2}:\d{2}$/.test(dateStr);
        return new Date(hasTz ? dateStr : dateStr + "Z");
    }


    async function openSampleEditForm(sample) {
        setModalWidth(true);
        $("modalTitle").textContent = `Edit ${sample.id}`;
        const body = $("modalBody");
        const actions = $("modalActions");

        body.innerHTML = `<div class="bb-loading"><div class="bb-spinner"></div></div>`;
        actions.innerHTML = "";
        $("bbModal").classList.add("show");

        let tubeBarcode = "";
        if (sample.currentBoxId && sample.currentPosition) {
            try {
                const details = await apiGet(`/biobank/api/boxes/${encodeURIComponent(sample.currentBoxId)}/tubes/${encodeURIComponent(sample.currentPosition)}`);
                tubeBarcode = details.barcode || "";
            } catch { /* ignore */ }
        }

        body.innerHTML = `
<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>Sample ID</label><input value="${sample.id}" readonly style="opacity:0.75;cursor:not-allowed;"></div>
    <div class="bb-form-row"><label>Sample Name *</label><input id="emName" value="${sample.name || ""}"></div>
    <div class="bb-form-row"><label>Tube Barcode</label><input id="emBarcode" value="${tubeBarcode}" placeholder="Scan or enter barcode"></div>
    <div class="bb-form-row"><label>Volume (ml)</label><input id="emVolume" type="number" step="0.1" value="${sample.volumeMl ?? ""}"></div>
</div>

<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>Sample Type</label><input id="emType" value="${sample.sampleType || ""}"></div>
    <div class="bb-form-row"><label>Owner / PI</label><input id="emOwner" value="${sample.patientRef || ""}"></div>
    <div class="bb-form-row"><label>Project</label><input id="emProject" value="${sample.projectCode || ""}"></div>
    <div class="bb-form-row"><label>Collected By</label><input id="emCollectedBy" value="${sample.collectedByEmployeeId || ""}"></div>
</div>

<div class="bb-form-grid-4">
    <div class="bb-form-row"><label>SOP</label><input id="emSOP" value="${sample.sopId || ""}" placeholder="SOP reference"></div>
    <div class="bb-form-row"><label>Analytics</label><input id="emAnalytics" value="${sample.analyticsResults || ""}"></div>
    <div class="bb-form-row bb-span-2"><label>Sample Notes</label><input id="emNotes" value="${sample.notes || ""}"></div>
</div>

<div class="bb-form-grid">
    <div class="bb-form-row">
        <label>FASTA Access Code (.txt file)</label>
        <input type="file" id="emFastaInput" accept=".txt">
        <div id="emFastaList" class="bb-attachment-list"></div>
    </div>
    <div class="bb-form-row">
        <label>Sample Attachments</label>
        <input type="file" id="emAttachmentsInput" multiple>
        <div id="emAttachmentsList" class="bb-attachment-list"></div>
    </div>
</div>

<div id="emError" class="bb-form-error" style="display:none;"></div>
    `;
        actions.innerHTML = `
        <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
        <button class="bb-modal-btn" id="btnSaveEdit">Save Changes</button>
    `;
        $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
        $("btnSaveEdit").onclick = () => doUpdateSample(sample.id);

        // Attachments upload/delete immediately — independent of "Save Changes"
        $("emFastaInput").onchange = async function () {
            const file = this.files[0];
            if (!file) return;
            try {
                await uploadAttachment(sample.id, "Fasta", file);
                this.value = "";
                await loadAttachmentList(sample.id, "Fasta", "emFastaList", true);
            } catch (e) {
                Swal.fire({ icon: "error", title: "Upload failed", text: e.message, ...BB_SWAL_THEME });
            }
        };
        $("emAttachmentsInput").onchange = async function () {
            const files = Array.from(this.files || []);
            this.value = "";
            for (const f of files) {
                try { await uploadAttachment(sample.id, "General", f); }
                catch (e) { Swal.fire({ icon: "error", title: `Upload failed: ${f.name}`, text: e.message, ...BB_SWAL_THEME }); }
            }
            await loadAttachmentList(sample.id, "General", "emAttachmentsList", true);
        };

        await loadAttachmentList(sample.id, "Fasta", "emFastaList", true);
        await loadAttachmentList(sample.id, "General", "emAttachmentsList", true);
    }

    async function doUpdateSample(sampleId) {
        const err = $("emError");
        err.style.display = "none";

        const payload = {
            sampleId,
            name: $("emName").value.trim(),
            volumeMl: $("emVolume").value ? parseFloat($("emVolume").value) : null,
            patientRef: $("emOwner").value.trim() || null,
            sampleType: $("emType").value.trim() || null,
            projectCode: $("emProject").value.trim() || null,
            collectedByEmployeeId: $("emCollectedBy").value.trim() || null,
            sopId: $("emSOP").value.trim() || null,
            analyticsResults: $("emAnalytics").value.trim() || null,
            notes: $("emNotes").value.trim() || null,
            tubeBarcode: $("emBarcode").value.trim() || null,
            performedBy: null
        };

        if (!payload.name) {
            err.textContent = "Sample Name is required.";
            err.style.display = "block";
            return;
        }

        try {
            await apiPost("/biobank/api/samples/update", payload);
            $("bbModal").classList.remove("show");
            Swal.fire({ icon: "success", title: "Saved", timer: 1200, showConfirmButton: false, ...BB_SWAL_THEME });
        } catch (e) {
            err.textContent = e.message;
            err.style.display = "block";
        }
    }


    function readTextFile(file) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsText(file);
        });
    }

    async function doStore() {
        const err = $("smError");
        err.style.display = "none";

        const fastaFile = $("smFasta")?.files?.[0] || null;
        const generalFiles = Array.from($("smAttachments")?.files || []);

        const payload = {
            sampleId: $("smId").value.trim(),
            name: $("smName").value.trim(),
            sampleType: $("smType").value.trim() || null,
            patientRef: $("smOwner").value.trim() || null,
            projectCode: $("smProject").value.trim() || null,
            notes: $("smNotes").value.trim() || null,
            volumeMl: $("smVolume").value ? parseFloat($("smVolume").value) : null,
            collectedByEmployeeId: $("smCollectedBy").value.trim() || null,
            tubeBarcode: $("tbBarcode").value.trim() || null,
            boxId: currentTubeContext.boxId,
            sopId: $("smSOP").value.trim() || null,
            analyticsResults: $("smAnalytics").value.trim() || null,
            position: currentTubeContext.position,
            performedBy: null
        };

        if (!payload.sampleId || !payload.name) {
            err.textContent = "Sample ID and Name are required.";
            err.style.display = "block";
            return;
        }

        try {
            const stored = await apiPost("/biobank/api/samples/store", payload);

            if (fastaFile) {
                try { await uploadAttachment(stored.id, "Fasta", fastaFile); }
                catch (e) { console.warn("FASTA upload failed:", e.message); }
            }
            for (const f of generalFiles) {
                try { await uploadAttachment(stored.id, "General", f); }
                catch (e) { console.warn(`Attachment upload failed (${f.name}):`, e.message); }
            }

            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            err.textContent = e.message;
            err.style.display = "block";
        }
    }


    async function buildFreezerSwalOptions() {
        if (!freezersCache.length) {
            try { freezersCache = await apiGet(API.freezers); } catch { freezersCache = []; }
        }
        const opts = { "": "Any freezer (first available)" };
        freezersCache.forEach(f => { opts[f.id] = `${f.id} (${f.typeDisplay || ""})`; });
        return opts;
    }

    async function openAddSampleWizard() {
        const opts = await buildFreezerSwalOptions();
        const { value: freezerId, isDismissed } = await Swal.fire({
            title: "Add New Sample",
            text: "We'll find the next available tube automatically.",
            input: "select",
            inputOptions: opts,
            inputPlaceholder: "Any freezer (first available)",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Find Empty Slot",
            ...BB_SWAL_THEME
        });
        if (isDismissed) return;

        try {
            const q = freezerId ? `?freezerId=${encodeURIComponent(freezerId)}` : "";
            const slot = await apiGet(`/biobank/api/tubes/next-available${q}`);
            await navigateToBoxLocation(slot.boxId);
            await openModalForPosition(slot.boxId, slot.position);
        } catch (e) {
            Swal.fire({ icon: "error", title: "No empty slot found", text: e.message, ...BB_SWAL_THEME });
        }
    }


    async function doReserve() {
        try {
            await apiPost("/biobank/api/samples/reserve", {
                boxId: currentTubeContext.boxId,
                position: currentTubeContext.position,
                sampleName: $("smName")?.value || "Reserved",
                performedBy: null
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
            // NEW (both places)
        } catch (e) {
            Swal.fire({ icon: "error", title: "Action failed", text: e.message, ...BB_SWAL_THEME });
        }
    }

    async function doCancelReserve() {
        try {
            const res = await fetch(`/biobank/api/boxes/${encodeURIComponent(currentTubeContext.boxId)}/tubes/${encodeURIComponent(currentTubeContext.position)}/reserve`, {
                method: "DELETE"
            });
            if (!res.ok) throw new Error("Cancel failed");
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
            // NEW (both places)
        } catch (e) {
            Swal.fire({ icon: "error", title: "Action failed", text: e.message, ...BB_SWAL_THEME });
        }
    }

    // NEW
    async function doWithdraw(sampleId) {
        $("bbModal").classList.remove("show");
        const { value: reason, isDismissed } = await Swal.fire({
            title: "Withdraw Sample",
            input: "text",
            inputLabel: "Reason",
            inputPlaceholder: "e.g. Sent to lab",
            inputValue: "Sent to lab",
            icon: "warning",
            showCancelButton: true,
            confirmButtonText: "Withdraw",
            inputValidator: (value) => !value ? "A reason is required." : undefined,
            ...BB_SWAL_THEME
        });
        if (isDismissed) return;

        try {
            await apiPost("/biobank/api/samples/withdraw", {
                sampleId,
                reason,
                performedBy: null
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            Swal.fire({ icon: "error", title: "Withdraw failed", text: e.message, ...BB_SWAL_THEME });
        }
    }

    // NEW
    async function doCheckOut(sampleId) {
        $("bbModal").classList.remove("show");
        const { value: formValues, isDismissed } = await Swal.fire({
            title: "Check Out Sample",
            html: `
            <label class="bb-swal-label">Reason (test / experiment) *</label>
            <input id="swalReason" class="swal2-input" placeholder="e.g. PCR analysis" style="margin:0;">
            <label class="bb-swal-label">Expected back in (days)</label>
            <input id="swalDays" type="number" class="swal2-input" value="7" min="0" style="margin:0;">
        `,
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Check Out",
            focusConfirm: false,
            preConfirm: () => {
                const reason = document.getElementById("swalReason").value.trim();
                const days = document.getElementById("swalDays").value;
                if (!reason) {
                    Swal.showValidationMessage("A reason is required.");
                    return false;
                }
                return { reason, days };
            },
            ...BB_SWAL_THEME
        });
        if (isDismissed || !formValues) return;

        let expectedReturnAt = null;
        if (formValues.days !== "" && !isNaN(parseInt(formValues.days, 10))) {
            const d = new Date();
            d.setDate(d.getDate() + parseInt(formValues.days, 10));
            expectedReturnAt = d.toISOString();
        }

        try {
            await apiPost("/biobank/api/samples/checkout", {
                sampleId,
                reason: formValues.reason,
                expectedReturnAt,
                performedBy: null
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            Swal.fire({ icon: "error", title: "Check-out failed", text: e.message, ...BB_SWAL_THEME });
        }
    }

    // NEW
    async function doCheckIn(sampleId) {
        $("bbModal").classList.remove("show");
        const { value: remaining, isDismissed } = await Swal.fire({
            title: "Check In Sample",
            input: "number",
            inputLabel: "Remaining volume (ml) — leave empty if fully consumed",
            inputPlaceholder: "e.g. 2.5",
            icon: "question",
            showCancelButton: true,
            confirmButtonText: "Continue",
            ...BB_SWAL_THEME
        });
        if (isDismissed) return;

        const consumed = remaining === "" || remaining === undefined || parseFloat(remaining) <= 0;

        if (consumed) {
            const confirmResult = await Swal.fire({
                title: "Mark as Disposed?",
                text: "No remaining volume — this sample will be marked Disposed and the slot freed.",
                icon: "warning",
                showCancelButton: true,
                confirmButtonText: "Yes, dispose",
                ...BB_SWAL_THEME
            });
            if (!confirmResult.isConfirmed) return;
        }

        try {
            await apiPost("/biobank/api/samples/checkin", {
                sampleId,
                remainingVolumeMl: consumed ? null : parseFloat(remaining),
                consumed,
                performedBy: null
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            Swal.fire({ icon: "error", title: "Check-in failed", text: e.message, ...BB_SWAL_THEME });
        }
    }

    // NEW — full replacement
    async function showHistory(sampleId) {
        setModalWidth(true); // timeline entries (reason/detail lines) read better with room
        $("modalTitle").textContent = "Sample History";
        const history = await apiGet(`/biobank/api/samples/${encodeURIComponent(sampleId)}/history`);
        const body = $("modalBody");

        body.innerHTML = history.length === 0
            ? `<p style="color:var(--bb-text-muted)">No history yet.</p>`
            : `<div class="bb-timeline">${history.map(renderTimelineItem).join("")}</div>`;
    }

    function renderTimelineItem(h) {
        const info = movementTypeInfo(h.type);
        const when = parseUtcDate(h.performedAt);
        const whenLabel = when
            ? when.toLocaleString(undefined, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" })
            : "-";

        let detail = "";
        if (h.fromPosition && h.toPosition && h.fromPosition !== h.toPosition) {
            detail = `${h.fromBoxId || ""} ${h.fromPosition} &rarr; ${h.toBoxId || ""} ${h.toPosition}`;
        } else if (h.toPosition) {
            detail = `${h.toBoxId || ""} ${h.toPosition}`;
        } else if (h.fromPosition) {
            detail = `${h.fromBoxId || ""} ${h.fromPosition}`;
        }

        return `
    <div class="bb-timeline-item">
        <div class="bb-timeline-dot" style="background:${info.color};">${info.icon}</div>
        <div class="bb-timeline-content">
            <div class="bb-timeline-header">
                <span class="bb-timeline-type" style="color:${info.color}">${info.label}</span>
                <span class="bb-timeline-date">${whenLabel}</span>
            </div>
            ${detail ? `<div class="bb-timeline-detail">${detail}</div>` : ""}
            ${h.reason ? `<div class="bb-timeline-reason">${h.reason}</div>` : ""}
            ${h.performedBy ? `<div class="bb-timeline-by">by ${h.performedBy}</div>` : ""}
        </div>
    </div>`;
    }

    async function apiPost(url, data) {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(data)
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `Request failed (${res.status})`);
        }
        return res.json();
    }
    function closeModal(e) {
        if (!e || e.target.id === "bbModal" || e.target.id === "bbModalCloseBtn") {
            $("bbModal").classList.remove("show");
        }
    }


    function formatFileSize(bytes) {
        if (bytes == null) return "-";
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
        return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
    }

    async function uploadAttachment(sampleId, category, file) {
        const formData = new FormData();
        formData.append("file", file);
        const res = await fetch(API.attachments(sampleId, category), {
            method: "POST",
            body: formData
        });
        if (!res.ok) {
            const err = await res.json().catch(() => ({}));
            throw new Error(err.message || `Upload failed (${res.status})`);
        }
        return res.json();
    }

    async function loadAttachmentList(sampleId, category, containerId, allowDelete) {
        const container = $(containerId);
        if (!container) return;
        container.innerHTML = `<div style="font-size:0.75rem;color:var(--bb-text-muted);">Loading…</div>`;
        try {
            const list = await apiGet(API.attachments(sampleId, category));
            if (!list.length) {
                container.innerHTML = `<div style="font-size:0.75rem;color:var(--bb-text-muted);">No files uploaded.</div>`;
                return;
            }
            container.innerHTML = list.map(a => `
            <div class="bb-attachment-item">
                <div class="bb-attachment-name" title="${a.originalFileName}">${a.originalFileName}</div>
                <div class="bb-attachment-meta">${formatFileSize(a.fileSizeBytes)} · ${new Date(a.uploadedAt).toLocaleDateString()}</div>
                <div class="bb-attachment-actions">
                    <a href="${API.attachmentDownload(a.id)}" class="bb-attachment-btn" title="Download">⬇</a>
                    ${allowDelete ? `<button type="button" class="bb-attachment-btn bb-attachment-btn-danger" data-delete-attachment="${a.id}" title="Delete">✕</button>` : ""}
                </div>
            </div>
        `).join("");

            if (allowDelete) {
                container.querySelectorAll("[data-delete-attachment]").forEach(btn => {
                    btn.onclick = async () => {
                        const id = btn.dataset.deleteAttachment;
                        const confirmResult = await Swal.fire({
                            title: "Delete this file?",
                            text: "This cannot be undone.",
                            icon: "warning",
                            showCancelButton: true,
                            confirmButtonText: "Delete",
                            ...BB_SWAL_THEME
                        });
                        if (!confirmResult.isConfirmed) return;
                        try {
                            const res = await fetch(API.attachmentDelete(id), { method: "DELETE" });
                            if (!res.ok) throw new Error("Delete failed");
                            await loadAttachmentList(sampleId, category, containerId, allowDelete);
                        } catch (e) {
                            Swal.fire({ icon: "error", title: "Delete failed", text: e.message, ...BB_SWAL_THEME });
                        }
                    };
                });
            }
        } catch (e) {
            container.innerHTML = `<div style="font-size:0.75rem;color:#fca5a5;">Couldn't load files.</div>`;
        }
    }

    // ===================== SEARCH =====================
    let searchTimeout = null;
    function handleSearch(query) {
        clearTimeout(searchTimeout);
        if (!query || query.length < 2) return;

        searchTimeout = setTimeout(async () => {
            try {
                const results = await apiGet(API.search(query));
                if (results.length === 1) {
                    navigateTo("freezer", results[0]);
                } else if (results.length > 1) {
                    // For now just go to the first match
                    navigateTo("freezer", results[0]);
                }
            } catch (err) {
                console.warn("Search failed", err);
            }
        }, 350);
    }

    // ===================== EVENTS =====================
    async function handleNavClick(navEl) {
        const view = navEl.dataset.nav;

        if (view === "room") {
            navigateTo("room");
            return;
        }

        if (view === "freezer") {
            const id = navEl.dataset.freezerId;
            let fz = freezersCache.find(f => f.id === id);
            if (!fz) fz = await apiGet(API.freezer(id));
            navigateTo("freezer", fz);
            return;
        }

        if (view === "shelf") {
            const freezer = currentPath[0];
            const shelfIndex = parseInt(navEl.dataset.shelfIndex, 10);
            navigateTo("shelf", { freezer, shelfIndex });
            return;
        }

        if (view === "rack") {
            const freezer = currentPath[0];
            navigateTo("rack", {
                freezer,
                id: navEl.dataset.rackId,
                rackId: navEl.dataset.rackId,
                shelfIndex: parseInt(navEl.dataset.shelfIndex || "0", 10),
                rackIndex: parseInt(navEl.dataset.rackIndex || "0", 10)
            });
            return;
        }

        if (view === "drawer") {
            const freezer = currentPath[0];
            navigateTo("drawer", {
                freezer,
                id: navEl.dataset.drawerId,
                drawerId: navEl.dataset.drawerId,
                drawerIndex: parseInt(navEl.dataset.drawerIndex || "0", 10),
                shelfIndex: parseInt(navEl.dataset.shelfIndex || currentPath[1]?.shelfIndex || "0", 10),
                rackIndex: parseInt(navEl.dataset.rackIndex || currentPath[2]?.rackIndex || "0", 10)
            });
            return;
        }

        if (view === "box") {
            const freezer = currentPath[0];
            navigateTo("box", {
                freezer,
                boxId: navEl.dataset.boxId,
                shelfIndex: parseInt(navEl.dataset.shelfIndex || "0", 10),
                rackIndex: parseInt(navEl.dataset.rackIndex || "0", 10),
                drawerIndex: parseInt(navEl.dataset.drawerIndex || "0", 10)
            });
        }
    }

    function attachEvents() {
        // Document-level so breadcrumb (outside #biobankApp) works
        document.addEventListener("click", async (e) => {
            const linkBtn = e.target.closest("[data-action='link-iot']");
            if (linkBtn) {
                e.preventDefault();
                e.stopPropagation();
                const freezerId = linkBtn.dataset.freezerId;
                if (freezerId) openLinkIoTModal(freezerId);
                return;
            }
            const actionEl = e.target.closest("[data-action='open-modal']");
            if (actionEl) {
                openModal(actionEl);
                return;
            }

            const navEl = e.target.closest("[data-nav]");
            if (navEl) {
                e.preventDefault();
                await handleNavClick(navEl);
            }
        });

        const app = $("biobankApp");
        if (!app) {
            console.error("[BioBank] #biobankApp not found – hover tooltips disabled");
        } else {
            app.addEventListener("mouseover", (e) => {
                const hoverEl = e.target.closest("[data-hover]");
                if (!hoverEl) return;

                const type = hoverEl.dataset.hover;
                if (type === "freezer") {
                    const fz = freezersCache.find(f => f.id === hoverEl.dataset.freezerId);
                    if (fz) showFreezerInfo(fz);
                } else if (type === "shelf") showShelfInfo(hoverEl);
                else if (type === "rack") showRackInfo(hoverEl);
                else if (type === "drawer") showDrawerInfo(hoverEl);
                else if (type === "box") showBoxInfo(hoverEl);
                else if (type === "tube") showTubeInfo(hoverEl, e);
            });

            app.addEventListener("mouseout", (e) => {
                const hoverEl = e.target.closest("[data-hover]");
                if (!hoverEl) return;
                if (hoverEl.contains(e.relatedTarget)) return;
                if (hoverEl.dataset.hover === "tube") hideTooltip();
                else hideInfoPanel();
            });
        }

        const searchBox = $("bbSearch");
        if (searchBox) {
            searchBox.addEventListener("input", function () {
                handleSearch(this.value);
            });
        }

        $("bbModal")?.addEventListener("click", closeModal);
        $("bbModalCloseBtn")?.addEventListener("click", closeModal);

        bindAddFreezerUI();
    }

    // ===================== ADD FREEZER (robust binding) =====================
    function bindAddFreezerUI() {
        // Document-level delegation – works even if button is outside #biobankApp
        if (!window.__bbAddFreezerBound) {
            window.__bbAddFreezerBound = true;
            document.addEventListener("click", function (e) {
                const btn = e.target.closest("#bbBtnAddFreezer");
                if (btn) {
                    e.preventDefault();
                    e.stopPropagation();
                    openAddFreezerModal();
                    return;
                }
                const cancel = e.target.closest("#bbAddFreezerCancel");
                if (cancel) {
                    e.preventDefault();
                    closeAddFreezerModal();
                    return;
                }
            });
            document.addEventListener("click", function (e) {
                if (e.target && e.target.id === "bbAddFreezerModal") {
                    closeAddFreezerModal();
                }
            });
        }

        $("fzType")?.addEventListener("change", function () {
            if (this.value === "2") {
                if ($("fzShelves")) $("fzShelves").value = 0;
                if ($("fzDrawersPerRack")) $("fzDrawersPerRack").value = 0;
                if ($("fzBoxesPerDrawer")) $("fzBoxesPerDrawer").value = 12;
                if ($("fzRacksPerShelf")) $("fzRacksPerShelf").value = 12;
            } else {
                if ($("fzShelves")) $("fzShelves").value = 4;
                if ($("fzDrawersPerRack")) $("fzDrawersPerRack").value = 5;
                if ($("fzBoxesPerDrawer")) $("fzBoxesPerDrawer").value = 5;
                if ($("fzRacksPerShelf")) $("fzRacksPerShelf").value = 4;
            }
        });

        const form = $("bbAddFreezerForm");
        if (form && !form.__bbSubmitBound) {
            form.__bbSubmitBound = true;
            form.addEventListener("submit", async function (e) {
                e.preventDefault();
                await submitAddFreezerForm();
            });
        }
    }

    async function submitAddFreezerForm() {
        const errorEl = $("bbAddFreezerError");
        const submitBtn = $("bbAddFreezerSubmit");
        if (errorEl) errorEl.style.display = "none";
        if (submitBtn) {
            submitBtn.disabled = true;
            submitBtn.textContent = "Creating...";
        }

        const type = parseInt(($("fzType") && $("fzType").value) || "1", 10);
        const payload = {
            id: (($("fzId") && $("fzId").value) || "").trim().toUpperCase(),
            type: type,
            temperature: type === 2 ? -150 : -80,
            barcode: (($("fzBarcode") && $("fzBarcode").value) || "").trim() || null,
            shelvesCount: parseInt(($("fzShelves") && $("fzShelves").value) || "0", 10) || 0,
            racksPerShelf: parseInt(($("fzRacksPerShelf") && $("fzRacksPerShelf").value) || "1", 10) || 1,
            drawersPerRack: parseInt(($("fzDrawersPerRack") && $("fzDrawersPerRack").value) || "0", 10) || 0,
            boxesPerDrawer: parseInt(($("fzBoxesPerDrawer") && $("fzBoxesPerDrawer").value) || "1", 10) || 1,
            tubesPerBox: parseInt(($("fzTubesPerBox") && $("fzTubesPerBox").value) || "100", 10) || 100
        };

        if (!payload.id) {
            if (errorEl) {
                errorEl.textContent = "Freezer ID is required";
                errorEl.style.display = "block";
            }
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = "Create Freezer";
            }
            return;
        }

        try {
            const res = await fetch(API.freezers, {
                method: "POST",
                headers: { "Content-Type": "application/json", "Accept": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                let msg = "Failed (" + res.status + ")";
                try {
                    const err = await res.json();
                    msg = err.message || err.title || msg;
                    if (err.errors) {
                        const first = Object.values(err.errors)[0];
                        if (Array.isArray(first) && first[0]) msg = first[0];
                    }
                } catch (parseErr) { }
                throw new Error(msg);
            }

            const created = await res.json();
            closeAddFreezerModal();
            await navigateTo("room");
            console.log("[BioBank] Freezer created:", created.id || created.Id);
        } catch (err) {
            console.error("[BioBank] Create freezer failed:", err);
            if (errorEl) {
                errorEl.textContent = err.message || "Failed to create freezer";
                errorEl.style.display = "block";
            }
        } finally {
            if (submitBtn) {
                submitBtn.disabled = false;
                submitBtn.textContent = "Create Freezer";
            }
        }
    }

    // ===================== ADD FREEZER =====================
    function openAddFreezerModal() {
        console.log("[BioBank] openAddFreezerModal called");
        // Prefer full implementation; keep global fallback in sync
        window.__bbOpenAddFreezer = openAddFreezerModal;
        const modal = $("bbAddFreezerModal");
        if (!modal) {
            console.error("[BioBank] #bbAddFreezerModal not found in DOM");
            alert("Add Freezer modal not found. Check Index.cshtml has id=bbAddFreezerModal");
            return;
        }
        const errorEl = $("bbAddFreezerError");
        if (errorEl) errorEl.style.display = "none";
        const form = $("bbAddFreezerForm");
        if (form) form.reset();
        if ($("fzShelves")) $("fzShelves").value = 4;
        if ($("fzRacksPerShelf")) $("fzRacksPerShelf").value = 4;
        if ($("fzDrawersPerRack")) $("fzDrawersPerRack").value = 5;
        if ($("fzBoxesPerDrawer")) $("fzBoxesPerDrawer").value = 5;
        if ($("fzTubesPerBox")) $("fzTubesPerBox").value = 100;
        if ($("fzType")) $("fzType").value = "1";

        // Force visible (class + inline styles as backup against CSS conflicts)
        modal.classList.add("show");
        modal.style.opacity = "1";
        modal.style.pointerEvents = "auto";
        modal.style.display = "flex";
        modal.style.zIndex = "9999";
        document.body.style.overflow = "hidden";
    }

    function closeAddFreezerModal() {
        const modal = $("bbAddFreezerModal");
        if (!modal) return;
        modal.classList.remove("show");
        modal.style.opacity = "";
        modal.style.pointerEvents = "";
        modal.style.display = "";
        modal.style.zIndex = "";
        document.body.style.overflow = "";
    }

    // ===================== INIT =====================
    function init() {
        console.log("[BioBank] Init started");
        try {
            attachEvents();
        } catch (err) {
            console.error("[BioBank] attachEvents failed:", err);
            // Still try to bind Add Freezer even if other events failed
            try { bindAddFreezerUI(); } catch (e2) { console.error(e2); }
        }
        navigateTo("room");
    }

    // Expose for debugging / external calls
    // NEW
    window.BioBank = {
        navigateTo,
        refresh: () => navigateTo(currentView, currentPath[0] || null),
        openAddFreezer: openAddFreezerModal,
        openAddSample: openAddSampleWizard,
        openSampleSearch: openSampleSearch
    };
    window.__bbOpenAddFreezer = openAddFreezerModal;
    window.__bbCloseAddFreezer = closeAddFreezerModal;
    window.__bbOpenAddSample = openAddSampleWizard;
    window.__bbOpenSampleSearch = openSampleSearch;

    // Start
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
