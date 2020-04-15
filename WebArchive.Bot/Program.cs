using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace WebArchive.Bot
{
    class Program
    {
        private static TelegramBotClient BotClient;
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        private static readonly WebProxy MWebProxy = new WebProxy("127.0.0.1", 7890);

        static void Main(string[] args)
        {
            //Environment.SetEnvironmentVariable("http_proxy", $"{MWebProxy.Address.Host}:{MWebProxy.Address.Port}", EnvironmentVariableTarget.User);
            Console.WriteLine("Telegram Wayback WebArchive Bot");
            string tokenStr;
            if (File.Exists(SetupBasePath + "token.text"))
                tokenStr = File.ReadAllText(SetupBasePath + "token.text");
            else if (!string.IsNullOrWhiteSpace(string.Join("", args)))
                tokenStr = string.Join("http_proxy", MWebProxy.Address.DnsSafeHost);
            else
            {
                Console.WriteLine("Token:");
                tokenStr = Console.ReadLine();
            }

            BotClient = new TelegramBotClient(tokenStr,MWebProxy);

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
                        Directory.CreateDirectory($"{SetupBasePath}html/{uuid}");
                        var startInfo = new ProcessStartInfo("monolith", $"\"{url}\" -o {SetupBasePath}html/{uuid}/index.html -t 30000")
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            StandardOutputEncoding = Encoding.UTF8
                        };
                        startInfo.EnvironmentVariables["http_proxy"] = MWebProxy.Address.ToString();
                        startInfo.EnvironmentVariables["https_proxy"] = MWebProxy.Address.ToString();
                        startInfo.EnvironmentVariables["no_proxy"] = false.ToString().ToLower();
                        var monolith = new Process {StartInfo = startInfo};
                        monolith.Start();
                        monolith.WaitForExit(30000);
                        try
                        {
                            if (!File.Exists($"{SetupBasePath}html/{uuid}/index.html"))
                                BotClient.SendTextMessageAsync(message.Chat.Id, "请求超时。",
                                    replyToMessageId: message.MessageId);
                            else
                            {
                                var webClient = new WebClient {Encoding = Encoding.UTF8};
                                webClient.Proxy = MWebProxy;
                                var strsBytes = webClient.UploadFile(
                                    "https://ipfs.infura.io:5001/api/v0/add?pin=true",
                                    $"{SetupBasePath}html/{uuid}/index.html");
                                Console.WriteLine(Encoding.UTF8.GetString(strsBytes).Trim());
                                var jObj = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(strsBytes));
                                BotClient.SendTextMessageAsync(message.Chat.Id, "https://ipfs.io/ipfs/" +
                                    jObj.Hash.ToString(), replyToMessageId: message.MessageId);
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
                            File.Delete($"{SetupBasePath}html/{uuid}/index.html");
                            Directory.Delete($"{SetupBasePath}html/{uuid}/");
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
