using CarShowJudging.Core.Interfaces;
using CarShowJudging.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CarShowJudging.Web.Pages.Auth;

public class ForgotPasswordModel : PageModel
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly IEmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<ForgotPasswordModel> _logger;

    public ForgotPasswordModel(UserManager<ApplicationUser> users, IEmailService email, IConfiguration config, ILogger<ForgotPasswordModel> logger)
    {
        _users = users;
        _email = email;
        _config = config;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool EmailSent { get; set; }
    public string? ResetUrl { get; set; }   // shown on-screen when SMTP is not configured
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required] public string UserName { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _users.FindByNameAsync(Input.UserName);
        if (user is null)
        {
            EmailSent = true;
            return Page();
        }

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?userId={user.Id}&token={encodedToken}";

        var smtpConfigured = !string.IsNullOrEmpty(_config["Smtp:Host"]);
        var hasRealEmail = !string.IsNullOrEmpty(user.Email) && !user.Email.EndsWith("@carshow.local");

        if (smtpConfigured && hasRealEmail)
        {
            var body = $"""
                <p>Hello {user.DisplayName},</p>
                <p>Click the link below to reset your Car Show Judging password.</p>
                <p><a href="{resetUrl}">Reset my password</a></p>
                <p>If you did not request this, you can ignore this email.</p>
                """;

            try
            {
                await _email.SendAsync(user.Email!, "Car Show Judging — Password Reset", body);
                EmailSent = true;
            }
            catch (Exception ex)
            {
                // SMTP configured but broken (e.g. missing credentials) — degrade to the same
                // on-screen fallback used when SMTP isn't configured at all, instead of a 500.
                _logger.LogWarning(ex, "Password reset email failed to send to {Email}", user.Email);
                ResetUrl = resetUrl;
            }
        }
        else
        {
            // No SMTP or no real email — surface the link on screen for the admin to relay
            ResetUrl = resetUrl;
        }

        return Page();
    }
}
