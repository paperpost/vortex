using IdentityModel.OidcClient.Browser;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace vortex.Manager
{
    public class SystemBrowser : IBrowser
    {
        public int Port { get; }
        private readonly string _path;

        public SystemBrowser(int? port = null, string path = null)
        {
            _path = path;

            if (!port.HasValue)
            {
                Port = GetRandomUnusedPort();
            }
            else
            {
                Port = port.Value;
            }
        }

        private int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public async Task<BrowserResult> InvokeAsync(BrowserOptions options, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var listener = new LoopbackHttpListener())
            {
                OpenBrowser(options.StartUrl);

                try {
                    var resp = await listener.WaitForCallbackAsync(Port, _path);

                    if (String.IsNullOrWhiteSpace(resp)) {
                        return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = "Empty response." };
                    }

                    return new BrowserResult { Response = resp, ResultType = BrowserResultType.Success };
                } catch (TaskCanceledException ex) {
                    return new BrowserResult { ResultType = BrowserResultType.Timeout, Error = ex.Message };
                } catch (Exception ex) {
                    return new BrowserResult { ResultType = BrowserResultType.UnknownError, Error = ex.Message };
                }
            }
        }

        public static void OpenBrowser(string url)
        {
            try {
                Process.Start(url);
            } catch {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                    Process.Start("xdg-open", url);
                } else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                    Process.Start("open", url);
                } else {
                    throw;
                }
            }
        }
    }

    public class LoopbackHttpListener : IDisposable
    {
        TaskCompletionSource<string> _source = new TaskCompletionSource<string>();

        HttpListener listener;

        public async Task<string> WaitForCallbackAsync(int port, string path = null)
        {
            string _url;
            string result = "";

            path = path ?? String.Empty;

            if (path.StartsWith("/")) path = path.Substring(1);

            _url = $"http://localhost:{port}/{path}";

            listener = new HttpListener();
            listener.Prefixes.Add(_url);
            listener.Start();

            var ctx = await listener.GetContextAsync();

            if (ctx.Request.HttpMethod == "GET") {
                List<string> q = new List<string>();

                foreach(string key in ctx.Request.QueryString.Keys)
                    q.Add($"{key}={ctx.Request.QueryString[key]}");

                string qs = string.Join("&", q);
                    
                await SetResultAsync(qs, ctx);

                result = qs;
            } else if (ctx.Request.HttpMethod == "POST") {
                if (!ctx.Request.ContentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)) {
                    ctx.Response.StatusCode = 415;
                } else {
                    using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8)) {
                        var body = await sr.ReadToEndAsync();
                        await SetResultAsync(body, ctx);
                        result = body;
                    }
                }
            } else {
                ctx.Response.StatusCode = 405;
            }

            return result;
        }

        public void Dispose() {
            listener.Stop();
        }

        private async Task SetResultAsync(string value, HttpListenerContext ctx)
        {
            StreamWriter sw = new StreamWriter(ctx.Response.OutputStream);

            try {
                ctx.Response.StatusCode = 200;
                ctx.Response.ContentType = "text/html";


                await sw.WriteAsync("<h1>You have successfully authenticated your user for Vortex.</h1><h3>Please close this browser tab and return to Vortex.</h3>");
                await sw.FlushAsync();

                sw.Close();

                _source.TrySetResult(value);
            } catch(Exception ex) {
                Console.WriteLine(ex.ToString());

                ctx.Response.StatusCode = 400;
                ctx.Response.ContentType = "text/html";

                sw.Write("<h1>Invalid request.</h1>");
            }
        }
    }
}

