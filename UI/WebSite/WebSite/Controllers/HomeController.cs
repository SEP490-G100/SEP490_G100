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
        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

        public HomeController(ILogger<HomeController> logger, IHttpClientFactory httpFactory)
        {
            _logger = logger;
            _http = httpFactory.CreateClient("BackendApi");
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

                    vm.FeaturedNannies = nannies;

                    var ratedNannies = nannies
                        .Where(n => n.AverageRating.HasValue && n.TotalReviews > 0)
                        .ToList();

                    vm.TotalReviews = ratedNannies.Sum(n => n.TotalReviews);
                    if (vm.TotalReviews > 0)
                    {
                        var weighted = ratedNannies.Sum(n => (n.AverageRating ?? 0m) * n.TotalReviews);
                        vm.AverageRating = Math.Round(weighted / vm.TotalReviews, 1, MidpointRounding.AwayFromZero);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Homepage nanny data skipped — backend not reachable within 5s.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load homepage featured nanny data.");
            }

            return View("_Services", vm);
        }
   

        public IActionResult Faq() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
