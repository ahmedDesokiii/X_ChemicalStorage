// ═══════════════════════════════════════════════════════════════
//  Inventory UI helpers (Items & Lots)
// ═══════════════════════════════════════════════════════════════
(function (w) {
    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => [...r.querySelectorAll(s)];

    // ───────── Count-up numbers: <b data-count="42">0</b> ─────────
    function countUp(root = document) {
        $$('[data-count]', root).forEach(el => {
            const end = parseFloat(el.dataset.count) || 0;
            if (!end) { el.textContent = '0'; return; }
            const t0 = performance.now(), dur = 900;
            const tick = t => {
                const p = Math.min(1, (t - t0) / dur);
                el.textContent = Math.round(end * (1 - Math.pow(1 - p, 3))).toLocaleString();
                if (p < 1) requestAnimationFrame(tick);
            };
            requestAnimationFrame(tick);
        });
    }

    // ───────── Segmented SDS toggle → hidden input ─────────
    //  <input type="radio" id="sdsYes" data-seg-target="#itemSDS" data-seg-value="True">
    function bindSeg(root = document) {
        $$('[data-seg-target]', root).forEach(r => r.addEventListener('change', () => {
            const t = $(r.dataset.segTarget);
            if (t) { t.value = r.dataset.segValue; t.dispatchEvent(new Event('change', { bubbles: true })); }
        }));
    }

    // ───────── Number stepper  <div class="iv-stepper-num"> ─────────
    function bindSteppers(root = document) {
        $$('.iv-stepper-num', root).forEach(box => {
            const input = $('input', box);
            $$('button[data-step]', box).forEach(b => b.addEventListener('click', () => {
                const min = input.min !== '' ? parseFloat(input.min) : -Infinity;
                const max = input.max !== '' ? parseFloat(input.max) : Infinity;
                const v = (parseFloat(input.value) || 0) + parseFloat(b.dataset.step);
                input.value = Math.min(max, Math.max(min, v));
                input.dispatchEvent(new Event('input', { bubbles: true }));
            }));
        });
    }

    // ───────── Scan / search picker inside a modal ─────────
    //  cards: .iv-pick[data-barcode][data-name] ; exact barcode match → auto-open
    function scanPicker({ modal, input, message }) {
        const m = typeof modal === 'string' ? document.getElementById(modal) : modal;
        if (!m) return;
        const inp = $(input, m), msg = $(message, m);
        const cards = $$('.iv-pick', m);
        let timer;

        inp.addEventListener('input', () => {
            clearTimeout(timer);
            const q = inp.value.trim().toLowerCase();
            msg?.classList.add('d-none');
            // live filter while typing
            cards.forEach(c => c.classList.toggle('hidden',
                !!q && !(c.dataset.barcode || '').toLowerCase().includes(q) && !(c.dataset.name || '').toLowerCase().includes(q)));

            // scanner = fast burst then pause → treat as exact code
            timer = setTimeout(() => {
                const exact = cards.find(c => (c.dataset.barcode || '').toLowerCase() === q);
                if (exact) {
                    exact.classList.add('hit');
                    setTimeout(() => exact.click(), 250);
                } else if (q && !cards.some(c => !c.classList.contains('hidden'))) {
                    msg?.classList.remove('d-none');
                }
            }, 350);
        });

        m.addEventListener('shown.bs.modal', () => { inp.value = ''; cards.forEach(c => c.classList.remove('hidden', 'hit')); inp.focus(); });
    }

    // ───────── Room → Unit → Shelf → Box → Slot picker ─────────
    function slotPicker(cfg) {
        const room = $(cfg.room), unit = $(cfg.unit), shelf = $(cfg.shelf), box = $(cfg.box);
        if (!room) return;
        const grid = $(cfg.grid), wrap = $(cfg.wrap), label = $(cfg.label);
        const hBox = $(cfg.hiddenBox), hRow = $(cfg.hiddenRow), hCol = $(cfg.hiddenCol);
        const unitsUrl = cfg.unitsUrl || '/Rooms/GetStorageUnitsByRoom?roomId=';

        const reset = (sel, ph) => { sel.innerHTML = `<option value="">${ph}</option>`; sel.disabled = true; };
        const clear = () => { wrap.hidden = true; hBox.value = hRow.value = hCol.value = ''; cfg.onChange?.(null); };
        const load = async (url, sel, map) => {
            sel.classList.add('iv-loading');
            try {
                const data = await (await fetch(url)).json();
                data.forEach(d => sel.add(map(d)));
                sel.disabled = false;
                if (!data.length) sel.options[0].text = 'Nothing here';
            } finally { sel.classList.remove('iv-loading'); }
        };

        room.addEventListener('change', async () => {
            reset(unit, 'Storage unit'); reset(shelf, 'Shelf'); reset(box, 'Box'); clear();
            if (room.value) await load(unitsUrl + room.value, unit, u => new Option(u.name, u.id));
        });
        unit.addEventListener('change', async () => {
            reset(shelf, 'Shelf'); reset(box, 'Box'); clear();
            if (unit.value) await load(`/Rooms/GetShelvesByStorageUnit?storageUnitId=${unit.value}`, shelf, s => new Option(s.name, s.id));
        });
        shelf.addEventListener('change', async () => {
            reset(box, 'Box'); clear();
            if (shelf.value) await load(`/Rooms/GetBoxesByShelf?shelfId=${shelf.value}`, box, b => new Option(`${b.name} (${b.rowsCount}×${b.columnsCount})`, b.id));
        });
        box.addEventListener('change', async () => {
            clear();
            if (!box.value) return;
            const d = await (await fetch(`/Rooms/GetBoxGrid?boxId=${box.value}`)).json();
            if (d) render(d);
        });

        function render(d) {
            grid.style.gridTemplateColumns = `repeat(${d.columnsCount}, minmax(24px, 1fr))`;
            grid.innerHTML = '';
            const occ = {};
            (d.occupied || []).forEach(o => occ[`${o.rowIndex}-${o.columnIndex}`] = o);
            let first = null;
            for (let r = 1; r <= d.rowsCount; r++) for (let c = 1; c <= d.columnsCount; c++) {
                const b = document.createElement('button');
                b.type = 'button';
                b.style.animationDelay = ((r + c) * 15) + 'ms';
                const o = occ[`${r}-${c}`];
                if (o) {
                    b.className = 'iv-cell ' + (o.kind === 'sample' ? 'sample' : 'lot');
                    b.disabled = true;
                    b.title = `${o.kind === 'sample' ? 'Sample' : 'Lot'}: ${o.lotNumber}`;
                } else {
                    b.className = 'iv-cell free';
                    b.title = `Row ${r} · Col ${c}`;
                    b.addEventListener('click', () => pick(b, r, c));
                    first ??= [b, r, c];
                }
                grid.appendChild(b);
            }
            hBox.value = box.value;
            wrap.hidden = false;
            label.innerHTML = `${d.availableSlots} free of ${d.totalSlots} · <a href="#" class="iv-auto">auto-pick first free</a>`;
            $('.iv-auto', label)?.addEventListener('click', e => { e.preventDefault(); if (first) pick(...first); });
        }

        function pick(cell, r, c) {
            $$('.iv-cell.sel', grid).forEach(x => x.classList.remove('sel'));
            cell.classList.add('sel');
            hRow.value = r; hCol.value = c;
            const t = s => s.options[s.selectedIndex].text;
            const path = `${t(room)} › ${t(unit)} › ${t(shelf)} › ${t(box).split(' (')[0]} · R${r}C${c}`;
            label.innerHTML = `<i class="bi bi-geo-alt-fill text-warning"></i> ${path}`;
            cfg.onChange?.(path);
        }
    }

    // ───────── Popovers ─────────
    function popovers() {
        if (!w.bootstrap) return;
        $$('[data-bs-toggle="popover"]').forEach(el => new bootstrap.Popover(el));
    }

    document.addEventListener('DOMContentLoaded', () => { countUp(); bindSeg(); bindSteppers(); popovers(); });

    w.IV = { countUp, bindSeg, bindSteppers, scanPicker, slotPicker };
})(window);
