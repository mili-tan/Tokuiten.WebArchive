using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WebArchive.Bot
{
    class Program
    {
        private static TelegramBotClient BotClient;
        private static readonly WebProxy MWebProxy = new WebProxy("127.0.0.1", 7890);

        static void Main(string[] args)
        {
            Console.WriteLine("Telegram Wayback WebArchive Bot");
            string tokenStr;
            if (File.Exists("token.text"))
                tokenStr = File.ReadAllText("token.text");
            else if (!string.IsNullOrWhiteSpace(string.Join("", args)))
                tokenStr = string.Join("", args);
            else
            {
                Console.WriteLine("Token:");
                tokenStr = Console.ReadLine();
            }

            BotClient = new TelegramBotClient(tokenStr, MWebProxy);

            Console.Title = "Bot:@" + BotClient.GetMeAsync().Result.Username;
            Console.WriteLine($"@{BotClient.GetMeAsync().Result.Username} : Connected");

            BotClient.OnMessage += (sender, eventArgs) =>
            {
                var message = eventArgs.Message;
                if (message == null || message.Type != MessageType.Text) return;
                Console.WriteLine($"@{message.From.Username}: " + message.Text);

                Task.Run(() =>
                {
                    var waitMessage = BotClient.SendTextMessageAsync(message.Chat.Id, "请稍等…",
                        replyToMessageId: message.MessageId).Result;
                    try
                    {
                        var url = new Uri(message.Text, UriKind.Absolute);
                        var uuid = Guid.NewGuid();
                        Console.WriteLine(uuid);
                        Directory.CreateDirectory($"./html/{uuid}");
                        var monolith = new Process
                        {
                            StartInfo = new ProcessStartInfo("monolith", $"\"{url}\" -o ./html/{uuid}/index.html -t 10000")
                            {
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                RedirectStandardInput = true,
                                RedirectStandardOutput = true,
                                StandardOutputEncoding = Encoding.UTF8
                            }
                        };
                        monolith.Start();
                        monolith.WaitForExit();
                        try
                        {
                            if (!File.Exists($"./html/{uuid}/index.html"))
                                BotClient.SendTextMessageAsync(message.Chat.Id, "请求超时。",
                                    replyToMessageId: message.MessageId);
                            else
                            {
                                var webClient = new WebClient {Proxy = MWebProxy, Encoding = Encoding.UTF8};
                                var strsBytes = webClient.UploadFile(
                                    "https://ipfs.infura.io:5001/api/v0/add?pin=false",
                                    $"./html/{uuid}/index.html");
                                Console.WriteLine(Encoding.UTF8.GetString(strsBytes));
                                BotClient.SendTextMessageAsync(message.Chat.Id, Encoding.UTF8.GetString(strsBytes),
                                    replyToMessageId: message.MessageId);
                            }
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                            BotClient.SendTextMessageAsync(message.Chat.Id, "上传到IPFS网络失败，请稍候重试。",
                                replyToMessageId: message.MessageId);
                        }
                        finally
                        {
                            File.Delete($"./html/{uuid}/index.html");
                            Directory.Delete($"./html/{uuid}/");
                            BotClient.DeleteMessageAsync(message.Chat.Id, waitMessage.MessageId);
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        BotClient.SendTextMessageAsync(message.Chat.Id, "可能是无效的网址，或不可达。",
                            replyToMessageId: message.MessageId);
                        BotClient.DeleteMessageAsync(message.Chat.Id, waitMessage.MessageId);
                    }
                });
            };

            BotClient.StartReceiving(Array.Empty<UpdateType>());
            while (true)
            {
                if (Console.ReadLine() != "exit") continue;
                BotClient.StopReceiving();
            }
        }
    }
}
