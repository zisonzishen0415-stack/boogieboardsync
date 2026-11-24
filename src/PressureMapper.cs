using System;

namespace BoogieBoardSync
{
    internal sealed class PressureMapper
    {
        private readonly int rawMin;
        private readonly int rawMax;
        private readonly double gamma;
        private readonly double deadZone;
        private readonly double smoothing;
        private double smoothed;

        public PressureMapper(int rawMin, int rawMax, double gamma, double deadZone, double smoothing)
        {
            this.rawMin = rawMin;
            this.rawMax = rawMax;
            this.gamma = Clamp(gamma, 0.1, 4.0);
            this.deadZone = Clamp(deadZone, 0.0, 0.95);
            this.smoothing = Clamp(smoothing, 0.0, 0.95);
            smoothed = 0.0;
        }

        public int Map(int raw)
        {
            double normalized = Normalize(raw);
            if (normalized <= deadZone)
            {
                normalized = 0.0;
            }
            else
            {
                normalized = (normalized - deadZone) / (1.0 - deadZone);
            }

            if (normalized > 0.0)
            {
                normalized = Math.Pow(normalized, gamma);
            }

            if (smoothing > 0.0)
            {
                smoothed = (smoothed * smoothing) + (normalized * (1.0 - smoothing));
                normalized = smoothed;
            }
            else
            {
                smoothed = normalized;
            }

            return (int)Math.Round(Clamp(normalized, 0.0, 1.0) * 1024.0);
        }

        public void ResetSmoothing()
        {
            smoothed = 0.0;
        }

        private double Normalize(int raw)
        {
            if (rawMax <= rawMin)
            {
                return raw > rawMin ? 1.0 : 0.0;
            }
            return Clamp((raw - rawMin) / (double)(rawMax - rawMin), 0.0, 1.0);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }
    }
}
