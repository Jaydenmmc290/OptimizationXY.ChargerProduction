namespace OptimizationXY.Charger.Models.Acquisition;

    /// <summary>
    /// Specifies how the camera acquires frames.
    /// </summary>
    public enum AcquisitionMode 
    {

        /// <summary>
        /// Free-running mode: the camera takes pictures on its own internal clock at
        /// <see cref="CameraDeviceSettings.AcquisitionFrameRate"/> frames per second.
        /// Used for bench testing when there is no external trigger signal.
        /// </summary>

        FrameRate,

        /// <summary>
        /// Triggered mode (production default): the camera takes a picture each time
        /// it receives a pulse on its trigger input (<see cref="CameraDeviceSettings.TriggerSource"/>,
        /// normally Line1). The machine hardware, not the camera, decides when frames
        /// are taken, so head and tail cameras fire together and stay paired.
        /// </summary>

        Encoder
    }

