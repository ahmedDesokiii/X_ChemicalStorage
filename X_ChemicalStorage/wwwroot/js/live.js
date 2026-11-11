// ===== Live updates (SignalR) =====
// One connection per page to /hubs/live. Pages subscribe with BBLive.on("freezersChanged", fn).
// The hub only says "something changed"; pages re-read their own API, so polling stays as the safety net.
(function () {
    "use strict";
    const handlers = {};
    const chips = () => document.querySelectorAll("[data-live-chip]");
    let state = "connecting";

    function setState(s) {
        state = s;
        const label = { live: "Live", connecting: "Connecting…", reconnecting: "Reconnecting…", offline: "Auto-refresh" }[s] || s;
        const title = {
            live: "Connected: changes appear the moment the server receives them.",
            connecting: "Connecting to live updates…",
            reconnecting: "Live connection lost, reconnecting. The page keeps refreshing on its own meanwhile.",
            offline: "Live updates unavailable. The page refreshes on a timer instead."
        }[s] || "";
        chips().forEach(el => {
            el.className = el.className.replace(/\blive-(live|connecting|reconnecting|offline)\b/g, "").trim() + " live-" + s;
            el.title = title;
            const t = el.querySelector("[data-live-text]");
            if (t) t.textContent = label;
        });
        emit("state", s);
    }

    function emit(evt, payload) {
        (handlers[evt] || []).forEach(fn => { try { fn(payload); } catch (e) { console.error("[live]", evt, e); } });
    }

    window.BBLive = {
        on(evt, fn) { (handlers[evt] = handlers[evt] || []).push(fn); return this; },
        get state() { return state; },
        get connected() { return state === "live"; }
    };

    if (!window.signalR) { setState("offline"); return; }

    const conn = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/live")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

    ["freezersChanged", "stationChanged", "maintenance"].forEach(evt => conn.on(evt, p => emit(evt, p)));
    conn.onreconnecting(() => setState("reconnecting"));
    conn.onreconnected(() => { setState("live"); emit("resync"); });   // we may have missed pushes while away
    conn.onclose(() => { setState("offline"); setTimeout(start, 15000); });

    async function start() {
        if (conn.state !== signalR.HubConnectionState.Disconnected) return;
        try {
            setState("connecting");
            await conn.start();
            setState("live");
        } catch {
            setState("offline");
            setTimeout(start, 15000);
        }
    }

    const go = () => { setState(state); start(); };
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", go); else go();
})();
