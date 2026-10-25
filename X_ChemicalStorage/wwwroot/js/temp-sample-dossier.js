// ═══════════════════════════════════════════════════════════════
//  Temporary Sample — Dossier page
// ═══════════════════════════════════════════════════════════════
window.TSDossier = (() => {
    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => [...r.querySelectorAll(s)];
    const sleep = ms => new Promise(r => setTimeout(r, ms));

    function rings() {
        requestAnimationFrame(() => $$('[data-ring]').forEach(r => r.style.setProperty('--p', r.dataset.ring)));
    }

    // ───────── Current box grid ─────────
    async function boxGrid() {
        const g = $('#dossierGrid');
        if (!g) return;
        const d = await (await fetch(`/Rooms/GetBoxGrid?boxId=${g.dataset.box}`)).json();
        if (!d) return;
        const occ = {};
        d.occupied.forEach(o => occ[`${o.rowIndex}-${o.columnIndex}`] = o);
        for (let r = 1; r <= d.rowsCount; r++) for (let c = 1; c <= d.columnsCount; c++) {
            const o = occ[`${r}-${c}`];
            const el = document.createElement('span');
            el.style.animationDelay = ((r + c) * 14) + 'ms';
            if (o && o.lotNumber === g.dataset.code) { el.className = 'iv-cell here'; el.innerHTML = '<i class="bi bi-geo-fill"></i>'; el.title = 'This sample'; }
            else if (o) { el.className = 'iv-cell ' + (o.kind === 'sample' ? 'sample' : 'lot'); el.title = o.lotNumber; }
            else { el.className = 'iv-cell free'; el.style.cursor = 'default'; el.title = `R${r} C${c}`; }
            g.appendChild(el);
        }
    }

    // ───────── Signature pads ─────────
    function signaturePads() {
        $$('.ts-sig').forEach(box => {
            const canvas = $('canvas', box), ctx = canvas.getContext('2d');
            let drawing = false, signed = false, last = null;

            const fit = () => {
                const ratio = window.devicePixelRatio || 1, w = canvas.clientWidth, h = canvas.clientHeight;
                if (!w) return;
                canvas.width = w * ratio; canvas.height = h * ratio;
                ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
                ctx.lineWidth = 2.4; ctx.lineCap = 'round'; ctx.lineJoin = 'round'; ctx.strokeStyle = '#032b42';
            };
            box.closest('.modal')?.addEventListener('shown.bs.modal', fit);

            const pos = e => { const r = canvas.getBoundingClientRect(); return { x: e.clientX - r.left, y: e.clientY - r.top }; };
            canvas.addEventListener('pointerdown', e => { drawing = true; last = pos(e); canvas.setPointerCapture(e.pointerId); });
            canvas.addEventListener('pointermove', e => {
                if (!drawing) return;
                const p = pos(e);
                ctx.beginPath(); ctx.moveTo(last.x, last.y);
                ctx.quadraticCurveTo(last.x, last.y, (last.x + p.x) / 2, (last.y + p.y) / 2);
                ctx.lineTo(p.x, p.y); ctx.stroke();
                last = p;
                if (!signed) { signed = true; box.classList.add('signed'); }
            });
            ['pointerup', 'pointerleave', 'pointercancel'].forEach(t => canvas.addEventListener(t, () => drawing = false));
            $('.clear', box).addEventListener('click', () => { ctx.clearRect(0, 0, canvas.width, canvas.height); signed = false; box.classList.remove('signed'); });

            const form = box.closest('form');
            form.addEventListener('submit', () => {
                $('input[name="SignatureDataUrl"]', form).value = signed ? canvas.toDataURL('image/png') : '';
            }, true);
        });
    }

    // ───────── Quantity previews ─────────
    function qtyPreviews() {
        $$('[data-qty-preview]').forEach(inp => {
            const box = $(inp.dataset.qtyPreview), max = parseFloat(box.dataset.max) || 0;
            const upd = () => {
                const q = Math.min(max, Math.max(0, parseFloat(inp.value) || 0));
                const left = +(max - q).toFixed(3);
                $('b', box).textContent = left;
                $('.bar span', box).style.width = (max ? left / max * 100 : 0) + '%';
                box.classList.toggle('empty', left === 0);
            };
            inp.addEventListener('input', upd); upd();
        });

        // Withdraw: full vs partial
        const full = $('#wFull'), part = $('#wPart');
        if (full) {
            const qf = $('#wQtyField'), qi = $('input[name="Quantity"]', qf), prev = $('#wPrev');
            const sync = () => {
                qf.hidden = full.checked;
                qi.required = !full.checked;
                if (full.checked) { qi.value = prev.dataset.max; } else { qi.value = ''; }
                qi.dispatchEvent(new Event('input'));
            };
            full.addEventListener('change', sync); part.addEventListener('change', sync); sync();
        }
    }

    // ───────── Upload modal ─────────
    function uploads() {
        const drop = $('#uDrop'); if (!drop) return;
        const input = $('#uFiles'), list = $('#uList');
        const KINDS = ['DeliveryNote', 'RequestForm', 'ChainOfCustodyForm', 'TemperatureLog', 'Photo', 'DisposalCertificate', 'Other'];
        let store = [];
        const guess = f => f.type.startsWith('image/') ? 'Photo' : /temp|log/i.test(f.name) ? 'TemperatureLog' : /request|req/i.test(f.name) ? 'RequestForm' : /dispos|certif/i.test(f.name) ? 'DisposalCertificate' : 'Other';
        const esc = s => s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

        drop.addEventListener('click', () => input.click());
        ['dragenter', 'dragover'].forEach(e => drop.addEventListener(e, ev => { ev.preventDefault(); drop.classList.add('over'); }));
        ['dragleave', 'drop'].forEach(e => drop.addEventListener(e, ev => { ev.preventDefault(); drop.classList.remove('over'); }));
        drop.addEventListener('drop', ev => add(ev.dataTransfer.files));
        input.addEventListener('change', () => add(input.files));

        function add(files) { [...files].forEach(f => { if (f.size <= 25 * 1024 * 1024) store.push({ f, k: guess(f) }); }); render(); }
        function render() {
            list.innerHTML = '';
            store.forEach((x, i) => {
                const li = document.createElement('li');
                li.innerHTML = `<i class="bi bi-file-earmark"></i><div class="nm"><b>${esc(x.f.name)}</b><small>${(x.f.size / 1024).toFixed(0)} KB</small></div>
                    <select class="form-select form-select-sm">${KINDS.map(k => `<option ${k === x.k ? 'selected' : ''}>${k}</option>`).join('')}</select>
                    <button type="button"><i class="bi bi-x-lg"></i></button>`;
                $('select', li).onchange = e => { x.k = e.target.value; sync(); };
                $('button', li).onclick = () => { store.splice(i, 1); render(); };
                list.appendChild(li);
            });
            sync();
        }
        function sync() {
            const dt = new DataTransfer(); store.forEach(x => dt.items.add(x.f)); input.files = dt.files;
            $('#uKinds').innerHTML = store.map(x => `<input type="hidden" name="fileKinds" value="${x.k}">`).join('');
        }
        drop.closest('form').addEventListener('submit', e => {
            if (!store.length) { e.preventDefault(); e.stopImmediatePropagation(); Swal.fire({ icon: 'info', title: 'Choose at least one file', confirmButtonColor: '#032b42' }); }
        }, true);
    }

    // ───────── Generic submit guards ─────────
    function guards() {
        $$('.ts-modal form').forEach(form => {
            form.addEventListener('submit', e => {
                const need = $('[data-need]', form);
                if (need && !$(need.dataset.need)?.value) {
                    e.preventDefault();
                    Swal.fire({ icon: 'info', title: 'Pick a free slot first', confirmButtonColor: '#032b42' });
                    return;
                }
                const method = $$('input[name="Method"]', form);
                if (method.length && !method.some(m => m.checked)) {
                    e.preventDefault();
                    Swal.fire({ icon: 'info', title: 'Choose a disposal method', confirmButtonColor: '#032b42' });
                    return;
                }
                if (form.dataset.confirm && !form.dataset.ok) {
                    e.preventDefault();
                    Swal.fire({ icon: 'warning', title: 'Are you sure?', text: form.dataset.confirm, showCancelButton: true, confirmButtonText: 'Yes', confirmButtonColor: '#e5484d' })
                        .then(r => { if (r.isConfirmed) { form.dataset.ok = 1; form.requestSubmit(); } });
                    return;
                }
                const btn = $('.modal-footer button:not([data-bs-dismiss])', form);
                if (btn) { btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Sealing…'; setTimeout(() => btn.disabled = true, 0); }
            });
        });
    }

    // ───────── Chain verification animation ─────────
    async function verify(url) {
        const btn = $('#btnVerify'); if (!btn) return;
        btn.addEventListener('click', async () => {
            btn.disabled = true;
            btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Verifying…';
            const res = await (await fetch(url)).json();

            const evs = $$('.ts-ev').sort((a, b) => a.dataset.seq - b.dataset.seq);   // oldest → newest
            evs.forEach(e => $('.ts-seal', e).classList.remove('ok', 'bad'));
            for (const e of evs) {
                const seq = +e.dataset.seq;
                const bad = !res.intact && seq >= res.brokenAt;
                e.classList.add('verifying');
                e.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
                $('.ts-seal', e).classList.add(bad ? 'bad' : 'ok');
                await sleep(Math.max(60, 900 / evs.length));
                e.classList.remove('verifying');
            }

            const banner = $('#chainBanner');
            banner.className = 'ts-chain-banner ' + (res.intact ? 'ok' : 'bad');
            banner.innerHTML = res.intact
                ? `<i class="bi bi-shield-fill-check"></i><span>Verified just now — all ${evs.length} seals match.</span>`
                : `<i class="bi bi-shield-fill-x"></i><span>Tampering detected at event #${res.brokenAt}.</span>`;
            btn.disabled = false;
            btn.innerHTML = '<i class="bi bi-shield-check"></i> Verify again';
        });
    }

    function init(cfg) {
        rings();
        boxGrid();
        signaturePads();
        qtyPreviews();
        uploads();
        guards();
        verify(cfg.verifyUrl);
        if (window.IV) IV.slotPicker({
            room: '#pRoom', unit: '#pUnit', shelf: '#pShelf', box: '#pBox',
            grid: '#pGrid', wrap: '#pWrap', label: '#pLabel',
            hiddenBox: '#pBoxId', hiddenRow: '#pRow', hiddenCol: '#pCol',
            unitsUrl: '/TempSamples/GetHoldingUnitsByRoom?roomId='
        });
    }

    return { init };
})();
