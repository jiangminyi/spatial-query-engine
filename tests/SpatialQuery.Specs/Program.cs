using SpatialQuery;

var specs = new (string Name, Action Run)[]
{
    ("empty query", () => Equal(0, new SpatialIndex<int>(1).QueryRadius(new Point2(0, 0), 1).Items.Count)),
    ("inclusive rectangle edges", () => { var g = Grid(); Equal("1,2,3", Ids(g.QueryRectangle(Box(-1, -1, 1, 1)))); }),
    ("negative cells use floor", () => { var g = new SpatialIndex<int>(1); g.Add(1, new Point2(-0.1, -0.1), 0); g.Add(2, new Point2(0.1, 0.1), 0); Equal("1", Ids(g.QueryRectangle(Box(-1, -1, -0.001, -0.001)))); Equal(2, g.OccupiedCellCount); }),
    ("zero-radius query", () => { var g = Grid(); Equal("2", Ids(g.QueryRadius(new Point2(0, 0), 0))); }),
    ("circle filters square corners", () => { var g = Grid(); Equal("2", Ids(g.QueryRadius(new Point2(0, 0), 1))); }),
    ("circle includes exact boundary", () => { var g = new SpatialIndex<int>(1); g.Add(1, new Point2(3, 4), 0); Equal("1", Ids(g.QueryRadius(new Point2(0, 0), 5))); }),
    ("Pythagorean boundaries survive numerical scaling", () => { foreach (var (x, y, radius) in new[] { (5.0, 12.0, 13.0), (8.0, 15.0, 17.0), (7.0, 24.0, 25.0), (20.0, 21.0, 29.0) }) { var g = new SpatialIndex<int>(1); g.Add(1, new Point2(x, y), 0); Equal("1", Ids(g.QueryRadius(new Point2(0, 0), radius))); } }),
    ("duplicate ID does not overwrite", () => { var g = Grid(); Throws<ArgumentException>(() => g.Add(2, new Point2(9, 9), 90)); Equal("2", Ids(g.QueryRadius(new Point2(0, 0), 0))); Equal(3, g.Count); }),
    ("same-cell update changes payload", () => { var g = new SpatialIndex<string>(10); g.Add(1, new Point2(1, 1), "old"); g.Update(1, new Point2(2, 2), "new"); Equal("new", g.QueryRadius(new Point2(2, 2), 0).Items.Single().Value); Equal(1, g.OccupiedCellCount); }),
    ("cross-cell update removes old membership", () => { var g = Grid(); g.Update(2, new Point2(8, 9), 88); Equal(0, g.QueryRadius(new Point2(0, 0), 0).Items.Count); Equal("2", Ids(g.QueryRadius(new Point2(8, 9), 0))); }),
    ("invalid update is atomic", () => { var g = Grid(); Throws<ArgumentOutOfRangeException>(() => g.Update(2, new Point2(1e20, 0), 99)); Equal("2", Ids(g.QueryRadius(new Point2(0, 0), 0))); Equal(3, g.Count); }),
    ("remove empties buckets", () => { var g = Grid(); Equal(true, g.Remove(1)); Equal(false, g.Remove(1)); g.Remove(2); g.Remove(3); Equal(0, g.Count); Equal(0, g.OccupiedCellCount); }),
    ("huge query uses bounded full scan", () => { var g = Grid(); var r = g.QueryRectangle(Box(-1e300, -1e300, 1e300, 1e300)); Equal(3, r.Items.Count); Equal(true, r.UsedFullScan); Equal(0, r.CellsVisited); Equal(3, r.CandidatesExamined); }),
    ("local query reduces candidates", () => { var g = new SpatialIndex<int>(1); for (var i = 0; i < 1000; i++) g.Add(i, new Point2(i, 0), i); var r = g.QueryRectangle(Box(10, 0, 10.5, 0)); Equal("10", Ids(r)); Equal(false, r.UsedFullScan); Equal(1, r.CandidatesExamined); Equal(1, r.CellsVisited); }),
    ("snapshot result survives later mutations", () => { var g = Grid(); var r = g.QueryRectangle(Box(-10, -10, 10, 10)); g.Remove(1); g.Update(2, new Point2(9, 9), 90); Equal("1,2,3", Ids(r)); Equal(new Point2(0, 0), r.Items[1].Position); }),
    ("stable ID order", () => { var g = new SpatialIndex<int>(10); foreach (var id in new long[] { long.MaxValue, -5, long.MinValue, 0 }) g.Add(id, new Point2(0, 0), 0); Equal($"{long.MinValue},-5,0,{long.MaxValue}", Ids(g.QueryRadius(new Point2(0, 0), 0))); }),
    ("non-finite and invalid arguments rejected", () => { Throws<ArgumentOutOfRangeException>(() => new Point2(double.NaN, 0)); Throws<ArgumentOutOfRangeException>(() => new Point2(0, double.PositiveInfinity)); Throws<ArgumentOutOfRangeException>(() => new SpatialIndex<int>(0)); Throws<ArgumentOutOfRangeException>(() => new SpatialIndex<int>(1, 0)); Throws<ArgumentException>(() => Box(1, 0, -1, 0)); var g = Grid(); Throws<ArgumentOutOfRangeException>(() => g.QueryRadius(new Point2(0, 0), -1)); Throws<ArgumentOutOfRangeException>(() => g.QueryRadius(new Point2(0, 0), double.NaN)); Throws<KeyNotFoundException>(() => g.Update(99, new Point2(0, 0), 0)); }),
    ("scaled radius avoids squared-distance overflow", () => { var g = new SpatialIndex<int>(1e200); g.Add(1, new Point2(3e200, 4e200), 0); g.Add(2, new Point2(5e200, 5e200), 0); Equal("1", Ids(g.QueryRadius(new Point2(0, 0), 5e200))); }),
    ("overflowing bounds retain exact filter", () => { var g = new SpatialIndex<int>(1e300); g.Add(1, new Point2(1e308, 0), 0); g.Add(2, new Point2(-1e308, 0), 0); Equal("1", Ids(g.QueryRadius(new Point2(1e308, 0), 1e308))); }),
    ("cell coordinate endpoints are supported", () => { var g = new SpatialIndex<int>(1); g.Add(1, new Point2(SpatialIndex<int>.MaxCellCoordinate, 0), 0); g.Add(2, new Point2(-SpatialIndex<int>.MaxCellCoordinate, 0), 0); Equal("1", Ids(g.QueryRadius(new Point2(SpatialIndex<int>.MaxCellCoordinate, 0), 0))); Throws<ArgumentOutOfRangeException>(() => g.Add(3, new Point2(-SpatialIndex<int>.MaxCellCoordinate - 0.5, 0), 0)); }),
    ("random operations agree with brute-force reference", RandomizedOracle),
};
var failures = 0;
foreach (var spec in specs)
{
    try { spec.Run(); Console.WriteLine("PASS " + spec.Name); }
    catch (Exception e) { failures++; Console.Error.WriteLine("FAIL " + spec.Name + ": " + e); }
}
Console.WriteLine($"{specs.Length - failures}/{specs.Length} specifications passed.");
return failures == 0 ? 0 : 1;

static SpatialIndex<int> Grid()
{
    var g = new SpatialIndex<int>(1); g.Add(1, new Point2(-1, -1), 10); g.Add(2, new Point2(0, 0), 20); g.Add(3, new Point2(1, 1), 30); return g;
}
static Rectangle2 Box(double x1, double y1, double x2, double y2) => new(new Point2(x1, y1), new Point2(x2, y2));
static string Ids<T>(QueryResult<T> result) => string.Join(',', result.Items.Select(i => i.Id));
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}"); }
static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }

static void RandomizedOracle()
{
    var random = new Random(81173);
    foreach (var cellSize in new[] { 0.25, 1.0, 7.0, 64.0 })
    {
        var g = new SpatialIndex<int>(cellSize, 200);
        var oracle = new Dictionary<long, (Point2 Point, int Value)>();
        for (var step = 0; step < 2500; step++)
        {
            var id = random.Next(200);
            var p = new Point2(random.Next(-100, 101) / 2.0, random.Next(-100, 101) / 2.0);
            if (random.Next(4) == 0) { Equal(oracle.Remove(id), g.Remove(id)); }
            else
            {
                if (oracle.ContainsKey(id)) g.Update(id, p, step); else g.Add(id, p, step);
                oracle[id] = (p, step);
            }
            Equal(oracle.Count, g.Count);
            var radius = random.Next(0, 60) / 2.0;
            // Small exact half-integer coordinates let the independent oracle use squared distance safely.
            var expectedCircle = oracle.Where(pair => Math.Pow(pair.Value.Point.X - p.X, 2) + Math.Pow(pair.Value.Point.Y - p.Y, 2) <= radius * radius).Select(pair => pair.Key).Order().ToArray();
            Equal(string.Join(',', expectedCircle), Ids(g.QueryRadius(p, radius)));
            var box = Box(p.X - radius, p.Y - radius, p.X + radius, p.Y + radius);
            var expectedBox = oracle.Where(pair => pair.Value.Point.X >= box.Min.X && pair.Value.Point.X <= box.Max.X && pair.Value.Point.Y >= box.Min.Y && pair.Value.Point.Y <= box.Max.Y).Select(pair => pair.Key).Order().ToArray();
            var actual = g.QueryRectangle(box);
            Equal(string.Join(',', expectedBox), Ids(actual));
            foreach (var item in actual.Items) Equal(oracle[item.Id].Value, item.Value);
        }
    }
}
