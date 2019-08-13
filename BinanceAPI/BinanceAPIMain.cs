using System;
using Binance.API.Csharp.Client;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Binance.API.Csharp.Client.Models.Enums;
using System.Threading;
using System.Configuration;
using Binance.API.Csharp.Client.Models.WebSocket;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Telegram.Bot;
using Quartz;
using Quartz.Impl;
using Quartz.Logging;
using System.Collections.Specialized;

namespace BinanceAPI
{
    class BinanceAPIMain
    {
        public static List<Currency> AllCurrencies = new List<Currency>();
        public static TimeInterval Interval = TimeInterval.Minutes_15;

        public static string Debug = ConfigurationManager.AppSettings["Debug"];
        public static string BotKey = ConfigurationManager.AppSettings["TelegramBotKey"];
        public static readonly TelegramBotClient Bot = new TelegramBotClient(BotKey);

        public static decimal numETH;
        public static decimal TotalTrades = 0;
        private static string apiKey = ConfigurationManager.AppSettings["ApiKey"];
        private static string apiSecret = ConfigurationManager.AppSettings["ApiSecret"];
        private static ApiClient ApiClient = new ApiClient(apiKey, apiSecret);
        public static BinanceClient BinanceClient = new BinanceClient(ApiClient, false);


        static void Main(string[] args)
        {
            try
            {
                Console.Title = "BinanceAPI 1.2.5";
                //Setup the telegram bot
                Bot.OnMessage += TelegramBot.BotOnMessageReceived;
                var me = Bot.GetMeAsync().Result;
                Bot.StartReceiving();
                Console.WriteLine($"Start listening for @{me.Username}");

                //Check and see if we have any open currencies due to system crash.
                //If not, then use the configuration setting file to allocate proper coins by total value and % allocation.
                GetAllCoins(BinanceClient);

                if (Debug == "false")
                {
                    //Get Streaming data from websocket and attempt to place trades on "turtle" technique
                    ConsoleKeyInfo cki;
                    do
                    {
                        Console.WriteLine();
                        Console.WriteLine("Press (Q) to quit");
                        TelegramBot.SendBotMessage("Binance API Startup at " + DateTime.Now.ToString(), ConfigurationManager.AppSettings["EMail"]);
                        RunBinanceConnection().GetAwaiter().GetResult();
                        cki = Console.ReadKey();

                    }
                    while (cki.Key != ConsoleKey.Q);
                    //Once we get ready to close the app, sell all coins that havent traded yet.
                    //Trading.SellAllHoldings(AllCurrencies, BinanceClient);
                }
                else if (Debug == "true")
                {
                    //Get the last 500 historical klines and feed them one by one into the same algorithm.
                    MimicStreamingData(BinanceClient);
                }
                Bot.StopReceiving();
            }
            catch(Exception ex)
            {
                WriteToLog(ex.Message + ex.InnerException, ConsoleColor.DarkRed);
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }

        static void GetAllCoins(BinanceClient client)
        {
            try
            {
                decimal walletPercentage = Convert.ToDecimal(ConfigurationManager.AppSettings["ETHWalletPercentage"]);
                string[] coinArray = ConfigurationManager.AppSettings["Coins"].Split(',').ToArray();
                var allPrices = client.GetAllPrices().Result;
                //now get the ETH/USD conversion rate
                var ethValue = allPrices.Where(p => p.Symbol == "ETHUSDT");
                numETH = client.GetAccountInfo().Result.Balances.Where(p => p.Asset == "ETH").FirstOrDefault().Free;

                Console.WriteLine("Free ETH: " + numETH);
                Console.WriteLine("ETH Percentage: " + walletPercentage.ToString());
                Console.WriteLine("ETH For Trading: " + (walletPercentage / 100) * numETH);

                foreach (var value in coinArray)
                {
                    var coin = allPrices.Where(p => p.Symbol == value).FirstOrDefault();
                    //Get the binance coin/price pair for each coin specified in the appSettings file.
                    string trimmedString = coin.Symbol.Substring(coin.Symbol.Length - 3);
                    decimal currentValueUSD = trimmedString == "ETH" ? coin.Price * ethValue.FirstOrDefault().Price : coin.Price;

                    if (currentValueUSD >= 10)
                    {
                        continue;
                    }

                    Currency c = new Currency
                    {
                        Name = coin.Symbol,
                        Price = coin.Price,
                        CurrentValueUSD = currentValueUSD,
                    };

                    //Look to see if we have anything with the specific coin. If we do, then we are in a trade, and add it to the coinsBought and set the InTrade to true.
                    if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + coin.Symbol + ".txt"))
                    {
                        Dictionary<decimal, decimal> priorHoldings = ReadFromCurrencyFiles(coin.Symbol);

                        if (priorHoldings.Count > 0)
                        {
                            c.CoinsBought.Add(new Tuple<decimal, decimal>(priorHoldings.ElementAt(0).Key, priorHoldings.ElementAt(0).Value));
                            c.InTrade = true;
                            TotalTrades++;
                            Console.WriteLine(c.Name + " found in previous trade, " + priorHoldings.ElementAt(0).Key.ToString() + " coins at " +
                                priorHoldings.ElementAt(0).Value.ToString());
                        }
                    }
                    AllCurrencies.Add(c);

                    Console.WriteLine(c.Name + " Price: " + c.Price + " Current Value USD: " + c.CurrentValueUSD + " allotted coins: " + c.AllottedCoins);
                }
            }
            catch (Exception ex)
            {
                WriteToLog(ex.Message + ex.InnerException, ConsoleColor.DarkRed);
                Console.WriteLine(ex.Message + ex.InnerException);
            }
        }

        public static void CalculateCoinsToTrade(BinanceClient client, Currency c, IEnumerable<Binance.API.Csharp.Client.Models.Market.SymbolPrice> allPrices)
        {
            decimal walletPercentage = Convert.ToDecimal(ConfigurationManager.AppSettings["ETHWalletPercentage"]);
            numETH = client.GetAccountInfo().Result.Balances.Where(p => p.Asset == "ETH").FirstOrDefault().Free;
            var ethForTrading = (walletPercentage / 100) * numETH;
            var price = allPrices.Where(p => p.Symbol == c.Name).FirstOrDefault();
            c.AllottedCoins = Decimal.Truncate((walletPercentage / 100) * numETH * (1 / (4 - TotalTrades)) / price.Price);
            
        }

        static void MimicStreamingData(BinanceClient client)
        {
            foreach (var symbol in AllCurrencies.Select(p => p.Name).ToList())
            {
                Currency currency = AllCurrencies.Where(p => p.Name == symbol).FirstOrDefault();

                var sticks = client.GetCandleSticks(currency.Name, Interval, null, DateTime.Now, 500).Result;

                //Cycle through each "close" like they were real time data.
                foreach (var stick in sticks)
                {
                    KlineMessage klineMessage = new KlineMessage();
                    KlineMessage.KlineData data = new KlineMessage.KlineData
                    {
                        Open = stick.Open,
                        Symbol = currency.Name,
                        EndTime = stick.CloseTime,
                        Close = stick.Close ,
                        StartTime = stick.OpenTime
                    };

                    currency.CalculateTradeAlgorithm(data, client);
                }
            }
        }

        public static async void WriteToLog(string message, ConsoleColor color, bool createFile = true)
        {
            try
            {
                Console.ForegroundColor = color;

                if (color == ConsoleColor.DarkRed)
                {
                    TelegramBot.SendBotMessage(message, ConfigurationManager.AppSettings["EMail"]);
                }
                Console.WriteLine(message);
                Console.ResetColor();

                ///Colors:
                ///White - Sold at profit
                ///Red - Sold at loss
                ///Cyan - information
                ///Magenta - possibly falling in price
                ///Green - threading issue falling back to standard format

                if (createFile == true)
                {
                    string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"BinanceAPI\TradeAlgorithm.txt";
                    using (StreamWriter wr = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\TradeAlgorithm.txt", true))
                    {
                        await wr.WriteLineAsync(message + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            
        }

        public static async void WriteToLogTrades(string message)
        {
            try
            {
                using (StreamWriter wr = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\TradeAlgorithm_Trades.txt", true))
                {
                    await wr.WriteLineAsync(message + Environment.NewLine);
                }

                TelegramBot.SendBotMessage(message, ConfigurationManager.AppSettings["EMail"]);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static async void WriteToCurrencyFile(string message, string symbol)
        {
            try
            {
                using (StreamWriter wr = new StreamWriter(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + symbol + ".txt", true))
                {
                    await wr.WriteLineAsync(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static Dictionary<decimal, decimal> ReadFromCurrencyFiles(string symbol)
        {
            try
            {
                Dictionary<decimal, decimal> tempDictionary = new Dictionary<decimal, decimal>();
                using (StreamReader read = new StreamReader(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + @"\BinanceAPI\" + symbol + ".txt"))
                {
                    string line= read.ReadLine();                    
                    string[] splitString = line.Split('|');
                    //First is the number of coins, second is the price we bought at.
                    tempDictionary.Add(Convert.ToDecimal(splitString.First()), Convert.ToDecimal(splitString.Last()));

                }
                return tempDictionary;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private static async Task RunBinanceConnection()
        {
            try
            {
                // Grab the Scheduler instance from the Factory
                NameValueCollection props = new NameValueCollection
                {
                    { "quartz.serializer.type", "binary" }
                };
                StdSchedulerFactory factory = new StdSchedulerFactory(props);
                IScheduler scheduler = await factory.GetScheduler();

                // and start it off
                await scheduler.Start();

                // define the job and tie it to our HelloJob class
                IJobDetail job = JobBuilder.Create<KlineHandler>()
                    .WithIdentity("job1", "group1")
                    .Build();

                // Trigger the job to run now, and then repeat every 10 seconds
                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("trigger1", "group1")
                    .StartAt(DateBuilder.EvenMinuteDateAfterNow().AddSeconds(15))
                    .WithSimpleSchedule(x => x  
                    .WithIntervalInSeconds(60)
                        .RepeatForever())
                    .Build();

                // Tell quartz to schedule the job using our trigger
                await scheduler.ScheduleJob(job, trigger);
            }
            catch (SchedulerException se)
            {
                Console.WriteLine(se);
            }
        }
    }
}
