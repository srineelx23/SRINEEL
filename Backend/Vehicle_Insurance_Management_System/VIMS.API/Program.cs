using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using VIMS.API.Exceptions;
using VIMS.Application.Interfaces.Repositories;
using VIMS.Application.Interfaces.Services;
using VIMS.Application.Services;
using VIMS.Domain.Entities;
using VIMS.Infrastructure;
using VIMS.Infrastructure.Persistence;
using VIMS.Infrastructure.Repositories;
using VIMS.Infrastructure.Services;
using VIMS.Infrastructure.Services.RAG;
using VIMS.Application.Settings;
namespace VIMS.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.Configure<GroqSettings>(builder.Configuration.GetSection("Groq"));
            builder.Services.Configure<GeminiSettings>(builder.Configuration.GetSection("Gemini"));
            builder.Services.AddControllers();
            builder.Services.AddHttpClient();
            builder.Services.AddHttpContextAccessor();
            // Add services to the container.
            builder.Services.AddAuthorization();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckl
            builder.Services.AddScoped<IAuthRepository, AuthRepository>();
            builder.Services.AddDbContext<VehicleInsuranceContext>(options =>
                options.UseSqlServer(
                    builder.Configuration.GetConnectionString("DefaultConnection")
                ));
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IJwtService, JwtService>();
            builder.Services.AddScoped<IAdminRepository, AdminRepository>();
            builder.Services.AddScoped<IAdminService, AdminService>();
            builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IClaimsRepository, ClaimsRepository>();
            builder.Services.AddScoped<VIMS.Application.Interfaces.Services.IClaimsService, VIMS.Application.Services.ClaimsService>();
            builder.Services.AddScoped<IVehicleApplicationRepository, VehicleApplicationRepository>();
            builder.Services.AddScoped<IVehicleRepository, VehicleRepository>();
            builder.Services.AddScoped<IAgentService, AgentService>();
            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IPolicyPlanRepository, PolicyPlanRepository>();
            builder.Services.AddScoped<IPolicyRepository, PolicyRepository>();
            builder.Services.AddScoped<VIMS.Application.Interfaces.Repositories.IPaymentRepository, VIMS.Infrastructure.Repositories.PaymentRepository>();
            builder.Services.AddScoped<IPolicyPlanService, PolicyPlanService>();
            builder.Services.AddScoped<IPricingService, PricingService>();
            builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<IPolicyTransferRepository, PolicyTransferRepository>();
            builder.Services.AddScoped<IFileStorageService, FileStorageService>();
            builder.Services.AddScoped<IInvoiceService, InvoiceService>();
            builder.Services.AddScoped<IGarageRepository, GarageRepository>();
            builder.Services.AddScoped<IGarageService, GarageService>();
            builder.Services.AddScoped<IOcrService, OcrService>();
            builder.Services.AddScoped<IGroqService, GroqService>();
            builder.Services.AddScoped<IVectorSearchService, CosineVectorSearchService>();
            builder.Services.AddScoped<IChatbotService, ChatbotService>();
            builder.Services.AddScoped<IHybridRuleEngineService, HybridRuleEngineService>();
            builder.Services.AddHttpClient<IEmbeddingService, EmbeddingService>();
            builder.Services.AddHttpClient<IGeminiService, GeminiService>();
            builder.Services.AddSingleton<IRAGService, RAGService>();
            builder.Services.AddScoped<ISafetyService, SafetyService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<VertexAgentService>();
            builder.Services.AddScoped<IVertexAgentService, VertexAgentService>();
            builder.Services.AddScoped<QueryExecutionService>();
            builder.Services.AddScoped<IQueryExecutionService, QueryExecutionService>();

            builder.Services.AddSignalR();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<INotificationService, NotificationService>();
            builder.Services.AddScoped<IPushNotificationService, VIMS.API.Hubs.PushNotificationService>();
            builder.Services.AddHostedService<VIMS.API.Services.PolicyExpirationWorker>();




            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
            builder.Services.AddProblemDetails();
            builder.Services.AddEndpointsApiExplorer();

            var jwtSettings = builder.Configuration.GetSection("Jwt");
            var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // dev only
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,

                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };
            });


            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter JWT token like this: Bearer {your token}"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            var app = builder.Build();
            app.UseCors(policy => 
                policy.WithOrigins("http://localhost:4200")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseStaticFiles();
            app.UseHttpsRedirection();
            app.UseAuthentication();
            app.UseExceptionHandler();
            app.UseAuthorization();

            var summaries = new[]
            {
                "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
            };

            //app.MapGet("/weatherforecast", (HttpContext httpContext) =>
            //{
            //    var forecast = Enumerable.Range(1, 5).Select(index =>
            //        new WeatherForecast
            //        {
            //            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            //            TemperatureC = Random.Shared.Next(-20, 55),
            //            Summary = summaries[Random.Shared.Next(summaries.Length)]
            //        })
            //        .ToArray();
            //    return forecast;
            //})
            //.WithName("GetWeatherForecast")
            //.WithOpenApi();
            app.MapControllers();
            app.MapHub<VIMS.API.Hubs.NotificationHub>("/notificationHub");

            app.Run();
        }
    }
}
