using System;
using System.Threading.Tasks;
using CDO;
using PuppeteerSharp;
using Spire.Pdf;

namespace WebArchive
{
    class Program
    {
        static async Task Main(string[] args)
        {
            string url = "https://www.cnblogs.com/";
            try
            {
                Message msg = new MessageClass();
                var c = new Configuration();
                msg.Configuration = c;
                msg.CreateMHTMLBody(url, CdoMHTMLFlags.cdoSuppressAll, "", "");
                msg.GetStream().SaveToFile(@"./1.mht", ADODB.SaveOptionsEnum.adSaveCreateOverWrite);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error:" + ex.Message);
            }

            new BrowserFetcher().GetExecutablePath(BrowserFetcher.DefaultRevision);
            var browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true
            });
            var page = await browser.NewPageAsync();
            await page.GoToAsync("https://www.cnblogs.com/");
            await page.SetViewportAsync(new ViewPortOptions
            {
                Width = 1080,
            });
            try
            {
                await page.WaitForNavigationAsync(new NavigationOptions
                {
                    Timeout = 1000,
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2, WaitUntilNavigation.Networkidle0 }
                });
            }
            catch (Exception)
            {
                // ignored
            }

            await page.EvaluateFunctionAsync("() => window.scrollBy(0, document.body.scrollHeight)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(0, window.innerHeight)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(window.innerHeight, 0)");
            await page.EvaluateFunctionAsync("() => window.scrollBy(document.body.scrollHeight, 0)");

            try
            {
                await page.WaitForNavigationAsync(new NavigationOptions
                {
                    Timeout = 3000,
                    WaitUntil = new[] { WaitUntilNavigation.Networkidle2, WaitUntilNavigation.Networkidle0 }
                });
            }
            catch (Exception)
            {
                // ignored
            }

            await page.ScreenshotAsync("./1.png", new ScreenshotOptions { FullPage = true });
            await page.PdfAsync("./1.pdf");
            var pdf = new PdfDocument();
            pdf.LoadFromFile("./1.pdf");
            pdf.SaveToFile("./1.html", FileFormat.HTML);
            await browser.CloseAsync();
        }
    }
}
