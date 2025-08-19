using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace PMM_Windows_Helper
{
    public class GitCatalogService
    {
        private readonly string _catalogUrl; // ví dụ: https://raw.githubusercontent.com/<owner>/<repo>/main/catalog.json
        private readonly string _cacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "WinSetupHelper");
        private string CachePath => Path.Combine(_cacheDir, "catalog-cache.json");
        private string ETagPath => Path.Combine(_cacheDir, "catalog-cache.etag");
        private string LastModPath => Path.Combine(_cacheDir, "catalog-cache.lastmod");

        public GitCatalogService(string catalogRawUrl)
        {
            _catalogUrl = catalogRawUrl ?? throw new ArgumentNullException(nameof(catalogRawUrl));
            Directory.CreateDirectory(_cacheDir);

            // Bắt buộc TLS 1.2 cho .NET 4.5
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public async Task<AppCatalog> GetCatalogAsync()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(_catalogUrl);
                req.Method = "GET";
                req.UserAgent = "WinSetupHelper/1.0";

                // If-None-Match / If-Modified-Since
                if (File.Exists(ETagPath))
                {
                    string etag = File.ReadAllText(ETagPath);
                    if (!string.IsNullOrWhiteSpace(etag))
                        req.Headers["If-None-Match"] = etag.Trim();
                }
                if (File.Exists(LastModPath))
                {
                    string last = File.ReadAllText(LastModPath);
                    DateTime dt;
                    if (DateTime.TryParse(last, out dt))
                        req.IfModifiedSince = dt.ToUniversalTime();
                }

                using (var resp = (HttpWebResponse)await req.GetResponseAsync())
                {
                    if (resp.StatusCode == HttpStatusCode.NotModified && File.Exists(CachePath))
                        return LoadFromCache();

                    using (var s = resp.GetResponseStream())
                    using (var ms = new MemoryStream())
                    {
                        await s.CopyToAsync(ms);
                        var json = Encoding.UTF8.GetString(ms.ToArray());

                        // Lưu cache + etag + last-modified
                        File.WriteAllText(CachePath, json, Encoding.UTF8);
                        var etag = resp.Headers["ETag"];
                        if (!string.IsNullOrEmpty(etag)) File.WriteAllText(ETagPath, etag);
                        var lastmod = resp.Headers["Last-Modified"];
                        if (!string.IsNullOrEmpty(lastmod)) File.WriteAllText(LastModPath, lastmod);

                        return JsonConvert.DeserializeObject<AppCatalog>(json);
                    }
                }
            }
            catch
            {
                // Fallback cache
                if (File.Exists(CachePath))
                    return LoadFromCache();

                // Fallback embedded (nếu bạn có nhúng file mặc định)
                throw; // hoặc trả về new AppCatalog();
            }
        }

        private AppCatalog LoadFromCache()
        {
            var json = File.ReadAllText(CachePath, Encoding.UTF8);
            return JsonConvert.DeserializeObject<AppCatalog>(json);
        }
    }
}
