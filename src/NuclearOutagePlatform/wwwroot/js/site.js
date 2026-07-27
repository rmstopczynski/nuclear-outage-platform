document.addEventListener('DOMContentLoaded', function () {
    // Guard: this used to run unconditionally on every page and throw
    // (silently, into the .catch below) on any page without these
    // canvases. Only run the chart setup where the canvases actually exist.
    if (!document.getElementById('lineChart')) return;

    // Palette matches site.css's control-room theme (signal-green /
    // signal-amber / muted text), not Chart.js's defaults.
    const colors = {
        green: '#4CD787',
        greenFill: 'rgba(76, 215, 135, 0.15)',
        amber: '#F5A623',
        textMuted: '#8FA69B',
        grid: 'rgba(139, 166, 155, 0.12)',
        panel: '#16211C',
        text: '#E8EDE9',
    };
    const palette = [
        '#4CD787', '#F5A623', '#5AA9E6', '#E5484D', '#B98CE0',
        '#6EE7C7', '#F2C94C', '#8FA69B', '#E07A5F', '#3D9970',
    ];

    Chart.defaults.color = colors.textMuted;
    Chart.defaults.font.family = "'IBM Plex Mono', monospace";
    Chart.defaults.borderColor = colors.grid;

    fetch('/Home/GetChartData')
        .then(response => response.json())
        .then(data => {
            const formattedLabels = data.dailyOutages.labels.map(dateStr => {
                const date = new Date(dateStr);
                return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
            });

            new Chart(document.getElementById('lineChart').getContext('2d'), {
                type: 'line',
                data: {
                    labels: formattedLabels,
                    datasets: [{
                        label: 'Daily Nuclear Outage (MW)',
                        data: data.dailyOutages.values,
                        borderColor: colors.green,
                        backgroundColor: colors.greenFill,
                        tension: 0.3,
                        fill: true,
                        pointBackgroundColor: colors.green,
                        pointRadius: 2,
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: { labels: { color: colors.text } },
                        tooltip: {
                            backgroundColor: colors.panel,
                            titleColor: colors.text,
                            bodyColor: colors.text,
                            borderColor: colors.grid,
                            borderWidth: 1,
                            callbacks: {
                                title: (context) => data.dailyOutages.labels[context[0].dataIndex]
                            }
                        }
                    },
                    scales: {
                        x: { grid: { color: colors.grid }, ticks: { color: colors.textMuted } },
                        y: { beginAtZero: true, grid: { color: colors.grid }, ticks: { color: colors.textMuted } }
                    }
                }
            });

            new Chart(document.getElementById('barChart').getContext('2d'), {
                type: 'bar',
                data: {
                    labels: data.generatorOutages.labels,
                    datasets: [{
                        label: 'Total Outage by Generator (MW)',
                        data: data.generatorOutages.values,
                        backgroundColor: colors.amber,
                        borderRadius: 3,
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: { labels: { color: colors.text } },
                        tooltip: {
                            backgroundColor: colors.panel,
                            titleColor: colors.text,
                            bodyColor: colors.text,
                            borderColor: colors.grid,
                            borderWidth: 1,
                            callbacks: {
                                label: (context) => {
                                    const value = context.raw;
                                    const total = context.chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    const percent = ((value / total) * 100).toFixed(1);
                                    return `${context.label}: ${value} MW (${percent}%)`;
                                }
                            }
                        }
                    },
                    scales: {
                        x: {
                            grid: { display: false },
                            ticks: {
                                color: colors.textMuted,
                                callback: function (value) {
                                    const label = this.getLabelForValue(value);
                                    return label.length > 15 ? label.slice(0, 12) + '...' : label;
                                }
                            }
                        },
                        y: { beginAtZero: true, grid: { color: colors.grid }, ticks: { color: colors.textMuted } }
                    }
                }
            });

            new Chart(document.getElementById('pieChart').getContext('2d'), {
                type: 'pie',
                data: {
                    labels: data.generatorFrequency.labels,
                    datasets: [{
                        data: data.generatorFrequency.values,
                        backgroundColor: palette,
                        borderColor: colors.panel,
                        borderWidth: 2,
                    }]
                },
                options: {
                    responsive: true,
                    plugins: {
                        legend: { position: 'right', labels: { color: colors.text, boxWidth: 12 } },
                        tooltip: {
                            backgroundColor: colors.panel,
                            titleColor: colors.text,
                            bodyColor: colors.text,
                            borderColor: colors.grid,
                            borderWidth: 1,
                            callbacks: {
                                label: function (context) {
                                    const value = context.raw;
                                    const total = context.chart.data.datasets[0].data.reduce((a, b) => a + b, 0);
                                    const percent = ((value / total) * 100).toFixed(1);
                                    return `${context.label}: ${value} (${percent}%)`;
                                }
                            }
                        }
                    }
                }
            });
        })
        .catch(error => {
            console.error('Error fetching chart data:', error);
        });
});
