using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    [Serializable]
    public sealed class Price
    {

        public double Ask { get; }

        public double Bid { get; }

        public long Time { get; }

        public double Mid => (Bid + Ask) / 2.0;

        public double Spread => Ask - Bid;

        public Price(double bid, double ask, long time)
        {
            Bid = bid;
            Ask = ask;
            Time = time;
        }

        public Price(double bid, double ask, DateTime time) : this(bid, ask, time.Ticks)
        {

        }

    }
}