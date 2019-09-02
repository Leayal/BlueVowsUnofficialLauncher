using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Cache;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using System.Linq;

namespace BlueVowsLauncher.Classes
{
    public sealed class WebAPI : IDisposable
    {
        private HttpClient client;
        private CookieContainer cookies;
        private HttpClientHandler handler;

        private List<(int, string)> preDefinedSign;

        public WebAPI()
        {
            this.cookies = new CookieContainer();
            this.handler = new HttpClientHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = true,
                Proxy = null,
                UseProxy = false,
                UseDefaultCredentials = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                ClientCertificateOptions = ClientCertificateOption.Automatic,
                CookieContainer = this.cookies
            };
            this.client = new HttpClient(this.handler);
            this.client.DefaultRequestHeaders.UserAgent.ParseAdd("Launcher");

            this.preDefinedSign = new List<(int, string)>(3)
            {
                (1567397570, "6a25c4df1c455532108577f3a7fcc504"),
                (1567399687, "9b16b4ccf6682753f29ef19fe26e487a")
            };
        }

        private (int, string) GetSign()
        {
            return this.preDefinedSign[(new Random().Next(0, this.preDefinedSign.Count - 1))];
        }

        public async Task<JsonDocument> GetStatusAsync()
        {
            using (var response = await this.client.PostAsync("https://www.zuiyouxi.com/launcher/getosstatus", new FormUrlEncodedContent(new Dictionary<string, string>(1)
            {
                { "gn", "all" }
            })))
            {
                using (var payload = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                {
                    return await JsonDocument.ParseAsync(payload);
                }
            }
        }

        public async Task<JsonDocument> GetVersionAsync()
        {
            var obj = GetSign();
            using (var response = await this.client.PostAsync("https://mapishipgirl.zuiyouxi.com/phone/getversion/getversion", new FormUrlEncodedContent(new Dictionary<string, string>(1)
            {
                { "gn", "shipgirl" },
                { "os", "android" },
                { "packageVersion", "0" },
                { "pl", "gwphone_windows" },
                { "scriptVersion", "0" },
                { "time", obj.Item1.ToString() },
                { "sign", obj.Item2 },
            })))
            {
                using (var payload = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                {
                    return await JsonDocument.ParseAsync(payload);
                }
            }
        }

        public Task<bool> DownloadFile(Uri url, Stream output) => this.DownloadFile(url, output, null);

        public Task<bool> DownloadFile(Uri url, Stream output, Func<long, long, bool> progressReport) => this.DownloadFile(url, 0, output, progressReport);

        public Task<bool> DownloadFile(Uri url, long startingByte, Stream output) => this.DownloadFile(url, startingByte, output, null);

        public async Task<bool> DownloadFile(Uri url, long startingByte, Stream output, Func<long, long, bool> progressReport)
        {
            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.Accept.ParseAdd("*/*");
            msg.Headers.Range = new RangeHeaderValue(startingByte, null);
            using (var response = await this.client.SendAsync(msg))
            {
                using (var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
                {
                    if (progressReport == null)
                    {
                        await stream.CopyToAsync(output, 4096);
                        return true;
                    }
                    else
                    {
                        long progressed = 0;
                        long totalToDownload = -1;
                        if (response.Content.Headers.ContentRange.HasRange)
                        {
                            if (response.Content.Headers.ContentRange.From.HasValue && response.Content.Headers.ContentRange.To.HasValue)
                            {
                                progressed = response.Content.Headers.ContentRange.From.Value;
                                totalToDownload = response.Content.Headers.ContentRange.To.Value;
                            }
                        }
                        if (totalToDownload == -1 && response.Content.Headers.ContentLength.HasValue)
                        {
                            totalToDownload = response.Content.Headers.ContentLength.Value;
                        }
                        if (totalToDownload == -1)
                        {
                            await stream.CopyToAsync(output, 4096);
                            return true;
                        }
                        else
                        {
                            byte[] buffer = new byte[4096];
                            int byteRead = await stream.ReadAsync(buffer, 0, 4096);
                            while (byteRead > 0)
                            {
                                progressed += byteRead;
                                await output.WriteAsync(buffer, 0, byteRead);
                                if (!progressReport.Invoke(progressed, totalToDownload))
                                {
                                    return false;
                                }
                                byteRead = await stream.ReadAsync(buffer, 0, 4096);
                            }
                            return true;
                        }
                    }
                }
            }
        }

        public HttpClient GetClient() => this.client;

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            this.Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.handler.Dispose();
            }
            this.client.Dispose();
        }

        ~WebAPI()
        {
            this.Dispose(false);
        }
    }
}
