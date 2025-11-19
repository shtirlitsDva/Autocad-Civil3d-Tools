# C# Elbow Transition Geometry – Full Explanation

Below is a C# version of the *math you’ve already validated* (the φ solver and Cy), plus a clean structure you can plug into your own coordinate system and pipe model.

I am **not** hard-coding a specific global layout for all the 3D points, because that depends on how your program represents directions/planes and where you place the elbows relative to your existing pipes. Instead I give:

- A precise solver for φ (roll / elbow2 angle) and Cy.
- A small set of geometry helpers to compute tangent points once you choose directions.
- A suggested way to use them for your case (p1 along +X, p2 along +Y).

You can then wire this into your existing geometry engine (which already knows where your “crossing” is, how you trim pipes, etc.).

---

## 1. Core math: solve φ and Cy

This matches what we’ve been using and validating:

- Given:
  - `H`  = vertical drop from p1 centerline to p2 centerline  
  - `R`  = elbow radius (R1 = R2)
- We solve:
  \[
    H = R \sin\varphi + R (1 - \cos\varphi)
  \]
- And then:
  \[
    \theta_2 = \varphi, \quad
    C_y = R \cos\varphi + R \sin\varphi
  \]

```csharp
using System;
using System.Numerics;

public static class ElbowTransitionSolver
{
    public sealed class ElbowSolution
    {
        /// <summary>Roll angle of elbow 1 (and included angle of elbow 2), radians.</summary>
        public double PhiRad { get; init; }

        /// <summary>Roll angle of elbow 1 (and included angle of elbow 2), degrees.</summary>
        public double PhiDeg => PhiRad * 180.0 / Math.PI;

        /// <summary>Offset along pipe2 direction from the local origin (Cy, same units as H and R).</summary>
        public double Cy { get; init; }
    }

    /// <summary>
    /// Solve for φ in [0, π/2] given vertical drop H and elbow radius R (R1 = R2 = R).
    /// Uses: H = R * (sin φ + 1 - cos φ).
    /// Returns φ and Cy = R * (cos φ + sin φ).
    /// </summary>
    public static ElbowSolution Solve(double H, double R, double tolerance = 1e-12, int maxIter = 80)
    {
        if (H < 0.0)
            throw new ArgumentOutOfRangeException(nameof(H), "H must be non-negative.");
        if (R <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(R), "R must be > 0.");

        double Hmax = 2.0 * R; // R + R, since R1 = R2
        if (H > Hmax)
            throw new InvalidOperationException(
                $"No geometric solution: H must be between 0 and R1 + R2 = {Hmax:0.###}."
            );

        // f(φ) = R * (sin φ + 1 - cos φ) - H
        double F(double phi) =>
            R * (Math.Sin(phi) + 1.0 - Math.Cos(phi)) - H;

        double a = 0.0;
        double b = 0.5 * Math.PI; // 90°
        double fa = F(a);
        double fb = F(b);

        if (Math.Abs(fa) < tolerance)
            return FromPhi(a, R);
        if (Math.Abs(fb) < tolerance)
            return FromPhi(b, R);

        if (fa * fb > 0.0)
            throw new InvalidOperationException(
                "Could not bracket a root numerically on [0, π/2]. " +
                "Check H and R or perturb H slightly."
            );

        double phi = 0.0;
        for (int i = 0; i < maxIter; i++)
        {
            phi = 0.5 * (a + b);
            double fm = F(phi);

            if (Math.Abs(fm) < tolerance)
                break;

            if (fa * fm <= 0.0)
            {
                b = phi;
                fb = fm;
            }
            else
            {
                a = phi;
                fa = fm;
            }
        }

        return FromPhi(phi, R);
    }

    private static ElbowSolution FromPhi(double phi, double R)
    {
        double cy = R * (Math.Cos(phi) + Math.Sin(phi));
        return new ElbowSolution
        {
            PhiRad = phi,
            Cy = cy
        };
    }
}
```

### Quick numerical check (your cases)

You can verify in a debugger or small console app:

```csharp
// Case 1: H = 22.94, R = 38
var s1 = ElbowTransitionSolver.Solve(22.94, 38);
Console.WriteLine($"φ = {s1.PhiDeg:F8}°, Cy = {s1.Cy:F5}");

// Case 2: H = 132.05, R = 305
var s2 = ElbowTransitionSolver.Solve(132.05, 305);
Console.WriteLine($"φ = {s2.PhiDeg:F8}°, Cy = {s2.Cy:F5}");
```

You should see φ ≈ 28.74° and φ ≈ 21.3615° respectively (up to normal rounding).

---

## 2. Generic 3D helpers for elbow points

Now, to compute the start/end/tangent points once you know φ and R, it is safer to work with generic vector helpers that you can plug your own directions into.

### 2.1. Simple 3D point type

If you already use your own, replace this with that.

```csharp
public readonly struct Point3D
{
    public readonly double X;
    public readonly double Y;
    public readonly double Z;

    public Point3D(double x, double y, double z)
    {
        X = x; Y = y; Z = z;
    }

    public static Point3D operator +(Point3D p, Vector3 v) =>
        new Point3D(p.X + v.X, p.Y + v.Y, p.Z + v.Z);

    public static Vector3 operator -(Point3D a, Point3D b) =>
        new Vector3((float)(a.X - b.X),
                    (float)(a.Y - b.Y),
                    (float)(a.Z - b.Z));
}
```

### 2.2. Tangent-point utility for a circular elbow

For a *single* elbow between two straight pipes:

- Let:
  - `PStart`   = elbow start point (centerline).
  - `dStart`   = unit tangent direction of the pipe at start (pointing away from elbow).
  - `PEnd`     = elbow end point (centerline).
  - `dEnd`     = unit tangent direction at end (pointing away from elbow).
  - `R`        = elbow radius.

Then, if the geometry is correct, the intersection of the two straight tangents is:

\[
T = P_{start} + R \, d_{start} = P_{end} + R \, d_{end}
\]

You can compute it as:

```csharp
public static class ElbowGeometry
{
    /// <summary>
    /// Compute the intersection point of the extended tangents for a single elbow.
    /// Assumes ideal geometry: T = PStart + R*dStart = PEnd + R*dEnd.
    /// </summary>
    public static Point3D ComputeTangentIntersection(
        Point3D pStart,
        Vector3 dStart, // must be unit length
        Point3D pEnd,
        Vector3 dEnd,   // must be unit length
        double R)
    {
        var t1 = pStart + dStart * (float)R;
        var t2 = pEnd   + dEnd   * (float)R;

        // In ideal math t1 == t2. In practice there may be small differences.
        // You can choose to average them:
        return new Point3D(
            0.5 * (t1.X + t2.X),
            0.5 * (t1.Y + t2.Y),
            0.5 * (t1.Z + t2.Z)
        );
    }
}
```

---

## 3. How to use this for your specific case

You described:

- `pipe1StartPoint = (0, 0, zp1s)`; `H = zp1s`.
- p1 along +X.
- p2 along +Y, ends at `pipe2EndPoint = (x2, y2, 0)`.
- R1 = R2 = R.
- Elbow1 is 90°.
- Elbow2 angle = φ (from solver).
- You want:

  1. `pipe1EndPoint` (where straight p1 stops and elbow1 starts).
  2. `pipe2StartPoint` (where elbow2 ends and straight p2 begins).
  3. For each elbow: start, end, tangent point.

Because your existing code already knows where the “crossing” between p1 and p2 is in plan, the *cleanest* pattern is:

1. Choose a local coordinate system around the crossing in your CAD (you probably already have this).
2. Use `ElbowTransitionSolver.Solve(H, R)` to get:
   - `phi = solution.PhiRad`
   - `Cy  = solution.Cy`
3. In that local coordinate system:

   - Proceed exactly as you did in CAD when you solved graphically, but use φ from the solver.
   - Use `ElbowGeometry.ComputeTangentIntersection` to compute 3a/3c and 4a/4c once you have the start/end points and directions.

Concretely, one simple convention you can adopt locally is:

- Local frame:
  - p1: along +X, at z = H, y = 0.
  - p2: along +Y, at z = 0, x = 0.
- Then the natural local points are:

  - `pipe1EndLocal` (= elbow1 start) at `(-R, 0, H)`  
    (cut p1 back by R).
  - `pipe2StartLocal` (= elbow2 end) at `(0, Cy, 0)`  
    (from the math above).

Once you’re happy with that, you can:

- Transform these local points into your actual global coordinates (translation + any rotation you need).
- Use them as:
  - `pipe1EndPoint = Transform(pipe1EndLocal)`
  - `elbow1StartPoint = pipe1EndPoint`
  - `elbow2EndPoint = pipe2StartPoint = Transform(pipe2StartLocal)`
- Compute tangent points via `ComputeTangentIntersection` using the correct direction vectors:
  - For elbow1: `dStart = +X_local`, `dEnd = direction of intermediate leg` (which you already know from your CAD layout or can define from the plane you use for the elbows).
  - For elbow2: `dStart = same intermediate-leg direction`, `dEnd = +Y_local`.

Because the precise 3D orientation of the intermediate leg between elbow1 and elbow2 depends on the plane in which you want to place the elbows in your model, it’s better that you choose that plane and direction explicitly in your code and then use the generic helpers above.

---

## 4. Summary of what is ready to drop in

- `ElbowTransitionSolver.Solve(H, R)`  
  → returns:
  - `PhiRad, PhiDeg` (roll of elbow1 and angle of elbow2)
  - `Cy` (offset along p2 from your chosen local origin).

- `Point3D` struct and `ElbowGeometry.ComputeTangentIntersection`  
  → let you compute tangent points once you have defined:
  - Start/end points of each elbow in your coordinate system.
  - Tangent directions at each end.

If you show me how you currently define the elbow plane (normal vector) and the intermediate leg direction in your code, I can give you a fully concrete `ComputeLayout(...)` that returns all the 3D points (1–4c) directly in your global coordinates.
