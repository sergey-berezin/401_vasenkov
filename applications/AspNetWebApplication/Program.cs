using ElectronNET.API;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;
using System.Threading;
using Models;
using Services;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ImageInfoContext>(options => options.UseSqlite("Data Source=images.db"));
builder.Services.AddSingleton<IVectorizationTaskManager>(new VectorizationTaskManager());
builder.Services.AddElectron();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseStaticFiles();

app.UseRouting();

Task.Run(async () => await Electron.WindowManager.CreateWindowAsync());

app.MapControllers();

app.Run();
