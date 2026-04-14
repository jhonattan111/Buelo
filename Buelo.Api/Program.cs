using Buelo.Engine;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

builder.Services.AddBueloEngine();

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("default", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

QuestPDF.Settings.License = LicenseType.Community;

app.UseHttpsRedirection();

app.UseCors("default");

app.UseAuthorization();

app.MapControllers();

app.Run();
