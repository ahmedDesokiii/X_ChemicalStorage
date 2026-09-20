// wwwroot/js/biobank.js
(function () {
    "use strict";

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
        search: (q) => `/biobank/api/search?q=${encodeURIComponent(q)}`
    };

    // ===================== STATE =====================
    let currentView = "room";
    let currentPath = [];          // [freezer, shelf?, rack?, drawer?, box?]
    let freezersCache = [];

    // ===================== HELPERS =====================
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
    async function navigateTo(view, data = null) {
        currentView = view;
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
            updateMinimap();
            hideInfoPanel();
        } catch (err) {
            console.error(err);
            $("bbViewport").innerHTML = `
                <div class="bb-loading-screen">
                    <div style="font-size:2rem">⚠️</div>
                    <div>${err.message || "Failed to load data"}</div>
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
            html += `<div class="bb-bcrumb-item ${currentView === "shelf" ? "active" : ""}" data-nav="shelf" data-shelf-index="${sIdx}"><span>📚</span> S${String(sIdx + 1).padStart(2, "0")}</div>`;
        }

        if (["rack", "drawer", "box"].includes(currentView) && currentPath[2]) {
            const rIdx = currentPath[2].rackIndex ?? 0;
            const rId = currentPath[2].id || "";
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "rack" ? "active" : ""}" data-nav="rack" data-rack-id="${rId}" data-shelf-index="${currentPath[1]?.shelfIndex ?? 0}" data-rack-index="${rIdx}"><span>🧰</span> R${String(rIdx + 1).padStart(2, "0")}</div>`;
        }

        if (["drawer", "box"].includes(currentView) && currentPath[3]) {
            const dIdx = currentPath[3].drawerIndex ?? 0;
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item ${currentView === "drawer" ? "active" : ""}" data-nav="drawer" data-drawer-id="${currentPath[3].id || ""}" data-drawer-index="${dIdx}"><span>🗄️</span> D${String(dIdx + 1).padStart(2, "0")}</div>`;
        }

        if (currentView === "box" && currentPath[4]) {
            html += `<span class="bb-bcrumb-sep">›</span>`;
            html += `<div class="bb-bcrumb-item active"><span>📦</span> ${currentPath[4].boxId || currentPath[4].id || "Box"}</div>`;
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

        let html = `<div class="bb-room bb-stagger">`;
        for (const fz of freezersCache) {
            const occColor = getOccupancyColor(fz.occupancyPercent);
            const is150 = fz.type === 2 || fz.type === "Minus150";
            let shelfBars = "";
            if (!is150) {
                for (let i = 0; i < Math.min(fz.shelvesCount || 4, 6); i++) {
                    shelfBars += `<div class="bb-fz-shelf"></div>`;
                }
            } else {
                shelfBars = `<div class="bb-fz-shelf" style="flex:0.35"></div>`;
            }

            html += `
            <div class="bb-freezer" data-nav="freezer" data-hover="freezer" data-freezer-id="${fz.id}">
                <div class="bb-freezer-header">
                    <div class="bb-freezer-id">${fz.id}</div>
                    <div class="bb-temp-badge ${is150 ? "cold" : ""}">🌡️ ${fz.temperature}°C</div>
                </div>
                <div class="bb-freezer-body">
                    <div class="bb-freezer-visual">
                        <div class="bb-freezer-door"></div>
                        <div class="bb-freezer-handle"></div>
                        <div class="bb-freezer-shelves">${shelfBars}</div>
                    </div>
                    <div class="bb-freezer-stats">
                        <div class="bb-stat-row"><span class="bb-stat-label">Type</span><span class="bb-stat-value">${fz.typeDisplay || (is150 ? "-150°C" : "-80°C")}</span></div>
                        <div class="bb-stat-row"><span class="bb-stat-label">Shelves</span><span class="bb-stat-value">${fz.shelvesCount || "-"}</span></div>
                        <div class="bb-stat-row"><span class="bb-stat-label">Racks</span><span class="bb-stat-value">${is150 ? fz.racksPerShelf : (fz.shelvesCount * fz.racksPerShelf)}</span></div>
                        <div class="bb-stat-row"><span class="bb-stat-label">Capacity</span><span class="bb-stat-value">${formatNumber(fz.totalCapacity)} tubes</span></div>
                        <div class="bb-occupancy-bar">
                            <div class="bb-occ-label"><span>Occupancy</span><span style="color:${occColor}">${fz.occupancyPercent}%</span></div>
                            <div class="bb-occ-track"><div class="bb-occ-fill" style="width:${fz.occupancyPercent}%;background:linear-gradient(90deg,${occColor},var(--bb-accent))"></div></div>
                        </div>
                        <div class="bb-freezer-status"><span class="bb-status-dot"></span> ${fz.status || "ONLINE"}</div>
                    </div>
                </div>
            </div>`;
        }
        html += `</div>`;
        container.innerHTML = html;
    }

    // ===================== RENDER: FREEZER =====================
    async function renderFreezer(fz) {
        const is150 = fz.type === 2 || fz.type === "Minus150";
        const container = $("bbViewport");
        container.className = "bb-viewport bb-zoom-in";

        if (is150) {
            const racks = await apiGet(API.racksByFreezer(fz.id));
            let html = `<div class="bb-racks-container bb-stagger">`;
            for (const r of racks) {
                html += `
                <div class="bb-rack" data-nav="rack" data-hover="rack"
                     data-rack-id="${r.id}" data-shelf-index="0" data-rack-index="${r.rackIndex}" data-occ="${r.occupancyPercent}">
                    <div class="bb-rack-visual"><div class="bb-rack-drawer" style="top:8px"></div></div>
                    <div style="font-weight:700;margin-bottom:4px;">${r.displayName || r.id}</div>
                    <div style="font-size:0.8rem;color:var(--bb-text-muted);">${r.boxesCount} Box · ${formatNumber(r.totalTubes)} Tubes</div>
                    <div style="margin-top:8px;"><div class="bb-occ-track"><div class="bb-occ-fill" style="width:${r.occupancyPercent}%"></div></div></div>
                </div>`;
            }
            html += `</div>`;
            container.innerHTML = html;
        } else {
            const shelves = await apiGet(API.shelves(fz.id));
            let html = `<div class="bb-shelves-container bb-stagger">`;
            for (const s of shelves) {
                let rackSlots = "";
                for (let i = 0; i < (s.racksCount || 4); i++) rackSlots += `<div class="bb-shelf-rack-slot"></div>`;
                html += `
                <div class="bb-shelf" data-nav="shelf" data-hover="shelf"
                     data-shelf-index="${s.shelfIndex}" data-shelf-id="${s.id}"
                     data-racks="${s.racksCount}" data-drawers="${s.drawersCount}"
                     data-boxes="${s.boxesCount}" data-tubes="${s.totalTubes}" data-occ="${s.occupancyPercent}">
                    <div class="bb-shelf-visual">${rackSlots}</div>
                    <div style="font-weight:700;font-size:1.1rem;margin-bottom:6px;">${s.displayName || s.id}</div>
                    <div style="font-size:0.85rem;color:var(--bb-text-muted);margin-bottom:8px;">
                        ${s.racksCount} Racks · ${s.drawersCount} Drawers · ${s.boxesCount} Boxes
                    </div>
                    <div class="bb-occ-track"><div class="bb-occ-fill" style="width:${s.occupancyPercent}%"></div></div>
                    <div style="display:flex;justify-content:space-between;font-size:0.8rem;margin-top:4px;">
                        <span style="color:var(--bb-text-muted)">Occupancy</span>
                        <span style="color:var(--bb-primary-light);font-weight:600">${s.occupancyPercent}%</span>
                    </div>
                </div>`;
            }
            html += `</div>`;
            container.innerHTML = html;
        }
    }

    // ===================== RENDER: SHELF =====================
    async function renderShelf(fz, shelfIndex) {
        const racks = await apiGet(API.racksByShelf(fz.id, shelfIndex));
        const container = $("bbViewport");
        container.className = "bb-viewport bb-zoom-in";

        let html = `<div class="bb-racks-container bb-stagger">`;
        for (const r of racks) {
            let drawers = "";
            for (let d = 0; d < (r.drawersCount || 5); d++) {
                drawers += `<div class="bb-rack-drawer" style="top:${8 + d * 14}px"></div>`;
            }
            html += `
            <div class="bb-rack" data-nav="rack" data-hover="rack"
                 data-rack-id="${r.id}" data-shelf-index="${shelfIndex}" data-rack-index="${r.rackIndex}" data-occ="${r.occupancyPercent}">
                <div class="bb-rack-visual">${drawers}</div>
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
        container.className = "bb-viewport bb-zoom-in";

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
                    <div class="bb-drawer-visual"><div class="bb-drawer-handle"></div></div>
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
        container.className = "bb-viewport bb-zoom-in";

        let html = `<div class="bb-boxes-container bb-stagger">`;
        for (const b of boxes) {
            html += renderBoxCard(b, data);
        }
        html += `</div>`;
        container.innerHTML = html;
    }

    function renderBoxCard(b, parentData) {
        const occCells = Math.floor((b.occupancyPercent || 0) / 4);
        let cells = "";
        for (let c = 0; c < 25; c++) {
            cells += `<div class="bb-box-cell ${c < occCells ? "occupied" : ""}"></div>`;
        }
        return `
        <div class="bb-box" data-nav="box" data-hover="box"
             data-box-id="${b.id}" data-box-index="${b.boxIndex}"
             data-shelf-index="${parentData.shelfIndex ?? 0}"
             data-rack-index="${parentData.rackIndex ?? 0}"
             data-drawer-index="${parentData.drawerIndex ?? 0}"
             data-occ="${b.occupancyPercent}">
            <div class="bb-box-visual">${cells}</div>
            <div style="font-weight:700;margin-bottom:4px;font-size:0.9rem;">${b.displayName || b.id}</div>
            <div style="font-size:0.8rem;color:var(--bb-text-muted);">100 Tubes</div>
            <div style="margin-top:6px;font-size:0.8rem;">
                <span style="color:${getOccupancyColor(b.occupancyPercent)};font-weight:600">${b.occupancyPercent}%</span> occupied
            </div>
        </div>`;
    }

    // ===================== RENDER: BOX (10x10) =====================
    async function renderBox(boxId) {
        const box = await apiGet(API.boxDetails(boxId));
        const container = $("bbViewport");
        container.className = "bb-viewport bb-zoom-in";

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

                if (tube) {
                    if (tube.status === 1 || tube.status === "Occupied") {
                        stateClass = "occupied";
                        isOccupied = true;
                    } else if (tube.status === 2 || tube.status === "Reserved") {
                        stateClass = "reserved";
                        isReserved = true;
                    }
                }

                const tubeId = tube ? tube.id : `${boxId}-${pos}`;
                tubes += `<div class="bb-tube ${stateClass}"
                    data-hover="tube" data-action="open-modal"
                    data-tube-id="${tubeId}" data-pos="${pos}"
                    data-occupied="${isOccupied}" data-reserved="${isReserved}"
                    data-sample-id="${tube?.sampleId || ""}"
                    data-sample-name="${tube?.sampleName || ""}"
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
                <div style="margin-top:16px;display:flex;gap:16px;justify-content:center;font-size:0.8rem;">
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:linear-gradient(135deg,var(--bb-accent),var(--bb-cyan));"></div>
                        <span style="color:var(--bb-text-muted)">Occupied (${box.occupiedCount})</span>
                    </div>
                    <div style="display:flex;align-items:center;gap:6px;">
                        <div style="width:12px;height:12px;border-radius:3px;background:linear-gradient(135deg,var(--bb-warning),var(--bb-gold));"></div>
                        <span style="color:var(--bb-text-muted)">Reserved (${box.reservedCount})</span>
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
        $("infoIcon").textContent = "📚";
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
        $("infoIcon").textContent = "🧰";
        $("infoTitle").textContent = el.dataset.rackId || "Rack";
        $("infoSubtitle").textContent = "Storage Rack";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ONLINE</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showDrawerInfo(el) {
        $("infoIcon").textContent = "🗄️";
        $("infoTitle").textContent = el.dataset.drawerId || "Drawer";
        $("infoSubtitle").textContent = "Storage Drawer";
        $("infoContent").innerHTML = `
            <div class="bb-info-row"><span class="label">Occupancy</span><span class="value" style="color:${getOccupancyColor(+el.dataset.occ)}">${el.dataset.occ}%</span></div>
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-success)">● ONLINE</span></div>`;
        $("bbInfoPanel").classList.add("show");
    }

    function showBoxInfo(el) {
        $("infoIcon").textContent = "📦";
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
        const status = isOccupied ? "Occupied" : (isReserved ? "Reserved" : "Available");
        const statusColor = isOccupied ? "var(--bb-accent)" : (isReserved ? "var(--bb-warning)" : "var(--bb-success)");

        $("ttContent").innerHTML = `
            <div class="bb-tooltip-row"><span>Position</span><span>${el.dataset.pos}</span></div>
            <div class="bb-tooltip-row"><span>Status</span><span style="color:${statusColor};font-weight:600">${status}</span></div>
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

    async function openModal(tubeEl) {
        const pos = tubeEl.dataset.pos;
        const boxId = currentPath[4]?.boxId || currentPath[4]?.id;
        if (!boxId || !pos) return;

        currentTubeContext = { boxId, position: pos, tubeId: tubeEl.dataset.tubeId };

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
            title.textContent = `Position ${pos}`;
            body.innerHTML = `<p style="color:#fca5a5">${err.message}</p>`;
            actions.innerHTML = `<button class="bb-btn-cancel" onclick="document.getElementById('bbModal').classList.remove('show')">Close</button>`;
        }
    }

    function renderTubeModal(details) {
        const title = $("modalTitle");
        const body = $("modalBody");
        const actions = $("modalActions");
        const status = details.tubeStatus; // 0 Available, 1 Occupied, 2 Reserved (or string)

        const isOccupied = status === 1 || status === "Occupied";
        const isReserved = status === 2 || status === "Reserved";
        const sample = details.sample;

        title.textContent = `Position ${details.position}`;

        if (isOccupied && sample) {
            body.innerHTML = `
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-accent)">Occupied</span></div>
            <div class="bb-info-row"><span class="label">Sample ID</span><span class="value">${sample.id}</span></div>
            <div class="bb-info-row"><span class="label">Name</span><span class="value">${sample.name}</span></div>
            <div class="bb-info-row"><span class="label">Type</span><span class="value">${sample.sampleType || "-"}</span></div>
            <div class="bb-info-row"><span class="label">Project</span><span class="value">${sample.projectCode || "-"}</span></div>
            <div class="bb-info-row"><span class="label">Freezer</span><span class="value">${sample.currentFreezerId || "-"}</span></div>
            <div class="bb-info-row"><span class="label">Stored</span><span class="value">${sample.createdAt ? new Date(sample.createdAt).toLocaleString() : "-"}</span></div>
            ${sample.notes ? `<p style="margin-top:10px;font-size:0.85rem;color:var(--bb-text-muted)">${sample.notes}</p>` : ""}
        `;
            actions.innerHTML = `
            <button class="bb-btn-cancel" id="btnCloseModal">Close</button>
            <button class="bb-modal-btn" style="background:linear-gradient(135deg,#f59e0b,#ef4444)" id="btnWithdraw">Withdraw</button>
            <button class="bb-modal-btn" id="btnHistory">History</button>
        `;
            $("btnCloseModal").onclick = () => $("bbModal").classList.remove("show");
            $("btnWithdraw").onclick = () => doWithdraw(sample.id);
            $("btnHistory").onclick = () => showHistory(sample.id);
        }
        else if (isReserved) {
            body.innerHTML = `
            <div class="bb-info-row"><span class="label">Status</span><span class="value" style="color:var(--bb-warning)">Reserved</span></div>
            <div class="bb-info-row"><span class="label">Sample</span><span class="value">${sample?.id || "-"}</span></div>
            <div class="bb-info-row"><span class="label">Name</span><span class="value">${sample?.name || "-"}</span></div>
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
            // Available
            body.innerHTML = `
            <p style="margin-bottom:12px;color:var(--bb-text-muted)">Position is <strong style="color:var(--bb-success)">Available</strong>. Store a new sample:</p>
            <div class="bb-form-row"><label>Sample ID *</label><input id="smId" required placeholder="SMP-2026-00001"></div>
            <div class="bb-form-row"><label>Name *</label><input id="smName" required placeholder="Plasma Sample"></div>
            <div class="bb-form-grid">
                <div class="bb-form-row"><label>Type</label><input id="smType" placeholder="Plasma"></div>
                <div class="bb-form-row"><label>Project</label><input id="smProject" placeholder="PRJ-01"></div>
            </div>
            <div class="bb-form-row"><label>Notes</label><input id="smNotes" placeholder="Optional"></div>
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
        }
    }

    async function doStore() {
        const err = $("smError");
        err.style.display = "none";
        const payload = {
            sampleId: $("smId").value.trim(),
            name: $("smName").value.trim(),
            sampleType: $("smType").value.trim() || null,
            projectCode: $("smProject").value.trim() || null,
            notes: $("smNotes").value.trim() || null,
            boxId: currentTubeContext.boxId,
            position: currentTubeContext.position,
            performedBy: "web-user"
        };
        if (!payload.sampleId || !payload.name) {
            err.textContent = "Sample ID and Name are required.";
            err.style.display = "block";
            return;
        }
        try {
            await apiPost("/biobank/api/samples/store", payload);
            $("bbModal").classList.remove("show");
            // refresh box view
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            err.textContent = e.message;
            err.style.display = "block";
        }
    }

    async function doReserve() {
        try {
            await apiPost("/biobank/api/samples/reserve", {
                boxId: currentTubeContext.boxId,
                position: currentTubeContext.position,
                sampleName: $("smName")?.value || "Reserved",
                performedBy: "web-user"
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            alert(e.message);
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
        } catch (e) {
            alert(e.message);
        }
    }

    async function doWithdraw(sampleId) {
        const reason = prompt("Withdrawal reason:", "Sent to lab");
        if (reason === null) return;
        try {
            await apiPost("/biobank/api/samples/withdraw", {
                sampleId,
                reason,
                performedBy: "web-user"
            });
            $("bbModal").classList.remove("show");
            if (currentPath[4]?.boxId) await navigateTo("box", currentPath[4]);
        } catch (e) {
            alert(e.message);
        }
    }

    async function showHistory(sampleId) {
        const history = await apiGet(`/biobank/api/samples/${encodeURIComponent(sampleId)}/history`);
        const body = $("modalBody");
        body.innerHTML = history.length === 0
            ? `<p style="color:var(--bb-text-muted)">No history yet.</p>`
            : history.map(h => `
            <div class="bb-info-row">
                <span class="label">${h.type} · ${new Date(h.performedAt).toLocaleString()}</span>
                <span class="value">${h.fromPosition || ""} ${h.toPosition ? "→ " + h.toPosition : ""}</span>
            </div>
            ${h.reason ? `<div style="font-size:0.8rem;color:var(--bb-text-muted);margin-bottom:8px">${h.reason}</div>` : ""}
        `).join("");
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
    function attachEvents() {
        const app = $("biobankApp");

        app.addEventListener("click", async (e) => {
            const actionEl = e.target.closest("[data-action='open-modal']");
            if (actionEl) {
                openModal(actionEl);
                return;
            }

            const navEl = e.target.closest("[data-nav]");
            if (!navEl) return;

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
        });

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

        const searchBox = $("bbSearch");
        if (searchBox) {
            searchBox.addEventListener("input", function () {
                handleSearch(this.value);
            });
        }

        $("bbModal")?.addEventListener("click", closeModal);
        $("bbModalCloseBtn")?.addEventListener("click", closeModal);
    }




    // ===================== ADD FREEZER =====================
    function openAddFreezerModal() {
        $("bbAddFreezerError").style.display = "none";
        $("bbAddFreezerForm").reset();
        $("fzShelves").value = 4;
        $("fzRacksPerShelf").value = 4;
        $("fzDrawersPerRack").value = 5;
        $("fzBoxesPerDrawer").value = 5;
        $("fzTubesPerBox").value = 100;
        $("bbAddFreezerModal").classList.add("show");
    }

    function closeAddFreezerModal() {
        $("bbAddFreezerModal").classList.remove("show");
    }

    // Type change → adjust defaults
    document.getElementById("fzType")?.addEventListener("change", function () {
        if (this.value === "2") { // -150
            $("fzShelves").value = 0;
            $("fzDrawersPerRack").value = 0;
            $("fzBoxesPerDrawer").value = 12;   // ← 12 Box لكل Rack
            $("fzRacksPerShelf").value = 12;
        } else {
            $("fzShelves").value = 4;
            $("fzDrawersPerRack").value = 5;
            $("fzBoxesPerDrawer").value = 5;
            $("fzRacksPerShelf").value = 4;
        }
    });

    document.getElementById("bbBtnAddFreezer")?.addEventListener("click", openAddFreezerModal);
    document.getElementById("bbAddFreezerCancel")?.addEventListener("click", closeAddFreezerModal);

    $("bbAddFreezerModal")?.addEventListener("click", function (e) {
        if (e.target.id === "bbAddFreezerModal") closeAddFreezerModal();
    });

    $("bbAddFreezerForm")?.addEventListener("submit", async function (e) {
        e.preventDefault();

        const errorEl = $("bbAddFreezerError");
        const submitBtn = $("bbAddFreezerSubmit");
        errorEl.style.display = "none";
        submitBtn.disabled = true;
        submitBtn.textContent = "Creating...";

        const type = parseInt($("fzType").value, 10);
        const payload = {
            id: $("fzId").value.trim().toUpperCase(),
            type: type,
            temperature: type === 2 ? -150 : -80,
            barcode: $("fzBarcode").value.trim() || null,
            shelvesCount: parseInt($("fzShelves").value, 10) || 0,
            racksPerShelf: parseInt($("fzRacksPerShelf").value, 10) || 1,
            drawersPerRack: parseInt($("fzDrawersPerRack").value, 10) || 0,
            boxesPerDrawer: parseInt($("fzBoxesPerDrawer").value, 10) || 1,
            tubesPerBox: parseInt($("fzTubesPerBox").value, 10) || 100
        };

        try {
            const res = await fetch("/biobank/api/freezers", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify(payload)
            });

            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || `Failed (${res.status})`);
            }

            const created = await res.json();
            closeAddFreezerModal();

            // Refresh room view
            await navigateTo("room");

            // Optional: highlight the new freezer
            console.log("[BioBank] Freezer created:", created.id);
        } catch (err) {
            errorEl.textContent = err.message || "Failed to create freezer";
            errorEl.style.display = "block";
        } finally {
            submitBtn.disabled = false;
            submitBtn.textContent = "Create Freezer";
        }
    });



    // ===================== INIT =====================
    async function navigateTo(view, data = null) {
        currentView = view;
        clearViewport();
        showLoading();

        try {
            console.log("[BioBank] Navigating to:", view, data);

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
            await updateMinimap();
            hideInfoPanel();
            console.log("[BioBank] Render done");
        } catch (err) {
            console.error("[BioBank] Error:", err);
            $("bbViewport").innerHTML = `
            <div class="bb-loading-screen">
                <div style="font-size:2rem">⚠️</div>
                <div style="max-width:420px;text-align:center;">
                    <strong>Failed to load Bio Bank</strong><br>
                    <span style="font-size:0.85rem;color:var(--bb-text-muted)">${err.message || err}</span>
                </div>
                <button class="bb-modal-btn" style="margin-top:16px" onclick="window.BioBank.navigateTo('room')">
                    Retry
                </button>
            </div>`;
        }
    }

    function init() {
        console.log("[BioBank] Init started");
        attachEvents();
        navigateTo("room");
    }

    // Expose for debugging / external calls
    window.BioBank = {
        navigateTo,
        refresh: () => navigateTo(currentView, currentPath[0] || null)
    };

    // Start
    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();