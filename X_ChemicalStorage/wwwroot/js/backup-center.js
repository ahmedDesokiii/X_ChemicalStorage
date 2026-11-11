// ===== Settings → Backup =====
(() => {
    "use strict";
    const { $, esc, api, toast, fail, showRestart, wireRestart } = window.SiteSettings;
    const can = k => !window.can || window.can(k);
    const SCOPES = { full: "vaultFull", biobank: "vaultBio" };
    const NAMES = { full: "Whole application", biobank: "Bio Bank data" };
    let data = null, tab = "full", cfg = null;

    // ---------- formatting
    const utc = iso => !iso ? null : new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : iso + "Z");
    const local = iso => !iso ? null : new Date(iso);                       // already Cairo time from the server
    function ago(d) {
        if (!d) return "never";
        const s = (Date.now() - d.getTime()) / 1000;
        if (s < 45) return "just now";
        if (s < 3600) return `${Math.round(s / 60)} min ago`;
        if (s < 86400) return `${Math.round(s / 3600)} h ago`;
        const days = Math.round(s / 86400);
        return days === 1 ? "yesterday" : `${days} days ago`;
    }
    function until(d) {
        if (!d) return "—";
        const s = (d.getTime() - Date.now()) / 1000;
        if (s <= 60) return "any minute";
        if (s < 3600) return `in ${Math.round(s / 60)} min`;
        if (s < 86400) return `in ${Math.round(s / 3600)} h`;
        return d.toLocaleString([], { weekday: "short", hour: "2-digit", minute: "2-digit" });
    }
    const when = d => d ? d.toLocaleString([], { day: "2-digit", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "—";
    function size(b) {
        if (b == null) return "—";
        const u = ["B", "KB", "MB", "GB", "TB"]; let i = 0, v = b;
        while (v >= 1024 && i < u.length - 1) { v /= 1024; i++; }
        return `${v >= 100 || i === 0 ? Math.round(v) : v.toFixed(1)} ${u[i]}`;
    }
    const typeLabel = t => ({ Auto: "Automatic", Manual: "Manual", PreRestore: "Safety copy", Uploaded: "Uploaded" }[t] || t);

    // ---------- load + render
    async function load() {
        data = await api("GET", "/biobank/api/backup/center");
        renderVault("full", data.full, 24);
        renderVault("biobank", data.biobank, data.biobank.enabled ? parseHours(data.biobank.schedule) : 24);
        renderList();
        renderActivity();
        renderMaint(data.maintenance);
    }
    const parseHours = s => Number((s || "").match(/\d+/)?.[0] || 6);

    function renderVault(scope, v, periodHours) {
        const root = $(SCOPES[scope]);
        const health = root.querySelector("[data-health]");
        const last = utc(v.lastSuccessAt);
        const ageH = last ? (Date.now() - last.getTime()) / 3600000 : Infinity;

        let cls, state;
        if (!v.files.length && !last) { cls = "bad"; state = "No backup yet"; }
        else if (!v.enabled) { cls = "off"; state = "Automatic backups off"; }
        else if (v.warning) { cls = "warn"; state = "Needs attention"; }
        else { cls = "ok"; state = "Protected"; }
        health.className = "bk-health " + cls;
        health.querySelector("[data-state]").textContent = state;

        const next = scope === "full" ? local(v.nextRunLocal) : utc(v.nextRunUtc);
        health.querySelector("[data-meta]").innerHTML =
            (v.warning ? `${esc(v.warning)}<br>` : "") +
            `Last backup <b>${esc(ago(last))}</b>` + (v.enabled ? ` · next ${esc(until(next))} · ${esc(v.schedule.toLowerCase())}` : "");

        // ring = freshness of the last backup against its schedule
        const fresh = !isFinite(ageH) ? 0 : Math.max(0.06, Math.min(1, 1 - ageH / (periodHours * 2)));
        const fg = root.querySelector(".bk-ring-fg");
        requestAnimationFrame(() => fg.style.strokeDashoffset = String(97.4 * (1 - fresh)));

        root.querySelector("[data-facts]").innerHTML = `
            <div><dt>Restore points</dt><dd>${v.files.length}</dd></div>
            <div><dt>Space used</dt><dd>${size(v.totalBytes)}</dd></div>
            <div><dt>Kept for</dt><dd>${v.keepDays} days</dd></div>`;
        const count = document.querySelector(`[data-count="${scope}"]`);
        if (count) count.textContent = v.files.length ? `(${v.files.length})` : "";
    }

    function renderList() {
        const v = data[tab];
        const list = $("bkList");
        list.className = "bk-list" + (tab === "biobank" ? " bio" : "");
        if (!v.files.length) {
            list.innerHTML = `<div class="bk-empty"><i class="bi bi-inbox"></i> No ${tab === "full" ? "application" : "Bio Bank"} backups yet. Press <b>Back up now</b> to make the first one.</div>`;
            return;
        }
        const canRestore = can("Backup.Restore") && !data.maintenance.active;
        const firstReal = v.files.find(f => f.type === "Auto" || f.type === "Manual");
        list.innerHTML = v.files.map(f => {
            const d = utc(f.modifiedUtc);
            const s = f.summary || {};
            const info = tab === "biobank"
                ? (f.readable
                    ? `<span><b>${s.freezers ?? 0}</b> freezers</span><span><b>${(s.samples ?? 0).toLocaleString()}</b> samples</span><span><b>${(s.readings ?? 0).toLocaleString()}</b> readings</span><span><b>${s.attachments ?? 0}</b> files</span>`
                    : `<span style="color:var(--bk-bad)">Cannot be opened</span>`)
                : `<span><b>${size(f.sizeBytes)}</b> database</span>` + (f.attachmentsFile ? `<span><b>${size(f.attachmentsBytes)}</b> attachments</span>` : "");
            return `
            <div class="bk-row ${f === firstReal ? "latest" : ""}">
                <span class="bk-dot"></span>
                <div class="bk-row-main">
                    <div class="bk-row-when">${esc(when(d))} <small>${esc(ago(d))}</small> <span class="bk-chip ${esc(f.type)}">${esc(typeLabel(f.type))}</span></div>
                    <div class="bk-row-file" title="${esc(f.name)}">${esc(f.name)}${f.createdBy && f.createdBy !== "system" ? ` · ${esc(f.createdBy)}` : ""}</div>
                </div>
                <div class="bk-row-info">${tab === "biobank" ? info + `<span>${size(f.sizeBytes)}</span>` : info}</div>
                <div class="bk-row-actions">
                    <a class="ss-btn sm" href="/biobank/api/backup/${tab}/download?file=${encodeURIComponent(f.name)}" title="Download"><i class="bi bi-download"></i></a>
                    ${canRestore && f.readable !== false ? `<button type="button" class="ss-btn sm ghost-danger" data-restore="${esc(f.name)}"><i class="bi bi-arrow-counterclockwise"></i> Restore</button>` : ""}
                </div>
            </div>`;
        }).join("");
    }

    function renderActivity() {
        const a = data.activity || [];
        const verb = { backup: "Backup", upload: "Upload", restore: "Restore" };
        $("bkActivity").innerHTML = a.length ? a.map(x => `
            <li class="${x.ok ? "ok" : "fail"}">
                <i class="bi ${x.ok ? (x.action === "restore" ? "bi-arrow-counterclockwise" : x.action === "upload" ? "bi-cloud-arrow-up" : "bi-check-circle-fill") : "bi-x-octagon-fill"}"></i>
                <div>
                    <div class="bk-act-title">${esc(verb[x.action] || x.action)} · ${esc(NAMES[x.scope] || x.scope)}${x.ok ? "" : " failed"}</div>
                    <div class="bk-act-meta">${esc(when(utc(x.at)))}${x.user ? " · " + esc(x.user) : ""}${x.message ? " · " + esc(x.message) : ""}</div>
                </div>
            </li>`).join("") : `<li class="bk-empty">Nothing yet.</li>`;
    }

    function renderMaint(m) {
        $("bkMaint").hidden = !m?.active;
        if (m?.active) $("bkMaintText").textContent = (m.reason || "Restore in progress") + ". Freezer data intake is paused until it finishes.";
    }

    // ---------- settings
    async function loadSettings() {
        if (!$("setSave")) return;
        try {
            cfg = await api("GET", "/biobank/api/site-config");
            const b = cfg.backup;
            $("setEnabled").checked = !!b.enabled; $("setDir").value = b.directory || ""; $("setDir2").value = b.secondaryDirectory || "";
            $("setKeep").value = b.keepDays;
            if ($("setFullEnabled")) { $("setFullEnabled").checked = !!b.enabled; $("setFullTime").value = b.dailyTime || "02:00"; }
            if ($("setBioEnabled")) { $("setBioEnabled").checked = !!b.bioEnabled; $("setBioHours").value = b.bioEveryHours; }
        } catch { /* no settings permission: the panels are not rendered anyway */ }
    }

    async function saveSettings(btn) {
        const num = id => { const el = $(id); if (!el) return null; const v = el.value.trim(); return v === "" ? null : Number(v); };
        const body = {
            backupEnabled: $("setEnabled")?.checked ?? null,
            backupDirectory: $("setDir")?.value ?? null,
            backupSecondaryDirectory: $("setDir2")?.value ?? null,
            backupKeepDays: num("setKeep"),
            backupDailyTime: $("setFullTime")?.value || null,
            backupBioEnabled: $("setBioEnabled")?.checked ?? null,
            backupBioEveryHours: num("setBioHours")
        };
        // the "every day" switch on the application card is the same master switch
        if ($("setFullEnabled") && btn !== "storage") body.backupEnabled = $("setFullEnabled").checked;
        try {
            const r = await api("PUT", "/biobank/api/site-config", body);
            showRestart(r.canRestart);
            toast("Backup settings saved");
            await loadSettings();
        } catch (e) { fail(e); }
    }

    // ---------- run now
    async function runNow(scope, btn) {
        btn.classList.add("busy"); btn.disabled = true;
        const label = btn.innerHTML;
        btn.innerHTML = `<i class="bi bi-arrow-repeat"></i> Backing up…`;
        try {
            const r = await api("POST", `/biobank/api/backup/${scope}/run`);
            toast(`${NAMES[scope]}: backup done (${size(r.sizeBytes)}, ${r.durationSeconds}s)`);
            tab = scope; syncTabs();
            await load();
        } catch (e) {
            Swal.fire({ icon: "error", title: "Backup did not complete", text: e.message });
        } finally {
            btn.classList.remove("busy"); btn.disabled = false; btn.innerHTML = label;
        }
    }

    // ---------- upload
    function upload(scope, file, drop) {
        const ext = scope === "full" ? ".bak" : ".zip";
        if (!file.name.toLowerCase().endsWith(ext)) {
            Swal.fire({ icon: "warning", title: "Wrong file", text: scope === "full" ? "The whole-application backup is a .bak file." : "A Bio Bank backup is a BioBank_….zip file." });
            return;
        }
        const bar = drop.querySelector(".bk-progress"), fill = bar.querySelector("span");
        const label = drop.querySelector("b"), small = drop.querySelector("small");
        const old = [label.textContent, small.textContent];
        bar.hidden = false; fill.style.width = "0";
        label.textContent = `Uploading ${file.name}`;

        const fd = new FormData(); fd.append("file", file);
        const xhr = new XMLHttpRequest();
        xhr.open("POST", `/biobank/api/backup/${scope}/upload`);
        xhr.upload.onprogress = e => {
            if (!e.lengthComputable) return;
            const p = e.loaded / e.total;
            fill.style.width = (p * 100).toFixed(1) + "%";
            small.textContent = p < 1 ? `${size(e.loaded)} of ${size(e.total)}` : "Checking the file…";
        };
        xhr.onloadend = async () => {
            bar.hidden = true; [label.textContent, small.textContent] = old;
            let r = {}; try { r = JSON.parse(xhr.responseText || "{}"); } catch { }
            if (xhr.status !== 200) {
                Swal.fire({ icon: "error", title: "Upload failed", text: r.message || (xhr.status === 403 ? "Your account cannot upload backups." : `HTTP ${xhr.status}`) });
                return;
            }
            tab = scope; syncTabs();
            await load().catch(fail);
            const go = await Swal.fire({
                icon: "success", title: "Backup uploaded and checked",
                text: `${r.fileName} is now in the restore points. Restore it now?`,
                showCancelButton: true, confirmButtonText: "Review and restore", cancelButtonText: "Not now"
            });
            if (go.isConfirmed) openRestore(scope, r.fileName);
        };
        xhr.send(fd);
    }

    // ---------- restore dialog
    let pending = null;
    function openRestore(scope, name) {
        const f = data[scope].files.find(x => x.name === name);
        if (!f) return;
        pending = { scope, name };
        const d = utc(f.modifiedUtc);
        $("bkModalTitle").textContent = scope === "full" ? "Restore the whole application" : "Restore the Bio Bank data";
        $("bkModalFile").textContent = `${f.name} · ${when(d)} (${ago(d)})`;
        const s = f.summary || {};
        $("bkModalBody").innerHTML = scope === "full" ? `
            <div>Everything in the application goes back to how it was on <b>${esc(when(d))}</b>: stock, users and permissions, and all Bio Bank data.</div>
            <div class="bk-callout warn"><b>Anything entered after that time is replaced.</b> Everyone using the app is disconnected for a moment, and freezer data intake pauses until it finishes.</div>
            <div class="bk-callout safe"><i class="bi bi-shield-check"></i> A safety copy of the current data is taken first, so you can undo this from the restore points.</div>` : `
            <div>The Bio Bank goes back to <b>${esc(when(d))}</b>:</div>
            <ul><li><b>${s.freezers ?? 0}</b> freezers, <b>${(s.samples ?? 0).toLocaleString()}</b> samples, <b>${(s.readings ?? 0).toLocaleString()}</b> readings, <b>${s.attachments ?? 0}</b> attachment files</li>
                <li>Alarm limits, contacts and gateway links as they were then</li></ul>
            <div class="bk-callout warn"><b>Bio Bank changes made after that time are replaced.</b> Stock, users and the rest of the application are not touched.</div>
            <div class="bk-callout safe"><i class="bi bi-shield-check"></i> A safety copy of the current Bio Bank data is taken first, so you can undo this.</div>`;
        $("bkSteps").hidden = true; $("bkModalResult").hidden = true; $("bkConfirm").hidden = false;
        $("bkSteps").querySelectorAll("li").forEach(li => li.className = "");
        $("bkConfirmInput").value = ""; $("bkGo").disabled = true; $("bkGo").hidden = false;
        $("bkCancel").textContent = "Cancel"; $("bkCancel").disabled = false;
        $("bkModal").hidden = false;
        setTimeout(() => $("bkConfirmInput").focus(), 50);
    }

    function closeRestore() { if (!$("bkCancel").disabled) { $("bkModal").hidden = true; pending = null; } }

    async function doRestore() {
        if (!pending) return;
        const { scope, name } = pending;
        $("bkConfirm").hidden = true; $("bkGo").hidden = true;
        $("bkCancel").disabled = true; $("bkCancel").textContent = "Please wait…";
        const steps = [...$("bkSteps").querySelectorAll("li")];
        $("bkSteps").hidden = false;
        steps[0].className = "active";

        const result = $("bkModalResult");
        try {
            const r = await api("POST", `/biobank/api/backup/${scope}/restore`, { file: name, confirm: "RESTORE" });
            for (const li of steps) { li.className = "done"; await new Promise(res => setTimeout(res, 140)); }
            const d = r.details || {}, x = d.details || {};
            result.className = "bk-modal-result ok"; result.hidden = false;
            result.innerHTML = `<b><i class="bi bi-check-circle-fill"></i> Restored in ${d.seconds ?? "?"} s.</b><br>` +
                `Safety copy of the data before: <code>${esc(d.safetyCopy || "—")}</code>` +
                (x.migrationsApplied?.length ? `<br>The backup was from an older version; ${x.migrationsApplied.length} update(s) applied.` : "") +
                (x.warning ? `<br><span style="color:#8a5300">${esc(x.warning)}</span>` : "") +
                (x.restartRecommended ? `<br>Restart the app so every screen starts from the restored data.` : "");
            if (x.restartRecommended) showRestart(data.canRestart);
        } catch (e) {
            const at = steps.findIndex(li => li.className === "active");
            if (at >= 0) steps[at].className = "failed";
            result.className = "bk-modal-result fail"; result.hidden = false;
            result.innerHTML = `<b><i class="bi bi-x-octagon-fill"></i> Restore did not complete.</b><br>${esc(e.message)}`;
        } finally {
            $("bkCancel").disabled = false; $("bkCancel").textContent = "Close";
            load().catch(() => { });
        }
    }

    // ---------- wiring
    function syncTabs() {
        document.querySelectorAll(".bk-seg button").forEach(b => b.classList.toggle("active", b.dataset.tab === tab));
        if (data) renderList();
    }

    Object.entries(SCOPES).forEach(([scope, id]) => {
        const root = $(id);
        root.querySelector("[data-run]")?.addEventListener("click", e => runNow(scope, e.currentTarget));
        const drop = root.querySelector("[data-drop]");
        if (drop) {
            const input = drop.querySelector("[data-file]");
            input.addEventListener("change", () => { const f = input.files[0]; input.value = ""; if (f) upload(scope, f, drop); });
            ["dragenter", "dragover"].forEach(ev => drop.addEventListener(ev, e => { e.preventDefault(); drop.classList.add("over"); }));
            ["dragleave", "drop"].forEach(ev => drop.addEventListener(ev, e => { e.preventDefault(); drop.classList.remove("over"); }));
            drop.addEventListener("drop", e => { const f = e.dataTransfer.files[0]; if (f) upload(scope, f, drop); });
        }
        root.querySelectorAll(".bk-auto input").forEach(inp => inp.addEventListener("change", () => saveSettings("auto")));
    });

    document.querySelectorAll(".bk-seg button").forEach(b => b.addEventListener("click", () => { tab = b.dataset.tab; syncTabs(); }));
    $("bkList").addEventListener("click", e => { const b = e.target.closest("[data-restore]"); if (b) openRestore(tab, b.dataset.restore); });
    $("setSave")?.addEventListener("click", () => saveSettings("storage"));
    $("bkConfirmInput").addEventListener("input", e => { $("bkGo").disabled = e.target.value.trim().toUpperCase() !== "RESTORE"; });
    $("bkConfirmInput").addEventListener("keydown", e => { if (e.key === "Enter" && !$("bkGo").disabled) doRestore(); });
    $("bkGo").addEventListener("click", doRestore);
    $("bkCancel").addEventListener("click", closeRestore);
    $("bkModal").addEventListener("click", e => { if (e.target.id === "bkModal") closeRestore(); });
    document.addEventListener("keydown", e => { if (e.key === "Escape" && !$("bkModal").hidden) closeRestore(); });
    wireRestart();

    if (window.BBLive) BBLive.on("maintenance", m => { renderMaint(m); load().catch(() => { }); });

    load().catch(fail);
    loadSettings();
    setInterval(() => { if ($("bkModal").hidden) load().catch(() => { }); }, 60000);
})();
