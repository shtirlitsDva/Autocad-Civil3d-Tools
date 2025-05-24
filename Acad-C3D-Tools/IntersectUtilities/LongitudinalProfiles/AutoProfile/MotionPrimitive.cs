using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IntersectUtilities.LongitudinalProfiles.AutoProfile
{
    internal record MotionPrimitive(double Radius, double AngleDeg, bool IsArc)
    {
        public static (double x, double y, double theta) ApplyPrimitive(
            double x, double y, double thetaDeg, MotionPrimitive mp)
        {
            if (!mp.IsArc)
            {
                // Straight segment
                double thetaRad = thetaDeg * Math.PI / 180.0;
                double dx = mp.Radius * Math.Cos(thetaRad); // here, Radius == segment length
                double dy = mp.Radius * Math.Sin(thetaRad);
                return (x + dx, y + dy, thetaDeg);
            }
            else
            {
                // Arc segment
                double angleRad = mp.AngleDeg * Math.PI / 180.0;
                double thetaRad = thetaDeg * Math.PI / 180.0;
                double R = mp.Radius;

                // Determine arc center
                double dir = Math.Sign(mp.AngleDeg); // +1 right turn, -1 left turn
                double xc = x - dir * R * Math.Sin(thetaRad);
                double yc = y + dir * R * Math.Cos(thetaRad);

                // New angle
                double thetaNew = thetaDeg + mp.AngleDeg;

                // Final position after arc
                double thetaRadNew = thetaNew * Math.PI / 180.0;
                double xNew = xc + dir * R * Math.Sin(thetaRadNew);
                double yNew = yc - dir * R * Math.Cos(thetaRadNew);

                return (xNew, yNew, thetaNew);
            }
        }
    }
}