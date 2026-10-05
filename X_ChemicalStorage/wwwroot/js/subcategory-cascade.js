/*
 * subcategory-cascade.js  (v2 - hardened)
 * Category -> SubCategory cascading dropdown for the Item modal (_SaveItem.cshtml)
 *
 * Public API:
 *   ItemSubCat.reset()                    -> open modal for ADD
 *   ItemSubCat.edit(categoryId, subId)    -> open modal for EDIT
 */
(function () {
    'use strict';

    const CFG = {
        categorySel: '#itemCategory',
        subSel: '#itemSubCategory',
        fieldSel: '#subCatField',
        hintSel: '#subCatHint',
        modalSel: '#exampleModal',
        url: '/Items/GetSubCategoriesByCategory'
    };

    let cat, sub, field, hint;
    let requestId = 0;

    const MESSAGES = {
        idle: 'Choose Category first',
        loading: 'Loading subcategories…',
        empty: 'No subcategories for this category',
        error: 'Could not load subcategories',
        ready: 'Choose SubCategory'
    };

    // Resolve elements lazily so it works even if the modal is rendered/loaded late
    function els() {
        cat = document.querySelector(CFG.categorySel);
        sub = document.querySelector(CFG.subSel);
        field = document.querySelector(CFG.fieldSel);
        hint = document.querySelector(CFG.hintSel);
        const ok = !!(cat && sub && field && hint);
        if (!ok) {
            console.warn('[ItemSubCat] missing elements:', {
                category: !!cat, subCategory: !!sub, subCatField: !!field, subCatHint: !!hint
            });
        }
        return ok;
    }

    function setState(state, placeholder) {
        field.dataset.state = state;
        sub.innerHTML = '';
        sub.add(new Option(placeholder || MESSAGES[state], '', true, true));
        sub.disabled = state !== 'ready';
        sub.required = state === 'ready';
        if (state !== 'ready') hint.innerHTML = '';
    }

    function renderHint(count) {
        const catText = cat.options[cat.selectedIndex]?.text?.trim() ?? '';
        const picked = sub.options[sub.selectedIndex];
        const subText = picked && picked.value ? picked.text : null;

        hint.innerHTML = '';
        const pill = document.createElement('span');
        pill.className = 'subcat-pill';

        const c = document.createElement('span');
        c.className = 'subcat-pill-part';
        c.textContent = catText;
        pill.appendChild(c);

        if (subText) {
            const arrow = document.createElement('i');
            arrow.className = 'bi bi-chevron-right subcat-pill-arrow';
            const s = document.createElement('span');
            s.className = 'subcat-pill-part subcat-pill-sub';
            s.textContent = subText;
            pill.append(arrow, s);
        } else {
            const n = document.createElement('span');
            n.className = 'subcat-pill-count';
            n.textContent = count + (count === 1 ? ' subcategory available' : ' subcategories available');
            pill.appendChild(n);
        }
        hint.appendChild(pill);
    }

    function pop() {
        field.classList.remove('subcat-pop');
        void field.offsetWidth;
        field.classList.add('subcat-pop');
    }

    function fill(list, selectedId) {
        setState('ready');
        list.forEach(function (x) {
            // يقبل: id/name  أو  Id/Name  أو  value/text  أو  Value/Text
            const id = x.id ?? x.Id ?? x.value ?? x.Value;
            const name = x.name ?? x.Name ?? x.text ?? x.Text;

            if (id == null || name == null) return;   // تجاهل العناصر الفاضية

            sub.add(new Option(name, id, false,
                selectedId != null && selectedId !== '' && String(id) === String(selectedId)));
        });

        if (selectedId == null || selectedId === '' || !sub.value) {
            sub.selectedIndex = 0;
        }
        if (sub.options.length > 0) {
            sub.options[0].disabled = true;
        }
        pop();
        renderHint(list.length);
    }
    async function load(categoryId, selectedId) {
        if (!els()) return;
        const myId = ++requestId;

        if (!categoryId) { setState('idle'); return; }
        setState('loading');

        const url = CFG.url + '?categoryId=' + encodeURIComponent(categoryId);
        try {
            const res = await fetch(url, {
                headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
                credentials: 'same-origin'
            });
            if (!res.ok) throw new Error('HTTP ' + res.status + ' from ' + url);

            const ct = res.headers.get('content-type') || '';
            if (!ct.includes('json')) throw new Error('Response is not JSON (probably a login redirect). URL: ' + url);

            const data = await res.json();
            if (myId !== requestId) return;

            if (!data || data.length === 0) setState('empty');
            else fill(data, selectedId);
        } catch (e) {
            if (myId !== requestId) return;
            console.error('[ItemSubCat] load failed:', e);
            setState('error');
        }
    }

    const api = {
        reset: function () {
            requestId++;
            if (!els()) return;
            cat.selectedIndex = 0;
            setState('idle');
        },
        edit: function (categoryId, subCategoryId) {
            if (!els()) return;
            cat.value = String(categoryId);
            load(categoryId, subCategoryId);
        },
        load: load // exposed for debugging: ItemSubCat.load(1)
    };
    window.ItemSubCat = api;

    // Delegated listeners: work regardless of when the modal markup/script were loaded
    document.addEventListener('change', function (e) {
        if (e.target.matches(CFG.categorySel)) {
            load(e.target.value, null);
        } else if (e.target.matches(CFG.subSel)) {
            if (!els()) return;
            renderHint(sub.options.length - 1);
            pop();
        }
    });

    document.addEventListener('hidden.bs.modal', function (e) {
        if (e.target.matches(CFG.modalSel)) api.reset();
    });

    function boot() { if (els()) setState('idle'); }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();

    console.info('[ItemSubCat] loaded');
})();