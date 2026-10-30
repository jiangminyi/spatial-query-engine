namespace SpatialQuery;

public readonly record struct Point2
{
    public double X { get; }
    public double Y { get; }
    public Point2(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x), "Coordinates must be finite.");
        X = x; Y = y;
    }
}

public readonly record struct Rectangle2
{
    public Point2 Min { get; }
    public Point2 Max { get; }
    public Rectangle2(Point2 min, Point2 max)
    {
        if (min.X > max.X || min.Y > max.Y) throw new ArgumentException("Rectangle bounds are reversed.");
        Min = min; Max = max;
    }
    public bool Contains(Point2 point) => point.X >= Min.X && point.X <= Max.X && point.Y >= Min.Y && point.Y <= Max.Y;
}

public sealed record SpatialItem<T>(long Id, Point2 Position, T Value);
public sealed record QueryResult<T>(IReadOnlyList<SpatialItem<T>> Items, int CandidatesExamined, int CellsVisited, bool UsedFullScan);

/// <summary>A mutable, single-threaded sparse uniform grid with exact point filtering.</summary>
public sealed class SpatialIndex<T>
{
    // A finite cell domain prevents integer conversion and query-area overflow.
    public const int MaxCellCoordinate = 1_000_000_000;
    private readonly record struct Cell(int X, int Y);
    private readonly Dictionary<Cell, HashSet<long>> _cells = [];
    private readonly Dictionary<long, SpatialItem<T>> _items = [];
    public double CellSize { get; }
    public int MaxQueryCells { get; }
    public int Count => _items.Count;
    public int OccupiedCellCount => _cells.Count;

    public SpatialIndex(double cellSize, int maxQueryCells = 4096)
    {
        if (!double.IsFinite(cellSize) || cellSize <= 0) throw new ArgumentOutOfRangeException(nameof(cellSize));
        if (maxQueryCells <= 0) throw new ArgumentOutOfRangeException(nameof(maxQueryCells));
        CellSize = cellSize;
        MaxQueryCells = maxQueryCells;
    }

    public void Add(long id, Point2 position, T value)
    {
        var cell = CellFor(position);
        if (_items.ContainsKey(id)) throw new ArgumentException("Duplicate ID.", nameof(id));
        _items.Add(id, new SpatialItem<T>(id, position, value));
        AddToCell(cell, id);
    }

    /// <summary>Validates the new position before removing the previous membership.</summary>
    public void Update(long id, Point2 position, T value)
    {
        if (!_items.TryGetValue(id, out var old)) throw new KeyNotFoundException("Unknown ID: " + id);
        var nextCell = CellFor(position);
        var previousCell = CellFor(old.Position);
        if (nextCell != previousCell)
        {
            RemoveFromCell(previousCell, id);
            AddToCell(nextCell, id);
        }
        _items[id] = new SpatialItem<T>(id, position, value);
    }

    public bool Remove(long id)
    {
        if (!_items.Remove(id, out var item)) return false;
        RemoveFromCell(CellFor(item.Position), id);
        return true;
    }

    public QueryResult<T> QueryRectangle(Rectangle2 rectangle) => Query(
        rectangle.Min.X, rectangle.Min.Y, rectangle.Max.X, rectangle.Max.Y, rectangle.Contains);

    public QueryResult<T> QueryRadius(Point2 center, double radius)
    {
        if (!double.IsFinite(radius) || radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
        // Bounds may overflow to infinity; Query clamps them before converting to cell coordinates.
        return Query(Math.BitDecrement(center.X - radius), Math.BitDecrement(center.Y - radius),
            Math.BitIncrement(center.X + radius), Math.BitIncrement(center.Y + radius),
            point => WithinRadius(point, center, radius));
    }

    private QueryResult<T> Query(double minX, double minY, double maxX, double maxY, Func<Point2, bool> predicate)
    {
        var firstX = QueryCell(minX); var firstY = QueryCell(minY);
        var lastX = QueryCell(maxX); var lastY = QueryCell(maxY);
        var area = ((long)lastX - firstX + 1) * ((long)lastY - firstY + 1);
        // A huge/sparse window must not allocate or visit billions of empty buckets.
        var fullScan = area > MaxQueryCells || area > Math.Max(1, _items.Count);
        var found = new List<SpatialItem<T>>();
        var candidates = 0; var visited = 0;
        if (fullScan)
        {
            foreach (var item in _items.Values) { candidates++; if (predicate(item.Position)) found.Add(item); }
        }
        else
        {
            for (var x = firstX; x <= lastX; x++)
                for (var y = firstY; y <= lastY; y++)
                {
                    visited++;
                    if (!_cells.TryGetValue(new Cell(x, y), out var ids)) continue;
                    foreach (var id in ids)
                    {
                        var item = _items[id]; candidates++;
                        if (predicate(item.Position)) found.Add(item);
                    }
                }
        }
        found.Sort((a, b) => a.Id.CompareTo(b.Id));
        return new QueryResult<T>(found.AsReadOnly(), candidates, visited, fullScan);
    }

    private static bool WithinRadius(Point2 point, Point2 center, double radius)
    {
        var dx = Math.Abs(point.X - center.X); var dy = Math.Abs(point.Y - center.Y);
        if (dx > radius || dy > radius) return false;
        if (radius == 0) return dx == 0 && dy == 0;
        // Scaling by a power of two avoids the boundary rounding introduced by dividing
        // each component by the radius, while keeping every square in a safe range.
        var exponent = Math.ILogB(radius);
        dx = Math.ScaleB(dx, -exponent); dy = Math.ScaleB(dy, -exponent);
        var scaledRadius = Math.ScaleB(radius, -exponent);
        return dx * dx + dy * dy <= scaledRadius * scaledRadius;
    }

    private Cell CellFor(Point2 point) => new(StoredCoordinate(point.X), StoredCoordinate(point.Y));

    private int StoredCoordinate(double coordinate)
    {
        var cell = Math.Floor(coordinate / CellSize);
        if (!double.IsFinite(cell) || cell < -MaxCellCoordinate || cell > MaxCellCoordinate)
            throw new ArgumentOutOfRangeException(nameof(coordinate), "Point lies outside the supported cell domain.");
        return (int)cell;
    }

    private int QueryCell(double coordinate) => (int)Math.Clamp(Math.Floor(coordinate / CellSize), -MaxCellCoordinate, MaxCellCoordinate);

    private void AddToCell(Cell cell, long id)
    {
        if (!_cells.TryGetValue(cell, out var ids)) _cells.Add(cell, ids = []);
        ids.Add(id);
    }

    private void RemoveFromCell(Cell cell, long id)
    {
        var ids = _cells[cell]; ids.Remove(id);
        if (ids.Count == 0) _cells.Remove(cell);
    }
}
