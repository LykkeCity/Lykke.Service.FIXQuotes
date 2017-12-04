using System;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    [Serializable]
    public sealed class DcOs
    {
        private double _extreme;
        private double _prevExtreme;
        private double _thresholdUp; // 1% == 0.01
        private double _thresholdDown;
        private double _osSizeUp;
        private double _osSizeDown;
        private int _mode; // +1 for expected upward DC, -1 for expected downward DC
        private bool _initialized;
        private double _reference;
        private double _latestDCprice; // this is the price of the latest registered DC IE
        private double _prevDCprice; // this is the price of the DC IE before the latest one
        private readonly bool _relativeMoves; // shows if the algorithm should compute relative of absolute price changes
        private double _osL; // is length of the previous overshoot

        public DcOs(double thresholdUp, double thresholdDown, int initialMode, double osSizeUp, double osSizeDown,
            bool relativeMoves)
        {
            _initialized = false;
            _thresholdUp = thresholdUp;
            _thresholdDown = thresholdDown;
            _mode = initialMode;
            _osSizeUp = osSizeUp;
            _osSizeDown = osSizeDown;
            _relativeMoves = relativeMoves;
        }

        public DcOs(double thresholdUp, double thresholdDown, int initialMode, double osSizeUp, double osSizeDown, Price initPrice, bool relativeMoves)
        {
            _initialized = true;
            _thresholdUp = thresholdUp;
            _thresholdDown = thresholdDown;
            _mode = initialMode;
            _osSizeUp = osSizeUp;
            _osSizeDown = osSizeDown;
            _extreme = _prevExtreme = _reference =
                _prevDCprice = _latestDCprice = (_mode == 1 ? initPrice.Ask : initPrice.Bid);

        }

        public int Run(Price aPrice)
        {
            if (_relativeMoves)
            {
                return RunRelative(aPrice);
            }
            return RunAbsolute(aPrice);
        }

        /**
         * Uses log function in order to compute a price move
         * @param aPrice is a new price
         * @return +1 or -1 in case of Directional-Change (DC) Intrinsic Event (IE) upward or downward and also +2 or -2
         * in case of an Overshoot (OS) IE upward or downward. Otherwise, returns 0;
         */
        private int RunRelative(Price aPrice)
        {
            if (!_initialized)
            {
                _initialized = true;
                _extreme = _prevExtreme =
                    _reference = _prevDCprice = _latestDCprice = (_mode == 1 ? aPrice.Ask : aPrice.Bid);
            }
            else
            {
                if (_mode == 1)
                {
                    if (aPrice.Ask < _extreme)
                    {
                        _extreme = aPrice.Ask;
                        if (-Math.Log(_extreme / _reference) >= _osSizeDown)
                        {
                            _reference = _extreme;
                            return -2;
                        }
                        return 0;
                    }
                    if (Math.Log(aPrice.Bid / _extreme) >= _thresholdUp)
                    {
                        _osL = -Math.Log(_extreme / _latestDCprice);
                        _prevDCprice = _latestDCprice;
                        _latestDCprice = aPrice.Bid;
                        _prevExtreme = _extreme;
                        _extreme = _reference = aPrice.Bid;
                        _mode *= -1;
                        return 1;
                    }
                }
                else if (_mode == -1)
                {
                    if (aPrice.Bid > _extreme)
                    {
                        _extreme = aPrice.Bid;
                        if (Math.Log(_extreme / _reference) >= _osSizeUp)
                        {
                            _reference = _extreme;
                            return 2;
                        }
                        return 0;
                    }
                    if (-Math.Log(aPrice.Ask / _extreme) >= _thresholdDown)
                    {
                        _osL = Math.Log(_extreme / _latestDCprice);
                        _prevDCprice = _latestDCprice;
                        _latestDCprice = aPrice.Ask;
                        _prevExtreme = _extreme;
                        _extreme = _reference = aPrice.Ask;
                        _mode *= -1;
                        return -1;
                    }
                }
            }

            return 0;
        }

        /**
         * Uses absolute values a price move
         * @param aPrice is a new price
         * @return +1 or -1 in case of Directional-Change (DC) Intrinsic Event (IE) upward or downward and also +2 or -2
         * in case of an Overshoot (OS) IE upward or downward. Otherwise, returns 0;
         */
        private int RunAbsolute(Price aPrice)
        {
            if (!_initialized)
            {
                _initialized = true;
                _extreme = _prevExtreme =
                    _reference = _prevDCprice = _latestDCprice = (_mode == 1 ? aPrice.Ask : aPrice.Bid);
            }
            else
            {
                if (_mode == 1)
                {
                    if (aPrice.Ask < _extreme)
                    {
                        _extreme = aPrice.Ask;
                        if (-(_extreme - _reference) >= _osSizeDown)
                        {
                            _reference = _extreme;
                            return -2;
                        }
                        return 0;
                    }
                    if (aPrice.Bid - _extreme >= _thresholdUp)
                    {
                        _osL = -(_extreme - _latestDCprice);
                        _prevDCprice = _latestDCprice;
                        _latestDCprice = aPrice.Bid;
                        _prevExtreme = _extreme;
                        _extreme = _reference = aPrice.Bid;
                        _mode *= -1;
                        return 1;
                    }
                }
                else if (_mode == -1)
                {
                    if (aPrice.Bid > _extreme)
                    {
                        _extreme = aPrice.Bid;
                        if (_extreme - _reference >= _osSizeUp)
                        {
                            _reference = _extreme;
                            return 2;
                        }
                        return 0;
                    }
                    if (-(aPrice.Ask - _extreme) >= _thresholdDown)
                    {
                        _osL = (_extreme - _latestDCprice);
                        _prevDCprice = _latestDCprice;
                        _latestDCprice = aPrice.Ask;
                        _prevExtreme = _extreme;
                        _extreme = _reference = aPrice.Ask;
                        _mode *= -1;
                        return -1;
                    }
                }
            }

            return 0;
        }

        /**
         * The function computes OS deviation defined as a squared difference between the size of overshoot and
         * correspondent threshold. Details can be found in "Bridging the gap between physical and intrinsic time"
         * @return double variability of an overshoot.
         */
        public double ComputeSqrtOsDeviation()
        {
            double sqrtOsDeviation;
            if (_mode == 1)
            {
                sqrtOsDeviation = Math.Pow(_osL - _thresholdUp, 2);
            }
            else
            {
                sqrtOsDeviation = Math.Pow(_osL - _thresholdDown, 2);
            }
            return sqrtOsDeviation;
        }

        public double GetOsL()
        {
            return _osL;
        }

        public double GetLatestDCprice()
        {
            return _latestDCprice;
        }

        public double GetPrevDCprice()
        {
            return _prevDCprice;
        }

        public double GetExtreme()
        {
            return _extreme;
        }

        public double GetPrevExtreme()
        {
            return _prevExtreme;
        }

        public void SetExtreme(long extreme)
        {
            _extreme = extreme;
        }

        public double GetThresholdUp()
        {
            return _thresholdUp;
        }

        public void SetThresholdUp(float thresholdUp)
        {
            _thresholdUp = thresholdUp;
        }

        public double GetThresholdDown()
        {
            return _thresholdDown;
        }

        public void SetThresholdDown(float thresholdDown)
        {
            _thresholdDown = thresholdDown;
        }

        public double GetOsSizeUp()
        {
            return _osSizeUp;
        }

        public void SetOsSizeUp(float osSizeUp)
        {
            _osSizeUp = osSizeUp;
        }

        public double GetOsSizeDown()
        {
            return _osSizeDown;
        }

        public void SetOsSizeDown(float osSizeDown)
        {
            _osSizeDown = osSizeDown;
        }

        public int GetMode()
        {
            return _mode;
        }

        public void SetMode(int mode)
        {
            _mode = mode;
        }

        public bool IsInitialized()
        {
            return _initialized;
        }

        public void SetInitialized(bool initialized)
        {
            _initialized = initialized;
        }

        public double GetReference()
        {
            return _reference;
        }

        public void SetReference(long reference)
        {
            _reference = reference;
        }

    }
}