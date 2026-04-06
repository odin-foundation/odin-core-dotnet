// ODIN Forms 1.0 — Unit Conversion
//
// Converts between page units (inch, cm, mm, pt) and pixels at 96 DPI.
// Matches the TypeScript implementation exactly.

using System;
using System.Collections.Generic;

namespace Odin.Core.Forms;

/// <summary>
/// Unit conversion utilities for ODIN Forms.
/// All conversions use 96 DPI, matching the TypeScript SDK.
/// </summary>
public static class FormUnits
{
    private const double Dpi = 96.0;

    private static readonly Dictionary<string, double> Conversions = new Dictionary<string, double>
    {
        { "inch", Dpi },           // 1 inch = 96px
        { "cm",   Dpi / 2.54 },   // 1 cm ≈ 37.795px
        { "mm",   Dpi / 25.4 },   // 1 mm ≈ 3.7795px
        { "pt",   Dpi / 72.0 },   // 1 pt ≈ 1.333px
    };

    /// <summary>
    /// Convert a value in the given unit to pixels.
    /// </summary>
    /// <param name="value">The measurement value.</param>
    /// <param name="unit">The unit: "inch", "cm", "mm", or "pt".</param>
    /// <returns>The equivalent pixel count, rounded to 3 decimal places.</returns>
    /// <exception cref="ArgumentException">Thrown when the unit is not recognised.</exception>
    public static double ToPixels(double value, string unit)
    {
        if (!Conversions.TryGetValue(unit, out var factor))
            throw new ArgumentException($"Unknown unit: {unit}", nameof(unit));
        return Math.Round(value * factor * 1000.0) / 1000.0;
    }

    /// <summary>
    /// Convert a pixel value back to the given unit.
    /// </summary>
    /// <param name="px">The pixel count.</param>
    /// <param name="unit">The unit: "inch", "cm", "mm", or "pt".</param>
    /// <returns>The equivalent measurement, rounded to 3 decimal places.</returns>
    /// <exception cref="ArgumentException">Thrown when the unit is not recognised.</exception>
    public static double FromPixels(double px, string unit)
    {
        if (!Conversions.TryGetValue(unit, out var factor))
            throw new ArgumentException($"Unknown unit: {unit}", nameof(unit));
        return Math.Round((px / factor) * 1000.0) / 1000.0;
    }
}
