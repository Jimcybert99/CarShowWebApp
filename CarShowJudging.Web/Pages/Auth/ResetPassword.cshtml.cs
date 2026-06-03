using CarShowJudging.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CarShowJudging.Web.Pages.Auth;

public class ResetPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;

    public ResetPasswordModel(UserManager<ApplicationUser> users) => _users = users;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Success { get; set; }
    public bool InvalidLink { get; set; }
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string UserId { get; set; } = string.Empty;
        [Required] public string Token { get; set; } = string.Empty;
        [Required, MinLength(6)] public string NewPassword { get; set; } = string.Empty;
        [Required, Compare(nameof(NewPassword))] public string ConfirmPassword { get; set; } = string.Empty;
    }

    public IActionResult OnGet(string? userId, string? token)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
        {
            InvalidLink = true;
            return Page();
        }

        Input.UserId = userId;
        Input.Token = token;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _users.FindByIdAsync(Input.UserId);
        if (user is null) { InvalidLink = true; return Page(); }

        var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Input.Token));
        var result = await _users.ResetPasswordAsync(user, decodedToken, Input.NewPassword);

        if (result.Succeeded)
        {
            Success = true;
            return Page();
        }

        foreach (var e in result.Errors)
            ModelState.AddModelError(string.Empty, e.Description);

        return Page();
    }
}
