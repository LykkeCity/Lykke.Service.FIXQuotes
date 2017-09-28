using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    public class VolatilityEstimator
    {
        private readonly DcOs _dcOs;
        private double _sqrtOsDeviation;
        private readonly long _msecInYear = TimeSpan.FromDays(365).Ticks;


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
        }

        public void Finish()
        {
            TotalVolatility = Math.Sqrt(_sqrtOsDeviation);
            NormalizeVolatility(TotalVolatility);
        }

        private void NormalizeVolatility(double totalVolatility)
        {
            var coef = (double)_msecInYear / (TimeLastPrice - TimeFirstPrice);
            NormalizedVolatility = totalVolatility * Math.Sqrt(coef);
        }

        public double TotalVolatility { get; private set; }

        public double NormalizedVolatility { get; private set; }

        public long TimeFirstPrice { get; private set; }

        public long TimeLastPrice { get; private set; }
    }
}