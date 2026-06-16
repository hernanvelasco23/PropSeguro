using System.Net;
using Microsoft.Extensions.Options;
using PropSeguro.Domain.Users;

namespace PropSeguro.Api.Email;

public sealed class BetaInvitationEmailService(
    IEmailSender emailSender,
    IOptions<EmailOptions> options) : IBetaInvitationEmailService
{
    private const string Subject = "Tu acceso a PropSegura Beta fue aprobado";

    public Task SendInvitationAsync(AppUser user, string temporaryPassword, string loginUrl, CancellationToken cancellationToken)
    {
        var fromName = string.IsNullOrWhiteSpace(options.Value.FromName) ? "PropSegura" : options.Value.FromName.Trim();
        var html = BuildHtml(user, temporaryPassword, loginUrl, fromName);
        var text = BuildText(user, temporaryPassword, loginUrl);

        return emailSender.SendAsync(new EmailMessage(
            user.Email,
            user.FullName,
            Subject,
            html,
            text), cancellationToken);
    }

    private static string BuildHtml(AppUser user, string temporaryPassword, string loginUrl, string fromName)
    {
        var encodedName = WebUtility.HtmlEncode(user.FullName);
        var encodedEmail = WebUtility.HtmlEncode(user.Email);
        var encodedPassword = WebUtility.HtmlEncode(temporaryPassword);
        var encodedLoginUrl = WebUtility.HtmlEncode(loginUrl);
        var encodedFromName = WebUtility.HtmlEncode(fromName);

        return $"""
            <!doctype html>
            <html lang="es">
            <head>
              <meta charset="UTF-8" />
              <title>Bienvenido a PropSegura Beta</title>
            </head>
            <body style="margin:0;background:#f3f7fa;color:#071f3a;font-family:Arial,Helvetica,sans-serif;">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f3f7fa;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #dbe7ee;border-radius:16px;box-shadow:0 18px 42px rgba(7,31,58,0.10);overflow:hidden;">
                      <tr>
                        <td style="padding:28px 32px;background:#071f3a;color:#ffffff;">
                          <div style="font-size:14px;letter-spacing:0.08em;text-transform:uppercase;color:#77d7ca;font-weight:700;">{encodedFromName}</div>
                          <h1 style="margin:10px 0 0;font-size:30px;line-height:1.2;">Bienvenido a PropSegura Beta</h1>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px;">
                          <p style="margin:0 0 18px;font-size:17px;line-height:1.6;">Hola {encodedName},</p>
                          <p style="margin:0 0 22px;font-size:17px;line-height:1.6;">Tu acceso fue aprobado. Ya podés ingresar a PropSegura y comenzar a analizar propiedades.</p>
                          <p style="margin:0 0 8px;color:#5e6f84;font-size:14px;">Email</p>
                          <p style="margin:0 0 18px;font-weight:700;font-size:16px;">{encodedEmail}</p>
                          <p style="margin:0 0 8px;color:#5e6f84;font-size:14px;">Contraseña temporal</p>
                          <p style="margin:0 0 24px;font-family:Consolas,Monaco,monospace;background:#eef8f6;border:1px solid #bfe5dd;border-radius:10px;padding:14px 16px;font-size:18px;font-weight:700;">{encodedPassword}</p>
                          <p style="margin:0 0 26px;font-size:15px;color:#44566c;">Te recomendamos cambiar tu contraseña luego de ingresar.</p>
                          <a href="{encodedLoginUrl}" style="display:inline-block;background:#0b766f;color:#ffffff;text-decoration:none;font-weight:700;border-radius:10px;padding:14px 22px;">Ingresar a PropSegura</a>
                          <p style="margin:24px 0 0;font-size:14px;color:#5e6f84;line-height:1.6;">Si el botón no funciona, copiá y pegá este link en tu navegador:<br />{encodedLoginUrl}</p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:20px 32px;background:#f8fbfd;border-top:1px solid #e5edf3;color:#5e6f84;font-size:13px;line-height:1.6;">
                          PropSegura no reemplaza la revisión de un escribano, abogado o profesional matriculado.
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static string BuildText(AppUser user, string temporaryPassword, string loginUrl)
    {
        return $"""
            Bienvenido a PropSegura Beta

            Hola {user.FullName},

            Tu acceso fue aprobado. Ya podés ingresar a PropSegura y comenzar a analizar propiedades.

            URL de ingreso: {loginUrl}
            Email: {user.Email}
            Contraseña temporal: {temporaryPassword}

            Te recomendamos cambiar tu contraseña luego de ingresar.

            PropSegura no reemplaza la revisión de un escribano, abogado o profesional matriculado.
            """;
    }
}
