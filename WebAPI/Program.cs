using System.Text.Json;
using Microsoft.AspNetCore.RateLimiting;
using WebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddHttpClient<IChatService, ChatService>();

builder.Services.AddRateLimiter(o =>
    o.AddFixedWindowLimiter("chat", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
    }));

builder.Services.AddCors(options =>
{
    options.AddPolicy("WebApp", policy =>
        policy.WithOrigins(builder.Configuration["AllowedOrigins"]!)
            .AllowAnyMethod()
            .AllowAnyHeader());
});

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

var app = builder.Build();

app.UseExceptionHandler(err => err.Run(async ctx =>
{
    ctx.Response.StatusCode = 500;
    await ctx.Response.WriteAsJsonAsync(new { error = "Erro interno." });
}));

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("WebApp");
app.UseAuthorization();
app.MapControllers();

app.Run();