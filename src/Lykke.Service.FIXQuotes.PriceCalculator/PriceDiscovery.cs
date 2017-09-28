namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    public class PriceDiscovery
    {
        private readonly double _threshold;
        private  VolatilityEstimator _volatilityEstimator;
        private double _latestVolatility; // holds result of the latest volatility estimation
        public Price LatestPrice { get; set; }

        /// <summary>
        /// Create a new instance of price discovery
        /// </summary>
        /// <param name="threshold">is size of the threshold used for in the Runner (in the intrinsic time)</param>
        public PriceDiscovery(double threshold)
        {
            _threshold = threshold;
            _volatilityEstimator = new VolatilityEstimator(_threshold);
        }

        public void Run(Price price)
        {
            _volatilityEstimator.Run(price);
            LatestPrice = price;
        }

        public void Finish(double dividend, double yearsToMaturity)
        {
            _volatilityEstimator.Finish();
            _latestVolatility = _volatilityEstimator.NormalizedVolatility;
            LatestCallStrike = AmericanOption.PriceCall(_latestVolatility, dividend, LatestPrice.FloatMid, yearsToMaturity);
            LatestPutStrike = AmericanOption.PricePut(_latestVolatility, dividend, LatestPrice.FloatMid, yearsToMaturity);
        }

        public void Reset()
        {
            _volatilityEstimator = new VolatilityEstimator(_threshold);
            LatestCallStrike = double.NaN;
            LatestPutStrike = double.NaN;
            LatestPrice = null;
        }

        public double GetLatestVolatility()
        {
            return _latestVolatility;
        }

        public double LatestCallStrike { get; private set; }

        public double LatestPutStrike { get; private set; }
    }
}