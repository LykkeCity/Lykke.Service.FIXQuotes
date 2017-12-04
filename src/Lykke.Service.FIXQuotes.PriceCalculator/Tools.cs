using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace Lykke.Service.FIXQuotes.PriceCalculator
{
    public class Tools
    {
        private static readonly double TOLERANCE = 0.0000001d;

        /**
    * The function checks if the certain directory exists and create it if it does not.
    * @param dirName is the name of the directory to check
    * @return true if the directory was created
    */
        public static bool CheckDirectory(string dirName)
        {

            if (!Directory.Exists(dirName))
            {
                try
                {
                    Directory.CreateDirectory(dirName);
                }
                catch (SecurityException se)
                {
                    Console.WriteLine(se);
                    return false;
                }
            }
            return true;
        }

        /**
         * The function GenerateLogSpace generates a list of Logarithmically distributed values.
         * @param min
         * @param max
         * @param nBins
         * @return array of Logarithmically distributed floats
         */
        public static double[] GenerateLogSpace(float min, float max, int nBins)
        {
            var logList = new double[nBins];
            var m = 1.0f / (nBins - 1);
            var quotient = (float)Math.Pow(max / min, m);
            logList[0] = min;
            for (var i = 1; i < nBins; i++)
            {
                logList[i] = logList[i - 1] * quotient;
            }
            return logList;
        }

        /**
         * The function returns a random value which is correlated with the random input value. The formula has been taken from the
         * https://quant.stackexchange.com/questions/24472/two-correlated-brownian-motions.
         * @param inputRand is the input value which you want to use as the original value.
         * @param corrCoeff is the size of the correlation coeff.
         * @return correlated value.
         */
        public static double GetCorrelatedRandom(double inputRand, double corrCoeff)
        {
            var random = new Random();
            return corrCoeff * inputRand + Math.Sqrt(1 - corrCoeff * corrCoeff) * random.NextDouble();//HACK expected Gaussian
        }

        /**
         * The function computes parameters of the simple linear regression. It takes two arrays (X and Y) and returns an
         * array of three parameters: slope, intersection, correlation coefficient.
         * @param setX is input x
         * @param setY is input y
         * @return float array: slope, intersection, r-Sqrt (error)
         */
        public static double[] LinearRegression(double[] setX, double[] setY)
        {
            var sumx = 0.0; // sum { x[i],      i = 1..n }
            var sumy = 0.0; // sum { y[i],      i = 1..n }
            var sumx2 = 0.0; // sum { x[i]*x[i], i = 1..n }
            var sumy2 = 0.0; // sum { y[i]*y[i], i = 1..n }
            var sumxy = 0.0; // sum { x[i]*y[i], i = 1..n }
            var lenInput = setX.Length;
            // read data and compute statistics
            for (var i = 0; i < lenInput; i++)
            {
                sumx += setX[i];
                sumy += setY[i];
                sumx2 += setX[i] * setX[i];
                sumy2 += setY[i] * setY[i];
                sumxy += setX[i] * setY[i];
            }
            var slope = (lenInput * sumxy - sumx * sumy) / (lenInput * sumx2 - sumx * sumx);
            var intersect = (sumy - slope * sumx) / lenInput;
            var rSqrt =
                Math.Pow(
                    (lenInput * sumxy - sumx * sumy) /
                    Math.Sqrt((lenInput * sumx2 - sumx * sumx) * (lenInput * sumy2 - sumy * sumy)), 2);

            var toReturn = new double[3];
            toReturn[0] = slope;
            toReturn[1] = intersect;
            toReturn[2] = rSqrt;

            return toReturn;
        }

        /**
         * The function to compute Log values of an array.
         * @param inputArray is what you want to convert to Log values
         * @return array with Logs of the initial values
         */
        public static double[] ToLog(double[] inputArray)
        {
            var logArray = new double[inputArray.Length];
            for (var i = 0; i < inputArray.Length; i++)
            {
                if (Math.Abs(inputArray[i]) > TOLERANCE)
                {
                    logArray[i] = Math.Log(inputArray[i]);
                }
            }
            return logArray;
        }

        /**
         * The function is dedicated to the computation of the scaling law parameters. These parameters are C, E and
         * R-squared. The used formula was found in the page 13 of the "Patterns in high-frequency FX data: Discovery
         * of 12 empirical scaling laws J.B.". IMPORTANT! For the scaling laws here we use 0.01 to define 1%. In the
         * original article they use 1.0 for the same value. Therefore, the parameter C should be multiplied by 100
         * in order to have the comparable coefficients.
         * @param arrayX is array x values
         * @param arrayY is array y values
         * @return a double array of the following values: [C, E, r^2]
         */
        public static double[] ComputeScalingParams(double[] arrayX, double[] arrayY)
        {
            var linRegParam = LinearRegression(ToLog(arrayX), ToLog(arrayY));
            var @params = new double[3];
            var paramC = Math.Exp(-linRegParam[1] / linRegParam[0]);
            @params[0] = paramC;
            var paramE = linRegParam[0];
            @params[1] = paramE;
            var rSqrt = linRegParam[2];
            @params[2] = rSqrt;
            return @params;
        }


        /**
         * The method converts a String into a Date instance.
         * @param inputStringDate is the date in the String format, for example "1992.12.01 13:23:54.012"
         * @param dateFormat is the date format, for example "yyyy.MM.dd HH:mm:ss.SSS"
         * @return converted to the Date format
         */
        public static DateTime StringToDate(string inputStringDate, string dateFormat)
        {
            return DateTime.Parse(inputStringDate);
        }

        /**
         * This method should convert a string of information about price to the proper Price format. IMPORTANT: by default
         * the time of a price is supposed to be given in sec.
         * @param inputString is a string which describes a price. For example, "1.23,1.24,12213"
         * @param delimiter in the previous example the delimiter is ","
         * @param nDecimals is how many numbers a price has after the point. 2 in the example
         * @param dateFormat is the date format if any. Otherwise, one should write ""
         * @param askIndex index of the ask price in the string format
         * @param bidIndex index of the bid price in the string format
         * @param timeIndex index of the time in the string format
         * @return an instance Price
         */
        public static Price PriceLineToPrice(string inputString, char delimiter, string dateFormat,
            int askIndex, int bidIndex, int timeIndex)
        {
            var priceInfo = inputString.Split(delimiter);
            var bid = double.Parse(priceInfo[bidIndex]);
            var ask = double.Parse(priceInfo[askIndex]);
            long time;
            if (dateFormat.Equals(""))
            {
                time = long.Parse(priceInfo[timeIndex]) * 1000L;
            }
            else
            {
                time = StringToDate(priceInfo[timeIndex], dateFormat).Ticks ;
            }
            return new Price(bid, ask, time);
        }


        /**
         * The method computes cumulative value of the normal distribution with parameter x
         * @param x is the x coordinate of the normal distribution
         * @return sum of the cumulative normal distribution
         */
        public static double CumNorm(double x)
        {
            // protect against overflow
            if (x > 6.0)
                return 1.0;
            if (x < -6.0)
                return 0.0;
            var b1 = 0.31938153;
            var b2 = -0.356563782;
            var b3 = 1.781477937;
            var b4 = -1.821255978;
            var b5 = 1.330274429;
            var p = 0.2316419;
            var c2 = 0.3989423;
            var a = Math.Abs(x);
            var t = 1.0 / (1.0 + a * p);
            var b = c2 * Math.Exp((-x) * (x / 2.0));
            var n = ((((b5 * t + b4) * t + b3) * t + b2) * t + b1) * t;
            n = 1.0 - b * n;
            if (x < 0.0)
                n = 1.0 - n;
            return n;
        }


        /**
         * This method can be called to compute the size of a threshold which in average returns ExpectedNDCs in the given
         * timeInterval.
         * IMPORTANT: scaling law parameters (scalingLawParam) must be normalized on one year.
         * @param timeInterval is the size of the time interval, in milliseconds
         * @param ExpectedNDCs is how many DC intrinsic event would you like to see within the given interval (in average)
         * @param scalingLawParam is an array with at least two elements: [C, E, ...]
         * @return the size of the threshold which in average returns ExpectedNDCs in the given timeInterval.
         */
        public static double FindDCcountThreshold(long timeInterval, int expectedNdCs, double[] scalingLawParam)
        {
            return Math.Pow(expectedNdCs * 31557600000L / (double)timeInterval, (1.0 / scalingLawParam[1])) *
                   scalingLawParam[0];
        }


        /**
         * This methods returns median value of a List<Double>
         * @param inputList is an input List<Double>
         * @return median value of the list
         */
        public static double FindMedian(List<double> inputList)
        {
            inputList.Sort();
            double median;
            if (inputList.Count > 0)
            {
                if (inputList.Count % 2 == 0)
                {
                    median = (inputList[inputList.Count / 2] + inputList[inputList.Count / 2 - 1]) / 2;
                }
                else
                {
                    median = inputList[inputList.Count / 2];
                }
            }
            else
            {
                median = 0.0;
            }
            return median;
        }

        /**
         * This method returns the average value of an input List<Double>
         * @param inputList in an input List<Double>
         * @return the average value
         */
        public static double FindAverage(List<double> inputList)
        {
            var listLen = inputList.Count;
            if (listLen == 0)
            {
                return 0;
            }
            var sum = 0.0;
            foreach (var value in inputList)
            {
                sum += value;
            }
            return sum / listLen;
        }
    }
}
