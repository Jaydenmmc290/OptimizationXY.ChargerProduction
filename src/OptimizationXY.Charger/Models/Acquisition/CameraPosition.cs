namespace OptimizationXY.Charger.Models.Acquisition;


/// <summary>
/// Which end of the block a camera looks at. The two cameras each see about
/// half of the log's length; their images are joined into one full-length image.
/// </summary>

public enum CameraPosition
{

    /// <summary>Head camera - looks at the HEAD end of the block (the end nearest the lathe operator).</summary>

    Head,

    /// <summary>Tail camera - looks at the TAIL end of the block (the end farthest from the lathe operator).</summary>

    Tail
}
