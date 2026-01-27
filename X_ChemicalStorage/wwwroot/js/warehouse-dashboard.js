/************************************
 * DASHBOARD – LOT + ITEM CHARTS
 ************************************/

/* ========== GLOBAL SAFE CHECK ========== */
const qs = s => document.querySelector(s);

/* ========== COLORS ========== */
const COLORS = {
    low: '#198754',
    medium: '#ffc107',
    high: '#dc3545'
};

/* ========== LOT TIMELINE (IMPROVED) ========== */
(function initLotTimeline() {
    const el = qs("#lotTimeline");
    if (!el || !window.lotTimelineData || !lotTimelineData.length) return;

    new ApexCharts(el, {
        chart: {
            type: 'rangeBar',
            height: 260,
            toolbar: { show: false }
        },
        plotOptions: {
            bar: { horizontal: true, borderRadius: 6 }
        },
        series: [{
            data: lotTimelineData.map(l => ({
                ...l,
                fillColor:
                    l.risk === 'high' ? COLORS.high :
                        l.risk === 'medium' ? COLORS.medium :
                            COLORS.low
            }))
        }],
        xaxis: { type: 'datetime' },
        tooltip: {
            theme: 'dark',
            custom({ w, dataPointIndex }) {
                const d = w.config.series[0].data[dataPointIndex];
                return `
                    <div class="p-2">
                        <b>${d.x}</b><br/>
                        Days Left: ${d.days ?? '-'}<br/>
                        Qty: ${d.qty ?? '-'}
                    </div>`;
            }
        }
    }).render();
})();

/* ========== STOCK DONUT (IMPROVED) ========== */
(function initStockDonut() {
    const el = qs("#stockDonut");
    if (!el || !window.stockDonut) return;

    new ApexCharts(el, {
        chart: {
            type: 'donut',
            height: 260,
            events: {
                dataPointSelection(e, chart, opts) {
                    const t = ['available', 'low', 'expired'][opts.dataPointIndex];
                    if (t) location.href = `/Warehouse/Stock/${t}`;
                }
            }
        },
        labels: ['Available', 'Low Stock', 'Expired'],
        series: [
            stockDonut.available ?? 0,
            stockDonut.low ?? 0,
            stockDonut.expired ?? 0
        ],
        colors: [COLORS.low, COLORS.medium, COLORS.high],
        legend: { position: 'bottom' },
        dataLabels: {
            formatter: v => `${v.toFixed(1)}%`
        }
    }).render();

    if (stockDonut.low > 0 || stockDonut.expired > 0) {
        el.classList.add('pulse-danger');
    }
})();

/* ========== ITEM TIMELINE (NEW) ========== */
(function initItemTimeline() {
    const el = qs("#itemTimeline");
    if (!el || !window.itemTimelineData || !itemTimelineData.length) return;

    new ApexCharts(el, {
        chart: {
            type: 'rangeBar',
            height: 300,
            toolbar: { show: false }
        },
        plotOptions: {
            bar: { horizontal: true, borderRadius: 6 }
        },
        series: [{
            data: itemTimelineData.map(i => ({
                x: i.itemName,
                y: [new Date(i.start), new Date(i.end)],
                qty: i.totalQty,
                fillColor:
                    i.risk === 'high' ? COLORS.high :
                        i.risk === 'medium' ? COLORS.medium :
                            COLORS.low
            }))
        }],
        xaxis: { type: 'datetime' },
        tooltip: {
            custom({ w, dataPointIndex }) {
                const d = w.config.series[0].data[dataPointIndex];
                return `
                    <div class="p-2">
                        <b>${d.x}</b><br/>
                        Qty: ${d.qty}
                    </div>`;
            }
        }
    }).render();
})();

/* ========== ITEM DONUT (NEW) ========== */
(function initItemDonut() {
    const el = qs("#itemDonut");
    if (!el || !window.itemStock) return;

    new ApexCharts(el, {
        chart: {
            type: 'donut',
            height: 260,
            events: {
                dataPointSelection(e, chart, opts) {
                    const t = ['available', 'low', 'expiring'][opts.dataPointIndex];
                    if (t) location.href = `/Warehouse/Items?filter=${t}`;
                }
            }
        },
        labels: ['Available', 'Low', 'Expiring'],
        series: [
            itemStock.available ?? 0,
            itemStock.low ?? 0,
            itemStock.expiring ?? 0
        ],
        colors: [COLORS.low, COLORS.medium, COLORS.high],
        legend: { position: 'bottom' }
    }).render();
})();

/* ========== LOT DRILL DOWN (UNCHANGED + SAFE) ========== */
function openLot(id) {
    if (!id) return;
    fetch(`/Warehouse/Lot/Details/${id}`)
        .then(r => r.text())
        .then(html => {
            qs("#lotContent").innerHTML = html;
            new bootstrap.Offcanvas('#lotDetails').show();
        });
}

/* ========== ITEM DRILL DOWN (NEW) ========== */
function openItem(id) {
    if (!id) return;
    fetch(`/Warehouse/Item/Details/${id}`)
        .then(r => r.text())
        .then(html => {
            qs("#lotContent").innerHTML = html;
            new bootstrap.Offcanvas('#lotDetails').show();
        });
}

/* ========== OPTIONAL: AUTO REFRESH (SAFE) ========== */
// setInterval(() => {
//     fetch('/Warehouse/Dashboard/Timeline')
//         .then(r => r.json())
//         .then(data => console.log('refresh ready', data));
// }, 300000);
