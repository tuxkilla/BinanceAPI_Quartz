using System;
using System.Configuration;
using System.Linq;
using System.Text;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace BinanceAPI
{
    class TelegramBot
    {
        public static async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            
            if (message == null || message.Type != MessageType.TextMessage) return;

            if (message.From.FirstName.ToLower().Contains("name") && ConfigurationManager.AppSettings["EMail"].Contains("name"))
            {
                switch (message.Text.Split(' ').First())
                {
                    case "/hello":
                        if (ConfigurationManager.AppSettings["EMail"].Contains("name"))
                        {
                            await BinanceAPIMain.Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                            await BinanceAPIMain.Bot.SendTextMessageAsync(
                            message.Chat.Id,
                            ConfigurationManager.AppSettings["EMail"] + " " + Console.Title + Environment.NewLine + "ChatID: " + message.Chat.Id.ToString() + " ACK",
                            replyMarkup: new ReplyKeyboardRemove());

                        }
                        break;

                    case "/help":
                        if (ConfigurationManager.AppSettings["EMail"].Contains("name"))
                        {
                            StringBuilder builder = new StringBuilder();
                            builder.AppendLine("Available Commands:");
                            builder.AppendLine(@"/hello: get chat window number of bot/client connection");
                            builder.AppendLine(@"/statusXXXX: get current kline close status for each user (name or name)");
                            builder.AppendLine(@"/pricesXXXX: get current prices for each coin purchased by BinanceAPI application and calc return.");

                            await BinanceAPIMain.Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);

                            await BinanceAPIMain.Bot.SendTextMessageAsync(
                            message.Chat.Id,
                            builder.ToString(),
                            replyMarkup: new ReplyKeyboardRemove());
                        }
                        break;

                    case "/status":
                        if (ConfigurationManager.AppSettings["EMail"].Contains("name"))
                        {
                            await BinanceAPIMain.Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                            StringBuilder b = new StringBuilder();
                            b.AppendLine(ConfigurationManager.AppSettings["EMail"] + " " + Console.Title);
                            foreach (Currency c in BinanceAPIMain.AllCurrencies)
                            {
                                b.AppendLine(c.Name + ": close kline at: " + UnixTimeStampToDateTime((double)c.CloseTime));
                            }

                            await BinanceAPIMain.Bot.SendTextMessageAsync(
                            message.Chat.Id,
                            b.ToString(),
                            replyMarkup: new ReplyKeyboardRemove());
                        }
                        break;

                    case "/prices":
                        if (ConfigurationManager.AppSettings["EMail"].Contains("name"))
                        {
                            await BinanceAPIMain.Bot.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                            await BinanceAPIMain.Bot.SendTextMessageAsync(
                            message.Chat.Id,
                            Trading.GetCurrentPurchases(BinanceAPIMain.BinanceClient),
                            replyMarkup: new ReplyKeyboardRemove());
                        }
                        break;
                }
            }
        }

        public static async void SendBotMessage(string message, string sender)
        {
            await BinanceAPIMain.Bot.SendTextMessageAsync(
            -258138735,
            "Message from: " + sender + Environment.NewLine + message,
            replyMarkup: new ReplyKeyboardRemove());
        }

        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            try
            {
                DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0);
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
