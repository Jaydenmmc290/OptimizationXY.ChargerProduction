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

        // ------------------------------------------------------------
        // Repeatability anchor.
        //
        // The largest inscribed cylinder is defined by only a few contact
        // points, so many different axes give almost the same diameter and
        // small scan differences move the winner by many millimetres. The
        // fitted centreline of the block (circle fit per slice, straight
        // line through the centres) is an average over the whole surface and
        // is stable from scan to scan. The search is centred on it, and a
        // candidate must gain CenterlinePullWeight mm of radius for every
        // millimetre it sits away from it.
        // ------------------------------------------------------------
        double pullWeight =
            double.IsFinite(geometrySettings.CenterlinePullWeight)
                ? Math.Max(0.0, geometrySettings.CenterlinePullWeight)
                : 0.0;

        ReferenceAxis? reference =
            pullWeight > 0.0
                ? TryFitReferenceAxis(samples)
                : null;

        if (reference is null)
            pullWeight = 0.0;

        double startOffsetXMM = 0.0;
        double startOffsetYMM = 0.0;
        double startSweepXMM = 0.0;
        double startSweepYMM = 0.0;

        if (reference is not null)
        {
            // Start the search inside the commissioned travel so a block that
            // sits further off-axis than the spindles can move still produces
            // the best reachable position instead of no candidates at all.
            double startTailX = Math.Clamp(reference.TailXMM, -machineLimits.MaxTailMoveXMM, machineLimits.MaxTailMoveXMM);
            double startTailY = Math.Clamp(reference.TailYMM, -machineLimits.MaxTailMoveYMM, machineLimits.MaxTailMoveYMM);
            double startHeadX = Math.Clamp(reference.HeadXMM, -machineLimits.MaxHeadMoveXMM, machineLimits.MaxHeadMoveXMM);
            double startHeadY = Math.Clamp(reference.HeadYMM, -machineLimits.MaxHeadMoveYMM, machineLimits.MaxHeadMoveYMM);

            // Tail = offset + sweep / 2, Head = offset - sweep / 2.
            startOffsetXMM = (startTailX + startHeadX) / 2.0;
            startOffsetYMM = (startTailY + startHeadY) / 2.0;
            startSweepXMM = startTailX - startHeadX;
            startSweepYMM = startTailY - startHeadY;
        }

        SearchPassResult coarse = Search(
            samples,
            kind,
            geometrySettings,
            machineLimits,
            reference,
            pullWeight,
            centerOffsetXMM: startOffsetXMM,
            centerOffsetYMM: startOffsetYMM,
            centerSweepXMM: startSweepXMM,
            centerSweepYMM: startSweepYMM,
            radiusMM: optimizationSettings.CoarseMaxMoveMM,
            stepMM: optimizationSettings.CoarseStepMM,
            token);

        SearchPassResult medium = Search(
            samples,
            kind,
            geometrySettings,
            machineLimits,
            reference,
            pullWeight,
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
            reference,
            pullWeight,
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

        double? certifiedGapMM = null;
        int certifiedEvaluations = 0;
        bool certifiedImproved = false;

        if (kind == InscribedGeometryKind.Cylinder &&
            geometrySettings.UseCertifiedCylinderSearch)
        {
            Candidate incumbent = EvaluateCandidate(
                samples,
                kind,
                geometrySettings,
                machineLimits,
                reference,
                pullWeight,
                fine.OffsetXMM,
                fine.OffsetYMM,
                fine.SweepXMM,
                fine.SweepYMM,
                out _) ?? throw new InvalidOperationException(
                    "The grid search result could not be re-evaluated for the certified search.");

            CertifiedOutcome outcome = CertifiedCylinderSearch(
                samples,
                geometrySettings,
                machineLimits,
                reference,
                pullWeight,
                incumbent,
                token);

            if (!ReferenceEquals(outcome.Best, incumbent))
            {
                result = outcome.Best.Geometry;
                certifiedImproved = true;
            }

            certifiedGapMM = outcome.GapMM;
            certifiedEvaluations = outcome.Evaluations;
        }

        result.CertifiedGapMM = certifiedGapMM;
        result.CertifiedEvaluations = certifiedEvaluations;
        result.CertifiedSearchImproved = certifiedImproved;

        if (reference is not null)
        {
            result.CenterlineAnchorUsed = true;
            result.CenterlineTailXMM = reference.TailXMM;
            result.CenterlineTailYMM = reference.TailYMM;
            result.CenterlineHeadXMM = reference.HeadXMM;
            result.CenterlineHeadYMM = reference.HeadYMM;
            result.CenterlineDeviationMM = DeviationFromReference(
                result.SpindleMovement,
                reference);
        }

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
        ReferenceAxis? reference,
        double pullWeight,
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
        object lockObj = new();
        long tested = 0;
        int maximumValidAngularBinsObserved = 0;

        List<double> offsetXValues = new(Range(centerOffsetXMM, radiusMM, stepMM));

        Parallel.ForEach(offsetXValues, new ParallelOptions
        {
            CancellationToken = token,
            MaxDegreeOfParallelism = Environment.ProcessorCount
        },
        delegate (double offsetX)
        {
            foreach (double offsetY in Range(centerOffsetYMM, radiusMM, stepMM))
            {
                foreach (double sweepX in Range(centerSweepXMM, radiusMM, stepMM))
                {
                    foreach (double sweepY in Range(centerSweepYMM, radiusMM, stepMM))
                    {
                        token.ThrowIfCancellationRequested();

                        Candidate? candidate = EvaluateCandidate(
                            samples,
                            kind,
                            settings,
                            machineLimits,
                            reference,
                            pullWeight,
                            offsetX,
                            offsetY,
                            sweepX,
                            sweepY,
                            out int validAngularBins);

                        UpdateMaximum(
                            ref maximumValidAngularBinsObserved,
                            validAngularBins);

                        System.Threading.Interlocked.Increment(ref tested);

                        if (candidate is null)
                            continue;

                        lock (lockObj)
                        {
                            if (best is null || IsBetter(candidate, best, settings, pullWeight > 0.0))
                                best = candidate;
                        }
                    }
                }
            }
        });

        if (best is null)
        {
            throw new InvalidOperationException(
                $"No valid {kind} candidate had sufficient angular coverage. " +
                $"Required {settings.MinimumValidAngularBins} of " +
                $"{settings.AngularBins} angular bins; best candidate had " +
                $"{maximumValidAngularBinsObserved}. " +
                $"Surface samples: {samples.Length}; candidates tested: {tested}.");
        }

        return new SearchPassResult
        {
            OffsetXMM = best.OffsetXMM,
            OffsetYMM = best.OffsetYMM,
            SweepXMM = best.SweepXMM,
            SweepYMM = best.SweepYMM,
            Geometry = best.Geometry,
            TestedCandidates = (int)tested
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
        ReferenceAxis? reference,
        double pullWeight,
        double offsetXMM,
        double offsetYMM,
        double sweepXMM,
        double sweepYMM,
        out int validAngularBins)
    {
        validAngularBins = 0;

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
            out validAngularBins);

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

        // Radius of the circle with the same area, so cylinder and oval
        // candidates are pulled toward the centreline on the same scale.
        double equivalentRadiusMM =
            kind == InscribedGeometryKind.Cylinder
                ? geometry.GuaranteedCircularRadiusMM
                : Math.Sqrt(Math.Max(0.0, geometry.AreaMM2) / Math.PI);

        double deviationMM =
            reference is null
                ? 0.0
                : DeviationFromReference(movement, reference);

        return new Candidate
        {
            OffsetXMM = offsetXMM,
            OffsetYMM = offsetYMM,
            SweepXMM = sweepXMM,
            SweepYMM = sweepYMM,
            Geometry = geometry,
            Score = equivalentRadiusMM - pullWeight * deviationMM
        };
    }

    private sealed record CertifiedOutcome(Candidate Best, double GapMM, int Evaluations);

    /// <summary>
    /// Branch-and-bound search for the largest-inscribed-cylinder score over the
    /// whole machine travel, started from the grid-search answer.
    ///
    /// Why a bound exists: the cylinder radius of an axis is the smallest
    /// distance from any surface sample to that axis (the per-bin envelope and
    /// its conservative fill only ever copy sample distances, so their minimum is
    /// the minimum over all samples), minus the safety margin. Moving the tail or
    /// head end of the axis by at most d moves the axis by at most d at every
    /// point along the block, so the radius changes by at most d. The centreline
    /// pull term changes by at most pullWeight x d. So the score of any axis in a
    /// box is at most the score at the box centre plus (1 + pullWeight) x d,
    /// where d is the largest tail/head distance from the centre to a corner.
    ///
    /// Boxes whose bound cannot beat the best answer by more than the tolerance
    /// are dropped; the others are split in half along all four coordinates. The
    /// angular-coverage requirement is checked only on candidate answers (the
    /// bound ignores it, which can only make the bound higher, so the reported
    /// gap stays valid). A better answer replaces the grid answer only when it
    /// scores more than GeometryTieToleranceMM higher.
    /// </summary>
    private static CertifiedOutcome CertifiedCylinderSearch(
        SurfaceSample[] samples,
        InscribedGeometrySettings settings,
        MachineLimitSettings limits,
        ReferenceAxis? reference,
        double pullWeight,
        Candidate incumbent,
        CancellationToken token)
    {
        double lipschitz = 1.0 + Math.Max(0.0, pullWeight);
        double tolerance = Math.Max(0.01, settings.CertifiedSearchToleranceMM);
        int maxEvaluations = Math.Max(1000, settings.CertifiedSearchMaxEvaluations);
        double tieTolerance = Math.Max(0.0, settings.GeometryTieToleranceMM);

        double[] sampleX = [.. samples.Select(sample => sample.XMM)];
        double[] sampleY = [.. samples.Select(sample => sample.YMM)];
        double[] sampleT = [.. samples.Select(sample => sample.NormalizedZ)];

        // Score ignoring the angular-coverage requirement (upper-bound side).
        double RelaxedScore(double tx, double ty, double hx, double hy)
        {
            double minimumSquared = double.PositiveInfinity;

            for (int i = 0; i < sampleX.Length; i++)
            {
                double t = sampleT[i];
                double dx = sampleX[i] - (tx + (hx - tx) * t);
                double dy = sampleY[i] - (ty + (hy - ty) * t);
                double squared = dx * dx + dy * dy;

                if (squared < minimumSquared)
                    minimumSquared = squared;
            }

            double radius = Math.Max(0.0, Math.Sqrt(minimumSquared) - settings.SafetyMarginMM);

            if (reference is null || pullWeight <= 0.0)
                return radius;

            double tailDeviation = Math.Sqrt(
                (tx - reference.TailXMM) * (tx - reference.TailXMM) +
                (ty - reference.TailYMM) * (ty - reference.TailYMM));
            double headDeviation = Math.Sqrt(
                (hx - reference.HeadXMM) * (hx - reference.HeadXMM) +
                (hy - reference.HeadYMM) * (hy - reference.HeadYMM));

            return radius - pullWeight * Math.Max(tailDeviation, headDeviation);
        }

        Candidate best = incumbent;
        int evaluations = 0;

        // Box = centre (tx, ty, hx, hy) and half-widths per coordinate.
        var queue = new PriorityQueue<double[], double>();

        void Push(double[] box, double centreScore)
        {
            double tailReach = Math.Sqrt(box[4] * box[4] + box[5] * box[5]);
            double headReach = Math.Sqrt(box[6] * box[6] + box[7] * box[7]);
            double bound = centreScore + lipschitz * Math.Max(tailReach, headReach);

            if (bound > best.Score + tolerance)
                queue.Enqueue(box, -bound);
        }

        double[] root =
        [
            0.0, 0.0, 0.0, 0.0,
            limits.MaxTailMoveXMM, limits.MaxTailMoveYMM,
            limits.MaxHeadMoveXMM, limits.MaxHeadMoveYMM
        ];

        Push(root, RelaxedScore(0.0, 0.0, 0.0, 0.0));
        evaluations++;

        while (queue.Count > 0 && evaluations < maxEvaluations)
        {
            token.ThrowIfCancellationRequested();

            queue.TryPeek(out _, out double negativeBound);
            if (-negativeBound <= best.Score + tolerance)
                break;

            double[] box = queue.Dequeue();

            // 16 children: halve all four coordinates.
            var children = new double[16][];
            var scores = new double[16];

            for (int child = 0; child < 16; child++)
            {
                double[] c = new double[8];

                for (int axis = 0; axis < 4; axis++)
                {
                    double half = box[4 + axis] / 2.0;
                    double sign = ((child >> axis) & 1) == 0 ? -1.0 : 1.0;
                    c[axis] = box[axis] + sign * half;
                    c[4 + axis] = half;
                }

                children[child] = c;
            }

            Parallel.For(0, 16, child =>
            {
                double[] c = children[child];
                scores[child] = RelaxedScore(c[0], c[1], c[2], c[3]);
            });

            evaluations += 16;

            for (int child = 0; child < 16; child++)
            {
                double[] c = children[child];

                if (scores[child] > best.Score + tieTolerance)
                {
                    // Possible better answer: evaluate it exactly as the grid
                    // search does (including the angular-coverage requirement).
                    double offsetX = (c[0] + c[2]) / 2.0;
                    double offsetY = (c[1] + c[3]) / 2.0;
                    double sweepX = c[0] - c[2];
                    double sweepY = c[1] - c[3];

                    Candidate? candidate = EvaluateCandidate(
                        samples,
                        InscribedGeometryKind.Cylinder,
                        settings,
                        limits,
                        reference,
                        pullWeight,
                        offsetX,
                        offsetY,
                        sweepX,
                        sweepY,
                        out _);

                    if (candidate is not null &&
                        candidate.Score > best.Score + tieTolerance)
                    {
                        best = candidate;
                    }
                }

                Push(c, scores[child]);
            }
        }

        double gap = 0.0;

        if (queue.Count > 0)
        {
            queue.TryPeek(out _, out double topNegativeBound);
            gap = Math.Max(0.0, -topNegativeBound - best.Score);
        }

        // Within the tolerance counts as proven.
        if (gap <= tolerance)
            gap = Math.Min(gap, tolerance);

        return new CertifiedOutcome(best, gap, evaluations);
    }

    /// <summary>
    /// Atomically records the highest observed integer value during a
    /// parallel search pass.
    /// </summary>
    private static void UpdateMaximum(
        ref int target,
        int candidate)
    {
        int current = System.Threading.Volatile.Read(ref target);

        while (candidate > current)
        {
            int observed = System.Threading.Interlocked.CompareExchange(
                ref target,
                candidate,
                current);

            if (observed == current)
                return;

            current = observed;
        }
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

        // AngleBinDeg is the lower edge of each angular bin. The measured surface
        // in that bin is, on average, half a bin further round, so samples are
        // placed at the bin centre to avoid rotating the whole field.
        double[] distinctAngles = [.. field
            .Select(point => point.AngleBinDeg)
            .Where(double.IsFinite)
            .Distinct()
            .OrderBy(angle => angle)];

        double angleBinSizeDeg = 0.0;
        for (int index = 1; index < distinctAngles.Length; index++)
        {
            double step = distinctAngles[index] - distinctAngles[index - 1];

            if (step > 0.0001 &&
                (angleBinSizeDeg <= 0.0 || step < angleBinSizeDeg))
            {
                angleBinSizeDeg = step;
            }
        }

        double halfAngleBinDeg = angleBinSizeDeg / 2.0;

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
                    (point.AngleBinDeg + halfAngleBinDeg) *
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
        InscribedGeometrySettings settings,
        bool useCenterlineScore)
    {
        InscribedGeometryResult left = candidate.Geometry;
        InscribedGeometryResult right = current.Geometry;

        if (useCenterlineScore)
        {
            double scoreDifference = candidate.Score - current.Score;

            if (Math.Abs(scoreDifference) >
                settings.GeometryTieToleranceMM)
            {
                return scoreDifference > 0;
            }
        }
        else if (left.Kind == InscribedGeometryKind.Cylinder)
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

        double movementDifference =
            left.MaxSpindleMovementMM - right.MaxSpindleMovementMM;

        if (Math.Abs(movementDifference) > 0.000001)
            return movementDifference < 0.0;

        // Parallel evaluation can discover exact ties in different orders.
        // Apply a stable final order so the same input is repeatable.
        int comparison = candidate.OffsetXMM.CompareTo(current.OffsetXMM);
        if (comparison != 0)
            return comparison < 0;

        comparison = candidate.OffsetYMM.CompareTo(current.OffsetYMM);
        if (comparison != 0)
            return comparison < 0;

        comparison = candidate.SweepXMM.CompareTo(current.SweepXMM);
        if (comparison != 0)
            return comparison < 0;

        return candidate.SweepYMM < current.SweepYMM;
    }

    /// <summary>
    /// Determine if the optimized spindle movement is within the machine limits specified in the settings.
    /// </summary>
    /// <param name="movement">The optimized spindle movement to evaluate.</param>
    /// <param name="limits">The machine limit settings to compare against.</param>
    /// <returns>True if the optimized spindle movement is within the machine limits; otherwise, false.</returns>
    /// <summary>
    /// Largest distance, at the tail or head end, between a candidate axis and
    /// the fitted centreline of the block.
    /// </summary>
    private static double DeviationFromReference(
        OptimizedSpindleMovement movement,
        ReferenceAxis reference)
    {
        double tailX = movement.TailMoveXMM - reference.TailXMM;
        double tailY = movement.TailMoveYMM - reference.TailYMM;
        double headX = movement.HeadMoveXMM - reference.HeadXMM;
        double headY = movement.HeadMoveYMM - reference.HeadYMM;

        return Math.Max(
            Math.Sqrt(tailX * tailX + tailY * tailY),
            Math.Sqrt(headX * headX + headY * headY));
    }

    /// <summary>
    /// Fits the centreline of the block in the machine frame: a least-squares
    /// circle per Z slice, then a straight line through the slice centres.
    /// Returns null when there is not enough data for a trustworthy fit.
    /// </summary>
    private static ReferenceAxis? TryFitReferenceAxis(
        SurfaceSample[] samples)
    {
        const int MinimumSamplesPerSlice = 12;
        const int MinimumSlices = 5;

        List<double> sliceT = [];
        List<double> sliceX = [];
        List<double> sliceY = [];

        foreach (var slice in samples.GroupBy(
                     sample => (long)Math.Round(sample.NormalizedZ * 1_000_000.0)))
        {
            SurfaceSample[] slicePoints = [.. slice];

            if (slicePoints.Length < MinimumSamplesPerSlice)
                continue;

            double meanX = slicePoints.Average(item => item.XMM);
            double meanY = slicePoints.Average(item => item.YMM);

            double suu = 0.0, suv = 0.0, svv = 0.0;
            double su = 0.0, sv = 0.0;
            double suz = 0.0, svz = 0.0, sz = 0.0;
            int n = slicePoints.Length;

            foreach (SurfaceSample surfacePoint in slicePoints)
            {
                double u = surfacePoint.XMM - meanX;
                double v = surfacePoint.YMM - meanY;
                double z = u * u + v * v;

                suu += u * u;
                suv += u * v;
                svv += v * v;
                su += u;
                sv += v;
                suz += u * z;
                svz += v * z;
                sz += z;
            }

            double det =
                suu * (svv * n - sv * sv) -
                suv * (suv * n - sv * su) +
                su * (suv * sv - svv * su);

            double scale = Math.Max(1.0, suu + svv);
            if (!double.IsFinite(det) ||
                Math.Abs(det) < 1e-9 * scale * scale)
            {
                continue;
            }

            double detA =
                suz * (svv * n - sv * sv) -
                suv * (svz * n - sv * sz) +
                su * (svz * sv - svv * sz);

            double detB =
                suu * (svz * n - sv * sz) -
                suz * (suv * n - sv * su) +
                su * (suv * sz - svz * su);

            double centerX = meanX + 0.5 * detA / det;
            double centerY = meanY + 0.5 * detB / det;

            if (!double.IsFinite(centerX) || !double.IsFinite(centerY))
                continue;

            sliceT.Add(slicePoints[0].NormalizedZ);
            sliceX.Add(centerX);
            sliceY.Add(centerY);
        }

        if (sliceT.Count < MinimumSlices)
            return null;

        double meanT = sliceT.Average();
        double meanCenterX = sliceX.Average();
        double meanCenterY = sliceY.Average();

        double sumTT = 0.0;
        double sumTX = 0.0;
        double sumTY = 0.0;

        for (int sliceIndex = 0; sliceIndex < sliceT.Count; sliceIndex++)
        {
            double dt = sliceT[sliceIndex] - meanT;
            sumTT += dt * dt;
            sumTX += dt * (sliceX[sliceIndex] - meanCenterX);
            sumTY += dt * (sliceY[sliceIndex] - meanCenterY);
        }

        double slopeX = sumTT > 1e-9 ? sumTX / sumTT : 0.0;
        double slopeY = sumTT > 1e-9 ? sumTY / sumTT : 0.0;

        // NormalizedZ = 0 is the tail end, 1 is the head end.
        double tailXMM = meanCenterX - slopeX * meanT;
        double tailYMM = meanCenterY - slopeY * meanT;
        double headXMM = tailXMM + slopeX;
        double headYMM = tailYMM + slopeY;

        if (!double.IsFinite(tailXMM) ||
            !double.IsFinite(tailYMM) ||
            !double.IsFinite(headXMM) ||
            !double.IsFinite(headYMM))
        {
            return null;
        }

        return new ReferenceAxis(
            tailXMM,
            tailYMM,
            headXMM,
            headYMM);
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
        if (step <= 0.0)
        {
            throw new InvalidOperationException(
                "Geometry optimizer range step must be greater than zero.");
        }

        radius = Math.Max(0.0, radius);

        // Preserve the prior pass winner and search equal distances on both
        // sides, even when radius is not an exact multiple of step.
        var values = new SortedSet<double>
        {
            Math.Round(center - radius, 4),
            Math.Round(center, 4),
            Math.Round(center + radius, 4)
        };

        for (double delta = step; delta < radius - 0.0001; delta += step)
        {
            values.Add(Math.Round(center - delta, 4));
            values.Add(Math.Round(center + delta, 4));
        }

        foreach (double value in values)
            yield return value;
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

        // Equivalent inscribed radius minus the centreline pull. Used to rank
        // candidates when the centreline anchor is active.
        public double Score { get; init; }
    }

    /// <summary>
    /// Fitted centreline of the block in the machine frame.
    /// </summary>
    private sealed record ReferenceAxis(
        double TailXMM,
        double TailYMM,
        double HeadXMM,
        double HeadYMM);

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
