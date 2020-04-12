using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using PuppeteerSharp;

namespace WebArchive
{
    class Program
    {
        private static string SetupBasePath = AppDomain.CurrentDomain.SetupInformation.ApplicationBase;
        static async Task Main(string[] args)
        {
            var monolith = new Process
            {
                StartInfo = new ProcessStartInfo("monolith.exe", "\"https://www.cnblogs.com/ \" -o ./index.html")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    StandardOutputEncoding = Encoding.UTF8
        }
            };

            monolith.Start();
            var outReadToEnd = monolith.StandardOutput.ReadToEnd();
            Console.WriteLine(outReadToEnd);
            new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                Timeout = 500,
                IgnoreHTTPSErrors = true
                
            });
            var page = await browser.NewPageAsync();
            await page.GoToAsync($"file:///{SetupBasePath}/web/index.html");
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1080,
            });

            await page.EvaluateFunctionAsync("() => window.scrollBy(0, document.body.scrollHeight)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(0, window.innerHeight)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(window.innerHeight, 0)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(document.body.scrollHeight, 0)");

            await page.ScreenshotAsync("./web/index.png", new ScreenshotOptions { FullPage = true });
            await page.PdfAsync("./web/index.pdf");
            await browser.CloseAsync();

            var webClient = new WebClient { Proxy = new WebProxy("127.0.0.1", 7890) };
            var strBytes = webClient.UploadFile("https://ipfs.infura.io:5001/api/v0/add?pin=false", "./web/index.html");
            Console.WriteLine(Encoding.UTF8.GetString(strBytes));
            Console.ReadKey();
        }
    }
}
