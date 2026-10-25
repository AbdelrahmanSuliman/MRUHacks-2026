using System;
using System.Globalization;
using UnityEngine;

namespace CalgaryCycling.RouteUI
{
    // Geometry intentionally stays with the coordinate-conversion integration.
    // JsonUtility ignores the export's unsupported nested coordinate arrays.
    [Serializable]
    public sealed class OfflineRouteSummary
    {
        [Serializable]
        public sealed class Endpoint
        {
            public string name;
            public double longitude = double.NaN;
            public double latitude = double.NaN;

            public string DisplayName => string.IsNullOrWhiteSpace(name)
                ? latitude.ToString("F5", CultureInfo.InvariantCulture) + ", " +
                  longitude.ToString("F5", CultureInfo.InvariantCulture)
                : name;

            public bool IsValid => !double.IsNaN(longitude) && !double.IsInfinity(longitude) &&
                !double.IsNaN(latitude) && !double.IsInfinity(latitude) &&
                Math.Abs(longitude) <= 180 && Math.Abs(latitude) <= 90;
        }

        public string source;
        public string travelMode;
        public Endpoint origin;
        public Endpoint destination;
        public float distanceMeters = -1;
        public float durationSeconds = -1;

        public static bool TryParse(string json, out OfflineRouteSummary summary)
        {
            summary = null;
            if(string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                var data = JsonUtility.FromJson<OfflineRouteSummary>(json);
                if(data == null || data.travelMode != "BICYCLE" ||
                   data.origin == null || !data.origin.IsValid ||
                   data.destination == null || !data.destination.IsValid ||
                   !float.IsFinite(data.distanceMeters) || data.distanceMeters <= 0 ||
                   !float.IsFinite(data.durationSeconds) || data.durationSeconds < 0)
                    return false;
                summary = data;
                return true;
            }
            catch(ArgumentException) { return false; }
        }

        public string PreviewText => string.Format(CultureInfo.InvariantCulture,
            "OFFLINE PREVIEW\nFrom: {0}   To: {1}\n{2:F2} km | {3:F1} min estimated\n{4}",
            origin.DisplayName, destination.DisplayName, distanceMeters / 1000f,
            durationSeconds / 60f, source);
    }
}
