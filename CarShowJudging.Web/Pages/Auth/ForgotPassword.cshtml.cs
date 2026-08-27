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

        var hasRealEmail = !string.IsNullOrEmpty(user.Email) && !user.Email.EndsWith("@carshow.local");

        if (hasRealEmail)
        {
            var body = $"""
                <p>Hello {user.UserName},</p>
                <p>Click the link below to reset your Car Show Judging password.</p>
                <p><a href="{resetUrl}">Reset my password</a></p>
                <p>If you did not request this, you can ignore this email.</p>
                """;

            try
            {
                // EmailService itself logs the link server-side when SMTP isn't configured,
                // so this covers both the "SMTP missing" and "SMTP misconfigured" cases without
                // ever putting the reset token in the HTTP response.
                await _email.SendAsync(user.Email!, "Car Show Judging — Password Reset", body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Password reset email failed to send to {Email}", user.Email);
            }
        }
        else
        {
            // No deliverable email on file (e.g. the seeded admin account) — the reset token
            // must never be exposed to an unauthenticated caller. Log it server-side only, so
            // an admin with server/log access can relay it out-of-band.
            _logger.LogWarning(
                "Password reset requested for {UserName}, who has no deliverable email on file. " +
                "Reset URL (relay manually): {ResetUrl}", user.UserName, resetUrl);
        }

        // Always the same response regardless of what happened above, so the page never reveals
        // account existence, email configuration, or SMTP delivery state to the caller.
        EmailSent = true;
        return Page();
    }
}
