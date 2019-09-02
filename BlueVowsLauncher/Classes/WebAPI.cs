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
using System.Buffers;
using System.Threading;

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
                (1567399687, "9b16b4ccf6682753f29ef19fe26e487a"),
                (1567432250, "f1417a116dbcccf832e00fc45bec502d"),
                (1567432321, "b79bf513d2414046f5a0dbdd7744b95c"),
                (1567397570, "6a25c4df1c455532108577f3a7fcc504")
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

        public async Task<bool> DownloadFile(Uri url, long? startingByte, Stream output, Action<long, long> progressReport, CancellationToken cancellationToken = default)
        {
            HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, url);
            msg.Headers.Accept.ParseAdd("*/*");
            if (startingByte.HasValue)
            {
                msg.Headers.Range = new RangeHeaderValue(startingByte.Value, null);
            }
            using (var response = await this.client.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            using (var stream = await response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync())
            {
                if (progressReport == null)
                {
                    await stream.CopyToAsync(output, 4096 * 2, cancellationToken);
                    return !cancellationToken.IsCancellationRequested;
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
                        await stream.CopyToAsync(output, 4096 * 2, cancellationToken);
                        return !cancellationToken.IsCancellationRequested;
                    }
                    else
                    {
                        const int size = 4096 * 2;
                        progressReport.Invoke(0, totalToDownload);
                        if (progressed != 0)
                        {
                            progressReport.Invoke(progressed, 0);
                        }
                        using (var pooledBuffer = MemoryPool<byte>.Shared.Rent(size))
                        {
                            int byteRead = await stream.ReadAsync(pooledBuffer.Memory, cancellationToken);
                            while (byteRead > 0 && !cancellationToken.IsCancellationRequested)
                            {
                                progressed += byteRead;
                                await output.WriteAsync(pooledBuffer.Memory.Slice(0, byteRead), cancellationToken);
                                progressReport.Invoke(progressed, 0);
                                byteRead = await stream.ReadAsync(pooledBuffer.Memory, cancellationToken);
                            }
                            await output.FlushAsync();
                        }
                        return !cancellationToken.IsCancellationRequested;
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
