using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;

namespace BUGrockLauncher {

    class BadUpdateIdentityException: ArgumentException{
        public BadUpdateIdentityException() : base("Bad updateIdentity") { }
    }

    class VersionDownloader {

        private HttpClient client = new HttpClient();
        private WUProtocol protocol = new WUProtocol();
        
        private async Task<XDocument> PostXmlAsync(string url, XDocument data, CancellationToken cancellationToken) {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            using (var stringWriter = new StringWriter()) {
                using (XmlWriter xmlWriter = XmlWriter.Create(stringWriter, new XmlWriterSettings { Indent = false, OmitXmlDeclaration = true })) {
                    data.Save(xmlWriter);
                }
                request.Content = new StringContent(stringWriter.ToString(), Encoding.UTF8, "application/soap+xml");
            }
            using (var resp = await client.SendAsync(request, cancellationToken)) {
                resp.EnsureSuccessStatusCode();
                string str = await resp.Content.ReadAsStringAsync(cancellationToken);
                return XDocument.Parse(str);
            }
        }

        private async Task DownloadFile(string url, string to, DownloadProgress progress, CancellationToken cancellationToken) {
            bool completed = false;
            try {
                using (var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)) {
                    resp.EnsureSuccessStatusCode();
                    using (var inStream = await resp.Content.ReadAsStreamAsync(cancellationToken))
                    using (var outStream = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None)) {
                        long? totalSize = resp.Content.Headers.ContentLength;
                        progress(0, totalSize);
                        long transferred = 0;
                        byte[] buf = new byte[64 * 1024];
                        while (true) {
                            int n = await inStream.ReadAsync(buf, 0, buf.Length, cancellationToken);
                            if (n == 0)
                                break;
                            await outStream.WriteAsync(buf, 0, n, cancellationToken);
                            transferred += n;
                            progress(transferred, totalSize);
                        }
                    }
                }
                completed = true;
            } finally {
                if (!completed) {
                    try {
                        if (File.Exists(to)) {
                            File.Delete(to);
                        }
                    } catch { }
                }
            }
        }

        private async Task<string?> GetDownloadUrl(string updateIdentity, string revisionNumber, CancellationToken cancellationToken) {
            try {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                XDocument result = await PostXmlAsync(protocol.GetDownloadUrl(),
                    protocol.BuildDownloadRequest(updateIdentity, revisionNumber), linkedCts.Token);
                Trace.WriteLine($"GetDownloadUrl() response for updateIdentity {updateIdentity}, revision {revisionNumber}:\n{result.ToString()}");
                foreach (string s in protocol.ExtractDownloadResponseUrls(result)) {
                    if (s.StartsWith("http://tlu.dl.delivery.mp.microsoft.com/"))
                        return s;
                }
                return null;
            } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
                throw new TimeoutException("Timed out while contacting Windows Update server.");
            }
        }

        public void EnableUserAuthorization() {
            protocol.SetMSAUserToken(WUTokenHelper.GetWUToken());
        }

        public async Task DownloadAppx(string updateIdentity, string revisionNumber, string destination, DownloadProgress progress, CancellationToken cancellationToken) {
            string? link = await GetDownloadUrl(updateIdentity, revisionNumber, cancellationToken);
            if (link == null)
                throw new BadUpdateIdentityException();
            Trace.WriteLine("Resolved download link: " + link);
            await DownloadFile(link, destination, progress, cancellationToken);
        }

        public async Task DownloadMsixvc(List<string> downloadUrls, string destination, DownloadProgress progress, CancellationToken cancellationToken) {
            if (downloadUrls == null || downloadUrls.Count == 0) {
                throw new BadUpdateIdentityException();
            }

            Exception? lastException = null;
            for (int i = 0; i < downloadUrls.Count; i++) {
                string url = downloadUrls[i];
                if (string.IsNullOrWhiteSpace(url)) continue;
                try {
                    Trace.WriteLine($"Downloading MSIXVC from URL ({i + 1}/{downloadUrls.Count}): {url}");
                    await DownloadFile(url, destination, progress, cancellationToken);
                    return;
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception ex) {
                    lastException = ex;
                    Trace.WriteLine($"Mirror {i + 1} failed ({url}): {ex.Message}");
                }
            }

            if (lastException != null) {
                throw lastException;
            }
            throw new BadUpdateIdentityException();
        }

        public delegate void DownloadProgress(long current, long? total);

    }
}
