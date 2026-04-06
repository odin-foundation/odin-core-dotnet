#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Odin.Core.Types;

using Odin.Core.Transform;

namespace Odin.Core.Transform.Verbs;

/// <summary>
/// Geographic verbs: distance (Haversine), bounding box, radians/degrees conversion,
/// bearing, and midpoint calculations.
/// </summary>
internal static class GeoVerbs
{
    /// <summary>
    /// Registers all geographic verbs into the provided dictionary.
    /// </summary>
    /// <param name="reg">The verb registration dictionary.</param>
    public static void Register(Dictionary<string, Func<DynValue[], VerbContext, DynValue>> reg)
    {
        reg["distance"] = Distance;
        reg["inBoundingBox"] = InBoundingBox;
        reg["toRadians"] = ToRadians;
        reg["toDegrees"] = ToDegrees;
        reg["bearing"] = Bearing;
        reg["midpoint"] = Midpoint;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Constants & Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private const double EarthRadiusKm = 6371.0;
    private const double DegToRad = Math.PI / 180.0;
    private const double RadToDeg = 180.0 / Math.PI;

    private static double? ToDouble(DynValue v)
    {
        if (v.IsNull) return null;
        var d = v.AsDouble();
        if (d.HasValue) return d.Value;
        var s = v.AsString();
        if (s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    private static DynValue NumericResult(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            return DynValue.Null();
        return DynValue.Float(v);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Verb Implementations
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Earth's radius in miles.
    /// </summary>
    private const double EarthRadiusMiles = 3959.0;

    /// <summary>
    /// Computes the great-circle distance between two points using the Haversine formula.
    /// args: lat1, lon1, lat2, lon2, [unit]. Default unit is "km".
    /// Supported units: "km", "mi", "miles". Unknown unit pushes T011 INCOMPATIBLE_CONVERSION error.
    /// </summary>
    private static DynValue Distance(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 4) return DynValue.Null();
        var lat1 = ToDouble(args[0]);
        var lon1 = ToDouble(args[1]);
        var lat2 = ToDouble(args[2]);
        var lon2 = ToDouble(args[3]);
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return DynValue.Null();

        // Validate coordinates
        if (lat1.Value < -90 || lat1.Value > 90 || lat2.Value < -90 || lat2.Value > 90)
            return DynValue.Null();
        if (lon1.Value < -180 || lon1.Value > 180 || lon2.Value < -180 || lon2.Value > 180)
            return DynValue.Null();

        // Determine unit
        string unit = args.Length >= 5
            ? (args[4].AsString() ?? "").ToLowerInvariant()
            : "km";
        if (unit != "km" && unit != "mi" && unit != "miles")
        {
            // T011: Incompatible conversion — unknown unit
            ctx.Errors.Add(new TransformError
            {
                Code = TransformErrorCode.IncompatibleConversion.Code(),
                Message = $"Incompatible conversion in 'distance': unknown unit '{unit}' (expected 'km', 'mi', or 'miles')"
            });
            return DynValue.Null();
        }
        double radius = (unit == "mi" || unit == "miles") ? EarthRadiusMiles : EarthRadiusKm;

        double lat1Rad = lat1.Value * DegToRad;
        double lat2Rad = lat2.Value * DegToRad;
        double dLat = (lat2.Value - lat1.Value) * DegToRad;
        double dLon = (lon2.Value - lon1.Value) * DegToRad;

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
                   + Math.Cos(lat1Rad) * Math.Cos(lat2Rad)
                   * Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));

        return NumericResult(radius * c);
    }

    /// <summary>
    /// Checks if a point is within a bounding box.
    /// args: lat, lon, minLat, minLon, maxLat, maxLon. Returns boolean.
    /// </summary>
    private static DynValue InBoundingBox(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 6) return DynValue.Null();
        var lat = ToDouble(args[0]);
        var lon = ToDouble(args[1]);
        var minLat = ToDouble(args[2]);
        var minLon = ToDouble(args[3]);
        var maxLat = ToDouble(args[4]);
        var maxLon = ToDouble(args[5]);

        if (!lat.HasValue || !lon.HasValue || !minLat.HasValue
            || !minLon.HasValue || !maxLat.HasValue || !maxLon.HasValue)
            return DynValue.Null();

        bool inside = lat.Value >= minLat.Value && lat.Value <= maxLat.Value
                      && lon.Value >= minLon.Value && lon.Value <= maxLon.Value;

        return DynValue.Bool(inside);
    }

    /// <summary>
    /// Converts degrees to radians.
    /// </summary>
    private static DynValue ToRadians(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var deg = ToDouble(args[0]);
        if (!deg.HasValue) return DynValue.Null();
        return NumericResult(deg.Value * DegToRad);
    }

    /// <summary>
    /// Converts radians to degrees.
    /// </summary>
    private static DynValue ToDegrees(DynValue[] args, VerbContext ctx)
    {
        if (args.Length == 0) return DynValue.Null();
        var rad = ToDouble(args[0]);
        if (!rad.HasValue) return DynValue.Null();
        return NumericResult(rad.Value * RadToDeg);
    }

    /// <summary>
    /// Computes the initial bearing (forward azimuth) from point 1 to point 2.
    /// args: lat1, lon1, lat2, lon2. Returns bearing in degrees (0-360).
    /// </summary>
    private static DynValue Bearing(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 4) return DynValue.Null();
        var lat1 = ToDouble(args[0]);
        var lon1 = ToDouble(args[1]);
        var lat2 = ToDouble(args[2]);
        var lon2 = ToDouble(args[3]);
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return DynValue.Null();

        double lat1Rad = lat1.Value * DegToRad;
        double lat2Rad = lat2.Value * DegToRad;
        double dLon = (lon2.Value - lon1.Value) * DegToRad;

        double y = Math.Sin(dLon) * Math.Cos(lat2Rad);
        double x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad)
                   - Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);
        double bearing = Math.Atan2(y, x) * RadToDeg;

        // Normalize to 0-360
        bearing = (bearing + 360.0) % 360.0;

        return NumericResult(bearing);
    }

    /// <summary>
    /// Computes the geographic midpoint between two points.
    /// args: lat1, lon1, lat2, lon2. Returns an array [midLat, midLon].
    /// </summary>
    private static DynValue Midpoint(DynValue[] args, VerbContext ctx)
    {
        if (args.Length < 4) return DynValue.Null();
        var lat1 = ToDouble(args[0]);
        var lon1 = ToDouble(args[1]);
        var lat2 = ToDouble(args[2]);
        var lon2 = ToDouble(args[3]);
        if (!lat1.HasValue || !lon1.HasValue || !lat2.HasValue || !lon2.HasValue)
            return DynValue.Null();

        double lat1Rad = lat1.Value * DegToRad;
        double lon1Rad = lon1.Value * DegToRad;
        double lat2Rad = lat2.Value * DegToRad;
        double dLon = (lon2.Value - lon1.Value) * DegToRad;

        double bx = Math.Cos(lat2Rad) * Math.Cos(dLon);
        double by = Math.Cos(lat2Rad) * Math.Sin(dLon);

        double midLat = Math.Atan2(
            Math.Sin(lat1Rad) + Math.Sin(lat2Rad),
            Math.Sqrt((Math.Cos(lat1Rad) + bx) * (Math.Cos(lat1Rad) + bx) + by * by)
        );
        double midLon = lon1Rad + Math.Atan2(by, Math.Cos(lat1Rad) + bx);

        var result = new List<DynValue>
        {
            DynValue.Float(midLat * RadToDeg),
            DynValue.Float(midLon * RadToDeg)
        };

        return DynValue.Array(result);
    }
}
