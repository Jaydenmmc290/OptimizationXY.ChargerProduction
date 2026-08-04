using System;
using System.Collections.Generic;
using System.Text;

namespace OptimizationXY.Charger.Models.Geometry
{
    /// <summary>
    /// Represents a single point in 3D Cartesian space with X, Y, and Z coordinates.
    ///
    /// This is a fundamental geometric data structure used throughout the application
    /// for representing 3D coordinates in point clouds, geometry calculations, and
    /// spatial analysis. All coordinates are in millimeters.
    ///
    /// Typical usage:
    /// - Representing points in a 3D point cloud from the laser scanner
    /// - Storing centerline positions along the log length
    /// - Defining log geometry vertices
    /// </summary>
    public class Point3D
    {
        /// <summary>
        /// Gets or sets the X coordinate of this point in 3D space, measured in millimeters.
        ///
        /// Typically represents horizontal position (left/right) or radial distance
        /// depending on the coordinate system context.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of this point in 3D space, measured in millimeters.
        ///
        /// Typically represents vertical position or second radial direction
        /// depending on the coordinate system context.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Gets or sets the Z coordinate of this point in 3D space, measured in millimeters.
        ///
        /// Typically represents axial position along the log length or depth
        /// depending on the coordinate system context.
        /// </summary>
        public double Z { get; set; }

        /// <summary>
        /// Initializes a new instance of the Point3D class at the origin (0, 0, 0).
        ///
        /// Properties must be set after construction using the X, Y, Z properties.
        /// </summary>
        public Point3D()
        {
        }

        /// <summary>
        /// Initializes a new instance of the Point3D class with specific coordinate values.
        /// </summary>
        /// <param name="x">The X coordinate in millimeters.</param>
        /// <param name="y">The Y coordinate in millimeters.</param>
        /// <param name="z">The Z coordinate in millimeters.</param>
        public Point3D(
            double x,
            double y,
            double z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }
}
