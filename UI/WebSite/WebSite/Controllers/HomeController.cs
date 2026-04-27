using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WebSite.Models.Home;
using WebSite.Models.Nanny;
using WebSite.Models;

namespace WebSite.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly HttpClient _http;
        private readonly string _apiBaseUrl;
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpFactory, IConfiguration config)
        {
            _logger = logger;
            _http = httpFactory.CreateClient("BackendApi");
            _apiBaseUrl = (config["ApiSettings:BaseUrl"] ?? "").TrimEnd('/');
        }

        private string? NormalizeAvatarUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (Uri.TryCreate(url, UriKind.Absolute, out _)) return url;
            if (url.StartsWith("~/", StringComparison.Ordinal))
                url = url[1..];
            if (url.StartsWith("/") && !string.IsNullOrWhiteSpace(_apiBaseUrl))
                return _apiBaseUrl + url;
            if (!string.IsNullOrWhiteSpace(_apiBaseUrl))
                return _apiBaseUrl + "/" + url.TrimStart('/');
            return url;
        }

        
        public async Task<IActionResult> Index()
        {
            var vm = new HomePageViewModel();

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await _http.GetAsync("/api/nannies?verificationStatus=2&page=1&pageSize=12", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var apiResult = JsonSerializer.Deserialize<NannyBrowseApiResponse>(json, JsonOpts);
                    var nannies = apiResult?.Data ?? [];
                    foreach (var n in nannies)
                    {
                        n.AvatarUrl = NormalizeAvatarUrl(n.AvatarUrl);
                    }

                    vm.FeaturedNannies = nannies;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Bỏ qua dữ liệu bảo mẫu trang chủ — không kết nối được backend trong 5 giây.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không tải được dữ liệu bảo mẫu nổi bật cho trang chủ.");
            }

            // Thống kê “Người dùng nói gì” (dữ liệu demo; khi có API tổng hợp đánh giá toàn hệ thống thì thay)
            vm.AverageRating = 4.7m;
            vm.TotalReviews = 12_847;

            return View("Index", vm);
        }
   

        public IActionResult Faq() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
