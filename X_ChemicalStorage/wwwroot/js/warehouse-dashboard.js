// LOT TIMELINE
new ApexCharts(document.querySelector("#lotTimeline"), {
    chart: {
        type: 'rangeBar',
        height: 260,
        toolbar: { show: false }
    },
    plotOptions: {
        bar: { horizontal: true, borderRadius: 6 }
    },
    series: [{ data: lotTimelineData }],
    xaxis: { type: 'datetime' },
    colors: ['#dc3545', '#ffc107', '#198754'],
    tooltip: { theme: 'dark' }
}).render();

// DONUT
new ApexCharts(document.querySelector("#stockDonut"), {
    chart: { type: 'donut', height: 260 },
    labels: ['Available', 'Low Stock', 'Expired'],
    series: [
        stockDonut.available,
        stockDonut.low,
        stockDonut.expired
    ],
    colors: ['#198754', '#ffc107', '#dc3545'],
    legend: { position: 'bottom' }
}).render();

// DRILL DOWN
function openLot(id) {
    fetch(`/Warehouse/Lot/Details/${id}`)
        .then(r => r.text())
        .then(html => {
            document.getElementById("lotContent").innerHTML = html;
            new bootstrap.Offcanvas('#lotDetails').show();
        });
}
