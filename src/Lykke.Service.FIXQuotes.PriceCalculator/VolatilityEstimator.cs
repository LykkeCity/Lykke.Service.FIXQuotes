using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    [Serializable]
    public sealed class VolatilityEstimator
    {
        private const int PricesCountThreshold = 5;
        private readonly DcOs _dcOs;
        private double _sqrtOsDeviation;
        private readonly double _ticksInYear = TimeSpan.FromDays(365).Ticks;
        private long _runCounter;


        public VolatilityEstimator(double threshold)
        {
            TimeFirstPrice = TimeLastPrice = 0L;
            _dcOs = new DcOs(threshold, threshold, -1, threshold, threshold, true);
        }

        public void Run(Price aPrice)
        {
            var @event = _dcOs.Run(aPrice);
            if (@event == 1 || @event == -1)
            {
                _sqrtOsDeviation += _dcOs.ComputeSqrtOsDeviation();
            }
            if (TimeFirstPrice == 0)
            {
                TimeFirstPrice = aPrice.Time;
            }
            TimeLastPrice = aPrice.Time;
            _runCounter++;
        }

        public double TotalVolatility => Math.Sqrt(_sqrtOsDeviation);

        public double NormalizedVolatility
        {
            get
            {
                if (_runCounter < PricesCountThreshold)
                {
                    throw new InvalidOperationException("Not enough prices to calculate volatility");
                }
                var coef = _ticksInYear / (TimeLastPrice - TimeFirstPrice);
                return TotalVolatility * Math.Sqrt(coef);
            }
        }

        public long TimeFirstPrice { get; private set; }

        public long TimeLastPrice { get; private set; }
    }
}