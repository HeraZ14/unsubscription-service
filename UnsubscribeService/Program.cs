using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using System.Net;
using System.Net.Mail;
//TEST
//http://localhost:5125/unsubscribe?id=user@example.com&htmlTemplate=123&t=24082025
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var tokenSeznam = new Dictionary<string, string>();
var mailingLista = new HashSet<string>();

//Testni primer
mailingLista.Add("user@example.com");

//string GenerateToken(string email)
//{
//    using var sha256 = SHA256.Create();
//    var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(email + DateTime.UtcNow));
//   return WebEncoders.Base64UrlEncode(bytes);
//}

// Generiranje tokena
string GenerateToken(string email, string time) 
    { 
        using var sha256 = SHA256.Create(); 
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(email + time)); 
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); 
    }
// Uporabnik klikne na odjavni link; generiramo token in ga shranimo,
// preusmerimo na potrditveno stran; če uporabnik klikne na gumb in potrdi odjavo,
// zbrišemo token in mail iz mailing liste
app.MapGet("/unsubscribe", async (string? id, string? htmlTemplate, string? t, string? token, bool? success) =>
{
    //Generiraj token in preusmeri
    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(htmlTemplate)&& !string.IsNullOrEmpty(t))
    {
        var generatedToken = GenerateToken(id, t);
        tokenSeznam[generatedToken] = id;
        return Results.Redirect($"/unsubscribe?token={generatedToken}");
    }

    //Potrditvena stran
    if (!string.IsNullOrEmpty(token) && success is null)
    {
        if (!tokenSeznam.ContainsKey(token))
            return Results.BadRequest("Invalid or expired token.");

        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "submit_unsubscription.html");
        if (!File.Exists(filePath))
        return Results.NotFound("Template not found");
        var htmlContent = await File.ReadAllTextAsync(filePath);
        htmlContent = htmlContent.Replace("{{token}}", token);
        return Results.Content(htmlContent, "text/html");
    }
    //Izpis sporočila
    if (!string.IsNullOrEmpty(token) && success is true)
    {
    return Results.Content($"<h3>You have successfully unsubscribed {id}.</h3>", "text/html");
    }

    return Results.BadRequest("Invalid request.");
});

app.MapPost("/unsubscribe", async (string token) =>
{
    if (!tokenSeznam.TryGetValue(token, out var email))
        return Results.BadRequest("Invalid token.");

    tokenSeznam.Remove(token); // Invalidate token

    var webhookUrl = "https://webhook.site/e92ec64c-931b-4bd7-b954-dccb5289a39a"; 
    var client = new HttpClient();
    await client.PostAsJsonAsync(webhookUrl, new { email });

    // Izbrišemo osebo iz mailing liste, kjerkoli jo pač imamo
    if (mailingLista.Contains(email))
    {
        mailingLista.Remove(email);  // Odstrani iz mailing liste
    }


    // Pošljemo e-pošto uporabniku o uspešni odjavi
    /* DODAMO PODATKE O NAŠI DOMENI, email, password itd...
    var smtpClient = new SmtpClient("smtp.EMAIL.com") //vnesemo našo domeno namesto EMAIL
    {
        Port = 587, // ali 465 za SSL
        Credentials = new NetworkCredential("your-email@example.com", "password"), //Vnesemo naš email in password
        EnableSsl = true,
    };

    var mailMessage = new MailMessage
    {
        From = new MailAddress("your-email@example.com"),
        Subject = "Unsubscribe Confirmation",
        Body = $"Dear user,\n\nYou have successfully unsubscribed from our mailing list.\n\nBest regards,\nYour Company",
        IsBodyHtml = false,
    };

    mailMessage.To.Add(email);

    try
    {
        await smtpClient.SendMailAsync(mailMessage);
    }
    catch (Exception ex)
    {
        // Napaka pri pošiljanju e-pošte
        Console.WriteLine("Error sending email: " + ex.Message);
    }*/

    return Results.Redirect($"/unsubscribe?token={token}&success=true");
});

app.Run();
