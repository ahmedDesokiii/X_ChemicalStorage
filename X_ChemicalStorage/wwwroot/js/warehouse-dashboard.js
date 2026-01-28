/************************************
 * DASHBOARD – LOT + ITEM CHARTS
 ************************************/

const qs = s => document.querySelector(s);

const COLORS = {
    low: '#198754',
    medium: '#ffc107',
    high: '#dc3545'
};

/* ========== LOT TIMELINE ========== */
(function () {
    const el = qs("#lotTimeline");
    if (!el || !window.lotTimelineData?.length) return;

    new ApexCharts(el, {
        chart: { type: 'rangeBar', height: 260, toolbar: { show: false } },
        plotOptions: { bar: { horizontal: true, borderRadius: 6 } },
        series: [{ data: lotTimelineData }],
        xaxis: { type: 'datetime' }
    }).render();
})();

/* ========== LOT DONUT ========== */
(function () {
    const el = qs("#stockDonut");
    if (!el || !window.stockDonut) return;

    new ApexCharts(el, {
        chart: { type: 'donut', height: 260 },
        labels: ['Available', 'Low', 'Expired'],
        series: [
            stockDonut.available ?? 0,
            stockDonut.low ?? 0,
            stockDonut.expired ?? 0
        ],
        colors: [COLORS.low, COLORS.medium, COLORS.high],
        legend: { position: 'bottom' }
    }).render();
})();

/* ========== ITEM TIMELINE ========== */
(function () {
    const el = qs("#itemTimeline");
    if (!el || !window.itemTimelineData?.length) return;

    new ApexCharts(el, {
        chart: { type: 'rangeBar', height: 300, toolbar: { show: false } },
        plotOptions: { bar: { horizontal: true, borderRadius: 6 } },
        series: [{
            data: itemTimelineData.map(i => ({
                x: i.itemName,
                y: [new Date(i.start), new Date(i.end)],
                fillColor:
                    i.risk === 'high' ? COLORS.high :
                        i.risk === 'medium' ? COLORS.medium :
                            COLORS.low
            }))
        }],
        xaxis: { type: 'datetime' }
    }).render();
})();

/* ========== ITEM DONUT ========== */
(function () {
    const el = qs("#itemDonut");
    if (!el || !window.itemStock) return;

    new ApexCharts(el, {
        chart: { type: 'donut', height: 260 },
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

/* ========== LOT DRILL DOWN ========== */
function openLot(id) {
    if (!id) return;
    fetch(`/Warehouse/Lot/Details/${id}`)
        .then(r => r.text())
        .then(html => {
            qs("#lotContent").innerHTML = html;
            new bootstrap.Offcanvas('#lotDetails').show();
        });
}

  

   
