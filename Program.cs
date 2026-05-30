using System.Text;
using System.Text.Json.Serialization;
using RealWorld.Data;
using RealWorld.Services;
using RealWorld.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RealWorld.Mappings;
using RealWorld.Models.Validators.Filters;
using RealWorld.Middleware;
using RealWorld.Settings;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

MapsterConfig.RegisterMappings();

// Add services to the container.

// Add JWT authentication setup
IConfigurationSection jwtSettings = builder.Configuration.GetSection("JwtSettings");
byte[] secretKey = Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(secretKey)
        };

        // Add "Token token" support
        options.Events = new()
        {
            OnMessageReceived = context =>
            {
                string? authHeader = context.Request.Headers.Authorization.FirstOrDefault();

                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Token "))
                {
                    context.Token = authHeader["Token ".Length..].Trim();
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.Configure<JwtSettingsOptions>(
      builder.Configuration.GetSection("JwtSettings"));

builder.Services.AddControllers(options =>
{
    options.Filters.Add<AutoValidationFilter>();
}).AddJsonOptions(options =>
    {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Add database support
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));
    
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IArticleService, ArticleService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IHttpContextService, HttpContextService>();
builder.Services.AddScoped<IFileService, FileService>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Learn more about configuring OpenAPI at 
builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
       policy.WithOrigins("http://localhost:4200") 
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
	{
		options.SwaggerEndpoint("/openapi/v1.json", "API v1");
	});
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();

app.UseExceptionHandler();
app.MapControllers();

app.Run();
