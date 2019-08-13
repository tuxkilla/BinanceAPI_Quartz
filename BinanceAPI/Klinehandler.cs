using Binance.API.Csharp.Client;
using Binance.API.Csharp.Client.Models.Enums;
using Binance.API.Csharp.Client.Models.WebSocket;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinanceAPI
{
    public class KlineHandler: IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
                await DetermineTrade();
        }

        private Task DetermineTrade()
        {
            return Task.Run(() =>
            {
                foreach (Currency currency in BinanceAPIMain.AllCurrencies)
                {
                    try
                    {
                        //Console.WriteLine("Symbol: " + currency.Name + " Processing start at " + DateTime.Now);
                        var recentStick = BinanceAPIMain.BinanceClient.GetCandleSticks(currency.Name.ToLower(), TimeInterval.Minutes_15, null, null, 1).Result;
                        if (recentStick.FirstOrDefault().CloseTime != currency.CloseTime)
                        {
                            //Console.WriteLine("Symbol: " + currency.Name + " End Time: " + CurrencyCalcs.UnixTimeStampToDateTime(recentStick.FirstOrDefault().CloseTime) + " at:" + DateTime.Now);
                            KlineMessage.KlineData data = new KlineMessage.KlineData
                            {
                                Close = recentStick.FirstOrDefault().Close,
                                EndTime = recentStick.FirstOrDefault().CloseTime,
                                StartTime = recentStick.FirstOrDefault().OpenTime,
                                Symbol = currency.Name
                            };

                            //currency.CloseTime = data.EndTime;
                            currency.CalculateTradeAlgorithm(data, BinanceAPIMain.BinanceClient);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Symbol: " + currency.Name + "Error Processing start at " + DateTime.Now + ex.InnerException);
                    }
                }
            });
        }
    }
}
