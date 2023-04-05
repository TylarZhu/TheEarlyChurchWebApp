using Infrastructure.MongoDBSetUp;
using Infrastructure.DBService;
using Domain.Interfaces;
using WebAPI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.


builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();

builder.Services.Configure<GameHistoryDBSettings>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<RankingBoradDBSettings>(builder.Configuration.GetSection("Database"));

builder.Services.AddScoped<IGameHistoryService, GameHistoryService>();
builder.Services.AddScoped<IRankingBoardService, RankingBoardService>();
builder.Services.AddScoped<PlayerGroupsHubBase, PlayerGroupsHub>();

string policyName = "defaultCorsPolicy";

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
    options.AddPolicy(policyName, builder => {
        builder.WithOrigins("https://localhost:4200")
            .WithMethods("GET", "POST")
            .AllowAnyHeader()
            .AllowCredentials();
    })
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{

}

app.UseCors(policyName);

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<PlayerGroupsHubBase>("/PlayerGroupsHub");

app.Run();
