// See https://aka.ms/new-console-template for more information
using NorsynHydraulicCalc;

using System.Diagnostics;

HydraulicCalc hc = new HydraulicCalc(
    "Fordelingsledning",
    50, //Total heating demand
    1, //Number of clients
    1, //Number of units
    50, //Hot water return temp
    0.6, //FactorTillægForOpvarmningUdenBrugsvandsprioritering
    0.5, //MinDifferentialPressureOverHovedHaner

    110, //TempFremFL
    60, //TempReturFL
    1, //FactorVarmtVandsTillægFL
    2000, //NyttetimerOneUserFL
    2800, //Nyttetimer50PlusUsersFL
    1.5, //AcceptVelocity20_150FL
    2.5, //AcceptVelocity200_300FL
    3, //AcceptVelocity300PlusFL
    100, //AcceptPressureGradient20_150FL
    100, //AcceptPressureGradient200_300FL
    120, //AcceptPressureGradient300PlusFL
    true, //UsePertFlextraFL
    75, //PertFlextraMaxDnFL

    110, //TempFremSL
    60, //TempReturSL
    1, //FactorVarmtVandsTillægSL
    2000, //NyttetimerOneUserSL
    "AluPEX", //PipeTypeSL
    1, //AcceptVelocityFlexibleSL
    1.5, //AcceptVelocity20_150SL
    600, //AcceptPressureGradientFlexibleSL
    600, //AcceptPressureGradient20_150SL
    0.3, //MaxPressureLossStikSL
    "CW"
    );

hc.Calculate();

HydraulicCalc hc2 = new HydraulicCalc(
    "Fordelingsledning",
    80, //Total heating demand
    1, //Number of clients
    1, //Number of units
    50, //Hot water return temp
    0.6, //FactorTillægForOpvarmningUdenBrugsvandsprioritering
    0.5, //MinDifferentialPressureOverHovedHaner

    110, //TempFremFL
    60, //TempReturFL
    1, //FactorVarmtVandsTillægFL
    2000, //NyttetimerOneUserFL
    2800, //Nyttetimer50PlusUsersFL
    1.5, //AcceptVelocity20_150FL
    2.5, //AcceptVelocity200_300FL
    3, //AcceptVelocity300PlusFL
    100, //AcceptPressureGradient20_150FL
    100, //AcceptPressureGradient200_300FL
    120, //AcceptPressureGradient300PlusFL
    true, //UsePertFlextraFL
    75, //PertFlextraMaxDnFL

    110, //TempFremSL
    60, //TempReturSL
    1, //FactorVarmtVandsTillægSL
    2000, //NyttetimerOneUserSL
    "AluPEX", //PipeTypeSL
    1, //AcceptVelocityFlexibleSL
    1.5, //AcceptVelocity20_150SL
    600, //AcceptPressureGradientFlexibleSL
    600, //AcceptPressureGradient20_150SL
    0.3, //MaxPressureLossStikSL
    "CW"
    );

hc2.Calculate();

#region Test calculation
//const double roughness = 0.0001;
//const double density = 951;
//const double viscosity = 0.0002546; //dynamic
//double tolerance = 1e-6;
//double maxIterations = 100;
//double pressureGradient = 100;

//double diameter = 0.0703;
//double flowRate = 11.60422 / 3600;

//// Step 1: Calculate flow velocity
//double area = Math.PI * Math.Pow(diameter, 2) / 4.0;
//double velocity = flowRate / area;

//// Step 2: Calculate Reynolds number
//double reynolds = (density * velocity * diameter) / viscosity;

//// Step 3: Solve Colebrook-White equation iteratively
//double frictionFactor = ColebrookWhiteOld(reynolds, roughness, diameter);

//// Step 4: Calculate pressure loss gradient using Darcy-Weisbach equation
//double pressureLossGradient = FrictionLoss(frictionFactor, density, velocity, diameter);

//Console.WriteLine("Pressure Loss Gradient: " + pressureLossGradient + " Pa/m");

//// Step 1: Guess initial flow velocity (assume an initial guess for Re or velocity)
//double initialVelocity = 0.2; // Guess initial velocity in m/s

//// Step 2: Perform iteration using Tkachenko-Mielkovskyi method
//double flowRateTkachenko = IterativelySolveFlowRate(initialVelocity, diameter, pressureGradient, density, viscosity, roughness, tolerance, maxIterations, useColebrook: false);

//// Step 3: Perform iteration using Colebrook-White method
//double flowRateColebrook = IterativelySolveFlowRate(initialVelocity, diameter, pressureGradient, density, viscosity, roughness, tolerance, maxIterations, useColebrook: true);

//Console.WriteLine("Flow Rate using Tkachenko-Mielkovskyi: " + flowRateTkachenko * 3600.0 + " m^3/h"); // Convert to m^3/h
//Console.WriteLine("Flow Rate using Colebrook-White: " + flowRateColebrook * 3600.0 + " m^3/h"); // Convert to m^3/h

//// Iteratively solves for the correct velocity and flow rate using a convergence approach
//static double IterativelySolveFlowRate(double initialVelocity, double diameter, double pressureGradient, double density, double viscosity, double roughness, double tolerance, double maxIterations, bool useColebrook)
//{
//    double velocity = initialVelocity;
//    double reynolds = 0, frictionFactor, newVelocity;
//    int iteration = 0;

//    while (iteration < maxIterations)
//    {
//        // Calculate Reynolds number for current velocity
//        reynolds = CalculateReynolds(velocity, diameter, density, viscosity);

//        // Calculate relative roughness
//        double relativeRoughness = roughness / diameter;

//        // Calculate friction factor using the chosen method
//        if (useColebrook)
//        {
//            frictionFactor = ColebrookWhite(reynolds, relativeRoughness);
//        }
//        else
//        {
//            frictionFactor = CalculateFrictionFactorTkachenkoMielkovskyi(reynolds, relativeRoughness);
//        }

//        // Update velocity based on Darcy-Weisbach equation and pressure gradient
//        newVelocity = SolveVelocityFromPressureGradient(pressureGradient, frictionFactor, density, diameter);

//        // Check for convergence
//        if (Math.Abs(newVelocity - velocity) < tolerance)
//        {
//            break; // Converged
//        }

//        // Update velocity for next iteration
//        velocity = newVelocity;
//        iteration++;
//    }

//    Console.WriteLine($"Re {reynolds}");

//    // Calculate the final flow rate based on the converged velocity
//    return CalculateFlowRate(velocity, diameter);
//}

//// Method to calculate the Reynolds number
//static double CalculateReynolds(double velocity, double diameter, double density, double viscosity)
//{
//    return (density * velocity * diameter) / viscosity;
//}

//// Colebrook-White solver using Newton's method
//static double ColebrookWhiteOld(double reynolds, double roughness, double diameter)
//{
//    double tolerance = 1e-6; // tolerance for iterative solution
//    double f = 0.02; // initial guess for friction factor

//    for (int i = 0; i < 100; i++)
//    {
//        double lhs = 1.0 / Math.Sqrt(f);
//        double rhs = -2.0 * Math.Log10((roughness / (3.7 * diameter)) + (2.51 / (reynolds * Math.Sqrt(f))));

//        double f_new = 1.0 / Math.Pow((lhs - rhs), 2);

//        if (Math.Abs(f_new - f) < tolerance) return f_new;

//        f = f_new;
//    }

//    return f; // Return friction factor
//}
//// Colebrook-White implicit solver using Newton's method
//static double ColebrookWhite(double Re, double relativeRoughness)
//{
//    double tolerance = 1e-6; // tolerance for iterative solution
//    double f = 0.02; // initial guess for friction factor

//    for (int i = 0; i < 100; i++)
//    {
//        double lhs = 1.0 / Math.Sqrt(f);
//        double rhs = -2.0 * Math.Log10((
//            relativeRoughness / 3.7) + (2.51 / (Re * Math.Sqrt(f))));

//        double f_new = 1.0 / Math.Pow((lhs - rhs), 2);

//        if (Math.Abs(f_new - f) < tolerance)
//            return f_new;

//        f = f_new;
//    }

//    return f; // Return friction factor
//}

//static double CalculateFrictionFactorTkachenkoMielkovskyi(double Re, double relativeRoughness)
//{
//    double f0inverseHalf = -0.79638 * Math.Log(relativeRoughness / 8.208 + 7.3357 / Re);
//    double f0 = Math.Pow(f0inverseHalf, -2);

//    double a1 = Re * relativeRoughness + 9.3120665 * f0inverseHalf;

//    double term1 = (8.128943 + a1) / (8.128943 * f0inverseHalf - 0.86859209 * a1 * Math.Log(a1 / (3.7099535 * Re)));
//    double f = Math.Pow(term1, 2);

//    return f;
//}

//// Darcy-Weisbach equation for pressure loss gradient
//static double FrictionLoss(double frictionFactor, double density, double velocity, double diameter)
//{
//    return frictionFactor * (density * Math.Pow(velocity, 2)) / (2 * diameter);
//}

//static double SolveVelocityFromPressureGradient(double pressureGradient, double frictionFactor, double density, double diameter)
//{
//    // Darcy-Weisbach equation rearranged to solve for velocity
//    return Math.Sqrt((2 * pressureGradient * diameter) / (frictionFactor * density));
//}

//// Calculate flow rate based on velocity and pipe diameter
//static double CalculateFlowRate(double velocity, double diameter)
//{
//    double area = Math.PI * Math.Pow(diameter, 2) / 4.0; // Cross-sectional area
//    return velocity * area; // Flow rate in m^3/s
//}
#endregion

