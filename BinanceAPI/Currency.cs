using System;
using System.Linq;
using System.Collections.Generic;
using Binance.API.Csharp.Client.Models.Enums;
using Binance.API.Csharp.Client;
using static Binance.API.Csharp.Client.Models.WebSocket.KlineMessage;

namespace BinanceAPI
{
    public class Currency
    {
        internal Currency()
        {
            Name = string.Empty;
            Price = -1;
            PurchaseValue = -1;
            CurrentValue = -1;
            HeldPositions = new Dictionary<decimal, decimal>();
            LastTransactionTime = new DateTime();
            InTrade = false;
            CoinsBought = new List<Tuple<decimal, decimal>>();
            EMA12 = new List<decimal>();
            EMA26 = new List<decimal>();
            EMA99 = new List<decimal>();
            EMA7 = new List<decimal>();
            MACD = new List<decimal>();
            Signal = new List<decimal>();
            MACDHistogram = new List<decimal>();
            MACDHistPosPercentage = -1;
            MACDHistNegPercentage = 1;
            LastSalePrice = -1;
            CycleSeen = false;
            AllottedCoins = -1;
            RSI = new List<decimal>();
            CloseTime = -1;
            PreviousRSI = -1;

        }

        public string Name { get; set; }

        public decimal Price { get; set; }

        public Dictionary<decimal, decimal> HeldPositions { get; set; }

        public decimal PurchaseValue { get; set; }

        public decimal CurrentValue { get; set; }

        public decimal Return { get; set; }

        public decimal PurchaseValueUSD { get; set; }

        public decimal CurrentValueUSD { get; set; }

        public decimal AllottedCoins { get; set; }

        public DateTime LastTransactionTime { get; set; }

        public bool InTrade { get; set; }

        public List<Tuple<decimal, decimal>> CoinsBought { get; set; }

        public List<decimal> EMA12 { get; set; }

        public List<decimal> EMA26 { get; set; }
        
        public List<decimal> EMA99 { get; set; }

        public List<decimal> EMA7 { get; set; }

        public List<decimal> MACD { get; set; }

        public List<decimal> Signal { get; set; }

        public List<decimal> MACDHistogram { get; set; }

        public decimal MACDHistPosPercentage { get; set; }

        public decimal MACDHistNegPercentage { get; set; }

        public decimal LastSalePrice { get; set; }

        public bool CycleSeen { get; set; }

        public List<decimal> RSI { get; set; }

        public decimal PreviousRSI { get; set; }

        public decimal CloseTime { get; set; }
    }

    public static class CurrencyCalc
    {
        public static void CalculateReturn(this Currency currency, List<Currency> allCurrencies)
        {
            //Cycle through the held positions and calculate the profit/loss for each.
            decimal tempValue = 0;
            decimal tempNumber = 0;
            foreach (KeyValuePair<decimal, decimal> kvp in currency.HeldPositions)
            {
                tempValue += (kvp.Key * kvp.Value);
                tempNumber += kvp.Key;
            }

            currency.PurchaseValue = tempValue;
            currency.PurchaseValueUSD = allCurrencies.Where(p => p.Name == "ETHUSDT").FirstOrDefault().Price * currency.PurchaseValue;
            currency.CurrentValue = tempNumber * currency.Price;
            currency.CurrentValueUSD = allCurrencies.Where(p => p.Name == "ETHUSDT").FirstOrDefault().Price * currency.CurrentValue;
            currency.Return = ((currency.CurrentValue - currency.PurchaseValue) / currency.PurchaseValue) * 100;
        }

        public static void CalculateTradeAlgorithm(this Currency currency, KlineData data, BinanceClient client)
        {
            DateTime currentTime = UnixTimeStampToDateTime(data.StartTime);
            DateTime endTime = UnixTimeStampToDateTime(data.EndTime);
            try
            {
                if (currency.CloseTime == data.EndTime)
                {
                    return;
                }

                currency.CloseTime = data.EndTime;
                //Get the last 99 klines to calc
                var sticks = client.GetCandleSticks(currency.Name, BinanceAPIMain.Interval, null, currentTime, 99).Result;

                var sticks1hr = client.GetCandleSticks(currency.Name, TimeInterval.Hours_1, null, currentTime, 200).Result;

                //Perhaps we calculate the EMA with the latest price, but the previous EMA is the close of the last kline?
                if (currency.PreviousRSI != -1)
                {
                    currency.PreviousRSI = currency.RSI.Last();
                }
                currency.EMA12.Clear();
                currency.EMA7.Clear();
                currency.EMA26.Clear();
                currency.EMA99.Clear();
                currency.MACD.Clear();
                currency.Signal.Clear();
                currency.MACDHistogram.Clear();
                currency.RSI.Clear();
                currency.EMA12 = CalculateEMA(sticks, 12);
                currency.EMA26 = CalculateEMA(sticks, 26);
                currency.EMA99 = CalculateEMA(sticks, 99);
                currency.EMA7 = CalculateEMA(sticks, 7);
                currency.MACD = SubtractValues(currency.EMA12, currency.EMA26);
                currency.Signal = CalculateTrendLine(currency.MACD, 9);
                currency.MACDHistogram = SubtractValues(currency.MACD, currency.Signal);
                currency.MACDHistPosPercentage = CalcLowestPercentPos(currency.MACDHistogram, 10);
                currency.RSI = CalculateRSI(sticks, 21);

                if (currency.PreviousRSI == -1)
                {
                    currency.PreviousRSI = currency.RSI.Last();
                }

                ConsoleColor color = currency.MACDHistogram.Last() < 0 ? ConsoleColor.Magenta : ConsoleColor.Cyan;
                BinanceAPIMain.WriteToLog("Symbol " + currency.Name + " Price: " + data.Close + " Close Time:" + endTime + Environment.NewLine +
                    "MACD Histogram: " + currency.MACDHistogram.Last() + Environment.NewLine +
                    "MACD Histogram Lowest 10%: " + currency.MACDHistPosPercentage + Environment.NewLine +
                    "RSI: " + currency.RSI.Last() + Environment.NewLine +
                    "RSI-1: " + currency.PreviousRSI + Environment.NewLine +
                    "Signal: " + currency.Signal.Last() + Environment.NewLine +
                    "EMA99(n): " + Decimal.Round(currency.EMA99.Last(), 7) + " EMA99(n-1): " + Decimal.Round(currency.EMA99[currency.EMA7.Count - 2], 7) + Environment.NewLine +
                    "EMA7(n): " + Decimal.Round(currency.EMA7.Last(), 7) + " EMA7(n-1): " + Decimal.Round(currency.EMA7[currency.EMA7.Count - 2], 7) +
                    " EMA7(n-2): " + Decimal.Round(currency.EMA7[currency.EMA7.Count - 3], 7) + Environment.NewLine, color, createFile: false);


                if (!currency.InTrade && currency.MACDHistogram != null && currency.CycleSeen &&
                    currency.MACDHistogram.Last() > currency.MACDHistPosPercentage &&
                    currency.EMA7.Last() > (currency.EMA7[currency.EMA7.Count - 2] * (decimal)1.0005) &&
                    currency.EMA7[currency.EMA7.Count - 2] > currency.EMA7[currency.EMA7.Count - 3] &&
                    currency.EMA99.Last() > currency.EMA99[currency.EMA99.Count() - 2] &&
                    currency.RSI.Last() < 60)
                {
                    if (BinanceAPIMain.Debug == "false")
                    {
                        Trading.ExecuteTrade(client, data, currency, OrderSide.BUY);
                    }
                    else
                    {
                        Trading.ExecuteTradeDebug(client, data, currency, OrderSide.BUY);
                    }
                }
                else if (currency.InTrade && 
                        (currency.RSI.Last() >= 70 || currency.RSI[currency.RSI.Count() - 2] >= 70) && currency.RSI.Last() < currency.RSI[currency.RSI.Count() - 2] && 
                        data.Close > currency.CoinsBought.Min(p => p.Item2))
                {
                    if (BinanceAPIMain.Debug == "false")
                    {
                        Trading.ExecuteTrade(client, data, currency, OrderSide.SELL);
                    }
                    else
                    {
                        Trading.ExecuteTradeDebug(client, data, currency, OrderSide.SELL);
                    }
                    currency.InTrade = false;
                    currency.CycleSeen = false;
                }
                else if (!currency.CycleSeen && currency.MACDHistogram.Last() < 0 && currency.MACDHistogram != null)
                {
                    currency.CycleSeen = true;
                    currency.LastTransactionTime = DateTime.Now.AddMinutes(-2);
                    BinanceAPIMain.WriteToLog("Symbol: " + currency.Name + " Cycle Seen: " + currency.CycleSeen.ToString() + Environment.NewLine, ConsoleColor.Cyan);
                }
            }
            catch (Exception ex)
            {
                BinanceAPIMain.WriteToLog(currency.Name + ": " + ex.Message + ex.StackTrace + " from currency.CalcAlgorithm" + " at " + DateTime.Now, ConsoleColor.DarkRed);
            }
        }

        public static List<decimal> CalculateEMA(IEnumerable<Binance.API.Csharp.Client.Models.Market.Candlestick> sticks, decimal numPeriods)
        {
            //Multiplier: (2 / (Time periods + 1) ) = (2 / (10 + 1)) = 0.1818(18.18 %)
            //EMA: { Close - EMA(previous min)} x multiplier + EMA(previous min).
            //12day EMA: 0.1538461538461538
            //26day EMA: 0.0740740740740741
            //Signal Line: 9 - day EMA of MACD Line
            //Last index is the most current

            decimal multiplier = (2 / (numPeriods + 1));
            List<decimal> tempList = new List<decimal>();

            try
            {
                for (int i = 0; i < sticks.Count(); i++)
                {
                    if (i == 0)
                    {
                        tempList.Add(sticks.ElementAt(i).Close);
                    }
                    else
                    {
                        decimal priorEMA = tempList.ElementAt(i - 1);
                        decimal finalResult = multiplier * (sticks.ElementAt(i).Close - priorEMA) + priorEMA;
                        tempList.Add(finalResult);
                    }
                }
                return tempList;
            }
            catch
            {
                return null;
            }           
        }

        public static List<decimal> CalculateRSI(IEnumerable<Binance.API.Csharp.Client.Models.Market.Candlestick> sticks, int numPeriods)
        {
            try
            {
                List<decimal> closeDifferences = new List<decimal>();
                List<decimal> tempGains = new List<decimal>();
                List<decimal> tempLosses = new List<decimal>();
                List<decimal> tempRSIs = new List<decimal>();
                for (int i = 1; i < sticks.Count(); i++)
                {
                    closeDifferences.Add(sticks.ElementAt(i).Close - sticks.ElementAt(i - 1).Close);
                }

                //Start by getting the average for the first specified periods
                var startGains = closeDifferences.Take(numPeriods).Where(x => x > 0);
                var startLosses = closeDifferences.Take(numPeriods).Where(x => x < 0);
                decimal avgGain = Math.Abs(startGains.Sum() / numPeriods);
                decimal avgLoss = Math.Abs(startLosses.Sum() / numPeriods);
                tempGains.Add(avgGain);
                tempLosses.Add(avgLoss);


                //Now that we have the average, start to run through index 14 and up.

                for (int n = numPeriods; n < sticks.Count() - 1; n++)
                {
                    //Start on index 14.
                    if (closeDifferences.ElementAt(n) > 0)
                    {
                        tempGains.Add(Math.Abs(((tempGains.Last() * (numPeriods - 1)) + Math.Abs(closeDifferences.ElementAt(n))) / numPeriods));
                        tempLosses.Add(Math.Abs(((tempLosses.Last() * (numPeriods - 1)) + 0) / numPeriods));
                    }
                    else if (closeDifferences.ElementAt(n) < 0)
                    {
                        tempGains.Add(Math.Abs(((tempGains.Last() * (numPeriods - 1)) + 0) / numPeriods));
                        tempLosses.Add(Math.Abs(((tempLosses.Last() * (numPeriods - 1)) + Math.Abs(closeDifferences.ElementAt(n))) / numPeriods));
                    }
                    else
                    {
                        tempGains.Add(Math.Abs(((tempGains.Last() * (numPeriods - 1)) + 0) / numPeriods));
                        tempLosses.Add(Math.Abs(((tempLosses.Last() * (numPeriods - 1)) + 0) / numPeriods));
                    }

                    decimal rs = Math.Abs(tempGains.Last()) / Math.Abs(tempLosses.Last());
                    tempRSIs.Add(100 - (100 / (1 + rs)));
                }
                return tempRSIs;
            }
            catch
            {
                return null;
            }
        }

        public static List<decimal> CalculateTrendLine(List<decimal> MACD, decimal numPeriods)
        {
            //Multiplier: (2 / (Time periods + 1) ) = (2 / (10 + 1)) = 0.1818(18.18 %)
            //EMA: { Close - EMA(previous min)} x multiplier + EMA(previous min).
            //12day EMA: 0.1538461538461538
            //26day EMA: 0.0740740740740741
            //Signal Line: 9 - day EMA of MACD Line
            //Last index is the most current
            try
            {
                decimal multiplier = (2 / (numPeriods + 1));
                List<decimal> tempList = new List<decimal>();


                for (int i = 0; i < MACD.Count(); i++)
                {
                    if (i == 0)
                    {
                        tempList.Add(MACD.ElementAt(i));
                    }
                    else
                    {
                        decimal priorEMA = tempList.ElementAt(i - 1);
                        decimal finalResult = multiplier * (MACD.ElementAt(i) - priorEMA) + priorEMA;
                        tempList.Add(finalResult);
                    }
                }
                return tempList;
            }
            catch
            {
                return null;
            }
        }

        public static List<decimal> SubtractValues(List<decimal> value1, List<decimal> value2)
        {
            try
            {
                List<decimal> tempList = new List<decimal>();
                if (value1 != null && value2 != null && value1.Count() == value2.Count())
                {
                    for (int i = 0; i < value1.Count(); i++)
                    {
                        tempList.Add(value1.ElementAt(i) - value2.ElementAt(i));
                    }
                    return tempList;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static decimal CalcLowestPercentPos(List<decimal> values, double percent)
        {
            try
            {
                List<decimal> tempList = new List<decimal>();
                List<decimal> finalList = new List<decimal>();

                foreach (decimal d in values)
                {
                    if (d > 0)
                        tempList.Add(d);
                }
                tempList.Sort();

                for (int i = 0; i < Convert.ToInt32(values.Count() * (percent / 100)); i++)
                {
                    finalList.Add(tempList[i]);
                }

                return finalList.Average();
            }
            catch
            {
                return -1;
            }
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            try
            {
                System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
                dtDateTime = dtDateTime.AddMilliseconds(unixTimeStamp).ToLocalTime();
                return dtDateTime;
            }
            catch
            {
                return new DateTime();
            }
        }
    }
}