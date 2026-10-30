using SpatialQuery;

var index = new SpatialIndex<string>(10);
for (var x = 0; x < 100; x++)
    for (var y = 0; y < 100; y++)
        index.Add(x * 100 + y, new Point2(x * 5, y * 5), $"unit({x},{y})");
var local = index.QueryRadius(new Point2(100, 100), 12);
Console.WriteLine($"Radius query: {local.Items.Count} matches; {local.CandidatesExamined}/{index.Count} candidate checks; cells={local.CellsVisited}");
index.Update(2020, new Point2(-20, -20), "moved-unit");
Console.WriteLine("Moved point: " + index.QueryRadius(new Point2(-20, -20), 0).Items.Single().Value);
var global = index.QueryRectangle(new Rectangle2(new Point2(-1e20, -1e20), new Point2(1e20, 1e20)));
Console.WriteLine($"Global query: {global.Items.Count} matches; full scan={global.UsedFullScan}; empty-cell walk avoided.");
