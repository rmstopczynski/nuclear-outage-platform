using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVC_EF_Start_8.Services;

namespace MVC_EF_Start_8.Controllers
{
    [Authorize]
    public class WatchlistController : Controller
    {
        private readonly WatchlistService _watchlistService;

        public WatchlistController(WatchlistService watchlistService)
        {
            _watchlistService = watchlistService;
        }

        public async Task<IActionResult> Index()
        {
            var outages = await _watchlistService.GetWatchedOutagesAsync(GetUserId());
            return View(outages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string facility, string facilityName, string? returnUrl)
        {
            await _watchlistService.AddAsync(GetUserId(), facility, facilityName);
            return LocalRedirectOrDefault(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(string facility, string? returnUrl)
        {
            await _watchlistService.RemoveAsync(GetUserId(), facility);
            return LocalRedirectOrDefault(returnUrl);
        }

        private IActionResult LocalRedirectOrDefault(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction("Index");
        }

        private int GetUserId()
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new System.InvalidOperationException("Authenticated request missing NameIdentifier claim.");
            return int.Parse(idClaim);
        }
    }
}
