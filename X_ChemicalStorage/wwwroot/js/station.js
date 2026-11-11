// ===== Local monitoring station =====
// Polls /biobank/api/iot/station and sounds an alarm on THIS machine while any alarm is unacknowledged.
// Everything is served from the local PC, so the alarm keeps working when the internet is down.
(function () {
    const API = "/biobank/api/iot/station";
    const REFRESH_MS = 10000;
    const SILENCE_MS = 10 * 60 * 1000;
    const $ = (id) => document.getElementById(id);
    const can = (k) => !window.can || window.can(k);

    let data = null;
    let armed = false;
    let silencedUntil = 0;
    let knownAlarmIds = new Set();
    let firstLoad = true;

    try { silencedUntil = parseInt(localStorage.getItem("st.silencedUntil") || "0", 10) || 0; } catch { }

    // ───────────── helpers ─────────────
    const esc = (v) => String(v ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
    const temp = (v) => v == null ? "--" : Number(v).toFixed(1);
    // The server stores UTC; values read from SQL come back without "Z", so mark them as UTC before parsing.
    function utc(iso) {
        if (!iso) return null;
        return new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(iso) ? iso : iso + "Z");
    }
    const when = (iso) => iso ? utc(iso).toLocaleString() : "–";
    function ago(iso) {
        if (!iso) return "never";
        const s = Math.max(0, (Date.now() - utc(iso).getTime()) / 1000);
        if (s < 60) return `${Math.round(s)} s ago`;
        if (s < 3600) return `${Math.round(s / 60)} min ago`;
        if (s < 86400) return `${Math.round(s / 3600)} h ago`;
        return utc(iso).toLocaleString();
    }
    function setTile(id, cls, value, meta) {
        const el = $(id);
        el.className = "st-tile " + cls;
        el.querySelector(".st-tile-value").textContent = value;
        el.querySelector(".st-tile-meta").textContent = meta || "";
    }

    // ───────────── local siren (Web Audio, no files, no internet) ─────────────
    let ctx = null, sirenTimer = null;
    function beep(freq, dur) {
        if (!ctx) return;
        const o = ctx.createOscillator(), g = ctx.createGain();
        o.type = "square";
        o.frequency.value = freq;
        g.gain.setValueAtTime(0.0001, ctx.currentTime);
        g.gain.exponentialRampToValueAtTime(0.25, ctx.currentTime + 0.02);
        g.gain.exponentialRampToValueAtTime(0.0001, ctx.currentTime + dur);
        o.connect(g).connect(ctx.destination);
        o.start();
        o.stop(ctx.currentTime + dur + 0.02);
    }
    function sirenOn() {
        if (!armed || sirenTimer) return;
        const cycle = () => { beep(880, 0.35); setTimeout(() => beep(660, 0.35), 420); };
        cycle();
        sirenTimer = setInterval(cycle, 1300);
    }
    function sirenOff() {
        if (sirenTimer) { clearInterval(sirenTimer); sirenTimer = null; }
    }

    function updateArmButton() {
        const b = $("stArm");
        b.classList.toggle("armed", armed);
        b.querySelector("i").className = armed ? "bi bi-bell-fill" : "bi bi-bell-slash-fill";
        b.querySelector("span").textContent = armed ? "Local alarm armed" : "Arm local alarm sound";
    }

    $("stArm").addEventListener("click", async () => {
        try {
            ctx = ctx || new (window.AudioContext || window.webkitAudioContext)();
            await ctx.resume();
            armed = true;
            beep(1046, 0.15);
        } catch { armed = false; }
        if ("Notification" in window && Notification.permission === "default") {
            try { await Notification.requestPermission(); } catch { }
        }
        updateArmButton();
        render();
    });

    $("stSilence").addEventListener("click", () => {
        silencedUntil = Date.now() + SILENCE_MS;
        try { localStorage.setItem("st.silencedUntil", String(silencedUntil)); } catch { }
        render();
    });

    $("stFullscreen").addEventListener("click", () => {
        const el = $("stationApp");
        if (document.fullscreenElement) document.exitFullscreen();
        else el.requestFullscreen?.();
    });

    // ───────────── rendering ─────────────
    function renderHealth(d) {
        const overall = $("stOverall");
        overall.className = "st-overall " + d.status;
        overall.querySelector("span:last-child").textContent =
            d.status === "ok" ? "All systems normal" : d.status === "degraded" ? "Attention needed" : "Monitoring problem";

        const st = d.station;
        $("stStationInfo").innerHTML =
            `<i class="bi bi-pc-display"></i> ${esc(st.machineName)}` +
            (st.addresses?.length ? ` · LAN ${st.addresses.map(esc).join(", ")}` : "") +
            ` · up ${Math.round(st.uptimeMinutes)} min`;

        const b = d.broker;
        if (!b.enabled) setTile("stBroker", "warn", "Disabled", "Mqtt:Enabled = false");
        else setTile("stBroker", b.connected ? "ok" : "bad", b.connected ? "Connected" : "Disconnected",
            `${b.host}:${b.port} · last message ${ago(b.lastMessageAt)}`);

        const net = d.internet;
        if (net.online === true) setTile("stInternet", "ok", "Online", "Telegram / SMS / e-mail active");
        else if (net.online === false) setTile("stInternet", "warn", "Offline", "Remote alerts queued · local alarm active");
        else setTile("stInternet", "", "Checking…", "");

        const w = d.workers;
        const engineOk = !d.problems.some(p => /watchdog|Notification worker/i.test(p));
        setTile("stEngine", engineOk ? "ok" : "bad", engineOk ? "Running" : "Stopped",
            `watchdog ${ago(w.watchdogLastRunAt)} · ${w.pendingNotifications} pending message(s)`);

        const c = d.counts;
        const bad = (c.highTemp || 0) + (c.lowTemp || 0) + (c.abnormal || 0);
        setTile("stFleet", bad ? "bad" : c.offline ? "warn" : "ok", `${c.all - c.offline}/${c.all} online`,
            `${bad} in alarm · ${c.offline} offline · ${d.unlinkedFreezers.length} not linked`);

        $("stNotes").innerHTML =
            d.problems.map(p => `<li class="bad"><i class="bi bi-x-octagon-fill"></i>${esc(p)}</li>`).join("") +
            d.warnings.map(p => `<li class="warn"><i class="bi bi-exclamation-triangle-fill"></i>${esc(p)}</li>`).join("");
    }

    function renderGateways(d) {
        $("stGwCount").textContent = d.gateways.length;
        if (!d.gateways.length) {
            $("stGateways").innerHTML = `<div class="st-gw"><div class="st-empty">No freezer is linked to a gateway yet. Link collectors from the panel on the right, or from the freezer page in Bio Bank.</div></div>`;
            return;
        }
        $("stGateways").innerHTML = d.gateways.map(g => `
            <div class="st-gw">
                <div class="st-gw-head">
                    <div class="st-gw-icon"><i class="bi bi-router-fill"></i></div>
                    <div>
                        <div class="st-gw-name">${esc(g.gatewaySerial)}</div>
                        <div class="st-gw-meta">${g.onlineCount}/${g.freezerCount} freezers reporting · last data ${ago(g.lastSeenAt)}</div>
                    </div>
                    <span class="st-pill ${g.status}">${g.status}</span>
                </div>
                <div class="st-fz-grid">
                    ${g.freezers.map(f => `
                        <div class="st-fz ${esc(f.state)} ${f.unacknowledgedAlarms ? "has-alarm" : ""}" data-fz="${esc(f.freezerId)}" title="${esc(f.activeAlarmTypes.join(", "))}">
                            ${f.unacknowledgedAlarms ? `<span class="st-fz-badge">${f.unacknowledgedAlarms}</span>` : ""}
                            <div class="st-fz-id">${esc(f.freezerId)}</div>
                            <div class="st-fz-type">${esc(f.deviceName || f.type)}</div>
                            <div class="st-fz-temp">${temp(f.temperature)}<small> °C</small></div>
                            <div class="st-fz-limits">limits ${temp(f.lowLimit)} … ${temp(f.highLimit)} °C</div>
                            <div class="st-fz-state ${esc(f.state)}">${esc(f.state)}${f.activeAlarmTypes.length ? " · " + esc(f.activeAlarmTypes.join(", ")) : ""}</div>
                            <div class="st-fz-age">${ago(f.lastReadingAt)}</div>
                            <div class="st-fz-code">${esc(f.collectorCode)}</div>
                        </div>`).join("")}
                </div>
            </div>`).join("");

        // Glow the tiles whose reading changed since the last render
        const now = {};
        d.gateways.forEach(g => g.freezers.forEach(f => { now[f.freezerId] = `${f.temperature}|${f.state}|${f.lastReadingAt}`; }));
        if (prevTiles) {
            Object.keys(now).forEach(id => {
                if (prevTiles[id] !== undefined && prevTiles[id] !== now[id]) {
                    const el = document.querySelector(`.st-fz[data-fz="${CSS.escape(id)}"] .st-fz-temp`);
                    if (el) { el.classList.remove("live-flash"); void el.offsetWidth; el.classList.add("live-flash"); }
                }
            });
        }
        prevTiles = now;
    }
    let prevTiles = null;

    function renderAlarms(d) {
        const open = d.alarms;
        $("stAlarmCount").textContent = open.length || "";
        if (!open.length) { $("stAlarmList").innerHTML = `<div class="st-empty"><i class="bi bi-check-circle"></i> No open alarms</div>`; return; }
        const canAck = can("IoT.AcknowledgeAlarms");
        $("stAlarmList").innerHTML = open.map(a => {
            const cls = a.isCleared ? "cleared" : a.acknowledgedAt ? "acked" : "";
            const status = a.isCleared ? "cleared, waiting for acknowledge" : a.acknowledgedAt ? `acknowledged by ${esc(a.acknowledgedBy || "?")}` : "NOT acknowledged";
            return `
            <div class="st-alarm ${cls}">
                <div class="st-alarm-head"><span>${esc(a.freezerId)} · ${esc(a.alarmType)}</span><span>L${a.level}</span></div>
                <div class="st-alarm-msg">${esc(a.message)}</div>
                <div class="st-alarm-meta">
                    <span>${when(a.triggeredAt)} · ${status}</span>
                    ${!a.acknowledgedAt && canAck ? `<button type="button" class="st-btn st-btn-primary st-btn-sm" data-ack="${a.id}">Acknowledge</button>` : ""}
                </div>
            </div>`;
        }).join("");
    }

    function renderUnlinked(d) {
        const canLink = can("IoT.ManageDevices");
        const cols = d.unlinkedCollectors.map(c => `
            <div class="st-unlinked">
                <div><code>${esc(c.collectorCode)}</code><small>heard ${ago(c.lastSeenAt)} · ${c.messages} msg${c.temperature != null ? ` · ${temp(c.temperature)} °C` : ""}</small></div>
                ${canLink ? `<button type="button" class="st-btn st-btn-primary st-btn-sm" data-link-collector="${esc(c.collectorCode)}">Link</button>` : ""}
            </div>`).join("");
        const fzs = d.unlinkedFreezers.map(f => `
            <div class="st-unlinked">
                <div><i class="bi bi-snow2"></i> <b>${esc(f.freezerId)}</b><small>${esc(f.type)} · no IoT device</small></div>
                ${canLink ? `<button type="button" class="st-btn st-btn-outline st-btn-sm" data-link-freezer="${esc(f.freezerId)}">Link</button>` : ""}
            </div>`).join("");
        $("stUnlinked").innerHTML = (cols + fzs) || `<div class="st-empty"><i class="bi bi-check-circle"></i> Every freezer is linked</div>`;
    }

    function renderAlarmState(d) {
        const pending = d.alarms.filter(a => !a.acknowledgedAt);
        const active = pending.filter(a => !a.isCleared);
        const banner = $("stAlarmBanner");
        const silenced = Date.now() < silencedUntil;

        if (pending.length) {
            banner.hidden = false;
            banner.classList.toggle("silenced", silenced);
            $("stAlarmTitle").textContent = `${pending.length} unacknowledged alarm${pending.length > 1 ? "s" : ""}` + (silenced ? " (sound silenced)" : "");
            $("stAlarmSub").textContent = pending.slice(0, 3).map(a => `${a.freezerId}: ${a.alarmType}`).join(" · ") + (pending.length > 3 ? " …" : "");
            document.title = `🔴 (${pending.length}) Monitoring Station`;
            if (!silenced && active.length) sirenOn(); else sirenOff();
        } else {
            banner.hidden = true;
            document.title = "Monitoring Station";
            sirenOff();
        }

        // Desktop notification for alarms we did not see before
        const ids = new Set(d.alarms.map(a => a.id));
        if (!firstLoad && "Notification" in window && Notification.permission === "granted") {
            d.alarms.filter(a => !knownAlarmIds.has(a.id) && !a.acknowledgedAt).forEach(a => {
                try { new Notification(`Freezer ${a.freezerId}: ${a.alarmType}`, { body: a.message, tag: "alarm-" + a.id, requireInteraction: true }); } catch { }
            });
        }
        knownAlarmIds = ids;
        firstLoad = false;
    }

    function render() {
        if (!data) return;
        renderHealth(data);
        renderGateways(data);
        renderAlarms(data);
        renderUnlinked(data);
        renderAlarmState(data);
        $("stUpdated").textContent = new Date().toLocaleTimeString();
        $("stServer").textContent = utc(data.serverTimeUtc).toLocaleTimeString();
    }

    let lastLoad = 0, loadQueued = null;
    async function load() {
        lastLoad = Date.now();
        try {
            const res = await fetch(API, { cache: "no-store" });
            if (!res.ok) throw new Error(`HTTP ${res.status}`);
            data = await res.json();
            render();
        } catch (e) {
            // The app itself is unreachable → this IS an emergency for a monitoring screen
            $("stOverall").className = "st-overall down";
            $("stOverall").querySelector("span:last-child").textContent = "Station unreachable";
            $("stNotes").innerHTML = `<li class="bad"><i class="bi bi-x-octagon-fill"></i>Cannot reach the monitoring service (${esc(e.message)}). Alarms are NOT being evaluated.</li>`;
            if (Date.now() >= silencedUntil) sirenOn();
        }
    }

    // ───────────── actions ─────────────
    document.addEventListener("click", async (e) => {
        const ack = e.target.closest("[data-ack]");
        if (ack) {
            const { value: note, isConfirmed } = await Swal.fire({
                title: "Acknowledge alarm", input: "text", inputPlaceholder: "Action taken (optional)",
                showCancelButton: true, confirmButtonText: "Acknowledge"
            });
            if (!isConfirmed) return;
            ack.disabled = true;
            const res = await fetch(`/biobank/api/alarms/${ack.dataset.ack}/acknowledge`, {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ note: note || null })
            });
            if (!res.ok) Swal.fire({ icon: "error", title: "Could not acknowledge", text: `HTTP ${res.status}` });
            await load();
            return;
        }

        const lc = e.target.closest("[data-link-collector]");
        const lf = e.target.closest("[data-link-freezer]");
        if (lc || lf) openLink(lc?.dataset.linkCollector || "", lf?.dataset.linkFreezer || "");
    });

    let linkModal = null;
    function openLink(collector, freezerId) {
        const d = data || { unlinkedFreezers: [], gateways: [], unlinkedCollectors: [] };
        const linked = d.gateways.flatMap(g => g.freezers.map(f => f.freezerId));
        const options = [
            ...d.unlinkedFreezers.map(f => `<option value="${esc(f.freezerId)}">${esc(f.freezerId)} (not linked)</option>`),
            ...linked.map(id => `<option value="${esc(id)}">${esc(id)} (replace current device)</option>`)
        ];
        $("stLinkFreezer").innerHTML = `<option value="">Choose freezer…</option>` + options.join("");
        $("stLinkFreezer").value = freezerId;
        $("stLinkCollector").value = collector;
        $("stLinkCollectors").innerHTML = d.unlinkedCollectors.map(c => `<option value="${esc(c.collectorCode)}">`).join("");
        $("stLinkGateways").innerHTML = d.gateways.map(g => `<option value="${esc(g.gatewaySerial)}">`).join("");
        $("stLinkGateway").value = d.gateways.length === 1 ? d.gateways[0].gatewaySerial : "";
        $("stLinkName").value = "";
        $("stLinkError").hidden = true;
        linkModal = linkModal || new bootstrap.Modal($("stLinkModal"));
        linkModal.show();
    }

    $("stLinkForm").addEventListener("submit", async (e) => {
        e.preventDefault();
        const payload = {
            freezerId: $("stLinkFreezer").value,
            collectorCode: $("stLinkCollector").value.trim(),
            gatewaySerial: $("stLinkGateway").value.trim(),
            deviceName: $("stLinkName").value.trim() || null
        };
        const btn = $("stLinkSave");
        btn.disabled = true;
        try {
            const res = await fetch("/biobank/api/iot/link", {
                method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(payload)
            });
            if (!res.ok) {
                const err = await res.json().catch(() => ({}));
                throw new Error(err.message || (err.errors ? Object.values(err.errors).flat().join(" ") : `HTTP ${res.status}`));
            }
            linkModal.hide();
            await load();
        } catch (err) {
            $("stLinkError").textContent = err.message;
            $("stLinkError").hidden = false;
        } finally {
            btn.disabled = false;
        }
    });

    updateArmButton();
    load();
    // Live: the server pushes "changed" the moment a reading / alarm lands; polling is only the safety net.
    function loadSoon() {
        if (loadQueued) return;
        loadQueued = setTimeout(() => { loadQueued = null; load(); }, 250);
    }
    if (window.BBLive) {
        BBLive.on("freezersChanged", loadSoon).on("stationChanged", loadSoon).on("resync", loadSoon)
              .on("maintenance", loadSoon);
    }
    setInterval(() => {
        if (window.BBLive && BBLive.connected && Date.now() - lastLoad < 30000) return;
        load();
    }, REFRESH_MS);
    setInterval(() => { if (data) renderGateways(data); }, 15000);   // keeps "x min ago" honest between pushes
    setInterval(() => { if (data) renderAlarmState(data); }, 15000);   // ends a silence period on time
})();
