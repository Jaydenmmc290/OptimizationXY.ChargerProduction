namespace OptimizationXY.Charger.Models.Acquisition;

    /// <summary>
    /// Specifies how the camera acquires frames.
    /// </summary>
    public enum AcquisitionMode 
    {

        /// Free-running frame rate mode — camera generates frames at a constant internal rate.
        /// Used for testing and frame-rate-driven acquisition.

        FrameRate,

        /// Hardware-triggered or encoder-based mode — camera is externally triggered by hardware signals.
        /// Used for production where frame timing is controlled by external hardware.

        Encoder
    }

