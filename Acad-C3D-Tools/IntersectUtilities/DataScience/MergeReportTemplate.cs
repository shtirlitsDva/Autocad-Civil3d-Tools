namespace IntersectUtilities.DataScience
{
    internal static class MergeReportTemplate
    {
        public const string Template = @"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>DSMERGE Report</title>
    <style>
{{ Styles }}
    </style>
</head>
<body>
    <h1>DSMERGE Report</h1>
    <p class=""timestamp"">Generated: {{ GeneratedAt | date: '%Y-%m-%d %H:%M:%S' }}</p>

    <!-- Summary Cards -->
    <div class=""summary-grid"">
{% for card in SummaryCards %}
        <div class=""summary-card{% if card.CssClass != '' %} {{ card.CssClass }}{% endif %}"">
            <div class=""value"">{{ card.Value }}</div>
            <div class=""label"">{{ card.Label | escape }}</div>
        </div>
{% endfor %}
    </div>

    <!-- Match Stage Breakdown -->
    <h2>Match Stage Breakdown</h2>
    <table class=""data-table"" style=""max-width: 400px;"">
        <tr><th>Stage</th><th>Count</th></tr>
{% for stage in MatchStages %}
        <tr>
            <td><span class=""stage-badge {{ stage.CssClass }}"">{{ stage.Stage | escape }}</span></td>
            <td>{{ stage.Count }}</td>
        </tr>
{% endfor %}
    </table>

    <!-- Panels -->
{% for sect in Sections %}
    <div class=""panel{% if sect.AutoOpen %} open{% endif %}"">
        <div class=""panel-header"">
            <h3>{{ sect.Title | escape }}</h3>
            <span class=""badge {{ sect.BadgeClass }}"">{{ sect.Count }}</span>
        </div>
        <div class=""panel-content"">
{% if sect.Groups.size == 0 %}
            <p class=""no-data"">{{ sect.EmptyMessage }}</p>
{% else %}
{% for group in sect.Groups %}
        <div class=""group-container"">
{% if group.Header != '' %}
            <div class=""group-header"">{{ group.Header }}</div>
{% endif %}
            <div class=""group-content"">
{% if group.Description != '' %}
                <p>{{ group.Description }}</p>
{% endif %}
{% for table in group.Tables %}
{% if table.Label != '' %}
                <div class=""sub-table-label"">{{ table.Label }}</div>
{% endif %}
                <div class=""table-scroll"">
                    <table class=""data-table"">
                        <tr>
{% for col in table.Columns %}
                            <th>{{ col | escape }}</th>
{% endfor %}
                        </tr>
{% for row in table.Rows %}
                        <tr>
{% for cell in row %}
                            <td title=""{{ cell.FullValue | escape }}"">
{% if cell.IsBadge %}
                                <span class=""stage-badge {{ cell.CssClass }}"">{{ cell.Display | escape }}</span>
{% else %}
                                {{ cell.Display | escape }}
{% endif %}
                            </td>
{% endfor %}
                        </tr>
{% endfor %}
                    </table>
                </div>
{% endfor %}
            </div>
        </div>
{% endfor %}
{% endif %}
        </div>
    </div>
{% endfor %}

    <script>
    document.querySelectorAll('.panel-header').forEach(header => {
        header.addEventListener('click', () => {
            header.parentElement.classList.toggle('open');
        });
    });
    </script>
</body>
</html>";
    }
}
