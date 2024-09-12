using opcuaTest1.Client.Pages;
using opcuaTest1.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddSingleton<OpcUaClientService>();


var app = builder.Build();

//using (var scope = app.Services.CreateScope())
//{
//    var opcUaClientService = scope.ServiceProvider.GetRequiredService<OpcUaClientService>();

//    await opcUaClientService.StartAsync();

//    var robotPoseX = opcUaClientService.GetRobotPoseX();

//    Console.WriteLine($"RobotPoseX: {robotPoseX}");

//    var robotPoseY = opcUaClientService.GetRobotPoseY();
//    Console.WriteLine($"RobotPoseY: {robotPoseY}");

//    var robotPoseYaw = opcUaClientService.GetRobotPoseYaw();
//    Console.WriteLine($"RobotPoseYaw: {robotPoseYaw}");
//}


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(opcuaTest1.Client._Imports).Assembly);

app.Run();
