document.addEventListener('DOMContentLoaded', function () {
    // Guard: this used to run unconditionally on every page. Only run on
    // pages that actually have these canvases.
    if (!document.getElementById('lineChart')) return;

    const colors = {
        green: '#4CD787',
        greenFill: 'rgba(76, 215, 135, 0.15)',
        amber: '#F5A623',
        textMuted: '#8FA69B',
        grid: 'rgba(139, 166, 155, 0.12)',
        panel: '#16211C',
        text: '#E8EDE9',
    };
    const palette = ['#4CD787', '#F5A623', '#5AA9E6', '#E5484D', '#B98CE0', '#6EE7C7'];
    const dimmed = 'rgba(143, 166, 155, 0.25)';

    Chart.defaults.color = colors.textMuted;
    Chart.defaults.font.family = "'IBM Plex Mono', monospace";
    Chart.defaults.borderColor = colors.grid;

    const tooltipStyle = {
        backgroundColor: colors.panel,
        titleColor: colors.text,
        bodyColor: colors.text,
        borderColor: colors.grid,
        borderWidth: 1,
        padding: 10,
    };

    // ---- Cross-filter state --------------------------------------
    // Each chart is filtered by the OTHER two dimensions, not its own --
    // that's what lets the daily chart keep showing every day (so you can
    // click a different one) while still narrowing to the selected
    // facility/region, and likewise for the other two charts.
    const state = { day: null, facility: null, region: null };
    let allRecords = [];
    let lineChart, facilityChart, regionChart;

    function matches(r, filters) {
        if (filters.day && r.period !== filters.day) return false;
        if (filters.region && r.region !== filters.region) return false;
        if (filters.facility && r.facilityName !== filters.facility) return false;
        return true;
    }

    function formatDay(iso) {
        return new Date(iso).toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    function toggle(dim, value) {
        state[dim] = state[dim] === value ? null : value;
        render();
    }

    function aggregateByDay(records) {
        const map = new Map();
        for (const r of records) map.set(r.period, (map.get(r.period) || 0) + r.outage);
        return [...map.entries()].sort((a, b) => a[0].localeCompare(b[0]));
    }

    function aggregateBy(records, key, limit) {
        const map = new Map();
        for (const r of records) map.set(r[key], (map.get(r[key]) || 0) + r.outage);
        const sorted = [...map.entries()].sort((a, b) => b[1] - a[1]);
        return limit ? sorted.slice(0, limit) : sorted;
    }

    function renderFilterBar() {
        const container = document.getElementById('chart-filters');
        const chips = [];
        if (state.day) chips.push({ dim: 'day', label: `Day: ${formatDay(state.day)}` });
        if (state.facility) chips.push({ dim: 'facility', label: `Facility: ${state.facility}` });
        if (state.region) chips.push({ dim: 'region', label: `Region: ${state.region}` });

        if (chips.length === 0) {
            container.innerHTML = '<span class="filter-label">No filters applied &mdash; click a chart to drill in</span>';
            return;
        }

        container.innerHTML = chips.map(c =>
            `<span class="filter-chip" data-dim="${c.dim}">${c.label}<button type="button" aria-label="Clear ${c.dim} filter">&times;</button></span>`
        ).join('') + '<button type="button" class="filter-clear-all">Clear all</button>';

        container.querySelectorAll('.filter-chip button').forEach(btn => {
            btn.addEventListener('click', () => {
                state[btn.closest('.filter-chip').dataset.dim] = null;
                render();
            });
        });
        const clearAll = container.querySelector('.filter-clear-all');
        if (clearAll) {
            clearAll.addEventListener('click', () => {
                state.day = null; state.facility = null; state.region = null;
                render();
            });
        }
    }

    function pointerCursor(evt, elements) {
        evt.native.target.style.cursor = elements.length ? 'pointer' : 'default';
    }

    function render() {
        renderFilterBar();

        const dayRecords = allRecords.filter(r => matches(r, { region: state.region, facility: state.facility }));
        const facilityRecords = allRecords.filter(r => matches(r, { day: state.day, region: state.region }));
        const regionRecords = allRecords.filter(r => matches(r, { day: state.day, facility: state.facility }));

        renderLineChart(dayRecords);
        renderFacilityChart(facilityRecords);
        renderRegionChart(regionRecords);
    }

    function renderLineChart(records) {
        const daily = aggregateByDay(records);
        if (lineChart) lineChart.destroy();

        if (daily.length === 0) {
            emptyState('lineChart', 'No data for this selection.');
            return;
        }

        const labels = daily.map(d => d[0]);
        const values = daily.map(d => d[1]);
        const pointColors = labels.map(l => l === state.day ? colors.amber : colors.green);
        const pointRadii = labels.map(l => l === state.day ? 7 : 3);

        lineChart = new Chart(canvasCtx('lineChart'), {
            type: 'line',
            data: {
                labels: labels.map(formatDay),
                datasets: [{
                    label: 'Daily Nuclear Outage (MW)',
                    data: values,
                    borderColor: colors.green,
                    backgroundColor: colors.greenFill,
                    tension: 0.3,
                    fill: true,
                    pointBackgroundColor: pointColors,
                    pointBorderColor: pointColors,
                    pointRadius: pointRadii,
                    pointHoverRadius: 8,
                    pointHitRadius: 14,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: 'index', intersect: false },
                onHover: pointerCursor,
                onClick: (evt, elements) => {
                    if (!elements.length) return;
                    toggle('day', labels[elements[0].index]);
                },
                plugins: {
                    legend: { labels: { color: colors.text } },
                    tooltip: {
                        ...tooltipStyle,
                        callbacks: {
                            title: (context) => labels[context[0].dataIndex],
                            label: (context) => `${context.formattedValue} MW total outage`
                        }
                    }
                },
                scales: {
                    x: { grid: { color: colors.grid }, ticks: { color: colors.textMuted, maxRotation: 0, autoSkip: true, autoSkipPadding: 16 } },
                    y: { beginAtZero: true, grid: { color: colors.grid }, ticks: { color: colors.textMuted } }
                }
            }
        });
    }

    function renderFacilityChart(records) {
        const top = aggregateBy(records, 'facilityName', 10);
        if (facilityChart) facilityChart.destroy();

        if (top.length === 0) {
            emptyState('facilityChart', 'No data for this selection.');
            return;
        }

        const labels = top.map(t => t[0]);
        const values = top.map(t => t[1]);
        const backgrounds = labels.map(l =>
            !state.facility || l === state.facility ? colors.amber : dimmed
        );

        facilityChart = new Chart(canvasCtx('facilityChart'), {
            type: 'bar',
            data: { labels, datasets: [{ label: 'Total Outage (MW)', data: values, backgroundColor: backgrounds, borderRadius: 3 }] },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                onHover: pointerCursor,
                onClick: (evt, elements) => {
                    if (!elements.length) return;
                    toggle('facility', labels[elements[0].index]);
                },
                plugins: {
                    legend: { display: false },
                    tooltip: { ...tooltipStyle, callbacks: { label: (c) => `${c.formattedValue} MW` } }
                },
                scales: {
                    x: { beginAtZero: true, grid: { color: colors.grid }, ticks: { color: colors.textMuted } },
                    y: { grid: { display: false }, ticks: { color: colors.text } }
                }
            }
        });
    }

    function renderRegionChart(records) {
        const totals = aggregateBy(records, 'region', null);
        if (regionChart) regionChart.destroy();

        if (totals.length === 0) {
            emptyState('regionChart', 'No data for this selection.');
            return;
        }

        const labels = totals.map(t => t[0]);
        const values = totals.map(t => t[1]);
        const backgrounds = labels.map((l, i) =>
            !state.region || l === state.region ? palette[i % palette.length] : dimmed
        );

        regionChart = new Chart(canvasCtx('regionChart'), {
            type: 'bar',
            data: { labels, datasets: [{ label: 'Total Outage (MW)', data: values, backgroundColor: backgrounds, borderRadius: 3 }] },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                onHover: pointerCursor,
                onClick: (evt, elements) => {
                    if (!elements.length) return;
                    toggle('region', labels[elements[0].index]);
                },
                plugins: {
                    legend: { display: false },
                    tooltip: { ...tooltipStyle, callbacks: { label: (c) => `${c.formattedValue} MW` } }
                },
                scales: {
                    x: { beginAtZero: true, grid: { color: colors.grid }, ticks: { color: colors.textMuted } },
                    y: { grid: { display: false }, ticks: { color: colors.text } }
                }
            }
        });
    }

    function canvasCtx(id) {
        // Chart.js destroy() leaves the <canvas> in place, so re-fetching
        // the context each render is safe and simple.
        return document.getElementById(id).getContext('2d');
    }

    function emptyState(canvasId, message) {
        const canvas = document.getElementById(canvasId);
        const wrap = canvas.closest('.chart-canvas-wrap');
        wrap.innerHTML = `<canvas id="${canvasId}"></canvas><p style="color: var(--text-muted); text-align: center; padding-top: 2rem;">${message}</p>`;
    }

    fetch('/Home/GetChartData')
        .then(response => response.json())
        .then(data => {
            allRecords = data.records;

            if (allRecords.length === 0) {
                ['lineChart', 'facilityChart', 'regionChart'].forEach(id => emptyState(id, 'No outage data in the last 30 days yet.'));
                document.getElementById('chart-filters').innerHTML = '';
                return;
            }

            render();
        })
        .catch(error => {
            console.error('Error fetching chart data:', error);
        });
});
