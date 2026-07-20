using LicenseServer.Data;
using LicenseServer.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=licenses.db"));

var app = builder.Build();

// Create DB if it doesn't exist
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseStaticFiles();

app.MapGet("/test", () => "Hello World!");

app.MapPost("/api/activate", async (ActivationRequest req, AppDbContext db) =>
{
    var license = await db.LicenseKeys.Include(k => k.Activations).FirstOrDefaultAsync(k => k.Key == req.Key);
    
    if (license == null)
        return Results.Json(new ActivationResponse(false, "Invalid License Key", null, null));
        
    if (!license.IsActive)
        return Results.Json(new ActivationResponse(false, "License Key is inactive", null, null));

    var existingActivation = license.Activations.FirstOrDefault(a => a.DeviceId == req.DeviceId);

    if (existingActivation == null)
    {
        if (license.Activations.Count >= license.MaxDevices)
        {
            return Results.Json(new ActivationResponse(false, "Device limit reached for this License Key", null, null));
        }

        existingActivation = new DeviceActivation
        {
            DeviceId = req.DeviceId,
            DeviceName = req.DeviceName,
            ActivatedAt = DateTime.UtcNow
        };
        license.Activations.Add(existingActivation);
        await db.SaveChangesAsync();
    }

    DateTime? expiry = license.Type == "Trial" 
        ? existingActivation.ActivatedAt.AddDays(license.TrialDays) 
        : null; // Null means lifetime

    // In a real app, you would sign this token as a JWT
    var token = $"{license.Key}:{existingActivation.DeviceId}:{expiry?.ToString("o") ?? "lifetime"}";

    return Results.Json(new ActivationResponse(true, "Activated successfully", token, expiry));
})
.WithName("ActivateLicense");

app.MapPost("/api/verify", async (ActivationRequest req, AppDbContext db) =>
{
    var license = await db.LicenseKeys.Include(k => k.Activations).FirstOrDefaultAsync(k => k.Key == req.Key);
    if (license == null || !license.IsActive)
        return Results.Json(new { Valid = false });

    var existingActivation = license.Activations.FirstOrDefault(a => a.DeviceId == req.DeviceId);
    if (existingActivation == null)
        return Results.Json(new { Valid = false });

    if (license.Type == "Trial")
    {
        var expiry = existingActivation.ActivatedAt.AddDays(license.TrialDays);
        if (expiry < DateTime.UtcNow)
            return Results.Json(new { Valid = false, Reason = "Trial expired" });
    }

    return Results.Json(new { Valid = true });
})
.WithName("VerifyLicense");

app.MapPost("/api/subscription/verify", async (SubscriptionVerifyRequest req, AppDbContext db) =>
{
    var license = await db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == req.Key);
    if (license == null || !license.IsActive || !license.CloudEnabled)
        return Results.Json(new { Valid = false, Message = "Invalid or inactive cloud subscription." });

    return Results.Json(new { Valid = true, Email = license.Email });
})
.WithName("VerifySubscription");

app.MapGet("/api/admin/keys", async (AppDbContext db) => 
{
    // Need to avoid circular reference in JSON serialization
    var keys = await db.LicenseKeys.Include(k => k.Activations).ToListAsync();
    // To cleanly serialize without cycle exceptions, we project it.
    return keys.Select(k => new {
        k.Id,
        k.Key,
        k.Type,
        k.TrialDays,
        k.MaxDevices,
        k.IsActive,
        k.Email,
        k.CloudEnabled,
        Activations = k.Activations.Select(a => new {
            a.Id,
            a.DeviceId,
            a.DeviceName,
            a.ActivatedAt
        })
    });
});

app.MapPost("/api/admin/keys", async (CreateKeyRequest req, AppDbContext db) =>
{
    var newKey = new LicenseKey 
    { 
        Key = string.IsNullOrWhiteSpace(req.Key) ? Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper() : req.Key, 
        Type = req.Type, 
        TrialDays = req.TrialDays,
        MaxDevices = req.MaxDevices,
        Email = req.Email ?? string.Empty,
        CloudEnabled = req.CloudEnabled,
        IsActive = true
    };
    db.LicenseKeys.Add(newKey);
    await db.SaveChangesAsync();
    return Results.Ok(newKey);
});

app.MapPut("/api/admin/keys/{id}", async (int id, UpdateKeyRequest req, AppDbContext db) =>
{
    var license = await db.LicenseKeys.FindAsync(id);
    if (license == null) return Results.NotFound();
    
    license.IsActive = req.IsActive;
    license.Email = req.Email ?? license.Email;
    license.CloudEnabled = req.CloudEnabled;
    await db.SaveChangesAsync();
    return Results.Ok(license);
});

app.MapFallbackToFile("admin.html");

app.Run();

// Models for requests/responses
public record ActivationRequest(string Key, string DeviceId, string? DeviceName = null);
public record ActivationResponse(bool Success, string Message, string? Token, DateTime? ExpiryDate);
public record SubscriptionVerifyRequest(string Key);
public record CreateKeyRequest(string Key, string Type, int TrialDays, int MaxDevices, string? Email, bool CloudEnabled);
public record UpdateKeyRequest(bool IsActive, string? Email, bool CloudEnabled);
