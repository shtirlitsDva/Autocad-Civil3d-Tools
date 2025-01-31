using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Numerics;

namespace DimensioneringV2.GraphUtilities
{
    public struct BigRational : IEquatable<BigRational>
    {
        public BigInteger Numerator { get; private set; }
        public BigInteger Denominator { get; private set; }

        public BigRational(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero)
                throw new DivideByZeroException("Denominator cannot be zero.");

            // Normalize sign so that Denominator is always positive
            if (denominator.Sign < 0)
            {
                numerator = -numerator;
                denominator = -denominator;
            }

            // Reduce fraction
            BigInteger gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
            Numerator = numerator / gcd;
            Denominator = denominator / gcd;
        }

        public static BigRational FromInt64(long value)
        {
            return new BigRational(value, 1);
        }

        public static BigRational FromBigInteger(BigInteger value)
        {
            return new BigRational(value, BigInteger.One);
        }

        // Addition
        public static BigRational operator +(BigRational a, BigRational b)
        {
            // a/b + c/d = (ad + bc) / bd
            BigInteger numerator = a.Numerator * b.Denominator + b.Numerator * a.Denominator;
            BigInteger denominator = a.Denominator * b.Denominator;
            return new BigRational(numerator, denominator);
        }

        // Subtraction
        public static BigRational operator -(BigRational a, BigRational b)
        {
            BigInteger numerator = a.Numerator * b.Denominator - b.Numerator * a.Denominator;
            BigInteger denominator = a.Denominator * b.Denominator;
            return new BigRational(numerator, denominator);
        }

        // Multiplication
        public static BigRational operator *(BigRational a, BigRational b)
        {
            BigInteger numerator = a.Numerator * b.Numerator;
            BigInteger denominator = a.Denominator * b.Denominator;
            return new BigRational(numerator, denominator);
        }

        // Division
        public static BigRational operator /(BigRational a, BigRational b)
        {
            if (b.Numerator.IsZero)
                throw new DivideByZeroException();

            BigInteger numerator = a.Numerator * b.Denominator;
            BigInteger denominator = a.Denominator * b.Numerator;
            return new BigRational(numerator, denominator);
        }

        // Negation
        public static BigRational operator -(BigRational a)
        {
            return new BigRational(-a.Numerator, a.Denominator);
        }

        // Convert to BigInteger if the fraction is actually an integer
        // (i.e. denominator == 1).
        public BigInteger ToBigInteger()
        {
            if (Denominator != BigInteger.One)
                throw new InvalidOperationException("Fraction is not an integer.");
            return Numerator;
        }

        // Zero & One
        public static BigRational Zero => new BigRational(BigInteger.Zero, BigInteger.One);
        public static BigRational One => new BigRational(BigInteger.One, BigInteger.One);

        // Comparisons & equality
        public bool Equals(BigRational other)
        {
            return this.Numerator.Equals(other.Numerator)
                && this.Denominator.Equals(other.Denominator);
        }
        public override bool Equals(object obj)
        {
            return obj is BigRational br && Equals(br);
        }
        public override int GetHashCode()
        {
            return HashCode.Combine(Numerator, Denominator);
        }
    }
}