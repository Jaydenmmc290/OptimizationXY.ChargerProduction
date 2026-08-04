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

    private static double[] BuildAngularEnvelope(
        SurfaceSample[] samples,
        OptimizedSpindleMovement movement,
        int angularBins,
        out int validAngularBins)
    {
        double[] envelope =
            Enumerable.Repeat(
                double.PositiveInfinity,
                angularBins)
            .ToArray();

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
    /// Missing angular bins are filled with the smaller of the nearest
    /// measured neighbors. This is deliberately conservative: an unmeasured
    /// direction can never make the reported geometry larger.
    /// </summary>
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

    private static SurfaceSample[] BuildSamples(
        List<BinnedRadiusFieldPoint> field)
    {
        double minZ = field.Min(point => point.ZBinMM);
        double maxZ = field.Max(point => point.ZBinMM);
        double length = Math.Max(0.001, maxZ - minZ);

        return field
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
                        Math.Sin(item.AngleRad)))
            .ToArray();
    }

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

    private sealed record SurfaceSample(
        double NormalizedZ,
        double XMM,
        double YMM);

    private sealed class Candidate
    {
        public double OffsetXMM { get; init; }
        public double OffsetYMM { get; init; }
        public double SweepXMM { get; init; }
        public double SweepYMM { get; init; }
        public InscribedGeometryResult Geometry { get; init; } = new();
    }

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
