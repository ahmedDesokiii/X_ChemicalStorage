// ═══════════════════════════════════════════════════════════════
//  Rooms — storage map interactions
// ═══════════════════════════════════════════════════════════════
window.RM = (() => {
    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => [...r.querySelectorAll(s)];
    const KEY = 'rm-open-units';
    const modal = id => bootstrap.Modal.getOrCreateInstance(document.getElementById(id));

    // ───────── open/closed state survives the post-redirect ─────────
    const load = () => { try { return new Set(JSON.parse(sessionStorage.getItem(KEY) || '[]')); } catch { return new Set(); } };
    const save = set => { try { sessionStorage.setItem(KEY, JSON.stringify([...set])); } catch { } };
    const uid = unit => unit.querySelector('[data-id]')?.dataset.id;

    function setOpen(unit, open) {
        unit.classList.toggle('open', open);
        const s = load(); open ? s.add(uid(unit)) : s.delete(uid(unit)); save(s);
    }
    function toggle(bar) { const u = bar.closest('.rm-unit'); setOpen(u, !u.classList.contains('open')); }
    function expandAll(open) { $$('.rm-unit').forEach(u => setOpen(u, open)); }
    function expandRoom(btn) { $$('.rm-unit', btn.closest('.rm-card')).forEach(u => setOpen(u, true)); }

    function inline(id) {
        const f = document.getElementById(id);
        if (!f) return;
        const unit = f.closest('.rm-unit');
        if (unit && !unit.classList.contains('open')) setOpen(unit, true);
        f.classList.toggle('show');
        if (f.classList.contains('show')) setTimeout(() => $('input:not([type=hidden])', f)?.select(), 50);
    }

    // ───────── edit modals ─────────
    function editRoom(id, btn) {
        $('#roomId').value = id;
        $('#roomNameInput').value = btn.dataset.name;
        $('#roomModalTitle').textContent = 'Rename room';
        modal('addRoomModal').show();
    }
    function editUnit(btn) {
        $('#editUnitId').value = btn.dataset.id;
        $('#editUnitName').value = btn.dataset.name;
        $('#editUnitType').value = btn.dataset.type;
        $('#editUnitHaz').checked = btn.dataset.haz === 'true';
        (btn.dataset.zone === 'TemporaryHolding' ? $('#ezHold') : $('#ezInv')).checked = true;
        modal('editUnitModal').show();
    }
    function editShelf(btn) {
        $('#editShelfId').value = btn.dataset.id;
        $('#editShelfName').value = btn.dataset.name;
        modal('editShelfModal').show();
    }

    // ───────── box inspector ─────────
    let current = null;
    async function inspect(tile) {
        current = tile.dataset;
        $('#biTitle').textContent = current.name;
        $('#biPath').innerHTML = `<i class="bi bi-geo-alt"></i> ${current.path}`;
        $('#biDeleteId').value = current.id;
        const grid = $('#biGrid'), list = $('#biList');
        grid.innerHTML = '<div class="p-4"><span class="spinner-border text-secondary"></span></div>';
        grid.style.gridTemplateColumns = '';
        list.innerHTML = '';
        ['#biSlots', '#biLots', '#biSamples', '#biFree'].forEach(s => $(s).textContent = '–');
        modal('boxInspector').show();

        const d = await (await fetch(`/Rooms/GetBoxGrid?boxId=${current.id}`)).json();
        if (!d) return;
        const occ = {};
        d.occupied.forEach(o => occ[`${o.rowIndex}-${o.columnIndex}`] = o);
        const lots = d.occupied.filter(o => o.kind !== 'sample'), samples = d.occupied.filter(o => o.kind === 'sample');

        grid.innerHTML = '';
        grid.style.gridTemplateColumns = `repeat(${d.columnsCount}, minmax(24px, 1fr))`;
        for (let r = 1; r <= d.rowsCount; r++) for (let c = 1; c <= d.columnsCount; c++) {
            const o = occ[`${r}-${c}`];
            const el = document.createElement(o ? 'a' : 'span');
            el.style.animationDelay = ((r + c) * 14) + 'ms';
            if (o) {
                el.className = 'iv-cell ' + (o.kind === 'sample' ? 'sample' : 'lot');
                el.href = o.kind === 'sample' ? `/TempSamples/Details/${o.id}` : `/Lots/SelectedLot/${o.id}`;
                el.title = `${o.kind === 'sample' ? 'Sample' : 'Lot'} ${o.lotNumber} · R${r}C${c}`;
            } else {
                el.className = 'iv-cell free';
                el.style.cursor = 'default';
                el.title = `R${r} C${c} · free`;
            }
            grid.appendChild(el);
        }

        count('#biSlots', d.totalSlots); count('#biLots', lots.length); count('#biSamples', samples.length); count('#biFree', d.availableSlots);
        list.innerHTML = d.occupied
            .sort((a, b) => a.rowIndex - b.rowIndex || a.columnIndex - b.columnIndex)
            .map(o => `<a href="${o.kind === 'sample' ? '/TempSamples/Details/' : '/Lots/SelectedLot/'}${o.id}">
                         <span class="k ${o.kind === 'sample' ? 'sample' : 'lot'}"></span>
                         <b style="font-family:ui-monospace,Consolas,monospace">${o.lotNumber}</b>
                         <span class="iv-sub ms-auto">R${o.rowIndex} · C${o.columnIndex}</span>
                         <i class="bi bi-arrow-right-short"></i></a>`).join('')
            || '<div class="iv-sub text-center py-2">This box is empty.</div>';
    }
    function count(sel, end) { const el = $(sel); el.dataset.count = end; window.IV?.countUp(el.parentElement); }

    function editBoxFromInspector() {
        if (!current) return;
        $('#editBoxId').value = current.id;
        $('#editBoxName').value = current.name;
        $('#editBoxRows').value = current.rows;
        $('#editBoxCols').value = current.cols;
        modal('boxInspector').hide();
        setTimeout(() => modal('editBoxModal').show(), 250);
    }

    // ───────── search ─────────
    function search() {
        const input = $('#rmSearch'); if (!input) return;
        document.addEventListener('keydown', e => {
            if (e.key === '/' && document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'TEXTAREA') { e.preventDefault(); input.focus(); }
            if (e.key === 'Escape' && document.activeElement === input) { input.value = ''; input.dispatchEvent(new Event('input')); input.blur(); }
        });
        let t;
        input.addEventListener('input', () => {
            clearTimeout(t);
            t = setTimeout(() => {
                const q = input.value.trim().toLowerCase();
                $$('.rm-card').forEach(card => {
                    const roomHit = (card.dataset.room || '').toLowerCase().includes(q);
                    let unitHit = false;
                    $$('.rm-unit', card).forEach(u => {
                        const hit = !!q && (u.dataset.search || '').toLowerCase().includes(q);
                        u.style.outline = hit ? '2px solid var(--iv-gold)' : '';
                        if (hit) { u.classList.add('open'); unitHit = true; }
                    });
                    card.classList.toggle('rm-hidden', !!q && !roomHit && !unitHit);
                });
            }, 150);
        });
    }

    // ───────── confirm deletes ─────────
    function confirms() {
        $$('form.rm-confirm').forEach(f => f.addEventListener('submit', e => {
            if (f.dataset.ok) return;
            e.preventDefault();
            Swal.fire({
                icon: 'warning', title: 'Are you sure?', text: f.dataset.msg,
                showCancelButton: true, confirmButtonText: 'Yes, delete', cancelButtonText: 'Cancel',
                confirmButtonColor: '#d64550', cancelButtonColor: '#6b7f92'
            }).then(r => { if (r.isConfirmed) { f.dataset.ok = 1; f.requestSubmit(); } });
        }));
    }

    function init() {
        // restore open units
        const open = load();
        $$('.rm-unit').forEach(u => { if (open.has(uid(u))) u.classList.add('open'); });
        // animate rings
        requestAnimationFrame(() => setTimeout(() => $$('[data-ring]').forEach(r => r.style.setProperty('--p', r.dataset.ring)), 200));
        // reset add-room modal
        document.getElementById('addRoomModal')?.addEventListener('hidden.bs.modal', () => {
            $('#roomId').value = '0'; $('#roomNameInput').value = ''; $('#roomModalTitle').textContent = 'Add room';
        });
        search();
        confirms();
    }
    document.addEventListener('DOMContentLoaded', init);

    return { toggle, expandAll, expandRoom, inline, editRoom, editUnit, editShelf, inspect, editBoxFromInspector };
})();
