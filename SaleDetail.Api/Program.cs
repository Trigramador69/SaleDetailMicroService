using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SaleDetail.Application.Interfaces;
using SaleDetail.Application.Services;
using SaleDetail.Application.Validators;
using SaleDetail.Domain.Interfaces;
using SaleDetail.Infrastructure.Persistences;
using SaleDetail.Infrastructure.Repository;
using SaleDetail.Infrastructure.Gateways;
using SaleDetail.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// Initialize DatabaseConnection singleton
DatabaseConnection.Initialize(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "SaleDetail.Api", Version = "v1" });
    
    // Configuración para JWT en Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Ingrese el token JWT en el formato: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// DI: domain/application wiring with UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IValidator<SaleDetail.Domain.Entities.SaleDetail>, SaleDetailValidator>();
builder.Services.AddScoped<ISaleDetailService, SaleDetailService>();
builder.Services.AddScoped<IMedicineGateway, MedicineGateway>();
builder.Services.AddScoped<ISaleGateway, SaleGateway>();
builder.Services.AddHttpContextAccessor();

// Messaging / outbox / Saga registrations
builder.Services.AddSingleton<IEventPublisher, RabbitPublisher>();
builder.Services.AddSingleton<RabbitMQConfiguration>();

// Register OutboxRepository for background processor as transient using a plain connection (no transaction)
builder.Services.AddTransient<IOutboxRepository>(sp =>
    new OutboxRepository(DatabaseConnection.Instance.GetConnection(), null));

// Hosted services for background processing
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<RabbitConsumer>();

builder.Services.AddHttpClient("MedicinesApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5143/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

builder.Services.AddHttpClient("SalesApi", client =>
{
    client.BaseAddress = new Uri("http://localhost:5000/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key no configurado");
var jwtIssuer = jwtSection["Issuer"];
var jwtAudience = jwtSection["Audience"];

var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.Zero
        };
        
        // DEBUG: Log authentication failures
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"🔴 AUTH FAILED: {context.Exception.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine($"✅ TOKEN VÁLIDO para: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 🐰 Inicializar RabbitMQ (crear cola y bindings)
using (var scope = app.Services.CreateScope())
{
    var rabbitConfig = scope.ServiceProvider.GetRequiredService<RabbitMQConfiguration>();
    rabbitConfig.Initialize();
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();   
app.UseAuthorization();    

app.MapControllers();

app.Run();
