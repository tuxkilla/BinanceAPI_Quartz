using Binance.API.Csharp.Client;
using Binance.API.Csharp.Client.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Binance.API.Csharp.Client.Models.WebSocket.KlineMessage;
using System.IO;
using System.Text;

namespace BinanceAPI
{
    static class Trading
    {     
        public static void ExecuteTrade(BinanceClient client, KlineData data, Currency currency, OrderSide side)
        {
            var allPrices = client.GetAllPrices().Result;
            var price = allPrices.Where(p => p.Symbol.Contains(currency.Name)).Select(a => a.Price).FirstOrDefault();

            DateTime currentTime = DateTime.Now;
            if (side == OrderSide.BUY && BinanceAPIMain.TotalTrades < 4)
            {
                //Before we execute, update the allotted coins
                BinanceAPIMain.CalculateCoinsToTrade(client, currency, allPrices);

                //Orders will break if not successful, so check for try/catch block and add into currency ledger or move on.
                try
                {
                    var order = client.PostNewOrder(currency.Name.ToLower(), currency.AllottedCoins, 0.1m, side, OrderType.MARKET, TimeInForce.IOC);
                    Task.WaitAll(order);
                    if (order.Result.OrderId != -1)
                    {
                        BinanceAPIMain.TotalTrades++;
                        currency.InTrade = true;
                        var finalCoinsBought = GetHeldCoins(client, currency.Name);
                        currency.CoinsBought.Add(new Tuple<decimal, decimal>(finalCoinsBought, price));
                        BinanceAPIMain.WriteToLog(finalCoinsBought + ":" + currency.Name + ": go in on trade at: " + price + " Time: " + currentTime, ConsoleColor.Yellow);
                        BinanceAPIMain.WriteToLogTrades(currency.Name + ": Buy " + finalCoinsBought + " coins at " + price + " Time: " + currentTime);
                        BinanceAPIMain.WriteToCurrencyFile(finalCoinsBought + "|" + price, currency.Name);
                    }
                }
                catch (OperationCanceledException oce)
                {
                    BinanceAPIMain.WriteToLog(oce.Message + " while buying " + currency.Name + " " + DateTime.Now, ConsoleColor.DarkRed);
                }
                catch(Exception ex)
                {
                    BinanceAPIMain.WriteToLog(ex.InnerException + " while buying " + currency.Name + " " + DateTime.Now, ConsoleColor.DarkRed);
                }
            }
            else if (side == OrderSide.SELL)
            {
                try
                {
                    foreach (Tuple<decimal, decimal> tuplePair in currency.CoinsBought)
                    {
                        var order = client.PostNewOrder(currency.Name.ToLower(), tuplePair.Item1, 0.1m, side, OrderType.MARKET, TimeInForce.IOC);
                        Task.WaitAll(order);
                        if (order.Result.OrderId != -1)
                        {
                            decimal percentage = Math.Round(((price - tuplePair.Item2) / tuplePair.Item2) * 100, 2);
                            ConsoleColor color = percentage > 0 ? ConsoleColor.White : ConsoleColor.Red;

                            BinanceAPIMain.WriteToLogTrades(currency.Name + ": Sell " + tuplePair.Item1 + " coins at " + price + " Time: " + currentTime +
                                " for a " + percentage + " percent change");
                            BinanceAPIMain.WriteToLog(currency.Name + ": sold " + tuplePair.Item1 + " coins at " + price + " on " + currentTime + Environment.NewLine +
                                currency.Name + ": Made: " + (price - tuplePair.Item2) + " Percentage: " + percentage, color);
                        }
                    }
                    BinanceAPIMain.TotalTrades--;
                    currency.CoinsBought.Clear();
                    currency.LastSalePrice = data.Close;
                    currency.LastTransactionTime = DateTime.Now;

                    //Remove the file which indicated we had an open position on the trade.
                    if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + currency.Name + ".txt"))
                    {
                        File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + currency.Name + ".txt");
                    }
                }
                catch (OperationCanceledException oce)
                {
                    BinanceAPIMain.WriteToLog(oce.Message + " while selling " + currency.Name + " " + DateTime.Now, ConsoleColor.DarkRed);
                }
                catch (Exception ex)
                {
                    BinanceAPIMain.WriteToLog(ex.InnerException + " while selling " + currency.Name + " " + DateTime.Now, ConsoleColor.DarkRed);
                }
            }
        }

        public static void ExecuteTradeDebug(BinanceClient client, KlineData data, Currency currency, OrderSide side)
        {
            var allPrices = client.GetAllPrices().Result;
            var price = allPrices.Where(p => p.Symbol.Contains(currency.Name)).Select(a => a.Price).FirstOrDefault();

            var finalPrice = BinanceAPIMain.Debug == "true" ? data.Close : price;

            DateTime currentTime = BinanceAPIMain.Debug == "true" ? CurrencyCalc.UnixTimeStampToDateTime(data.EndTime) : DateTime.Now;
            if (side == OrderSide.BUY)
            {
                //Orders will break if not successful, so check for try/catch block and add into currency ledger or move on.
                try
                {
                    currency.CoinsBought.Add(new Tuple<decimal, decimal>(currency.AllottedCoins, data.Close));
                    currency.InTrade = true;
                    BinanceAPIMain.WriteToLog(currency.AllottedCoins + ":" + currency.Name + ": go in on trade at: " + data.Close + " Time: " + currentTime, ConsoleColor.Yellow);
                    BinanceAPIMain.WriteToLogTrades(currency.Name + ": Buy " + currency.AllottedCoins + " coins at " + data.Close + " for the kline ending at " + currentTime);
                    BinanceAPIMain.WriteToCurrencyFile(currency.AllottedCoins + "|" + data.Close, currency.Name);
                }
                catch (Exception ex)
                {
                    BinanceAPIMain.WriteToLog(ex.Message + " while buying " + currency.Name, ConsoleColor.DarkRed);
                }
            }
            else if (side == OrderSide.SELL)
            {
                try
                {
                    foreach (Tuple<decimal, decimal> tuplePair in currency.CoinsBought)
                    {
                        decimal percentage = Math.Round(((data.Close - tuplePair.Item2) / tuplePair.Item2) * 100, 2);
                        ConsoleColor color = percentage > 0 ? ConsoleColor.White : ConsoleColor.Red;

                        BinanceAPIMain.WriteToLogTrades(currency.Name + ": Sell " + currency.AllottedCoins + " coins at " + data.Close + " for the kline ending at " + currentTime +
                            " for a " + percentage + " percent change");
                        BinanceAPIMain.WriteToLog(currency.Name + ": sold " + tuplePair.Item1 + " coins at " + data.Close + " on " + currentTime + Environment.NewLine +
                            currency.Name + ": Made: " + (data.Close - tuplePair.Item2) + " Percentage: " + percentage, color);
                    }
                    currency.CoinsBought.Clear();
                    currency.LastSalePrice = data.Close;
                    currency.LastTransactionTime = DateTime.Now;

                    //Remove the file which indicated we had an open position on the trade.
                    if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + currency.Name + ".txt"))
                    {
                        File.Delete(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + currency.Name + ".txt");
                    }
                }
                catch (Exception ex)
                {
                    BinanceAPIMain.WriteToLog(ex.Message + " while selling " + currency.Name, ConsoleColor.DarkRed);
                }
            }
        }

        public static void SellAllHoldings(List<Currency> allCurrencies, BinanceClient client)
        {
            try
            {
                foreach (Currency currency in allCurrencies)
                {
                    foreach (Tuple<decimal, decimal> tuplePair in currency.CoinsBought)
                    {
                        try
                        {
                           // NewOrder order = client.PostNewOrder(currency.Name, tuplePair.Item1, 0.1m, OrderSide.SELL, OrderType.MARKET, TimeInForce.IOC).Result;
                           // if (order.OrderId != -1)
                           // {
                                BinanceAPIMain.WriteToLogTrades(currency.Name + "|sold " + tuplePair.Item1 + "|coins on" + DateTime.Now.ToString());
                                BinanceAPIMain.WriteToLog(currency.Name + ": sold " + tuplePair.Item1 + " coins on " + DateTime.Now.ToString(), ConsoleColor.White);
                           // }
                        }
                        catch
                        {

                        }
                    }
                    currency.CoinsBought.Clear();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static decimal GetHeldCoins(BinanceClient client, string currency)
        {
            string trimmedName = currency.ToUpper().Replace("ETH", "");
            try
            {
                Binance.API.Csharp.Client.Models.Market.Balance balance = client.GetAccountInfo().Result.Balances.Where(p => p.Asset == trimmedName.ToUpper()).FirstOrDefault();
                return decimal.Truncate(balance.Free);
            }
            catch
            {
                return -1;
            }

        }

        public static void GetExistingHoldings(BinanceClient client, string baseCurrency)
        {
            //Get the currencies that we have ordered in the past
            foreach (Binance.API.Csharp.Client.Models.Market.Balance b in client.GetAccountInfo().Result.Balances.Where(p => p.Free > 0))
            {
                try
                {
                    var allOrders = client.GetTradeList(b.Asset + baseCurrency);
                    if (allOrders.Result != null)
                    {
                        foreach (var trade in allOrders.Result)
                        {
                            BinanceAPIMain.AllCurrencies.Where(p => p.Name == b.Asset + baseCurrency).FirstOrDefault().HeldPositions.Add(trade.Quantity, trade.Price);
                        }
                        BinanceAPIMain.AllCurrencies.Where(p => p.Name == b.Asset + baseCurrency).FirstOrDefault().CalculateReturn(BinanceAPIMain.AllCurrencies);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
            var heldCurrencies = BinanceAPIMain.AllCurrencies.Where(x => x.HeldPositions.Count > 0);

            //Now output the data to a text file.
            using (StreamWriter wr = new StreamWriter(@"C:\Users\user\Desktop\HeldPositions.csv", true))
            {
                wr.WriteLine("Symbol, Purchase Value, Current Value, Purchase Value USD, Current Value USD, RoR");
                foreach (Currency c in heldCurrencies)
                {
                    wr.WriteLine(
                        c.Name + "," +
                        c.PurchaseValue.ToString() + "," +
                        c.CurrentValue.ToString() + "," +
                        c.PurchaseValueUSD.ToString() + "," +
                        c.CurrentValueUSD.ToString() + "," +
                        c.Return.ToString());
                }
            }

        }

        public static string GetCurrentPurchases(BinanceClient client)
        {
            var currentCurrencies = BinanceAPIMain.AllCurrencies.Where(p => p.InTrade);
            var currentPrices = client.GetAllPrices().Result;

            StringBuilder b = new StringBuilder();
            b.AppendLine("Current gain/loss:");

            foreach (Currency c in currentCurrencies)
            {
                var purchasePrice = c.CoinsBought.FirstOrDefault().Item2;
                var currentPrice = currentPrices.Where(p => p.Symbol.ToLower() == c.Name.ToLower()).Select(p => p.Price).FirstOrDefault();
                decimal percentage = Math.Round(((currentPrice - purchasePrice) / purchasePrice) * 100, 2);
                b.AppendLine(c.Name + ":");
                b.AppendLine("\tBought at: " + purchasePrice);
                b.AppendLine("\tCurrent Price: " + currentPrice);
                b.AppendLine("\tPercent Change:" + percentage.ToString());
                b.AppendLine();
            }

            return b.ToString();
        }
    }
}
