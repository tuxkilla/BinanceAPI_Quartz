using Binance.API.Csharp.Client;
using Binance.API.Csharp.Client.Models.WebSocket;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Binance.API.Csharp.Client.Models.Enums;

namespace BinanceAPI
{
    class UserStreamingData
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_SHOW = 5;

        static bool restart;
        static CancellationTokenSource cts;
        static ManualResetEventSlim mres;

        static void GetStreamingData(BinanceClient client, ApiClient apiClient, TimeInterval interval)
        {
            try
            {
                mres = new ManualResetEventSlim();
                Console.WriteLine("Type QUIT and enter to exit.");
                TelegramBot.SendBotMessage("Binance API Startup at " + DateTime.Now.ToString(), ConfigurationManager.AppSettings["EMail"]);
                restart:
                do
                {
                    cts = new CancellationTokenSource();

                    List<Task> tasks = new List<Task>
                    {
                        Task.Factory.StartNew(ListenForQuit)
                    };
                    foreach (var symbol in BinanceAPIMain.AllCurrencies.Select(p => p.Name).ToList())
                    {
                        var task = new Task(() => client.ListenKlineEndpoint(symbol.ToLower(), interval, (data) =>
                        {
                            KlineHandler(data, symbol, client);

                        }, cts));

                        tasks.Add(task);
                        task.Start();
                    }

                    while (tasks.TrueForAll(p =>
                        p.Status == TaskStatus.Canceled ||
                        p.Status == TaskStatus.Faulted ||
                        p.Status == TaskStatus.RanToCompletion ||
                        p.Status == TaskStatus.WaitingForActivation ||
                        p.Status == TaskStatus.WaitingForChildrenToComplete ||
                        p.Status == TaskStatus.WaitingToRun
                    ))
                    {
                        Thread.Sleep(50);
                    }

                    cts.CancelAfter(TimeSpan.FromSeconds(84600));

                    // Need to end ListenForQuit method...
                    cts.Token.Register(() =>
                    {
                        while (!mres.IsSet)
                        {
                            IntPtr handle = GetConsoleWindow();
                            ShowWindow(handle, SW_SHOW);
                            SetForegroundWindow(handle);
                            SendKeys.SendWait("`");
                            Thread.Sleep(100);
                        }
                        mres.Reset();
                    });

                    Task.WaitAll(tasks.ToArray());

                    // Wait for socket closure... but don't get spammy.
                    int openSockets = apiClient._openSockets.Count;
                    Console.WriteLine($"Waiting for {apiClient._openSockets.Count} web socket closure...");
                    while (apiClient._openSockets.Count > 0)
                    {
                        if (openSockets != apiClient._openSockets.Count)
                        {
                            openSockets = apiClient._openSockets.Count;
                            Console.WriteLine($"Waiting for {apiClient._openSockets.Count} web socket closure...");
                        }
                        Thread.Sleep(3000);
                    }

                    cts.Dispose();
                    Array.ForEach(tasks.ToArray(), p => p.Dispose());
                }
                while (restart);

                ConsoleKeyInfo cki;
                do
                {
                    Console.WriteLine("Gracefully stopped executing.");
                    Console.WriteLine();
                    Console.WriteLine("Press (R) to restart or (Q) to quit");
                    cki = Console.ReadKey();
                }
                while (cki.Key != ConsoleKey.R && cki.Key != ConsoleKey.Q);

                if (cki.Key == ConsoleKey.R)
                {
                    Console.WriteLine();
                    Console.WriteLine("Restarting...");
                    Console.WriteLine();
                    restart = true;
                    goto restart;
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        static void KlineHandler(KlineMessage data, string symbol, BinanceClient client)
        {
            if (data.EventType == "Cancellation Requested")
            {
                Console.WriteLine(data.Symbol + ", " + data.EventType);
            }
            else
            {
                CurrencyCalc currency = BinanceAPIMain.AllCurrencies.Where(p => p.Name == symbol).FirstOrDefault();
                currency.CalculateTradeAlgorithm(data.KlineInfo, client);
            }
        }

        static void ListenForQuit()
        {
            while (!cts.IsCancellationRequested)
            {
                ConsoleKey ck = Console.ReadKey().Key;

                if (ck == ConsoleKey.X)
                {
                    Console.WriteLine();
                    Console.WriteLine("Stop...");
                    Console.WriteLine();
                    restart = false;
                    mres.Set();
                    cts.Cancel();
                }
                else if (cts.IsCancellationRequested && ck == ConsoleKey.Oem3)
                {
                    Console.WriteLine();
                    Console.WriteLine("Auto restarting...");
                    Console.WriteLine();
                    mres.Set();
                }
            }
        }
    }
}
