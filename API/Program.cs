using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics; // 添加这个命名空间
using Persistent;
using Application.Activities.Queries;
using Application.Core;
using MediatR;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 获取连接字符串并添加回退
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=reactivities.db"; // 开发环境回退

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlite(connectionString);
    
    // 禁用迁移警告 - 修复后的代码
    options.ConfigureWarnings(warnings => 
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddMediatR(x => x.RegisterServicesFromAssemblyContaining<GetActivityList.Handler>());
builder.Services.AddAutoMapper(typeof(MappingProfiles));
builder.Services.AddHttpClient();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000") // Your React app URLs
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ================ 新增：添加根路径路由 ================
// 添加根路径欢迎信息
app.MapGet("/", () => 
{
    return Results.Ok(new 
    { 
        message = "ServiceNow Ticket Automation API", 
        status = "Running",
        timestamp = DateTime.UtcNow,
        documentation = "Use /api/ endpoints to access the API",
        healthCheck = "/health"
    });
});

// 添加健康检查端点
app.MapGet("/health", () => 
{
    return Results.Ok(new 
    { 
        status = "Healthy",
        service = "ServiceNow Ticket Automation API",
        timestamp = DateTime.UtcNow
    });
});

// 添加API信息端点
app.MapGet("/api/info", () =>
{
    return Results.Ok(new
    {
        name = "ServiceNow Ticket Automation API",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName,
        endpoints = new[]
        {
            "/api/activities",
            "/api/activities/{id}",
            "/health"
        }
    });
});
// ================ 结束新增 ================

// Configure the HTTP request pipeline.
app.UseCors("AllowReactApp");
app.UseRouting();
app.MapControllers();  // 原来的控制器映射

using var scope = app.Services.CreateScope();
var services = scope.ServiceProvider;

try
{
    var context = services.GetRequiredService<AppDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Starting database initialization...");
    
    // 改进：尝试迁移，失败则使用 EnsureCreated
    try
    {
        // 检查是否有待处理的迁移
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation($"Applying {pendingMigrations.Count()} pending migrations...");
            await context.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            logger.LogInformation("No pending migrations.");
            // 确保数据库被创建
            await context.Database.EnsureCreatedAsync();
        }
    }
    catch (Exception migrateEx)
    {
        logger.LogWarning(migrateEx, "Migration failed, falling back to EnsureCreated...");
        // 回退：确保数据库被创建
        await context.Database.EnsureCreatedAsync();
        logger.LogInformation("Database created using EnsureCreated.");
    }
    
    // 播种数据
    await DbInitializer.SeedData(context);
    logger.LogInformation("Database initialization completed successfully.");
}
catch (Exception ex)
{
    var logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "An error occurred during database initialization.");
    // 不重新抛出 - 让应用继续运行
}

app.Run();