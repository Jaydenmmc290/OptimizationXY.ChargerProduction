using OptimizationXY.Charger.Models.Geometry;
using OptimizationXY.Charger.Models.Optimization.Geometry;
using OptimizationXY.Charger.Models.Optimization.Spindle;
using OptimizationXY.Charger.Models.Settings;
using OptimizationXY.Charger.Services.Optimization.Movement;

namespace OptimizationXY.Charger.Services.Optimization.Geometry;

/// <summary>
/// Geometry-only coarse-to-fine spindle search.
///
/// Cylinder mode maximizes the radius of one circular cylinder that remains
/// inside the measured log envelope for the full log length.
///
/// Oval mode maximizes the area of one centered ellipse that remains inside
/// the measured log envelope for the full log length. The ellipse is used only
/// to choose the spindle axis. A conventional rotary lathe still peels circles,
/// so final recovery is always checked with the circular virtual-peel model.
/// </summary>
public static class InscribedGeometryOptimizer
{
    /// <summary>
    /// Find the largest inscribed cylinder that fits within the measured log envelope.
    /// </summary>
    /// <param name="field">The binned radius field points representing the measured log envelope.</param>
    /// <param name="optimizationSettings">The optimization settings.</param>
    /// <param name="geometrySettings">The inscribed geometry settings.</param>
    /// <param name="machineLimits">The machine limit settings.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result of the inscribed geometry optimization. </returns>
    public static InscribedGeometryResult FindLargestCylinder(
        List<BinnedRadiusFieldPoint> field,
        OptimizationSettings optimizationSettings,
        InscribedGeometrySettings geometrySettings,
        MachineLimitSettings machineLimits,
        CancellationToken token)
    {
        return FindBest(
            field,
            optimizationSettings,
            geometrySettings,
            machineLimits,
            InscribedGeometryKind.Cylinder,
            token);
    }

    /// <summary>
    /// Find the largest inscribed oval that fits within the measured log envelope.
    /// </summary>
    /// <param name="field">The binned radius field points representing the measured log envelope.</param>
    /// <param name="optimizationSettings">The optimization settings.</param>
    /// <param name="geometrySettings">The inscribed geometry settings.</param>
    /// <param name="machineLimits">The machine limit settings.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result of the inscribed geometry optimization. </returns>
    public static InscribedGeometryResult FindLargestOval(
        List<BinnedRadiusFieldPoint> field,
        OptimizationSettings optimizationSettings,
        InscribedGeometrySettings geometrySettings,
        MachineLimitSettings machineLimits,
        CancellationToken token)
    {
        return FindBest(
            field,
            optimizationSettings,
            geometrySettings,
            machineLimits,
            InscribedGeometryKind.Oval,
            token);
    }

    /// <summary>
    /// Find the best inscribed geometry (cylinder or oval) that fits within the measured log envelope.
    /// </summary>
    /// <param name="field">The binned radius field points representing the measured log envelope.</param>
    /// <param name="optimizationSettings">The optimization settings.</param>
    /// <param name="geometrySettings">The inscribed geometry settings.</param>
    /// <param name="machineLimits">The machine limit settings.</param>
    /// <param name="kind">The kind of inscribed geometry to find (cylinder or oval).</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The result of the inscribed geometry optimization. </returns>
    /// <exception cref="InvalidOperationException">Thrown if the search does not produce a valid geometry.</exception>
    private static InscribedGeometryResult FindBest(
        List<BinnedRadiusFieldPoint> field,
        OptimizationSettings optimizationSettings,
        InscribedGeometrySettings geometrySettings,
        MachineLimitSettings machineLimits,
        InscribedGeometryKind kind,
        CancellationToken token)
    {
        ValidateInputs(
            field,
            optimizationSettings,
            geometrySettings,
            machineLimits);

        SurfaceSample[] samples = BuildSamples(field);

        SearchPassResult coarse = Search(
            samples,
            kind,
            geometrySettings,
            machineLimits,
            centerOffsetXMM: 0.0,
            centerOffsetYMM: 0.0,
            centerSweepXMM: 0.0,
            centerSweepYMM: 0.0,
            radiusMM: optimizationSettings.CoarseMaxMoveMM,
            stepMM: optimizationSettings.CoarseStepMM,
            token);

        SearchPassResult medium = Search(
            samples,
            kind,
            geometrySettings,
            machineLimits,
            coarse.OffsetXMM,
            coarse.OffsetYMM,
            coarse.SweepXMM,
            coarse.SweepYMM,
            optimizationSettings.MediumSearchRadiusMM,
            optimizationSettings.MediumStepMM,
            token);

        SearchPassResult fine = Search(
            samples,
            kind,
            geometrySettings,
            machineLimits,
            medium.OffsetXMM,
            medium.OffsetYMM,
            medium.SweepXMM,
            medium.SweepYMM,
            optimizationSettings.FineSearchRadiusMM,
            optimizationSettings.FineStepMM,
            token);

        InscribedGeometryResult result = fine.Geometry
            ?? throw new InvalidOperationException(
                $"The {kind} search did not produce a valid geometry.");

        result.CoarseCandidates = coarse.TestedCandidates;
        result.MediumCandidates = medium.TestedCandidates;
        result.FineCandidates = fine.TestedCandidates;
        result.TestedCandidates =
            result.CoarseCandidates +
            result.MediumCandidates +
            result.FineCandidates;

        return result;
    }

    /// <summary>
    /// Perform a single search pass over the specified range of offsets and sweeps, evaluating candidates and returning the best one found.
    /// </summary>
    /// <param name="samples">The array of surface samples to evaluate.</param>
    /// <param name="kind">The kind of inscribed geometry to optimize.</param>
    /// <param name="settings">The inscribed geometry settings.</param>
    /// <param name="machineLimits">The machine limit settings.</param>
    /// <param name="centerOffsetXMM">The center offset in the X direction, in millimeters.</param>
    /// <param name="centerOffsetYMM">The center offset in the Y direction, in millimeters.</param>
    /// <param name="centerSweepXMM">The center sweep in the X direction, in millimeters.</param>
    /// <param name="centerSweepYMM">The center sweep in the Y direction, in millimeters.</param>
    /// <param name="radiusMM">The radius of the search area, in millimeters.</param>
    /// <param name="stepMM">The step size for the search, in millimeters.</param>
    /// <param name="token">The cancellation token to observe.</param>
    /// <returns>The result of the search pass.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no valid candidate is found.</exception>
    private static SearchPassResult Search(
        SurfaceSample[] samples,
        InscribedGeometryKind kind,
        InscribedGeometrySettings settings,
        MachineLimitSettings machineLimits,
        double centerOffsetXMM,
        double centerOffsetYMM,
        double centerSweepXMM,
        double centerSweepYMM,
        double radiusMM,
        double stepMM,
        CancellationToken token)
    {
        if (stepMM <= 0)
        {
            throw new InvalidOperationException(
                "Geometry optimizer step size must be greater than zero.");
        }

        Candidate? best = null;
        int tested = 0;

        foreach (double offsetX in Range(centerOffsetXMM, radiusMM, stepMM))
        {
            foreach (double offsetY in Range(centerOffsetYMM, radiusMM, stepMM))
            {
                foreach (double sweepX in Range(centerSweepXMM, radiusMM, stepMM))
                {
                    foreach (double sweepY in Range(centerSweepYMM, radiusMM, stepMM))
                    {
                        if ((tested & 255) == 0)
                            token.ThrowIfCancellationRequested();

                        Candidate? candidate = EvaluateCandidate(
                            samples,
                            kind,
                            settings,
                            machineLimits,
                            offsetX,
                            offsetY,
                            sweepX,
                            sweepY);

                        tested++;

                        if (candidate is null)
                            continue;

                        if (best is null || IsBetter(candidate, best, settings))
                            best = candidate;
                    }
                }
            }
        }

        if (best is null)
        {
            throw new InvalidOperationException(
                $"No valid {kind} candidate had sufficient angular coverage.");
        }

        return new SearchPassResult
        {
            OffsetXMM = best.OffsetXMM,
            OffsetYMM = best.OffsetYMM,
            SweepXMM = best.SweepXMM,
            SweepYMM = best.SweepYMM,
            Geometry = best.Geometry,
            TestedCandidates = tested
        };
    }

    /// <summary>
    /// Evaluate a single candidate inscribed geometry based on the provided offsets and sweeps, returning the resulting geometry if valid.
    /// </summary>
    /// <param name="samples">The array of surface samples to evaluate.</param>
    /// <param name="kind">The kind of inscribed geometry to optimize.</param>
    /// <param name="settings">The inscribed geometry settings.</param>
    /// <param name="machineLimits">The machine limit settings.</param>
    /// <param name="offsetXMM">The offset in the X direction, in millimeters.</param>
    /// <param name="offsetYMM">The offset in the Y direction, in millimeters.</param>
    /// <param name="sweepXMM">The sweep in the X direction, in millimeters.</param>
    /// <param name="sweepYMM">The sweep in the Y direction, in millimeters.</param>
    /// <returns>The resulting candidate geometry if valid; otherwise, null.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when an unsupported inscribed geometry kind is provided.</exception>
    private static Candidate? EvaluateCandidate(
        SurfaceSample[] samples,
        InscribedGeometryKind kind,
        InscribedGeometrySettings settings,
        MachineLimitSettings machineLimits,
        double offsetXMM,
        double offsetYMM,
        double sweepXMM,
        double sweepYMM)
    {
        OptimizedSpindleMovement movement =
            SpindleMovementBuilder.BuildFromSweep(
                offsetXMM,
                offsetYMM,
                sweepXMM,
                sweepYMM);

        // Machine travel is a hard feasibility constraint, not a score or
        // penalty. Geometry candidates outside physical travel are not tested.
        if (!IsWithinMachineLimits(movement, machineLimits))
            return null;

        double[] envelope = BuildAngularEnvelope(
            samples,
            movement,
            settings.AngularBins,
            out int validAngularBins);

        if (validAngularBins < settings.MinimumValidAngularBins)
            return null;

        if (!FillMissingEnvelopeConservatively(envelope))
            return null;

        InscribedGeometryResult geometry = kind switch
        {
            InscribedGeometryKind.Cylinder =>
                EvaluateCylinder(
                    envelope,
                    movement,
                    validAngularBins,
                    settings),

            InscribedGeometryKind.Oval =>
                EvaluateOval(
                    envelope,
                    movement,
                    validAngularBins,
                    settings),

            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unsupported inscribed geometry kind.")
        };

        return new Candidate
        {
            OffsetXMM = offsetXMM,
            OffsetYMM = offsetYMM,
            SweepXMM = sweepXMM,
            SweepYMM = sweepYMM,
            Geometry = geometry
        };
    }

    /// <summary>
    /// Evaluate the inscribed cylinder geometry based on the provided envelope and spindle movement, returning the resulting geometry.
    /// </summary>
    /// <param name="envelope">The array of envelope values representing the available radius at each angular bin.</param>
    /// <param name="movement">The optimized spindle movement.</param>
    /// <param name="validAngularBins">The number of valid angular bins.</param>
    /// <param name="settings">The inscribed geometry settings.</param>
    /// <returns>The resulting inscribed geometry result.</returns>
    private static InscribedGeometryResult EvaluateCylinder(
        double[] envelope,
        OptimizedSpindleMovement movement,
        int validAngularBins,
        InscribedGeometrySettings settings)
    {
        double radiusMM =
            envelope.Min() - settings.SafetyMarginMM;

        radiusMM = Math.Max(0.0, radiusMM);

        return new InscribedGeometryResult
        {
            Kind = InscribedGeometryKind.Cylinder,
            SpindleMovement = movement,
            GuaranteedCircularRadiusMM = radiusMM,
            MajorSemiAxisMM = radiusMM,
            MinorSemiAxisMM = radiusMM,
            OrientationDeg = 0.0,
            AreaMM2 = Math.PI * radiusMM * radiusMM,
            ValidAngularBins = validAngularBins,
            MaxSpindleMovementMM = MaxSpindleMovementMM(movement)
        };
    }

    /// <summary>
    /// Evaluate the inscribed oval geometry based on the provided envelope and spindle movement, returning the resulting geometry.
    /// </summary>
    /// <param name="envelope">The array of envelope values representing the available radius at each angular bin.</param>
    /// <param name="movement">The optimized spindle movement.</param>
    /// <param name="validAngularBins">The number of valid angular bins.</param>
    /// <param name="settings">The inscribed geometry settings.</param>
    /// <returns>The resulting inscribed geometry result.</returns>
    private static InscribedGeometryResult EvaluateOval(
        double[] envelope,
        OptimizedSpindleMovement movement,
        int validAngularBins,
        InscribedGeometrySettings settings)
    {
        double bestArea = double.NegativeInfinity;
        double bestMajor = 0.0;
        double bestMinor = 0.0;
        double bestOrientation = 0.0;

        double minRatio = Math.Clamp(
            settings.MinimumAxisRatio,
            0.05,
            1.0);

        for (double ratio = minRatio;
             ratio <= 1.000001;
             ratio += settings.AxisRatioStep)
        {
            double clampedRatio = Math.Min(1.0, ratio);

            for (double orientationDeg = 0.0;
                 orientationDeg < 180.0;
                 orientationDeg += settings.OrientationStepDeg)
            {
                double majorLimitMM = double.PositiveInfinity;
                double orientationRad =
                    orientationDeg * Math.PI / 180.0;

                for (int angleIndex = 0;
                     angleIndex < envelope.Length;
                     angleIndex++)
                {
                    double availableRadiusMM =
                        envelope[angleIndex] -
                        settings.SafetyMarginMM;

                    if (availableRadiusMM <= 0)
                    {
                        majorLimitMM = 0.0;
                        break;
                    }

                    double angleRad =
                        (angleIndex + 0.5) *
                        2.0 * Math.PI /
                        envelope.Length;

                    double delta = angleRad - orientationRad;
                    double cos = Math.Cos(delta);
                    double sin = Math.Sin(delta);

                    // Radial distance of an ellipse with major semi-axis A
                    // and minor semi-axis ratio*A is A*unitRadialFactor.
                    double denominator = Math.Sqrt(
                        clampedRatio * clampedRatio * cos * cos +
                        sin * sin);

                    double unitRadialFactor =
                        denominator > 0.0000001
                            ? clampedRatio / denominator
                            : 1.0;

                    double candidateMajorLimit =
                        availableRadiusMM / unitRadialFactor;

                    if (candidateMajorLimit < majorLimitMM)
                        majorLimitMM = candidateMajorLimit;
                }

                double majorMM = Math.Max(0.0, majorLimitMM);
                double minorMM = majorMM * clampedRatio;
                double areaMM2 = Math.PI * majorMM * minorMM;

                bool better =
                    areaMM2 > bestArea + 0.000001 ||
                    (Math.Abs(areaMM2 - bestArea) <= 0.000001 &&
                     minorMM > bestMinor);

                if (better)
                {
                    bestArea = areaMM2;
                    bestMajor = majorMM;
                    bestMinor = minorMM;
                    bestOrientation = orientationDeg;
                }
            }
        }

        return new InscribedGeometryResult
        {
            Kind = InscribedGeometryKind.Oval,
            SpindleMovement = movement,
            GuaranteedCircularRadiusMM = bestMinor,
            MajorSemiAxisMM = bestMajor,
            MinorSemiAxisMM = bestMinor,
            OrientationDeg = bestOrientation,
            AreaMM2 = Math.Max(0.0, bestArea),
            ValidAngularBins = validAngularBins,
            MaxSpindleMovementMM = MaxSpindleMovementMM(movement)
        };
    }

    /// <summary>
    /// Build an angular envelope of available radii from the provided surface samples and spindle movement,
    /// returning the envelope and the count of valid angular bins.
    /// </summary>
    /// <param name="samples">The array of surface samples.</param>
    /// <param name="movement">The optimized spindle movement.</param>
    /// <param name="angularBins">The number of angular bins.</param>
    /// <param name="validAngularBins">The number of valid angular bins.</param>
    /// <returns>The array of envelope values representing the available radius at each angular bin.</returns>
    private static double[] BuildAngularEnvelope(
        SurfaceSample[] samples,
        OptimizedSpindleMovement movement,
        int angularBins,
        out int validAngularBins)
    {
        double[] envelope =
            [.. Enumerable.Repeat(
                double.PositiveInfinity,
                angularBins)];

        foreach (SurfaceSample sample in samples)
        {
            double axisX =
                movement.TailMoveXMM +
                (movement.HeadMoveXMM -
                 movement.TailMoveXMM) *
                sample.NormalizedZ;

            double axisY =
                movement.TailMoveYMM +
                (movement.HeadMoveYMM -
                 movement.TailMoveYMM) *
                sample.NormalizedZ;

            double dx = sample.XMM - axisX;
            double dy = sample.YMM - axisY;
            double radiusMM = Math.Sqrt(dx * dx + dy * dy);

            double angleRad = Math.Atan2(dy, dx);
            if (angleRad < 0)
                angleRad += 2.0 * Math.PI;

            int angleIndex = Math.Clamp(
                (int)Math.Floor(
                    angleRad /
                    (2.0 * Math.PI) *
                    angularBins),
                0,
                angularBins - 1);

            if (radiusMM < envelope[angleIndex])
                envelope[angleIndex] = radiusMM;
        }

        validAngularBins =
            envelope.Count(double.IsFinite);

        return envelope;
    }

    /// <summary>
    /// Fill missing values in the envelope conservatively by using the nearest finite neighbors or the global
    /// minimum if no neighbors are available.
    /// </summary>
    /// <param name="envelope">The array of envelope values representing the available radius at each angular bin.</param>
    /// <returns>True if any missing values were filled; otherwise, false.</returns>
    private static bool FillMissingEnvelopeConservatively(
        double[] envelope)
    {
        int count = envelope.Length;

        if (!envelope.Any(double.IsFinite))
            return false;

        double globalMinimum =
            envelope.Where(double.IsFinite).Min();

        for (int index = 0; index < count; index++)
        {
            if (double.IsFinite(envelope[index]))
                continue;

            double previous = double.PositiveInfinity;
            double next = double.PositiveInfinity;

            for (int step = 1; step < count; step++)
            {
                int previousIndex =
                    (index - step + count) % count;

                if (double.IsFinite(envelope[previousIndex]))
                {
                    previous = envelope[previousIndex];
                    break;
                }
            }

            for (int step = 1; step < count; step++)
            {
                int nextIndex =
                    (index + step) % count;

                if (double.IsFinite(envelope[nextIndex]))
                {
                    next = envelope[nextIndex];
                    break;
                }
            }

            double fill = Math.Min(previous, next);

            envelope[index] =
                double.IsFinite(fill)
                    ? fill
                    : globalMinimum;
        }

        return true;
    }

    /// <summary>
    /// Build an array of surface samples from the provided binned radius field points, normalizing the
    /// Z values and converting polar coordinates to Cartesian coordinates.
    /// </summary>
    /// <param name="field">The list of binned radius field points to convert into surface samples.</param>
    /// <returns>An array of surface samples.</returns>
    private static SurfaceSample[] BuildSamples(
        List<BinnedRadiusFieldPoint> field)
    {
        double minZ = field.Min(point => point.ZBinMM);
        double maxZ = field.Max(point => point.ZBinMM);
        double length = Math.Max(0.001, maxZ - minZ);

        return [.. field
            .Select(point =>
            {
                // An inscribed result must not cross the measured surface.
                // Use each bin's minimum measured radius when available; only
                // fall back to the mean for legacy/sparse bins without a minimum.
                double conservativeRadiusMM =
                    point.MinRadiusMM > 0 &&
                    double.IsFinite(point.MinRadiusMM)
                        ? point.MinRadiusMM
                        : point.MeanRadiusMM;

                double angleRad =
                    point.AngleBinDeg *
                    Math.PI /
                    180.0;

                return new
                {
                    Point = point,
                    RadiusMM = conservativeRadiusMM,
                    AngleRad = angleRad
                };
            })
            .Where(item =>
                item.RadiusMM > 0 &&
                double.IsFinite(item.RadiusMM))
            .Select(item =>
                new SurfaceSample(
                    NormalizedZ:
                        Math.Clamp(
                            (item.Point.ZBinMM - minZ) / length,
                            0.0,
                            1.0),
                    XMM:
                        item.RadiusMM *
                        Math.Cos(item.AngleRad),
                    YMM:
                        item.RadiusMM *
                        Math.Sin(item.AngleRad)))];
    }

    /// <summary>
    /// Determine if the candidate geometry is better than the current best geometry based on the inscribed geometry settings.
    /// </summary>
    /// <param name="candidate">The candidate geometry to evaluate.</param>
    /// <param name="current">The current best geometry to compare against.</param>
    /// <param name="settings">The inscribed geometry settings used for comparison.</param>
    /// <returns>True if the candidate geometry is better than the current best geometry; otherwise, false.</returns>
    private static bool IsBetter(
        Candidate candidate,
        Candidate current,
        InscribedGeometrySettings settings)
    {
        InscribedGeometryResult left = candidate.Geometry;
        InscribedGeometryResult right = current.Geometry;

        if (left.Kind == InscribedGeometryKind.Cylinder)
        {
            double difference =
                left.GuaranteedCircularRadiusMM -
                right.GuaranteedCircularRadiusMM;

            if (Math.Abs(difference) >
                settings.GeometryTieToleranceMM)
            {
                return difference > 0;
            }
        }
        else
        {
            double areaDifference =
                left.AreaMM2 -
                right.AreaMM2;

            double areaTolerance =
                Math.PI *
                settings.GeometryTieToleranceMM *
                settings.GeometryTieToleranceMM;

            if (Math.Abs(areaDifference) > areaTolerance)
                return areaDifference > 0;

            double minorDifference =
                left.MinorSemiAxisMM -
                right.MinorSemiAxisMM;

            if (Math.Abs(minorDifference) >
                settings.GeometryTieToleranceMM)
            {
                return minorDifference > 0;
            }
        }

        return left.MaxSpindleMovementMM <
               right.MaxSpindleMovementMM;
    }

    /// <summary>
    /// Determine if the optimized spindle movement is within the machine limits specified in the settings.
    /// </summary>
    /// <param name="movement">The optimized spindle movement to evaluate.</param>
    /// <param name="limits">The machine limit settings to compare against.</param>
    /// <returns>True if the optimized spindle movement is within the machine limits; otherwise, false.</returns>
    private static bool IsWithinMachineLimits(
        OptimizedSpindleMovement movement,
        MachineLimitSettings limits)
    {
        return
            Math.Abs(movement.TailMoveXMM) <= limits.MaxTailMoveXMM &&
            Math.Abs(movement.TailMoveYMM) <= limits.MaxTailMoveYMM &&
            Math.Abs(movement.HeadMoveXMM) <= limits.MaxHeadMoveXMM &&
            Math.Abs(movement.HeadMoveYMM) <= limits.MaxHeadMoveYMM;
    }

    /// <summary>
    /// Calculate the maximum spindle movement in millimeters based on the optimized spindle movement,
    /// considering both tail and head movements.
    /// </summary>
    /// <param name="movement">The optimized spindle movement to evaluate.</param>
    /// <returns>The maximum spindle movement in millimeters.</returns>
    private static double MaxSpindleMovementMM(
        OptimizedSpindleMovement movement)
    {
        double tail = Math.Sqrt(
            movement.TailMoveXMM * movement.TailMoveXMM +
            movement.TailMoveYMM * movement.TailMoveYMM);

        double head = Math.Sqrt(
            movement.HeadMoveXMM * movement.HeadMoveXMM +
            movement.HeadMoveYMM * movement.HeadMoveYMM);

        return Math.Max(tail, head);
    }

    /// <summary>
    /// Generate a range of double values centered around a specified value, with a given radius and step size.
    /// </summary>
    /// <param name="center">The center value around which to generate the range.</param>
    /// <param name="radius">The radius around the center value to define the range.</param>
    /// <param name="step">The step size between consecutive values in the range.</param>
    /// <returns>An enumerable of double values within the specified range.</returns>
    private static IEnumerable<double> Range(
        double center,
        double radius,
        double step)
    {
        for (double value = center - radius;
             value <= center + radius + 0.0001;
             value += step)
        {
            yield return Math.Round(value, 4);
        }
    }

    /// <summary>
    /// Validate the input parameters for the inscribed geometry optimization, ensuring that they meet
    /// the required constraints and throwing exceptions for invalid inputs.
    /// </summary>
    /// <param name="field">The binned radius field points to validate.</param>
    /// <param name="optimizationSettings">The optimization settings to validate.</param>
    /// <param name="geometrySettings">The inscribed geometry settings to validate.</param>
    /// <param name="machineLimits">The machine limit settings to validate.</param>
    /// <exception cref="InvalidOperationException">Thrown when any of the input parameters are invalid.</exception>

    private static void ValidateInputs(
        List<BinnedRadiusFieldPoint> field,
        OptimizationSettings optimizationSettings,
        InscribedGeometrySettings geometrySettings,
        MachineLimitSettings machineLimits)
    {
        if (field is null || field.Count == 0)
        {
            throw new InvalidOperationException(
                "Binned radius field is empty.");
        }

        ArgumentNullException.ThrowIfNull(optimizationSettings);
        ArgumentNullException.ThrowIfNull(geometrySettings);
        ArgumentNullException.ThrowIfNull(machineLimits);

        if (geometrySettings.AngularBins < 12)
        {
            throw new InvalidOperationException(
                "InscribedGeometry AngularBins must be at least 12.");
        }

        if (geometrySettings.MinimumValidAngularBins < 3 ||
            geometrySettings.MinimumValidAngularBins >
            geometrySettings.AngularBins)
        {
            throw new InvalidOperationException(
                "MinimumValidAngularBins must be between 3 and AngularBins.");
        }

        if (geometrySettings.AxisRatioStep <= 0 ||
            geometrySettings.AxisRatioStep > 1)
        {
            throw new InvalidOperationException(
                "AxisRatioStep must be greater than zero and no more than one.");
        }

        if (geometrySettings.MinimumAxisRatio <= 0 ||
            geometrySettings.MinimumAxisRatio > 1)
        {
            throw new InvalidOperationException(
                "MinimumAxisRatio must be greater than zero and no more than one.");
        }

        if (geometrySettings.OrientationStepDeg <= 0 ||
            geometrySettings.OrientationStepDeg > 180)
        {
            throw new InvalidOperationException(
                "OrientationStepDeg must be greater than zero and no more than 180.");
        }

        if (geometrySettings.SafetyMarginMM < 0)
        {
            throw new InvalidOperationException(
                "SafetyMarginMM cannot be negative.");
        }

        if (geometrySettings.GeometryTieToleranceMM < 0)
        {
            throw new InvalidOperationException(
                "GeometryTieToleranceMM cannot be negative.");
        }
    }

    /// <summary>
    /// Represents a sample point on the surface of the log, including its normalized Z position and Cartesian coordinates in millimeters.
    /// </summary>
    /// <param name="NormalizedZ">The normalized Z position of the sample point.</param>
    /// <param name="XMM">The X coordinate of the sample point in millimeters.</param>
    /// <param name="YMM">The Y coordinate of the sample point in millimeters.</param>
    private sealed record SurfaceSample(
        double NormalizedZ,
        double XMM,
        double YMM);

    /// <summary>
    /// Represents a candidate inscribed geometry configuration, including its offsets, sweeps, and the resulting geometry evaluation.
    /// </summary>
    private sealed class Candidate
    {
        public double OffsetXMM { get; init; }
        public double OffsetYMM { get; init; }
        public double SweepXMM { get; init; }
        public double SweepYMM { get; init; }
        public InscribedGeometryResult Geometry { get; init; } = new();
    }

    /// <summary>
    /// Represents the result of a search pass, including the best offsets, sweeps, resulting geometry, and the number of tested candidates.
    /// </summary>
    private sealed class SearchPassResult
    {
        public double OffsetXMM { get; init; }
        public double OffsetYMM { get; init; }
        public double SweepXMM { get; init; }
        public double SweepYMM { get; init; }
        public InscribedGeometryResult? Geometry { get; init; }
        public int TestedCandidates { get; init; }
    }
}
