// Temporary Sample Reception wizard
(() => {
    const $ = (s, r = document) => r.querySelector(s);
    const $$ = (s, r = document) => [...r.querySelectorAll(s)];
    const form = $('#tsForm');
    if (!form) return;

    // ───────────────── Stepper ─────────────────
    let step = 1;
    const TOTAL = 4;
    const panels = $$('.ts-panel');
    const steps = $$('.ts-step');
    const prev = $('#tsPrev'), next = $('#tsNext'), submit = $('#tsSubmit');

    function go(to) {
        if (to > step && !validateStep(step)) return;
        const dir = to > step ? 'fwd' : 'back';
        panels.forEach(p => {
            const n = +p.dataset.step;
            p.classList.remove('active', 'enter-fwd', 'enter-back');
            if (n === to) { p.classList.add('active', 'enter-' + dir); }
        });
        steps.forEach(s => {
            const n = +s.dataset.step;
            s.classList.toggle('active', n === to);
            s.classList.toggle('done', n < to);
        });
        $('#tsTrackFill').style.width = ((to - 1) / (TOTAL - 1) * 100) + '%';
        step = to;
        prev.disabled = step === 1;
        next.hidden = step === TOTAL;
        submit.hidden = step !== TOTAL;
        window.scrollTo({ top: form.offsetTop - 20, behavior: 'smooth' });
    }

    function validateStep(n) {
        const panel = $(`.ts-panel[data-step="${n}"]`);
        let ok = true;
        $$('[required]', panel).forEach(el => {
            const bad = !el.value || !el.checkValidity();
            el.classList.toggle('is-invalid', bad);
            if (bad && ok) { el.focus(); shake(el.closest('.ts-field') || el); }
            ok = ok && !bad;
        });
        if (n === 1) {
            const d = new Date($('#DeliveryDateTime').value), r = new Date($('#ReceptionDateTime').value);
            if (r < d) {
                $('#ReceptionDateTime').classList.add('is-invalid');
                shake($('#ReceptionDateTime').closest('.ts-field'));
                ok = false;
            }
        }
        return ok;
    }

    function shake(el) { el.classList.remove('ts-shake'); void el.offsetWidth; el.classList.add('ts-shake'); }

    next.addEventListener('click', () => go(Math.min(TOTAL, step + 1)));
    prev.addEventListener('click', () => go(Math.max(1, step - 1)));
    steps.forEach(s => s.addEventListener('click', () => {
        const to = +s.dataset.step;
        if (to < step) return go(to);
        for (let i = step; i < to; i++) if (!validateStep(i)) return;
        go(to);
    }));
    $$('input', form).forEach(i => i.addEventListener('keydown', e => {
        if (e.key === 'Enter' && step < TOTAL) { e.preventDefault(); next.click(); }
    }));

    // ───────────────── Live passport ─────────────────
    const out = k => $$(`[data-out="${k}"]`);
    const set = (k, v) => out(k).forEach(o => {
        if (o.textContent === v) return;
        o.textContent = v;
        o.classList.remove('ts-flash'); void o.offsetWidth; o.classList.add('ts-flash');
    });
    const fmtDate = v => v ? new Date(v).toLocaleDateString(undefined, { day: '2-digit', month: 'short', year: 'numeric' }) : '—';

    function refreshPassport() {
        set('type', $('#SampleType').value || 'Sample type');
        set('qty', $('#QuantityReceived').value || '0');
        const u = $('#UnitId'); set('unit', u.value ? u.options[u.selectedIndex].text : '');
        set('containers', $('#ContainersCount').value || '1');
        set('fromOrg', $('#DeliveringOrganization').value || '—');
        set('deliveredBy', $('#DeliveredByName').value);
        set('toOrg', $('#ReceivingOrganization').value || '—');
        set('analysis', $('#RequestedAnalysis').value || '—');
        set('ref', $('#RequestReferenceNumber').value || '—');
        set('withdraw', fmtDate($('#PlannedWithdrawalDate').value));
        const t = $('input[name="TransportCondition"]:checked');
        const temp = $('#ArrivalTemperatureC').value;
        set('transport', (t ? t.value : '—') + (temp ? ` · ${temp}°C` : ''));
        lagHint();
        completeness();
    }
    form.addEventListener('input', refreshPassport);
    form.addEventListener('change', refreshPassport);

    function lagHint() {
        const d = new Date($('#DeliveryDateTime').value), r = new Date($('#ReceptionDateTime').value);
        const h = $('#tsLagHint');
        if (isNaN(d) || isNaN(r)) { h.textContent = ''; return; }
        const mins = Math.round((r - d) / 60000);
        h.className = 'ts-hint ' + (mins < 0 ? 'bad' : mins > 120 ? 'warn' : 'good');
        h.innerHTML = mins < 0 ? '<i class="bi bi-x-circle"></i> Reception before delivery'
            : `<i class="bi bi-stopwatch"></i> Hand-over lag: ${mins >= 60 ? Math.floor(mins / 60) + 'h ' : ''}${mins % 60}m`;
    }

    const tracked = ['DeliveredByName', 'DeliveringOrganization', 'ReceivingOrganization', 'SampleType', 'QuantityReceived',
        'UnitId', 'RequestedAnalysis', 'RequestReferenceNumber', 'PlannedWithdrawalDate', 'ArrivalTemperatureC', 'Description'];
    function completeness() {
        let filled = tracked.filter(id => $('#' + id)?.value).length;
        if ($('#tsBoxId').value && $('#tsRow').value) filled++;
        if (fileStore.length) filled++;
        const p = Math.round(filled / (tracked.length + 2) * 100);
        const ring = $('#tsCompleteness');
        ring.style.setProperty('--p', p);
        ring.querySelector('span').textContent = p + '%';
    }

    // Containers +/-
    $$('.ts-stepper-num button').forEach(b => b.addEventListener('click', () => {
        const i = $('#ContainersCount');
        i.value = Math.max(1, (+i.value || 1) + (+b.dataset.num));
        i.dispatchEvent(new Event('input', { bubbles: true }));
    }));

    // ───────────────── Temperature gauge ─────────────────
    const G_MIN = -200, G_MAX = 40;                    // gauge scale
    const ARC_LEN = Math.PI * 80;                      // half circle, r=80
    const angleFor = t => -90 + (Math.min(Math.max(t, G_MIN), G_MAX) - G_MIN) / (G_MAX - G_MIN) * 180;

    function updateGauge() {
        const chip = $('input[name="TransportCondition"]:checked');
        const min = chip?.dataset.min !== '' ? parseFloat(chip?.dataset.min) : NaN;
        const max = chip?.dataset.max !== '' ? parseFloat(chip?.dataset.max) : NaN;
        const band = $('#tsGaugeBand');
        const gauge = $('#tsGauge');

        if (!isNaN(min)) {
            const a = (min - G_MIN) / (G_MAX - G_MIN), b = (max - G_MIN) / (G_MAX - G_MIN);
            band.style.strokeDasharray = `${(b - a) * ARC_LEN} ${ARC_LEN}`;
            band.style.strokeDashoffset = `${-a * ARC_LEN}`;
            band.style.opacity = 1;
        } else band.style.opacity = 0;

        const raw = $('#ArrivalTemperatureC').value;
        if (raw === '') {
            gauge.dataset.state = 'idle';
            $('#tsGaugeVal').textContent = '—';
            $('#tsGaugeMsg').textContent = 'enter arrival temperature';
            $('#tsNeedle').style.transform = `rotate(${angleFor(G_MIN)}deg)`;
            return;
        }
        const t = parseFloat(raw);
        $('#tsNeedle').style.transform = `rotate(${angleFor(t)}deg)`;
        $('#tsGaugeVal').textContent = t.toFixed(1) + '°C';
        if (isNaN(min)) { gauge.dataset.state = 'neutral'; $('#tsGaugeMsg').textContent = 'no range defined'; }
        else if (t < min || t > max) { gauge.dataset.state = 'bad'; $('#tsGaugeMsg').textContent = `OUT of range (${min}…${max})`; }
        else { gauge.dataset.state = 'good'; $('#tsGaugeMsg').textContent = 'within range'; }
    }
    $$('input[name="TransportCondition"]').forEach(r => r.addEventListener('change', updateGauge));
    $('#ArrivalTemperatureC').addEventListener('input', updateGauge);

    // ───────────────── Attachments ─────────────────
    const fileInput = $('#tsFiles'), drop = $('#tsDrop'), list = $('#tsFileList');
    let fileStore = [];   // { file, kind }
    const KINDS = ['DeliveryNote', 'RequestForm', 'ChainOfCustodyForm', 'TemperatureLog', 'Photo', 'Other'];
    const guessKind = f => f.type.startsWith('image/') ? 'Photo'
        : /temp|log/i.test(f.name) ? 'TemperatureLog'
        : /request|req/i.test(f.name) ? 'RequestForm'
        : /delivery|dn|note/i.test(f.name) ? 'DeliveryNote' : 'Other';

    drop.addEventListener('click', () => fileInput.click());
    ['dragenter', 'dragover'].forEach(e => drop.addEventListener(e, ev => { ev.preventDefault(); drop.classList.add('over'); }));
    ['dragleave', 'drop'].forEach(e => drop.addEventListener(e, ev => { ev.preventDefault(); drop.classList.remove('over'); }));
    drop.addEventListener('drop', ev => addFiles(ev.dataTransfer.files));
    fileInput.addEventListener('change', () => addFiles(fileInput.files));

    function addFiles(files) {
        [...files].forEach(f => {
            if (f.size > 25 * 1024 * 1024) { Swal.fire({ icon: 'warning', title: 'Too large', text: `${f.name} exceeds 25 MB` }); return; }
            fileStore.push({ file: f, kind: guessKind(f) });
        });
        renderFiles();
    }

    function renderFiles() {
        list.innerHTML = '';
        fileStore.forEach((x, i) => {
            const li = document.createElement('li');
            li.style.animationDelay = (i * 60) + 'ms';
            const icon = x.file.type.startsWith('image/') ? 'bi-file-earmark-image' : x.file.type.includes('pdf') ? 'bi-file-earmark-pdf' : 'bi-file-earmark-text';
            li.innerHTML = `<i class="bi ${icon}"></i>
                <div class="nm"><b>${escapeHtml(x.file.name)}</b><small>${(x.file.size / 1024).toFixed(0)} KB</small></div>
                <select class="form-select form-select-sm">${KINDS.map(k => `<option ${k === x.kind ? 'selected' : ''}>${k}</option>`).join('')}</select>
                <button type="button" title="Remove"><i class="bi bi-x-lg"></i></button>`;
            li.querySelector('select').addEventListener('change', e => { x.kind = e.target.value; syncFiles(); });
            li.querySelector('button').addEventListener('click', () => { fileStore.splice(i, 1); renderFiles(); });
            list.appendChild(li);
        });
        syncFiles();
        completeness();
    }

    function syncFiles() {
        const dt = new DataTransfer();
        fileStore.forEach(x => dt.items.add(x.file));
        fileInput.files = dt.files;
        $('#tsKindInputs').innerHTML = fileStore.map(x => `<input type="hidden" name="FileKinds" value="${x.kind}" />`).join('');
    }

    const escapeHtml = s => s.replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

    // ───────────────── Placement cascade + grid ─────────────────
    const room = $('#tsRoom'), unit = $('#tsUnit'), shelf = $('#tsShelf'), box = $('#tsBox');
    if (room) {
        const reset = (sel, ph) => { sel.innerHTML = `<option value="">${ph}</option>`; sel.disabled = true; };
        const clearSlot = () => {
            $('#tsGridWrap').hidden = true;
            ['#tsBoxId', '#tsRow', '#tsCol'].forEach(s => $(s).value = '');
            setLocation(null);
        };
        const load = async (url, sel, map) => {
            sel.classList.add('ts-loading');
            const data = await (await fetch(url)).json();
            data.forEach(d => sel.add(map(d)));
            sel.disabled = false;
            sel.classList.remove('ts-loading');
        };

        room.addEventListener('change', async () => {
            reset(unit, 'Holding unit'); reset(shelf, 'Shelf'); reset(box, 'Box'); clearSlot();
            if (room.value) await load(`/TempSamples/GetHoldingUnitsByRoom?roomId=${room.value}`, unit, u => new Option(`${u.name} · ${u.type}`, u.id));
        });
        unit.addEventListener('change', async () => {
            reset(shelf, 'Shelf'); reset(box, 'Box'); clearSlot();
            if (unit.value) await load(`/Rooms/GetShelvesByStorageUnit?storageUnitId=${unit.value}`, shelf, s => new Option(s.name, s.id));
        });
        shelf.addEventListener('change', async () => {
            reset(box, 'Box'); clearSlot();
            if (shelf.value) await load(`/Rooms/GetBoxesByShelf?shelfId=${shelf.value}`, box, b => new Option(`${b.name} (${b.rowsCount}×${b.columnsCount})`, b.id));
        });
        box.addEventListener('change', async () => {
            clearSlot();
            if (!box.value) return;
            const data = await (await fetch(`/Rooms/GetBoxGrid?boxId=${box.value}`)).json();
            if (data) renderGrid(data);
        });

        function renderGrid(d) {
            const grid = $('#tsGrid');
            grid.style.gridTemplateColumns = `repeat(${d.columnsCount}, minmax(26px, 1fr))`;
            grid.innerHTML = '';
            const occ = {};
            d.occupied.forEach(o => occ[`${o.rowIndex}-${o.columnIndex}`] = o);
            let firstFree = null;
            for (let r = 1; r <= d.rowsCount; r++) for (let c = 1; c <= d.columnsCount; c++) {
                const cell = document.createElement('button');
                cell.type = 'button';
                cell.style.animationDelay = ((r + c) * 18) + 'ms';
                const o = occ[`${r}-${c}`];
                if (o) {
                    cell.className = 'ts-cell ' + (o.kind === 'sample' ? 'sample' : 'lot');
                    cell.disabled = true;
                    cell.title = `${o.kind === 'sample' ? 'Sample' : 'Lot'} ${o.lotNumber}`;
                } else {
                    cell.className = 'ts-cell free';
                    cell.title = `Row ${r} · Col ${c}`;
                    cell.addEventListener('click', () => pick(cell, r, c));
                    firstFree ??= [cell, r, c];
                }
                grid.appendChild(cell);
            }
            $('#tsGridWrap').hidden = false;
            $('#tsBoxId').value = box.value;
            $('#tsSlotLabel').innerHTML = `${d.availableSlots} free of ${d.totalSlots} · <a href="#" id="tsAuto">auto-pick first free</a>`;
            $('#tsAuto')?.addEventListener('click', e => { e.preventDefault(); if (firstFree) pick(...firstFree); });
        }

        function pick(cell, r, c) {
            $$('.ts-cell.sel').forEach(x => x.classList.remove('sel'));
            cell.classList.add('sel');
            $('#tsRow').value = r; $('#tsCol').value = c;
            const path = `${room.options[room.selectedIndex].text} › ${unit.options[unit.selectedIndex].text.split(' · ')[0]} › ${shelf.options[shelf.selectedIndex].text} › ${box.options[box.selectedIndex].text.split(' (')[0]} [R${r}·C${c}]`;
            $('#tsSlotLabel').innerHTML = `<i class="bi bi-geo-alt-fill"></i> ${path}`;
            setLocation(path);
            completeness();
        }
    }

    function setLocation(path) {
        set('location', path || 'Awaiting placement');
        $('#tsChainStored')?.classList.toggle('on', !!path);
    }

    // ───────────────── Submit ─────────────────
    form.addEventListener('submit', e => {
        for (let i = 1; i <= TOTAL; i++) if (!validateStep(i)) { e.preventDefault(); go(i); return; }
        const hasBox = $('#tsBoxId').value, hasSlot = $('#tsRow').value;
        if (hasBox && !hasSlot) {
            e.preventDefault();
            Swal.fire({ icon: 'info', title: 'Pick a slot', text: 'You chose a box — click a free slot, or clear the box to place later.' });
            return;
        }
        if (!hasBox && !form.dataset.confirmed) {
            e.preventDefault();
            Swal.fire({
                icon: 'question', title: 'Receive without placement?',
                text: 'The sample will be logged as "Awaiting placement".',
                showCancelButton: true, confirmButtonText: 'Yes, receive', cancelButtonText: 'Choose a slot'
            }).then(r => { if (r.isConfirmed) { form.dataset.confirmed = '1'; seal(); } });
            return;
        }
        if (!form.dataset.sealing) { e.preventDefault(); seal(); }
    });

    function seal() {
        form.dataset.sealing = '1';
        submit.disabled = true;
        submit.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Sealing chain…';
        $('#tsPassport').classList.add('sealing');
        setTimeout(() => form.submit(), 650);   // let the seal animation play
    }

    refreshPassport();
    updateGauge();
})();
