# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy everything
COPY . .

# Clean and restore
RUN dotnet restore "ServiceNowTicketAutomation_Net.sln"

# 安装 EF 工具
RUN dotnet tool install --global dotnet-ef --version 9.0.10
ENV PATH="$PATH:/root/.dotnet/tools"

# Build
RUN dotnet build "ServiceNowTicketAutomation_Net.sln" -c Release --no-restore

# 创建迁移
RUN dotnet ef migrations add InitialCreate -p Persistent -s API --verbose

# 应用迁移到 SQLite 数据库文件
RUN mkdir -p /app/data && \
    export ConnectionStrings__DefaultConnection="Data Source=/app/data/database.db" && \
    dotnet ef database update -p Persistent -s API --verbose

# Publish
RUN dotnet publish "API/API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# 创建数据目录
RUN mkdir -p /app/data

COPY --from=build /app/publish .

# 检查并复制数据库文件（如果有）
RUN if [ -f /app/data/database.db ]; then \
        echo "Copying database from build stage..."; \
        cp /app/data/database.db .; \
    else \
        echo "No database file from build stage, will be created at runtime"; \
    fi

ENTRYPOINT ["dotnet", "API.dll"]