using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    /**
     * The main inspiration was found in the paper "American Options on Assets with Dividends Near Expiry" by J. D. Evans et. al.
     * We use the eq. 1.6, hence equations 1.5 and 1.7 could also be used depending on the required properties.
     */

    public class AmericanOption
    {
        //    private int _type; // 1 for a Call and -1 for a Short option
        public static double PriceCall(double volatility, double dividents, double strikePrice, double yearsToMaturity)
        {
            return strikePrice * (1 + volatility * Math.Sqrt(2 * (yearsToMaturity) * Math.Log(1 / (4 * Math.Sqrt(3.14159) * dividents * yearsToMaturity))));
        }

        public static double PricePut(double volatility, double dividents, double strikePrice, double yearsToMaturity)
        {
            return strikePrice * (1 - volatility * Math.Sqrt(2 * (yearsToMaturity) * Math.Log(1 / (4 * Math.Sqrt(3.14159) * dividents * yearsToMaturity))));
        }


    }
}
