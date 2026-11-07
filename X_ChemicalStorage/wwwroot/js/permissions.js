// ===== Permission matrix behaviour (role checkboxes + user grant/deny overrides) =====
(function () {
    const form = document.getElementById('permForm');
    if (!form) return;

    const summary = document.getElementById('permSummary');
    const countEl = document.getElementById('permCount');
    const barEl = document.getElementById('permBar');
    const search = document.getElementById('permSearch');
    const readOnly = form.dataset.readonly === 'true';
    const isUserMode = form.dataset.mode === 'user';
    let dirty = false;

    const rows = () => Array.from(form.querySelectorAll('.perm-row'));

    // Is the permission in this row effective (after role + override)?
    function effective(row) {
        if (!isUserMode) return row.querySelector('input[type=checkbox]').checked;
        const state = row.querySelector('input[type=radio]:checked')?.value || 'inherit';
        if (state === 'grant') return true;
        if (state === 'deny') return false;
        return row.dataset.fromRole === 'true';
    }

    function refresh() {
        const all = rows();
        let granted = 0;
        all.forEach(r => {
            const on = effective(r);
            if (on) granted++;
            const icon = r.querySelector('.perm-effective i');
            if (icon) icon.className = on ? 'bi bi-check-circle-fill' : 'bi bi-x-circle-fill';
        });
        countEl.textContent = `${granted} / ${all.length}`;
        barEl.style.width = (all.length ? Math.round(granted * 100 / all.length) : 0) + '%';

        form.querySelectorAll('.perm-col').forEach(col => {
            const items = Array.from(col.querySelectorAll('.perm-row'));
            const on = items.filter(effective).length;
            col.querySelector('.mod-count').textContent = `${on}/${items.length}`;
            const toggle = col.querySelector('.mod-toggle');
            if (toggle) {
                toggle.checked = on === items.length;
                toggle.indeterminate = on > 0 && on < items.length;
            }
        });
    }

    function markDirty() {
        dirty = true;
        summary?.classList.add('is-dirty');
        refresh();
    }

    if (!readOnly) {
        form.addEventListener('change', e => {
            if (e.target.classList.contains('mod-toggle')) {
                const col = e.target.closest('.perm-col');
                col.querySelectorAll('.perm-row').forEach(r => setRow(r, e.target.checked));
            }
            markDirty();
        });

        // Role mode: on/off. User mode: "all on" = grant, "all off" = back to inherit.
        function setRow(row, on) {
            if (row.classList.contains('is-hidden')) return;
            if (!isUserMode) {
                row.querySelector('input[type=checkbox]').checked = on;
            } else {
                const v = on ? 'grant' : 'inherit';
                const radio = row.querySelector(`input[type=radio][value=${v}]`);
                if (radio) radio.checked = true;
            }
        }

        document.getElementById('permAll')?.addEventListener('click', () => { rows().forEach(r => setRow(r, true)); markDirty(); });
        document.getElementById('permNone')?.addEventListener('click', () => { rows().forEach(r => setRow(r, false)); markDirty(); });

        form.addEventListener('submit', () => { dirty = false; });
        window.addEventListener('beforeunload', e => { if (dirty) { e.preventDefault(); e.returnValue = ''; } });
    }

    // Search by module name, permission name (Arabic/English) or key
    search?.addEventListener('input', () => {
        const q = search.value.trim().toLowerCase();
        form.querySelectorAll('.perm-col').forEach(col => {
            const moduleHit = !q || (col.dataset.search || '').includes(q);
            let any = false;
            col.querySelectorAll('.perm-row').forEach(r => {
                const hit = moduleHit || (r.dataset.search || '').includes(q);
                r.classList.toggle('is-hidden', !hit);
                any = any || hit;
            });
            col.classList.toggle('is-hidden', !any);
        });
    });

    refresh();
})();
