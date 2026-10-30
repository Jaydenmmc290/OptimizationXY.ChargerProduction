using OptimizationXY.Charger.Models.Defects;

namespace OptimizationXY.Charger.Services.Defects;

/// <summary>
/// Groups neighbouring low-radius defect points into connected "patches"
/// (regions of missing wood) using a flood fill over the Z/angle grid.
/// </summary>
public static class RadiusDefectPatchBuilder
{

    /// <summary>
    /// Builds a list of RadiusDefectPatch objects from a given RadiusDefectMap, grouping connected defect points into
    /// patches based on proximity in Z and angle, as well as defect type.
    /// </summary>
    /// <param name="map">The radius defect map to build patches from.</param>
    /// <param name="zNeighborToleranceMM">The tolerance for considering points as neighbors in the Z direction, in millimeters.</param>
    /// <param name="angleNeighborToleranceDeg">The tolerance for considering points as neighbors in the angle direction, in degrees.</param>
    /// <param name="minCellsPerPatch">The minimum number of cells required to form a patch.</param>
    /// <returns>A list of RadiusDefectPatch objects representing the identified patches.</returns>
    public static List<RadiusDefectPatch> Build(RadiusDefectMap map,double zNeighborToleranceMM = 10.0,double angleNeighborToleranceDeg = 5.0,int minCellsPerPatch = 2)
    {
        if (map == null || map.Points == null || map.Points.Count == 0)
            return [];

        // Only missing-wood points (LowRadius/SevereLowRadius) can form patches;
        // Good and HighRadius points are ignored.
        var candidates = map.Points
            .Where(p =>
                p.DefectType == RadiusDefectType.LowRadius ||
                p.DefectType == RadiusDefectType.SevereLowRadius)
            .ToList();

        if (candidates.Count == 0)
            return [];

        var visited = new HashSet<RadiusDefectPoint>();
        var patches = new List<RadiusDefectPatch>();

        int nextId = 1;

        // Start a flood fill from every point not yet assigned to a patch. Each
        // fill collects one connected region. Regions smaller than
        // minCellsPerPatch (isolated single cells) are dropped as noise.
        foreach (var seed in candidates)
        {
            if (visited.Contains(seed))
                continue;

            var patchPoints = FloodFill(
                seed,
                candidates,
                visited,
                zNeighborToleranceMM,
                angleNeighborToleranceDeg);

            if (patchPoints.Count < minCellsPerPatch)
                continue;

            var patch = BuildPatch(
                nextId,
                patchPoints);

            patches.Add(patch);
            nextId++;
        }

        // Largest patches first; equal sizes ordered deepest first.
        return [.. patches
            .OrderByDescending(p => p.CellCount)
            .ThenBy(p => p.WorstErrorMM)];
    }

    /// <summary>
    /// Uses a breadth-first search (BFS) approach to find all connected points that belong to the same patch.
    /// </summary>
    /// <param name="seed">The starting point for the flood fill.</param>
    /// <param name="candidates">The list of candidate points to consider.</param>
    /// <param name="visited">A set of points that have already been visited.</param>
    /// <param name="zNeighborToleranceMM">The tolerance for considering points as neighbors in the Z direction, in millimeters.</param>
    /// <param name="angleNeighborToleranceDeg">The tolerance for considering points as neighbors in the angle direction, in degrees.</param>
    /// <returns>A list of connected points that form a patch.</returns>
    private static List<RadiusDefectPoint> FloodFill(RadiusDefectPoint seed,List<RadiusDefectPoint> candidates,
        HashSet<RadiusDefectPoint> visited,double zNeighborToleranceMM,double angleNeighborToleranceDeg)
    {
        var patchPoints = new List<RadiusDefectPoint>();
        var queue = new Queue<RadiusDefectPoint>();

        queue.Enqueue(seed);
        visited.Add(seed);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            patchPoints.Add(current);

            // NOTE: this scans every candidate for each point (O(n^2)). Fine for the
            // usual few hundred defect cells; a grid lookup would be faster for very
            // large defect maps.
            var neighbors = candidates
                .Where(p =>
                    !visited.Contains(p) &&
                    IsNeighbor(
                        current,
                        p,
                        zNeighborToleranceMM,
                        angleNeighborToleranceDeg))
                .ToList();

            foreach (var neighbor in neighbors)
            {
                visited.Add(neighbor);
                queue.Enqueue(neighbor);
            }
        }

        return patchPoints;
    }

    /// <summary>
    /// Determines if two defect points are considered neighbors based on their Z and angle proximity, as well as defect type.
    /// </summary>
    /// <param name="a">The first defect point.</param>
    /// <param name="b">The second defect point.</param>
    /// <param name="zNeighborToleranceMM">The tolerance for considering points as neighbors in the Z direction, in millimeters.</param>
    /// <param name="angleNeighborToleranceDeg">The tolerance for considering points as neighbors in the angle direction, in degrees.</param>
    /// <returns>True if the points are considered neighbors; otherwise, false.</returns>
    private static bool IsNeighbor(RadiusDefectPoint a,RadiusDefectPoint b,double zNeighborToleranceMM,double angleNeighborToleranceDeg)
    {
        double zDiff = Math.Abs(a.ZBinMM - b.ZBinMM);

        double angleDiff = Math.Abs(a.AngleBinDeg - b.AngleBinDeg);

        // Handles wraparound between 355 degrees and 0 degrees.
        double wrappedAngleDiff = 360.0 - angleDiff;

        double smallestAngleDiff = Math.Min(
            angleDiff,
            wrappedAngleDiff);

        bool closeInZ =
            zDiff <= zNeighborToleranceMM;

        bool closeInAngle =
            smallestAngleDiff <= angleNeighborToleranceDeg;

        bool sameDefectFamily =
            IsLowDefect(a.DefectType) &&
            IsLowDefect(b.DefectType);

        return closeInZ && closeInAngle && sameDefectFamily;
    }

    /// <summary>
    /// Determines if a defect type is considered a low radius defect for patch building purposes.
    /// </summary>
    /// <param name="type">The defect type to evaluate.</param>
    /// <returns>True if the defect type is considered a low radius defect; otherwise, false.</returns>
    private static bool IsLowDefect(RadiusDefectType type)
    {
        return type == RadiusDefectType.LowRadius ||
               type == RadiusDefectType.SevereLowRadius;
    }

    /// <summary>
    /// Builds a RadiusDefectPatch object from a list of defect points, calculating summary statistics and patch boundaries.
    /// </summary>
    /// <param name="id">The unique identifier for the patch.</param>
    /// <param name="points">The list of defect points to include in the patch.</param>
    /// <returns>The constructed RadiusDefectPatch object.</returns>
    private static RadiusDefectPatch BuildPatch(int id,List<RadiusDefectPoint> points)
    {
        double avgError =
            points.Average(p => p.RadiusErrorMM);

        double worstError =
            points.Min(p => p.RadiusErrorMM);

        var patch = new RadiusDefectPatch
        {
            Id = id,

            Points = points,

            DefectType = GetPatchDefectType(points),

            StartZMM =
                points.Min(p => p.ZBinMM),

            EndZMM =
                points.Max(p => p.ZBinMM),

            StartAngleDeg =
                points.Min(p => p.AngleBinDeg),

            EndAngleDeg =
                points.Max(p => p.AngleBinDeg),

            AverageErrorMM = avgError,

            WorstErrorMM = worstError,

            // Simple severity: number of cells x average depth.
            SeverityScore =
                points.Count * Math.Abs(avgError)
        };

        return patch;
    }

    /// <summary>
    /// Determines the overall defect type for a patch based on the most severe defect type present in its points.
    /// </summary>
    /// <param name="points">The list of defect points to evaluate.</param>
    /// <returns>The overall defect type for the patch.</returns>
    private static RadiusDefectType GetPatchDefectType(List<RadiusDefectPoint> points)
    {
        if (points.Any(p => p.DefectType == RadiusDefectType.SevereLowRadius))
            return RadiusDefectType.SevereLowRadius;

        return RadiusDefectType.LowRadius;
    }
}
