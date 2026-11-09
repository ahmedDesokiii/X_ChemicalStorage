// Shared helpers for the Settings pages (system + alarms).
window.SiteSettings = (() => {
    const $ = id => document.getElementById(id);

    const esc = v => String(v ?? "").replace(/[&<>"']/g, c =>
        ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

    async function api(method, url, body) {
        const res = await fetch(url, {
            method, cache: "no-store",
            headers: body === undefined ? {} : { "Content-Type": "application/json" },
            body: body === undefined ? undefined : JSON.stringify(body)
        });
        let data = null;
        try { data = await res.json(); } catch { /* empty body */ }
        if (!res.ok) {
            const msg = data?.message || data?.title ||
                (res.status === 403 ? "Your account doesn't have permission for this." : `Request failed (HTTP ${res.status}).`);
            throw new Error(msg);
        }
        return data;
    }

    const toast = text => (window.Swal
        ? Swal.fire({ toast: true, position: "bottom-start", icon: "success", title: text, showConfirmButton: false, timer: 2500 })
        : console.log(text));

    const fail = e => (window.Swal
        ? Swal.fire({ icon: "error", title: "Not saved", text: e?.message || String(e) })
        : alert(e?.message || e));

    function showRestart(canRestart) {
        const b = $("ssRestartBanner"); if (!b) return;
        b.hidden = false;
        $("ssRestartText").textContent = canRestart
            ? "Settings saved. Restart the app to apply them (takes about 20 seconds)."
            : "Settings saved. Running from Visual Studio: stop it and start it again (Shift+F5, then F5) to apply them.";
        $("ssRestartBtn").hidden = !canRestart;
    }

    function wireRestart() {
        const btn = $("ssRestartBtn"); if (!btn) return;
        btn.addEventListener("click", async () => {
            btn.disabled = true;
            try {
                await api("POST", "/biobank/api/site-config/restart");
                $("ssRestartText").textContent = "Restarting… this page reloads when the app is back.";
                // wait for the app to go down and come back
                await new Promise(r => setTimeout(r, 6000));
                for (let i = 0; i < 40; i++) {
                    try { const r = await fetch("/healthz", { cache: "no-store" }); if (r.ok) { location.reload(); return; } } catch { }
                    await new Promise(r => setTimeout(r, 2000));
                }
                $("ssRestartText").textContent = "The app didn't come back. Check the XChemicalStorage service on this PC (services.msc).";
            } catch (e) { fail(e); btn.disabled = false; }
        });
    }

    return { $, esc, api, toast, fail, showRestart, wireRestart };
})();
